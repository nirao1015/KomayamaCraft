using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftInputController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private KomayamaEscapeController escapeController;
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KCMouseFoxFollower foxFollower;
        [SerializeField] private KomayamaFacilityMenuView facilityMenu;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField, Min(1f)] private float mapDragStartPixels = 10f;
        [SerializeField, Min(0f)] private float holdStartDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float holdRepeatSeconds = 0.12f;
        [SerializeField, Min(0.02f)] private float gatherHoldIntervalSeconds = 0.5f;

        private float nextLeftRepeatAt;
        private float nextRightRepeatAt;
        private float nextGatherAt;
        private KomayamaResourceNode heldResourceNode;
        private KomayamaGhost selectedGhost;
        private bool mapDragPending;
        private bool mapDragging;
        private Vector2 mapDragLastScreen;
        /// <summary>
        /// ??????????????????????????????????
        /// </summary>
        private bool suppressFacilityDepositUntilRightRelease;

        /// <summary>
        /// 右クリック長押し中、納入 SE／失敗 SE を既に1回鳴らしたか。
        /// 納入ゴミ箱・NPC 納品口の両方で使う。
        /// </summary>
        private bool depositSePlayedThisHold;

        private bool RapidHoldRepeat =>
            debugManager != null && debugManager.RapidHoldDrop;

        private float HoldStartDelay =>
            RapidHoldRepeat ? 0.02f : holdStartDelay;

        private float HoldRepeatSeconds =>
            RapidHoldRepeat ? 0.01f : holdRepeatSeconds;

        private float GatherHoldRepeatSeconds =>
            RapidHoldRepeat ? 0.01f : gatherHoldIntervalSeconds;

        private float RightHoldStartDelay =>
            itemSettings != null
                ? itemSettings.DropHoldStartDelay
                : holdStartDelay;

        private float RightHoldRepeatSeconds =>
            itemSettings != null
                ? itemSettings.DropHoldRepeatSeconds
                : holdRepeatSeconds;

        private void RepeatRightHold()
        {
            float interval = RightHoldRepeatSeconds;
            int repeats = 0;
            const int MaxRepeatsPerFrame = 12;
            while (Time.unscaledTime >= nextRightRepeatAt &&
                   repeats < MaxRepeatsPerFrame)
            {
                HandleRight();
                if (interval <= 0f)
                {
                    nextRightRepeatAt = Time.unscaledTime;
                    break;
                }

                nextRightRepeatAt += interval;
                repeats++;
            }
        }

        public float GatherHoldIntervalSeconds => gatherHoldIntervalSeconds;

        public void SetGatherHoldIntervalSeconds(float seconds)
        {
            gatherHoldIntervalSeconds = Mathf.Max(0.02f, seconds);
        }

        private void Update()
        {
            if ((KomayamaCraftDialogueOverlay.Instance != null &&
                 KomayamaCraftDialogueOverlay.Instance.IsDialogueActive) ||
                (KomayamaCraftOpeningController.Instance != null &&
                 KomayamaCraftOpeningController.Instance.IsOpeningActive) ||
                KomayamaShipRepairCinematic.IsPlaying)
            {
                foxFollower?.NotifyRightDropHolding(false);
                foxFollower?.NotifyGatherHolding(false, GatherHoldRepeatSeconds);
                return;
            }

            HandleModeKeys();
            Mouse mouse = Mouse.current;
            if (mouse == null || targetCamera == null)
            {
                return;
            }

            if (foxFollower == null)
            {
                foxFollower = FindFirstObjectByType<KCMouseFoxFollower>();
            }

            if (foxFollower != null && foxFollower.TryConsumePointerInteraction())
            {
                foxFollower.NotifyRightDropHolding(false);
                return;
            }

            if (buildController != null &&
                buildController.Mode != KomayamaInputMode.Field)
            {
                foxFollower?.NotifyRightDropHolding(false);
                HandleBuildOrEdit(mouse);
                return;
            }

            UpdateFocus();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                BeginLeftPress(mouse);
            }
            else if (mouse.leftButton.isPressed)
            {
                if (HandleMapDrag(mouse))
                {
                    foxFollower?.NotifyRightDropHolding(false);
                    foxFollower?.NotifyGatherHolding(false, GatherHoldRepeatSeconds);
                    return;
                }

                HandleDragPickup();

                KomayamaResourceNode hoveredResource =
                    GetResourceNodeUnderPointer();
                if (heldResourceNode != null &&
                    hoveredResource == heldResourceNode)
                {
                    if (Time.unscaledTime >= nextGatherAt)
                    {
                        heldResourceNode.TryGather();
                        nextGatherAt =
                            Time.unscaledTime + GatherHoldRepeatSeconds;
                        // 2????????????????????????
                        foxFollower?.NotifyGatherHolding(
                            true,
                            GatherHoldRepeatSeconds);
                    }
                }
                else
                {
                    foxFollower?.NotifyGatherHolding(false, GatherHoldRepeatSeconds);
                    if (hoveredResource == null &&
                        Time.unscaledTime >= nextLeftRepeatAt)
                    {
                        HandleLeft(false, false);
                        nextLeftRepeatAt =
                            Time.unscaledTime + HoldRepeatSeconds;
                    }
                }
            }
            else
            {
                heldResourceNode = null;
                mapDragPending = false;
                mapDragging = false;
                foxFollower?.NotifyGatherHolding(false, GatherHoldRepeatSeconds);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                suppressFacilityDepositUntilRightRelease = false;
                depositSePlayedThisHold = false;
                HandleRight();
                nextRightRepeatAt = Time.unscaledTime + RightHoldStartDelay;
            }
            else if (mouse.rightButton.isPressed)
            {
                RepeatRightHold();
            }
            else
            {
                suppressFacilityDepositUntilRightRelease = false;
                depositSePlayedThisHold = false;
            }

            bool dropping =
                mouse.rightButton.isPressed &&
                hand != null &&
                !hand.IsEmpty;
            foxFollower?.NotifyRightDropHolding(dropping);
        }

        private void BeginLeftPress(Mouse mouse)
        {
            mapDragPending = false;
            mapDragging = false;
            mapDragLastScreen = mouse.position.ReadValue();
            if (CanStartMapDrag())
            {
                mapDragPending = true;
                return;
            }

            heldResourceNode = GetResourceNodeUnderPointer();
            HandleLeft(true, false);
            nextLeftRepeatAt = Time.unscaledTime + HoldStartDelay;
            nextGatherAt =
                Time.unscaledTime + GatherHoldRepeatSeconds;
        }

        private bool HandleMapDrag(Mouse mouse)
        {
            if (!mapDragPending && !mapDragging)
            {
                return false;
            }

            Vector2 screen = mouse.position.ReadValue();
            if (!mapDragging)
            {
                if ((screen - mapDragLastScreen).sqrMagnitude <
                    mapDragStartPixels * mapDragStartPixels)
                {
                    return false;
                }

                mapDragging = true;
            }

            if (cameraController != null &&
                TryReadWorldPoint(mapDragLastScreen, out Vector2 lastWorld) &&
                TryReadWorldPoint(screen, out Vector2 currentWorld))
            {
                cameraController.PanByMapDrag(currentWorld - lastWorld);
            }

            mapDragLastScreen = screen;
            return true;
        }

        private bool CanStartMapDrag()
        {
            if (cameraController == null ||
                (escapeController != null && escapeController.IsSequenceRunning) ||
                (buildController != null &&
                 buildController.Mode != KomayamaInputMode.Field) ||
                !TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi())
            {
                return false;
            }

            if (Physics2D.OverlapPoint(worldPosition, interactableLayers) != null)
            {
                return false;
            }

            return !TryGetDroppedNear(worldPosition, out _);
        }

        private bool TryReadWorldPoint(Vector2 screenPosition, out Vector2 worldPosition)
        {
            worldPosition = default;
            if (targetCamera == null || !targetCamera.pixelRect.Contains(screenPosition))
            {
                return false;
            }

            Vector3 screen = new(
                screenPosition.x,
                screenPosition.y,
                -targetCamera.transform.position.z);
            worldPosition = targetCamera.ScreenToWorldPoint(screen);
            return true;
        }

        private void HandleLeft(
            bool allowResourceGather,
            bool quietPickupFailure)
        {
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi())
            {
                return;
            }

            // 初回プレス時のみ NPC 会話（ホールドリピートでは再発火しない）
            if (allowResourceGather)
            {
                KomayamaNpc npc = FindNpcAt(worldPosition);
                if (npc != null)
                {
                    if (!npc.TryTalk())
                    {
                        Reject("会話を開始できません");
                    }

                    return;
                }
            }

            ResolveLeftTargets(
                worldPosition,
                out KomayamaGhost ghost,
                out KomayamaResourceNode resourceNode,
                out KomayamaShip ship,
                out KomayamaProcessingFacility facility,
                out KomayamaStorageFacility storage,
                out KomayamaDroppedItem dropped);

            if (ghost != null)
            {
                selectedGhost = ghost;
                ghost.ToggleEnabled();
                return;
            }

            if (selectedGhost != null && resourceNode != null && !allowResourceGather)
            {
                selectedGhost.AssignNode(resourceNode);
                return;
            }

            if (selectedGhost != null && resourceNode != null && allowResourceGather)
            {
                selectedGhost.AssignNode(resourceNode);
            }

            if (ship != null)
            {
                if (!ship.TryInstall(hand, out string shipReason))
                {
                    Reject(shipReason);
                }

                return;
            }

            // 施設上でもドロップ拾いを施設メニューより優先する
            if (TryTakeDropped(worldPosition, dropped, quietPickupFailure))
            {
                return;
            }

            if (facility != null)
            {
                if (facilityMenu != null)
                {
                    facilityMenu.Open(facility);
                }
                else
                {
                    Reject("????????????");
                }

                return;
            }

            if (storage != null)
            {
                if (!storage.TryCollectOne(hand, out string storageReason))
                {
                    Reject(storageReason);
                }

                return;
            }

            if (allowResourceGather && resourceNode != null)
            {
                if (resourceNode.TryGather())
                {
                    foxFollower?.NotifyValidGatherStarted();
                }
            }
        }

        /// <summary>
        /// クリック位置のドロップ、または拾い半径内のドロップを手に入れる。
        /// </summary>
        private bool TryTakeDropped(
            Vector2 worldPosition,
            KomayamaDroppedItem droppedAtPoint,
            bool quietFailure)
        {
            KomayamaDroppedItem target = droppedAtPoint;
            if (target == null && !TryGetDroppedNear(worldPosition, out target))
            {
                return false;
            }

            if (target.TryTakeOne(hand))
            {
                seManager?.Play(KomayamaCraftSeCue.Pickup);
                return true;
            }

            if (!quietFailure)
            {
                Reject(hand != null
                    ? hand.GetAddFailureReason(target.Item)
                    : "???????");
            }

            // 拾い対象はあったが失敗（満杯など）。施設メニューへフォールスルーしない。
            return true;
        }

        private void ResolveLeftTargets(
            Vector2 worldPosition,
            out KomayamaGhost ghost,
            out KomayamaResourceNode resourceNode,
            out KomayamaShip ship,
            out KomayamaProcessingFacility facility,
            out KomayamaStorageFacility storage,
            out KomayamaDroppedItem dropped)
        {
            ghost = null;
            resourceNode = null;
            ship = null;
            facility = null;
            storage = null;
            dropped = null;
            Collider2D[] hits = Physics2D.OverlapPointAll(
                worldPosition,
                interactableLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                if (ghost == null)
                {
                    ghost = hit.GetComponentInParent<KomayamaGhost>();
                }

                if (resourceNode == null)
                {
                    resourceNode = hit.GetComponentInParent<KomayamaResourceNode>();
                }

                if (ship == null)
                {
                    ship = hit.GetComponentInParent<KomayamaShip>();
                }

                if (facility == null)
                {
                    facility = hit.GetComponentInParent<KomayamaProcessingFacility>();
                }

                if (storage == null)
                {
                    storage = hit.GetComponentInParent<KomayamaStorageFacility>();
                }

                if (dropped == null)
                {
                    KomayamaDroppedItem candidate =
                        hit.GetComponentInParent<KomayamaDroppedItem>();
                    if (candidate != null && !candidate.IsMoving)
                    {
                        dropped = candidate;
                    }
                }
            }
        }

        private KomayamaNpc FindNpcAt(Vector2 worldPosition)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(
                worldPosition,
                interactableLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                KomayamaNpc npc = hit.GetComponentInParent<KomayamaNpc>();
                if (npc != null)
                {
                    return npc;
                }
            }

            return null;
        }

        private void HandleDragPickup()
        {
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi())
            {
                return;
            }

            TryPickupDroppedNear(worldPosition, true);
        }

        private void TryPickupDroppedNear(Vector2 worldPosition, bool quietFailure)
        {
            if (!TryGetDroppedNear(worldPosition, out KomayamaDroppedItem dropped))
            {
                return;
            }

            if (dropped.TryTakeOne(hand))
            {
                seManager?.Play(KomayamaCraftSeCue.Pickup);
                if (dropped.Item != null)
                {
                    GroundItemPickedUp?.Invoke(dropped.Item, 1);
                }

                return;
            }

            if (!quietFailure)
            {
                Reject(hand != null
                    ? hand.GetAddFailureReason(dropped.Item)
                    : "拾えない");
            }
        }

        /// <summary>地面ドロップを1個拾ったとき。</summary>
        public static event System.Action<ItemDefinition, int> GroundItemPickedUp;

        private bool TryGetDroppedNear(Vector2 worldPosition, out KomayamaDroppedItem dropped)
        {
            dropped = null;
            float radius = itemSettings != null ? itemSettings.PickupRadius : 0.5f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                worldPosition,
                radius,
                interactableLayers);
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                KomayamaDroppedItem candidate =
                    hits[i] != null
                        ? hits[i].GetComponentInParent<KomayamaDroppedItem>()
                        : null;
                if (candidate == null || candidate.IsMoving)
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - worldPosition).sqrMagnitude;
                if (distance >= best)
                {
                    continue;
                }

                best = distance;
                dropped = candidate;
            }

            return dropped != null;
        }

        private KomayamaResourceNode GetResourceNodeUnderPointer()
        {
            return TryGetPointerHit(out Collider2D hit)
                ? hit.GetComponentInParent<KomayamaResourceNode>()
                : null;
        }

        private bool TryGetPointerHit(out Collider2D hit)
        {
            hit = null;
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi())
            {
                return false;
            }

            hit = Physics2D.OverlapPoint(
                worldPosition,
                interactableLayers);
            return hit != null;
        }

        private void HandleRight()
        {
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi() || hand == null || hand.IsEmpty)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapPointAll(
                worldPosition,
                interactableLayers);

            KomayamaDepositBin depositBin = FindUnderPointer<KomayamaDepositBin>(hits);
            if (depositBin != null)
            {
                // 長押し連続納入でも SE は押下あたり1回（ゴミ箱・NPC 納品口とも）
                bool playSe = !depositSePlayedThisHold;
                if (!depositBin.TryDepositOne(hand, out string binReason, playSe))
                {
                    if (playSe)
                    {
                        Reject(binReason);
                        depositSePlayedThisHold = true;
                    }
                }
                else if (playSe)
                {
                    depositSePlayedThisHold = true;
                }

                return;
            }

            KomayamaProvisionalFacility provisional =
                FindUnderPointer<KomayamaProvisionalFacility>(hits);
            if (provisional != null)
            {
                // ?????????????????????????????????
                if (provisional.TryDepositConstructionMaterial(hand, out bool completed) &&
                    completed)
                {
                    // ??????????????????????
                    suppressFacilityDepositUntilRightRelease = true;
                }

                return;
            }

            KomayamaProcessingFacility facility =
                FindUnderPointer<KomayamaProcessingFacility>(hits);
            if (facility != null)
            {
                if (suppressFacilityDepositUntilRightRelease)
                {
                    return;
                }

                if (!facility.TryDepositOne(hand, out string depositReason))
                {
                    Reject(depositReason);
                }
                return;
            }

            KomayamaStorageFacility storage =
                FindUnderPointer<KomayamaStorageFacility>(hits);
            if (storage != null)
            {
                if (suppressFacilityDepositUntilRightRelease)
                {
                    return;
                }

                if (!storage.TryDepositOne(hand, out string storageReason))
                {
                    Reject(storageReason);
                }
                return;
            }

            ItemDefinition heldItem = hand.Item;
            if (dropArea == null || !dropArea.Contains(worldPosition))
            {
                Reject("DropArea??????????????");
                return;
            }

            if (!dropArea.TrySpawnAt(heldItem, 1, worldPosition, out _))
            {
                Reject("????????????????????");
                return;
            }

            hand.TryRemoveOne(heldItem, out _);
            seManager?.Play(KomayamaCraftSeCue.Drop);
        }

        private static T FindUnderPointer<T>(Collider2D[] hits) where T : Component
        {
            if (hits == null)
            {
                return null;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null)
                {
                    continue;
                }

                T found = hits[i].GetComponentInParent<T>();
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private bool TryGetPointerWorldPosition(out Vector2 worldPosition)
        {
            if (KomayamaCraftAutoPlayInput.IsActive &&
                KomayamaCraftAutoPlayInput.TryGetForcedWorldPointer(out worldPosition))
            {
                return true;
            }

            worldPosition = default;
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            if (!targetCamera.pixelRect.Contains(screenPosition))
            {
                return false;
            }

            Vector3 screen = new(
                screenPosition.x,
                screenPosition.y,
                -targetCamera.transform.position.z);
            worldPosition = targetCamera.ScreenToWorldPoint(screen);
            return true;
        }

        /// <summary>オートプレイ用。指定ワールド位置で左クリック相当を1回実行する。</summary>
        public void AutoPlayLeftClickAt(Vector2 worldPosition)
        {
            KomayamaCraftAutoPlayInput.SetForcedWorldPointer(worldPosition);
            HandleLeft(true, false);
            KomayamaCraftAutoPlayInput.ClearForcedWorldPointer();
        }

        /// <summary>オートプレイ用。指定ワールド位置で右クリック相当を1回実行する。</summary>
        public void AutoPlayRightClickAt(Vector2 worldPosition)
        {
            KomayamaCraftAutoPlayInput.SetForcedWorldPointer(worldPosition);
            suppressFacilityDepositUntilRightRelease = false;
            depositSePlayedThisHold = false;
            HandleRight();
            KomayamaCraftAutoPlayInput.ClearForcedWorldPointer();
        }

        private bool IsPointerOverUi()
        {
            if (KomayamaCraftAutoPlayInput.IsActive &&
                KomayamaCraftAutoPlayInput.TryGetForcedWorldPointer(out _))
            {
                return false;
            }

            if (eventSystem == null)
            {
                return false;
            }

            // Physics2DRaycaster ????????? Collider ??
            // IsPointerOverGameObject() ? true ??????????????
            // Canvas?GraphicRaycaster????? UI ?????
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = mouse.position.ReadValue(),
            };
            uiRaycastScratch.Clear();
            eventSystem.RaycastAll(pointerData, uiRaycastScratch);
            for (int i = 0; i < uiRaycastScratch.Count; i++)
            {
                if (uiRaycastScratch[i].module is GraphicRaycaster)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly List<RaycastResult> uiRaycastScratch = new List<RaycastResult>();

        private void HandleModeKeys()
        {
            if (buildController == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (facilityMenu != null && facilityMenu.IsOpen)
                {
                    facilityMenu.Close();
                }
                else
                {
                    buildController.SetMode(KomayamaInputMode.Field);
                }
            }
            else if (keyboard.bKey.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SetMode(
                    buildController.Mode == KomayamaInputMode.Build
                        ? KomayamaInputMode.Field
                        : KomayamaInputMode.Build);
            }
            else if (keyboard.xKey.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SetMode(
                    buildController.Mode == KomayamaInputMode.Edit
                        ? KomayamaInputMode.Field
                        : KomayamaInputMode.Edit);
            }
            else if (keyboard.digit1Key.wasPressedThisFrame ||
                     keyboard.numpad1Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame ||
                     keyboard.numpad2Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame ||
                     keyboard.numpad3Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(2);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame ||
                     keyboard.numpad4Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(3);
            }
            else if (keyboard.digit5Key.wasPressedThisFrame ||
                     keyboard.numpad5Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(4);
            }
            else if (keyboard.digit6Key.wasPressedThisFrame ||
                     keyboard.numpad6Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(5);
            }
            else if (keyboard.digit7Key.wasPressedThisFrame ||
                     keyboard.numpad7Key.wasPressedThisFrame)
            {
                if (!IsBuildUnlocked())
                {
                    return;
                }

                buildController.SelectBuildIndex(6);
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                TryCycleFocusedRecipe();
            }
        }

        private static bool IsBuildUnlocked()
        {
            KomayamaQuestController quest = KomayamaQuestController.Instance;
            return quest == null || quest.AreBuildAndSettingsUnlocked;
        }

        private void TryCycleFocusedRecipe()
        {
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition))
            {
                return;
            }

            Collider2D hit = Physics2D.OverlapPoint(worldPosition, interactableLayers);
            KomayamaProcessingFacility facility =
                hit != null
                    ? hit.GetComponentInParent<KomayamaProcessingFacility>()
                    : null;
            if (facility == null)
            {
                return;
            }

            if (!facility.TryCycleRecipe(1, out string reason))
            {
                Reject(reason);
            }
        }

        private void HandleBuildOrEdit(Mouse mouse)
        {
            if (!TryGetPointerWorldPosition(out Vector2 worldPosition) ||
                IsPointerOverUi())
            {
                buildController.UpdatePreview(Vector2.zero, false);
                return;
            }

            buildController.UpdatePreview(worldPosition, true);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (buildController.Mode == KomayamaInputMode.Build)
                {
                    if (!buildController.TryBuildAt(worldPosition, out string buildReason))
                    {
                        Reject(buildReason);
                    }
                }
                else if (buildController.IsMovingFacility)
                {
                    if (!buildController.TryFinishMove(worldPosition, out string moveReason))
                    {
                        Reject(moveReason);
                    }
                }
                else if (TryGetPointerHit(out Collider2D hit) &&
                         !buildController.TryBeginMove(hit.gameObject, out string pickReason))
                {
                    Reject(pickReason);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (buildController.Mode == KomayamaInputMode.Build)
                {
                    buildController.SetMode(KomayamaInputMode.Field);
                }
                else if (!buildController.IsMovingFacility &&
                         TryGetPointerHit(out Collider2D hit) &&
                         !buildController.TryRemove(hit.gameObject, out string removeReason))
                {
                    Reject(removeReason);
                }
                else if (buildController.IsMovingFacility)
                {
                    buildController.CancelMove();
                }
            }
        }

        private void UpdateFocus()
        {
            if (hud == null)
            {
                return;
            }

            TryGetPointerHit(out Collider2D hit);
            hud.SetFocus(
                hit != null ? hit.GetComponentInParent<KomayamaProcessingFacility>() : null,
                hit != null ? hit.GetComponentInParent<KomayamaStorageFacility>() : null,
                hit != null ? hit.GetComponentInParent<KomayamaGhost>() : null);
        }

        private void Reject(string message)
        {
            seManager?.Play(KomayamaCraftSeCue.Invalid);
            hud?.ShowMessage(message);
        }
    }
}
