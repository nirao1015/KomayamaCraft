using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorkMovieUploadController : MonoBehaviour, IWorkplaceTarget
{
    /// <summary>動画アップロード受け入れ時。<paramref name="slotIndex"/> は WorkMovie_N の N。</summary>
    public static event Action<int> MovieUploadAccepted;
    public static event Action BuzzTriggered;

    [SerializeField] private int slotIndex;
    private static Sprite cachedUiWhiteSprite;
    [Serializable]
    private sealed class SlotView
    {
        public GameObject Root;
        public Image RootRaycastImage;
        public GameObject HoverHit;
        public Game02HoverContentProvider HoverContentProvider;
        public GameObject BuzzEffect;
        public RectTransform BuzzEffectRect;
        public CanvasGroup BuzzEffectCanvasGroup;
        public WorkMovieBuzzEffect02Controller[] BuzzEffect02Controllers;
        public WorkMovieBuzzEffect03Controller[] BuzzEffect03Controllers;
        public Image AcceptedItemView;
        public AcceptedItemViewBounceController AcceptedItemViewBounce;
        public AcceptedItemViewBreathingScaleController AcceptedItemViewBreathingScale;
        public WorkMovieDirectionView01RiseArrowController DirectionView01RiseArrow;
        public WorkMovieDirectionView02CommentBalloonController DirectionView02CommentBalloon;
        public WorkMovieDirectionView04SpinController DirectionView04StageSpin;
        public Image ImageTimeB;
        public Image ImageTimeF;
        public TMP_Text ViewsUiText;
        public Game02.TmpLongCounterRollPresenter ViewsCounterRoll;
        public TMP_Text MoneyAddUiText;
        public IWorkStreamingDirectionView DirectionView01;
        public IWorkStreamingDirectionView DirectionView02;
        public IWorkStreamingDirectionView DirectionView03;
        public IWorkStreamingDirectionView DirectionView04;
    }

    private sealed class SlotSession
    {
        public bool IsWorking;
        public bool IsVeryFirstUploadedMovie;
        public bool HasAppliedFirstIncomeBonus;
        public int MoneyPayoutCount;
        public bool HasAppliedFixedIncomeAtTenthTiming;
        public string MovieName;
        public Sprite DisplaySprite;
        public long MoviePopularityBase;
        public long MovieBuzzGain;
        public bool IsBuzzMovie;
        public float ElapsedSeconds;
        public long AccumulatedViews;
        public long PendingMonetizedViews;
        public double PendingViewFraction;
        public double PendingMoneyFraction;
        public long LastMoneyDeltaDisplay;
        public bool HasAppliedInitialViews;
        public int FiveSecondTickCount;
        public int MoneyTickAccumulator;
        public bool IsMoneyAddFloating;
        public long MoneyAddFloatingValue;
        public float MoneyAddFloatingElapsedSeconds;
        public bool IsBuzzEffectActive;
        public bool IsBuzzEffectStopRequested;
        public float BuzzEffectCycleElapsedSeconds;
        public bool IsBuzzBgmRequested;
    }

    private const string FallbackMovieName = "禁忌";

    [Header("Slot Views")]
    [SerializeField] private SlotView[] slotViews = new SlotView[5];

    [Header("Optional UI")]
    [SerializeField] private TMP_Text viewsUiText;
    [SerializeField] private TMP_Text moneyAddUiTextInViews;
    [SerializeField] private Game02.Game02EffectManager game02EffectManager;

    [Header("Runtime Ticks")]
    [SerializeField] private float viewsTickSeconds = 5f;
    [SerializeField] private float moneyTickSeconds = 10f;

    [Header("Fixed Income Timing Bonus")]
    [SerializeField, Min(1), Tooltip("動画ごとに、この回目の収入タイミングで固定収入を加算（押し出しで未到達なら未適用）。")]
    private int fixedIncomeTimingIndex = 10;
    [SerializeField, Min(0), Tooltip("上記タイミングで、通常収入計算の後に加算する固定額（係数では掛けない）。")]
    private long fixedIncomeAmount = 500L;

    [Header("Random Factors")]
    [SerializeField] private int initialViewsOffsetMin = -5;
    [SerializeField] private int initialViewsOffsetMax = 5;
    [SerializeField] private float initialViewsBaseMultiplier = 4.79f;
    [SerializeField] private float initialViewsPopularityExponent = 0.5f;
    [SerializeField] private long initialViewsBoostPopularityCap = 500L;
    [SerializeField] private float initialViewsLowPopularityBoostAtZero = 1.7f;
    [SerializeField] private float initialViewsLowPopularityGuaranteedRate = 0.8f;
    [SerializeField] private float viewsBaseA = 0.30f;
    [SerializeField] private float viewsBeta = 0.93f;
    [SerializeField] private float randomViewsFactorMin = 0.92f;
    [SerializeField] private float randomViewsFactorMax = 1.08f;
    [SerializeField] private float phaseViewsGainEarly = 0.90f;
    [SerializeField] private float phaseViewsGainMid = 1.25f;
    [SerializeField] private float phaseViewsGainLate = 0.95f;
    [SerializeField] private float phasePopularityGainEarly = 1.00f;
    [SerializeField] private float phasePopularityGainMid = 1.12f;
    [SerializeField] private float phasePopularityGainLate = 0.72f;
    [SerializeField] private float phaseMoneyGainEarly = 0.95f;
    [SerializeField] private float phaseMoneyGainMid = 1.25f;
    [SerializeField] private float phaseMoneyGainLate = 1.08f;
    [SerializeField] private float phaseEarlyEndMinutes = 20f;
    [SerializeField] private float phaseMidEndMinutes = 45f;
    [SerializeField] private float basePopularityGainRate = 0.22f;
    [SerializeField] private float popularityDampingC = 180000f;
    [SerializeField] private float earlyRandomFactorMin = 0.92f;
    [SerializeField] private float earlyRandomFactorMax = 1.08f;
    [SerializeField] private float midRandomFactorMin = 0.95f;
    [SerializeField] private float midRandomFactorMax = 1.02f;
    [SerializeField] private float lateRandomFactorMin = 0.70f;
    [SerializeField] private float lateRandomFactorMax = 1.00f;
    [SerializeField] private float popularityRandomFactorMin = 0.95f;
    [SerializeField] private float popularityRandomFactorMax = 1.05f;
    [Header("Popularity Decade Damping")]
    [SerializeField] private bool enableDecadeDamping = true;
    [SerializeField] private float[] viewsDampingByPopularityDecade = new float[]
    {
        1.00f, 0.90f, 0.72f, 0.58f, 0.45f, 0.34f, 0.25f
    };
    [SerializeField] private float[] moneyDampingByPopularityDecade = new float[]
    {
        1.00f, 0.62f, 0.42f, 0.28f, 0.18f, 0.11f, 0.07f
    };

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Header("AcceptedItemView Mosaic (Per WorkMovie_ Slot)")]
    [SerializeField] private bool enableAcceptedItemMosaic = true;
    [SerializeField] private int acceptedItemMosaicPixelSize = 8;
    [SerializeField] private string acceptedItemMosaicShaderName = "UI/Game02/ItemStreamMosaic";

    [Header("AcceptedItemView Breathing Scale (Per WorkMovie_ Slot)")]
    [SerializeField] private bool enableAcceptedItemBreathingScale = true;
    [SerializeField] private float acceptedItemBreathingScaleMultiplier = 1.03f;
    [SerializeField] private float acceptedItemBreathingCycleSeconds = 2.5f;

    [Header("DirectionView04 Stage Spin (Per WorkMovie_ Slot)")]
    [SerializeField] private bool enableDirectionView04StageSpin = true;
    [SerializeField] private float directionView04InitialRotationSpeedDegPerSec = 180f;
    [SerializeField] private float directionView04FinalRotationSpeedDegPerSec = 45f;
    [SerializeField] private float directionView04Stage1EndSeconds = 100f;
    [SerializeField] private float directionView04Stage2EndSeconds = 180f;

    [Header("DirectionView01 Rise Arrow (Per WorkMovie_ Slot)")]
    [SerializeField] private bool enableDirectionView01RiseArrow = true;
    [SerializeField] private Vector2 directionView01MoveDistance = new Vector2(70f, 50f);
    [SerializeField] private float directionView01MoveDurationSeconds = 1.2f;
    [SerializeField] private float directionView01FadeInSeconds = 0.2f;
    [SerializeField] private float directionView01FadeOutSeconds = 0.4f;
    [SerializeField] private float directionView01Stage1SpawnIntervalSeconds = 5f;
    [SerializeField] private float directionView01Stage2SpawnIntervalSeconds = 12.5f;
    [SerializeField] private float directionView01Stage3SpawnIntervalSeconds = 20f;
    [SerializeField] private float directionView01Stage1EndSeconds = 100f;
    [SerializeField] private float directionView01Stage2EndSeconds = 180f;
    [SerializeField] private Color directionView01Stage1Color = new Color(0f, 0.898f, 1f, 1f);
    [SerializeField] private Color directionView01Stage2Color = new Color(1f, 0.761f, 0.278f, 1f);
    [SerializeField] private Color directionView01Stage3Color = new Color(0.435f, 0.525f, 0.659f, 1f);

    [Header("Buzz Effect (Per WorkMovie_ Slot)")]
    [SerializeField] private int buzzEffectPulseCount = 3;
    [SerializeField] private float buzzEffectPulseScale = 1.4f;
    [SerializeField] private float buzzEffectPulseDurationSeconds = 1.2f;
    [SerializeField] private float buzzEffectExpandScale = 1.8f;
    [SerializeField] private float buzzEffectExpandDurationSeconds = 0.8f;
    [SerializeField] private float buzzEffectStage1EndSeconds = 100f;

    private readonly SlotSession[] sessions = new SlotSession[5];
    private float viewsTickAccumulator;
    private long totalViews;
    private bool hasAssignedVeryFirstUploadedMovie;

    /// <summary>スロット1本体の参照。<see cref="SceneHasBuzzMovieInBuzzPeriod"/> などの静的クエリ用。</summary>
    private static WorkMovieUploadController s_primaryBuzzQueryController;

    private long[] slotViewsUiLastGoalPushed;
    private Game02.TmpLongCounterRollPresenter viewsUiRoll;
    private long lastTotalViewsUiPushed = long.MinValue;

    public bool CanAcceptItem(DraggableItemController item)
    {
        if (!isActiveAndEnabled || item == null)
        {
            return false;
        }

        if (slotIndex != 1)
        {
            return false;
        }

        if (!IsWorkplaceAvailable(slotIndex))
        {
            return false;
        }

        if (item.ItemType != ItemType.ItemMovie)
        {
            return false;
        }

        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm != null && (gm.ShouldSuppressPlayerInteractions || gm.HasFatalError))
        {
            return false;
        }

        return true;
    }

    public int SlotIndex => slotIndex;

    public bool HasAnyWorkingSession()
    {
        EnsureSessionObjects();
        for (int i = 0; i < sessions.Length; i++)
        {
            SlotSession session = sessions[i];
            if (session != null && session.IsWorking)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// いずれかのスロットで稼働中かつ <see cref="SlotSession.IsBuzzBgmRequested"/> が真か。
    /// バズ BGM を通常へ戻すタイミング（<see cref="RequestStageBgmBuzzEnd"/> と対になるフラグの落下）と一致する。
    /// バズ動画でも基底時間経過後に演出サイクルが終わり <see cref="SlotSession.IsBuzzBgmRequested"/> が下りたあとは偽。
    /// </summary>
    public bool HasAnyBuzzMovieInBuzzPeriod()
    {
        EnsureSessionObjects();
        for (int i = 0; i < sessions.Length; i++)
        {
            SlotSession session = sessions[i];
            if (session != null &&
                session.IsWorking &&
                session.IsBuzzBgmRequested)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>シーン内のスロット1 <see cref="WorkMovieUploadController"/> を経由して <see cref="HasAnyBuzzMovieInBuzzPeriod"/> を評価する。</summary>
    public static bool SceneHasBuzzMovieInBuzzPeriod()
    {
        if (s_primaryBuzzQueryController != null)
        {
            return s_primaryBuzzQueryController.HasAnyBuzzMovieInBuzzPeriod();
        }

        WorkMovieUploadController[] arr =
            FindObjectsByType<WorkMovieUploadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < arr.Length; i++)
        {
            WorkMovieUploadController c = arr[i];
            if (c != null && c.SlotIndex == 1)
            {
                return c.HasAnyBuzzMovieInBuzzPeriod();
            }
        }

        return false;
    }

    public int GetCurrentHeadSessionStage()
    {
        EnsureSessionObjects();
        SlotSession head = sessions != null && sessions.Length > 0 ? sessions[0] : null;
        if (head == null || !head.IsWorking)
        {
            return 0;
        }

        float elapsed = Mathf.Max(0f, head.ElapsedSeconds);
        if (elapsed < 100f)
        {
            return 1;
        }

        if (elapsed < 180f)
        {
            return 2;
        }

        return 3;
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (slotIndex != 1)
        {
            return;
        }

        if (!CanAcceptItem(item))
        {
            return;
        }

        PrepareSlotViewsIfMissing();
        EnsureSessionObjects();

        Sprite incomingSprite = ResolveIncomingSprite(item);
        ItemMoviePower moviePower = item.GetComponent<ItemMoviePower>();
        long popularity = moviePower != null ? Math.Max(0L, moviePower.GetFinalPopularity()) : 0L;
        long rawBuzzGain = moviePower != null ? Math.Max(0L, moviePower.GetBuzzGainValue()) : 0L;
        int trendLevel = ResolveTrendUpgradeLevel();
        long buzzGain = ComputeTrendAdjustedBuzzGain(rawBuzzGain, trendLevel);
        string movieName = SanitizeMovieName(moviePower != null ? moviePower.GetMovieName() : string.Empty);
        bool isBuzz = DetermineAndApplyBuzzState(buzzGain);
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkMovieAccepted);

        ShiftSessionsRightWithinActiveRange();
        SlotSession head = sessions[0];
        head.IsWorking = true;
        head.IsVeryFirstUploadedMovie = !hasAssignedVeryFirstUploadedMovie;
        if (!hasAssignedVeryFirstUploadedMovie)
        {
            hasAssignedVeryFirstUploadedMovie = true;
        }
        head.HasAppliedFirstIncomeBonus = false;
        head.MoneyPayoutCount = 0;
        head.HasAppliedFixedIncomeAtTenthTiming = false;
        head.MovieName = movieName;
        head.DisplaySprite = incomingSprite;
        head.MoviePopularityBase = popularity;
        head.MovieBuzzGain = buzzGain;
        head.IsBuzzMovie = isBuzz;
        head.ElapsedSeconds = 0f;
        head.AccumulatedViews = 0L;
        head.PendingMonetizedViews = 0L;
        head.PendingViewFraction = 0d;
        head.PendingMoneyFraction = 0d;
        head.LastMoneyDeltaDisplay = 0L;
        long initialViews = ComputeInitialViewsDelta(popularity);
        head.HasAppliedInitialViews = true;
        head.FiveSecondTickCount = 0;
        head.MoneyTickAccumulator = 0;
        head.IsMoneyAddFloating = false;
        head.MoneyAddFloatingValue = 0L;
        head.MoneyAddFloatingElapsedSeconds = 0f;
        head.IsBuzzEffectActive = isBuzz;
        head.IsBuzzEffectStopRequested = false;
        head.BuzzEffectCycleElapsedSeconds = 0f;
        head.IsBuzzBgmRequested = isBuzz;

        if (initialViews > 0L)
        {
            head.AccumulatedViews = initialViews;
            head.PendingMonetizedViews = initialViews;
            totalViews += initialViews;
            ApplyPopularityDelta(initialViews, head.IsBuzzMovie);
            if (debugLog)
            {
                Debug.Log($"[WorkMovieUpload] initialViews applied immediately={initialViews} popularity={popularity}");
            }

            Game02.Game02AlienProgressTracker.EnsureExists()?.NotifyMovieAccumulatedViews(head.AccumulatedViews);
        }

        if (isBuzz)
        {
            RequestStageBgmBuzzStart();
            Game02.BuzzPaperConfettiController controller = Game02.BuzzPaperConfettiController.EnsureSceneController();
            if (controller != null)
            {
                controller.Play();
            }

            Game02.Game02AlienProgressTracker.EnsureExists()?.NotifyBuzzMovieUploaded();
        }

        Game02.GameManager.Instance?.OnMovieUploadAccepted();

        MovieUploadAccepted?.Invoke(slotIndex);
        if (isBuzz)
        {
            if (slotIndex == 1)
            {
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_03);
            }

            BuzzTriggered?.Invoke();
        }

        SnapViewsCounterUiToSessionTruth();
        RefreshAllSlotVisuals();
        UpdateViewsUiText();
        SnapViewsCounterUiToSessionTruth();
    }

    public void OnGameManagerWorkTick(float deltaSeconds)
    {
        if (slotIndex != 1)
        {
            return;
        }

        if (!isActiveAndEnabled || deltaSeconds <= 0f)
        {
            return;
        }

        Game02.GameManager gm = Game02.GameManager.Instance;
            if (gm == null || gm.ShouldSuppressPlayerInteractions || gm.HasFatalError)
        {
            return;
        }

        PrepareSlotViewsIfMissing();
        EnsureSessionObjects();
        viewsTickAccumulator += deltaSeconds;
        float safeViewsTick = Mathf.Max(0.01f, viewsTickSeconds);
        while (viewsTickAccumulator >= safeViewsTick)
        {
            viewsTickAccumulator -= safeViewsTick;
            AdvanceOneViewsTick();
        }
    }

    private void Awake()
    {
        if (slotIndex <= 0)
        {
            slotIndex = ParseSlotIndexFromName(gameObject.name);
        }

        if (slotIndex == 1)
        {
            s_primaryBuzzQueryController = this;
        }

        ApplyAcceptedItemMosaicForOwnSlot();
        if (slotIndex != 1)
        {
            return;
        }

        hasAssignedVeryFirstUploadedMovie = false;
        PrepareSlotViewsIfMissing();
        EnsureSessionObjects();
        RefreshAllSlotVisuals();
        UpdateViewsUiText();
    }

    private void OnDestroy()
    {
        if (s_primaryBuzzQueryController == this)
        {
            s_primaryBuzzQueryController = null;
        }
    }

    private void Update()
    {
        if (slotIndex != 1)
        {
            return;
        }

        RefreshAllSlotVisuals();
    }

    private void PrepareSlotViewsIfMissing()
    {
        if (slotViews == null || slotViews.Length != 5)
        {
            slotViews = new SlotView[5];
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] == null)
            {
                slotViews[i] = new SlotView();
            }

            if (slotViews[i].Root == null)
            {
                GameObject go = FindSlotRoot(i + 1);
                if (go != null)
                {
                    slotViews[i].Root = go;
                }
            }

            SlotView view = slotViews[i];
            if (view.Root == null)
            {
                continue;
            }

            if (view.AcceptedItemView == null)
            {
                view.AcceptedItemView = FindImageByNameRecursive(view.Root.transform, "AcceptedItemView");
            }

            if (view.RootRaycastImage == null)
            {
                view.RootRaycastImage = view.Root.GetComponent<Image>();
            }

            if (view.HoverHit == null)
            {
                Transform byPath = view.Root.transform.Find("Hover/ HoverHit");
                if (byPath == null)
                {
                    byPath = view.Root.transform.Find("Hover/HoverHit");
                }

                view.HoverHit = byPath != null
                    ? byPath.gameObject
                    : FindGameObjectByNameRecursive(view.Root.transform, "HoverHit");
            }

            if (view.HoverContentProvider == null && view.HoverHit != null)
            {
                view.HoverContentProvider = view.HoverHit.GetComponent<Game02HoverContentProvider>();
                if (view.HoverContentProvider == null)
                {
                    view.HoverContentProvider = view.HoverHit.AddComponent<Game02HoverContentProvider>();
                }
            }

            EnsureHoverBinding(view);

            if (view.BuzzEffect == null)
            {
                Transform buzzEffectTransform = view.Root.transform.Find("BuzzEffect");
                if (buzzEffectTransform != null)
                {
                    view.BuzzEffect = buzzEffectTransform.gameObject;
                }
            }

            if (view.BuzzEffectRect == null && view.BuzzEffect != null)
            {
                view.BuzzEffectRect = view.BuzzEffect.GetComponent<RectTransform>();
            }

            if (view.BuzzEffectCanvasGroup == null && view.BuzzEffect != null)
            {
                view.BuzzEffectCanvasGroup = view.BuzzEffect.GetComponent<CanvasGroup>();
                if (view.BuzzEffectCanvasGroup == null)
                {
                    view.BuzzEffectCanvasGroup = view.BuzzEffect.AddComponent<CanvasGroup>();
                }
            }

            if (view.BuzzEffect02Controllers == null || view.BuzzEffect02Controllers.Length == 0)
            {
                view.BuzzEffect02Controllers = view.Root.GetComponentsInChildren<WorkMovieBuzzEffect02Controller>(true);
            }

            if (view.BuzzEffect03Controllers == null || view.BuzzEffect03Controllers.Length == 0)
            {
                view.BuzzEffect03Controllers = view.Root.GetComponentsInChildren<WorkMovieBuzzEffect03Controller>(true);
            }

            if (view.AcceptedItemViewBounce == null && view.AcceptedItemView != null)
            {
                view.AcceptedItemViewBounce = view.AcceptedItemView.GetComponent<AcceptedItemViewBounceController>();
            }

            if (view.AcceptedItemViewBreathingScale == null && view.AcceptedItemView != null)
            {
                view.AcceptedItemViewBreathingScale = view.AcceptedItemView.GetComponent<AcceptedItemViewBreathingScaleController>();
            }

            if (view.ImageTimeB == null)
            {
                view.ImageTimeB = FindImageByNameRecursive(view.Root.transform, "ImageTimeB");
            }

            if (view.ImageTimeF == null)
            {
                view.ImageTimeF = FindImageByNameRecursive(view.Root.transform, "ImageTimeF");
            }

            if (view.DirectionView01 == null)
            {
                view.DirectionView01 = ResolveDirectionView(view.Root.transform, "DirectionView01");
            }

            if (view.DirectionView01RiseArrow == null && view.DirectionView01 is MonoBehaviour directionView01Behaviour)
            {
                view.DirectionView01RiseArrow = directionView01Behaviour.GetComponent<WorkMovieDirectionView01RiseArrowController>();
            }

            if (view.DirectionView01RiseArrow != null)
            {
                view.DirectionView01 = view.DirectionView01RiseArrow;
            }

            if (view.DirectionView02 == null)
            {
                view.DirectionView02 = ResolveDirectionView(view.Root.transform, "DirectionView02");
            }

            if (view.DirectionView02CommentBalloon == null && view.DirectionView02 is MonoBehaviour directionView02Behaviour)
            {
                view.DirectionView02CommentBalloon = directionView02Behaviour.GetComponent<WorkMovieDirectionView02CommentBalloonController>();
            }

            if (view.DirectionView02CommentBalloon != null)
            {
                view.DirectionView02 = view.DirectionView02CommentBalloon;
            }

            if (view.DirectionView03 == null)
            {
                view.DirectionView03 = ResolveDirectionView(view.Root.transform, "DirectionView03");
            }

            if (view.DirectionView04 == null)
            {
                view.DirectionView04 = ResolveDirectionView(view.Root.transform, "DirectionView04");
            }

            if (view.DirectionView04StageSpin == null && view.DirectionView04 is MonoBehaviour directionView04Behaviour)
            {
                view.DirectionView04StageSpin = directionView04Behaviour.GetComponent<WorkMovieDirectionView04SpinController>();
            }

            if (view.DirectionView04StageSpin != null)
            {
                view.DirectionView04 = view.DirectionView04StageSpin;
            }

            if (view.ViewsUiText == null)
            {
                view.ViewsUiText = FindTextByNameRecursive(view.Root.transform, "ViewsUI");
            }

            EnsureViewsCounterRollOnView(view);

            if (view.MoneyAddUiText == null)
            {
                TMP_Text preferredMoneyAdd = null;
                if ((i + 1) == slotIndex &&
                    moneyAddUiTextInViews != null &&
                    moneyAddUiTextInViews.transform.IsChildOf(view.Root.transform))
                {
                    preferredMoneyAdd = moneyAddUiTextInViews;
                }

                if (preferredMoneyAdd == null)
                {
                    preferredMoneyAdd = FindTextByPath(view.Root.transform, "Views/MoneyAddUI");
                }

                view.MoneyAddUiText = preferredMoneyAdd != null
                    ? preferredMoneyAdd
                    : FindTextByNameRecursive(view.Root.transform, "MoneyAddUI");
            }

        }

        if (viewsUiText == null)
        {
            GameObject viewsGo = GameObject.Find("ViewsUI");
            if (viewsGo != null)
            {
                viewsUiText = viewsGo.GetComponent<TMP_Text>();
            }
        }

        if (viewsUiText != null)
        {
            if (viewsUiRoll == null)
            {
                viewsUiRoll = viewsUiText.GetComponent<Game02.TmpLongCounterRollPresenter>();
                if (viewsUiRoll == null)
                {
                    viewsUiRoll = viewsUiText.gameObject.AddComponent<Game02.TmpLongCounterRollPresenter>();
                }
            }
        }
    }

    private static void EnsureViewsCounterRollOnView(SlotView view)
    {
        if (view == null || view.ViewsUiText == null)
        {
            return;
        }

        if (view.ViewsCounterRoll == null)
        {
            view.ViewsCounterRoll = view.ViewsUiText.GetComponent<Game02.TmpLongCounterRollPresenter>();
            if (view.ViewsCounterRoll == null)
            {
                view.ViewsCounterRoll = view.ViewsUiText.gameObject.AddComponent<Game02.TmpLongCounterRollPresenter>();
            }
        }
    }

    private void EnsureSlotViewsUiGoalCache()
    {
        if (slotViewsUiLastGoalPushed == null || slotViewsUiLastGoalPushed.Length != 5)
        {
            slotViewsUiLastGoalPushed = new long[5];
            for (int i = 0; i < slotViewsUiLastGoalPushed.Length; i++)
            {
                slotViewsUiLastGoalPushed[i] = long.MinValue;
            }
        }
    }

    /// <summary>
    /// 各スロットの <c>ViewsUI</c> とオプションの集計テキストを、現在のセッション／<c>totalViews</c> に即一致させる（<c>SnapTo</c> のみ）。
    /// セーブ復帰、および FIFO 右シフト＋先頭への新規投入など<strong>論理カウンタが不連続に変わる直後</strong>に呼ぶ。
    /// 通常の views tick による増分は呼び出さず、<see cref="RefreshAllSlotVisuals"/> / <see cref="UpdateViewsUiText"/> の <c>SetTarget</c> に任せる。
    /// </summary>
    private void SnapViewsCounterUiToSessionTruth()
    {
        PrepareSlotViewsIfMissing();
        EnsureSessionObjects();
        EnsureSlotViewsUiGoalCache();
        for (int i = 0; i < slotViews.Length; i++)
        {
            SlotView view = slotViews[i];
            SlotSession session = sessions[i];
            if (view == null || view.ViewsUiText == null)
            {
                continue;
            }

            bool working = session != null && session.IsWorking;
            long v = working && session != null ? Math.Max(0L, session.AccumulatedViews) : 0L;
            EnsureViewsCounterRollOnView(view);
            if (view.ViewsCounterRoll != null)
            {
                view.ViewsCounterRoll.SnapTo(v);
            }
            else
            {
                view.ViewsUiText.text = v.ToString("N0");
            }

            slotViewsUiLastGoalPushed[i] = working ? v : long.MinValue;
        }

        if (viewsUiText != null && !IsViewsUiTextUsedAsSlotViewsCounter(viewsUiText))
        {
            if (viewsUiRoll == null)
            {
                viewsUiRoll = viewsUiText.GetComponent<Game02.TmpLongCounterRollPresenter>();
                if (viewsUiRoll == null)
                {
                    viewsUiRoll = viewsUiText.gameObject.AddComponent<Game02.TmpLongCounterRollPresenter>();
                }
            }

            if (viewsUiRoll != null)
            {
                viewsUiRoll.SnapTo(totalViews);
            }
            else
            {
                viewsUiText.text = totalViews.ToString("N0");
            }
        }

        lastTotalViewsUiPushed = totalViews;
    }

    /// <summary>
    /// シーンの <see cref="viewsUiText"/> が <c>Find(&quot;ViewsUI&quot;)</c> 等でスロット用カウンタと同一参照になっている場合、
    /// 集計 <c>totalViews</c> で上書きしない（当該テキストはスロットの <c>AccumulatedViews</c> のみ表示する）。
    /// </summary>
    private bool IsViewsUiTextUsedAsSlotViewsCounter(TMP_Text text)
    {
        if (text == null || slotViews == null)
        {
            return false;
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            SlotView v = slotViews[i];
            if (v != null && v.ViewsUiText == text)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureSessionObjects()
    {
        for (int i = 0; i < sessions.Length; i++)
        {
            if (sessions[i] == null)
            {
                sessions[i] = new SlotSession();
            }
        }
    }

    private static GameObject FindSlotRoot(int slotIndex)
    {
        string byPath = $"UnitCanvas/WorkMovie_{slotIndex}";
        GameObject found = GameObject.Find(byPath);
        if (found != null)
        {
            return found;
        }

        Transform[] all = UnityEngine.Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == $"WorkMovie_{slotIndex}")
            {
                return t.gameObject;
            }
        }

        return null;
    }

    private static int ParseSlotIndexFromName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return 0;
        }

        const string prefix = "WorkMovie_";
        if (!objectName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        string suffix = objectName.Substring(prefix.Length);
        return int.TryParse(suffix, out int parsed) ? parsed : 0;
    }

    private static IWorkStreamingDirectionView ResolveDirectionView(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, StringComparison.Ordinal))
            {
                continue;
            }

            IWorkStreamingDirectionView v = t.GetComponent<IWorkStreamingDirectionView>();
            if (v != null)
            {
                return v;
            }
        }

        return null;
    }

    private static Image FindImageByNameRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, StringComparison.Ordinal))
            {
                continue;
            }

            Image image = t.GetComponent<Image>();
            if (image != null)
            {
                return image;
            }
        }

        return null;
    }

    private static TMP_Text FindTextByNameRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, StringComparison.Ordinal))
            {
                continue;
            }

            TMP_Text text = t.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private static TMP_Text FindTextByPath(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        Transform found = root.Find(relativePath);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<TMP_Text>();
    }

    private static GameObject FindGameObjectByNameRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && string.Equals(t.name, childName, StringComparison.Ordinal))
            {
                return t.gameObject;
            }
        }

        return null;
    }

    private static string SanitizeMovieName(string movieName)
    {
        if (string.IsNullOrEmpty(movieName))
        {
            return FallbackMovieName;
        }

        string sanitized = movieName.Replace("\r", string.Empty).Replace("\n", string.Empty);
        return string.IsNullOrEmpty(sanitized) ? FallbackMovieName : sanitized;
    }

    private static void ApplySlotRaycastPolicy(SlotView view, bool working)
    {
        if (view == null)
        {
            return;
        }

        Image hoverHitImage = view.HoverHit != null ? view.HoverHit.GetComponent<Image>() : null;
        Graphic[] graphics = view.Root != null ? view.Root.GetComponentsInChildren<Graphic>(true) : null;
        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null)
                {
                    continue;
                }

                // 仕事中は HoverHit だけを受け口にする。
                if (working)
                {
                    g.raycastTarget = g == hoverHitImage;
                    continue;
                }

                // 非仕事中は WorkMovie ルートのみ受け口に戻し、それ以外は無効化。
                g.raycastTarget = g == view.RootRaycastImage;
            }
        }

        if (view.RootRaycastImage != null)
        {
            view.RootRaycastImage.raycastTarget = !working;
            if (!working)
            {
                Color c = view.RootRaycastImage.color;
                if (c.a != 0f)
                {
                    c.a = 0f;
                    view.RootRaycastImage.color = c;
                }
            }
        }

        if (view.AcceptedItemView != null)
        {
            view.AcceptedItemView.raycastTarget = false;
        }

        if (view.ImageTimeB != null)
        {
            view.ImageTimeB.raycastTarget = false;
        }

        if (view.ImageTimeF != null)
        {
            view.ImageTimeF.raycastTarget = false;
        }

        if (view.ViewsUiText != null)
        {
            view.ViewsUiText.raycastTarget = false;
        }

        if (view.MoneyAddUiText != null)
        {
            view.MoneyAddUiText.raycastTarget = false;
        }

        if (hoverHitImage != null)
        {
            hoverHitImage.raycastTarget = working;
        }
    }

    private void UpdateBuzzEffectVisual(SlotView view, SlotSession session, bool working)
    {
        if (view == null)
        {
            return;
        }

        float delta = Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        UpdateBuzzEffect02Visual(view, session, working, delta);
        UpdateBuzzEffect03Visual(view, session, working, delta);

        if (view.BuzzEffect == null)
        {
            return;
        }

        if (!working || session == null || !session.IsBuzzEffectActive)
        {
            if (view.BuzzEffect.activeSelf)
            {
                view.BuzzEffect.SetActive(false);
            }

            if (view.BuzzEffectCanvasGroup != null)
            {
                view.BuzzEffectCanvasGroup.alpha = 0f;
            }

            if (view.BuzzEffectRect != null)
            {
                view.BuzzEffectRect.localScale = Vector3.one;
            }

            return;
        }

        if (!view.BuzzEffect.activeSelf)
        {
            view.BuzzEffect.SetActive(true);
        }

        float cycleDuration = GetBuzzEffectCycleDuration();
        session.BuzzEffectCycleElapsedSeconds += delta;
        while (session.BuzzEffectCycleElapsedSeconds >= cycleDuration)
        {
            session.BuzzEffectCycleElapsedSeconds -= cycleDuration;
            if (session.IsBuzzEffectStopRequested)
            {
                session.IsBuzzEffectActive = false;
                if (session.IsBuzzBgmRequested)
                {
                    session.IsBuzzBgmRequested = false;
                    RequestStageBgmBuzzEnd();
                }

                view.BuzzEffect.SetActive(false);
                if (view.BuzzEffectCanvasGroup != null)
                {
                    view.BuzzEffectCanvasGroup.alpha = 0f;
                }

                if (view.BuzzEffectRect != null)
                {
                    view.BuzzEffectRect.localScale = Vector3.one;
                }

                return;
            }
        }

        float t = cycleDuration > 0.0001f ? Mathf.Clamp01(session.BuzzEffectCycleElapsedSeconds / cycleDuration) : 0f;
        EvaluateBuzzEffectCycle(t, out float alpha, out float scale);
        if (view.BuzzEffectCanvasGroup != null)
        {
            view.BuzzEffectCanvasGroup.alpha = alpha;
        }

        if (view.BuzzEffectRect != null)
        {
            view.BuzzEffectRect.localScale = Vector3.one * scale;
        }
    }

    private static void UpdateBuzzEffect02Visual(SlotView view, SlotSession session, bool working, float deltaSeconds)
    {
        WorkMovieBuzzEffect02Controller[] controllers = view != null ? view.BuzzEffect02Controllers : null;
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        bool buzzActive = working && session != null && session.IsBuzzEffectActive;
        bool stopRequested = session != null && session.IsBuzzEffectStopRequested;
        float dt = Mathf.Max(0f, deltaSeconds);
        for (int i = 0; i < controllers.Length; i++)
        {
            WorkMovieBuzzEffect02Controller controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.SetBuzzState(buzzActive, stopRequested);
            controller.ManualTick(dt);
        }
    }

    private static void UpdateBuzzEffect03Visual(SlotView view, SlotSession session, bool working, float deltaSeconds)
    {
        WorkMovieBuzzEffect03Controller[] controllers = view != null ? view.BuzzEffect03Controllers : null;
        if (controllers == null || controllers.Length == 0)
        {
            return;
        }

        bool buzzActive = working && session != null && session.IsBuzzEffectActive;
        bool stopRequested = session != null && session.IsBuzzEffectStopRequested;
        float dt = Mathf.Max(0f, deltaSeconds);
        for (int i = 0; i < controllers.Length; i++)
        {
            WorkMovieBuzzEffect03Controller controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.SetBuzzState(buzzActive, stopRequested);
            controller.ManualTick(dt);
        }
    }

    private void EvaluateBuzzEffectCycle(float normalizedTime, out float alpha, out float scale)
    {
        int pulseCount = Mathf.Max(1, buzzEffectPulseCount);
        float pulseDuration = Mathf.Max(0.01f, buzzEffectPulseDurationSeconds);
        float expandDuration = Mathf.Max(0.01f, buzzEffectExpandDurationSeconds);
        float pulseTotal = pulseCount * pulseDuration;
        float total = Mathf.Max(0.01f, pulseTotal + expandDuration);
        float time = Mathf.Clamp01(normalizedTime) * total;

        if (time <= pulseTotal)
        {
            float pulseT = (time % pulseDuration) / pulseDuration;
            float s = Mathf.Sin(pulseT * Mathf.PI);
            scale = Mathf.Lerp(1f, Mathf.Max(1f, buzzEffectPulseScale), s);
            alpha = Mathf.Clamp01(0.2f + 0.8f * s);
            return;
        }

        float expandT = Mathf.Clamp01((time - pulseTotal) / expandDuration);
        float eased = 1f - Mathf.Pow(1f - expandT, 2f);
        scale = Mathf.Lerp(Mathf.Max(1f, buzzEffectPulseScale), Mathf.Max(1f, buzzEffectExpandScale), eased);
        alpha = 1f - expandT;
    }

    private float GetBuzzEffectCycleDuration()
    {
        int pulseCount = Mathf.Max(1, buzzEffectPulseCount);
        return pulseCount * Mathf.Max(0.01f, buzzEffectPulseDurationSeconds) + Mathf.Max(0.01f, buzzEffectExpandDurationSeconds);
    }

    private static void RequestStageBgmBuzzStart()
    {
        Game02.Game02BgmManager.TryGet()?.RequestBuzzModeStart();
    }

    private static void RequestStageBgmBuzzEnd()
    {
        Game02.Game02BgmManager.TryGet()?.RequestBuzzModeEnd();
    }

    private static void EnsureHoverBinding(SlotView view)
    {
        if (view == null || view.HoverHit == null || view.HoverContentProvider == null)
        {
            return;
        }

        Game02HoverPresenter presenter = UnityEngine.Object.FindObjectOfType<Game02HoverPresenter>(true);
        if (presenter == null)
        {
            return;
        }

        Game02HoverTrigger trigger = view.HoverHit.GetComponent<Game02HoverTrigger>();
        if (trigger == null)
        {
            trigger = view.HoverHit.AddComponent<Game02HoverTrigger>();
        }

        if (trigger != null)
        {
            trigger.ConfigureBinding(presenter, view.HoverContentProvider);
        }

        Image hitImage = view.HoverHit.GetComponent<Image>();
        if (hitImage == null)
        {
            hitImage = view.HoverHit.AddComponent<Image>();
        }

        if (hitImage != null)
        {
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;
        }

        RectTransform hitRect = view.HoverHit.GetComponent<RectTransform>();
        if (hitRect != null)
        {
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = Vector2.zero;
            hitRect.offsetMax = Vector2.zero;
            hitRect.SetAsLastSibling();
        }

        Transform parent = view.HoverHit.transform.parent;
        if (parent == null || trigger == null)
        {
            return;
        }

        Game02HoverPointerBridge bridge = parent.GetComponent<Game02HoverPointerBridge>();
        if (bridge == null)
        {
            bridge = parent.gameObject.AddComponent<Game02HoverPointerBridge>();
        }

        bridge.RegisterTarget(trigger);
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

    private static void EnsureImageSpriteIfMissing(Image image)
    {
        if (image == null || image.sprite != null)
        {
            return;
        }

        image.sprite = GetOrCreateUiWhiteSprite();
        image.type = Image.Type.Simple;
    }

    private Sprite ResolveIncomingSprite(DraggableItemController item)
    {
        if (item != null && item.TryGetDisplaySprite(out Sprite display) && display != null)
        {
            return display;
        }

        Image image = item != null ? item.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    private bool DetermineAndApplyBuzzState(long buzzGain)
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null)
        {
            return false;
        }

        bool isBuzz = gm.IsBuzzTriggeredByGain(buzzGain);
        if (isBuzz)
        {
            if (gm.CurrentBuzz > 0)
            {
                gm.RequestBuzzDelta(-gm.CurrentBuzz, "WorkMovie buzz reset", "WorkMovie_1");
            }
        }
        else
        {
            gm.RequestBuzzDelta(buzzGain, "WorkMovie buzz gain", "WorkMovie_1");
        }

        return isBuzz;
    }

    /// <summary>FIFO 用にセッションを右へ詰める。表示の <c>SnapTo</c> は <see cref="OnItemDropped"/> 完了時の <see cref="SnapViewsCounterUiToSessionTruth"/> に任せる。</summary>
    private void ShiftSessionsRightWithinActiveRange()
    {
        int maxActive = ResolveActiveSlotCount();
        if (maxActive <= 1)
        {
            return;
        }

        int last = Mathf.Clamp(maxActive - 1, 0, sessions.Length - 1);
        SlotSession ejected = sessions[last];
        if (ejected != null && ejected.IsWorking)
        {
            Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkMovieFifoEjected);
        }

        TryReleaseBuzzBgmOnEjectedSession(sessions[last]);
        for (int i = last; i >= 1; i--)
        {
            CopySession(sessions[i - 1], sessions[i]);
        }
    }

    private void TryReleaseBuzzBgmOnEjectedSession(SlotSession session)
    {
        if (session == null || !session.IsBuzzBgmRequested)
        {
            return;
        }

        session.IsBuzzBgmRequested = false;
        RequestStageBgmBuzzEnd();
    }

    private static void CopySession(SlotSession src, SlotSession dst)
    {
        if (src == null || dst == null)
        {
            return;
        }

        dst.IsWorking = src.IsWorking;
        dst.IsVeryFirstUploadedMovie = src.IsVeryFirstUploadedMovie;
        dst.HasAppliedFirstIncomeBonus = src.HasAppliedFirstIncomeBonus;
        dst.MoneyPayoutCount = src.MoneyPayoutCount;
        dst.HasAppliedFixedIncomeAtTenthTiming = src.HasAppliedFixedIncomeAtTenthTiming;
        dst.MovieName = src.MovieName;
        dst.DisplaySprite = src.DisplaySprite;
        dst.MoviePopularityBase = src.MoviePopularityBase;
        dst.MovieBuzzGain = src.MovieBuzzGain;
        dst.IsBuzzMovie = src.IsBuzzMovie;
        dst.ElapsedSeconds = src.ElapsedSeconds;
        dst.AccumulatedViews = src.AccumulatedViews;
        dst.PendingMonetizedViews = src.PendingMonetizedViews;
        dst.PendingViewFraction = src.PendingViewFraction;
        dst.PendingMoneyFraction = src.PendingMoneyFraction;
        dst.LastMoneyDeltaDisplay = src.LastMoneyDeltaDisplay;
        dst.HasAppliedInitialViews = src.HasAppliedInitialViews;
        dst.FiveSecondTickCount = src.FiveSecondTickCount;
        dst.MoneyTickAccumulator = src.MoneyTickAccumulator;
        dst.IsMoneyAddFloating = src.IsMoneyAddFloating;
        dst.MoneyAddFloatingValue = src.MoneyAddFloatingValue;
        dst.MoneyAddFloatingElapsedSeconds = src.MoneyAddFloatingElapsedSeconds;
        dst.IsBuzzEffectActive = src.IsBuzzEffectActive;
        dst.IsBuzzEffectStopRequested = src.IsBuzzEffectStopRequested;
        dst.BuzzEffectCycleElapsedSeconds = src.BuzzEffectCycleElapsedSeconds;
        dst.IsBuzzBgmRequested = src.IsBuzzBgmRequested;
    }

    private int ResolveActiveSlotCount()
    {
        int count = 0;
        for (int i = 0; i < slotViews.Length; i++)
        {
            SlotView v = slotViews[i];
            if (v != null && v.Root != null && IsWorkplaceAvailable(i + 1))
            {
                count += 1;
            }
        }

        return Mathf.Clamp(count, 1, sessions.Length);
    }

    private bool IsWorkplaceAvailable(int targetSlotIndex)
    {
        SlotView v = slotViews != null && targetSlotIndex - 1 >= 0 && targetSlotIndex - 1 < slotViews.Length
            ? slotViews[targetSlotIndex - 1]
            : null;
        Game02.WorkMovieSlotController controller = v != null && v.Root != null
            ? v.Root.GetComponent<Game02.WorkMovieSlotController>()
            : null;
        if (controller == null)
        {
            return true;
        }

        return controller.IsWorkplaceAvailable;
    }

    private void AdvanceOneViewsTick()
    {
        int maxActive = ResolveActiveSlotCount();
        for (int i = 0; i < maxActive; i++)
        {
            SlotSession session = sessions[i];
            if (session == null || !session.IsWorking)
            {
                continue;
            }

            long deltaViews = ComputeViewsDeltaForTick(session);
            if (deltaViews < 0L)
            {
                deltaViews = 0L;
            }

            if (deltaViews > 0L)
            {
                session.AccumulatedViews += deltaViews;
                session.PendingMonetizedViews += deltaViews;
                totalViews += deltaViews;
                ApplyPopularityDelta(deltaViews, session.IsBuzzMovie);
                Game02.Game02AlienProgressTracker.EnsureExists()?.NotifyMovieAccumulatedViews(session.AccumulatedViews);
            }

            session.ElapsedSeconds += Mathf.Max(0.01f, viewsTickSeconds);
            session.FiveSecondTickCount += 1;
            session.MoneyTickAccumulator += 1;
            if (session.IsBuzzEffectActive &&
                !session.IsBuzzEffectStopRequested &&
                session.ElapsedSeconds >= Mathf.Max(0.01f, buzzEffectStage1EndSeconds))
            {
                session.IsBuzzEffectStopRequested = true;
            }

            if (session.MoneyTickAccumulator * Mathf.Max(0.01f, viewsTickSeconds) >= Mathf.Max(0.01f, moneyTickSeconds))
            {
                session.MoneyTickAccumulator = 0;
                ProcessMoneyPayout(session);
            }
        }

        UpdateViewsUiText();
    }

    private long ComputeViewsDeltaForTick(SlotSession session)
    {
        long p = Math.Max(1L, session.MoviePopularityBase);
        long currentPopularity = Math.Max(0L, Game02.GameManager.Instance != null ? Game02.GameManager.Instance.CurrentPopularity : 0L);
        long delta = 0L;
        if (!session.HasAppliedInitialViews)
        {
            session.HasAppliedInitialViews = true;
            long initialViews = ComputeInitialViewsDelta(p);
            if (initialViews > 0L)
            {
                delta += initialViews;
            }
        }

        double phaseGain = ResolvePhaseViewsGain();
        double randomFactor = UnityEngine.Random.Range(
            Mathf.Min(randomViewsFactorMin, randomViewsFactorMax),
            Mathf.Max(randomViewsFactorMin, randomViewsFactorMax));
        double secondsScale = Mathf.Max(0.01f, viewsTickSeconds) / 5d;
        float popularityDecadeDamping = ResolveViewsDecadeDamping(currentPopularity);
        double baseViewsTick = Math.Max(0d, viewsBaseA) * Math.Pow(Math.Max(1d, p), Math.Max(0.01f, viewsBeta));
        double tickRaw = Math.Max(0d, baseViewsTick * phaseGain * randomFactor * secondsScale * popularityDecadeDamping);
        session.PendingViewFraction += tickRaw;
        long tickDelta = (long)Math.Floor(session.PendingViewFraction);
        if (tickDelta > 0L)
        {
            session.PendingViewFraction -= tickDelta;
        }
        if (tickDelta < 0L)
        {
            tickDelta = 0L;
        }

        if (session.IsBuzzMovie)
        {
            tickDelta = MultiplyAndRound(tickDelta, ResolveBuzzMultiplier());
        }

        delta += tickDelta;
        return delta;
    }

    private long ComputeInitialViewsDelta(long popularity)
    {
        double p = Math.Max(1d, popularity);
        double exponent = Math.Max(0.01f, initialViewsPopularityExponent);
        double baseMultiplier = initialViewsBaseMultiplier > 0f ? initialViewsBaseMultiplier : 4.79f;
        double rootBased = Math.Max(0d, baseMultiplier) * Math.Pow(p, exponent);

        long cap = Math.Max(1L, initialViewsBoostPopularityCap);
        double t = Mathf.Clamp01((float)(Math.Max(0L, popularity) / (double)cap));
        double lowPopBoost = Mathf.Lerp(
            Mathf.Max(1f, initialViewsLowPopularityBoostAtZero),
            1f,
            (float)t);

        int offset = UnityEngine.Random.Range(
            Math.Min(initialViewsOffsetMin, initialViewsOffsetMax),
            Math.Max(initialViewsOffsetMin, initialViewsOffsetMax) + 1);

        double raw = Math.Max(0d, (rootBased * lowPopBoost) + offset);

        // 低人気帯の初動不足を防ぐ最低保証。人気100で約80、人気170で約136を下限目安にする。
        double guaranteedRate = Mathf.Clamp(initialViewsLowPopularityGuaranteedRate, 0f, 2f);
        long guaranteedMin = 0L;
        if (popularity <= cap)
        {
            guaranteedMin = (long)Math.Floor(Math.Max(0d, popularity * guaranteedRate));
        }

        long result = (long)Math.Floor(raw);
        if (result < guaranteedMin)
        {
            result = guaranteedMin;
        }

        float seoMultiplier = ResolveSeoInitialViewsMultiplier();
        if (seoMultiplier > 1f && result > 0L)
        {
            result = MultiplyAndRound(result, seoMultiplier);
        }

        if (debugLog)
        {
            Debug.Log($"[WorkMovieUpload] initialViews calc popularity={popularity} result={result} guaranteedMin={guaranteedMin} raw={raw:0.###} multiplier={baseMultiplier:0.###} exp={exponent:0.###} boost={lowPopBoost:0.###} offset={offset}");
        }

        return result;
    }

    private static float ResolveSeoInitialViewsMultiplier()
    {
        Game02.UpgradesManager upgrades = Game02.UpgradesManager.Instance;
        if (upgrades == null)
        {
            return 1f;
        }

        return Mathf.Max(1f, upgrades.GetSeoInitialViewerMultiplier());
    }

    private double ComputeTargetViewsAt(float elapsedSeconds, long popularity)
    {
        double p = Math.Max(0L, popularity);
        if (elapsedSeconds <= 0f)
        {
            return 0d;
        }

        if (elapsedSeconds <= 100f)
        {
            double t = elapsedSeconds / 100d;
            return 0.35d * p * t;
        }

        if (elapsedSeconds <= 180f)
        {
            double t = (elapsedSeconds - 100d) / 80d;
            return (0.35d * p) + ((0.5d * p - 0.35d * p) * t);
        }

        double lateSeconds = elapsedSeconds - 180d;
        double lateTicks = lateSeconds / Mathf.Max(0.01f, viewsTickSeconds);
        double lateBase = p / 100d;
        return 0.5d * p + (lateTicks * lateBase);
    }

    private float ResolveStageRandomFactor(float nextElapsedSeconds)
    {
        if (nextElapsedSeconds <= 100f)
        {
            return UnityEngine.Random.Range(
                Mathf.Min(earlyRandomFactorMin, earlyRandomFactorMax),
                Mathf.Max(earlyRandomFactorMin, earlyRandomFactorMax));
        }

        if (nextElapsedSeconds <= 180f)
        {
            return UnityEngine.Random.Range(
                Mathf.Min(midRandomFactorMin, midRandomFactorMax),
                Mathf.Max(midRandomFactorMin, midRandomFactorMax));
        }

        return UnityEngine.Random.Range(
            Mathf.Min(lateRandomFactorMin, lateRandomFactorMax),
            Mathf.Max(lateRandomFactorMin, lateRandomFactorMax));
    }

    private void ApplyPopularityDelta(long deltaViews, bool isBuzzMovie)
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null || deltaViews <= 0L)
        {
            return;
        }

        double currentPopularity = Math.Max(0L, gm.CurrentPopularity);
        double damping = 1d + (currentPopularity / Math.Max(1d, popularityDampingC));
        double raw = (deltaViews * Math.Max(0f, basePopularityGainRate)) / damping;
        float randomFactor = UnityEngine.Random.Range(
            Mathf.Min(popularityRandomFactorMin, popularityRandomFactorMax),
            Mathf.Max(popularityRandomFactorMin, popularityRandomFactorMax));
        double phaseGain = ResolvePhasePopularityGain();
        long deltaPopularity = (long)Math.Round(raw * randomFactor * phaseGain);
        if (isBuzzMovie)
        {
            deltaPopularity = MultiplyAndRound(deltaPopularity, ResolveBuzzMultiplier());
        }

        if (deltaPopularity > 0L)
        {
            gm.RequestPopularityDelta(deltaPopularity, "WorkMovie views->popularity", "WorkMovie_1");
        }
    }

    private void ProcessMoneyPayout(SlotSession session)
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null || session == null)
        {
            return;
        }

        long payableUnits = session.PendingMonetizedViews / 100L;
        long payableViews = payableUnits * 100L;
        if (payableViews > 0L)
        {
            session.PendingMonetizedViews -= payableViews;
        }

        double rate = ResolveMoneyRateByPopularity(session.MoviePopularityBase);
        double moneyRaw = payableViews * rate;
        if (session.IsBuzzMovie)
        {
            moneyRaw *= ResolveBuzzMultiplier();
        }
        moneyRaw *= ResolvePhaseMoneyGain();
        moneyRaw *= ResolveMoneyDecadeDamping(Math.Max(0L, gm.CurrentPopularity));

        if (session.IsVeryFirstUploadedMovie && !session.HasAppliedFirstIncomeBonus)
        {
            if (gm.TryConsumeFirstUploadFirstIncomeBonus(out long bonusMoney))
            {
                moneyRaw += Math.Max(0L, bonusMoney);
            }

            session.HasAppliedFirstIncomeBonus = true;
        }

        moneyRaw += session.PendingMoneyFraction;
        long deltaMoney = (long)Math.Floor(Math.Max(0d, moneyRaw));
        session.PendingMoneyFraction = Math.Max(0d, moneyRaw - deltaMoney);

        int payoutTimingIndex = session.MoneyPayoutCount + 1;
        if (payoutTimingIndex == Mathf.Max(1, fixedIncomeTimingIndex) &&
            !session.HasAppliedFixedIncomeAtTenthTiming)
        {
            session.HasAppliedFixedIncomeAtTenthTiming = true;
            deltaMoney += Math.Max(0L, fixedIncomeAmount);
        }

        session.MoneyPayoutCount = payoutTimingIndex;
        session.LastMoneyDeltaDisplay = deltaMoney;
        session.IsMoneyAddFloating = false;
        session.MoneyAddFloatingValue = 0L;
        session.MoneyAddFloatingElapsedSeconds = 0f;
        TryPlayMoneyAddUiForSession(session, deltaMoney);

        // 0円でも処理タイミングを通す（仕様）。
        gm.RequestMoneyDelta(deltaMoney, "WorkMovie payout", "WorkMovie_1");
        if (debugLog)
        {
            Debug.Log($"[WorkMovieUpload] payout views={payableViews} money={deltaMoney} carry={session.PendingMoneyFraction:0.###}");
        }
    }

    private static double ResolveMoneyRateByPopularity(long popularity)
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null)
        {
            return 0.2d;
        }

        return gm.ResolveMovieUnitPriceByPopularity(popularity);
    }

    private static long MultiplyAndRound(long value, float factor)
    {
        double raw = value * Math.Max(0f, factor);
        if (raw >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)Math.Round(raw);
    }

    private static float ResolveBuzzMultiplier()
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        float baseMultiplier = gm != null ? Mathf.Max(0f, gm.BuzzMultiplier) : 1f;
        int trendLevel = ResolveTrendUpgradeLevel();
        return Mathf.Max(0f, baseMultiplier + (0.2f * trendLevel));
    }

    private static int ResolveTrendUpgradeLevel()
    {
        Game02.UpgradesManager upgrades = Game02.UpgradesManager.Instance;
        if (upgrades == null)
        {
            return 0;
        }

        return Mathf.Max(0, upgrades.GetTrendPowerUpgradeLevel());
    }

    private static long ComputeTrendAdjustedBuzzGain(long baseBuzzGain, int trendLevel)
    {
        long safeBase = Math.Max(0L, baseBuzzGain);
        long safeTrend = Math.Max(0L, trendLevel);
        long sum;
        try
        {
            checked
            {
                sum = safeBase + safeTrend;
            }
        }
        catch (OverflowException)
        {
            sum = long.MaxValue;
        }

        return Math.Max(0L, sum);
    }

    private float ResolveViewsDecadeDamping(long popularity)
    {
        return ResolveDecadeDamping(popularity, viewsDampingByPopularityDecade);
    }

    private float ResolveMoneyDecadeDamping(long popularity)
    {
        return ResolveDecadeDamping(popularity, moneyDampingByPopularityDecade);
    }

    private float ResolveDecadeDamping(long popularity, float[] table)
    {
        if (!enableDecadeDamping || table == null || table.Length == 0)
        {
            return 1f;
        }

        double safePopularity = Math.Max(1d, popularity);
        int decade = (int)Math.Floor(Math.Log10(safePopularity));
        int index = Mathf.Clamp(decade - 2, 0, table.Length - 1);
        return Mathf.Max(0f, table[index]);
    }

    private float ResolvePhaseViewsGain()
    {
        int band = ResolvePhaseBand();
        if (band == 0)
        {
            return Mathf.Max(0f, phaseViewsGainEarly);
        }

        if (band == 1)
        {
            return Mathf.Max(0f, phaseViewsGainMid);
        }

        return Mathf.Max(0f, phaseViewsGainLate);
    }

    private float ResolvePhasePopularityGain()
    {
        int band = ResolvePhaseBand();
        if (band == 0)
        {
            return Mathf.Max(0f, phasePopularityGainEarly);
        }

        if (band == 1)
        {
            return Mathf.Max(0f, phasePopularityGainMid);
        }

        return Mathf.Max(0f, phasePopularityGainLate);
    }

    private float ResolvePhaseMoneyGain()
    {
        int band = ResolvePhaseBand();
        if (band == 0)
        {
            return Mathf.Max(0f, phaseMoneyGainEarly);
        }

        if (band == 1)
        {
            return Mathf.Max(0f, phaseMoneyGainMid);
        }

        return Mathf.Max(0f, phaseMoneyGainLate);
    }

    private int ResolvePhaseBand()
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null)
        {
            return 1;
        }

        float elapsedMinutes = Mathf.Max(0f, gm.GameplayElapsedSeconds) / 60f;
        float earlyEnd = Mathf.Max(0f, phaseEarlyEndMinutes);
        float midEnd = Mathf.Max(earlyEnd, phaseMidEndMinutes);
        if (elapsedMinutes < earlyEnd)
        {
            return 0;
        }

        if (elapsedMinutes < midEnd)
        {
            return 1;
        }

        return 2;
    }

    private void RefreshAllSlotVisuals()
    {
        EnsureSlotViewsUiGoalCache();
        int maxActive = ResolveActiveSlotCount();
        for (int i = 0; i < slotViews.Length; i++)
        {
            SlotView view = slotViews[i];
            SlotSession session = sessions[i];
            if (view == null || view.Root == null)
            {
                continue;
            }

            if (i >= maxActive)
            {
                if (view.HoverHit != null)
                {
                    view.HoverHit.SetActive(false);
                }

                if (view.HoverContentProvider != null)
                {
                    view.HoverContentProvider.SetContent(view.HoverContentProvider.GetTitle(), FallbackMovieName);
                }

                UpdateBuzzEffectVisual(view, session, false);
                slotViewsUiLastGoalPushed[i] = long.MinValue;

                continue;
            }

            bool working = session != null && session.IsWorking;
            string movieName = SanitizeMovieName(session != null ? session.MovieName : string.Empty);
            if (view.HoverHit != null)
            {
                view.HoverHit.SetActive(working);
            }

            if (view.HoverContentProvider != null)
            {
                view.HoverContentProvider.SetContent(view.HoverContentProvider.GetTitle(), working ? movieName : FallbackMovieName);
            }

            ApplySlotRaycastPolicy(view, working);
            UpdateBuzzEffectVisual(view, session, working);

            if (view.AcceptedItemView != null)
            {
                view.AcceptedItemView.sprite = working ? session.DisplaySprite : null;
                view.AcceptedItemView.enabled = working && session.DisplaySprite != null;
                if (working && session.DisplaySprite != null)
                {
                    Color c = view.AcceptedItemView.color;
                    if (c.a <= 0f)
                    {
                        c.a = 1f;
                        view.AcceptedItemView.color = c;
                    }
                }
            }
            if (view.AcceptedItemViewBounce != null)
            {
                view.AcceptedItemViewBounce.SetCarrierActive(working && session.DisplaySprite != null);
            }

            if (view.AcceptedItemViewBreathingScale != null)
            {
                view.AcceptedItemViewBreathingScale.SetCarrierActive(working && session.DisplaySprite != null);
            }

            if (view.ImageTimeF != null)
            {
                EnsureImageSpriteIfMissing(view.ImageTimeF);
                view.ImageTimeF.gameObject.SetActive(working);
            }

            if (view.ImageTimeB != null)
            {
                EnsureImageSpriteIfMissing(view.ImageTimeB);
                view.ImageTimeB.gameObject.SetActive(working);
                if (working)
                {
                    float t = Mathf.Clamp01(session.ElapsedSeconds / 181f);
                    view.ImageTimeB.color = Color.Lerp(Color.blue, Color.red, t);
                }
            }

            bool enableDirectionEffects = working && session.DisplaySprite != null;
            float visualElapsedSeconds = ResolveVisualElapsedSeconds(session);
            if (view.DirectionView01RiseArrow != null)
            {
                view.DirectionView01RiseArrow.SetStageElapsedSeconds(working ? visualElapsedSeconds : 0f);
            }
            if (view.DirectionView02CommentBalloon != null)
            {
                view.DirectionView02CommentBalloon.SetStageElapsedSeconds(working ? visualElapsedSeconds : 0f);
            }
            view.DirectionView01?.SetCarrierActive(enableDirectionEffects);
            view.DirectionView02?.SetCarrierActive(enableDirectionEffects);
            view.DirectionView03?.SetCarrierActive(enableDirectionEffects);
            if (view.DirectionView04StageSpin != null)
            {
                view.DirectionView04StageSpin.SetStageElapsedSeconds(working ? visualElapsedSeconds : 0f);
            }
            view.DirectionView04?.SetCarrierActive(enableDirectionEffects);

            if (view.ViewsUiText != null)
            {
                view.ViewsUiText.gameObject.SetActive(working);
                if (!working)
                {
                    slotViewsUiLastGoalPushed[i] = long.MinValue;
                }
                else
                {
                    long slotViews = Math.Max(0L, session.AccumulatedViews);
                    EnsureViewsCounterRollOnView(view);
                    if (view.ViewsCounterRoll != null)
                    {
                        if (slotViewsUiLastGoalPushed[i] != slotViews)
                        {
                            view.ViewsCounterRoll.SetTarget(slotViews);
                            slotViewsUiLastGoalPushed[i] = slotViews;
                        }
                    }
                    else
                    {
                        view.ViewsUiText.text = slotViews.ToString("N0");
                        slotViewsUiLastGoalPushed[i] = slotViews;
                    }
                }
            }

            if (view.MoneyAddUiText != null)
            {
                if (!working)
                {
                    ClearAddUiByManager(view.MoneyAddUiText);
                }
            }
        }
    }

    private void UpdateViewsUiText()
    {
        if (viewsUiText == null)
        {
            return;
        }

        PrepareSlotViewsIfMissing();
        if (IsViewsUiTextUsedAsSlotViewsCounter(viewsUiText))
        {
            lastTotalViewsUiPushed = totalViews;
            return;
        }

        if (viewsUiRoll == null)
        {
            viewsUiRoll = viewsUiText.GetComponent<Game02.TmpLongCounterRollPresenter>();
            if (viewsUiRoll == null)
            {
                viewsUiRoll = viewsUiText.gameObject.AddComponent<Game02.TmpLongCounterRollPresenter>();
            }
        }

        if (viewsUiRoll != null)
        {
            if (lastTotalViewsUiPushed != totalViews)
            {
                viewsUiRoll.SetTarget(totalViews);
                lastTotalViewsUiPushed = totalViews;
            }
        }
        else
        {
            viewsUiText.text = totalViews.ToString("N0");
            lastTotalViewsUiPushed = totalViews;
        }
    }

    private void ApplyAcceptedItemMosaicToView(Image targetImage)
    {
        if (targetImage == null)
        {
            return;
        }

        UIMosaicEffect mosaic = targetImage.GetComponent<UIMosaicEffect>();
        if (mosaic == null)
        {
            mosaic = targetImage.gameObject.AddComponent<UIMosaicEffect>();
        }

        if (mosaic == null)
        {
            return;
        }

        mosaic.SetMosaicShaderName(acceptedItemMosaicShaderName);
        mosaic.SetMosaicPixelSize(Mathf.Max(1, acceptedItemMosaicPixelSize));
        mosaic.SetMosaicEnabled(enableAcceptedItemMosaic);
        mosaic.ApplyMosaicStateIfConfigured();
    }

    private void ApplyAcceptedItemMosaicForOwnSlot()
    {
        if (slotIndex <= 0)
        {
            return;
        }

        GameObject ownSlot = FindSlotRoot(slotIndex);
        if (ownSlot == null)
        {
            return;
        }

        Image ownAcceptedItemView = FindImageByNameRecursive(ownSlot.transform, "AcceptedItemView");
        if (ownAcceptedItemView == null)
        {
            return;
        }

        ApplyAcceptedItemMosaicToView(ownAcceptedItemView);
        ApplyAcceptedItemBreathingScaleToView(ownAcceptedItemView);
    }

    private void ApplyAcceptedItemBreathingScaleToView(Image targetImage)
    {
        if (targetImage == null)
        {
            return;
        }

        AcceptedItemViewBreathingScaleController breathing = targetImage.GetComponent<AcceptedItemViewBreathingScaleController>();
        if (breathing == null)
        {
            breathing = targetImage.gameObject.AddComponent<AcceptedItemViewBreathingScaleController>();
        }

        if (breathing == null)
        {
            return;
        }

        breathing.SetBreathingScaleMultiplier(Mathf.Max(1f, acceptedItemBreathingScaleMultiplier));
        breathing.SetBreathingCycleSeconds(Mathf.Max(0.01f, acceptedItemBreathingCycleSeconds));
        breathing.SetBreathingEnabled(enableAcceptedItemBreathingScale);
    }

    private void ApplyDirectionView04StageSpinForOwnSlot()
    {
        if (slotIndex <= 0)
        {
            return;
        }

        GameObject ownSlot = FindSlotRoot(slotIndex);
        if (ownSlot == null)
        {
            return;
        }

        Transform[] all = ownSlot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, "DirectionView04", StringComparison.Ordinal))
            {
                continue;
            }

            DirectionView04Controller oldController = t.GetComponent<DirectionView04Controller>();
            if (oldController != null)
            {
                oldController.enabled = false;
            }

            WorkMovieDirectionView04SpinController spin = t.GetComponent<WorkMovieDirectionView04SpinController>();
            bool created = false;
            if (spin == null)
            {
                spin = t.gameObject.AddComponent<WorkMovieDirectionView04SpinController>();
                created = spin != null;
            }

            if (spin != null && created)
            {
                spin.Configure(
                    enableDirectionView04StageSpin,
                    Mathf.Max(0f, directionView04InitialRotationSpeedDegPerSec),
                    Mathf.Max(0f, directionView04FinalRotationSpeedDegPerSec),
                    Mathf.Max(0.01f, directionView04Stage1EndSeconds),
                    Mathf.Max(0.01f, directionView04Stage2EndSeconds));
            }

            break;
        }
    }

    private void ApplyDirectionView01RiseArrowForOwnSlot()
    {
        if (slotIndex <= 0)
        {
            return;
        }

        GameObject ownSlot = FindSlotRoot(slotIndex);
        if (ownSlot == null)
        {
            return;
        }

        Transform[] all = ownSlot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, "DirectionView01", StringComparison.Ordinal))
            {
                continue;
            }

            DirectionView01Controller oldController = t.GetComponent<DirectionView01Controller>();
            if (oldController != null)
            {
                oldController.enabled = false;
            }

            WorkMovieDirectionView01RiseArrowController riseArrow = t.GetComponent<WorkMovieDirectionView01RiseArrowController>();
            bool created = false;
            if (riseArrow == null)
            {
                riseArrow = t.gameObject.AddComponent<WorkMovieDirectionView01RiseArrowController>();
                created = riseArrow != null;
            }

            if (riseArrow != null && created)
            {
                riseArrow.Configure(
                    enableDirectionView01RiseArrow,
                    directionView01MoveDistance,
                    Mathf.Max(0.01f, directionView01MoveDurationSeconds),
                    Mathf.Max(0f, directionView01FadeInSeconds),
                    Mathf.Max(0f, directionView01FadeOutSeconds),
                    Mathf.Max(0.01f, directionView01Stage1SpawnIntervalSeconds),
                    Mathf.Max(0.01f, directionView01Stage2SpawnIntervalSeconds),
                    Mathf.Max(0.01f, directionView01Stage3SpawnIntervalSeconds),
                    Mathf.Max(0.01f, directionView01Stage1EndSeconds),
                    Mathf.Max(0.01f, directionView01Stage2EndSeconds),
                    directionView01Stage1Color,
                    directionView01Stage2Color,
                    directionView01Stage3Color);
            }

            break;
        }
    }

    private float ResolveVisualElapsedSeconds(SlotSession session)
    {
        if (session == null || !session.IsWorking)
        {
            return 0f;
        }

        float carry = Mathf.Clamp(viewsTickAccumulator, 0f, Mathf.Max(0.01f, viewsTickSeconds));
        return Mathf.Max(0f, session.ElapsedSeconds + carry);
    }

    private void TryPlayMoneyAddUiForSession(SlotSession session, long deltaMoney)
    {
        if (session == null || deltaMoney <= 0L)
        {
            return;
        }

        int slot = ResolveSlotIndexBySession(session);
        if (slot < 0 || slot >= slotViews.Length)
        {
            return;
        }

        SlotView view = slotViews[slot];
        TMP_Text target = view != null ? view.MoneyAddUiText : null;
        if (target == null)
        {
            return;
        }

        if (game02EffectManager != null && game02EffectManager.PlayAddUi(target, deltaMoney))
        {
            return;
        }

        if (!target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
        }

        target.text = $"+{deltaMoney:N0}";
        Color color = target.color;
        color.a = 1f;
        target.color = color;
    }

    private void ClearAddUiByManager(TMP_Text target)
    {
        if (target == null)
        {
            return;
        }

        if (game02EffectManager != null && game02EffectManager.ClearAddUi(target))
        {
            return;
        }

        target.text = string.Empty;
        Color color = target.color;
        color.a = 0f;
        target.color = color;
        if (target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(false);
        }
    }

    private int ResolveSlotIndexBySession(SlotSession session)
    {
        for (int i = 0; i < sessions.Length; i++)
        {
            if (ReferenceEquals(sessions[i], session))
            {
                return i;
            }
        }

        return -1;
    }

    public Game02.WorkMovieUploadState CaptureSaveState()
    {
        EnsureSessionObjects();
        var state = new Game02.WorkMovieUploadState
        {
            totalViews = totalViews,
            viewsTickAccumulator = viewsTickAccumulator,
            hasAssignedVeryFirstUploadedMovie = hasAssignedVeryFirstUploadedMovie
        };

        for (int i = 0; i < sessions.Length; i++)
        {
            SlotSession s = sessions[i];
            if (s == null)
            {
                continue;
            }

            state.sessions.Add(new Game02.WorkMovieUploadSessionState
            {
                index = i,
                isWorking = s.IsWorking,
                isVeryFirstUploadedMovie = s.IsVeryFirstUploadedMovie,
                hasAppliedFirstIncomeBonus = s.HasAppliedFirstIncomeBonus,
                moneyPayoutCount = s.MoneyPayoutCount,
                hasAppliedFixedIncomeAtTenthTiming = s.HasAppliedFixedIncomeAtTenthTiming,
                movieName = s.MovieName ?? string.Empty,
                displaySpriteName = s.DisplaySprite != null ? s.DisplaySprite.name : string.Empty,
                moviePopularityBase = s.MoviePopularityBase,
                movieBuzzGain = s.MovieBuzzGain,
                isBuzzMovie = s.IsBuzzMovie,
                elapsedSeconds = s.ElapsedSeconds,
                accumulatedViews = s.AccumulatedViews,
                pendingMonetizedViews = s.PendingMonetizedViews,
                pendingViewFraction = s.PendingViewFraction,
                pendingMoneyFraction = s.PendingMoneyFraction,
                lastMoneyDeltaDisplay = s.LastMoneyDeltaDisplay,
                hasAppliedInitialViews = s.HasAppliedInitialViews,
                fiveSecondTickCount = s.FiveSecondTickCount,
                moneyTickAccumulator = s.MoneyTickAccumulator,
                isMoneyAddFloating = false,
                moneyAddFloatingValue = 0L,
                moneyAddFloatingElapsedSeconds = 0f,
                isBuzzEffectActive = s.IsBuzzEffectActive,
                isBuzzEffectStopRequested = s.IsBuzzEffectStopRequested,
                buzzEffectCycleElapsedSeconds = s.BuzzEffectCycleElapsedSeconds,
                isBuzzBgmRequested = s.IsBuzzBgmRequested
            });
        }

        return state;
    }

    public void ApplySaveState(Game02.WorkMovieUploadState state)
    {
        if (state == null)
        {
            return;
        }

        PrepareSlotViewsIfMissing();
        EnsureSessionObjects();
        totalViews = Math.Max(0L, state.totalViews);
        viewsTickAccumulator = Mathf.Max(0f, state.viewsTickAccumulator);
        hasAssignedVeryFirstUploadedMovie = state.hasAssignedVeryFirstUploadedMovie;

        for (int i = 0; i < sessions.Length; i++)
        {
            sessions[i] = new SlotSession();
        }

        if (state.sessions != null)
        {
            for (int i = 0; i < state.sessions.Count; i++)
            {
                Game02.WorkMovieUploadSessionState src = state.sessions[i];
                if (src == null || src.index < 0 || src.index >= sessions.Length)
                {
                    continue;
                }

                SlotSession dst = sessions[src.index];
                dst.IsWorking = src.isWorking;
                dst.IsVeryFirstUploadedMovie = src.isVeryFirstUploadedMovie;
                dst.HasAppliedFirstIncomeBonus = src.hasAppliedFirstIncomeBonus;
                dst.MoneyPayoutCount = Mathf.Max(0, src.moneyPayoutCount);
                dst.HasAppliedFixedIncomeAtTenthTiming = src.hasAppliedFixedIncomeAtTenthTiming;
                dst.MovieName = src.movieName ?? string.Empty;
                dst.DisplaySprite = Game02.Game02SpriteResolver.ResolveByName(src.displaySpriteName);
                dst.MoviePopularityBase = Math.Max(0L, src.moviePopularityBase);
                dst.MovieBuzzGain = Math.Max(0L, src.movieBuzzGain);
                dst.IsBuzzMovie = src.isBuzzMovie;
                dst.ElapsedSeconds = Mathf.Max(0f, src.elapsedSeconds);
                dst.AccumulatedViews = Math.Max(0L, src.accumulatedViews);
                dst.PendingMonetizedViews = Math.Max(0L, src.pendingMonetizedViews);
                dst.PendingViewFraction = Math.Max(0d, src.pendingViewFraction);
                dst.PendingMoneyFraction = Math.Max(0d, src.pendingMoneyFraction);
                dst.LastMoneyDeltaDisplay = Math.Max(0L, src.lastMoneyDeltaDisplay);
                dst.HasAppliedInitialViews = src.hasAppliedInitialViews;
                dst.FiveSecondTickCount = Mathf.Max(0, src.fiveSecondTickCount);
                dst.MoneyTickAccumulator = Mathf.Max(0, src.moneyTickAccumulator);
                dst.IsMoneyAddFloating = false;
                dst.MoneyAddFloatingValue = 0L;
                dst.MoneyAddFloatingElapsedSeconds = 0f;
                dst.IsBuzzEffectActive = src.isBuzzEffectActive;
                dst.IsBuzzEffectStopRequested = src.isBuzzEffectStopRequested;
                dst.BuzzEffectCycleElapsedSeconds = Mathf.Max(0f, src.buzzEffectCycleElapsedSeconds);
                dst.IsBuzzBgmRequested = src.isBuzzBgmRequested;
            }
        }

        SnapViewsCounterUiToSessionTruth();
        RefreshAllSlotVisuals();
        UpdateViewsUiText();
    }
}
