using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game02
{
    public class GameManager : MonoBehaviour
    {
        private struct MoneyOperation
        {
            public readonly long Delta;
            public readonly string Reason;
            public readonly string SourceId;
            public readonly long OperationId;

            public MoneyOperation(long delta, string reason, string sourceId, long operationId)
            {
                Delta = delta;
                Reason = reason;
                SourceId = sourceId;
                OperationId = operationId;
            }
        }

        private struct PopularityOperation
        {
            public readonly long Delta;
            public readonly string Reason;
            public readonly string SourceId;
            public readonly long OperationId;

            public PopularityOperation(long delta, string reason, string sourceId, long operationId)
            {
                Delta = delta;
                Reason = reason;
                SourceId = sourceId;
                OperationId = operationId;
            }
        }

        private struct BuzzOperation
        {
            public readonly long Delta;
            public readonly string Reason;
            public readonly string SourceId;
            public readonly long OperationId;

            public BuzzOperation(long delta, string reason, string sourceId, long operationId)
            {
                Delta = delta;
                Reason = reason;
                SourceId = sourceId;
                OperationId = operationId;
            }
        }

        public static GameManager Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private ItemSpawnController itemSpawnController;
        [SerializeField] private Game02EffectManager game02EffectManager;
        [Tooltip("メイン所持金表示用 TMP_Text（各シーンでインスペクター割り当て。game02 既定: PanelCanvas/MoneyUIBackground/MoneyUI の TextMeshProUGUI）")]
        [SerializeField] private TMP_Text moneyUiText;
        [Tooltip("所持金増加分ポップ用 TMP_Text（game02 既定: PanelCanvas/MoneyUIBackground/MoneyAddUI）")]
        [SerializeField] private TMP_Text moneyAddUiText;
        [Tooltip("メイン人気表示用 TMP_Text（game02 既定: PanelCanvas/PopularUIBackground/PopularUI）")]
        [SerializeField] private TMP_Text popularUiText;
        [Tooltip("人気増加分ポップ用 TMP_Text（game02 既定: PanelCanvas/PopularUIBackground/PopularAddUI）")]
        [SerializeField] private TMP_Text popularAddUiText;
        [Tooltip("デバッグ用バズ表示 TMP_Text（game02 既定: PanelCanvas/PanelObject/ButtonDebugBuzz/BuzzView）")]
        [SerializeField] private TMP_Text buzzUiText;
        [Tooltip("ゲームクリア表示パネル（既定: PanelCanvas/GameClearedPanel）")]
        [SerializeField] private GameObject gameClearedPanel;

        [Header("Runtime")]
        [SerializeField] private float eventCheckIntervalSeconds = 0.5f;
        [SerializeField] private bool isPaused;

        /// <summary>一時停止中でも PausePanel を出さない（GameMenuButton 経由のシーン遷移など）。</summary>
        private bool suppressPausePanelWhilePaused;

        private static readonly float[] GameSpeedMultipliers = { 1f, 3f, 5f, 10f };
        private int gameSpeedStepIndex;

        [Header("Money")]
        [SerializeField] private long initialMoney = 0;
        [SerializeField] private long maxMoney = 999999999;
        [Tooltip("プレイ中の現在所持金（実行時のみ更新・表示用。ここを編集してもゲームロジックには反映されません）。")]
        [SerializeField] private long inspectorRuntimeCurrentMoney;

        [Header("Popular")]
        [SerializeField] private long initialPopularity = 0;
        [SerializeField] private long maxPopularity = 999999999;
        [Tooltip("プレイ中の現在の人気（実行時のみ更新・表示用。ここを編集してもゲームロジックには反映されません）。")]
        [SerializeField] private long inspectorRuntimeCurrentPopularity;

        [Header("Buzz")]
        [SerializeField] private long initialBuzz = 50;
        [SerializeField] private long maxBuzz = 999999999;
        [SerializeField] private long buzzThreshold = 60;
        [SerializeField] private float buzzMultiplier = 4f;
        [SerializeField] private float[] buzzCorrectionList =
        {
            0.0f, 0.8f, 0.9f, 0.9f, 0.9f, 1.0f, 1.0f, 1.0f,
            1.0f, 1.0f, 1.1f, 1.1f, 1.2f, 1.3f, 1.4f, 2.0f
        };

        [Header("Money Operation")]
        [SerializeField] private bool enableOperationDeduplication = true;
        [SerializeField] private int processedOperationCapacity = 2048;

        [Header("Debug Spawn")]
        [SerializeField] private float debugItemStreamSpawnAboveTopPixels = 100f;
        [Header("Debug Buzz")]
        [SerializeField] private bool enableDebugBuzzConfetti = true;
        [Header("Debug UI Sync")]
        [SerializeField] private bool enableBootMoneyPopularityLogs = false;
        [SerializeField] private bool autoRecoverMainUiTextRefs = true;
        [SerializeField] private bool forceSyncNamedMoneyPopularityUi = true;
        [Header("First Edit Override")]
        [SerializeField] private bool enableFirstEditSecondsOverride = false;
        [SerializeField] private float firstEditSeconds = 15f;
        [Header("First Upload First Income Bonus")]
        [SerializeField] private bool enableFirstUploadFirstIncomeBonus = true;
        [SerializeField] private long firstUploadFirstIncomeBonusAmount = 800L;
        [Header("Early Upload Support")]
        [SerializeField] private long supportPopularityCap = 3000L;
        [SerializeField] private long supportMoneyPerUpload = 220L;
        [SerializeField] private string supportEndedMessage = "運営の援助がなくなりました";
        [Header("Stage Start Sequence")]
        [SerializeField, Tooltip("開始演出の完了通知が来ない場合の入力停止フェイルセーフ秒数。0 以下で無効。")]
        private float preGameSequenceSafetyTimeoutSeconds = 3f;
        [Header("Incoming Fade Cleanup")]
        [SerializeField, Tooltip("menu/title から持ち越された FadeCanvas が黒で残る場合に、入場時に明るく戻す秒数。0 以下で無効。")]
        private float incomingMenuFadeOutSeconds = 0.5f;
        [Header("Movie Unit Price Tiers")]
        [SerializeField] private long movieRateTierStage1Popularity = 10_000L;
        [SerializeField] private long movieRateTierStage2Popularity = 100_000L;
        [SerializeField] private long movieRateTierGoalPopularity = 1_000_000L;
        [SerializeField] private double movieUnitPriceTier0 = 0.2d;
        [SerializeField] private double movieUnitPriceTier1 = 0.4d;
        [SerializeField] private double movieUnitPriceTier2 = 0.7d;
        [SerializeField] private double movieUnitPriceTierGoal = 1.0d;
        [Header("MsgOb メッセージ")]
        [SerializeField] private float movieRateTierMessageHoldSeconds = 2.2f;
        [Header("First Movie Income Message")]
        [SerializeField] private string firstMovieIncomeMessage = "初めて動画収入を得たよ！";
        [Header("Game Clear")]
        [SerializeField] private long gameClearPopularityThreshold = 1_000_000L;

        private bool hasRunInitialAwakeCheck;
        private bool hasFatalError;
        private float gameplayElapsedSeconds;
        private float eventCheckAccumulator;
        private long currentMoney;
        private long autoIssuedMoneyOperationId;
        private readonly Queue<MoneyOperation> pendingMoneyOperations = new Queue<MoneyOperation>();
        private readonly HashSet<long> processedMoneyOperationIds = new HashSet<long>();
        private readonly Queue<long> processedMoneyOperationOrder = new Queue<long>();
        private long queuedMoneyDeltaPreview;

        private long currentPopularity;
        private long autoIssuedPopularityOperationId;
        private readonly Queue<PopularityOperation> pendingPopularityOperations = new Queue<PopularityOperation>();
        private readonly HashSet<long> processedPopularityOperationIds = new HashSet<long>();
        private readonly Queue<long> processedPopularityOperationOrder = new Queue<long>();

        private long currentBuzz;
        private long autoIssuedBuzzOperationId;
        private readonly Queue<BuzzOperation> pendingBuzzOperations = new Queue<BuzzOperation>();
        private readonly HashSet<long> processedBuzzOperationIds = new HashSet<long>();
        private readonly Queue<long> processedBuzzOperationOrder = new Queue<long>();
        private bool runtimeSessionInitialized;
        private int lastObservedFrameCount = -1;
        private bool hasConsumedFirstEditSecondsOverride;
        private int lifetimeEditWorkSessionsStarted;
        private bool hasConsumedFirstUploadFirstIncomeBonus;
        private ulong popularityMilestoneMsgShownMask;
        private bool hasShownFirstFanParson01Message;
        private bool hasShownCatNeko100Message;
        private bool hasShownCatNeko1000Message;
        private bool hasShownParsonR01UnlockMessage;
        private bool hasShownBuzzMovieCount1Message;
        private bool hasShownBuzzMovieCount10Message;
        private bool hasShownGameplayElapsed1HourMessage;
        private bool hasShownGameplayElapsed100HourMessage;
        private bool hasShownFirstTrashDropMessage;
        private bool hasShownFirstWorkEditorExtAssignMessage;
        private bool hasShownWorkEditorExt15MinutesMessage;
        private bool hasShownFirstWorkMovieSlot1UploadMessage;
        private bool hasShownFirstMailCharaWorkEditor1Message;
        private bool hasShownFirstItemEditorWorkEditor1Message;
        private bool hasShownFirstEd51AutomationMessage;
        private bool hasShownFirstWorkUpgradeStMessage;
        private bool hasShownFirstMovieIncomeMessage;
        private bool hasShownSupportEndedMessage;
        private bool hasReceivedSupportReward;
        private bool hasShownGameClearedPanel;
        private bool isPreGameSequenceActive;
        private float preGameSequenceSafetyElapsedSeconds;
        private float activePreGameSequenceSafetyTimeoutSeconds;
        private bool gameplayTimerStarted;
        private bool isGameCleared;
        private bool hasAppliedSaveState;
        /// <summary>セーブ読込後の強制 StageStartOverlay 完了時、KickGameplayStart で isGameCleared をリセットしない。</summary>
        private bool preserveGameClearedThroughNextKick;

        private int itemStreamSequence;
        private int itemMovieSequence;

        public bool IsPaused => isPaused;

        /// <summary>停止ボタン用 PausePanel を表示してよいとき。一時停止中かつ抑止フラグが立っていない。</summary>
        public bool ShouldShowPausePanelWhilePaused => isPaused && !suppressPausePanelWhilePaused;

        /// <summary>
        /// 一時停止または開始演出プリゲーム区間。UI の「一時停止」状態とは別に、ドラッグ等を抑止する。
        /// </summary>
        public bool ShouldSuppressPlayerInteractions => hasFatalError || isPaused || isPreGameSequenceActive;
        public int GameSpeedStepIndex => Mathf.Clamp(gameSpeedStepIndex, 0, GameSpeedMultipliers.Length - 1);
        public float GameSpeedMultiplier =>
            GameSpeedMultipliers[Mathf.Clamp(gameSpeedStepIndex, 0, GameSpeedMultipliers.Length - 1)];

        /// <summary>
        /// 一時停止・致命エラー時は 0。それ以外は <see cref="Time.deltaTime"/> × 現在のゲーム速度。
        /// </summary>
        public static float GameplayDelta =>
            Instance != null ? Instance.GetGameplayDeltaTime() : Time.deltaTime;

        public float EventCheckIntervalSeconds => Mathf.Max(0.01f, eventCheckIntervalSeconds);
        public float GameplayElapsedSeconds => gameplayElapsedSeconds;
        public bool IsGameplayTimerStarted => gameplayTimerStarted;
        public bool IsPreGameSequenceActive => isPreGameSequenceActive;
        public bool IsGameCleared => isGameCleared;
        public bool IsGameplayTimerRunning =>
            gameplayTimerStarted && !isGameCleared && !isPaused && !hasFatalError && !isPreGameSequenceActive;
        public long CurrentMoney => currentMoney;

        /// <summary>シーン設定の初期所持金（生涯獲得カウンタのベースライン用）。</summary>
        public long InitialMoney => Math.Max(0L, initialMoney);

        public long CurrentPopularity => currentPopularity;

        /// <summary>シーン／セーブ適用後のセッション初期人口気（クリア演出カウントアップ開始値）。</summary>
        public long InitialPopularity => Math.Max(0L, initialPopularity);

        public bool HasShownFirstFanParson01Message => hasShownFirstFanParson01Message;

        public bool HasShownCatNeko100Message => hasShownCatNeko100Message;

        public bool HasShownCatNeko1000Message => hasShownCatNeko1000Message;

        public bool HasShownFirstMovieIncomeMessage => hasShownFirstMovieIncomeMessage;

        internal void MarkFirstFanParson01MessageShown()
        {
            hasShownFirstFanParson01Message = true;
        }

        internal void MarkCatNeko100MessageShown()
        {
            hasShownCatNeko100Message = true;
        }

        internal void MarkCatNeko1000MessageShown()
        {
            hasShownCatNeko1000Message = true;
        }

        internal void MarkFirstMovieIncomeMessageShown()
        {
            hasShownFirstMovieIncomeMessage = true;
        }

        public bool HasShownParsonR01UnlockMessage => hasShownParsonR01UnlockMessage;

        internal void MarkParsonR01UnlockMessageShown()
        {
            hasShownParsonR01UnlockMessage = true;
        }

        public bool HasShownBuzzMovieCount1Message => hasShownBuzzMovieCount1Message;

        internal void MarkBuzzMovieCount1MessageShown()
        {
            hasShownBuzzMovieCount1Message = true;
        }

        public bool HasShownBuzzMovieCount10Message => hasShownBuzzMovieCount10Message;

        internal void MarkBuzzMovieCount10MessageShown()
        {
            hasShownBuzzMovieCount10Message = true;
        }

        public bool HasShownGameplayElapsed1HourMessage => hasShownGameplayElapsed1HourMessage;

        internal void MarkGameplayElapsed1HourMessageShown()
        {
            hasShownGameplayElapsed1HourMessage = true;
        }

        public bool HasShownGameplayElapsed100HourMessage => hasShownGameplayElapsed100HourMessage;

        internal void MarkGameplayElapsed100HourMessageShown()
        {
            hasShownGameplayElapsed100HourMessage = true;
        }

        public bool HasShownFirstTrashDropMessage => hasShownFirstTrashDropMessage;

        internal void MarkFirstTrashDropMessageShown()
        {
            hasShownFirstTrashDropMessage = true;
        }

        public bool HasShownFirstWorkEditorExtAssignMessage => hasShownFirstWorkEditorExtAssignMessage;

        internal void MarkFirstWorkEditorExtAssignMessageShown()
        {
            hasShownFirstWorkEditorExtAssignMessage = true;
        }

        public bool HasShownWorkEditorExt15MinutesMessage => hasShownWorkEditorExt15MinutesMessage;

        internal void MarkWorkEditorExt15MinutesMessageShown()
        {
            hasShownWorkEditorExt15MinutesMessage = true;
        }

        public bool HasShownFirstWorkMovieSlot1UploadMessage => hasShownFirstWorkMovieSlot1UploadMessage;

        internal void MarkFirstWorkMovieSlot1UploadMessageShown()
        {
            hasShownFirstWorkMovieSlot1UploadMessage = true;
        }

        public bool HasShownFirstMailCharaWorkEditor1Message => hasShownFirstMailCharaWorkEditor1Message;

        internal void MarkFirstMailCharaWorkEditor1MessageShown()
        {
            hasShownFirstMailCharaWorkEditor1Message = true;
        }

        public bool HasShownFirstItemEditorWorkEditor1Message => hasShownFirstItemEditorWorkEditor1Message;

        internal void MarkFirstItemEditorWorkEditor1MessageShown()
        {
            hasShownFirstItemEditorWorkEditor1Message = true;
        }

        public bool HasShownFirstEd51AutomationMessage => hasShownFirstEd51AutomationMessage;

        internal void MarkFirstEd51AutomationMessageShown()
        {
            hasShownFirstEd51AutomationMessage = true;
        }

        public bool HasShownFirstWorkUpgradeStMessage => hasShownFirstWorkUpgradeStMessage;

        internal void MarkFirstWorkUpgradeStMessageShown()
        {
            hasShownFirstWorkUpgradeStMessage = true;
        }

        public float GetMessageHoldSeconds()
        {
            return Mathf.Max(0f, movieRateTierMessageHoldSeconds);
        }

        public string GetFirstMovieIncomeMessageText()
        {
            return string.IsNullOrEmpty(firstMovieIncomeMessage)
                ? "初めて動画収入を得たよ！"
                : firstMovieIncomeMessage;
        }

        internal bool TryClaimPopularityMilestoneBit(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex >= 64)
            {
                return false;
            }

            ulong bit = 1ul << bitIndex;
            if ((popularityMilestoneMsgShownMask & bit) != 0)
            {
                return false;
            }

            popularityMilestoneMsgShownMask |= bit;
            return true;
        }

        public long CurrentBuzz => currentBuzz;
        public long BuzzThreshold => Math.Max(0L, buzzThreshold);
        public float BuzzMultiplier => Mathf.Max(0f, buzzMultiplier);
        public bool HasFatalError => hasFatalError;
        public bool HasFatalMoneyError => hasFatalError;

        public long MovieRateTierStage1Popularity => Math.Max(0L, movieRateTierStage1Popularity);
        public long MovieRateTierStage2Popularity => Math.Max(MovieRateTierStage1Popularity, movieRateTierStage2Popularity);
        public long MovieRateTierGoalPopularity => Math.Max(MovieRateTierStage2Popularity, movieRateTierGoalPopularity);
        public double MovieUnitPriceTier0 => Math.Max(0d, movieUnitPriceTier0);
        public double MovieUnitPriceTier1 => Math.Max(0d, movieUnitPriceTier1);
        public double MovieUnitPriceTier2 => Math.Max(0d, movieUnitPriceTier2);
        public double MovieUnitPriceTierGoal => Math.Max(0d, movieUnitPriceTierGoal);
        public long GameClearPopularityThreshold => Math.Max(0L, gameClearPopularityThreshold);

        public bool TryConsumeFirstEditWorkSeconds(out float fixedSeconds)
        {
            fixedSeconds = 0f;
            if (!enableFirstEditSecondsOverride || hasConsumedFirstEditSecondsOverride)
            {
                return false;
            }

            float seconds = Mathf.Max(0f, firstEditSeconds);
            if (seconds <= 0f)
            {
                return false;
            }

            hasConsumedFirstEditSecondsOverride = true;
            fixedSeconds = seconds;
            return true;
        }

        /// <summary>動画編集仕事を開始するたびに +1 し、1 始まりの回数を返す（序盤短縮・1 回目固定秒の判定用）。</summary>
        public int RegisterEditWorkSessionStarted()
        {
            lifetimeEditWorkSessionsStarted = Mathf.Max(0, lifetimeEditWorkSessionsStarted) + 1;
            return lifetimeEditWorkSessionsStarted;
        }

        public bool TryConsumeFirstUploadFirstIncomeBonus(out long bonusAmount)
        {
            bonusAmount = 0L;
            if (!enableFirstUploadFirstIncomeBonus || hasConsumedFirstUploadFirstIncomeBonus)
            {
                return false;
            }

            long safeBonus = Math.Max(0L, firstUploadFirstIncomeBonusAmount);
            if (safeBonus <= 0L)
            {
                return false;
            }

            hasConsumedFirstUploadFirstIncomeBonus = true;
            bonusAmount = safeBonus;
            return true;
        }

        public double ResolveMovieUnitPriceByPopularity(long popularity)
        {
            long p = Math.Max(0L, popularity);
            if (p < MovieRateTierStage1Popularity)
            {
                return MovieUnitPriceTier0;
            }

            if (p < MovieRateTierStage2Popularity)
            {
                return MovieUnitPriceTier1;
            }

            if (p < MovieRateTierGoalPopularity)
            {
                return MovieUnitPriceTier2;
            }

            return MovieUnitPriceTierGoal;
        }

        public float GetRandomBuzzCorrectionFactor()
        {
            if (buzzCorrectionList == null || buzzCorrectionList.Length == 0)
            {
                return 1f;
            }

            int index = UnityEngine.Random.Range(0, buzzCorrectionList.Length);
            return buzzCorrectionList[index];
        }

        public bool IsBuzzTriggeredByGain(long movieBuzzGain)
        {
            long safeGain = Math.Max(0L, movieBuzzGain);
            long nextBuzz;
            try
            {
                checked
                {
                    nextBuzz = currentBuzz + safeGain;
                }
            }
            catch (OverflowException)
            {
                // オーバーフロー相当は閾値超え扱いに倒して安全側に寄せる。
                return true;
            }

            return nextBuzz > BuzzThreshold;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            isPaused = false;
            suppressPausePanelWhilePaused = false;
            itemStreamSequence = 0;
            itemMovieSequence = 0;
            hasConsumedFirstEditSecondsOverride = false;
            lifetimeEditWorkSessionsStarted = 0;
            hasConsumedFirstUploadFirstIncomeBonus = false;
            popularityMilestoneMsgShownMask = 0UL;
            hasShownFirstFanParson01Message = false;
            hasShownCatNeko100Message = false;
            hasShownCatNeko1000Message = false;
            hasShownParsonR01UnlockMessage = false;
            hasShownBuzzMovieCount1Message = false;
            hasShownBuzzMovieCount10Message = false;
            hasShownGameplayElapsed1HourMessage = false;
            hasShownGameplayElapsed100HourMessage = false;
            hasShownFirstTrashDropMessage = false;
            hasShownFirstWorkEditorExtAssignMessage = false;
            hasShownWorkEditorExt15MinutesMessage = false;
            hasShownFirstWorkMovieSlot1UploadMessage = false;
            hasShownFirstMailCharaWorkEditor1Message = false;
            hasShownFirstItemEditorWorkEditor1Message = false;
            hasShownFirstEd51AutomationMessage = false;
            hasShownFirstWorkUpgradeStMessage = false;
            hasShownFirstMovieIncomeMessage = false;
            hasShownSupportEndedMessage = false;
            hasReceivedSupportReward = false;
            hasShownGameClearedPanel = false;
            isPreGameSequenceActive = false;
            gameplayTimerStarted = false;
            isGameCleared = false;
            SetGameClearedPanelVisible(false);
            ResolveMainMoneyPopularityUiTextIfNeeded();
            InitializeMoneyState();
            if (hasFatalError)
            {
                RunInitialAwakeCheckOnce();
                return;
            }

            InitializePopularityState();
            if (hasFatalError)
            {
                RunInitialAwakeCheckOnce();
                return;
            }

            InitializeBuzzState();
            LogMissingMoneyPopularityUiRefsIfNeeded();
            EnsureMoneyPopularAddUiRectsCached();
            RunInitialAwakeCheckOnce();
            runtimeSessionInitialized = true;
        }

        private void Start()
        {
            if (hasFatalError)
            {
                return;
            }

            TryClearIncomingMenuFade();

            if (hasAppliedSaveState)
            {
                ResolveMainMoneyPopularityUiTextIfNeeded();
                RefreshMainMoneyPopularityUiFromCurrentState();
                TryBeginForcedStageStartOverlayAfterSaveLoadForDebug();
                Game02SceneLifecycleLog.InitComplete(
                    $"hasAppliedSaveState=true didLoadSaveSession={Game02SaveCoordinator.DidLoadSaveThisSession} isPreGameSequenceActive={isPreGameSequenceActive}");
                return;
            }

            // シーン側の他コンポーネント初期化（Start/OnEnable）後に、
            // 初期所持金・初期人気と表示を必ず再反映する。
            ResolveMainMoneyPopularityUiTextIfNeeded();
            InitializeMoneyState();
            if (hasFatalError)
            {
                return;
            }

            InitializePopularityState();
            if (hasFatalError)
            {
                return;
            }

            SetGameClearedPanelVisible(false);
            RefreshMainMoneyPopularityUiFromCurrentState();
            Game02SceneLifecycleLog.InitComplete(
                $"hasAppliedSaveState=false didLoadSaveSession={Game02SaveCoordinator.DidLoadSaveThisSession} isPreGameSequenceActive={isPreGameSequenceActive}");
            BeginGameplayStartFlow();
        }

        private void TryClearIncomingMenuFade()
        {
            if (incomingMenuFadeOutSeconds <= 0f)
            {
                return;
            }

            Fade fade = FindAnyObjectByType<Fade>();
            if (fade == null)
            {
                return;
            }

            fade.FadeOut(Mathf.Max(0.01f, incomingMenuFadeOutSeconds));
        }

        private void Update()
        {
            EnsureRuntimeSessionInitializedFromUpdate();

            // UI の生成/有効化が遅れる環境（ビルド差分含む）でも、
            // 参照が取れ次第すぐに初期値を表示へ反映する。
            if (moneyUiText == null || popularUiText == null)
            {
                ResolveMainMoneyPopularityUiTextIfNeeded();
                RefreshMainMoneyPopularityUiFromCurrentState();
            }

            if (forceSyncNamedMoneyPopularityUi)
            {
                SyncNamedMainUiTexts();
            }

            if (hasFatalError)
            {
                return;
            }

            ProcessMoneyOperations();
            ProcessPopularityOperations();
            ProcessBuzzOperations();
            ForceBootUiSyncForEarlyFrames();
            UpdatePreGameSequenceSafetyTimeout();

            if (!IsGameplayTimerRunning)
            {
                return;
            }

            float scaledDt = GetGameplayDeltaTime();
            float gameplayBefore = gameplayElapsedSeconds;
            gameplayElapsedSeconds += scaledDt;
            Game02MsgManager.TryGet()?.NotifyGameplayHourMilestones(gameplayBefore, gameplayElapsedSeconds, this);
            eventCheckAccumulator += scaledDt;

            float interval = Mathf.Max(0.01f, eventCheckIntervalSeconds);
            while (eventCheckAccumulator >= interval)
            {
                eventCheckAccumulator -= interval;
                OnEventCheckTick();
            }
        }

        private void ForceBootUiSyncForEarlyFrames()
        {
            if (hasAppliedSaveState)
            {
                return;
            }

            // 開始直後のみ、初期値とメイン表示を強制同期して「開始時だけ 0」を潰す。
            if (Time.frameCount > 5)
            {
                return;
            }

            if (autoRecoverMainUiTextRefs)
            {
                ResolveMainMoneyPopularityUiTextIfNeeded();
            }

            if (currentMoney != initialMoney)
            {
                currentMoney = initialMoney;
            }

            if (currentPopularity != initialPopularity)
            {
                currentPopularity = initialPopularity;
            }

            ApplyMoneyText();
            ApplyPopularText();

            if (!enableBootMoneyPopularityLogs)
            {
                return;
            }

            string moneyUiValue = moneyUiText != null ? moneyUiText.text : "(null)";
            string popularUiValue = popularUiText != null ? popularUiText.text : "(null)";
            Debug.Log(
                $"[GameManager] BootUI frame={Time.frameCount} money {currentMoney}/{initialMoney} ui='{moneyUiValue}', popularity {currentPopularity}/{initialPopularity} ui='{popularUiValue}'");
        }

        private void EnsureRuntimeSessionInitializedFromUpdate()
        {
            // Enter Play Mode Options で Scene/Domain Reload を切っていても、
            // プレイ開始ごとに初期値（initialMoney / initialPopularity）を反映する。
            int frame = Time.frameCount;
            if (frame < lastObservedFrameCount)
            {
                runtimeSessionInitialized = false;
            }
            lastObservedFrameCount = frame;

            if (runtimeSessionInitialized)
            {
                return;
            }

            currentMoney = initialMoney;
            currentPopularity = initialPopularity;
            currentBuzz = initialBuzz;
            pendingMoneyOperations.Clear();
            queuedMoneyDeltaPreview = 0L;
            pendingPopularityOperations.Clear();
            pendingBuzzOperations.Clear();

            hasFatalError = false;
            isPaused = false;
            suppressPausePanelWhilePaused = false;
            gameplayElapsedSeconds = 0f;
            eventCheckAccumulator = 0f;
            hasConsumedFirstEditSecondsOverride = false;
            lifetimeEditWorkSessionsStarted = 0;
            hasConsumedFirstUploadFirstIncomeBonus = false;
            popularityMilestoneMsgShownMask = 0UL;
            hasShownFirstFanParson01Message = false;
            hasShownCatNeko100Message = false;
            hasShownCatNeko1000Message = false;
            hasShownParsonR01UnlockMessage = false;
            hasShownBuzzMovieCount1Message = false;
            hasShownBuzzMovieCount10Message = false;
            hasShownGameplayElapsed1HourMessage = false;
            hasShownGameplayElapsed100HourMessage = false;
            hasShownFirstTrashDropMessage = false;
            hasShownFirstWorkEditorExtAssignMessage = false;
            hasShownWorkEditorExt15MinutesMessage = false;
            hasShownFirstWorkMovieSlot1UploadMessage = false;
            hasShownFirstMailCharaWorkEditor1Message = false;
            hasShownFirstItemEditorWorkEditor1Message = false;
            hasShownFirstEd51AutomationMessage = false;
            hasShownFirstWorkUpgradeStMessage = false;
            hasShownFirstMovieIncomeMessage = false;
            hasShownSupportEndedMessage = false;
            hasReceivedSupportReward = false;
            hasShownGameClearedPanel = false;
            isPreGameSequenceActive = false;
            gameplayTimerStarted = false;
            isGameCleared = false;
            SetGameClearedPanelVisible(false);

            EnsureMoneyPopularAddUiRectsCached();
            ResolveMainMoneyPopularityUiTextIfNeeded();
            ApplyMoneyText();
            ApplyPopularText();
            ApplyBuzzText();
            InitializeMoneyAddUi();
            InitializePopularAddUi();
            runtimeSessionInitialized = true;
        }

        /// <param name="showPausePanelWhenPaused">true のときのみ、停止中に PausePanel を表示する（既定 true）。</param>
        public void SetPaused(bool paused, bool showPausePanelWhenPaused = true)
        {
            isPaused = paused;
            if (!paused)
            {
                suppressPausePanelWhilePaused = false;
                return;
            }

            suppressPausePanelWhilePaused = !showPausePanelWhenPaused;
        }

        public void KickGameplayStart()
        {
            if (hasFatalError)
            {
                return;
            }

            Game02SceneLifecycleLog.ActualGameplayStart(
                $"hasAppliedSaveState={hasAppliedSaveState} didLoadSaveSession={Game02SaveCoordinator.DidLoadSaveThisSession} isPaused={isPaused}");

            gameplayTimerStarted = true;
            isPreGameSequenceActive = false;
            preGameSequenceSafetyElapsedSeconds = 0f;
            isPaused = false;
            suppressPausePanelWhilePaused = false;

            bool skipClearGameCleared = preserveGameClearedThroughNextKick;
            preserveGameClearedThroughNextKick = false;
            if (!skipClearGameCleared)
            {
                isGameCleared = false;
            }

            if (itemSpawnController == null)
            {
                itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
            }

            // セーフ配置: 非Load開始かつ ItemMailChara が ItemCanvas に存在しない場合に備えて、
            // ゲーム開始時に保険として一度スポーンを試みる（ExecuteInitialMainCharacterSpawnCheckOnce は一度きり）。
            if (itemSpawnController != null && !Game02SaveCoordinator.DidLoadSaveThisSession)
            {
                itemSpawnController.ExecuteInitialMainCharacterSpawnCheckOnce();
            }

            // StageStartOverlay 経由で生成した ItemMailChara は TrySpawn 内で Reflow しないため、
            // ゲーム開始キック時に初めて配置ゾーンへ収める。
            itemSpawnController?.NotifyPlacementLayoutNeedsRefresh();
        }

        public void NotifyStageStartSequenceCompleted()
        {
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_01);
            KickGameplayStart();
        }

        private void BeginGameplayStartFlow()
        {
            gameplayTimerStarted = false;
            isGameCleared = false;

            if (!ResolveEnableStageStartOverlaySequence())
            {
                Game02SceneLifecycleLog.StageStartSequenceEnd("演出なし（開発ビルドかつ Game02DebugManager.enableStageStartOverlaySequence が OFF）");
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_01);
                KickGameplayStart();
                return;
            }

            isPreGameSequenceActive = true;
            preGameSequenceSafetyElapsedSeconds = 0f;
            activePreGameSequenceSafetyTimeoutSeconds = ResolveEffectivePreGameSequenceSafetyTimeoutSeconds();
            StageStartOverlayController controller = StageStartOverlayController.EnsureSceneController();
            if (controller == null)
            {
                Game02SceneLifecycleLog.StageStartSequenceEnd("演出なし（StageStartOverlayController が見つからない）");
                KickGameplayStart();
                return;
            }

            controller.PlaySequence(this);
        }

        /// <summary>
        /// セーブ読込で開始した直後のみ。<see cref="Game02DebugManager.debugForceStageStartOverlayAfterSaveLoad"/> が有効なら
        /// StageStartOverlay を強制再生する。
        /// </summary>
        private void TryBeginForcedStageStartOverlayAfterSaveLoadForDebug()
        {
            Game02DebugManager debugManager =
                FindAnyObjectByType<Game02DebugManager>(FindObjectsInactive.Include);
            if (debugManager == null || !debugManager.ComputeEffectiveForceStageStartOverlayAfterSaveLoad())
            {
                return;
            }

            gameplayTimerStarted = false;
            preserveGameClearedThroughNextKick = true;
            isPreGameSequenceActive = true;
            preGameSequenceSafetyElapsedSeconds = 0f;
            activePreGameSequenceSafetyTimeoutSeconds = ResolveEffectivePreGameSequenceSafetyTimeoutSeconds();

            StageStartOverlayController controller = StageStartOverlayController.EnsureSceneController();
            if (controller == null)
            {
                preserveGameClearedThroughNextKick = false;
                Game02SceneLifecycleLog.StageStartSequenceEnd("演出なし（強制演出要求だが StageStartOverlayController が見つからない）");
                KickGameplayStart();
                return;
            }

            controller.PlaySequence(this);
        }

        private void UpdatePreGameSequenceSafetyTimeout()
        {
            if (!isPreGameSequenceActive)
            {
                preGameSequenceSafetyElapsedSeconds = 0f;
                activePreGameSequenceSafetyTimeoutSeconds = 0f;
                return;
            }

            float timeout = Mathf.Max(
                0f,
                activePreGameSequenceSafetyTimeoutSeconds > 0f
                    ? activePreGameSequenceSafetyTimeoutSeconds
                    : preGameSequenceSafetyTimeoutSeconds);
            if (timeout <= 0f)
            {
                return;
            }

            preGameSequenceSafetyElapsedSeconds += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (preGameSequenceSafetyElapsedSeconds < timeout)
            {
                return;
            }

            Debug.LogWarning(
                "[GameManager] StageStartOverlay completion notification timed out. Gameplay start was forced by safety fallback.");
            Game02SceneLifecycleLog.StageStartSequenceEnd("フェイルセーフ（preGameSequenceSafetyTimeout）");
            KickGameplayStart();
        }

        private float ResolveEffectivePreGameSequenceSafetyTimeoutSeconds()
        {
            float configured = Mathf.Max(0f, preGameSequenceSafetyTimeoutSeconds);
            Game02EffectManager effectManager = Game02EffectManager.TryGet();
            if (effectManager == null)
            {
                return configured;
            }

            float expectedSequenceSeconds =
                effectManager.GameStartStampDelaySeconds +
                effectManager.GameStartCharaAppearDelaySeconds +
                effectManager.GameStartCharaMoveDelaySeconds +
                effectManager.GameStartCharaMoveDurationSeconds +
                effectManager.GameStartCharaSpinSettleDurationSeconds;

            // 演出完了通知が最終フレームで遅れても誤発火しないよう、少し余裕を持たせる。
            float minimumSafeTimeout = Mathf.Max(0f, expectedSequenceSeconds + 0.75f);
            return Mathf.Max(configured, minimumSafeTimeout);
        }

        public void MarkGameCleared()
        {
            isGameCleared = true;
        }

        /// <summary>
        /// <see cref="Game02DebugManager"/> 用。人気閾値・演出済みセーブフラグを無視してクリア演出を適用する。本番ビルドでは無効にすること。
        /// </summary>
        public void DebugForceStartGameClearCeremony()
        {
            PresentGameClearCeremonyOnce();
        }

        /// <summary>
        /// GameClearedPanel の Return ボタンから呼び出し、クリア表示を閉じてゲーム進行へ戻す。
        /// </summary>
        public void OnClickGameReturnButton()
        {
            GameClearedCeremonyController.TryNotifyEndingCleanupStatic();
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.GameClearedPanelReturn);
            SetGameClearedPanelVisible(false);
            isGameCleared = false;
            SetPaused(false);
        }

        public float GetGameplayDeltaTime()
        {
            if (!IsGameplayTimerRunning)
            {
                return 0f;
            }

            return Time.deltaTime * GameSpeedMultiplier;
        }

        public void SetGameSpeedStepIndex(int stepIndex)
        {
            gameSpeedStepIndex = Mathf.Clamp(stepIndex, 0, GameSpeedMultipliers.Length - 1);
        }

        public void CycleGameSpeed()
        {
            gameSpeedStepIndex = (gameSpeedStepIndex + 1) % GameSpeedMultipliers.Length;
        }

        public bool RequestMoneyDelta(long delta, string reason = "", string sourceId = "", long operationId = -1)
        {
            if (hasFatalError)
            {
                return false;
            }

            long projectedMoney;
            try
            {
                checked
                {
                    projectedMoney = currentMoney + queuedMoneyDeltaPreview + delta;
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            if (projectedMoney < 0 || projectedMoney > maxMoney)
            {
                return false;
            }

            long safeOperationId = operationId >= 0 ? operationId : NextAutoMoneyOperationId();
            pendingMoneyOperations.Enqueue(new MoneyOperation(
                delta,
                reason ?? string.Empty,
                sourceId ?? string.Empty,
                safeOperationId));
            queuedMoneyDeltaPreview += delta;
            return true;
        }

        public bool RequestPopularityDelta(long delta, string reason = "", string sourceId = "", long operationId = -1)
        {
            if (hasFatalError)
            {
                return false;
            }

            long safeOperationId = operationId >= 0 ? operationId : NextAutoPopularityOperationId();
            pendingPopularityOperations.Enqueue(new PopularityOperation(
                delta,
                reason ?? string.Empty,
                sourceId ?? string.Empty,
                safeOperationId));
            return true;
        }

        public bool RequestBuzzDelta(long delta, string reason = "", string sourceId = "", long operationId = -1)
        {
            if (hasFatalError)
            {
                return false;
            }

            long safeOperationId = operationId >= 0 ? operationId : NextAutoBuzzOperationId();
            pendingBuzzOperations.Enqueue(new BuzzOperation(
                delta,
                reason ?? string.Empty,
                sourceId ?? string.Empty,
                safeOperationId));
            return true;
        }

        public void OnClickButtonDebugItemSt()
        {
            TrySpawnDebugItemStream();
        }

        public void OnClickButtonDebugItemMv()
        {
            TrySpawnDebugItemMovie();
        }

        public void OnClickButtonDebugBuzz()
        {
            RequestBuzzDelta(30L, "Debug buzz +30", "ButtonDebugBuzz", -1);
            if (enableDebugBuzzConfetti)
            {
                BuzzPaperConfettiController controller = BuzzPaperConfettiController.EnsureSceneController();
                if (controller != null)
                {
                    controller.Play();
                }
            }
        }

        public bool TryIssueNextItemDisplayName(ItemType itemType, out string generatedName)
        {
            generatedName = string.Empty;
            switch (itemType)
            {
                case ItemType.ItemStream:
                    itemStreamSequence += 1;
                    generatedName = $"ItemStream_{itemStreamSequence}";
                    return true;
                case ItemType.ItemMovie:
                    itemMovieSequence += 1;
                    generatedName = $"ItemMovie_{itemMovieSequence}";
                    return true;
                default:
                    return false;
            }
        }

        public bool TrySpawnDebugItemStream()
        {
            if (itemSpawnController == null)
            {
                itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
            }

            if (itemSpawnController == null || itemSpawnController.ItemCanvas == null)
            {
                Debug.LogWarning("[GameManager] ItemSpawnController または ItemCanvas が見つからないため ItemStream を生成できません。");
                return false;
            }

            RectTransform itemCanvasRect = itemSpawnController.ItemCanvas;
            float y = itemCanvasRect.rect.yMax + Mathf.Max(0f, debugItemStreamSpawnAboveTopPixels);
            Vector2 spawnPosition = new Vector2(0f, y);

            if (!itemSpawnController.TrySpawnItem(ItemType.ItemStream, spawnPosition, true, out _))
            {
                return false;
            }

            return true;
        }

        public bool TrySpawnDebugItemMovie()
        {
            if (itemSpawnController == null)
            {
                itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
            }

            if (itemSpawnController == null || itemSpawnController.ItemCanvas == null)
            {
                Debug.LogWarning("[GameManager] ItemSpawnController または ItemCanvas が見つからないため ItemMovie を生成できません。");
                return false;
            }

            RectTransform itemCanvasRect = itemSpawnController.ItemCanvas;
            float y = itemCanvasRect.rect.yMax + Mathf.Max(0f, debugItemStreamSpawnAboveTopPixels);
            Vector2 spawnPosition = new Vector2(0f, y);

            if (!itemSpawnController.TrySpawnItem(ItemType.ItemMovie, spawnPosition, true, out DraggableItemController spawnedMovie))
            {
                return false;
            }

            if (spawnedMovie != null)
            {
                ItemMoviePower moviePower = spawnedMovie.GetComponent<ItemMoviePower>();
                if (moviePower == null)
                {
                    moviePower = spawnedMovie.gameObject.AddComponent<ItemMoviePower>();
                }

                long popularityAtSpawn = Math.Max(0L, CurrentPopularity);
                string debugGenre = ResolveDebugStreamGenre();
                string movieName = ResolveDebugMovieName(debugGenre);
                moviePower.InitializeMovieStats(popularityAtSpawn, 0L, movieName);
            }

            return true;
        }

        public GameManagerState CaptureSaveState()
        {
            return new GameManagerState
            {
                currentMoney = currentMoney,
                currentPopularity = currentPopularity,
                currentBuzz = currentBuzz,
                gameplayElapsedSeconds = gameplayElapsedSeconds,
                gameSpeedStepIndex = GameSpeedStepIndex,
                isPaused = isPaused,
                hasConsumedFirstEditSecondsOverride = hasConsumedFirstEditSecondsOverride,
                lifetimeEditWorkSessionsStarted = lifetimeEditWorkSessionsStarted,
                hasConsumedFirstUploadFirstIncomeBonus = hasConsumedFirstUploadFirstIncomeBonus,
                popularityMilestoneMsgShownMask = popularityMilestoneMsgShownMask,
                hasShownFirstFanParson01Message = hasShownFirstFanParson01Message,
                hasShownCatNeko100Message = hasShownCatNeko100Message,
                hasShownCatNeko1000Message = hasShownCatNeko1000Message,
                hasShownParsonR01UnlockMessage = hasShownParsonR01UnlockMessage,
                hasShownBuzzMovieCount1Message = hasShownBuzzMovieCount1Message,
                hasShownBuzzMovieCount10Message = hasShownBuzzMovieCount10Message,
                hasShownGameplayElapsed1HourMessage = hasShownGameplayElapsed1HourMessage,
                hasShownGameplayElapsed100HourMessage = hasShownGameplayElapsed100HourMessage,
                hasShownFirstTrashDropMessage = hasShownFirstTrashDropMessage,
                hasShownFirstWorkEditorExtAssignMessage = hasShownFirstWorkEditorExtAssignMessage,
                hasShownWorkEditorExt15MinutesMessage = hasShownWorkEditorExt15MinutesMessage,
                hasShownFirstWorkMovieSlot1UploadMessage = hasShownFirstWorkMovieSlot1UploadMessage,
                hasShownFirstMailCharaWorkEditor1Message = hasShownFirstMailCharaWorkEditor1Message,
                hasShownFirstItemEditorWorkEditor1Message = hasShownFirstItemEditorWorkEditor1Message,
                hasShownFirstEd51AutomationMessage = hasShownFirstEd51AutomationMessage,
                hasShownFirstWorkUpgradeStMessage = hasShownFirstWorkUpgradeStMessage,
                hasPlayedMovieRateTierStage1Se = false,
                hasPlayedMovieRateTierStage2Se = false,
                hasPlayedMovieRateTierGoalSe = false,
                hasShownFirstMovieIncomeMessage = hasShownFirstMovieIncomeMessage,
                hasShownSupportEndedMessage = hasShownSupportEndedMessage,
                hasReceivedSupportReward = hasReceivedSupportReward,
                hasShownGameClearedPanel = hasShownGameClearedPanel,
                isPreGameSequenceActive = isPreGameSequenceActive,
                gameplayTimerStarted = gameplayTimerStarted,
                isGameCleared = isGameCleared,
                itemStreamSequence = itemStreamSequence,
                itemMovieSequence = itemMovieSequence
            };
        }

        public void ApplySaveState(GameManagerState state)
        {
            if (state == null)
            {
                return;
            }

            currentMoney = Math.Max(0L, Math.Min(maxMoney, state.currentMoney));
            currentPopularity = Math.Max(0L, Math.Min(maxPopularity, state.currentPopularity));
            currentBuzz = Math.Max(0L, Math.Min(maxBuzz, state.currentBuzz));
            gameplayElapsedSeconds = Mathf.Max(0f, state.gameplayElapsedSeconds);
            gameSpeedStepIndex = Mathf.Clamp(state.gameSpeedStepIndex, 0, GameSpeedMultipliers.Length - 1);
            isPaused = state.isPaused;
            suppressPausePanelWhilePaused = false;
            hasConsumedFirstEditSecondsOverride = state.hasConsumedFirstEditSecondsOverride;
            lifetimeEditWorkSessionsStarted = Mathf.Max(0, state.lifetimeEditWorkSessionsStarted);
            hasConsumedFirstUploadFirstIncomeBonus = state.hasConsumedFirstUploadFirstIncomeBonus;
            popularityMilestoneMsgShownMask = state.popularityMilestoneMsgShownMask;
            hasShownFirstFanParson01Message = state.hasShownFirstFanParson01Message;
            hasShownCatNeko100Message = state.hasShownCatNeko100Message;
            hasShownCatNeko1000Message = state.hasShownCatNeko1000Message;
            hasShownParsonR01UnlockMessage = state.hasShownParsonR01UnlockMessage;
            hasShownBuzzMovieCount1Message = state.hasShownBuzzMovieCount1Message;
            hasShownBuzzMovieCount10Message = state.hasShownBuzzMovieCount10Message;
            hasShownGameplayElapsed1HourMessage = state.hasShownGameplayElapsed1HourMessage;
            hasShownGameplayElapsed100HourMessage = state.hasShownGameplayElapsed100HourMessage;
            hasShownFirstTrashDropMessage = state.hasShownFirstTrashDropMessage;
            hasShownFirstWorkEditorExtAssignMessage = state.hasShownFirstWorkEditorExtAssignMessage;
            hasShownWorkEditorExt15MinutesMessage = state.hasShownWorkEditorExt15MinutesMessage;
            hasShownFirstWorkMovieSlot1UploadMessage = state.hasShownFirstWorkMovieSlot1UploadMessage;
            hasShownFirstMailCharaWorkEditor1Message = state.hasShownFirstMailCharaWorkEditor1Message;
            hasShownFirstItemEditorWorkEditor1Message = state.hasShownFirstItemEditorWorkEditor1Message;
            hasShownFirstEd51AutomationMessage = state.hasShownFirstEd51AutomationMessage;
            hasShownFirstWorkUpgradeStMessage = state.hasShownFirstWorkUpgradeStMessage;
            hasShownFirstMovieIncomeMessage = state.hasShownFirstMovieIncomeMessage;
            hasShownSupportEndedMessage = state.hasShownSupportEndedMessage;
            hasReceivedSupportReward = state.hasReceivedSupportReward;
            hasShownGameClearedPanel = state.hasShownGameClearedPanel;
            isPreGameSequenceActive = false;
            gameplayTimerStarted = state.gameplayTimerStarted;
            isGameCleared = state.isGameCleared;
            itemStreamSequence = Mathf.Max(0, state.itemStreamSequence);
            itemMovieSequence = Mathf.Max(0, state.itemMovieSequence);
            popularityMilestoneMsgShownMask = Game02MsgManager.MergeSilentCatchupPopularityMask(
                popularityMilestoneMsgShownMask,
                currentPopularity);
            hasAppliedSaveState = true;
            runtimeSessionInitialized = true;
        }

        public void PostLoadRefresh()
        {
            TryApplyGameClearAfterLoadIfNeeded();
            Game02MsgManager.TryUnlockTenMillionPopularityAchievementIfAlreadyAt(currentPopularity);

            ResolveMainMoneyPopularityUiTextIfNeeded();
            EnsureMoneyPopularAddUiRectsCached();
            ApplyMoneyText();
            ApplyPopularText();
            ApplyBuzzText();
            SetGameClearedPanelVisible(hasShownGameClearedPanel && isGameCleared);
        }

        public void PlayPurchaseSuccessSe()
        {
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.PurchaseSuccess);
        }

        public void PlayPurchaseFailureSe()
        {
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.PurchaseFailure);
        }

        private void RunInitialAwakeCheckOnce()
        {
            if (hasRunInitialAwakeCheck)
            {
                return;
            }

            hasRunInitialAwakeCheck = true;
            Game02AlienProgressTracker.EnsureExists();
            WorkEditor.EnsureSceneWorkEditorsExist();
            WorkEditorExt.EnsureSceneWorkEditorExtExists();
            WorkUpgradeSt02Controller.EnsureSceneWorkUpgradeSt02Exists();
            WorkUpgradeSt03Controller.EnsureSceneWorkUpgradeSt03Exists();
            WorkUpgradeSt04Controller.EnsureSceneWorkUpgradeSt04Exists();
            WorkMovieSlotController.EnsureSceneWorkMovieSlotsExist();
            WorkUpgradeEd01Controller.EnsureSceneWorkUpgradeEd01Exists();
            WorkUpgradeEd02Controller.EnsureSceneWorkUpgradeEd02Exists();
            WorkUpgradeEd03Controller.EnsureSceneWorkUpgradeEd03Exists();
            WorkUpgradeEd04Controller.EnsureSceneWorkUpgradeEd04Exists();
            WorkUpgradeEd05Controller.EnsureSceneWorkUpgradeEd05Exists();
            WorkUpgradeEd51Controller.EnsureSceneWorkUpgradeEd51Exists();
            EditorBuyController.EnsureSceneEditorBuyExists();
            Game02MessagePresenter.EnsureSceneController();
            GameClearedPanelController.EnsureSceneController();
            GameMenuPanelController.EnsureSceneController();
            Game02SaveCoordinator.EnsureSceneController();
            DebugAutoPlayManager.EnsureSceneController();
            SidePanelPriceColorSync.EnsureSceneSidePanelPriceColorSyncExists();
            Game02ElapsedTimeUiController.EnsureSceneController();
            TrashDropTarget.EnsureSceneTrashDropTargetExists();
            if (itemSpawnController == null)
            {
                itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
            }

            if (itemSpawnController != null &&
                !Game02SaveCoordinator.DidLoadSaveThisSession &&
                !ResolveEnableStageStartOverlaySequence())
            {
                itemSpawnController.ExecuteInitialMainCharacterSpawnCheckOnce();
            }
        }

        /// <summary>
        /// 開始演出を行うか。未設定時は <c>true</c>。
        /// <see cref="Game02DebugManager"/> ありのときは <see cref="Game02DebugManager.ComputeEffectiveEnableStageStartOverlaySequence"/>（本番リリース用 ON では常に演出 ON）。
        /// </summary>
        private bool ResolveEnableStageStartOverlaySequence()
        {
            Game02DebugManager debugManager =
                FindAnyObjectByType<Game02DebugManager>(FindObjectsInactive.Include);
            if (debugManager == null)
            {
                return true;
            }

            return debugManager.ComputeEffectiveEnableStageStartOverlaySequence();
        }

        private void OnEventCheckTick()
        {
            WorkStreaming[] workers = FindObjectsOfType<WorkStreaming>(true);
            WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
            WorkUpgradeSt01Controller[] upgradeSt01Workers = FindObjectsOfType<WorkUpgradeSt01Controller>(true);
            WorkUpgradeSt02Controller[] upgradeSt02Workers = FindObjectsOfType<WorkUpgradeSt02Controller>(true);
            WorkUpgradeSt03Controller[] upgradeSt03Workers = FindObjectsOfType<WorkUpgradeSt03Controller>(true);
            WorkUpgradeSt04Controller[] upgradeSt04Workers = FindObjectsOfType<WorkUpgradeSt04Controller>(true);
            WorkMovieUploadController[] workMovieUploadWorkers = FindObjectsOfType<WorkMovieUploadController>(true);
            float safeInterval = Mathf.Max(0.01f, eventCheckIntervalSeconds);
            for (int i = 0; i < workers.Length; i++)
            {
                if (workers[i] == null || !workers[i].isActiveAndEnabled)
                {
                    continue;
                }

                workers[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i] == null || !editors[i].isActiveAndEnabled)
                {
                    continue;
                }

                editors[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < upgradeSt01Workers.Length; i++)
            {
                if (upgradeSt01Workers[i] == null || !upgradeSt01Workers[i].isActiveAndEnabled)
                {
                    continue;
                }

                upgradeSt01Workers[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < upgradeSt02Workers.Length; i++)
            {
                if (upgradeSt02Workers[i] == null || !upgradeSt02Workers[i].isActiveAndEnabled)
                {
                    continue;
                }

                upgradeSt02Workers[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < upgradeSt03Workers.Length; i++)
            {
                if (upgradeSt03Workers[i] == null || !upgradeSt03Workers[i].isActiveAndEnabled)
                {
                    continue;
                }

                upgradeSt03Workers[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < upgradeSt04Workers.Length; i++)
            {
                if (upgradeSt04Workers[i] == null || !upgradeSt04Workers[i].isActiveAndEnabled)
                {
                    continue;
                }

                upgradeSt04Workers[i].OnGameManagerWorkTick(safeInterval);
            }

            for (int i = 0; i < workMovieUploadWorkers.Length; i++)
            {
                if (workMovieUploadWorkers[i] == null || !workMovieUploadWorkers[i].isActiveAndEnabled)
                {
                    continue;
                }

                workMovieUploadWorkers[i].OnGameManagerWorkTick(safeInterval);
            }
        }

        private void InitializeMoneyState()
        {
            if (maxMoney < 0)
            {
                maxMoney = 0;
            }

            currentMoney = initialMoney;
            ApplyMoneyText();
            InitializeMoneyAddUi();

            if (currentMoney < 0)
            {
                TriggerFatalError($"[GameManager] currentMoney underflow. money={currentMoney}");
                return;
            }

            if (currentMoney > maxMoney)
            {
                TriggerFatalError($"[GameManager] currentMoney overflow. money={currentMoney}, max={maxMoney}");
            }
        }

        private void InitializePopularityState()
        {
            if (maxPopularity < 0)
            {
                maxPopularity = 0;
            }

            currentPopularity = initialPopularity;
            ApplyPopularText();
            InitializePopularAddUi();

            if (currentPopularity < 0)
            {
                TriggerFatalError($"[GameManager] currentPopularity underflow. popularity={currentPopularity}");
                return;
            }

            if (currentPopularity > maxPopularity)
            {
                TriggerFatalError($"[GameManager] currentPopularity overflow. popularity={currentPopularity}, max={maxPopularity}");
            }
        }

        private void InitializeBuzzState()
        {
            if (maxBuzz < 0)
            {
                maxBuzz = 0;
            }

            currentBuzz = initialBuzz;

            if (currentBuzz < 0)
            {
                TriggerFatalError($"[GameManager] currentBuzz underflow. buzz={currentBuzz}");
                return;
            }

            if (currentBuzz > maxBuzz)
            {
                TriggerFatalError($"[GameManager] currentBuzz overflow. buzz={currentBuzz}, max={maxBuzz}");
            }

            ApplyBuzzText();
        }

        private void LogMissingMoneyPopularityUiRefsIfNeeded()
        {
            if (moneyUiText == null)
            {
                Debug.LogWarning(
                    "[GameManager] moneyUiText が未設定です。ヒエラルキーから所持金メイン表示の TMP_Text をドラッグし、Game Manager (Script) の Refs に割り当ててください。");
            }

            if (moneyAddUiText == null)
            {
                Debug.LogWarning(
                    "[GameManager] moneyAddUiText が未設定です。所持金増加分表示の TMP_Text を Refs に割り当ててください。");
            }

            if (popularUiText == null)
            {
                Debug.LogWarning(
                    "[GameManager] popularUiText が未設定です。人気メイン表示の TMP_Text を Refs に割り当ててください。");
            }

            if (popularAddUiText == null)
            {
                Debug.LogWarning(
                    "[GameManager] popularAddUiText が未設定です。人気増加分表示の TMP_Text を Refs に割り当ててください。");
            }

            if (buzzUiText == null)
            {
                Debug.LogWarning(
                    "[GameManager] buzzUiText が未設定です。ButtonDebugBuzz/BuzzView の TMP_Text を Refs に割り当ててください。");
            }
        }

        private void EnsureMoneyPopularAddUiRectsCached()
        {
            ResolveMainMoneyPopularityUiTextIfNeeded();
            ResolveBuzzUiTextIfNeeded();
        }

        private void ResolveBuzzUiTextIfNeeded()
        {
            if (buzzUiText != null)
            {
                return;
            }

            GameObject byPath = GameObject.Find("PanelCanvas/PanelObject/ButtonDebugBuzz/BuzzView");
            if (byPath != null)
            {
                buzzUiText = byPath.GetComponent<TMP_Text>();
                if (buzzUiText != null)
                {
                    return;
                }
            }

            Transform[] all = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "BuzzView")
                {
                    continue;
                }

                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null)
                {
                    buzzUiText = text;
                    return;
                }
            }
        }

        private void ResolveMainMoneyPopularityUiTextIfNeeded()
        {
            if (moneyUiText == null)
            {
                GameObject byPath = GameObject.Find("PanelCanvas/MoneyUIBackground/MoneyUI");
                if (byPath != null)
                {
                    moneyUiText = byPath.GetComponent<TMP_Text>();
                }
            }

            if (popularUiText == null)
            {
                GameObject byPath = GameObject.Find("PanelCanvas/PopularUIBackground/PopularUI");
                if (byPath != null)
                {
                    popularUiText = byPath.GetComponent<TMP_Text>();
                }
            }
        }

        private void SyncNamedMainUiTexts()
        {
            Transform[] all = FindObjectsOfType<Transform>(true);
            string money = currentMoney.ToString("N0");
            string popularity = currentPopularity.ToString("N0");
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                {
                    continue;
                }

                if (t.name == "MoneyUI")
                {
                    TMP_Text text = t.GetComponent<TMP_Text>();
                    if (text != null && text.text != money)
                    {
                        text.text = money;
                    }
                }
                else if (t.name == "PopularUI")
                {
                    TMP_Text text = t.GetComponent<TMP_Text>();
                    if (text != null && text.text != popularity)
                    {
                        text.text = popularity;
                    }
                }
            }
        }

        private void ProcessMoneyOperations()
        {
            if (pendingMoneyOperations.Count == 0)
            {
                return;
            }

            long totalDelta = 0;
            bool hasMoviePayoutIncomeInBatch = false;
            while (pendingMoneyOperations.Count > 0)
            {
                MoneyOperation operation = pendingMoneyOperations.Dequeue();
                queuedMoneyDeltaPreview -= operation.Delta;
                if (enableOperationDeduplication && IsMoneyOperationAlreadyProcessed(operation.OperationId))
                {
                    continue;
                }

                if (enableOperationDeduplication)
                {
                    MarkMoneyOperationProcessed(operation.OperationId);
                }

                Game02AlienProgressTracker.EnsureExists()?.RecordProcessedMoneyDelta(
                    operation.Delta,
                    operation.Reason,
                    operation.SourceId);

                if (operation.Delta > 0L && string.Equals(operation.Reason, "WorkMovie payout", StringComparison.Ordinal))
                {
                    hasMoviePayoutIncomeInBatch = true;
                }

                try
                {
                    checked
                    {
                        totalDelta += operation.Delta;
                    }
                }
                catch (OverflowException)
                {
                    TriggerFatalError(
                        $"[GameManager] currentMoney overflow while summing deltas. opId={operation.OperationId}, delta={operation.Delta}, source={operation.SourceId}, reason={operation.Reason}");
                    return;
                }
            }

            if (totalDelta == 0)
            {
                return;
            }

            long nextMoney;
            try
            {
                checked
                {
                    nextMoney = currentMoney + totalDelta;
                }
            }
            catch (OverflowException)
            {
                TriggerFatalError($"[GameManager] currentMoney overflow. money={currentMoney}, delta={totalDelta}, max={maxMoney}");
                return;
            }

            if (nextMoney < 0)
            {
                TriggerFatalError($"[GameManager] currentMoney underflow. money={nextMoney}");
                return;
            }

            if (nextMoney > maxMoney)
            {
                TriggerFatalError($"[GameManager] currentMoney overflow. money={nextMoney}, max={maxMoney}");
                return;
            }

            currentMoney = nextMoney;
            ApplyMoneyText();
            if (totalDelta > 0)
            {
                ShowMoneyAdd(totalDelta);
            }

            if (hasMoviePayoutIncomeInBatch)
            {
                TryShowFirstMovieIncomeMessage();
            }
        }

        private void ProcessPopularityOperations()
        {
            if (pendingPopularityOperations.Count == 0)
            {
                return;
            }

            long totalDelta = 0;
            while (pendingPopularityOperations.Count > 0)
            {
                PopularityOperation operation = pendingPopularityOperations.Dequeue();
                if (enableOperationDeduplication && IsPopularityOperationAlreadyProcessed(operation.OperationId))
                {
                    continue;
                }

                if (enableOperationDeduplication)
                {
                    MarkPopularityOperationProcessed(operation.OperationId);
                }

                try
                {
                    checked
                    {
                        totalDelta += operation.Delta;
                    }
                }
                catch (OverflowException)
                {
                    TriggerFatalError(
                        $"[GameManager] currentPopularity overflow while summing deltas. opId={operation.OperationId}, delta={operation.Delta}, source={operation.SourceId}, reason={operation.Reason}");
                    return;
                }
            }

            if (totalDelta == 0)
            {
                return;
            }

            long nextPopularity;
            try
            {
                checked
                {
                    nextPopularity = currentPopularity + totalDelta;
                }
            }
            catch (OverflowException)
            {
                TriggerFatalError($"[GameManager] currentPopularity overflow. popularity={currentPopularity}, delta={totalDelta}, max={maxPopularity}");
                return;
            }

            if (nextPopularity < 0)
            {
                TriggerFatalError($"[GameManager] currentPopularity underflow. popularity={nextPopularity}");
                return;
            }

            if (nextPopularity > maxPopularity)
            {
                TriggerFatalError($"[GameManager] currentPopularity overflow. popularity={nextPopularity}, max={maxPopularity}");
                return;
            }

            long previousPopularity = currentPopularity;
            currentPopularity = nextPopularity;
            ApplyPopularText();
            if (totalDelta > 0)
            {
                ShowPopularAdd(totalDelta);
            }

            TryShowSupportEndedMessageOnPopularityCross(previousPopularity, currentPopularity);

            Game02MsgManager.TryUnlockTenMillionPopularityAchievementIfReached(previousPopularity, currentPopularity);
            Game02MsgManager.TryGet()?.NotifyPopularityIncreased(previousPopularity, currentPopularity, this);
            TryShowGameClearedPanel(previousPopularity, currentPopularity);
            Game02AlienProgressTracker.EnsureExists()?.NotifyProgressRelevantWorldStateChanged();
        }

        private void TryShowFirstMovieIncomeMessage()
        {
            Game02MsgManager.TryGet()?.TryPresentFirstMovieIncomeMessage(this);
        }

        public void OnMovieUploadAccepted()
        {
            long supportAmount = Math.Max(0L, supportMoneyPerUpload);
            long popularityCap = Math.Max(0L, supportPopularityCap);
            if (supportAmount > 0L && currentPopularity <= popularityCap)
            {
                RequestMoneyDelta(supportAmount, "Early upload support", "GameManager.Support", -1);
                hasReceivedSupportReward = true;
                return;
            }

            TryShowSupportEndedMessage();
        }

        private void TryShowSupportEndedMessageOnPopularityCross(long previousPopularity, long nextPopularity)
        {
            if (!hasReceivedSupportReward || hasShownSupportEndedMessage)
            {
                return;
            }

            long cap = Math.Max(0L, supportPopularityCap);
            long before = Math.Max(0L, previousPopularity);
            long after = Math.Max(0L, nextPopularity);
            if (before <= cap && after > cap)
            {
                TryShowSupportEndedMessage();
            }
        }

        private void TryShowSupportEndedMessage()
        {
            if (!hasReceivedSupportReward || hasShownSupportEndedMessage)
            {
                return;
            }

            hasShownSupportEndedMessage = true;
            string message = string.IsNullOrEmpty(supportEndedMessage)
                ? "運営の援助がなくなりました"
                : supportEndedMessage;
            Game02MsgManager.TryGet()?.PresentSupportEndedMessage(message, Mathf.Max(0f, movieRateTierMessageHoldSeconds));
        }

        private static string FormatUnitPrice(double unitPrice)
        {
            double safe = Math.Max(0d, unitPrice);
            return safe.ToString("0.###");
        }

        /// <summary>
        /// セーブ適用直後。演出済みでなく既に人気がクリア閾値以上なら、プレイ中と同様にクリア演出を一度だけ適用する。
        /// </summary>
        private void TryApplyGameClearAfterLoadIfNeeded()
        {
            if (hasShownGameClearedPanel)
            {
                return;
            }

            long threshold = GameClearPopularityThreshold;
            if (currentPopularity < threshold)
            {
                return;
            }

            PresentGameClearCeremonyOnce();
        }

        private void TryShowGameClearedPanel(long previousPopularity, long nextPopularity)
        {
            if (hasShownGameClearedPanel)
            {
                return;
            }

            long before = Math.Max(0L, previousPopularity);
            long after = Math.Max(0L, nextPopularity);
            long threshold = GameClearPopularityThreshold;
            // 閾値ちょうど（例: 1_000_000）到達でも発火するよう >= とする。
            if (before >= threshold || after < threshold)
            {
                return;
            }

            PresentGameClearCeremonyOnce();
        }

        /// <summary>クリア演出を一通り適用し、セーブ用フラグを立てる（各セッションで一度だけ）。</summary>
        private void PresentGameClearCeremonyOnce()
        {
            hasShownGameClearedPanel = true;
            SetGameClearedPanelVisible(true);
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_06);
            MarkGameCleared();
            SoundSettingsManager.Instance?.MarkGame02Cleared();
            SetPaused(true, showPausePanelWhenPaused: false);
            GameObject panel = ResolveGameClearedPanel();
            GameClearedCeremonyController ceremony =
                panel != null ? panel.GetComponent<GameClearedCeremonyController>() : null;
            ceremony?.BeginCeremony(this);
        }

        private void SetGameClearedPanelVisible(bool visible)
        {
            GameObject panel = ResolveGameClearedPanel();
            if (panel == null)
            {
                return;
            }

            if (panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }

        private GameObject ResolveGameClearedPanel()
        {
            if (gameClearedPanel != null)
            {
                return gameClearedPanel;
            }

            gameClearedPanel = GameObject.Find("PanelCanvas/GameClearedPanel");
            if (gameClearedPanel != null)
            {
                return gameClearedPanel;
            }

            Transform[] all = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "GameClearedPanel")
                {
                    continue;
                }

                Transform parent = t.parent;
                if (parent != null && parent.name == "PanelCanvas")
                {
                    gameClearedPanel = t.gameObject;
                    return gameClearedPanel;
                }
            }

            return gameClearedPanel;
        }

        private void ProcessBuzzOperations()
        {
            if (pendingBuzzOperations.Count == 0)
            {
                return;
            }

            long totalDelta = 0;
            while (pendingBuzzOperations.Count > 0)
            {
                BuzzOperation operation = pendingBuzzOperations.Dequeue();
                if (enableOperationDeduplication && IsBuzzOperationAlreadyProcessed(operation.OperationId))
                {
                    continue;
                }

                if (enableOperationDeduplication)
                {
                    MarkBuzzOperationProcessed(operation.OperationId);
                }

                Game02AlienProgressTracker.EnsureExists()?.RecordProcessedBuzzDelta(operation.Delta);

                try
                {
                    checked
                    {
                        totalDelta += operation.Delta;
                    }
                }
                catch (OverflowException)
                {
                    TriggerFatalError(
                        $"[GameManager] currentBuzz overflow while summing deltas. opId={operation.OperationId}, delta={operation.Delta}, source={operation.SourceId}, reason={operation.Reason}");
                    return;
                }
            }

            if (totalDelta == 0)
            {
                return;
            }

            long nextBuzz;
            try
            {
                checked
                {
                    nextBuzz = currentBuzz + totalDelta;
                }
            }
            catch (OverflowException)
            {
                TriggerFatalError($"[GameManager] currentBuzz overflow. buzz={currentBuzz}, delta={totalDelta}, max={maxBuzz}");
                return;
            }

            if (nextBuzz < 0)
            {
                TriggerFatalError($"[GameManager] currentBuzz underflow. buzz={nextBuzz}");
                return;
            }

            if (nextBuzz > maxBuzz)
            {
                TriggerFatalError($"[GameManager] currentBuzz overflow. buzz={nextBuzz}, max={maxBuzz}");
                return;
            }

            currentBuzz = nextBuzz;
            ApplyBuzzText();
        }

        private bool IsMoneyOperationAlreadyProcessed(long operationId)
        {
            return processedMoneyOperationIds.Contains(operationId);
        }

        private void MarkMoneyOperationProcessed(long operationId)
        {
            if (!processedMoneyOperationIds.Add(operationId))
            {
                return;
            }

            processedMoneyOperationOrder.Enqueue(operationId);
            TrimProcessedRing(processedMoneyOperationOrder, processedMoneyOperationIds);
        }

        private bool IsPopularityOperationAlreadyProcessed(long operationId)
        {
            return processedPopularityOperationIds.Contains(operationId);
        }

        private void MarkPopularityOperationProcessed(long operationId)
        {
            if (!processedPopularityOperationIds.Add(operationId))
            {
                return;
            }

            processedPopularityOperationOrder.Enqueue(operationId);
            TrimProcessedRing(processedPopularityOperationOrder, processedPopularityOperationIds);
        }

        private bool IsBuzzOperationAlreadyProcessed(long operationId)
        {
            return processedBuzzOperationIds.Contains(operationId);
        }

        private void MarkBuzzOperationProcessed(long operationId)
        {
            if (!processedBuzzOperationIds.Add(operationId))
            {
                return;
            }

            processedBuzzOperationOrder.Enqueue(operationId);
            TrimProcessedRing(processedBuzzOperationOrder, processedBuzzOperationIds);
        }

        private void TrimProcessedRing(Queue<long> orderQueue, HashSet<long> idSet)
        {
            int capacity = Mathf.Max(1, processedOperationCapacity);
            while (orderQueue.Count > capacity)
            {
                long removedId = orderQueue.Dequeue();
                idSet.Remove(removedId);
            }
        }

        private long NextAutoMoneyOperationId()
        {
            autoIssuedMoneyOperationId += 1;
            return autoIssuedMoneyOperationId;
        }

        private long NextAutoPopularityOperationId()
        {
            autoIssuedPopularityOperationId += 1;
            return autoIssuedPopularityOperationId;
        }

        private long NextAutoBuzzOperationId()
        {
            autoIssuedBuzzOperationId += 1;
            return autoIssuedBuzzOperationId;
        }

        private void ApplyMoneyText()
        {
            inspectorRuntimeCurrentMoney = currentMoney;
            if (moneyUiText == null)
            {
                return;
            }

            moneyUiText.text = currentMoney.ToString("N0");
        }

        private void ApplyPopularText()
        {
            inspectorRuntimeCurrentPopularity = currentPopularity;
            if (popularUiText == null)
            {
                return;
            }

            popularUiText.text = currentPopularity.ToString("N0");
        }

        private void ApplyBuzzText()
        {
            if (buzzUiText == null)
            {
                return;
            }

            buzzUiText.text = currentBuzz.ToString("N0");
        }

        private void RefreshMainMoneyPopularityUiFromCurrentState()
        {
            ApplyMoneyText();
            ApplyPopularText();
        }

        private void InitializeMoneyAddUi()
        {
            if (moneyAddUiText == null)
            {
                return;
            }

            if (game02EffectManager != null && game02EffectManager.ClearAddUi(moneyAddUiText))
            {
                return;
            }

            moneyAddUiText.text = string.Empty;
            Color color = moneyAddUiText.color;
            color.a = 0f;
            moneyAddUiText.color = color;
            if (moneyAddUiText.gameObject.activeSelf)
            {
                moneyAddUiText.gameObject.SetActive(false);
            }
        }

        private void InitializePopularAddUi()
        {
            if (popularAddUiText == null)
            {
                return;
            }

            if (game02EffectManager != null && game02EffectManager.ClearAddUi(popularAddUiText))
            {
                return;
            }

            popularAddUiText.text = string.Empty;
            Color color = popularAddUiText.color;
            color.a = 0f;
            popularAddUiText.color = color;
            if (popularAddUiText.gameObject.activeSelf)
            {
                popularAddUiText.gameObject.SetActive(false);
            }
        }

        private void ShowMoneyAdd(long delta)
        {
            if (moneyAddUiText == null)
            {
                return;
            }

            if (game02EffectManager != null && game02EffectManager.PlayAddUi(moneyAddUiText, delta))
            {
                return;
            }

            if (!moneyAddUiText.gameObject.activeSelf)
            {
                moneyAddUiText.gameObject.SetActive(true);
            }

            moneyAddUiText.text = $"+{delta:N0}";
            Color color = moneyAddUiText.color;
            color.a = 1f;
            moneyAddUiText.color = color;
        }

        private void ShowPopularAdd(long delta)
        {
            if (popularAddUiText == null)
            {
                return;
            }

            if (game02EffectManager != null && game02EffectManager.PlayAddUi(popularAddUiText, delta))
            {
                return;
            }

            if (!popularAddUiText.gameObject.activeSelf)
            {
                popularAddUiText.gameObject.SetActive(true);
            }

            popularAddUiText.text = $"+{delta:N0}";
            Color color = popularAddUiText.color;
            color.a = 1f;
            popularAddUiText.color = color;
        }

        private void TriggerFatalError(string message)
        {
            hasFatalError = true;
            isPaused = true;
            suppressPausePanelWhilePaused = false;
            Debug.LogError(message);
        }

        private static string ResolveDebugStreamGenre()
        {
            ItemStreamGenreCatalog catalog = FindObjectOfType<ItemStreamGenreCatalog>(true);
            if (catalog == null)
            {
                return ItemStreamGenreCatalog.DefaultGenre;
            }

            return catalog.PickRandomGenre();
        }

        private static string ResolveDebugMovieName(string streamGenre)
        {
            MovieNameCatalog catalog = FindObjectOfType<MovieNameCatalog>(true);
            if (catalog == null)
            {
                return MovieNameCatalog.DefaultMovieName;
            }

            return catalog.PickRandomMovieName(streamGenre);
        }
    }
}
