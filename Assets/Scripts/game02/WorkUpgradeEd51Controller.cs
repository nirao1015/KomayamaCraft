using UnityEngine;

namespace Game02
{
    public sealed class WorkUpgradeEd51Controller : WorkUpgradeEdBuyControllerBase
    {
        public static void EnsureSceneWorkUpgradeEd51Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd51");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd51");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd51Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd51Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            basePrice = 100000L;
            priceMultiplier = 1d;
            purchaseLimit = 1;
        }

        protected override int GetPurchasedCount()
        {
            return UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd51PurchaseCount() : 0;
        }

        protected override bool ApplyPurchase()
        {
            return UpgradesManager.Instance != null && UpgradesManager.Instance.ApplyEd51Purchase();
        }
    }
}
