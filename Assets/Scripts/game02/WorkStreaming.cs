using System;
using System.Collections;
using Game02;
using UnityEngine;
using UnityEngine.UI;

public class WorkStreaming : MonoBehaviour, IWorkplaceTarget
{
    public static event Action WorkCompleted;

    [Header("Accept State")]
    [SerializeField] private bool isAcceptingItems = true;
    [SerializeField] private bool isWorking;

    [Header("Accepted Item Type")]
    [SerializeField] private bool acceptItemMailChara = true;
    [SerializeField] private Image invalidView;

    [Header("Work Rule")]
    [SerializeField] private float basicWorkSeconds = 15f;
    [SerializeField] private float workTimeVariation = 1f;
    [SerializeField] private float streamPopularityK = 0.95f;
    [SerializeField] private float streamPopularityAlpha = 0.88f;
    private const float NoItemStreamInItemBarTimeMultiplier = 0.5f;

    [Header("Debug — 配信時間式（実行時のみ上書き）")]
    [SerializeField] private int debugStreamingT;
    [SerializeField] private int debugStreamingN;
    [SerializeField] private int debugStreamingD;
    [SerializeField] private int debugStreamingItemStreamCountInItemBar;
    [SerializeField] private float debugStreamingBaseTimeSeconds;
    [SerializeField] private float debugStreamingFinalTimeSeconds;
    [Tooltip("FinalTime 秒を今から取り続けた場合のローカル時計での終了見込み（再計算時点）")]
    [SerializeField] private string debugStreamingFinalTimeLocalEndClock;

    [Header("Debug — 現在の仕事セッション（実行時のみ上書き）")]
    [SerializeField] private float debugCurrentRequiredWorkSeconds;
    [SerializeField] private float debugCurrentRemainingWorkSeconds;
    [Tooltip("残り秒がそのまま実時間で経過するときの終了見込み（一時停止は未反映）")]
    [SerializeField] private string debugCurrentSessionLocalEndClock;

    [Header("Accept Visuals")]
    [SerializeField] private WorkStreamingAcceptEffect acceptEffect;
    [SerializeField] private Image acceptedItemView;
    [SerializeField] private AcceptedItemViewBounceController acceptedItemViewBounce;
    [SerializeField] private Image dropRaycastArea;

    [Header("Work Remaining Time UI")]
    [SerializeField] private Image imageTimeB;
    [SerializeField] private Image imageTimeF;

    [Header("Direction Views")]
    [SerializeField] private DirectionView01Controller directionView01;
    [SerializeField] private DirectionView02Controller directionView02;
    [SerializeField] private DirectionView03Controller directionView03;
    [SerializeField] private DirectionView04Controller directionView04;

    private static Sprite cachedUiWhiteSprite;

    private Coroutine acceptFlowRoutine;
    private float elapsedWorkSeconds;
    private float workVisualElapsedSeconds;
    private float currentSessionRequiredWorkSeconds;
    private ItemSpawnController cachedSpawnController;
    private bool hasAcceptedRuntimeItem;
    private ItemSpawnController cachedItemSpawnController;
    private Transform imageTimeFOriginalParent;
    private int imageTimeFOriginalSiblingIndex;
    private bool imageTimeFReparentedUnderBackground;

    public bool IsAcceptingItems
    {
        get => isAcceptingItems;
        set => isAcceptingItems = value;
    }

    private void Awake()
    {
        if (acceptEffect == null)
        {
            acceptEffect = GetComponent<WorkStreamingAcceptEffect>();
        }

        if (acceptedItemView == null)
        {
            Transform t = transform.Find("AcceptedItemView");
            if (t != null)
            {
                acceptedItemView = t.GetComponent<Image>();
            }
        }

        EnsureDropRaycastArea();
        ResolveInvalidViewIfNeeded();

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
            directionView01 = FindChildComponent<DirectionView01Controller>("DirectionView01");
        }

        if (directionView02 == null)
        {
            directionView02 = FindChildComponent<DirectionView02Controller>("DirectionView02");
        }

        if (directionView03 == null)
        {
            directionView03 = FindChildComponent<DirectionView03Controller>("DirectionView03");
        }

        if (directionView04 == null)
        {
            directionView04 = FindChildComponent<DirectionView04Controller>("DirectionView04");
        }

        ResolveWorkTimeImagesIfNeeded();
        SetWorkTimeImagesActive(false);

        SetDirectionViewsActive(false);
        acceptedItemViewBounce?.SetCarrierActive(false);
        hasAcceptedRuntimeItem = isWorking && acceptedItemView != null && acceptedItemView.sprite != null;

        cachedSpawnController = FindObjectOfType<ItemSpawnController>(true);
        RefreshInvalidView();
    }

    private void Start()
    {
        RefreshInvalidView();

        if (UpgradesManager.Instance != null)
        {
            RefreshDebugUpgradeAwareWorkSeconds();
        }
        else
        {
            StartCoroutine(RefreshDebugWhenUpgradesReady());
        }
    }

    private IEnumerator RefreshDebugWhenUpgradesReady()
    {
        yield return null;
        RefreshDebugUpgradeAwareWorkSeconds();
    }

    private void Update()
    {
        UpdateWorkRemainingTimeBarVisual();
    }

    /// <summary>
    /// アップグレード反映後の配信時間式（side_panel_upgrade_spec.md の FinalTime）を再計算し、インスペクター表示を更新する。
    /// </summary>
    public void RefreshDebugUpgradeAwareWorkSeconds()
    {
        if (UpgradesManager.Instance == null)
        {
            debugStreamingT = 0;
            debugStreamingN = 0;
            debugStreamingD = 0;
            debugStreamingBaseTimeSeconds = 0f;
            debugStreamingFinalTimeSeconds = 0f;
            debugStreamingFinalTimeLocalEndClock = "";
            return;
        }

        int t = UpgradesManager.Instance.GetStreamingRelatedUpgradeTotalCount();
        int d = UpgradesManager.Instance.GetStreamEnvironmentUpgradeCount();
        int n = UpgradesManager.Instance.GetStreamingIncreaseUpgradeCountN();
        debugStreamingT = t;
        debugStreamingD = d;
        debugStreamingN = n;

        float final = ComputeStreamingFinalTimeSeconds(n, d);
        float baseOnly = ComputeStreamingBaseTimeSeconds(n);
        int itemStreamCount = CountItemStreamInItemBar();
        debugStreamingItemStreamCountInItemBar = itemStreamCount;
        debugStreamingBaseTimeSeconds = baseOnly;
        debugStreamingFinalTimeSeconds = ApplyRequiredWorkSecondsFromBaseSeconds(final, itemStreamCount);

        try
        {
            debugStreamingFinalTimeLocalEndClock = DateTime.Now.AddSeconds(debugStreamingFinalTimeSeconds).ToString("yyyy/MM/dd HH:mm:ss");
        }
        catch (ArgumentOutOfRangeException)
        {
            debugStreamingFinalTimeLocalEndClock = "(範囲外)";
        }
    }

    /// <summary>
    /// シーン内のすべての <see cref="WorkStreaming"/> でデバッグ表示を再計算する。
    /// </summary>
    public static void RefreshAllDebugUpgradeAwareWorkSeconds()
    {
        WorkStreaming[] list = FindObjectsOfType<WorkStreaming>(true);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null)
            {
                list[i].RefreshDebugUpgradeAwareWorkSeconds();
                list[i].UpdateDebugCurrentSessionReadouts();
            }
        }
    }

    /// <summary>BaseTime(n) = 15 + 105 × (n / 19)^1.4（秒）</summary>
    public static float ComputeStreamingBaseTimeSeconds(int n)
    {
        double nn = Math.Max(0, n);
        return (float)(15.0 + 105.0 * Math.Pow(nn / 19.0, 1.4));
    }

    /// <summary>FinalTime(n, d) = BaseTime(n) × 0.88^d（秒）</summary>
    public static float ComputeStreamingFinalTimeSeconds(int n, int d)
    {
        double dd = Math.Max(0, d);
        return (float)(ComputeStreamingBaseTimeSeconds(n) * Math.Pow(0.88, dd));
    }

    private void UpdateDebugCurrentSessionReadouts()
    {
        if (!isWorking)
        {
            debugCurrentRequiredWorkSeconds = 0f;
            debugCurrentRemainingWorkSeconds = 0f;
            debugCurrentSessionLocalEndClock = "";
            return;
        }

        float req = GetRequiredWorkSeconds();
        float rem = Mathf.Max(0f, req - elapsedWorkSeconds);
        debugCurrentRequiredWorkSeconds = req;
        debugCurrentRemainingWorkSeconds = rem;

        try
        {
            debugCurrentSessionLocalEndClock = DateTime.Now.AddSeconds(rem).ToString("yyyy/MM/dd HH:mm:ss");
        }
        catch (ArgumentOutOfRangeException)
        {
            debugCurrentSessionLocalEndClock = "(範囲外)";
        }
    }

    public bool CanAcceptItem(DraggableItemController item)
    {
        if (IsDropCheckBlockedByPause())
        {
            return false;
        }

        if (!isAcceptingItems)
        {
            return false;
        }

        if (isWorking)
        {
            return false;
        }

        if (IsAcceptedSlotOccupied())
        {
            return false;
        }

        if (item == null)
        {
            return false;
        }

        if (!acceptItemMailChara)
        {
            return false;
        }

        return item.ItemType == ItemType.ItemMailChara;
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return;
        }

        if (item == null || !item.TryGetDisplaySprite(out Sprite acceptedSprite))
        {
            Debug.LogError("[WorkStreaming] Accepted sprite is null.");
            return;
        }

        if (acceptFlowRoutine != null)
        {
            StopCoroutine(acceptFlowRoutine);
        }

        acceptFlowRoutine = StartCoroutine(RunAcceptFlow(acceptedSprite));
    }

    private IEnumerator RunAcceptFlow(Sprite acceptedSprite)
    {
        PlayAcceptedSeIfConfigured();

        if (acceptEffect != null)
        {
            yield return acceptEffect.Play(acceptedSprite, acceptedItemView);
            if (acceptedItemView == null || acceptedItemView.sprite == null)
            {
                Debug.LogError("[WorkStreaming] AcceptedItemView is missing or sprite not applied after accept effect.");
                acceptFlowRoutine = null;
                yield break;
            }
        }
        else if (!ApplyAcceptedItemViewCore(acceptedSprite))
        {
            acceptFlowRoutine = null;
            yield break;
        }

        if (!CompleteAcceptedItemPresentation())
        {
            acceptFlowRoutine = null;
            yield break;
        }

        StartWorkSession();
        acceptFlowRoutine = null;
    }

    private bool IsDropCheckBlockedByPause()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.ShouldSuppressPlayerInteractions;
    }

    private bool IsAcceptedSlotOccupied()
    {
        return hasAcceptedRuntimeItem;
    }

    public void SetAvailable(bool available)
    {
        isAcceptingItems = available;
        RefreshInvalidView();
    }

    public void DisableInvalidViewByUpgrade()
    {
        SetAvailable(true);
    }

    public void RefreshInvalidView()
    {
        if (invalidView == null)
        {
            return;
        }

        bool isWorkplaceAvailable = isAcceptingItems;
        invalidView.gameObject.SetActive(!isWorkplaceAvailable);
        if (Game02DebugManager.ShouldLogInvalidViewRefresh())
        {
            Debug.Log($"[WorkStreaming] InvalidView active={invalidView.gameObject.activeSelf} available={isWorkplaceAvailable} object={name}");
        }
    }

    public void OnGameManagerWorkTick(float deltaSeconds)
    {
        if (!isWorking || deltaSeconds <= 0f)
        {
            return;
        }

        if (Game02.GameManager.Instance != null && Game02.GameManager.Instance.ShouldSuppressPlayerInteractions)
        {
            return;
        }

        elapsedWorkSeconds += deltaSeconds;
        workVisualElapsedSeconds = Mathf.Max(workVisualElapsedSeconds, elapsedWorkSeconds);
        PlayWorkingEffect();

        UpdateDebugCurrentSessionReadouts();

        float requiredWorkSeconds = GetRequiredWorkSeconds();
        if (elapsedWorkSeconds >= requiredWorkSeconds)
        {
            EndWorkSession();
        }
    }

    private void PlayAcceptedSeIfConfigured()
    {
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkStreamingAccepted);
    }

    private bool ApplyAcceptedItemViewCore(Sprite acceptedSprite)
    {
        if (acceptedItemView == null)
        {
            Debug.LogError("[WorkStreaming] AcceptedItemView is missing.");
            return false;
        }

        acceptedItemView.sprite = acceptedSprite;
        acceptedItemView.enabled = true;
        return true;
    }

    private bool CompleteAcceptedItemPresentation()
    {
        if (acceptedItemView == null)
        {
            Debug.LogError("[WorkStreaming] AcceptedItemView is missing.");
            return false;
        }

        hasAcceptedRuntimeItem = true;
        SetDirectionViewsActive(true);
        acceptedItemViewBounce?.SetCarrierActive(true);
        return true;
    }

    private void StartWorkSession()
    {
        isWorking = true;
        elapsedWorkSeconds = 0f;
        workVisualElapsedSeconds = 0f;
        currentSessionRequiredWorkSeconds = ComputeRequiredWorkSecondsForCurrentUpgradeState();
        RefreshDebugUpgradeAwareWorkSeconds();
        UpdateDebugCurrentSessionReadouts();
        EnsureWorkTimeBarSpritesIfMissing();
        BeginWorkTimeFrontBarLayout();

        SetWorkTimeImagesActive(true);
    }

    private void EndWorkSession()
    {
        long popularityWhenWorkEnds = Game02.GameManager.Instance != null
            ? Game02.GameManager.Instance.CurrentPopularity
            : 0L;
        long itemStreamSpawnPopularity = ComputeStreamPopularitySnapshot(popularityWhenWorkEnds);

        isWorking = false;
        elapsedWorkSeconds = 0f;
        workVisualElapsedSeconds = 0f;
        currentSessionRequiredWorkSeconds = 0f;
        UpdateDebugCurrentSessionReadouts();
        RestoreWorkTimeFrontBarParentIfNeeded();
        SetWorkTimeImagesActive(false);
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkStreamingComplete);
        PlayWorkExitEffect();
        RespawnWorkExitItemsOnCanvas(itemStreamSpawnPopularity);
        ClearAcceptedItemView();
        WorkCompleted?.Invoke();
    }

    private long ComputeStreamPopularitySnapshot(long currentPopularity)
    {
        double p = Math.Max(1d, currentPopularity);
        double k = Math.Max(0d, streamPopularityK);
        double alpha = Math.Max(0.01d, streamPopularityAlpha);
        double raw = k * Math.Pow(p, alpha);
        if (raw >= long.MaxValue)
        {
            return long.MaxValue;
        }

        long result = (long)Math.Round(raw);
        return Math.Max(1L, result);
    }

    private void ClearAcceptedItemView()
    {
        if (acceptedItemView == null)
        {
            return;
        }

        acceptedItemView.sprite = null;
        acceptedItemView.enabled = false;
        hasAcceptedRuntimeItem = false;
        SetDirectionViewsActive(false);
        acceptedItemViewBounce?.SetCarrierActive(false);
    }

    private float GetRequiredWorkSeconds()
    {
        if (isWorking && currentSessionRequiredWorkSeconds > 0f)
        {
            return currentSessionRequiredWorkSeconds;
        }

        return ComputeRequiredWorkSecondsForCurrentUpgradeState();
    }

    private float ComputeRequiredWorkSecondsForCurrentUpgradeState()
    {
        // WorkStreaming の実仕事秒は side_panel_upgrade_spec の FinalTime(n, d) を正とする。
        // UpgradesManager 不在時は Inspector の basicWorkSeconds × workTimeVariation にフォールバック。
        float baseSeconds = ComputeUpgradeAwareBaseWorkSeconds();
        return ApplyRequiredWorkSecondsFromBaseSeconds(baseSeconds, CountItemStreamInItemBar());
    }

    private float ComputeUpgradeAwareBaseWorkSeconds()
    {
        if (UpgradesManager.Instance != null)
        {
            int n = UpgradesManager.Instance.GetStreamingIncreaseUpgradeCountN();
            int d = UpgradesManager.Instance.GetStreamEnvironmentUpgradeCount();
            return ComputeStreamingFinalTimeSeconds(n, d);
        }

        return Mathf.Max(0f, basicWorkSeconds) * Mathf.Max(0f, workTimeVariation);
    }

    private static float ApplyRequiredWorkSecondsFromBaseSeconds(float baseSeconds, int itemStreamCountInItemBar)
    {
        float adjusted = baseSeconds;
        if (itemStreamCountInItemBar <= 0)
        {
            adjusted *= NoItemStreamInItemBarTimeMultiplier;
        }

        return Mathf.Max(1f, Mathf.Floor(adjusted));
    }

    private int CountItemStreamInItemBar()
    {
        if (cachedItemSpawnController == null)
        {
            cachedItemSpawnController = FindAnyObjectByType<ItemSpawnController>(FindObjectsInactive.Exclude);
        }

        return cachedItemSpawnController != null
            ? cachedItemSpawnController.GetActiveItemStreamCountOnCanvas()
            : 0;
    }

    private void PlayWorkingEffect()
    {
        // 現段階は空実装。
    }

    private void PlayWorkExitEffect()
    {
        // 現段階は空実装。
    }

    private void ResolveWorkTimeImagesIfNeeded()
    {
        if (imageTimeB == null)
        {
            Transform t = transform.Find("ImageTimeB");
            if (t != null)
            {
                imageTimeB = t.GetComponent<Image>();
            }
        }

        if (imageTimeF == null)
        {
            Transform t = transform.Find("ImageTimeF");
            if (t != null)
            {
                imageTimeF = t.GetComponent<Image>();
            }
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
        if (imageTimeF == null || !isWorking)
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

    private void RespawnWorkExitItemsOnCanvas(long itemStreamSpawnPopularity)
    {
        cachedSpawnController = cachedSpawnController != null
            ? cachedSpawnController
            : FindObjectOfType<ItemSpawnController>(true);

        if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
        {
            Debug.LogWarning("[WorkStreaming] Work exit respawn skipped: ItemSpawnController or ItemCanvas not found.");
            return;
        }

        if (!TryGetSpawnPointCenterSelfInItemCanvas(cachedSpawnController.ItemCanvas, out Vector2 centerAnchored))
        {
            Debug.LogWarning("[WorkStreaming] Work exit respawn skipped: could not resolve WorkStreaming center in ItemCanvas.");
            return;
        }

        if (!cachedSpawnController.TrySpawnItem(
                ItemType.ItemMailChara,
                centerAnchored,
                true,
                out DraggableItemController mailSpawned,
                null))
        {
            LogWorkExitSpawnFailure(nameof(ItemType.ItemMailChara));
        }
        else if (mailSpawned == null)
        {
            Debug.LogWarning("[WorkStreaming] ItemMailChara work-exit respawn produced no DraggableItemController on instance.");
        }

        if (!cachedSpawnController.TrySpawnItem(
                ItemType.ItemStream,
                centerAnchored,
                true,
                out DraggableItemController streamSpawned,
                itemStreamSpawnPopularity))
        {
            LogWorkExitSpawnFailure(nameof(ItemType.ItemStream));
        }
        else if (streamSpawned == null)
        {
            Debug.LogWarning("[WorkStreaming] ItemStream work-exit respawn produced no DraggableItemController on instance.");
        }
    }

    private static void LogWorkExitSpawnFailure(string itemLabel)
    {
        if (Game02.GameManager.Instance != null && Game02.GameManager.Instance.HasFatalError)
        {
            Debug.LogWarning($"[WorkStreaming] {itemLabel} work-exit respawn skipped: GameManager fatal state (spawn blocked).");
        }
        else
        {
            Debug.LogWarning($"[WorkStreaming] {itemLabel} work-exit respawn failed: TrySpawnItem returned false (prefab / canvas / controller).");
        }
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

    private void ResolveInvalidViewIfNeeded()
    {
        if (invalidView != null)
        {
            return;
        }

        Transform t = transform.Find("InvalidView");
        if (t != null)
        {
            invalidView = t.GetComponent<Image>();
        }
    }

    public Game02.WorkStreamingState CaptureSaveState()
    {
        return new Game02.WorkStreamingState
        {
            objectName = name,
            isAcceptingItems = isAcceptingItems,
            isWorking = isWorking,
            hasAcceptedRuntimeItem = hasAcceptedRuntimeItem,
            acceptedSpriteName = acceptedItemView != null && acceptedItemView.sprite != null ? acceptedItemView.sprite.name : string.Empty,
            elapsedWorkSeconds = elapsedWorkSeconds,
            workVisualElapsedSeconds = workVisualElapsedSeconds,
            currentSessionRequiredWorkSeconds = currentSessionRequiredWorkSeconds
        };
    }

    public void ApplySaveState(Game02.WorkStreamingState state)
    {
        if (state == null)
        {
            return;
        }

        isAcceptingItems = state.isAcceptingItems;
        isWorking = state.isWorking;
        hasAcceptedRuntimeItem = state.hasAcceptedRuntimeItem;
        elapsedWorkSeconds = Mathf.Max(0f, state.elapsedWorkSeconds);
        workVisualElapsedSeconds = Mathf.Max(0f, state.workVisualElapsedSeconds);
        currentSessionRequiredWorkSeconds = Mathf.Max(0f, state.currentSessionRequiredWorkSeconds);
        if (acceptedItemView != null)
        {
            acceptedItemView.sprite = Game02.Game02SpriteResolver.ResolveByName(state.acceptedSpriteName);
            acceptedItemView.enabled = hasAcceptedRuntimeItem && acceptedItemView.sprite != null;
        }

        RefreshInvalidView();
        SetDirectionViewsActive(isWorking);
        if (isWorking)
        {
            EnsureWorkTimeBarSpritesIfMissing();
            BeginWorkTimeFrontBarLayout();
        }
        else
        {
            RestoreWorkTimeFrontBarParentIfNeeded();
        }

        SetWorkTimeImagesActive(isWorking);
    }
}
