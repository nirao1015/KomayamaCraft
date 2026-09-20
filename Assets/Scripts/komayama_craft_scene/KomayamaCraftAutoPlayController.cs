using System.Collections;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// チュートリアル自動プレイ（OP → 雨漏り初期納品完了まで）。
    /// 手順再生ではなく、クエスト／ワールド状態を読んで次の操作を決める。
    /// 内部値の書き換えはせず、既存入力経路のみ使う。起動は DebugManager。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftAutoPlayController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KomayamaQuestController questController;
        [SerializeField] private KomayamaCraftInputController inputController;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private KomayamaFacilityMenuView facilityMenu;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaNpc deliveryNpc;
        [SerializeField] private Transform buildPlaceMarker;

        [Header("設定")]
        [SerializeField, Min(0.05f)] private float dialogueAdvanceInterval = 0.35f;
        [SerializeField, Min(0.05f)] private float thinkInterval = 0.15f;
        [SerializeField, Min(0.1f)] private float deliverClickCooldownSeconds = 0.45f;
        [SerializeField, Min(1f)] private float stallTimeoutSeconds = 120f;
        [SerializeField] private string workbenchDefinitionId =
            KomayamaQuestController.ScaleRollingWorkbenchId;
        [SerializeField] private string plateRecipeDefinitionId =
            KomayamaQuestController.IronScaleRollingRecipeId;
        [SerializeField] private string ironScaleItemId =
            KomayamaQuestController.IronScaleItemId;
        [SerializeField] private string ironScalePlateItemId =
            KomayamaQuestController.IronScalePlateItemId;

        private Coroutine runRoutine;
        private Coroutine dialoguePulseRoutine;
        private bool running;
        private string statusMessage = "Idle";
        private string lastProgressKey = "";
        private float lastProgressAt;
        private float nextDeliverClickAt;

        public bool IsRunning => running;
        public string StatusMessage => statusMessage;

        private void Start()
        {
            if (debugManager != null &&
                !debugManager.ProductionReleaseBuild &&
                debugManager.StartTutorialAutoPlay)
            {
                StartTutorialAutoPlay();
            }
        }

        private void OnDisable()
        {
            StopTutorialAutoPlay("disabled");
        }

        public void StartTutorialAutoPlay()
        {
            if (debugManager != null && debugManager.ProductionReleaseBuild)
            {
                return;
            }

            if (running)
            {
                return;
            }

            ResolveReferences();
            running = true;
            lastProgressKey = "";
            lastProgressAt = Time.unscaledTime;
            KomayamaCraftAutoPlayInput.BeginSession();
            statusMessage = "Starting";
            Debug.Log("[CraftAutoPlay] Start reactive (OP → initial delivery)");
            runRoutine = StartCoroutine(ThinkLoop());
            dialoguePulseRoutine = StartCoroutine(DialogueAdvancePulse());
        }

        public void StopTutorialAutoPlay(string reason)
        {
            if (!running && runRoutine == null)
            {
                return;
            }

            running = false;
            statusMessage = "Stopped: " + reason;
            ClearWasdHolds();
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            if (dialoguePulseRoutine != null)
            {
                StopCoroutine(dialoguePulseRoutine);
                dialoguePulseRoutine = null;
            }

            KomayamaCraftAutoPlayInput.EndSession();
            Debug.Log("[CraftAutoPlay] " + statusMessage);
        }

        private void ResolveReferences()
        {
            if (questController == null)
            {
                questController = FindFirstObjectByType<KomayamaQuestController>();
            }

            if (inputController == null)
            {
                inputController = FindFirstObjectByType<KomayamaCraftInputController>();
            }

            if (buildController == null)
            {
                buildController = FindFirstObjectByType<KomayamaBuildController>();
            }

            if (facilityMenu == null)
            {
                facilityMenu = FindFirstObjectByType<KomayamaFacilityMenuView>();
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<KomayamaCraftCameraController>();
            }

            if (hand == null)
            {
                hand = FindFirstObjectByType<KomayamaHandInventory>();
            }

            if (deliveryNpc == null)
            {
                deliveryNpc = FindFirstObjectByType<KomayamaNpc>();
            }
        }

        private IEnumerator DialogueAdvancePulse()
        {
            while (running)
            {
                if (IsPresentationBlocking())
                {
                    KomayamaCraftAutoPlayInput.RequestDialogueAdvance();
                }

                yield return new WaitForSecondsRealtime(dialogueAdvanceInterval);
            }
        }

        private IEnumerator ThinkLoop()
        {
            while (running)
            {
                if (questController == null || inputController == null)
                {
                    Fail("missing references");
                    yield break;
                }

                if (IsRainLeakCompleted() && !KomayamaShipRepairCinematic.IsPlaying)
                {
                    if (!TryPostAssertSuccess(out string assertFail))
                    {
                        Fail("postcondition " + assertFail);
                        yield break;
                    }

                    Debug.Log("[CraftAutoPlay] PostAssert OK");
                    StopTutorialAutoPlay("delivery complete");
                    yield break;
                }

                if (IsRainLeakCompleted() && KomayamaShipRepairCinematic.IsPlaying)
                {
                    ClearWasdHolds();
                    statusMessage = "Wait ship repair cinematic";
                    NoteProgress(statusMessage);
                }
                else if (IsPresentationBlocking())
                {
                    ClearWasdHolds();
                    statusMessage = KomayamaShipRepairCinematic.IsPlaying
                        ? "Wait ship repair cinematic"
                        : "Advance dialogue";
                    NoteProgress(statusMessage);
                }
                else
                {
                    DecideAndAct();
                    if (IsStalled())
                    {
                        Fail("stalled: " + statusMessage);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(thinkInterval);
            }
        }

        private void DecideAndAct()
        {
            QuestSaveStatus sit;
            QuestSaveStatus leak;
            questController.TryGetQuestStatus(
                KomayamaQuestController.SituationSurveyQuestId,
                out sit);
            questController.TryGetQuestStatus(
                KomayamaQuestController.RainLeakQuestId,
                out leak);

            if (sit == QuestSaveStatus.Active)
            {
                ActSituationWasd();
                return;
            }

            ClearWasdHolds();

            if (leak != QuestSaveStatus.Active)
            {
                statusMessage = "Wait quest start";
                NoteProgress(BuildProgressKey());
                return;
            }

            if (!ObjDone(KomayamaQuestController.LeakIronScaleObjectiveId) ||
                questController.IsAwaitingIronScaleGather)
            {
                ActGatherIronScales();
                return;
            }

            if (!ObjDone(KomayamaQuestController.LeakPlaceBenchObjectiveId))
            {
                ActPlaceWorkbench();
                return;
            }

            if (FindWorkbench() == null)
            {
                ActConstructWorkbench();
                return;
            }

            if (!ObjDone(KomayamaQuestController.LeakSelectPlateRecipeObjectiveId))
            {
                ActSelectRecipe();
                return;
            }

            if (!ObjDone(KomayamaQuestController.LeakProducePlateObjectiveId) ||
                questController.IsAwaitingPlateProduction)
            {
                ActProducePlates();
                return;
            }

            if (ObjDone(KomayamaQuestController.LeakDeliverPlateObjectiveId))
            {
                // 納品目標完了後は会話／修理待ち。NPC へクリック連打しない
                ClearWasdHolds();
                statusMessage = "Wait rain leak complete";
                NoteProgress(BuildProgressKey());
                return;
            }

            ActDeliverPlates();
        }

        private void ActSituationWasd()
        {
            string[] ids = { "wasd_w", "wasd_a", "wasd_s", "wasd_d" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (ObjDoneSituation(ids[i]))
                {
                    continue;
                }

                statusMessage = "WASD:" + ids[i];
                KomayamaCraftAutoPlayInput.HoldW = ids[i] == "wasd_w";
                KomayamaCraftAutoPlayInput.HoldA = ids[i] == "wasd_a";
                KomayamaCraftAutoPlayInput.HoldS = ids[i] == "wasd_s";
                KomayamaCraftAutoPlayInput.HoldD = ids[i] == "wasd_d";
                NoteProgress(statusMessage + ":" + BuildProgressKey());
                return;
            }

            ClearWasdHolds();
            statusMessage = "Wait situation complete";
            NoteProgress(BuildProgressKey());
        }

        private void ActGatherIronScales()
        {
            statusMessage = "Gather iron scales";
            if (TryPickupNearby(ironScaleItemId))
            {
                NoteProgress(BuildProgressKey());
                return;
            }

            KomayamaResourceNode beast = FindIronScaleBeast();
            if (beast != null)
            {
                Focus(beast.transform.position);
                inputController.AutoPlayLeftClickAt(beast.transform.position);
            }

            NoteProgress(BuildProgressKey());
        }

        private void ActPlaceWorkbench()
        {
            if (!questController.IsAwaitingWorkbenchPlacement)
            {
                statusMessage = "Wait place phase";
                NoteProgress(BuildProgressKey());
                return;
            }

            if (buildController == null)
            {
                Fail("build missing");
                return;
            }

            statusMessage = "Place workbench";
            // 監視前置きの仮組が残っているとイベントが飛ばないので除去して置き直す
            ForcePlaceWorkbenchProvisional();
            NoteProgress(BuildProgressKey());
        }

        /// <summary>
        /// 配置監視フラグに依らず仮組を置き直す（消失リカバリ）。
        /// </summary>
        private void ForcePlaceWorkbenchProvisional()
        {
            if (buildController == null)
            {
                Fail("build missing");
                return;
            }

            DestroyMatchingProvisionals();
            if (!buildController.TrySelectFacilityByDefinitionId(workbenchDefinitionId))
            {
                Fail("workbench not in build list");
                return;
            }

            Vector2 place = ResolveBuildPlace();
            Focus(place);
            Vector2[] candidates =
            {
                place,
                place + new Vector2(2f, 0f),
                place + new Vector2(-2f, 0f),
                place + new Vector2(0f, 2f),
                place + new Vector2(0f, -2f),
                place + new Vector2(4f, -2f),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (buildController.TryBuildAt(candidates[i], out _))
                {
                    return;
                }
            }
        }

        private void ActConstructWorkbench()
        {
            KomayamaProvisionalFacility provisional = FindWorkbenchProvisional();
            if (provisional == null)
            {
                // 配置済み扱いなのに仮組も完成台もない → 再配置へ
                if (questController.IsAwaitingWorkbenchPlacement ||
                    !ObjDone(KomayamaQuestController.LeakPlaceBenchObjectiveId))
                {
                    ActPlaceWorkbench();
                    return;
                }

                // 目標だけ完了・ワールド上に台が無い（仮組消失など）→ 待機せず置き直す
                statusMessage = "Recover missing workbench";
                ForcePlaceWorkbenchProvisional();
                NoteProgress(BuildProgressKey());
                return;
            }

            statusMessage = "Construct workbench";
            if (!HandHas(ironScaleItemId, 1))
            {
                if (TryPickupNearby(ironScaleItemId))
                {
                    NoteProgress(BuildProgressKey());
                    return;
                }

                if (HandHas(ironScalePlateItemId, 1))
                {
                    // 板を持っていると鉄鱗を拾えない。納品前なので生産フェーズではないが、
                    // 建設中なら板は通常ない。採集して手を空ける手段がないため採集優先。
                }

                KomayamaResourceNode beast = FindIronScaleBeast();
                if (beast != null)
                {
                    Focus(beast.transform.position);
                    inputController.AutoPlayLeftClickAt(beast.transform.position);
                }

                NoteProgress(BuildProgressKey());
                return;
            }

            Focus(provisional.transform.position);
            inputController.AutoPlayRightClickAt(provisional.transform.position);
            NoteProgress(BuildProgressKey());
        }

        private void ActSelectRecipe()
        {
            if (!questController.IsAwaitingPlateRecipeSelection)
            {
                statusMessage = "Wait recipe phase";
                NoteProgress(BuildProgressKey());
                return;
            }

            KomayamaProcessingFacility bench = FindWorkbench();
            if (bench == null)
            {
                statusMessage = "Wait workbench";
                NoteProgress(BuildProgressKey());
                return;
            }

            statusMessage = "Select recipe";
            if (facilityMenu != null && facilityMenu.IsOpen)
            {
                if (facilityMenu.TrySelectRecipeByDefinitionId(plateRecipeDefinitionId))
                {
                    facilityMenu.Close();
                }

                NoteProgress(BuildProgressKey());
                return;
            }

            Focus(bench.transform.position);
            inputController.AutoPlayLeftClickAt(bench.transform.position);
            NoteProgress(BuildProgressKey());
        }

        private void ActProducePlates()
        {
            statusMessage = "Produce plates";
            KomayamaProcessingFacility bench = FindWorkbench();
            if (bench == null)
            {
                NoteProgress(BuildProgressKey());
                return;
            }

            // 出力が床にあれば拾う（納品準備にもなる）
            if (TryPickupNearby(ironScalePlateItemId))
            {
                NoteProgress(BuildProgressKey());
                return;
            }

            if (!questController.IsAwaitingPlateProduction &&
                ObjDone(KomayamaQuestController.LeakProducePlateObjectiveId))
            {
                NoteProgress(BuildProgressKey());
                return;
            }

            // 手に板があるときは投入できないので、納品フェーズまで保持して待機
            if (HandHas(ironScalePlateItemId, 1))
            {
                NoteProgress(BuildProgressKey());
                return;
            }

            if (!HandHas(ironScaleItemId, 1))
            {
                if (TryPickupNearby(ironScaleItemId))
                {
                    NoteProgress(BuildProgressKey());
                    return;
                }

                KomayamaResourceNode beast = FindIronScaleBeast();
                if (beast != null)
                {
                    Focus(beast.transform.position);
                    inputController.AutoPlayLeftClickAt(beast.transform.position);
                }

                NoteProgress(BuildProgressKey());
                return;
            }

            Focus(bench.transform.position);
            inputController.AutoPlayRightClickAt(bench.transform.position);
            NoteProgress(BuildProgressKey());
        }

        private void ActDeliverPlates()
        {
            statusMessage = "Deliver plates";
            if (deliveryNpc == null)
            {
                Fail("delivery npc missing");
                return;
            }

            if (!questController.IsAwaitingPlateDelivery)
            {
                // 生産完了〜納品会話待ち。板は拾っておく。
                TryPickupNearby(ironScalePlateItemId);
                NoteProgress(BuildProgressKey());
                return;
            }

            if (!HandHas(ironScalePlateItemId, 1))
            {
                if (TryPickupNearby(ironScalePlateItemId))
                {
                    NoteProgress(BuildProgressKey());
                    return;
                }

                // まだ床に無く、手が空なら追加生産を試みる
                KomayamaProcessingFacility bench = FindWorkbench();
                if (bench != null && HandHas(ironScaleItemId, 1))
                {
                    Focus(bench.transform.position);
                    inputController.AutoPlayRightClickAt(bench.transform.position);
                }
                else if (bench != null)
                {
                    if (!TryPickupNearby(ironScaleItemId))
                    {
                        KomayamaResourceNode beast = FindIronScaleBeast();
                        if (beast != null)
                        {
                            Focus(beast.transform.position);
                            inputController.AutoPlayLeftClickAt(beast.transform.position);
                        }
                    }
                }

                NoteProgress(BuildProgressKey());
                return;
            }

            Focus(deliveryNpc.transform.position);
            if (Time.unscaledTime < nextDeliverClickAt)
            {
                statusMessage = "Deliver plates (cooldown)";
                NoteProgress(BuildProgressKey());
                return;
            }

            inputController.AutoPlayRightClickAt(deliveryNpc.transform.position);
            nextDeliverClickAt = Time.unscaledTime + deliverClickCooldownSeconds;
            NoteProgress(BuildProgressKey());
        }

        private bool TryPickupNearby(string itemDefinitionId)
        {
            KomayamaDroppedItem drop = FindNearestDrop(itemDefinitionId);
            if (drop == null || inputController == null)
            {
                return false;
            }

            Focus(drop.transform.position);
            inputController.AutoPlayLeftClickAt(drop.transform.position);
            return true;
        }

        private KomayamaDroppedItem FindNearestDrop(string itemDefinitionId)
        {
            KomayamaDroppedItem[] drops =
                FindObjectsByType<KomayamaDroppedItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            KomayamaDroppedItem best = null;
            float bestDist = float.MaxValue;
            Vector2 origin = cameraController != null
                ? (Vector2)cameraController.transform.position
                : Vector2.zero;
            for (int i = 0; i < drops.Length; i++)
            {
                KomayamaDroppedItem drop = drops[i];
                if (drop == null || drop.Item == null || drop.IsMoving)
                {
                    continue;
                }

                if (!string.Equals(
                        drop.Item.DefinitionId,
                        itemDefinitionId,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                float dist = Vector2.Distance(origin, drop.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = drop;
                }
            }

            return best;
        }

        private bool HandHas(string itemDefinitionId, int minCount)
        {
            if (hand == null || hand.IsEmpty || hand.Item == null)
            {
                return false;
            }

            if (!string.Equals(
                    hand.Item.DefinitionId,
                    itemDefinitionId,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            return hand.Amount >= minCount;
        }

        private int CountGround(string itemDefinitionId)
        {
            int count = 0;
            KomayamaDroppedItem[] drops =
                FindObjectsByType<KomayamaDroppedItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i] != null &&
                    drops[i].Item != null &&
                    string.Equals(
                        drops[i].Item.DefinitionId,
                        itemDefinitionId,
                        System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private KomayamaResourceNode FindIronScaleBeast()
        {
            KomayamaResourceNode[] nodes =
                FindObjectsByType<KomayamaResourceNode>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                KomayamaResourceNode node = nodes[i];
                if (node == null || node.Definition == null)
                {
                    continue;
                }

                if (string.Equals(
                        node.Definition.DefinitionId,
                        "resource_node.iron_scale_beast",
                        System.StringComparison.Ordinal) ||
                    node.Definition.DefinitionId.IndexOf(
                        "iron_scale",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return node;
                }
            }

            return nodes.Length > 0 ? nodes[0] : null;
        }

        private KomayamaProcessingFacility FindWorkbench()
        {
            KomayamaProcessingFacility[] facilities =
                FindObjectsByType<KomayamaProcessingFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                KomayamaProcessingFacility facility = facilities[i];
                if (facility?.Definition != null &&
                    string.Equals(
                        facility.Definition.DefinitionId,
                        workbenchDefinitionId,
                        System.StringComparison.Ordinal))
                {
                    return facility;
                }
            }

            return null;
        }

        private KomayamaProvisionalFacility FindWorkbenchProvisional()
        {
            KomayamaProvisionalFacility[] list =
                FindObjectsByType<KomayamaProvisionalFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                KomayamaProvisionalFacility provisional = list[i];
                if (provisional?.Definition != null &&
                    string.Equals(
                        provisional.Definition.DefinitionId,
                        workbenchDefinitionId,
                        System.StringComparison.Ordinal))
                {
                    return provisional;
                }
            }

            return list.Length > 0 ? list[0] : null;
        }

        private void DestroyMatchingProvisionals()
        {
            KomayamaProvisionalFacility[] list =
                FindObjectsByType<KomayamaProvisionalFacility>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    Destroy(list[i].gameObject);
                }
            }
        }

        private Vector2 ResolveBuildPlace()
        {
            if (buildPlaceMarker != null)
            {
                return buildPlaceMarker.position;
            }

            if (deliveryNpc != null)
            {
                return (Vector2)deliveryNpc.transform.position + new Vector2(3f, -1f);
            }

            return Vector2.zero;
        }

        private void Focus(Vector2 world)
        {
            cameraController?.SnapToWorldCenter(world);
        }

        private void ClearWasdHolds()
        {
            KomayamaCraftAutoPlayInput.HoldW =
                KomayamaCraftAutoPlayInput.HoldA =
                    KomayamaCraftAutoPlayInput.HoldS =
                        KomayamaCraftAutoPlayInput.HoldD = false;
        }

        private static bool IsPresentationBlocking()
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

            if (KomayamaShipRepairCinematic.IsPlaying)
            {
                return true;
            }

            return false;
        }

        private bool IsRainLeakCompleted()
        {
            return questController != null &&
                   questController.TryGetQuestStatus(
                       KomayamaQuestController.RainLeakQuestId,
                       out QuestSaveStatus status) &&
                   status == QuestSaveStatus.Completed;
        }

        /// <summary>
        /// 成功停止前の読み取りアサート（状態捏造なし）。
        /// </summary>
        private bool TryPostAssertSuccess(out string failureReason)
        {
            failureReason = null;

            KomayamaShipVisual shipVisual =
                FindFirstObjectByType<KomayamaShipVisual>();
            if (shipVisual == null)
            {
                failureReason = "missing KomayamaShipVisual";
                return false;
            }

            if (shipVisual.StageIndex != KomayamaShipVisual.StageAfterRainLeak)
            {
                failureReason =
                    "ship stage expected " +
                    KomayamaShipVisual.StageAfterRainLeak +
                    " but was " +
                    shipVisual.StageIndex;
                return false;
            }

            KCMouseFoxFollower fox =
                FindFirstObjectByType<KCMouseFoxFollower>();
            if (fox == null)
            {
                failureReason = "missing KCMouseFoxFollower";
                return false;
            }

            if (fox.DisplayMode == KCFoxDisplayMode.Off)
            {
                failureReason = "fox DisplayMode is Off";
                return false;
            }

            if (fox.IsSuppressedForCinematic)
            {
                failureReason = "fox still suppressed for cinematic";
                return false;
            }

            return true;
        }

        private bool ObjDone(string objectiveId)
        {
            return questController.IsQuestObjectiveDone(
                KomayamaQuestController.RainLeakQuestId,
                objectiveId);
        }

        private bool ObjDoneSituation(string objectiveId)
        {
            return questController.IsQuestObjectiveDone(
                KomayamaQuestController.SituationSurveyQuestId,
                objectiveId);
        }

        private string BuildProgressKey()
        {
            int handAmt = hand != null && hand.Item != null ? hand.Amount : 0;
            string handId = hand != null && hand.Item != null
                ? hand.Item.DefinitionId
                : "-";
            return string.Join(
                "|",
                statusMessage,
                ObjDoneSituation("wasd_w") ? "1" : "0",
                ObjDoneSituation("wasd_a") ? "1" : "0",
                ObjDoneSituation("wasd_s") ? "1" : "0",
                ObjDoneSituation("wasd_d") ? "1" : "0",
                ObjDone(KomayamaQuestController.LeakIronScaleObjectiveId) ? "1" : "0",
                ObjDone(KomayamaQuestController.LeakPlaceBenchObjectiveId) ? "1" : "0",
                ObjDone(KomayamaQuestController.LeakDepositIronScaleObjectiveId) ? "1" : "0",
                ObjDone(KomayamaQuestController.LeakSelectPlateRecipeObjectiveId) ? "1" : "0",
                ObjDone(KomayamaQuestController.LeakProducePlateObjectiveId) ? "1" : "0",
                questController.GetQuestCounter(
                    KomayamaQuestController.RainLeakQuestId,
                    KomayamaQuestController.LeakProducePlateObjectiveId),
                ObjDone(KomayamaQuestController.LeakDeliverPlateObjectiveId) ? "1" : "0",
                questController.GetQuestCounter(
                    KomayamaQuestController.RainLeakQuestId,
                    KomayamaQuestController.LeakDeliverPlateObjectiveId),
                FindWorkbench() != null ? "1" : "0",
                FindWorkbenchProvisional() != null ? "1" : "0",
                handId,
                handAmt,
                CountGround(ironScaleItemId),
                CountGround(ironScalePlateItemId));
        }

        private void NoteProgress(string key)
        {
            if (key == lastProgressKey)
            {
                return;
            }

            lastProgressKey = key;
            lastProgressAt = Time.unscaledTime;
        }

        private bool IsStalled()
        {
            // 会話・OP・修理演出中は進行キーが変わらなくてよい
            if (IsPresentationBlocking())
            {
                return false;
            }

            return Time.unscaledTime - lastProgressAt > stallTimeoutSeconds;
        }

        private void Fail(string reason)
        {
            StopTutorialAutoPlay("FAIL: " + reason);
            Debug.LogError("[CraftAutoPlay] " + reason);
        }
    }
}
