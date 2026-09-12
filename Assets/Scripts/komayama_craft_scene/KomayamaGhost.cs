using UnityEngine;

namespace KomayamaCraft
{
    public enum KomayamaGhostJob
    {
        Idle,
        Moving,
        Gathering,
        Delivering,
        SupplyingFuel
    }

    [DisallowMultipleComponent]
    public sealed class KomayamaGhost : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private KomayamaResourceNode assignedNode;
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float autoGatherSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float arriveDistance = 0.4f;

        private ItemDefinition carriedItem;
        private int carriedAmount;
        private string lockedTargetId;
        private Transform moveTarget;
        private KomayamaGhostJob job = KomayamaGhostJob.Idle;
        private float gatherReadyAt;
        private float moveStartedAt;
        private string stopReason = "待機";
        private Vector3 homePosition;

        public FacilityDefinition Definition => definition;
        public string InstanceId => instanceId;
        public bool IsEnabled => isEnabled;
        public KomayamaGhostJob Job => job;
        public string StopReason => stopReason;
        public string AssignedNodeName =>
            assignedNode != null && assignedNode.Definition != null
                ? assignedNode.Definition.DisplayName
                : "未指定";
        public string CarriedLabel =>
            carriedItem != null ? $"{carriedItem.DisplayName}×{carriedAmount}" : "空";

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = GameDataId.CreateInstanceId();
            }

            homePosition = transform.position;
        }

        private void OnDisable()
        {
            KomayamaWorkLock.ReleaseAll(instanceId);
        }

        public void BindRuntime(KomayamaCraftHud runtimeHud)
        {
            if (hud == null)
            {
                hud = runtimeHud;
            }
        }

        public void Configure(FacilityDefinition facilityDefinition, string savedInstanceId)
        {
            definition = facilityDefinition;
            if (!string.IsNullOrEmpty(savedInstanceId))
            {
                instanceId = savedInstanceId;
            }

            homePosition = transform.position;
        }

        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (!enabled)
            {
                Abort("停止中");
            }
        }

        public void ToggleEnabled()
        {
            SetEnabled(!isEnabled);
            hud?.ShowMessage(isEnabled ? "Ghostを稼働" : "Ghostを停止");
        }

        public void AssignNode(KomayamaResourceNode node)
        {
            assignedNode = node;
            hud?.ShowMessage(node != null
                ? $"自動採集: {node.Definition.DisplayName}"
                : "自動採集を解除");
        }

        private void Update()
        {
            if (!isEnabled)
            {
                stopReason = "停止中";
                return;
            }

            if (moveTarget != null)
            {
                StepMove();
                return;
            }

            if (carriedItem != null)
            {
                BeginDeliveryOrFuel();
                return;
            }

            if (TryBeginGather())
            {
                return;
            }

            if (TryBeginFuelPickup())
            {
                return;
            }

            if (TryBeginGroundPickup())
            {
                return;
            }

            if (job != KomayamaGhostJob.Idle)
            {
                job = KomayamaGhostJob.Idle;
            }

            if (string.IsNullOrEmpty(stopReason))
            {
                stopReason = "対象なし";
            }
        }

        private void StepMove()
        {
            if (moveTarget == null)
            {
                Abort("対象を失った");
                return;
            }

            if (Time.time - moveStartedAt > 8f)
            {
                Abort("到達できない");
                return;
            }

            Vector3 next = Vector3.MoveTowards(
                transform.position,
                moveTarget.position,
                moveSpeed * Time.deltaTime);
            transform.position = next;
            if (Vector2.Distance(transform.position, moveTarget.position) > arriveDistance)
            {
                return;
            }

            OnArrived();
        }

        private void OnArrived()
        {
            moveTarget = null;
            if (job == KomayamaGhostJob.Gathering)
            {
                FinishGather();
                return;
            }

            if (job == KomayamaGhostJob.Delivering)
            {
                FinishDelivery();
                return;
            }

            if (job == KomayamaGhostJob.SupplyingFuel)
            {
                FinishFuel();
            }
        }

        private bool TryBeginGather()
        {
            if (assignedNode == null || Time.time < gatherReadyAt)
            {
                return false;
            }

            if (!KomayamaWorkLock.TryAcquire(assignedNode.InstanceId, instanceId))
            {
                stopReason = "採集対象が使用中";
                return false;
            }

            lockedTargetId = assignedNode.InstanceId;
            moveTarget = assignedNode.transform;
            moveStartedAt = Time.time;
            job = KomayamaGhostJob.Gathering;
            stopReason = "採集へ移動";
            return true;
        }

        private void FinishGather()
        {
            bool spawned = assignedNode != null && assignedNode.TryGather();
            KomayamaWorkLock.Release(lockedTargetId, instanceId);
            lockedTargetId = null;
            gatherReadyAt = Time.time + autoGatherSeconds;
            job = KomayamaGhostJob.Idle;
            stopReason = spawned ? "採集した" : "置ける場所がない";
        }

        private bool TryBeginGroundPickup()
        {
            KomayamaDroppedItem[] dropped = FindObjectsByType<KomayamaDroppedItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            KomayamaDroppedItem best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < dropped.Length; i++)
            {
                KomayamaDroppedItem item = dropped[i];
                if (item == null || item.IsMoving || item.Item == null)
                {
                    continue;
                }

                if (FindStorageFor(item.Item) == null)
                {
                    continue;
                }

                if (!KomayamaWorkLock.TryAcquire(item.InstanceId, instanceId))
                {
                    continue;
                }

                KomayamaWorkLock.Release(item.InstanceId, instanceId);
                float distance = Vector2.Distance(transform.position, item.transform.position);
                if (distance < bestDistance)
                {
                    best = item;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                stopReason = HasGroundItemWithoutChest()
                    ? "受け入れ先がない"
                    : "対象なし";
                return false;
            }

            if (!KomayamaWorkLock.TryAcquire(best.InstanceId, instanceId))
            {
                return false;
            }

            lockedTargetId = best.InstanceId;
            moveTarget = best.transform;
            moveStartedAt = Time.time;
            job = KomayamaGhostJob.Delivering;
            stopReason = "回収へ移動";
            return true;
        }

        private void BeginDeliveryOrFuel()
        {
            if (job == KomayamaGhostJob.SupplyingFuel)
            {
                KomayamaProcessingFacility facility = FindFuelDestination(carriedItem);
                if (facility == null)
                {
                    Abort("燃料の行き先がない");
                    return;
                }

                moveTarget = facility.transform;
                moveStartedAt = Time.time;
                stopReason = "燃料を運ぶ";
                return;
            }

            KomayamaStorageFacility storage = FindStorageFor(carriedItem);
            if (storage == null)
            {
                Abort("受け入れ先がない");
                return;
            }

            moveTarget = storage.transform;
            moveStartedAt = Time.time;
            job = KomayamaGhostJob.Delivering;
            stopReason = "配送へ移動";
        }

        private void FinishDelivery()
        {
            if (carriedItem == null)
            {
                KomayamaDroppedItem dropped = FindLockedDroppedItem();
                if (dropped == null || !dropped.TryExtractOne(out carriedItem))
                {
                    Abort("対象を失った");
                    return;
                }

                carriedAmount = 1;
                KomayamaWorkLock.Release(lockedTargetId, instanceId);
                lockedTargetId = null;
                BeginDeliveryOrFuel();
                return;
            }

            KomayamaStorageFacility storage = FindStorageFor(carriedItem);
            if (storage == null || !storage.TryStoreOne(carriedItem))
            {
                Abort("受け入れ先がない");
                return;
            }

            carriedItem = null;
            carriedAmount = 0;
            job = KomayamaGhostJob.Idle;
            stopReason = "配送した";
        }

        private bool TryBeginFuelPickup()
        {
            KomayamaProcessingFacility[] facilities = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                KomayamaProcessingFacility facility = facilities[i];
                if (facility == null || !facility.NeedsFuelSupply || facility.Definition == null)
                {
                    continue;
                }

                if (!KomayamaWorkLock.TryAcquire(facility.InstanceId, instanceId))
                {
                    continue;
                }

                ItemDefinition fuel = FindFuelInStorage(facility);
                if (fuel == null)
                {
                    KomayamaWorkLock.Release(facility.InstanceId, instanceId);
                    stopReason = "燃料の供給元がない";
                    continue;
                }

                KomayamaStorageFacility source = FindStorageHolding(fuel);
                if (source == null)
                {
                    KomayamaWorkLock.Release(facility.InstanceId, instanceId);
                    continue;
                }

                lockedTargetId = facility.InstanceId;
                if (!source.TryTakeOne(fuel, out carriedItem))
                {
                    KomayamaWorkLock.Release(facility.InstanceId, instanceId);
                    continue;
                }

                carriedAmount = 1;
                moveTarget = facility.transform;
                moveStartedAt = Time.time;
                job = KomayamaGhostJob.SupplyingFuel;
                stopReason = "燃料を運ぶ";
                return true;
            }

            return false;
        }

        private void FinishFuel()
        {
            KomayamaProcessingFacility facility = FindLockedFacility();
            if (facility == null ||
                carriedItem == null ||
                !facility.TryAcceptFuel(carriedItem))
            {
                Abort("燃料を投入できなかった");
                return;
            }

            carriedItem = null;
            carriedAmount = 0;
            KomayamaWorkLock.Release(lockedTargetId, instanceId);
            lockedTargetId = null;
            job = KomayamaGhostJob.Idle;
            stopReason = "燃料を補給した";
        }

        private void Abort(string reason)
        {
            if (carriedItem != null)
            {
                KomayamaDropArea dropArea = FindFirstObjectByType<KomayamaDropArea>();
                KomayamaFacilityContents.GiveOrDrop(
                    carriedItem,
                    carriedAmount,
                    null,
                    dropArea,
                    transform.position);
                carriedItem = null;
                carriedAmount = 0;
            }

            KomayamaWorkLock.Release(lockedTargetId, instanceId);
            KomayamaWorkLock.ReleaseAll(instanceId);
            lockedTargetId = null;
            moveTarget = null;
            job = KomayamaGhostJob.Idle;
            stopReason = reason;
        }

        private KomayamaStorageFacility FindStorageFor(ItemDefinition item)
        {
            KomayamaStorageFacility fallback = null;
            KomayamaStorageFacility[] storages = FindObjectsByType<KomayamaStorageFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                KomayamaStorageFacility storage = storages[i];
                if (storage == null || !storage.CanAccept(item))
                {
                    continue;
                }

                if (storage.StoredItem == item)
                {
                    return storage;
                }

                if (storage.StoredItem == null && fallback == null)
                {
                    fallback = storage;
                }
            }

            return fallback;
        }

        private static bool HasGroundItemWithoutChest()
        {
            return FindFirstObjectByType<KomayamaDroppedItem>() != null;
        }

        private KomayamaDroppedItem FindLockedDroppedItem()
        {
            KomayamaDroppedItem[] dropped = FindObjectsByType<KomayamaDroppedItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < dropped.Length; i++)
            {
                if (dropped[i] != null && dropped[i].InstanceId == lockedTargetId)
                {
                    return dropped[i];
                }
            }

            return null;
        }

        private KomayamaProcessingFacility FindLockedFacility()
        {
            KomayamaProcessingFacility[] facilities = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                if (facilities[i] != null && facilities[i].InstanceId == lockedTargetId)
                {
                    return facilities[i];
                }
            }

            return null;
        }

        private static KomayamaProcessingFacility FindFuelDestination(ItemDefinition fuel)
        {
            KomayamaProcessingFacility[] facilities = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < facilities.Length; i++)
            {
                if (facilities[i] != null &&
                    facilities[i].NeedsFuelSupply &&
                    facilities[i].CanAcceptFuel(fuel))
                {
                    return facilities[i];
                }
            }

            return null;
        }

        private static ItemDefinition FindFuelInStorage(KomayamaProcessingFacility facility)
        {
            if (facility.Definition == null)
            {
                return null;
            }

            for (int i = 0; i < facility.Definition.AcceptedFuelItems.Count; i++)
            {
                ItemDefinition fuel = facility.Definition.AcceptedFuelItems[i];
                if (FindStorageHolding(fuel) != null)
                {
                    return fuel;
                }
            }

            return null;
        }

        private static KomayamaStorageFacility FindStorageHolding(ItemDefinition item)
        {
            KomayamaStorageFacility[] storages = FindObjectsByType<KomayamaStorageFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                if (storages[i] != null &&
                    storages[i].StoredItem == item &&
                    storages[i].StoredAmount > 0)
                {
                    return storages[i];
                }
            }

            return null;
        }

        public void CaptureSave(GhostSaveDto dto)
        {
            dto.facilityDefinitionId = definition != null ? definition.DefinitionId : string.Empty;
            dto.instanceId = instanceId;
            dto.position = new Float2SaveDto(transform.position.x, transform.position.y);
            dto.isEnabled = isEnabled;
            dto.assignedResourceNodeInstanceId =
                assignedNode != null ? assignedNode.InstanceId : string.Empty;
            dto.carriedItemDefinitionId = carriedItem != null ? carriedItem.DefinitionId : string.Empty;
            dto.carriedAmount = carriedAmount;
            dto.stopReason = stopReason;
        }

        public void ApplySave(GhostSaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            instanceId = dto.instanceId;
            isEnabled = dto.isEnabled;
            stopReason = dto.stopReason;
            carriedAmount = dto.carriedAmount;
            carriedItem = null;
            if (!string.IsNullOrEmpty(dto.carriedItemDefinitionId))
            {
                GameDataCatalogs.KomayamaItems.TryGet(dto.carriedItemDefinitionId, out carriedItem);
            }

            assignedNode = null;
            if (!string.IsNullOrEmpty(dto.assignedResourceNodeInstanceId))
            {
                KomayamaResourceNode[] nodes = FindObjectsByType<KomayamaResourceNode>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] != null &&
                        nodes[i].InstanceId == dto.assignedResourceNodeInstanceId)
                    {
                        assignedNode = nodes[i];
                        break;
                    }
                }
            }

            homePosition = transform.position;
        }
    }
}
