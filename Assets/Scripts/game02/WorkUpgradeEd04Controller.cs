using UnityEngine;

namespace Game02
{
    public sealed class WorkUpgradeEd04Controller : WorkUpgradeEdBuyControllerBase
    {
        public static void EnsureSceneWorkUpgradeEd04Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd04");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd04");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd04Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd04Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            basePrice = 28000L;
            priceMultiplier = 2.10d;
            purchaseLimit = 5;
        }

        protected override int GetPurchasedCount()
        {
            return UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd04PurchaseCount() : 0;
        }

        protected override bool ApplyPurchase()
        {
            return UpgradesManager.Instance != null && UpgradesManager.Instance.ApplyEd04Purchase();
        }
    }
}
