using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaShip : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaProgressService progress;
        [SerializeField] private ItemDefinition hullSlotItem;
        [SerializeField] private ItemDefinition emberSlotItem;
        [SerializeField] private ItemDefinition starChartSlotItem;
        [SerializeField] private ItemDefinition inertiaRingSlotItem;
        [SerializeField] private ItemDefinition compassSlotItem;
        [SerializeField] private bool hullInstalled;
        [SerializeField] private bool emberInstalled;
        [SerializeField] private bool starChartInstalled;
        [SerializeField] private bool inertiaRingInstalled;
        [SerializeField] private bool compassInstalled;

        public bool HullInstalled => hullInstalled;
        public bool EmberInstalled => emberInstalled;
        public bool StarChartInstalled => starChartInstalled;
        public bool InertiaRingInstalled => inertiaRingInstalled;
        public bool CompassInstalled => compassInstalled;
        public bool IsTakeoffReady => inertiaRingInstalled;
        public bool CanEscape => compassInstalled;

        public string StatusLabel =>
            $"船: 鱗{(hullInstalled ? "済" : "未")} 火{(emberInstalled ? "済" : "未")} " +
            $"図{(starChartInstalled ? "済" : "未")} 環{(inertiaRingInstalled ? "済" : "未")} " +
            $"針{(compassInstalled ? "済" : "未")}" +
            (CanEscape ? " 脱出可" : IsTakeoffReady ? " 離陸可" : string.Empty);

        public bool TryInstall(KomayamaHandInventory fromHand, out string failureReason)
        {
            failureReason = null;
            if (fromHand == null || fromHand.IsEmpty)
            {
                failureReason = "投入する修復部品がありません";
                return false;
            }

            if (TryInstallSlot(fromHand, hullSlotItem, ref hullInstalled, "船殻再生鱗", out failureReason) ||
                TryInstallSlot(fromHand, emberSlotItem, ref emberInstalled, "恒星炉の種火", out failureReason) ||
                TryInstallSlot(fromHand, starChartSlotItem, ref starChartInstalled, "星図復元核", out failureReason) ||
                TryInstallSlot(fromHand, inertiaRingSlotItem, ref inertiaRingInstalled, "慣性相殺環", out failureReason) ||
                TryInstallSlot(fromHand, compassSlotItem, ref compassInstalled, "恒星間羅針", out failureReason))
            {
                return string.IsNullOrEmpty(failureReason);
            }

            failureReason = "この部品は船に取り付けられません";
            return false;
        }

        private bool TryInstallSlot(
            KomayamaHandInventory fromHand,
            ItemDefinition required,
            ref bool installed,
            string label,
            out string failureReason)
        {
            failureReason = null;
            if (required == null || fromHand.CountOf(required) <= 0)
            {
                return false;
            }

            if (installed)
            {
                failureReason = $"{label}は投入済みです";
                return true;
            }

            if (!fromHand.TryRemoveOne(out _))
            {
                failureReason = "投入できません";
                return true;
            }

            installed = true;
            progress?.RecordDelivery(required, 1);
            hud?.ShowMessage($"{label}を取り付けた");
            return true;
        }

        public void CaptureSave(ShipProgressSaveDto dto)
        {
            dto.EnsureCollections();
            dto.slots.Clear();
            AddSlot(dto, "hull", hullSlotItem, hullInstalled);
            AddSlot(dto, "ember", emberSlotItem, emberInstalled);
            AddSlot(dto, "star_chart", starChartSlotItem, starChartInstalled);
            AddSlot(dto, "inertia_ring", inertiaRingSlotItem, inertiaRingInstalled);
            AddSlot(dto, "compass", compassSlotItem, compassInstalled);
        }

        public void ApplySave(ShipProgressSaveDto dto)
        {
            hullInstalled = false;
            emberInstalled = false;
            starChartInstalled = false;
            inertiaRingInstalled = false;
            compassInstalled = false;
            if (dto?.slots == null)
            {
                return;
            }

            for (int i = 0; i < dto.slots.Count; i++)
            {
                switch (dto.slots[i].slotId)
                {
                    case "hull":
                        hullInstalled = dto.slots[i].isInstalled;
                        break;
                    case "ember":
                        emberInstalled = dto.slots[i].isInstalled;
                        break;
                    case "star_chart":
                        starChartInstalled = dto.slots[i].isInstalled;
                        break;
                    case "inertia_ring":
                        inertiaRingInstalled = dto.slots[i].isInstalled;
                        break;
                    case "compass":
                        compassInstalled = dto.slots[i].isInstalled;
                        break;
                }
            }
        }

        private static void AddSlot(
            ShipProgressSaveDto dto,
            string slotId,
            ItemDefinition item,
            bool installed)
        {
            dto.slots.Add(new ShipSlotSaveDto
            {
                slotId = slotId,
                requiredItemDefinitionId = item != null ? item.DefinitionId : string.Empty,
                isInstalled = installed
            });
        }
    }
}
