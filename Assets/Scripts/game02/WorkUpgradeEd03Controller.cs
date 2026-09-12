using UnityEngine;

namespace Game02
{
    public sealed class WorkUpgradeEd03Controller : WorkUpgradeEdBuyControllerBase
    {
        public static void EnsureSceneWorkUpgradeEd03Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd03");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd03");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd03Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd03Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            basePrice = 22000L;
            priceMultiplier = 2.05d;
            purchaseLimit = 5;
        }

        protected override int GetPurchasedCount()
        {
            return UpgradesManager.Instance != null ? UpgradesManager.Instance.GetWorkUpgradeEd03PurchaseCount() : 0;
        }

        protected override bool ApplyPurchase()
        {
            return UpgradesManager.Instance != null && UpgradesManager.Instance.ApplyEd03Purchase();
        }
    }
}
