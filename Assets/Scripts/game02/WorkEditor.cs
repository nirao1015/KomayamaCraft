using System;
using System.Collections;
using Game02;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorkEditor : MonoBehaviour, IWorkplaceTarget, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static event Action WorkCompleted;

    private enum RememberedEditorKind
    {
        None = 0,
        MailChara = 1,
        Editor = 2
    }

    [Header("Initial State")]
    [SerializeField] private bool startDormant;

    [Header("Visuals")]
    [SerializeField] private Image dropRaycastArea;
    [SerializeField] private Image invalidView;
    [SerializeField] private Image acceptedItemView;
    [SerializeField] private AcceptedItemViewBounceController acceptedItemViewBounce;
    [SerializeField] private DirectionView01Controller directionView01;
    [SerializeField] private DirectionView02Controller directionView02;
    [SerializeField] private DirectionView03Controller directionView03;
    [SerializeField] private DirectionView04Controller directionView04;

    [Header("Work Remaining Time UI")]
    [SerializeField] private Image imageTimeB;
    [SerializeField] private Image imageTimeF;

    private static Sprite cachedUiWhiteSprite;

    private bool isDormant;
    private bool isWorking;
    private bool hasEditorAssigned;

    private RememberedEditorKind rememberedEditorKind;
    private string rememberedEditorDisplayName;
    private Sprite rememberedEditorSprite;
    [Header("Edit Work Params")]
    [SerializeField] private float baseEditSeconds = 120f;
    [SerializeField] private float phaseMovieGainEarly = 0.90f;
    [SerializeField] private float phaseMovieGainMid = 1.10f;
    [SerializeField] private float phaseMovieGainLate = 0.92f;
    [SerializeField] private float phaseEarlyEndMinutes = 20f;
    [SerializeField] private float phaseMidEndMinutes = 45f;

    [Header("Auto Search (Ed51)")]
    [SerializeField] private float uneditedItemSearchSeconds = 5f;
    [SerializeField] private float searchAcquireMoveSeconds = 0.5f;
    [SerializeField] private bool enableAutoSearchDebugLog;

    private float rememberedEditorEditSpeed = 1f;
    private float rememberedEditorPopularityMultiplier = 1f;
    private long rememberedEditorBuzzBaseValue = 2L;
    private long rememberedStreamPopularity;
    private string rememberedStreamGenre = ItemStreamGenreCatalog.DefaultGenre;

    private float elapsedWorkSeconds;
    private float workVisualElapsedSeconds;
    private float currentSessionRequiredWorkSeconds;
    private ItemSpawnController cachedSpawnController;

    private Transform imageTimeFOriginalParent;
    private int imageTimeFOriginalSiblingIndex;
    private bool imageTimeFReparentedUnderBackground;

    private DraggableItemController extractingItem;
    private float autoSearchAccumulator;
    private bool wasAutoSearchRunnableLastFrame;
    private DraggableItemController autoReservedStreamItem;
    private Coroutine autoCarryRoutine;
    private int autoSearchRequesterId;
    public bool IsDormant => isDormant;

    /// <summary>編集者アイテムが配置済み（オートプレイ等の判定用）。</summary>
    public bool HasEditorAssigned => hasEditorAssigned;

    public static void EnsureSceneWorkEditorsExist()
    {
        if (HasSceneWorkEditor("WorkEditor_1") && HasSceneWorkEditor("WorkEditor_2"))
        {
            return;
        }

        EnsureOneSceneWorkEditor("UnitCanvas/WorkEditor_1", false);
        EnsureOneSceneWorkEditor("UnitCanvas/WorkEditor_2", true);
        // シーン階層差異（親名変更など）でも確実に拾えるように名前フォールバックも行う。
        EnsureOneSceneWorkEditor("WorkEditor_1", false);
        EnsureOneSceneWorkEditor("WorkEditor_2", true);
    }

    private static bool HasSceneWorkEditor(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
        for (int i = 0; i < editors.Length; i++)
        {
            WorkEditor editor = editors[i];
            if (editor != null && editor.name == objectName)
            {
                return true;
            }
        }

        return false;
    }

    public void UnlockSecondSlotIfPurchasedEd01()
    {
        SetAvailable(true);
    }

    private static void EnsureOneSceneWorkEditor(string objectPath, bool defaultDormant)
    {
        GameObject go = GameObject.Find(objectPath);
        if (go == null)
        {
            return;
        }

        WorkEditor existing = go.GetComponent<WorkEditor>();
        if (existing != null)
        {
            return;
        }

        WorkEditor added = go.AddComponent<WorkEditor>();
        if (added != null)
        {
            added.startDormant = defaultDormant;
        }
    }

    private void Awake()
    {
        // 仕様固定:
        // - WorkEditor_1 は常時有効（未配置開始）
        // - WorkEditor_2 は Ed01 購入まで休眠
        // シーン側シリアライズ値や一時的な値ズレがあってもここで初期状態を補正する。
        if (name == "WorkEditor_1")
        {
            startDormant = false;
        }
        else if (name == "WorkEditor_2")
        {
            startDormant = true;
        }

        isDormant = startDormant;
        EnsureDropRaycastArea();
        ResolveReferencesIfNeeded();
        RefreshInvalidView();
        SetWorkTimeImagesActive(false);
        ApplyAcceptedEditorVisual();
        SetDirectionViewsActive(false);

        cachedSpawnController = FindObjectOfType<ItemSpawnController>(true);
        autoSearchRequesterId = GetInstanceID();
    }

    private void Update()
    {
        UpdateWorkRemainingTimeBarVisual();
        UpdateAutoSearch();
    }

    private void OnDisable()
    {
        CancelAutoSearchState();
    }

    public bool CanAcceptItem(DraggableItemController item)
    {
        if (isDormant || item == null || IsBlockedByPauseOrFatal())
        {
            return false;
        }

        if (isWorking)
        {
            return false;
        }

        if (!hasEditorAssigned)
        {
            return IsEditorItem(item);
        }

        return item.ItemType == ItemType.ItemStream;
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return;
        }

        if (!hasEditorAssigned)
        {
            AcceptEditor(item);
            return;
        }

        AcceptStreamAndStartWork(item);
    }

    public void OnGameManagerWorkTick(float deltaSeconds)
    {
        if (!isWorking || deltaSeconds <= 0f || isDormant)
        {
            return;
        }

        if (Game02.GameManager.Instance != null && Game02.GameManager.Instance.ShouldSuppressPlayerInteractions)
        {
            return;
        }

        elapsedWorkSeconds += deltaSeconds;
        workVisualElapsedSeconds = Mathf.Max(workVisualElapsedSeconds, elapsedWorkSeconds);
        if (elapsedWorkSeconds >= GetRequiredWorkSeconds())
        {
            EndWorkSessionAndSpawnMovie();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanExtractEditorByDrag())
        {
            return;
        }

        if (!TrySpawnRememberedEditorForExtraction(eventData, out DraggableItemController spawned))
        {
            return;
        }

        extractingItem = spawned;
        ClearRememberedEditor();
        extractingItem.BeginDragFromExternal(eventData);
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorExtractGrab);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (extractingItem == null)
        {
            return;
        }

        extractingItem.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (extractingItem == null)
        {
            return;
        }

        DraggableItemController current = extractingItem;
        extractingItem = null;
        ReparentExtractedItemBackToItemCanvasIfNeeded(current);
        if (eventData != null)
        {
            // WorkEditor 用 Canvas から ItemCanvas へ戻した直後に、同じスクリーン座標へ再スナップして
            // 取り出し直後の見た目位置ずれ（カーソルとアイテムの乖離）を防ぐ。
            current.TrySnapAnchoredPositionToScreenPoint(eventData.position);
            current.RefreshBaseStateFromCurrentTransform();
        }

        current.OnEndDrag(eventData);
    }

    private bool CanExtractEditorByDrag()
    {
        if (isDormant || isWorking || !hasEditorAssigned)
        {
            return false;
        }

        return !IsBlockedByPauseOrFatal();
    }

    private bool TrySpawnRememberedEditorForExtraction(PointerEventData eventData, out DraggableItemController spawned)
    {
        spawned = null;
        cachedSpawnController = cachedSpawnController != null
            ? cachedSpawnController
            : FindObjectOfType<ItemSpawnController>(true);

        if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
        {
            return false;
        }

        RectTransform itemCanvasRect = cachedSpawnController.ItemCanvas;

        ItemType spawnType = rememberedEditorKind == RememberedEditorKind.MailChara
            ? ItemType.ItemMailChara
            : ItemType.ItemEditor01;

        // 生成直後の Reflow は skip。座標は Awake 後に Draggable と同一の ScreenToCanvas でスナップする。
        if (!cachedSpawnController.TrySpawnItem(spawnType, Vector2.zero, false, out spawned, null, true))
        {
            return false;
        }

        if (spawned == null)
        {
            return false;
        }

        if (rememberedEditorSprite != null)
        {
            spawned.ApplyDisplaySprite(rememberedEditorSprite);
        }

        // 取り出し直後は「同じ WorkEditor への再投入」だけ禁止し、
        // WorkStreaming など他仕事場への直投入は許可する。
        spawned.SuppressNextWorkplaceDropFor(this);

        if (!string.IsNullOrEmpty(rememberedEditorDisplayName))
        {
            spawned.name = rememberedEditorDisplayName;
        }

        if (!spawned.TrySnapAnchoredPositionToScreenPoint(eventData.position))
        {
            RectTransform rt = spawned.GetComponent<RectTransform>();
            if (rt != null && TryGetSpawnPointCenterSelfInItemCanvas(itemCanvasRect, out Vector2 fallback))
            {
                rt.anchoredPosition = fallback;
                spawned.RefreshBaseStateFromCurrentTransform();
            }
        }

        // WorkEditor は UnitCanvas、生成アイテムは ItemCanvas 子のため、同一スクリーン座標でも帯側にずれて見える。
        // ドラッグ中だけ WorkEditor と同じルート Canvas へ付け替え、スクリーン／ローカル変換を揃える。
        ReparentExtractedItemToWorkEditorCanvas(spawned);

        return true;
    }

    private void ReparentExtractedItemToWorkEditorCanvas(DraggableItemController spawned)
    {
        WorkplaceDragExtractionUtility.ReparentExtractedItemToWorkCanvas(spawned, transform);
    }

    private void ReparentExtractedItemBackToItemCanvasIfNeeded(DraggableItemController item)
    {
        WorkplaceDragExtractionUtility.ReparentExtractedItemBackToItemCanvas(item, ref cachedSpawnController);
    }

    private RectTransform ResolveItemCanvasRect()
    {
        return WorkplaceDragExtractionUtility.ResolveItemCanvasRect(ref cachedSpawnController);
    }

    private void AcceptEditor(DraggableItemController item)
    {
        if (item == null || !item.TryGetDisplaySprite(out Sprite acceptedSprite))
        {
            return;
        }

        rememberedEditorKind = item.ItemType == ItemType.ItemMailChara
            ? RememberedEditorKind.MailChara
            : RememberedEditorKind.Editor;
        rememberedEditorDisplayName = item.name;
        rememberedEditorSprite = acceptedSprite;

        rememberedEditorEditSpeed = Mathf.Max(0f, item.EditorEditSpeed);
        rememberedEditorPopularityMultiplier = Mathf.Max(0f, item.EditorPopularityMultiplier);
        rememberedEditorBuzzBaseValue = System.Math.Max(0L, item.EditorBuzzBaseValue);
        rememberedStreamPopularity = 0L;
        rememberedStreamGenre = ItemStreamGenreCatalog.DefaultGenre;

        hasEditorAssigned = true;
        Game02.Game02MsgManager.TryGet()?.NotifyWorkEditorPrimaryAcceptedEditorItem(Game02.GameManager.Instance, transform, item.ItemType);
        PlayAcceptedSeIfConfigured();
        ApplyAcceptedEditorVisual();
        SetDirectionViewsActive(false);
        PlayIdleAssignedEditorEffect();
    }

    private void AcceptStreamAndStartWork(DraggableItemController item)
    {
        if (item != null)
        {
            item.SetInputLocked(false);
            Game02.ItemStreamReservationRegistry.Release(item, autoSearchRequesterId);
            if (autoReservedStreamItem == item)
            {
                autoReservedStreamItem = null;
            }
        }

        rememberedStreamPopularity = ReadStreamPopularity(item);
        rememberedStreamGenre = ReadStreamGenre(item);
        StopIdleAssignedEditorEffect();
        SetDirectionViewsActive(true);
        StartWorkSession();
    }

    private void StartWorkSession()
    {
        acceptedItemViewBounce?.ForceResetToBaseTransform();
        isWorking = true;
        elapsedWorkSeconds = 0f;
        workVisualElapsedSeconds = 0f;
        currentSessionRequiredWorkSeconds = ResolveWorkSecondsForNewSession();

        EnsureWorkTimeBarSpritesIfMissing();
        BeginWorkTimeFrontBarLayout();
        SetWorkTimeImagesActive(true);
    }

    private void EndWorkSessionAndSpawnMovie()
    {
        isWorking = false;
        elapsedWorkSeconds = 0f;
        workVisualElapsedSeconds = 0f;
        currentSessionRequiredWorkSeconds = 0f;
        acceptedItemViewBounce?.ForceResetToBaseTransform();
        RestoreWorkTimeFrontBarParentIfNeeded();
        SetWorkTimeImagesActive(false);
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorComplete);
        SpawnMovieFromCenter();
        rememberedStreamPopularity = 0L;
        rememberedStreamGenre = ItemStreamGenreCatalog.DefaultGenre;
        SetDirectionViewsActive(false);
        PlayIdleAssignedEditorEffect();
        WorkCompleted?.Invoke();
    }

    private void SpawnMovieFromCenter()
    {
        cachedSpawnController = cachedSpawnController != null
            ? cachedSpawnController
            : FindObjectOfType<ItemSpawnController>(true);
        if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
        {
            return;
        }

        if (!TryGetSpawnPointCenterSelfInItemCanvas(cachedSpawnController.ItemCanvas, out Vector2 spawnPoint))
        {
            return;
        }

        if (!cachedSpawnController.TrySpawnItem(ItemType.ItemMovie, spawnPoint, true, out DraggableItemController movie))
        {
            return;
        }

        if (movie == null)
        {
            return;
        }

        ItemMoviePower moviePower = movie.GetComponent<ItemMoviePower>();
        if (moviePower == null)
        {
            moviePower = movie.gameObject.AddComponent<ItemMoviePower>();
        }

        long finalPopularity = CalculateFinalPopularity();
        long buzzGainValue = CalculateBuzzGainValue();
        moviePower.InitializeMovieStats(finalPopularity, buzzGainValue, ResolveMovieNameFromStreamGenre());
        GetTrendUpgradeContributions(out int trendUpgradeBuzzValue, out int trendUpgradeBuzzFactor);
        moviePower.ApplyTrendUpgradeContribution(trendUpgradeBuzzValue, trendUpgradeBuzzFactor);
    }

    private long CalculateFinalPopularity()
    {
        double basePopularity = rememberedStreamPopularity + WorkEditorExt.GetActiveWorkEditorPopularityBaseBonus();
        double value = System.Math.Max(0d, basePopularity);
        value *= rememberedEditorPopularityMultiplier;
        value *= GetUpgradeEditPopularityMultiplier();
        value *= ResolvePhaseMovieGain();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0L;
        }

        long rounded;
        if (value >= long.MaxValue)
        {
            rounded = long.MaxValue;
        }
        else if (value <= long.MinValue)
        {
            rounded = long.MinValue;
        }
        else
        {
            rounded = System.Convert.ToInt64(System.Math.Round(value));
        }

        return rounded < 0L ? 0L : rounded;
    }

    private float ResolvePhaseMovieGain()
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null)
        {
            return Mathf.Max(0f, phaseMovieGainMid);
        }

        float elapsedMinutes = Mathf.Max(0f, gm.GameplayElapsedSeconds) / 60f;
        float earlyEnd = Mathf.Max(0f, phaseEarlyEndMinutes);
        float midEnd = Mathf.Max(earlyEnd, phaseMidEndMinutes);
        if (elapsedMinutes < earlyEnd)
        {
            return Mathf.Max(0f, phaseMovieGainEarly);
        }

        if (elapsedMinutes < midEnd)
        {
            return Mathf.Max(0f, phaseMovieGainMid);
        }

        return Mathf.Max(0f, phaseMovieGainLate);
    }

    private long CalculateBuzzGainValue()
    {
        float randomBuzzFactor = 1f;
        if (Game02.GameManager.Instance != null)
        {
            randomBuzzFactor = Game02.GameManager.Instance.GetRandomBuzzCorrectionFactor();
        }

        double value = rememberedEditorBuzzBaseValue;
        value *= GetUpgradeEditBuzzMultiplier();
        value *= randomBuzzFactor;
        value += GetUpgradeEditBuzzAdditive();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0L;
        }

        long rounded;
        if (value >= long.MaxValue)
        {
            rounded = long.MaxValue;
        }
        else if (value <= long.MinValue)
        {
            rounded = long.MinValue;
        }
        else
        {
            rounded = System.Convert.ToInt64(System.Math.Round(value));
        }

        return rounded < 0L ? 0L : rounded;
    }

    private float GetRequiredWorkSeconds()
    {
        if (isWorking && currentSessionRequiredWorkSeconds > 0f)
        {
            return currentSessionRequiredWorkSeconds;
        }

        float multiplied = ResolveEffectiveBaseEditSeconds() * rememberedEditorEditSpeed * GetUpgradeEditDurationMultiplier();
        return Mathf.Max(1f, Mathf.Floor(multiplied));
    }

    private float ResolveWorkSecondsForNewSession()
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        int sessionIndex = gm != null ? gm.RegisterEditWorkSessionStarted() : 1;

        if (sessionIndex == 1 && gm != null && gm.TryConsumeFirstEditWorkSeconds(out float fixedSeconds))
        {
            return Mathf.Max(1f, Mathf.Floor(fixedSeconds));
        }

        float multiplied = ResolveEffectiveBaseEditSeconds() * rememberedEditorEditSpeed * GetUpgradeEditDurationMultiplier();
        if (Game02EarlyEditDuration.TryGetSessionDurationMultiplier(sessionIndex, out float earlyDurationMultiplier))
        {
            multiplied *= earlyDurationMultiplier;
        }

        return Mathf.Max(1f, Mathf.Floor(multiplied));
    }

    private float ResolveEffectiveBaseEditSeconds()
    {
        float effectiveBase = baseEditSeconds + WorkEditorExt.GetActiveWorkEditorBaseEditSecondsOffset();
        return Mathf.Max(1f, effectiveBase);
    }

    private float GetUpgradeEditDurationMultiplier()
    {
        if (Game02.UpgradesManager.Instance == null)
        {
            return 1f;
        }

        return Mathf.Max(0f, Game02.UpgradesManager.Instance.GetEditTimeMultiplier());
    }

    private float GetUpgradeEditPopularityMultiplier()
    {
        if (Game02.UpgradesManager.Instance == null)
        {
            return 1f;
        }

        return Mathf.Max(0f, Game02.UpgradesManager.Instance.GetEditPopularityMultiplier());
    }

    private float GetUpgradeEditBuzzMultiplier()
    {
        if (Game02.UpgradesManager.Instance == null)
        {
            return 1f;
        }

        return Mathf.Max(0f, Game02.UpgradesManager.Instance.GetEditBuzzGainMultiplier());
    }

    private long GetUpgradeEditBuzzAdditive()
    {
        if (Game02.UpgradesManager.Instance == null)
        {
            return 0L;
        }

        return System.Math.Max(0L, Game02.UpgradesManager.Instance.GetEditBuzzGainAdditive());
    }

    private void GetTrendUpgradeContributions(out int upgradeBuzzValue, out int upgradeBuzzFactor)
    {
        upgradeBuzzValue = 0;
        upgradeBuzzFactor = 0;
        if (Game02.UpgradesManager.Instance == null)
        {
            return;
        }

        upgradeBuzzValue = Mathf.Max(0, Game02.UpgradesManager.Instance.GetTrendUpgradeBuzzValue());
        upgradeBuzzFactor = Mathf.Max(0, Game02.UpgradesManager.Instance.GetTrendUpgradeBuzzFactor());
    }

    private static long ReadStreamPopularity(DraggableItemController item)
    {
        if (item == null)
        {
            return 0L;
        }

        ItemStreamSpawnPopularity streamPopularity = item.GetComponent<ItemStreamSpawnPopularity>();
        if (streamPopularity == null)
        {
            return 0L;
        }

        return streamPopularity.GetSpawnPopularity();
    }

    private static string ReadStreamGenre(DraggableItemController item)
    {
        if (item == null)
        {
            return ItemStreamGenreCatalog.DefaultGenre;
        }

        ItemStreamSpawnPopularity streamPopularity = item.GetComponent<ItemStreamSpawnPopularity>();
        if (streamPopularity == null)
        {
            return ItemStreamGenreCatalog.DefaultGenre;
        }

        string genre = streamPopularity.GetStreamGenre();
        return string.IsNullOrEmpty(genre) ? ItemStreamGenreCatalog.DefaultGenre : genre;
    }

    private string ResolveMovieNameFromStreamGenre()
    {
        MovieNameCatalog catalog = FindObjectOfType<MovieNameCatalog>(true);
        if (catalog == null)
        {
            return MovieNameCatalog.DefaultMovieName;
        }

        return catalog.PickRandomMovieName(rememberedStreamGenre);
    }

    private bool IsEditorItem(DraggableItemController item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.ItemType == ItemType.ItemMailChara)
        {
            return true;
        }

        if (item.ItemType == ItemType.ItemEditor01)
        {
            return true;
        }

        return item.name.StartsWith("ItemEditor", System.StringComparison.Ordinal);
    }

    private bool IsBlockedByPauseOrFatal()
    {
        if (Game02.GameManager.Instance == null)
        {
            return false;
        }

        return Game02.GameManager.Instance.ShouldSuppressPlayerInteractions || Game02.GameManager.Instance.HasFatalError;
    }

    private void UpdateAutoSearch()
    {
        bool runnable = CanRunAutoSearch();
        if (!runnable)
        {
            if (wasAutoSearchRunnableLastFrame)
            {
                autoSearchAccumulator = 0f;
            }

            wasAutoSearchRunnableLastFrame = false;
            if (autoCarryRoutine == null)
            {
                ReleaseReservedStreamIfAny();
            }

            return;
        }

        if (!wasAutoSearchRunnableLastFrame)
        {
            autoSearchAccumulator = 0f;
        }

        wasAutoSearchRunnableLastFrame = true;
        if (autoCarryRoutine != null || autoReservedStreamItem != null)
        {
            return;
        }

        autoSearchAccumulator += Game02.GameManager.GameplayDelta;
        if (autoSearchAccumulator < Mathf.Max(0.01f, uneditedItemSearchSeconds))
        {
            return;
        }

        autoSearchAccumulator = 0f;
        TryAcquireStreamForAutoSearch();
    }

    private bool CanRunAutoSearch()
    {
        if (isDormant || isWorking || !hasEditorAssigned || IsBlockedByPauseOrFatal())
        {
            return false;
        }

        return Game02.UpgradesManager.Instance != null && Game02.UpgradesManager.Instance.IsWorkUpgradeEd51Purchased();
    }

    private void TryAcquireStreamForAutoSearch()
    {
        DraggableItemController target = FindHighestPopularityAvailableStreamItem();
        if (target == null)
        {
            return;
        }

        if (!Game02.ItemStreamReservationRegistry.TryReserve(target, autoSearchRequesterId))
        {
            return;
        }

        target.SetInputLocked(true);
        autoReservedStreamItem = target;
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorAutoSearchReserved);
        autoCarryRoutine = StartCoroutine(CoAutoCarryReservedStream(target));
        LogAutoSearch($"reserve:{target.name}");
    }

    private DraggableItemController FindHighestPopularityAvailableStreamItem()
    {
        DraggableItemController[] all = FindObjectsOfType<DraggableItemController>(true);
        DraggableItemController best = null;
        long bestPopularity = long.MinValue;
        for (int i = 0; i < all.Length; i++)
        {
            DraggableItemController candidate = all[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.ItemType != ItemType.ItemStream)
            {
                continue;
            }

            if (candidate.IsDragging || candidate.IsBounceReturnActive || candidate.SuppressPlacementReflowForSpawnBounce)
            {
                continue;
            }

            if (Game02.ItemStreamReservationRegistry.IsReservedByOther(candidate, autoSearchRequesterId))
            {
                continue;
            }

            ItemStreamSpawnPopularity popularityComponent = candidate.GetComponent<ItemStreamSpawnPopularity>();
            long popularity = popularityComponent != null ? popularityComponent.GetSpawnPopularity() : long.MinValue;
            if (best == null || popularity > bestPopularity)
            {
                best = candidate;
                bestPopularity = popularity;
            }
        }

        return best;
    }

    private IEnumerator CoAutoCarryReservedStream(DraggableItemController streamItem)
    {
        if (streamItem == null)
        {
            autoCarryRoutine = null;
            autoReservedStreamItem = null;
            yield break;
        }

        RectTransform streamRect = streamItem.GetComponent<RectTransform>();
        RectTransform targetCanvas = ResolveStreamItemCanvas(streamItem);
        if (streamRect == null || targetCanvas == null)
        {
            streamItem.SetInputLocked(false);
            Game02.ItemStreamReservationRegistry.Release(streamItem, autoSearchRequesterId);
            autoCarryRoutine = null;
            autoReservedStreamItem = null;
            yield break;
        }

        if (!TryGetSpawnPointCenterSelfInItemCanvas(targetCanvas, out Vector2 targetPoint))
        {
            streamItem.SetInputLocked(false);
            Game02.ItemStreamReservationRegistry.Release(streamItem, autoSearchRequesterId);
            autoCarryRoutine = null;
            autoReservedStreamItem = null;
            yield break;
        }

        Vector2 startPoint = streamRect.anchoredPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, searchAcquireMoveSeconds);
        while (elapsed < duration)
        {
            if (streamItem == null)
            {
                autoCarryRoutine = null;
                autoReservedStreamItem = null;
                yield break;
            }

            if (IsBlockedByPauseOrFatal())
            {
                yield return null;
                continue;
            }

            elapsed += Game02.GameManager.GameplayDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            streamRect.anchoredPosition = Vector2.LerpUnclamped(startPoint, targetPoint, t);
            yield return null;
        }

        if (streamItem != null)
        {
            streamRect.anchoredPosition = targetPoint;
            if (CanAcceptItem(streamItem))
            {
                if (Game02.UpgradesManager.Instance != null && Game02.UpgradesManager.Instance.IsWorkUpgradeEd51Purchased())
                {
                    Game02.Game02MsgManager.TryGet()?.NotifyFirstEd51AutomationUsed(Game02.GameManager.Instance);
                }

                Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorAutoCarryDropAccepted);
                OnItemDropped(streamItem);
                LogAutoSearch($"drop:{streamItem.name}");
                Destroy(streamItem.gameObject);
            }
            else
            {
                streamItem.SetInputLocked(false);
                Game02.ItemStreamReservationRegistry.Release(streamItem, autoSearchRequesterId);
                LogAutoSearch($"drop-skip:{streamItem.name}");
            }
        }

        autoReservedStreamItem = null;
        autoCarryRoutine = null;
    }

    private RectTransform ResolveStreamItemCanvas(DraggableItemController item)
    {
        if (item == null)
        {
            return null;
        }

        Canvas canvas = item.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            return canvas.GetComponent<RectTransform>();
        }

        cachedSpawnController = cachedSpawnController != null
            ? cachedSpawnController
            : FindObjectOfType<ItemSpawnController>(true);
        return cachedSpawnController != null ? cachedSpawnController.ItemCanvas : null;
    }

    private void ReleaseReservedStreamIfAny()
    {
        if (autoReservedStreamItem == null)
        {
            return;
        }

        autoReservedStreamItem.SetInputLocked(false);
        Game02.ItemStreamReservationRegistry.Release(autoReservedStreamItem, autoSearchRequesterId);
        autoReservedStreamItem = null;
    }

    private void CancelAutoSearchState()
    {
        if (autoCarryRoutine != null)
        {
            StopCoroutine(autoCarryRoutine);
            autoCarryRoutine = null;
        }

        ReleaseReservedStreamIfAny();
        autoSearchAccumulator = 0f;
        wasAutoSearchRunnableLastFrame = false;
    }

    private void LogAutoSearch(string message)
    {
        if (!enableAutoSearchDebugLog)
        {
            return;
        }

        Debug.Log($"[WorkEditor][AutoSearch] {name} {message}");
    }

    private void ClearRememberedEditor()
    {
        rememberedEditorKind = RememberedEditorKind.None;
        rememberedEditorDisplayName = string.Empty;
        rememberedEditorSprite = null;
        rememberedEditorEditSpeed = 1f;
        rememberedEditorPopularityMultiplier = 1f;
        rememberedEditorBuzzBaseValue = 2L;
        rememberedStreamPopularity = 0L;
        rememberedStreamGenre = ItemStreamGenreCatalog.DefaultGenre;
        hasEditorAssigned = false;
        isWorking = false;
        elapsedWorkSeconds = 0f;
        workVisualElapsedSeconds = 0f;
        currentSessionRequiredWorkSeconds = 0f;
        acceptedItemViewBounce?.ForceResetToBaseTransform();
        RestoreWorkTimeFrontBarParentIfNeeded();
        SetWorkTimeImagesActive(false);
        ApplyAcceptedEditorVisual();
        SetDirectionViewsActive(false);
        StopIdleAssignedEditorEffect();
    }

    private void ApplyAcceptedEditorVisual()
    {
        if (acceptedItemView == null)
        {
            return;
        }

        acceptedItemView.sprite = hasEditorAssigned ? rememberedEditorSprite : null;
        acceptedItemView.enabled = hasEditorAssigned && rememberedEditorSprite != null;
        acceptedItemViewBounce?.SetCarrierActive(hasEditorAssigned);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (invalidView == null)
        {
            invalidView = FindComponentInDescendantsByName<Image>("InvalidView");
        }

        if (acceptedItemView == null)
        {
            acceptedItemView = FindComponentInDescendantsByName<Image>("AcceptedItemView");
        }

        if (acceptedItemViewBounce == null && acceptedItemView != null)
        {
            acceptedItemViewBounce = acceptedItemView.GetComponent<AcceptedItemViewBounceController>();
            if (acceptedItemViewBounce == null)
            {
                acceptedItemViewBounce = acceptedItemView.gameObject.AddComponent<AcceptedItemViewBounceController>();
            }
        }

        if (directionView01 == null)
        {
            directionView01 = FindOrAddComponentInDescendantsByName<DirectionView01Controller>("DirectionView01");
        }

        if (directionView02 == null)
        {
            directionView02 = FindOrAddComponentInDescendantsByName<DirectionView02Controller>("DirectionView02");
        }

        if (directionView03 == null)
        {
            directionView03 = FindOrAddComponentInDescendantsByName<DirectionView03Controller>("DirectionView03");
        }

        if (directionView04 == null)
        {
            directionView04 = FindOrAddComponentInDescendantsByName<DirectionView04Controller>("DirectionView04");
        }

        ResolveWorkTimeImagesIfNeeded();
    }


    private void PlayAcceptedSeIfConfigured()
    {
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorAccepted);
    }

    private void SetDormantVisual(bool dormant)
    {
        if (invalidView != null)
        {
            invalidView.gameObject.SetActive(dormant);
        }
    }

    public void SetAvailable(bool available)
    {
        isDormant = !available;
        startDormant = !available;
        RefreshInvalidView();
    }

    public void DisableInvalidViewByUpgrade()
    {
        SetAvailable(true);
    }

    // 再ロック仕様を導入する場合にここへ実処理を追加する。
    public void SetUnavailableReserved()
    {
    }

    public void RefreshInvalidView()
    {
        SetDormantVisual(isDormant);
        if (Game02DebugManager.ShouldLogInvalidViewRefresh())
        {
            Debug.Log($"[WorkEditor] InvalidView active={isDormant} available={!isDormant} object={name}");
        }
    }

    private void ResolveWorkTimeImagesIfNeeded()
    {
        if (imageTimeB == null)
        {
            imageTimeB = FindComponentInDescendantsByName<Image>("ImageTimeB");
        }

        if (imageTimeF == null)
        {
            imageTimeF = FindComponentInDescendantsByName<Image>("ImageTimeF");
        }
    }

    private static Sprite GetOrCreateUiWhiteSprite()
    {
        if (cachedUiWhiteSprite != null)
        {
            return cachedUiWhiteSprite;
        }

        Texture2D tex = Texture2D.whiteTexture;
        cachedUiWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return cachedUiWhiteSprite;
    }

    private void EnsureWorkTimeBarSpritesIfMissing()
    {
        if (imageTimeB != null && imageTimeB.sprite == null)
        {
            imageTimeB.sprite = GetOrCreateUiWhiteSprite();
            imageTimeB.type = Image.Type.Simple;
        }

        if (imageTimeF != null && imageTimeF.sprite == null)
        {
            imageTimeF.sprite = GetOrCreateUiWhiteSprite();
            imageTimeF.type = Image.Type.Simple;
        }
    }

    private void BeginWorkTimeFrontBarLayout()
    {
        if (imageTimeB == null || imageTimeF == null)
        {
            return;
        }

        RectTransform f = imageTimeF.rectTransform;
        if (!imageTimeFReparentedUnderBackground)
        {
            imageTimeFOriginalParent = f.parent;
            imageTimeFOriginalSiblingIndex = f.GetSiblingIndex();
            f.SetParent(imageTimeB.transform, false);
            imageTimeFReparentedUnderBackground = true;
        }

        f.anchorMin = Vector2.zero;
        f.anchorMax = Vector2.one;
        f.pivot = new Vector2(0.5f, 0.5f);
        f.offsetMin = Vector2.zero;
        f.offsetMax = Vector2.zero;
        f.localScale = Vector3.one;
        f.localRotation = Quaternion.identity;
        ApplyWorkTimeFrontBarRemainingVisual(1f);
    }

    private void RestoreWorkTimeFrontBarParentIfNeeded()
    {
        if (!imageTimeFReparentedUnderBackground || imageTimeF == null)
        {
            return;
        }

        RectTransform f = imageTimeF.rectTransform;
        if (imageTimeFOriginalParent != null)
        {
            f.SetParent(imageTimeFOriginalParent, false);
            int max = Mathf.Max(0, imageTimeFOriginalParent.childCount - 1);
            f.SetSiblingIndex(Mathf.Clamp(imageTimeFOriginalSiblingIndex, 0, max));
        }

        imageTimeFReparentedUnderBackground = false;
    }

    private void ApplyWorkTimeFrontBarRemainingVisual(float remaining01)
    {
        if (imageTimeF == null)
        {
            return;
        }

        float r = Mathf.Clamp01(remaining01);
        if (imageTimeFReparentedUnderBackground)
        {
            RectTransform f = imageTimeF.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(r, 1f);
            f.offsetMin = Vector2.zero;
            f.offsetMax = Vector2.zero;
        }
        else
        {
            imageTimeF.type = Image.Type.Filled;
            imageTimeF.fillMethod = Image.FillMethod.Horizontal;
            imageTimeF.fillOrigin = (int)Image.OriginHorizontal.Left;
            imageTimeF.fillAmount = r;
        }
    }

    private void SetWorkTimeImagesActive(bool active)
    {
        if (imageTimeB != null)
        {
            imageTimeB.gameObject.SetActive(active);
        }

        if (imageTimeF != null)
        {
            imageTimeF.gameObject.SetActive(active);
        }
    }

    private void UpdateWorkRemainingTimeBarVisual()
    {
        if (!isWorking || imageTimeF == null)
        {
            return;
        }

        float required = GetRequiredWorkSeconds();
        if (required <= 0f)
        {
            ApplyWorkTimeFrontBarRemainingVisual(0f);
            return;
        }

            bool paused = Game02.GameManager.Instance != null && Game02.GameManager.Instance.ShouldSuppressPlayerInteractions;
        if (!paused)
        {
            workVisualElapsedSeconds += Game02.GameManager.GameplayDelta;
            workVisualElapsedSeconds = Mathf.Max(workVisualElapsedSeconds, elapsedWorkSeconds);
        }

        float displayElapsed = Mathf.Min(workVisualElapsedSeconds, required);
        float remaining = Mathf.Clamp01(1f - displayElapsed / required);
        ApplyWorkTimeFrontBarRemainingVisual(remaining);
    }

    private bool TryGetSpawnPointCenterSelfInItemCanvas(RectTransform itemCanvasRect, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        RectTransform selfRect = transform as RectTransform;
        if (selfRect == null)
        {
            return false;
        }

        Vector3 worldCenter = selfRect.TransformPoint(selfRect.rect.center);
        Canvas canvas = itemCanvasRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(itemCanvasRect, screenPoint, cam, out anchoredPosition))
        {
            return true;
        }

        anchoredPosition = itemCanvasRect.InverseTransformPoint(worldCenter);
        return true;
    }

    private void SetDirectionViewsActive(bool active)
    {
        directionView01?.SetCarrierActive(active);
        directionView02?.SetCarrierActive(active);
        directionView03?.SetCarrierActive(active);
        directionView04?.SetCarrierActive(active);
    }

    private void PlayIdleAssignedEditorEffect()
    {
        // 後続で編集者配置済み専用演出を追加するためのフック。
    }

    private void StopIdleAssignedEditorEffect()
    {
        // 後続で編集者配置済み専用演出を追加するためのフック。
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            return null;
        }

        T component = child.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return child.gameObject.AddComponent<T>();
    }

    private T FindComponentInDescendantsByName<T>(string childName) where T : Component
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, System.StringComparison.Ordinal))
            {
                continue;
            }

            T component = t.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private T FindOrAddComponentInDescendantsByName<T>(string childName) where T : Component
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, System.StringComparison.Ordinal))
            {
                continue;
            }

            T component = t.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return t.gameObject.AddComponent<T>();
        }

        return null;
    }

    private void EnsureDropRaycastArea()
    {
        if (dropRaycastArea == null)
        {
            dropRaycastArea = GetComponent<Image>();
        }

        if (dropRaycastArea == null)
        {
            dropRaycastArea = gameObject.AddComponent<Image>();
        }

        if (dropRaycastArea == null)
        {
            return;
        }

        dropRaycastArea.enabled = true;
        dropRaycastArea.raycastTarget = true;
        Color c = dropRaycastArea.color;
        c.a = 0f;
        dropRaycastArea.color = c;
    }

    public Game02.WorkEditorState CaptureSaveState()
    {
        return new Game02.WorkEditorState
        {
            objectName = name,
            isDormant = isDormant,
            isWorking = isWorking,
            hasEditorAssigned = hasEditorAssigned,
            rememberedEditorKind = (int)rememberedEditorKind,
            rememberedEditorDisplayName = rememberedEditorDisplayName ?? string.Empty,
            rememberedEditorSpriteName = rememberedEditorSprite != null ? rememberedEditorSprite.name : string.Empty,
            rememberedEditorEditSpeed = rememberedEditorEditSpeed,
            rememberedEditorPopularityMultiplier = rememberedEditorPopularityMultiplier,
            rememberedEditorBuzzBaseValue = rememberedEditorBuzzBaseValue,
            rememberedStreamPopularity = rememberedStreamPopularity,
            rememberedStreamGenre = rememberedStreamGenre ?? ItemStreamGenreCatalog.DefaultGenre,
            elapsedWorkSeconds = elapsedWorkSeconds,
            workVisualElapsedSeconds = workVisualElapsedSeconds,
            currentSessionRequiredWorkSeconds = currentSessionRequiredWorkSeconds
        };
    }

    public void ApplySaveState(Game02.WorkEditorState state)
    {
        if (state == null)
        {
            return;
        }

        bool inferredHasEditorAssigned =
            state.hasEditorAssigned
            || state.rememberedEditorKind != (int)RememberedEditorKind.None
            || !string.IsNullOrEmpty(state.rememberedEditorDisplayName)
            || !string.IsNullOrEmpty(state.rememberedEditorSpriteName)
            || state.isWorking;

        // WorkEditor_1 は常時有効。ロード値で誤って休眠化されても復元しない。
        isDormant = name == "WorkEditor_1" ? false : state.isDormant;
        hasEditorAssigned = inferredHasEditorAssigned;
        isWorking = state.isWorking && hasEditorAssigned && !isDormant;
        rememberedEditorKind = (RememberedEditorKind)Mathf.Clamp(state.rememberedEditorKind, 0, 2);
        rememberedEditorDisplayName = state.rememberedEditorDisplayName ?? string.Empty;
        rememberedEditorSprite = Game02.Game02SpriteResolver.ResolveByName(state.rememberedEditorSpriteName);
        rememberedEditorEditSpeed = Mathf.Max(0f, state.rememberedEditorEditSpeed);
        rememberedEditorPopularityMultiplier = Mathf.Max(0f, state.rememberedEditorPopularityMultiplier);
        rememberedEditorBuzzBaseValue = Math.Max(0L, state.rememberedEditorBuzzBaseValue);
        rememberedStreamPopularity = Math.Max(0L, state.rememberedStreamPopularity);
        rememberedStreamGenre = string.IsNullOrEmpty(state.rememberedStreamGenre)
            ? ItemStreamGenreCatalog.DefaultGenre
            : state.rememberedStreamGenre;
        elapsedWorkSeconds = Mathf.Max(0f, state.elapsedWorkSeconds);
        workVisualElapsedSeconds = Mathf.Max(0f, state.workVisualElapsedSeconds);
        currentSessionRequiredWorkSeconds = Mathf.Max(0f, state.currentSessionRequiredWorkSeconds);
        if (isWorking && currentSessionRequiredWorkSeconds <= 0f)
        {
            float multiplied = ResolveEffectiveBaseEditSeconds() * rememberedEditorEditSpeed * GetUpgradeEditDurationMultiplier();
            currentSessionRequiredWorkSeconds = Mathf.Max(1f, Mathf.Floor(multiplied));
        }
        RefreshInvalidView();
        ApplyAcceptedEditorVisual();
        SetDirectionViewsActive(isWorking);
        SetWorkTimeImagesActive(isWorking);
        if (isWorking)
        {
            EnsureWorkTimeBarSpritesIfMissing();
            BeginWorkTimeFrontBarLayout();
        }
        else
        {
            RestoreWorkTimeFrontBarParentIfNeeded();
        }
    }
}
