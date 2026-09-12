using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftHud : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KomayamaResourceNode resourceNode;
        [SerializeField] private KomayamaProcessingFacility facility;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private KomayamaProgressService progress;
        [SerializeField] private KomayamaShip ship;
        [SerializeField] private TMP_Text handText;
        [SerializeField] private TMP_Text guideText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform cursorHandRoot;
        [SerializeField, Min(0f)] private float messageSeconds = 2.5f;

        private float messageUntil;
        private KomayamaProcessingFacility focusedProcessing;
        private KomayamaStorageFacility focusedStorage;
        private KomayamaGhost focusedGhost;

        public void SetFocus(
            KomayamaProcessingFacility processing,
            KomayamaStorageFacility storage,
            KomayamaGhost ghost = null)
        {
            focusedProcessing = processing;
            focusedStorage = storage;
            focusedGhost = ghost;
        }

        private void Update()
        {
            UpdateHand();
            UpdateState();
            UpdateCursorHand();

            if (messageText != null &&
                messageText.gameObject.activeSelf &&
                Time.unscaledTime >= messageUntil)
            {
                messageText.gameObject.SetActive(false);
            }
        }

        public void ShowMessage(string message)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageUntil = Time.unscaledTime + messageSeconds;
        }

        private void UpdateHand()
        {
            if (handText == null || hand == null)
            {
                return;
            }

            handText.text = DescribeHand();
        }

        private void UpdateState()
        {
            if (stateText == null)
            {
                return;
            }

            string mode = buildController != null
                ? buildController.ModeLabel
                : "通常";
            string gather = "採集: クリックで1回 / 長押しで連続";
            string progression = DescribeProgress();
            string focused = DescribeFocusedFacility();
            if (string.IsNullOrEmpty(focused) && facility != null)
            {
                focused = DescribeProcessing(facility);
            }

            stateText.text = $"{mode}\n{gather}\n{progression}\n{focused}";

            if (guideText != null)
            {
                guideText.text =
                    "左:採集/回収 右:投入 R:レシピ 1-7:建設 G:強化 Enter:脱出 T:案内 F5/F9";
            }
        }

        private void UpdateCursorHand()
        {
            if (cursorHandRoot == null || Mouse.current == null)
            {
                return;
            }

            if (cursorHandRoot.GetComponent<KCCursorHandIcons>() != null ||
                cursorHandRoot.GetComponent<KCMouseHandView>() != null)
            {
                return;
            }

            cursorHandRoot.position = Mouse.current.position.ReadValue();
            bool show = hand != null && !hand.IsEmpty;
            cursorHandRoot.gameObject.SetActive(show);
            if (show && cursorHandRoot.TryGetComponent(out TMP_Text cursorText))
            {
                cursorText.text = DescribeHand();
            }
        }

        private string DescribeHand()
        {
            if (hand == null || hand.IsEmpty)
            {
                return "手持ち: なし";
            }

            var parts = new System.Collections.Generic.List<string>();
            ItemDefinition current = null;
            int amount = 0;
            System.Collections.Generic.List<ItemDefinition> sorted = hand.GetSortedItems();
            for (int i = 0; i < sorted.Count; i++)
            {
                ItemDefinition item = sorted[i];
                if (item == current)
                {
                    amount++;
                    continue;
                }

                if (current != null)
                {
                    parts.Add($"{current.DisplayName}×{amount}");
                }

                current = item;
                amount = 1;
            }

            if (current != null)
            {
                parts.Add($"{current.DisplayName}×{amount}");
            }

            return $"手持ち: {string.Join(" ", parts)} ({hand.TotalCount}/{hand.Capacity})";
        }

        private string DescribeProgress()
        {
            string shipLabel = ship != null ? ship.StatusLabel : string.Empty;
            string progressLabel = progress != null
                ? $"Lv{progress.Level} EXP{progress.Experience} SP{progress.SkillPoints} T{progress.CurrentTier}"
                : string.Empty;
            return string.IsNullOrEmpty(shipLabel)
                ? progressLabel
                : $"{shipLabel}  {progressLabel}";
        }

        private string DescribeFocusedFacility()
        {
            if (focusedGhost != null)
            {
                return $"Ghost: {(focusedGhost.IsEnabled ? focusedGhost.Job.ToString() : "停止")} {focusedGhost.StopReason} 採集:{focusedGhost.AssignedNodeName} 運搬:{focusedGhost.CarriedLabel}";
            }

            if (focusedStorage != null)
            {
                string storageName = focusedStorage.Definition != null
                    ? focusedStorage.Definition.DisplayName
                    : "保管";
                return $"{storageName}: {focusedStorage.StoredItemName} × {focusedStorage.StoredAmount} / {focusedStorage.Capacity}";
            }

            if (focusedProcessing != null)
            {
                return DescribeProcessing(focusedProcessing);
            }

            return string.Empty;
        }

        private static string DescribeProcessing(KomayamaProcessingFacility target)
        {
            string name = target.Definition != null ? target.Definition.DisplayName : "設備";
            string recipe = target.RecipeName;
            string fuel = target.Definition != null && target.Definition.UsesFuel
                ? $" 燃料 {target.FuelItemName}×{target.FuelAmount}/{target.Definition.FuelCapacity}"
                : string.Empty;
            return target.State switch
            {
                KomayamaFacilityState.Processing =>
                    $"{name} [{recipe}]: 加工中 {target.Progress01:P0}{fuel}",
                KomayamaFacilityState.WaitingForOutput =>
                    $"{name} [{recipe}]: 完成品 {target.OutputItemName}{fuel}",
                KomayamaFacilityState.WaitingForFuel =>
                    $"{name} [{recipe}]: 燃料不足 {target.InputItemName}{fuel}",
                _ =>
                    $"{name} [{recipe}]: 材料 {target.InputItemName}{fuel}"
            };
        }
    }
}
