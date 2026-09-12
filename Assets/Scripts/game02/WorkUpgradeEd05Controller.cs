using System.Collections;
using UnityEngine;

namespace Game02
{
    public sealed class WorkUpgradeEd05Controller : WorkUpgradeEdBuyControllerBase
    {
        [SerializeField]
        private long[] tierSalePrices = { 4900L, 15300L, 116000L };

        private bool hasInitializedEd05BootState;

        public static void EnsureSceneWorkUpgradeEd05Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd05");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd05");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd05Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd05Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            purchaseLimit = 3;
        }

        public override long GetCurrentSalePrice()
        {
            if (IsSoldOut)
            {
                return 1L;
            }

            int level = GetPurchasedCount();
            if (tierSalePrices == null || level < 0 || level >= tierSalePrices.Length)
            {
                return 1L;
            }

            return tierSalePrices[level] > 0L ? tierSalePrices[level] : 1L;
        }

        protected override int GetPurchasedCount()
        {
            return UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd05PurchaseCount() : 0;
        }

        protected override bool ApplyPurchase()
        {
            if (UpgradesManager.Instance == null)
            {
                return false;
            }

            if (!UpgradesManager.Instance.ApplyEd05Purchase(out int unlockedSlotIndex))
            {
                return false;
            }

            UnlockWorkMovieSlot(unlockedSlotIndex);
            SyncFieldViewEd05AdditionalHoles();
            StartCoroutine(CoUnlockWorkMovieNextFrame(unlockedSlotIndex));
            return true;
        }

        protected override void RunInitialStatusCheckOnce()
        {
            if (hasInitializedEd05BootState)
            {
                return;
            }

            hasInitializedEd05BootState = true;
            if (UpgradesManager.Instance == null)
            {
                return;
            }

            int maxSlots = UpgradesManager.Instance.GetMaxUploadSlotCount();
            for (int slot = 3; slot <= maxSlots; slot++)
            {
                WorkMovieSlotController.UnlockBySlotIndex(slot);
            }

            SyncFieldViewEd05AdditionalHoles();
        }

        private IEnumerator CoUnlockWorkMovieNextFrame(int slotIndex)
        {
            yield return null;
            UnlockWorkMovieSlot(slotIndex);
            SyncFieldViewEd05AdditionalHoles();
        }

        private static void UnlockWorkMovieSlot(int slotIndex)
        {
            WorkMovieSlotController.UnlockBySlotIndex(slotIndex);

            WorkMovieSlotController[] slots = Object.FindObjectsOfType<WorkMovieSlotController>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                WorkMovieSlotController slot = slots[i];
                if (slot != null && slot.SlotIndex == slotIndex)
                {
                    slot.DisableInvalidViewByUpgrade();
                }
            }
        }

        private static void SyncFieldViewEd05AdditionalHoles()
        {
            FieldView[] views = Object.FindObjectsOfType<FieldView>(true);
            int purchasedCount = UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd05PurchaseCount() : 0;
            for (int i = 0; i < views.Length; i++)
            {
                FieldView view = views[i];
                if (view != null)
                {
                    view.ApplyEd05AdditionalHolesByPurchaseCount(purchasedCount);
                }
            }
        }
    }
}
