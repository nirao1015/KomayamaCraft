using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaStorageFacility : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftSeManager seManager;

        private ItemDefinition storedItem;
        private int storedAmount;

        public FacilityDefinition Definition => definition;
        public string InstanceId => instanceId;
        public ItemDefinition StoredItem => storedItem;
        public int StoredAmount => storedAmount;
        public int Capacity => definition != null ? definition.StorageCapacity : 0;
        public string StoredItemName =>
            storedItem != null ? storedItem.DisplayName : "空";

        private void Awake()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = GameDataId.CreateInstanceId();
            }
        }

        public void BindRuntime(KomayamaCraftHud runtimeHud, KomayamaCraftSeManager runtimeSe)
        {
            if (hud == null)
            {
                hud = runtimeHud;
            }

            if (seManager == null)
            {
                seManager = runtimeSe;
            }
        }

        public void Configure(FacilityDefinition facilityDefinition, string savedInstanceId)
        {
            definition = facilityDefinition;
            if (!string.IsNullOrEmpty(savedInstanceId))
            {
                instanceId = savedInstanceId;
            }
        }

        public bool TryDepositOne(KomayamaHandInventory hand, out string failureReason)
        {
            failureReason = null;
            if (hand == null || hand.IsEmpty || definition == null)
            {
                failureReason = "格納できる素材がありません";
                return false;
            }

            ItemDefinition incoming = storedItem != null ? storedItem : hand.Item;
            if (incoming == null || hand.CountOf(incoming) <= 0)
            {
                failureReason = storedItem != null
                    ? $"この保管設備は{storedItem.DisplayName}だけ受け入れます"
                    : "格納できる素材がありません";
                return false;
            }

            if (storedAmount >= definition.StorageCapacity)
            {
                failureReason = "保管設備が満杯です";
                return false;
            }

            if (!hand.TryRemoveOne(incoming, out _))
            {
                failureReason = "格納できる素材がありません";
                return false;
            }

            storedItem = incoming;
            storedAmount++;
            seManager?.Play(KomayamaCraftSeCue.Deposit);
            return true;
        }

        public bool TryCollectOne(KomayamaHandInventory hand, out string failureReason)
        {
            failureReason = null;
            if (hand == null || storedItem == null || storedAmount <= 0)
            {
                failureReason = "取り出せる保管物がありません";
                return false;
            }

            if (!hand.TryAdd(storedItem))
            {
                failureReason = hand.GetAddFailureReason(storedItem);
                return false;
            }

            storedAmount--;
            if (storedAmount <= 0)
            {
                storedAmount = 0;
                storedItem = null;
            }

            seManager?.Play(KomayamaCraftSeCue.Pickup);
            return true;
        }

        public bool CanAccept(ItemDefinition item)
        {
            return item != null &&
                   definition != null &&
                   storedAmount < definition.StorageCapacity &&
                   (storedItem == null || storedItem == item);
        }

        public bool TryStoreOne(ItemDefinition item)
        {
            if (!CanAccept(item))
            {
                return false;
            }

            storedItem = item;
            storedAmount++;
            return true;
        }

        public bool TryTakeOne(ItemDefinition required, out ItemDefinition taken)
        {
            taken = null;
            if (storedItem == null || storedAmount <= 0)
            {
                return false;
            }

            if (required != null && storedItem != required)
            {
                return false;
            }

            taken = storedItem;
            storedAmount--;
            if (storedAmount <= 0)
            {
                storedAmount = 0;
                storedItem = null;
            }

            return true;
        }

        public void EvacuateTo(KomayamaHandInventory hand, KomayamaDropArea dropArea)
        {
            if (storedItem == null || storedAmount <= 0)
            {
                return;
            }

            KomayamaFacilityContents.GiveOrDrop(
                storedItem,
                storedAmount,
                hand,
                dropArea,
                transform.position);
            storedItem = null;
            storedAmount = 0;
        }

        public void CaptureSave(FacilitySaveDto dto)
        {
            dto.facilityDefinitionId = definition != null ? definition.DefinitionId : string.Empty;
            dto.instanceId = instanceId;
            dto.position = new Float2SaveDto(transform.position.x, transform.position.y);
            dto.rotationDegrees = transform.eulerAngles.z;
            dto.processingState = FacilityProcessingState.Idle;
            dto.processingProgress01 = 0f;
            dto.selectedRecipeId = string.Empty;
            dto.EnsureCollections();
            dto.inputItems.Clear();
            if (storedItem != null && storedAmount > 0)
            {
                dto.inputItems.Add(new ItemStackSaveDto
                {
                    itemDefinitionId = storedItem.DefinitionId,
                    amount = storedAmount
                });
            }
        }

        public void ApplySave(FacilitySaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            instanceId = dto.instanceId;
            storedItem = null;
            storedAmount = 0;
            if (dto.inputItems != null &&
                dto.inputItems.Count > 0 &&
                GameDataCatalogs.KomayamaItems.TryGet(
                    dto.inputItems[0].itemDefinitionId,
                    out ItemDefinition item))
            {
                storedItem = item;
                storedAmount = Mathf.Max(0, dto.inputItems[0].amount);
            }
        }
    }
}
