using System.Collections;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// WorkUpgradeEd01（編集枠購入数）専用。
    /// </summary>
    public sealed class WorkUpgradeEd01Controller : WorkUpgradeEdBuyControllerBase
    {
        private bool hasInitializedEd01BootState;

        public static void EnsureSceneWorkUpgradeEd01Exists()
        {
            GameObject go = GameObject.Find("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/WorkUpgradeEd01");
            if (go == null)
            {
                go = FindInactiveObjectByName("WorkUpgradeEd01");
            }

            if (go != null && go.GetComponent<WorkUpgradeEd01Controller>() == null)
            {
                go.AddComponent<WorkUpgradeEd01Controller>();
            }
        }

        protected override void ConfigureControllerDefaults()
        {
            // Game02Economy の共通スケール後に 2,600 付近になるよう調整する。
            basePrice = 8000L;
            priceMultiplier = 1d;
            purchaseLimit = 1;
        }

        protected override int GetPurchasedCount()
        {
            if (UpgradesManager.Instance == null)
            {
                return 0;
            }

            return UpgradesManager.Instance.GetWorkUpgradeEd01PurchaseCount();
        }

        protected override bool ApplyPurchase()
        {
            if (UpgradesManager.Instance == null)
            {
                return false;
            }

            bool applied = UpgradesManager.Instance.ApplyEd01Purchase();
            if (applied)
            {
                TryUnlockEditor2Immediately();
                StartCoroutine(CoUnlockEditor2NextFrame());
            }

            return applied;
        }

        protected override void RunInitialStatusCheckOnce()
        {
            if (hasInitializedEd01BootState)
            {
                return;
            }

            hasInitializedEd01BootState = true;
            WorkEditor editor2 = FindWorkEditor2();
            bool isEditor2Unlocked = editor2 != null && !editor2.IsDormant;
            if (isEditor2Unlocked)
            {
                UpgradesManager.Instance?.ApplyEd01Purchase();
                NotifyMoneyOrUpgradeStateChanged();
                return;
            }

            if (UpgradesManager.Instance != null && UpgradesManager.Instance.IsWorkUpgradeEd01Purchased())
            {
                StartCoroutine(CoUnlockEditor2NextFrame());
                NotifyMoneyOrUpgradeStateChanged();
            }
        }

        private WorkEditor FindWorkEditor2()
        {
            GameObject go = GameObject.Find("UnitCanvas/WorkEditor_2");
            if (go != null)
            {
                return go.GetComponent<WorkEditor>();
            }

            WorkEditor[] editors = FindObjectsOfType<WorkEditor>(true);
            for (int i = 0; i < editors.Length; i++)
            {
                WorkEditor editor = editors[i];
                if (editor != null && editor.name == "WorkEditor_2")
                {
                    return editor;
                }
            }

            return null;
        }

        private IEnumerator CoUnlockEditor2NextFrame()
        {
            yield return null;
            TryUnlockEditor2Immediately();
        }

        private void TryUnlockEditor2Immediately()
        {
            WorkEditor editor2 = FindWorkEditor2();
            if (editor2 == null)
            {
                return;
            }

            editor2.UnlockSecondSlotIfPurchasedEd01();
            editor2.DisableInvalidViewByUpgrade();
        }
    }
}
