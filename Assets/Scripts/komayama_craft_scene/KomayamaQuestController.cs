using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    /// <summary>
    /// メインクエスト進行（チュートリアル：現状把握 → 雨漏りを直そう）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class KomayamaQuestController : MonoBehaviour
    {
        public const string SituationSurveyQuestId = "main_situation_survey";
        public const string RainLeakQuestId = "main_rain_leak";
        public const string LeakIronScaleObjectiveId = "leak_iron_scale_drops";
        public const string LeakPlaceBenchObjectiveId = "leak_place_rolling_bench";
        public const string LeakPickupIronScaleObjectiveId = "leak_pickup_iron_scale";
        public const string LeakDepositIronScaleObjectiveId = "leak_deposit_iron_scale";
        public const string LeakSelectPlateRecipeObjectiveId = "leak_select_plate_recipe";
        public const string LeakProducePlateObjectiveId = "leak_produce_plate";
        public const string LeakDeliverPlateObjectiveId = "leak_deliver_plate";
        public const string FuelRefillQuestId = "main_refuel_bench";
        public const string IronScaleItemId = "item.iron_scale";
        public const string IronScalePlateItemId = "item.iron_scale_plate";
        public const string ScaleRollingWorkbenchId = "facility.scale_rolling_workbench";
        public const string IronScaleRollingRecipeId = "recipe.iron_scale_rolling";
        public const int TutorialWorkbenchFuelCharges = 3;

        public static KomayamaQuestController Instance { get; private set; }

        /// <summary>建設・設定メニューとセーブが解禁されているか。</summary>
        public bool AreBuildAndSettingsUnlocked => menusUnlocked;

        /// <summary>雨漏り：作業台仮組の配置待ち監視中。</summary>
        public bool IsAwaitingWorkbenchPlacement => rainLeakPlaceMonitoring;

        /// <summary>雨漏り：仮組への建設資材投入待ち監視中。</summary>
        public bool IsAwaitingWorkbenchConstruction => rainLeakFeedMonitoring;

        /// <summary>雨漏り：鱗鉄板レシピ選択待ち監視中。</summary>
        public bool IsAwaitingPlateRecipeSelection => rainLeakRecipeMonitoring;

        /// <summary>雨漏り：鱗鉄板生産待ち監視中。</summary>
        public bool IsAwaitingPlateProduction => rainLeakProduceMonitoring;

        /// <summary>雨漏り：鱗鉄板納品待ち監視中。</summary>
        public bool IsAwaitingPlateDelivery => rainLeakDeliverMonitoring;

        /// <summary>鉄鱗ドロップ集め監視中。</summary>
        public bool IsAwaitingIronScaleGather => rainLeakMonitoring;

        public bool TryGetQuestStatus(string questId, out QuestSaveStatus status)
        {
            QuestRuntime quest = EnsureQuestSlot(questId);
            status = quest.Status;
            return quest != null;
        }

        public bool IsQuestObjectiveDone(string questId, string objectiveId)
        {
            QuestRuntime quest = EnsureQuestSlot(questId);
            return IsObjectiveDone(quest, objectiveId);
        }

        public int GetQuestCounter(string questId, string counterId)
        {
            QuestRuntime quest = EnsureQuestSlot(questId);
            return GetCounter(quest, counterId);
        }

        [Header("参照")]
        [SerializeField] private KomayamaQuestLogView questLogView;
        [SerializeField] private KomayamaCraftDialogueOverlay dialogueOverlay;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField] private Transform shipFocusTarget;
        [SerializeField] private Transform ironScaleBeastTarget;
        [SerializeField] private KomayamaShipRepairCinematic shipRepairCinematic;
        [SerializeField] private KomayamaShipVisual shipVisual;
        [SerializeField] private KomayamaNpc deliveryNpc;
        [SerializeField] private KomayamaSaveService saveService;
        [SerializeField] private KomayamaCraftDebugManager debugManager;

        [Header("メニュー（チュートリアル解禁）")]
        [SerializeField] private GameObject menuObjectRoot;
        [SerializeField] private GameObject menuBuildButton;
        [SerializeField] private GameObject menuSettingsButton;

        [Header("現状把握")]
        [SerializeField] private string situationStartStageKey = "craft_quest_situation_start";
        [SerializeField] private string situationEndStageKey = "craft_quest_situation_end";
        [SerializeField, Min(0.01f)] private float keyHoldSecondsRequired = 0.1f;
        [SerializeField, Min(0.01f)] private float situationCameraReturnSeconds = 1.2f;

        [Header("雨漏りを直そう")]
        [SerializeField] private string leakIntroAStageKey = "craft_quest_leak_intro_a";
        [SerializeField] private string leakIntroBStageKey = "craft_quest_leak_intro_b";
        [SerializeField] private string leakMakeBenchStageKey = "craft_quest_leak_make_bench";
        [SerializeField] private string leakAfterPlaceStageKey = "craft_quest_leak_after_place";
        [SerializeField] private string leakFeedStageKey = "craft_quest_leak_feed";
        [SerializeField] private string leakPickRecipeStageKey = "craft_quest_leak_pick_recipe";
        [SerializeField] private string leakAfterRecipeStageKey = "craft_quest_leak_after_recipe";
        [SerializeField] private string leakAskDeliverStageKey = "craft_quest_leak_ask_deliver";
        [SerializeField] private string leakDeliverHintStageKey = "craft_quest_leak_deliver_hint";
        [SerializeField] private string leakDeliverDoneStageKey = "craft_quest_leak_deliver_done";
        [SerializeField] private string npcDefaultStageKey = "craft_npc_unit05_default";
        [SerializeField, Min(0.01f)] private float leakCameraMoveSeconds = 1.2f;
        [SerializeField, Min(1)] private int leakIronScaleRequired = 3;
        [SerializeField, Min(1)] private int leakPickupRequired = 3;
        [SerializeField, Min(1)] private int leakDepositRequired = 3;
        [SerializeField, Min(1)] private int leakProducePlateRequired = 3;
        [SerializeField, Min(1)] private int leakDeliverPlateRequired = 3;

        private readonly Dictionary<string, QuestRuntime> quests = new(StringComparer.Ordinal);
        private bool openingPhaseEnded;
        private bool pendingPlayedOpening;
        private bool pendingIsNewGameSession;
        private bool situationMonitoring;
        private bool situationCompleting;
        private bool rainLeakMonitoring;
        private bool rainLeakPlaceMonitoring;
        private bool rainLeakFeedMonitoring;
        private bool rainLeakRecipeMonitoring;
        private bool rainLeakProduceMonitoring;
        private bool rainLeakDeliverMonitoring;
        private bool rainLeakIntroRunning;
        private bool rainLeakPhaseRunning;
        private bool saveApplied;
        private bool debugStartConsumed;
        private bool menusUnlocked;
        private bool presentationHudSuppressed;
        private KomayamaProvisionalFacility tutorialProvisional;
        private KomayamaDepositBin deliveryNpcBin;
        private float holdW;
        private float holdA;
        private float holdS;
        private float holdD;

        private void Awake()
        {
            Instance = this;
            EnsureSituationQuest();
            EnsureRainLeakQuest();
            EnsureFuelRefillQuest();
            if (deliveryNpc != null)
            {
                deliveryNpcBin = deliveryNpc.GetComponent<KomayamaDepositBin>();
            }

            ApplyMenuVisibility();
        }

        private void OnEnable()
        {
            KomayamaDropArea.GroundItemSpawned += OnGroundItemSpawned;
            KomayamaBuildController.ProvisionalFacilityPlaced += OnProvisionalFacilityPlaced;
            KomayamaProvisionalFacility.ConstructionMaterialDeposited += OnConstructionMaterialDeposited;
            KomayamaProvisionalFacility.FacilityConstructionCompleted += OnFacilityConstructionCompleted;
            KomayamaCraftInputController.GroundItemPickedUp += OnGroundItemPickedUp;
            KomayamaProcessingFacility.RecipeSelected += OnRecipeSelected;
            KomayamaProcessingFacility.ItemProduced += OnFacilityItemProduced;
            KomayamaDepositBin.ItemDeposited += OnDepositBinItemDeposited;
        }

        private void OnDisable()
        {
            KomayamaDropArea.GroundItemSpawned -= OnGroundItemSpawned;
            KomayamaBuildController.ProvisionalFacilityPlaced -= OnProvisionalFacilityPlaced;
            KomayamaProvisionalFacility.ConstructionMaterialDeposited -= OnConstructionMaterialDeposited;
            KomayamaProvisionalFacility.FacilityConstructionCompleted -= OnFacilityConstructionCompleted;
            KomayamaCraftInputController.GroundItemPickedUp -= OnGroundItemPickedUp;
            KomayamaProcessingFacility.RecipeSelected -= OnRecipeSelected;
            KomayamaProcessingFacility.ItemProduced -= OnFacilityItemProduced;
            KomayamaDepositBin.ItemDeposited -= OnDepositBinItemDeposited;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            StartCoroutine(BootstrapAfterSave());
        }

        private IEnumerator BootstrapAfterSave()
        {
            yield return null;
            saveApplied = true;
            RefreshQuestLog();

            if (TryApplyQuestDebugBootstrap())
            {
                yield break;
            }

            if (openingPhaseEnded)
            {
                HandleOpeningPhaseEndedInternal();
            }
        }

        /// <summary>OP 完了またはスキップ後に Opening から呼ばれる。</summary>
        public void NotifyOpeningPhaseEnded(bool playedOpening, bool isNewGameSession)
        {
            openingPhaseEnded = true;
            pendingPlayedOpening = playedOpening;
            pendingIsNewGameSession = isNewGameSession;
            if (!saveApplied)
            {
                return;
            }

            if (TryApplyQuestDebugBootstrap())
            {
                return;
            }

            HandleOpeningPhaseEndedInternal();
        }

        /// <summary>
        /// <see cref="KomayamaQuestDebugController"/> から呼ばれる開始シナリオ。
        /// </summary>
        public bool DebugBootstrapSituationSurveyIntro()
        {
            if (!BeginDebugBootstrap())
            {
                return false;
            }

            EnsureRainLeakQuest().Status = QuestSaveStatus.Inactive;
            EnsureRainLeakQuest().Counters.Clear();
            EnsureRainLeakQuest().CompletedObjectiveIds.Clear();
            StartCoroutine(StartSituationSurveyRoutine());
            return true;
        }

        public bool DebugBootstrapRainLeakIntro()
        {
            if (!BeginDebugBootstrap())
            {
                return false;
            }

            CompleteSituationSurveyForDebug();
            StartCoroutine(StartRainLeakRoutine());
            return true;
        }

        /// <summary>雨漏り納品待ちまで進め、納品箱を有効化する（地面の鉄板はデバッグ側が生成）。</summary>
        public bool DebugBootstrapRainLeakPreDelivery()
        {
            if (!BeginDebugBootstrap())
            {
                return false;
            }

            CompleteSituationSurveyForDebug();

            QuestRuntime leak = EnsureRainLeakQuest();
            leak.Status = QuestSaveStatus.Active;
            leak.Counters.Clear();
            leak.CompletedObjectiveIds.Clear();

            MarkObjectiveDone(leak, LeakIronScaleObjectiveId);
            leak.Counters[LeakIronScaleObjectiveId] = leakIronScaleRequired;
            MarkObjectiveDone(leak, LeakPlaceBenchObjectiveId);
            leak.Counters[LeakPlaceBenchObjectiveId] = 1;
            MarkObjectiveDone(leak, LeakPickupIronScaleObjectiveId);
            leak.Counters[LeakPickupIronScaleObjectiveId] = leakPickupRequired;
            MarkObjectiveDone(leak, LeakDepositIronScaleObjectiveId);
            leak.Counters[LeakDepositIronScaleObjectiveId] = leakDepositRequired;
            MarkObjectiveDone(leak, LeakSelectPlateRecipeObjectiveId);
            MarkObjectiveDone(leak, LeakProducePlateObjectiveId);
            leak.Counters[LeakProducePlateObjectiveId] = leakProducePlateRequired;
            leak.Counters[LeakDeliverPlateObjectiveId] = 0;

            UnlockBuildAndSettingsMenus();
            BeginRainLeakDeliverMonitoring();
            ApplyDeliveryNpcState();
            RefreshQuestLog();
            return true;
        }

        private bool TryApplyQuestDebugBootstrap()
        {
            if (debugStartConsumed || debugManager == null || debugManager.ProductionReleaseBuild)
            {
                return false;
            }

            KomayamaQuestDebugController controller = debugManager.QuestDebugController;
            if (controller == null || !controller.HasActiveScenario)
            {
                return false;
            }

            return controller.TryBootstrap();
        }

        private bool BeginDebugBootstrap()
        {
            if (debugStartConsumed)
            {
                return false;
            }

            debugStartConsumed = true;
            StopMonitoringAll();
            return true;
        }

        private void CompleteSituationSurveyForDebug()
        {
            QuestRuntime situation = EnsureSituationQuest();
            situation.Status = QuestSaveStatus.Completed;
            MarkObjectiveDone(situation, "wasd_w");
            MarkObjectiveDone(situation, "wasd_a");
            MarkObjectiveDone(situation, "wasd_s");
            MarkObjectiveDone(situation, "wasd_d");
        }

        private void HandleOpeningPhaseEndedInternal()
        {
            QuestRuntime leak = EnsureRainLeakQuest();
            if (leak.Status == QuestSaveStatus.Completed)
            {
                ApplyFuelQuestAcceptIcon();
                RefreshQuestLog();
                return;
            }

            if (leak.Status == QuestSaveStatus.Active)
            {
                UnlockBuildAndSettingsMenus();

                if (IsObjectiveDone(leak, LeakDeliverPlateObjectiveId))
                {
                    RefreshQuestLog();
                    return;
                }

                if (IsObjectiveDone(leak, LeakProducePlateObjectiveId) &&
                    !IsObjectiveDone(leak, LeakDeliverPlateObjectiveId))
                {
                    BeginRainLeakDeliverMonitoring();
                    ApplyDeliveryNpcState();
                    RefreshQuestLog();
                    return;
                }

                if (IsObjectiveDone(leak, LeakSelectPlateRecipeObjectiveId) &&
                    !IsObjectiveDone(leak, LeakProducePlateObjectiveId))
                {
                    BeginRainLeakProduceMonitoring();
                    RefreshQuestLog();
                    return;
                }

                if (IsObjectiveDone(leak, LeakPickupIronScaleObjectiveId) &&
                    IsObjectiveDone(leak, LeakDepositIronScaleObjectiveId) &&
                    !IsObjectiveDone(leak, LeakSelectPlateRecipeObjectiveId))
                {
                    BeginRainLeakRecipeMonitoring();
                    RefreshQuestLog();
                    return;
                }

                if (IsObjectiveDone(leak, LeakPlaceBenchObjectiveId) &&
                    (!IsObjectiveDone(leak, LeakPickupIronScaleObjectiveId) ||
                     !IsObjectiveDone(leak, LeakDepositIronScaleObjectiveId)))
                {
                    BeginRainLeakFeedMonitoring();
                    RefreshQuestLog();
                    return;
                }

                if (IsObjectiveDone(leak, LeakIronScaleObjectiveId) &&
                    !IsObjectiveDone(leak, LeakPlaceBenchObjectiveId))
                {
                    BeginRainLeakPlaceMonitoring();
                }
                else if (!IsObjectiveDone(leak, LeakIronScaleObjectiveId))
                {
                    BeginRainLeakMonitoring();
                }

                RefreshQuestLog();
                return;
            }

            QuestRuntime situation = EnsureSituationQuest();
            if (situation.Status == QuestSaveStatus.Completed)
            {
                StartCoroutine(StartRainLeakRoutine());
                return;
            }

            if (situation.Status == QuestSaveStatus.Active)
            {
                BeginSituationMonitoring(resetHolds: false);
                RefreshQuestLog();
                return;
            }

            if (!pendingPlayedOpening && !pendingIsNewGameSession)
            {
                RefreshQuestLog();
                return;
            }

            StartCoroutine(StartSituationSurveyRoutine());
        }

        private IEnumerator StartSituationSurveyRoutine()
        {
            QuestRuntime quest = EnsureSituationQuest();
            quest.Status = QuestSaveStatus.Active;
            ClearSituationObjectiveCompletions(quest);
            RefreshQuestLog();

            yield return PlayDialogue(situationStartStageKey);
            BeginSituationMonitoring(resetHolds: true);
            RefreshQuestLog();
        }

        private void BeginSituationMonitoring(bool resetHolds)
        {
            situationMonitoring = true;
            situationCompleting = false;
            rainLeakMonitoring = false;
            if (resetHolds)
            {
                holdW = 0f;
                holdA = 0f;
                holdS = 0f;
                holdD = 0f;
            }
        }

        private void Update()
        {
            UpdatePresentationHudVisibility();
            UpdateSituationMonitoring();
        }

        private void UpdatePresentationHudVisibility()
        {
            bool suppress = IsBlockingPresentation() ||
                KomayamaShipRepairCinematic.IsPlaying;
            if (suppress == presentationHudSuppressed)
            {
                return;
            }

            presentationHudSuppressed = suppress;
            questLogView?.SetPresentationSuppressed(suppress);
            ApplyMenuVisibility();

            if (suppress)
            {
                FindFirstObjectByType<KomayamaFacilityMenuView>()?.Close();
                FindFirstObjectByType<KomayamaBuildMenuSlide>()?.Close();
            }
        }

        private void UpdateSituationMonitoring()
        {
            if (!situationMonitoring || situationCompleting)
            {
                return;
            }

            if (IsBlockingPresentation())
            {
                return;
            }

            QuestRuntime quest = EnsureSituationQuest();
            bool changed = false;
            Keyboard keyboard = Keyboard.current;

            bool holdWKey = (keyboard != null && keyboard.wKey.isPressed) ||
                (KomayamaCraftAutoPlayInput.IsActive && KomayamaCraftAutoPlayInput.HoldW);
            bool holdAKey = (keyboard != null && keyboard.aKey.isPressed) ||
                (KomayamaCraftAutoPlayInput.IsActive && KomayamaCraftAutoPlayInput.HoldA);
            bool holdSKey = (keyboard != null && keyboard.sKey.isPressed) ||
                (KomayamaCraftAutoPlayInput.IsActive && KomayamaCraftAutoPlayInput.HoldS);
            bool holdDKey = (keyboard != null && keyboard.dKey.isPressed) ||
                (KomayamaCraftAutoPlayInput.IsActive && KomayamaCraftAutoPlayInput.HoldD);

            changed |= TryAccumulateKey(quest, "wasd_w", holdWKey, ref holdW);
            changed |= TryAccumulateKey(quest, "wasd_a", holdAKey, ref holdA);
            changed |= TryAccumulateKey(quest, "wasd_s", holdSKey, ref holdS);
            changed |= TryAccumulateKey(quest, "wasd_d", holdDKey, ref holdD);

            if (changed)
            {
                RefreshQuestLog();
            }

            if (AreAllSituationObjectivesDone(quest))
            {
                situationMonitoring = false;
                situationCompleting = true;
                StartCoroutine(CompleteSituationSurveyRoutine());
            }
        }

        private bool TryAccumulateKey(
            QuestRuntime quest,
            string objectiveId,
            bool pressed,
            ref float hold)
        {
            if (IsObjectiveDone(quest, objectiveId) || !pressed)
            {
                return false;
            }

            hold += Time.deltaTime;
            if (hold < keyHoldSecondsRequired)
            {
                return false;
            }

            MarkObjectiveDone(quest, objectiveId);
            return true;
        }

        private IEnumerator CompleteSituationSurveyRoutine()
        {
            bool pausePushed = false;
            if (gameClock != null)
            {
                gameClock.PushPause();
                pausePushed = true;
            }

            if (cameraController != null && shipFocusTarget != null)
            {
                cameraController.BeginDialogueFramingSnapshot();
                cameraController.FrameForDialogue(
                    shipFocusTarget.position,
                    orthographicSize: 0f,
                    situationCameraReturnSeconds);
                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, situationCameraReturnSeconds);
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                cameraController.EndDialogueFramingKeepPosition();
            }

            // カメラ戻し完了で達成。リストから外す。
            QuestRuntime quest = EnsureSituationQuest();
            quest.Status = QuestSaveStatus.Completed;
            situationCompleting = false;
            RefreshQuestLog();

            yield return PlayDialogue(situationEndStageKey, managePause: false);

            if (pausePushed && gameClock != null)
            {
                gameClock.PopPause();
            }

            StartCoroutine(StartRainLeakRoutine());
        }

        private IEnumerator StartRainLeakRoutine()
        {
            if (rainLeakIntroRunning)
            {
                yield break;
            }

            rainLeakIntroRunning = true;
            situationMonitoring = false;
            rainLeakMonitoring = false;

            QuestRuntime quest = EnsureRainLeakQuest();
            quest.Status = QuestSaveStatus.Active;
            quest.CompletedObjectiveIds.Remove(LeakIronScaleObjectiveId);
            quest.Counters[LeakIronScaleObjectiveId] = 0;
            RefreshQuestLog();

            bool pausePushed = false;
            if (gameClock != null)
            {
                gameClock.PushPause();
                pausePushed = true;
            }

            yield return PlayDialogue(leakIntroAStageKey, managePause: false);

            if (cameraController != null && ironScaleBeastTarget != null)
            {
                cameraController.BeginDialogueFramingSnapshot();
                cameraController.FrameForDialogue(
                    ironScaleBeastTarget.position,
                    orthographicSize: 0f,
                    leakCameraMoveSeconds);
                float elapsed = 0f;
                while (elapsed < leakCameraMoveSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return PlayDialogue(leakIntroBStageKey, managePause: false);

            if (cameraController != null)
            {
                cameraController.EndDialogueFramingKeepPosition();
            }

            if (pausePushed && gameClock != null)
            {
                gameClock.PopPause();
            }

            rainLeakIntroRunning = false;
            BeginRainLeakMonitoring();
            RefreshQuestLog();
        }

        private void BeginRainLeakMonitoring()
        {
            rainLeakMonitoring = true;
            rainLeakPlaceMonitoring = false;
            rainLeakFeedMonitoring = false;
            rainLeakRecipeMonitoring = false;
            rainLeakProduceMonitoring = false;
            rainLeakDeliverMonitoring = false;
            situationMonitoring = false;
        }

        private void BeginRainLeakPlaceMonitoring()
        {
            rainLeakPlaceMonitoring = true;
            rainLeakMonitoring = false;
            rainLeakFeedMonitoring = false;
            rainLeakRecipeMonitoring = false;
            rainLeakProduceMonitoring = false;
            rainLeakDeliverMonitoring = false;
            situationMonitoring = false;
        }

        private void BeginRainLeakFeedMonitoring()
        {
            rainLeakFeedMonitoring = true;
            rainLeakPlaceMonitoring = false;
            rainLeakMonitoring = false;
            rainLeakRecipeMonitoring = false;
            rainLeakProduceMonitoring = false;
            rainLeakDeliverMonitoring = false;
            situationMonitoring = false;
        }

        private void BeginRainLeakRecipeMonitoring()
        {
            rainLeakRecipeMonitoring = true;
            rainLeakFeedMonitoring = false;
            rainLeakPlaceMonitoring = false;
            rainLeakMonitoring = false;
            rainLeakProduceMonitoring = false;
            rainLeakDeliverMonitoring = false;
            situationMonitoring = false;
        }

        private void BeginRainLeakProduceMonitoring()
        {
            rainLeakProduceMonitoring = true;
            rainLeakRecipeMonitoring = false;
            rainLeakFeedMonitoring = false;
            rainLeakPlaceMonitoring = false;
            rainLeakMonitoring = false;
            rainLeakDeliverMonitoring = false;
            situationMonitoring = false;
        }

        private void BeginRainLeakDeliverMonitoring()
        {
            rainLeakDeliverMonitoring = true;
            rainLeakProduceMonitoring = false;
            rainLeakRecipeMonitoring = false;
            rainLeakFeedMonitoring = false;
            rainLeakPlaceMonitoring = false;
            rainLeakMonitoring = false;
            situationMonitoring = false;
        }

        private void OnGroundItemSpawned(ItemDefinition item, int amount)
        {
            if (!rainLeakMonitoring || item == null || amount <= 0)
            {
                return;
            }

            if (!string.Equals(item.DefinitionId, IronScaleItemId, StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (quest.Status != QuestSaveStatus.Active ||
                IsObjectiveDone(quest, LeakIronScaleObjectiveId))
            {
                return;
            }

            int current = GetCounter(quest, LeakIronScaleObjectiveId);
            current = Mathf.Min(leakIronScaleRequired, current + amount);
            quest.Counters[LeakIronScaleObjectiveId] = current;
            RefreshQuestLog();

            if (current >= leakIronScaleRequired)
            {
                MarkObjectiveDone(quest, LeakIronScaleObjectiveId);
                quest.Counters[LeakPlaceBenchObjectiveId] = 0;
                rainLeakMonitoring = false;
                UnlockBuildAndSettingsMenus();
                RefreshQuestLog();
                StartCoroutine(EnterPlaceBenchPhaseRoutine());
            }
        }

        private IEnumerator EnterPlaceBenchPhaseRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            yield return PlayDialogue(leakMakeBenchStageKey);
            BeginRainLeakPlaceMonitoring();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void OnProvisionalFacilityPlaced(KomayamaProvisionalFacility provisional)
        {
            if (!rainLeakPlaceMonitoring || provisional == null || provisional.Definition == null)
            {
                return;
            }

            if (!string.Equals(
                    provisional.Definition.DefinitionId,
                    ScaleRollingWorkbenchId,
                    StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (quest.Status != QuestSaveStatus.Active ||
                IsObjectiveDone(quest, LeakPlaceBenchObjectiveId))
            {
                return;
            }

            tutorialProvisional = provisional;
            quest.Counters[LeakPlaceBenchObjectiveId] = 1;
            MarkObjectiveDone(quest, LeakPlaceBenchObjectiveId);
            rainLeakPlaceMonitoring = false;
            RefreshQuestLog();
            StartCoroutine(AfterPlaceBenchRoutine());
        }

        private IEnumerator AfterPlaceBenchRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            yield return PlayDialogue(leakAfterPlaceStageKey);
            yield return PlayDialogue(leakFeedStageKey);
            QuestRuntime quest = EnsureRainLeakQuest();
            quest.Counters[LeakPickupIronScaleObjectiveId] = 0;
            quest.Counters[LeakDepositIronScaleObjectiveId] = 0;
            BeginRainLeakFeedMonitoring();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void OnGroundItemPickedUp(ItemDefinition item, int amount)
        {
            if (!rainLeakFeedMonitoring || item == null || amount <= 0)
            {
                return;
            }

            if (!string.Equals(item.DefinitionId, IronScaleItemId, StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (IsObjectiveDone(quest, LeakPickupIronScaleObjectiveId))
            {
                return;
            }

            int current = GetCounter(quest, LeakPickupIronScaleObjectiveId);
            current = Mathf.Min(leakPickupRequired, current + amount);
            quest.Counters[LeakPickupIronScaleObjectiveId] = current;
            if (current >= leakPickupRequired)
            {
                MarkObjectiveDone(quest, LeakPickupIronScaleObjectiveId);
            }

            RefreshQuestLog();
        }

        private void OnConstructionMaterialDeposited(
            KomayamaProvisionalFacility provisional,
            ItemDefinition item)
        {
            if (!rainLeakFeedMonitoring ||
                provisional == null ||
                item == null ||
                provisional != tutorialProvisional)
            {
                return;
            }

            if (!string.Equals(item.DefinitionId, IronScaleItemId, StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (IsObjectiveDone(quest, LeakDepositIronScaleObjectiveId))
            {
                return;
            }

            int current = GetCounter(quest, LeakDepositIronScaleObjectiveId);
            current = Mathf.Min(leakDepositRequired, current + 1);
            quest.Counters[LeakDepositIronScaleObjectiveId] = current;
            if (current >= leakDepositRequired)
            {
                MarkObjectiveDone(quest, LeakDepositIronScaleObjectiveId);
            }

            RefreshQuestLog();
        }

        private void OnFacilityConstructionCompleted(KomayamaProcessingFacility facility)
        {
            if (facility == null || facility.Definition == null)
            {
                return;
            }

            if (!string.Equals(
                    facility.Definition.DefinitionId,
                    ScaleRollingWorkbenchId,
                    StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (quest.Status != QuestSaveStatus.Active ||
                IsObjectiveDone(quest, LeakSelectPlateRecipeObjectiveId))
            {
                return;
            }

            if (!rainLeakFeedMonitoring && tutorialProvisional == null)
            {
                return;
            }

            facility.ApplyTutorialFuelCharges(TutorialWorkbenchFuelCharges);
            tutorialProvisional = null;
            rainLeakFeedMonitoring = false;

            if (!IsObjectiveDone(quest, LeakPickupIronScaleObjectiveId))
            {
                MarkObjectiveDone(quest, LeakPickupIronScaleObjectiveId);
                quest.Counters[LeakPickupIronScaleObjectiveId] = leakPickupRequired;
            }

            if (!IsObjectiveDone(quest, LeakDepositIronScaleObjectiveId))
            {
                MarkObjectiveDone(quest, LeakDepositIronScaleObjectiveId);
                quest.Counters[LeakDepositIronScaleObjectiveId] = leakDepositRequired;
            }

            RefreshQuestLog();
            StartCoroutine(EnterRecipePickPhaseRoutine());
        }

        private IEnumerator EnterRecipePickPhaseRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            yield return PlayDialogue(leakPickRecipeStageKey);
            BeginRainLeakRecipeMonitoring();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void OnRecipeSelected(
            KomayamaProcessingFacility facility,
            RecipeDefinition recipe)
        {
            if (!rainLeakRecipeMonitoring || facility == null || recipe == null)
            {
                return;
            }

            if (!string.Equals(
                    recipe.DefinitionId,
                    IronScaleRollingRecipeId,
                    StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (IsObjectiveDone(quest, LeakSelectPlateRecipeObjectiveId))
            {
                return;
            }

            MarkObjectiveDone(quest, LeakSelectPlateRecipeObjectiveId);
            quest.Counters[LeakProducePlateObjectiveId] = 0;
            quest.Counters[LeakDeliverPlateObjectiveId] = 0;
            rainLeakRecipeMonitoring = false;
            RefreshQuestLog();
            StartCoroutine(AfterRecipeSelectedRoutine());
        }

        private IEnumerator AfterRecipeSelectedRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            yield return PlayDialogue(leakAfterRecipeStageKey);
            BeginRainLeakProduceMonitoring();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void OnFacilityItemProduced(
            KomayamaProcessingFacility facility,
            RecipeDefinition recipe)
        {
            if (!rainLeakProduceMonitoring || facility == null || recipe == null)
            {
                return;
            }

            if (!string.Equals(
                    recipe.DefinitionId,
                    IronScaleRollingRecipeId,
                    StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (quest.Status != QuestSaveStatus.Active ||
                IsObjectiveDone(quest, LeakProducePlateObjectiveId))
            {
                return;
            }

            int producedAmount = 0;
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                RecipeOutput output = recipe.Outputs[i];
                if (output.Item != null &&
                    string.Equals(
                        output.Item.DefinitionId,
                        IronScalePlateItemId,
                        StringComparison.Ordinal))
                {
                    producedAmount += Mathf.Max(0, output.Amount);
                }
            }

            if (producedAmount <= 0)
            {
                producedAmount = 1;
            }

            int current = GetCounter(quest, LeakProducePlateObjectiveId);
            current = Mathf.Min(leakProducePlateRequired, current + producedAmount);
            quest.Counters[LeakProducePlateObjectiveId] = current;
            RefreshQuestLog();

            if (current >= leakProducePlateRequired)
            {
                MarkObjectiveDone(quest, LeakProducePlateObjectiveId);
                rainLeakProduceMonitoring = false;
                RefreshQuestLog();
                StartCoroutine(EnterDeliverPhaseRoutine());
            }
        }

        private IEnumerator EnterDeliverPhaseRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            bool pausePushed = false;
            if (gameClock != null)
            {
                gameClock.PushPause();
                pausePushed = true;
            }

            if (cameraController != null && deliveryNpc != null)
            {
                cameraController.BeginDialogueFramingSnapshot();
                cameraController.FrameForDialogue(
                    deliveryNpc.transform.position,
                    orthographicSize: 0f,
                    leakCameraMoveSeconds);
                float elapsed = 0f;
                while (elapsed < leakCameraMoveSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return PlayDialogue(leakAskDeliverStageKey, managePause: false);

            if (cameraController != null)
            {
                cameraController.EndDialogueFramingKeepPosition();
            }

            if (pausePushed && gameClock != null)
            {
                gameClock.PopPause();
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            quest.Counters[LeakDeliverPlateObjectiveId] = 0;
            BeginRainLeakDeliverMonitoring();
            ApplyDeliveryNpcState();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void OnDepositBinItemDeposited(
            KomayamaDepositBin bin,
            ItemDefinition item)
        {
            if (!rainLeakDeliverMonitoring ||
                bin == null ||
                item == null ||
                deliveryNpcBin == null ||
                bin != deliveryNpcBin)
            {
                return;
            }

            if (!string.Equals(item.DefinitionId, IronScalePlateItemId, StringComparison.Ordinal))
            {
                return;
            }

            QuestRuntime quest = EnsureRainLeakQuest();
            if (quest.Status != QuestSaveStatus.Active ||
                IsObjectiveDone(quest, LeakDeliverPlateObjectiveId))
            {
                return;
            }

            int current = GetCounter(quest, LeakDeliverPlateObjectiveId);
            current = Mathf.Min(leakDeliverPlateRequired, current + 1);
            quest.Counters[LeakDeliverPlateObjectiveId] = current;
            RefreshQuestLog();

            if (current >= leakDeliverPlateRequired)
            {
                MarkObjectiveDone(quest, LeakDeliverPlateObjectiveId);
                rainLeakDeliverMonitoring = false;
                RefreshQuestLog();
                StartCoroutine(CompleteRainLeakRoutine());
            }
        }

        private IEnumerator CompleteRainLeakRoutine()
        {
            if (rainLeakPhaseRunning)
            {
                yield break;
            }

            rainLeakPhaseRunning = true;
            yield return PlayDialogue(leakDeliverDoneStageKey);

            if (shipRepairCinematic != null)
            {
                yield return shipRepairCinematic.PlayRoutine();
            }
            else if (shipVisual != null)
            {
                shipVisual.SetStage(KomayamaShipVisual.StageAfterRainLeak, force: true);
            }

            QuestRuntime leak = EnsureRainLeakQuest();
            leak.Status = QuestSaveStatus.Completed;
            ClearDeliveryNpcState();
            ApplyFuelQuestAcceptIcon();
            RefreshQuestLog();
            rainLeakPhaseRunning = false;
        }

        private void ApplyDeliveryNpcState()
        {
            if (deliveryNpc == null)
            {
                return;
            }

            deliveryNpc.SetMarker(NpcMarkerKind.Delivery);
            deliveryNpc.SetDialogueStageKey(leakDeliverHintStageKey);
        }

        private void ClearDeliveryNpcState()
        {
            if (deliveryNpc == null)
            {
                return;
            }

            deliveryNpc.SetDialogueStageKey(npcDefaultStageKey);
        }

        private void ApplyFuelQuestAcceptIcon()
        {
            QuestRuntime fuel = EnsureFuelRefillQuest();
            if (fuel.Status == QuestSaveStatus.Inactive)
            {
                // 受注可能状態（進行自体は未実装）。アイコンのみ出す。
                fuel.Status = QuestSaveStatus.Inactive;
            }

            if (deliveryNpc != null)
            {
                deliveryNpc.SetMarker(NpcMarkerKind.MainQuest);
                deliveryNpc.SetDialogueStageKey(npcDefaultStageKey);
            }
        }

        private void UnlockBuildAndSettingsMenus()
        {
            menusUnlocked = true;
            ApplyMenuVisibility();
        }

        private void ApplyMenuVisibility()
        {
            bool showUnlockedMenus = menusUnlocked && !presentationHudSuppressed;

            if (menuObjectRoot != null)
            {
                menuObjectRoot.SetActive(true);
                for (int i = 0; i < menuObjectRoot.transform.childCount; i++)
                {
                    Transform child = menuObjectRoot.transform.GetChild(i);
                    bool show = showUnlockedMenus &&
                        (child.gameObject == menuBuildButton ||
                         child.gameObject == menuSettingsButton);
                    child.gameObject.SetActive(show);
                }

                return;
            }

            if (menuBuildButton != null)
            {
                menuBuildButton.SetActive(showUnlockedMenus);
            }

            if (menuSettingsButton != null)
            {
                menuSettingsButton.SetActive(showUnlockedMenus);
            }
        }

        private static bool IsBlockingPresentation()
        {
            if (KomayamaCraftDialogueOverlay.Instance != null &&
                KomayamaCraftDialogueOverlay.Instance.IsDialogueActive)
            {
                return true;
            }

            if (KomayamaCraftOpeningController.Instance != null &&
                KomayamaCraftOpeningController.Instance.IsOpeningActive)
            {
                return true;
            }

            return false;
        }

        private IEnumerator PlayDialogue(string stageKey, bool managePause = true)
        {
            if (dialogueOverlay == null || string.IsNullOrEmpty(stageKey))
            {
                yield break;
            }

            bool done = false;
            dialogueOverlay.Play(
                stageKey,
                managePause,
                restoreCamera: false,
                dimAlpha: 0f,
                () => done = true);
            while (!done)
            {
                yield return null;
            }
        }

        private void StopMonitoringAll()
        {
            situationMonitoring = false;
            situationCompleting = false;
            rainLeakMonitoring = false;
            rainLeakPlaceMonitoring = false;
            rainLeakFeedMonitoring = false;
            rainLeakRecipeMonitoring = false;
            rainLeakProduceMonitoring = false;
            rainLeakDeliverMonitoring = false;
            rainLeakIntroRunning = false;
            rainLeakPhaseRunning = false;
            tutorialProvisional = null;
        }

        public void CaptureSave(KomayamaCraftSaveData data)
        {
            if (data == null)
            {
                return;
            }

            data.EnsureCollections();
            data.craftTutorialMenusUnlocked = menusUnlocked;
            data.questProgress.Clear();
            foreach (KeyValuePair<string, QuestRuntime> pair in quests)
            {
                QuestRuntime q = pair.Value;
                if (q == null)
                {
                    continue;
                }

                var dto = new QuestProgressSaveDto
                {
                    questId = q.Id,
                    status = q.Status
                };
                dto.EnsureCollections();
                dto.completedObjectiveIds.AddRange(q.CompletedObjectiveIds);
                foreach (KeyValuePair<string, int> counter in q.Counters)
                {
                    dto.counters.Add(new QuestCounterSaveDto
                    {
                        counterId = counter.Key,
                        value = counter.Value
                    });
                }

                data.questProgress.Add(dto);
            }
        }

        public void ApplySave(KomayamaCraftSaveData data)
        {
            quests.Clear();
            EnsureSituationQuest();
            EnsureRainLeakQuest();
            EnsureFuelRefillQuest();
            menusUnlocked = data != null && data.craftTutorialMenusUnlocked;
            if (data?.questProgress == null)
            {
                // セーブにフラグが無くても、設置フェーズ以降なら解禁扱いにする
                QuestRuntime leakEarly = EnsureRainLeakQuest();
                if (IsObjectiveDone(leakEarly, LeakIronScaleObjectiveId))
                {
                    menusUnlocked = true;
                }

                ApplyMenuVisibility();
                SyncShipVisualFromQuests();
                RefreshQuestLog();
                return;
            }

            for (int i = 0; i < data.questProgress.Count; i++)
            {
                QuestProgressSaveDto dto = data.questProgress[i];
                if (dto == null || string.IsNullOrEmpty(dto.questId))
                {
                    continue;
                }

                QuestRuntime quest = EnsureQuestSlot(dto.questId);
                quest.Status = dto.status;
                quest.CompletedObjectiveIds.Clear();
                quest.Counters.Clear();
                dto.EnsureCollections();
                for (int j = 0; j < dto.completedObjectiveIds.Count; j++)
                {
                    string oid = dto.completedObjectiveIds[j];
                    if (!string.IsNullOrEmpty(oid))
                    {
                        quest.CompletedObjectiveIds.Add(oid);
                    }
                }

                for (int j = 0; j < dto.counters.Count; j++)
                {
                    QuestCounterSaveDto counter = dto.counters[j];
                    if (counter == null || string.IsNullOrEmpty(counter.counterId))
                    {
                        continue;
                    }

                    quest.Counters[counter.counterId] = Mathf.Max(0, counter.value);
                }
            }

            QuestRuntime leak = EnsureRainLeakQuest();
            if (!menusUnlocked && IsObjectiveDone(leak, LeakIronScaleObjectiveId))
            {
                menusUnlocked = true;
            }

            if (leak.Status == QuestSaveStatus.Completed)
            {
                ApplyFuelQuestAcceptIcon();
            }
            else if (IsObjectiveDone(leak, LeakProducePlateObjectiveId) &&
                     !IsObjectiveDone(leak, LeakDeliverPlateObjectiveId))
            {
                ApplyDeliveryNpcState();
            }

            SyncShipVisualFromQuests();
            ApplyMenuVisibility();
            RefreshQuestLog();
        }

        private void SyncShipVisualFromQuests()
        {
            if (shipVisual == null)
            {
                return;
            }

            QuestRuntime leak = EnsureRainLeakQuest();
            int stage = leak.Status == QuestSaveStatus.Completed
                ? KomayamaShipVisual.StageAfterRainLeak
                : KomayamaShipVisual.StageInitial;
            shipVisual.SetStage(stage, force: true);
        }

        private void RefreshQuestLog()
        {
            if (questLogView == null)
            {
                return;
            }

            var displays = new List<KomayamaQuestLogView.QuestDisplay>();
            QuestRuntime situation = EnsureSituationQuest();
            if (situation.Status == QuestSaveStatus.Active)
            {
                displays.Add(new KomayamaQuestLogView.QuestDisplay
                {
                    id = situation.Id,
                    title = "現状把握",
                    body = BuildSituationBody(situation)
                });
            }

            QuestRuntime leak = EnsureRainLeakQuest();
            if (leak.Status == QuestSaveStatus.Active ||
                leak.Status == QuestSaveStatus.Completed)
            {
                bool completed = leak.Status == QuestSaveStatus.Completed;
                const string leakTitle = "宇宙船の雨漏りを直そう";
                displays.Add(new KomayamaQuestLogView.QuestDisplay
                {
                    id = leak.Id,
                    title = completed
                        ? "<color=#9A9A9A>" + leakTitle + "</color>"
                        : leakTitle,
                    body = BuildRainLeakBody(leak)
                });
            }

            questLogView.SetQuests(displays);
        }

        private string BuildRainLeakBody(QuestRuntime quest)
        {
            if (!IsObjectiveDone(quest, LeakIronScaleObjectiveId))
            {
                int current = GetCounter(quest, LeakIronScaleObjectiveId);
                string progressLine = FormatObjectiveLine(
                    quest,
                    LeakIronScaleObjectiveId,
                    "<sprite name=\"mouse_lmb\"> クリック 又は 長押し で採集　" +
                    current + "/" + leakIronScaleRequired);
                // 見出しはチェックなし。マウス行のみチェック。空行で上マージン確保。
                return "鉄鱗獣から素材を集める\n\n" + progressLine;
            }

            if (!IsObjectiveDone(quest, LeakPlaceBenchObjectiveId))
            {
                int placed = GetCounter(quest, LeakPlaceBenchObjectiveId);
                string placeLabel = "鉄鱗圧延台を作ろう　" + placed + "/1";
                return FormatObjectiveLine(quest, LeakPlaceBenchObjectiveId, placeLabel);
            }

            if (!IsObjectiveDone(quest, LeakPickupIronScaleObjectiveId) ||
                !IsObjectiveDone(quest, LeakDepositIronScaleObjectiveId))
            {
                int picked = GetCounter(quest, LeakPickupIronScaleObjectiveId);
                int deposited = GetCounter(quest, LeakDepositIronScaleObjectiveId);
                string pickupLabel =
                    "左クリックで鉄鱗を拾う　" + picked + "/" + leakPickupRequired;
                string depositLabel =
                    "右クリックで鉄鱗圧延加工台に投入　" +
                    deposited + "/" + leakDepositRequired;
                return FormatObjectiveLine(quest, LeakPickupIronScaleObjectiveId, pickupLabel) +
                       "\n" +
                       FormatObjectiveLine(quest, LeakDepositIronScaleObjectiveId, depositLabel);
            }

            if (!IsObjectiveDone(quest, LeakSelectPlateRecipeObjectiveId))
            {
                return FormatObjectiveLine(
                    quest,
                    LeakSelectPlateRecipeObjectiveId,
                    "作業台メニューから鱗鉄板を選ぶ");
            }

            if (!IsObjectiveDone(quest, LeakProducePlateObjectiveId))
            {
                int produced = GetCounter(quest, LeakProducePlateObjectiveId);
                string produceLabel =
                    "鱗鉄板を3枚つくろう　" + produced + "/" + leakProducePlateRequired;
                return FormatObjectiveLine(quest, LeakProducePlateObjectiveId, produceLabel);
            }

            int delivered = GetCounter(quest, LeakDeliverPlateObjectiveId);
            string deliverLabel =
                "鉄板を3枚納品する　" + delivered + "/" + leakDeliverPlateRequired;
            return FormatObjectiveLine(quest, LeakDeliverPlateObjectiveId, deliverLabel);
        }

        private static string BuildSituationBody(QuestRuntime quest)
        {
            return FormatObjectiveLine(quest, "wasd_w", "<sprite name=\"key_w\"> W 上移動") + "\n" +
                   FormatObjectiveLine(quest, "wasd_a", "<sprite name=\"key_a\"> A 左移動") + "\n" +
                   FormatObjectiveLine(quest, "wasd_s", "<sprite name=\"key_s\"> S 下移動") + "\n" +
                   FormatObjectiveLine(quest, "wasd_d", "<sprite name=\"key_d\"> D 右移動");
        }

        private static string FormatObjectiveLine(QuestRuntime quest, string id, string label)
        {
            bool done = IsObjectiveDone(quest, id);
            if (done)
            {
                return "<color=#9A9A9A>☑ " + label + "</color>";
            }

            return "□ " + label;
        }

        private static int GetCounter(QuestRuntime quest, string counterId)
        {
            if (quest == null ||
                !quest.Counters.TryGetValue(counterId, out int value))
            {
                return 0;
            }

            return value;
        }

        private static bool IsObjectiveDone(QuestRuntime quest, string objectiveId)
        {
            return quest != null && quest.CompletedObjectiveIds.Contains(objectiveId);
        }

        private static void MarkObjectiveDone(QuestRuntime quest, string objectiveId)
        {
            if (quest == null || string.IsNullOrEmpty(objectiveId))
            {
                return;
            }

            quest.CompletedObjectiveIds.Add(objectiveId);
        }

        private static bool AreAllSituationObjectivesDone(QuestRuntime quest)
        {
            return IsObjectiveDone(quest, "wasd_w") &&
                   IsObjectiveDone(quest, "wasd_a") &&
                   IsObjectiveDone(quest, "wasd_s") &&
                   IsObjectiveDone(quest, "wasd_d");
        }

        private static void ClearSituationObjectiveCompletions(QuestRuntime quest)
        {
            quest.CompletedObjectiveIds.Remove("wasd_w");
            quest.CompletedObjectiveIds.Remove("wasd_a");
            quest.CompletedObjectiveIds.Remove("wasd_s");
            quest.CompletedObjectiveIds.Remove("wasd_d");
        }

        private QuestRuntime EnsureSituationQuest()
        {
            return EnsureQuestSlot(SituationSurveyQuestId);
        }

        private QuestRuntime EnsureRainLeakQuest()
        {
            return EnsureQuestSlot(RainLeakQuestId);
        }

        private QuestRuntime EnsureFuelRefillQuest()
        {
            return EnsureQuestSlot(FuelRefillQuestId);
        }

        private QuestRuntime EnsureQuestSlot(string questId)
        {
            if (quests.TryGetValue(questId, out QuestRuntime existing) && existing != null)
            {
                return existing;
            }

            var created = new QuestRuntime
            {
                Id = questId,
                Status = QuestSaveStatus.Inactive
            };
            quests[questId] = created;
            return created;
        }

        private sealed class QuestRuntime
        {
            public string Id;
            public QuestSaveStatus Status;
            public readonly HashSet<string> CompletedObjectiveIds = new(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Counters = new(StringComparer.Ordinal);
        }
    }
}
