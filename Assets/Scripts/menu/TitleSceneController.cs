using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TitleSceneController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TitleSeManager titleSeManager;
    [SerializeField] private TitleBgmManager titleBgmManager;

    [Header("Config UI")]
    [SerializeField] private Button masterMinusButton;
    [SerializeField] private Button masterPlusButton;
    [SerializeField] private Button bgmMinusButton;
    [SerializeField] private Button bgmPlusButton;
    [SerializeField] private Button seMinusButton;
    [SerializeField] private Button sePlusButton;
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private TextMeshProUGUI seValueText;
    [SerializeField] private float configCanvasBgAlpha = 0.82f;

    private const string MasterMinusButtonObjectName = "MasterMinusButton";
    private const string MasterPlusButtonObjectName = "MasterPlusButton";
    private const string BgmMinusButtonObjectName = "BgmMinusButton";
    private const string BgmPlusButtonObjectName = "BgmPlusButton";
    private const string SeMinusButtonObjectName = "SeMinusButton";
    private const string SePlusButtonObjectName = "SePlusButton";
    private const string MasterValueTextObjectName = "MasterValueText";
    private const string BgmValueTextObjectName = "BgmValueText";
    private const string SeValueTextObjectName = "SeValueText";

    private void Awake()
    {
        ResolveCoreReferences();
        EnsureConfigUiVisuals();
        RefreshConfigVolumeTexts();
    }

    private void Start()
    {
        SoundSettingsManager.Instance?.UpdateRecordedBuildVersionOnTitleEntry();
    }

    private void OnEnable()
    {
        if (masterMinusButton != null) masterMinusButton.onClick.AddListener(OnClickMasterMinus);
        if (masterPlusButton != null) masterPlusButton.onClick.AddListener(OnClickMasterPlus);
        if (bgmMinusButton != null) bgmMinusButton.onClick.AddListener(OnClickBgmMinus);
        if (bgmPlusButton != null) bgmPlusButton.onClick.AddListener(OnClickBgmPlus);
        if (seMinusButton != null) seMinusButton.onClick.AddListener(OnClickSeMinus);
        if (sePlusButton != null) sePlusButton.onClick.AddListener(OnClickSePlus);
    }

    private void OnDisable()
    {
        if (masterMinusButton != null) masterMinusButton.onClick.RemoveListener(OnClickMasterMinus);
        if (masterPlusButton != null) masterPlusButton.onClick.RemoveListener(OnClickMasterPlus);
        if (bgmMinusButton != null) bgmMinusButton.onClick.RemoveListener(OnClickBgmMinus);
        if (bgmPlusButton != null) bgmPlusButton.onClick.RemoveListener(OnClickBgmPlus);
        if (seMinusButton != null) seMinusButton.onClick.RemoveListener(OnClickSeMinus);
        if (sePlusButton != null) sePlusButton.onClick.RemoveListener(OnClickSePlus);
    }

    private void Update()
    {
        ApplyBgmVolume();
    }

    private void OnClickMasterMinus()
    {
        PlayConfigVolumeButtonSe(false);
        ChangeVolume(SoundVolumeTarget.Master, -1);
    }

    private void OnClickMasterPlus()
    {
        PlayConfigVolumeButtonSe(true);
        ChangeVolume(SoundVolumeTarget.Master, 1);
    }

    private void OnClickBgmMinus()
    {
        PlayConfigVolumeButtonSe(false);
        ChangeVolume(SoundVolumeTarget.Bgm, -1);
    }

    private void OnClickBgmPlus()
    {
        PlayConfigVolumeButtonSe(true);
        ChangeVolume(SoundVolumeTarget.Bgm, 1);
    }

    private void OnClickSeMinus()
    {
        PlayConfigVolumeButtonSe(false);
        ChangeVolume(SoundVolumeTarget.Se, -1);
    }

    private void OnClickSePlus()
    {
        PlayConfigVolumeButtonSe(true);
        ChangeVolume(SoundVolumeTarget.Se, 1);
    }

    private void PlayConfigVolumeButtonSe(bool isIncrease)
    {
        TitleSeManager.TryPlay(titleSeManager, isIncrease ? TitleSeCue.ConfigVolumeUp : TitleSeCue.ConfigVolumeDown);
    }

    private void ChangeVolume(SoundVolumeTarget target, int delta)
    {
        SoundSettingsManager settings = SoundSettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        switch (target)
        {
            case SoundVolumeTarget.Master:
                settings.SetMasterVolume(settings.GetMasterVolume() + delta);
                break;
            case SoundVolumeTarget.Bgm:
                settings.SetBgmVolume(settings.GetBgmVolume() + delta);
                break;
            case SoundVolumeTarget.Se:
                settings.SetSeVolume(settings.GetSeVolume() + delta);
                break;
        }

        RefreshConfigVolumeTexts();
        ApplyBgmVolume();
    }

    private void RefreshConfigVolumeTexts()
    {
        SoundSettingsManager settings = SoundSettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        if (masterValueText != null) masterValueText.text = settings.GetMasterVolume().ToString("00");
        if (bgmValueText != null) bgmValueText.text = settings.GetBgmVolume().ToString("00");
        if (seValueText != null) seValueText.text = settings.GetSeVolume().ToString("00");
    }

    private void ApplyBgmVolume()
    {
        if (titleBgmManager != null)
        {
            titleBgmManager.ApplyBgmVolumeFromSettings();
        }
    }

    private void EnsureConfigUiVisuals()
    {
        // Inspector 調整済みの CanvasBG 見た目はここで上書きしない。
    }

    private void ResolveCoreReferences()
    {
        if (masterMinusButton == null) masterMinusButton = FindButton(MasterMinusButtonObjectName);
        if (masterPlusButton == null) masterPlusButton = FindButton(MasterPlusButtonObjectName);
        if (bgmMinusButton == null) bgmMinusButton = FindButton(BgmMinusButtonObjectName);
        if (bgmPlusButton == null) bgmPlusButton = FindButton(BgmPlusButtonObjectName);
        if (seMinusButton == null) seMinusButton = FindButton(SeMinusButtonObjectName);
        if (sePlusButton == null) sePlusButton = FindButton(SePlusButtonObjectName);
        if (masterValueText == null) masterValueText = FindTmpText(MasterValueTextObjectName);
        if (bgmValueText == null) bgmValueText = FindTmpText(BgmValueTextObjectName);
        if (seValueText == null) seValueText = FindTmpText(SeValueTextObjectName);

    }

    private static GameObject FindSceneGameObjectByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName))
        {
            return null;
        }

        GameObject active = GameObject.Find(exactName);
        if (active != null)
        {
            return active;
        }

        int sceneCount = SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null)
                {
                    continue;
                }

                if (root.name == exactName)
                {
                    return root;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (t != null && t.name == exactName)
                    {
                        return t.gameObject;
                    }
                }
            }
        }

        return null;
    }

    private static Button FindButton(string objectName)
    {
        GameObject buttonObject = FindSceneGameObjectByExactName(objectName);
        if (buttonObject == null)
        {
            return null;
        }

        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI FindTmpText(string objectName)
    {
        GameObject textObject = FindSceneGameObjectByExactName(objectName);
        if (textObject == null)
        {
            return null;
        }

        return textObject.GetComponent<TextMeshProUGUI>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveCoreReferences();
    }
#endif

    private enum SoundVolumeTarget
    {
        Master,
        Bgm,
        Se
    }
}
