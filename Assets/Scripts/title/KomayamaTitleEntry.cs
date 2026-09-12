using KomayamaCraft;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class KomayamaTitleEntry : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject slotPanel;
    [SerializeField] private TMP_Text slotPanelTitle;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Button slotBackButton;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private string craftSceneName = "komayama_craft_scene";

    private bool selectingNewGame;
    private int pendingOverwriteSlot;

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
            confirmYesButton.onClick.AddListener(ConfirmOverwrite);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.AddListener(CancelOverwrite);
        }

        if (slotButtons != null)
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i + 1;
                if (slotButtons[i] != null)
                {
                    slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
                }
            }
        }

        ShowMainButtons();
    }

    private void OnEnable()
    {
        RefreshContinueAvailability();
    }

    public void OpenNewGameSlots()
    {
        selectingNewGame = true;
        ShowSlotPanel("新規開始するスロット");
    }

    public void OpenContinueSlots()
    {
        selectingNewGame = false;
        ShowSlotPanel("続きから始めるスロット");
    }

    public void CloseSlotPanel()
    {
        CancelOverwrite();
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
                pendingOverwriteSlot = slot;
                if (confirmText != null)
                {
                    confirmText.text = $"スロット{slot}のデータを上書きしますか？";
                }

                if (confirmPanel != null)
                {
                    confirmPanel.SetActive(true);
                }

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
        SceneManager.LoadScene(craftSceneName);
    }

    private void ConfirmOverwrite()
    {
        int slot = pendingOverwriteSlot;
        CancelOverwrite();
        if (slot >= 1)
        {
            BeginNewGame(slot);
        }
    }

    private void CancelOverwrite()
    {
        pendingOverwriteSlot = 0;
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void BeginNewGame(int slot)
    {
        KomayamaBootRequest.RequestNewGame(slot);
        SceneManager.LoadScene(craftSceneName);
    }

    private void ShowMainButtons()
    {
        if (slotPanel != null)
        {
            slotPanel.SetActive(false);
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(true);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
        }

        RefreshContinueAvailability();
    }

    private void ShowSlotPanel(string title)
    {
        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (slotPanelTitle != null)
        {
            slotPanelTitle.text = title;
        }

        if (slotPanel != null)
        {
            slotPanel.SetActive(true);
        }

        RefreshSlotButtons();
    }

    private void RefreshContinueAvailability()
    {
        if (continueButton != null)
        {
            continueButton.interactable = KomayamaSaveSlots.HasAnySave();
        }
    }

    private void RefreshSlotButtons()
    {
        int lastSlot = KomayamaSaveSlots.LastPlayedSlot;
        int count = slotButtons != null ? slotButtons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int slot = i + 1;
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
