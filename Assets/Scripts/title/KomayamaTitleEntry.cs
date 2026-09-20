using KomayamaCraft;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class KomayamaTitleEntry : MonoBehaviour
{
    private enum ConfirmMode
    {
        None,
        Overwrite,
        Delete
    }

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField, Tooltip("設定の ConfigCanvas と同様。スロット UI 全体の Canvas。未設定時は slotPanel のみ開閉。")]
    private GameObject slotCanvas;
    [SerializeField] private GameObject slotPanel;
    [SerializeField] private TMP_Text slotPanelTitle;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private KomayamaTitleSlotCard[] slotCards;
    [SerializeField] private Button slotBackButton;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private TMP_Text confirmYesLabel;
    [SerializeField] private string craftSceneName = "komayama_craft_scene";

    private bool selectingNewGame;
    private int pendingSlot;
    private ConfirmMode confirmMode;

    private void Awake()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OpenNewGameSlots);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OpenContinueSlots);
        }

        if (slotBackButton != null)
        {
            slotBackButton.onClick.AddListener(CloseSlotPanel);
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.AddListener(ConfirmYes);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.AddListener(CancelConfirm);
        }

        if (confirmYesLabel == null && confirmYesButton != null)
        {
            confirmYesLabel = confirmYesButton.GetComponentInChildren<TMP_Text>(true);
        }

        WireSlotButtons();
        ShowMainButtons();
    }

    private void OnDestroy()
    {
        UnwireSlotCards();
    }

    private void WireSlotButtons()
    {
        UnwireSlotCards();

        if (slotCards != null)
        {
            for (int i = 0; i < slotCards.Length; i++)
            {
                KomayamaTitleSlotCard card = slotCards[i];
                if (card == null)
                {
                    continue;
                }

                int slot = card.SlotNumber > 0 ? card.SlotNumber : i + 1;
                Button cardButton = card.SelectButton;
                if (cardButton != null)
                {
                    cardButton.onClick.RemoveAllListeners();
                    cardButton.onClick.AddListener(() => SelectSlot(slot));
                }

                card.WireDeleteClick();
                card.DeleteRequested += OnSlotDeleteRequested;
            }
        }

        if (slotButtons == null)
        {
            return;
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null)
            {
                continue;
            }

            if (HasCardForSlot(i + 1))
            {
                continue;
            }

            int slot = i + 1;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
        }
    }

    private void UnwireSlotCards()
    {
        if (slotCards == null)
        {
            return;
        }

        for (int i = 0; i < slotCards.Length; i++)
        {
            if (slotCards[i] != null)
            {
                slotCards[i].DeleteRequested -= OnSlotDeleteRequested;
            }
        }
    }

    private bool HasCardForSlot(int slot)
    {
        if (slotCards == null)
        {
            return false;
        }

        for (int i = 0; i < slotCards.Length; i++)
        {
            if (slotCards[i] != null && slotCards[i].SlotNumber == slot)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        RefreshContinueAvailability();
    }

    public void OpenNewGameSlots()
    {
        selectingNewGame = true;
        ShowSlotPanel("はじめから");
    }

    public void OpenContinueSlots()
    {
        selectingNewGame = false;
        ShowSlotPanel("続きから");
    }

    public void CloseSlotPanel()
    {
        CancelConfirm();
        ShowMainButtons();
    }

    public void StartNewGame()
    {
        OpenNewGameSlots();
    }

    public void ContinueGame()
    {
        OpenContinueSlots();
    }

    private void SelectSlot(int slot)
    {
        KomayamaSaveSlotInfo info = KomayamaSaveSlots.GetInfo(slot);
        if (selectingNewGame)
        {
            if (info.HasData)
            {
                ShowConfirm(
                    ConfirmMode.Overwrite,
                    slot,
                    $"スロット{slot}のデータを上書きしますか？",
                    "上書きする");
                return;
            }

            BeginNewGame(slot);
            return;
        }

        if (!info.HasData)
        {
            return;
        }

        KomayamaBootRequest.RequestContinue(slot);
        KomayamaCraftSceneLoader.LoadCraftScene(craftSceneName);
    }

    private void OnSlotDeleteRequested(int slot)
    {
        if (!KomayamaSaveSlots.HasData(slot))
        {
            return;
        }

        ShowConfirm(
            ConfirmMode.Delete,
            slot,
            $"スロット{slot}のセーブデータを削除しますか？",
            "削除する");
    }

    private void ShowConfirm(ConfirmMode mode, int slot, string message, string yesLabel)
    {
        confirmMode = mode;
        pendingSlot = slot;
        if (confirmText != null)
        {
            confirmText.text = message;
        }

        if (confirmYesLabel != null)
        {
            confirmYesLabel.text = yesLabel;
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    private void ConfirmYes()
    {
        int slot = pendingSlot;
        ConfirmMode mode = confirmMode;
        CancelConfirm();
        if (slot < 1)
        {
            return;
        }

        if (mode == ConfirmMode.Overwrite)
        {
            BeginNewGame(slot);
            return;
        }

        if (mode == ConfirmMode.Delete)
        {
            KomayamaSaveSlots.DeleteSlotData(slot);
            RefreshSlotButtons();
            RefreshContinueAvailability();
        }
    }

    private void CancelConfirm()
    {
        pendingSlot = 0;
        confirmMode = ConfirmMode.None;
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void BeginNewGame(int slot)
    {
        KomayamaBootRequest.RequestNewGame(slot);
        KomayamaCraftSceneLoader.LoadCraftScene(craftSceneName);
    }

    private void ShowMainButtons()
    {
        SetSlotCanvasVisible(false);

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(true);
        }

        RefreshContinueAvailability();
    }

    private void ShowSlotPanel(string title)
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (slotPanelTitle != null)
        {
            slotPanelTitle.text = title;
        }

        SetSlotCanvasVisible(true);
        RefreshSlotButtons();
    }

    private void SetSlotCanvasVisible(bool visible)
    {
        if (slotCanvas != null)
        {
            slotCanvas.SetActive(visible);
            if (visible && slotPanel != null && !slotPanel.activeSelf)
            {
                slotPanel.SetActive(true);
            }

            return;
        }

        if (slotPanel != null)
        {
            slotPanel.SetActive(visible);
        }
    }

    private void RefreshContinueAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        // 全スロット未使用なら出さない（灰ボタンより親切）
        bool hasSave = KomayamaSaveSlots.HasAnySave();
        continueButton.gameObject.SetActive(hasSave);
        continueButton.interactable = hasSave;
    }

    private void RefreshSlotButtons()
    {
        int lastSlot = KomayamaSaveSlots.LastPlayedSlot;
        if (slotCards != null)
        {
            for (int i = 0; i < slotCards.Length; i++)
            {
                KomayamaTitleSlotCard card = slotCards[i];
                if (card == null)
                {
                    continue;
                }

                int slot = card.SlotNumber > 0 ? card.SlotNumber : i + 1;
                KomayamaSaveSlotInfo info = KomayamaSaveSlots.GetInfo(slot);
                bool selectable = selectingNewGame || info.HasData;
                card.Apply(info, slot == lastSlot, selectable);
            }
        }

        int count = slotButtons != null ? slotButtons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int slot = i + 1;
            if (HasCardForSlot(slot))
            {
                continue;
            }

            KomayamaSaveSlotInfo info = KomayamaSaveSlots.GetInfo(slot);
            bool isLast = slot == lastSlot;
            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
            {
                slotLabels[i].text = info.FormatLabel(isLast);
            }

            if (slotButtons[i] != null)
            {
                slotButtons[i].interactable = selectingNewGame || info.HasData;
                Image image = slotButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = isLast
                        ? new Color(1f, 0.88f, 0.55f, 0.98f)
                        : new Color(0.97f, 0.93f, 0.82f, 0.96f);
                }
            }
        }
    }
}
