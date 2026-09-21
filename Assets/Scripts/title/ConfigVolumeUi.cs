using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定パネルの音量 ±。タイトル／クラフト共通。SE・BGM 適用は任意参照。
/// </summary>
[DisallowMultipleComponent]
public sealed class ConfigVolumeUi : MonoBehaviour
{
    [SerializeField] private Button masterMinusButton;
    [SerializeField] private Button masterPlusButton;
    [SerializeField] private Button bgmMinusButton;
    [SerializeField] private Button bgmPlusButton;
    [SerializeField] private Button seMinusButton;
    [SerializeField] private Button sePlusButton;
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text seValueText;

    [Header("SE（プレハブ共用 ConfigSePlayer 優先）")]
    [SerializeField, Tooltip("ConfigCanvas プレハブ上の共用 SE。未設定時のみ titleSeManager。")]
    private ConfigSePlayer configSePlayer;
    [SerializeField, Tooltip("タイトル専用フォールバック。")]
    private TitleSeManager titleSeManager;
    [SerializeField, Tooltip("未設定なら BGM 即時反映はしない（クラフト等）。")]
    private TitleBgmManager titleBgmManager;

    private UnityEngine.Events.UnityAction masterMinusAction;
    private UnityEngine.Events.UnityAction masterPlusAction;
    private UnityEngine.Events.UnityAction bgmMinusAction;
    private UnityEngine.Events.UnityAction bgmPlusAction;
    private UnityEngine.Events.UnityAction seMinusAction;
    private UnityEngine.Events.UnityAction sePlusAction;

    private void Awake()
    {
        if (configSePlayer == null)
        {
            configSePlayer = GetComponent<ConfigSePlayer>();
        }

        masterMinusAction = () => Change(SoundVolumeTarget.Master, -1);
        masterPlusAction = () => Change(SoundVolumeTarget.Master, 1);
        bgmMinusAction = () => Change(SoundVolumeTarget.Bgm, -1);
        bgmPlusAction = () => Change(SoundVolumeTarget.Bgm, 1);
        seMinusAction = () => Change(SoundVolumeTarget.Se, -1);
        sePlusAction = () => Change(SoundVolumeTarget.Se, 1);
    }

    private void OnEnable()
    {
        Wire(masterMinusButton, masterMinusAction);
        Wire(masterPlusButton, masterPlusAction);
        Wire(bgmMinusButton, bgmMinusAction);
        Wire(bgmPlusButton, bgmPlusAction);
        Wire(seMinusButton, seMinusAction);
        Wire(sePlusButton, sePlusAction);
        RefreshTexts();
    }

    private void OnDisable()
    {
        Unwire(masterMinusButton, masterMinusAction);
        Unwire(masterPlusButton, masterPlusAction);
        Unwire(bgmMinusButton, bgmMinusAction);
        Unwire(bgmPlusButton, bgmPlusAction);
        Unwire(seMinusButton, seMinusAction);
        Unwire(sePlusButton, sePlusAction);
    }

    private void Update()
    {
        if (titleBgmManager != null)
        {
            titleBgmManager.ApplyBgmVolumeFromSettings();
        }
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void Unwire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private void Change(SoundVolumeTarget target, int delta)
    {
        SoundSettingsManager settings = SoundSettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        if (!ConfigSePlayer.TryPlay(
                configSePlayer,
                delta > 0 ? TitleSeCue.ConfigVolumeUp : TitleSeCue.ConfigVolumeDown))
        {
            TitleSeManager.TryPlay(
                titleSeManager,
                delta > 0 ? TitleSeCue.ConfigVolumeUp : TitleSeCue.ConfigVolumeDown);
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

        RefreshTexts();
        if (titleBgmManager != null)
        {
            titleBgmManager.ApplyBgmVolumeFromSettings();
        }
    }

    private void RefreshTexts()
    {
        SoundSettingsManager settings = SoundSettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        SetText(masterValueText, settings.GetMasterVolume());
        SetText(bgmValueText, settings.GetBgmVolume());
        SetText(seValueText, settings.GetSeVolume());
    }

    private static void SetText(TMP_Text text, int value)
    {
        if (text != null)
        {
            text.text = value.ToString("00");
        }
    }

    private enum SoundVolumeTarget
    {
        Master = 0,
        Bgm = 1,
        Se = 2
    }
}
