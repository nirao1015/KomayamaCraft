using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
            HandleModeKeys();
            Mouse mouse = Mouse.current;
            if (mouse == null || targetCamera == null)
            {
                return;
            }

            if (buildController != null &&
                buildController.Mode != KomayamaInputMode.Field)
            {
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
                    return;
                }

                HandleDragPickup();

                KomayamaResourceNode hoveredResource =
                    GetResourceNodeUnderPointer();
                if (heldResourceNode != null &&
                    hoveredResource == heldResourceNode &&
                    Time.unscaledTime >= nextGatherAt)
                {
                    heldResourceNode.TryGather();
                    nextGatherAt =
                        Time.unscaledTime + GatherHoldRepeatSeconds;
                }
                else if (hoveredResource == null &&
                         Time.unscaledTime >= nextLeftRepeatAt)
                {
                    HandleLeft(false, false);
                    nextLeftRepeatAt =
                        Time.unscaledTime + HoldRepeatSeconds;
                }
            }
            else
            {
                heldResourceNode = null;
                mapDragPending = false;
                mapDragging = false;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                HandleRight();
                nextRightRepeatAt = Time.unscaledTime + RightHoldStartDelay;
            }
            else if (mouse.rightButton.isPressed)
            {
                RepeatRightHold();
            }
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

            if (facility != null)
            {
                if (!facility.TryCollectOne(hand, out string collectReason))
                {
                    Reject(collectReason);
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
                resourceNode.TryGather();
                return;
            }

            if (dropped != null)
            {
                if (dropped.TryTakeOne(hand))
                {
                    seManager?.Play(KomayamaCraftSeCue.Pickup);
                }
                else if (!quietPickupFailure)
                {
                    Reject(hand != null
                        ? hand.GetAddFailureReason(dropped.Item)
                        : "回収できません");
                }

                return;
            }

            TryPickupDroppedNear(worldPosition, quietPickupFailure);
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
                return;
            }

            if (!quietFailure)
            {
                Reject(hand != null
                    ? hand.GetAddFailureReason(dropped.Item)
                    : "回収できません");
            }
        }

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

            Collider2D hit = Physics2D.OverlapPoint(
                worldPosition,
                interactableLayers);
            KomayamaProcessingFacility facility =
                hit != null
                    ? hit.GetComponentInParent<KomayamaProcessingFacility>()
                    : null;
            if (facility != null)
            {
                if (!facility.TryDepositOne(hand, out string depositReason))
                {
                    Reject(depositReason);
                }
                return;
            }

            KomayamaStorageFacility storage =
                hit != null
                    ? hit.GetComponentInParent<KomayamaStorageFacility>()
                    : null;
            if (storage != null)
            {
                if (!storage.TryDepositOne(hand, out string storageReason))
                {
                    Reject(storageReason);
                }
                return;
            }

            ItemDefinition heldItem = hand.Item;
            if (dropArea == null || !dropArea.Contains(worldPosition))
            {
                Reject("DropAreaの外にはアイテムを置けません");
                return;
            }

            if (!dropArea.TrySpawnAt(heldItem, 1, worldPosition, out _))
            {
                Reject("置ける空間がないため、ドロップできません");
                return;
            }

            hand.TryRemoveOne(heldItem, out _);
            seManager?.Play(KomayamaCraftSeCue.Drop);
        }

        private bool TryGetPointerWorldPosition(out Vector2 worldPosition)
        {
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

        private bool IsPointerOverUi()
        {
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

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
                buildController.SetMode(KomayamaInputMode.Field);
            }
            else if (keyboard.bKey.wasPressedThisFrame)
            {
                buildController.SetMode(
                    buildController.Mode == KomayamaInputMode.Build
                        ? KomayamaInputMode.Field
                        : KomayamaInputMode.Build);
            }
            else if (keyboard.xKey.wasPressedThisFrame)
            {
                buildController.SetMode(
                    buildController.Mode == KomayamaInputMode.Edit
                        ? KomayamaInputMode.Field
                        : KomayamaInputMode.Edit);
            }
            else if (keyboard.digit1Key.wasPressedThisFrame ||
                     keyboard.numpad1Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame ||
                     keyboard.numpad2Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame ||
                     keyboard.numpad3Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(2);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame ||
                     keyboard.numpad4Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(3);
            }
            else if (keyboard.digit5Key.wasPressedThisFrame ||
                     keyboard.numpad5Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(4);
            }
            else if (keyboard.digit6Key.wasPressedThisFrame ||
                     keyboard.numpad6Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(5);
            }
            else if (keyboard.digit7Key.wasPressedThisFrame ||
                     keyboard.numpad7Key.wasPressedThisFrame)
            {
                buildController.SelectBuildIndex(6);
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                TryCycleFocusedRecipe();
            }
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
