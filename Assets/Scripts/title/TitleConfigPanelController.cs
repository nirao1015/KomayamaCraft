using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ConfigCanvas のタブ切替と一般設定（言語 ID・非アクティブ挙動）。
/// キー設定は表示のみ（変更・セーブ・ゲーム内ショートカットは未実装）。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleConfigPanelController : MonoBehaviour
{
    private enum ConfigTab
    {
        General = 0,
        Volume = 1,
        Key = 2
    }

    private const string LanguageJa = "ja";
    private const string LanguageEn = "en";

    private const string DefaultKeyBuild = "R";
    private const string DefaultKeyEdit = "T";
    private const string DefaultKeySkill = "F";
    private const string DefaultKeyDrop = "Space";
    private const string DefaultKeyItemSwitch = "E";

    [Header("タブ")]
    [SerializeField] private Button generalTabButton;
    [SerializeField] private Button volumeTabButton;
    [SerializeField] private Button keyTabButton;
    [SerializeField, Tooltip("選択中表示。選択時のみ active。")]
    private GameObject generalTabSelectedImage;
    [SerializeField, Tooltip("選択中表示。選択時のみ active。")]
    private GameObject volumeTabSelectedImage;
    [SerializeField, Tooltip("選択中表示。選択時のみ active。")]
    private GameObject keyTabSelectedImage;
    [SerializeField] private GameObject panelGeneral;
    [SerializeField] private GameObject panelVolume;
    [SerializeField] private GameObject panelKey;

    [Header("一般")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private Toggle pauseWhenInactiveToggle;
    [SerializeField] private Toggle playAudioWhenInactiveToggle;

    [Header("キー（表示のみ）")]
    [SerializeField] private TMP_Text keyBuildValueText;
    [SerializeField] private TMP_Text keyEditValueText;
    [SerializeField] private TMP_Text keySkillValueText;
    [SerializeField] private TMP_Text keyDropValueText;
    [SerializeField] private TMP_Text keyItemSwitchValueText;
    [SerializeField] private Button keyResetToDefaultButton;

    [Header("SE")]
    [SerializeField, Tooltip("タブ押下時の SE。未設定時は鳴らさない。")]
    private TitleSeManager titleSeManager;

    private bool suppressUiCallbacks;
    private ConfigTab currentTab = ConfigTab.General;

    private void Awake()
    {
        WireUi();
        ShowGeneralTab();
        RefreshGeneralFromSettings();
        RefreshKeyBindingsDisplay();
    }

    private void OnEnable()
    {
        // Config を開くたびに一般タブへ
        ShowGeneralTab();
        RefreshGeneralFromSettings();
        RefreshKeyBindingsDisplay();
    }

    private void OnDestroy()
    {
        UnwireUi();
    }

    private void WireUi()
    {
        // Image / ImageFilter は raycast を切り、親 Button へクリックを通す。
        // ImageSelected は見た目用（子のままでは Button へバブルするため、再押下防止は currentTab で行う）
        DisableDecorativeChildRaycasts(generalTabButton);
        DisableDecorativeChildRaycasts(volumeTabButton);
        DisableDecorativeChildRaycasts(keyTabButton);

        if (generalTabButton != null)
        {
            generalTabButton.onClick.AddListener(OnClickGeneralTab);
        }

        if (volumeTabButton != null)
        {
            volumeTabButton.onClick.AddListener(OnClickVolumeTab);
        }

        if (keyTabButton != null)
        {
            keyTabButton.onClick.AddListener(OnClickKeyTab);
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
        }

        if (pauseWhenInactiveToggle != null)
        {
            pauseWhenInactiveToggle.onValueChanged.AddListener(OnPauseWhenInactiveChanged);
        }

        if (playAudioWhenInactiveToggle != null)
        {
            playAudioWhenInactiveToggle.onValueChanged.AddListener(OnPlayAudioWhenInactiveChanged);
        }

        if (keyResetToDefaultButton != null)
        {
            keyResetToDefaultButton.onClick.AddListener(OnClickKeyResetToDefault);
        }
    }

    private void UnwireUi()
    {
        if (generalTabButton != null)
        {
            generalTabButton.onClick.RemoveListener(OnClickGeneralTab);
        }

        if (volumeTabButton != null)
        {
            volumeTabButton.onClick.RemoveListener(OnClickVolumeTab);
        }

        if (keyTabButton != null)
        {
            keyTabButton.onClick.RemoveListener(OnClickKeyTab);
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownChanged);
        }

        if (pauseWhenInactiveToggle != null)
        {
            pauseWhenInactiveToggle.onValueChanged.RemoveListener(OnPauseWhenInactiveChanged);
        }

        if (playAudioWhenInactiveToggle != null)
        {
            playAudioWhenInactiveToggle.onValueChanged.RemoveListener(OnPlayAudioWhenInactiveChanged);
        }

        if (keyResetToDefaultButton != null)
        {
            keyResetToDefaultButton.onClick.RemoveListener(OnClickKeyResetToDefault);
        }
    }

    public void ShowGeneralTab()
    {
        SetActiveTab(ConfigTab.General);
    }

    public void ShowVolumeTab()
    {
        SetActiveTab(ConfigTab.Volume);
    }

    public void ShowKeyTab()
    {
        SetActiveTab(ConfigTab.Key);
        RefreshKeyBindingsDisplay();
    }

    private void OnClickGeneralTab()
    {
        if (currentTab == ConfigTab.General)
        {
            return;
        }

        PlayTabSwitchSe();
        ShowGeneralTab();
    }

    private void OnClickVolumeTab()
    {
        if (currentTab == ConfigTab.Volume)
        {
            return;
        }

        PlayTabSwitchSe();
        ShowVolumeTab();
    }

    private void OnClickKeyTab()
    {
        if (currentTab == ConfigTab.Key)
        {
            return;
        }

        PlayTabSwitchSe();
        ShowKeyTab();
    }

    private void PlayTabSwitchSe()
    {
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.ConfigTabSwitch);
    }

    private void SetActiveTab(ConfigTab tab)
    {
        currentTab = tab;

        if (panelGeneral != null)
        {
            panelGeneral.SetActive(tab == ConfigTab.General);
        }

        if (panelVolume != null)
        {
            panelVolume.SetActive(tab == ConfigTab.Volume);
        }

        if (panelKey != null)
        {
            panelKey.SetActive(tab == ConfigTab.Key);
        }

        SetTabSelectedImage(generalTabSelectedImage, tab == ConfigTab.General);
        SetTabSelectedImage(volumeTabSelectedImage, tab == ConfigTab.Volume);
        SetTabSelectedImage(keyTabSelectedImage, tab == ConfigTab.Key);
    }

    private static void SetTabSelectedImage(GameObject selectedImage, bool selected)
    {
        if (selectedImage != null)
        {
            selectedImage.SetActive(selected);
        }
    }

    private void RefreshGeneralFromSettings()
    {
        SoundSettingsManager settings = SoundSettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        suppressUiCallbacks = true;
        try
        {
            if (languageDropdown != null)
            {
                EnsureLanguageOptions();
                languageDropdown.value = settings.GetLanguageId() == LanguageEn ? 1 : 0;
                languageDropdown.RefreshShownValue();
            }

            if (pauseWhenInactiveToggle != null)
            {
                pauseWhenInactiveToggle.isOn = settings.GetPauseWhenInactive();
            }

            if (playAudioWhenInactiveToggle != null)
            {
                playAudioWhenInactiveToggle.isOn = settings.GetPlayAudioWhenInactive();
            }
        }
        finally
        {
            suppressUiCallbacks = false;
        }
    }

    private void EnsureLanguageOptions()
    {
        if (languageDropdown == null)
        {
            return;
        }

        if (languageDropdown.options != null && languageDropdown.options.Count >= 2)
        {
            return;
        }

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "日本語",
            "English"
        });
    }

    private void OnLanguageDropdownChanged(int index)
    {
        if (suppressUiCallbacks)
        {
            return;
        }

        string id = index == 1 ? LanguageEn : LanguageJa;
        SoundSettingsManager.Instance?.SetLanguageId(id);
    }

    private void OnPauseWhenInactiveChanged(bool isOn)
    {
        if (suppressUiCallbacks)
        {
            return;
        }

        SoundSettingsManager.Instance?.SetPauseWhenInactive(isOn);
    }

    private void OnPlayAudioWhenInactiveChanged(bool isOn)
    {
        if (suppressUiCallbacks)
        {
            return;
        }

        SoundSettingsManager.Instance?.SetPlayAudioWhenInactive(isOn);
    }

    private void OnClickKeyResetToDefault()
    {
        // 表示のみ。セーブ・実キー変更は未実装。
        RefreshKeyBindingsDisplay();
    }

    private void RefreshKeyBindingsDisplay()
    {
        SetKeyText(keyBuildValueText, DefaultKeyBuild);
        SetKeyText(keyEditValueText, DefaultKeyEdit);
        SetKeyText(keySkillValueText, DefaultKeySkill);
        SetKeyText(keyDropValueText, DefaultKeyDrop);
        SetKeyText(keyItemSwitchValueText, DefaultKeyItemSwitch);
    }

    private static void SetKeyText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void DisableDecorativeChildRaycasts(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.gameObject == button.gameObject)
            {
                continue;
            }

            if (IsTabSelectedImage(graphic.gameObject))
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }

    private bool IsTabSelectedImage(GameObject go)
    {
        return go == generalTabSelectedImage
            || go == volumeTabSelectedImage
            || go == keyTabSelectedImage;
    }
}
