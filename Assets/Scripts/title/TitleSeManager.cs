using UnityEngine;

public enum TitleSeCue
{
    TransitionStart = 0,
    ConfigToggle = 1,
    ConfigVolumeUp = 2,
    ConfigVolumeDown = 3,
    ConfigTabSwitch = 4,
    SlotDelete = 5,
    ConfirmCancel = 6,
    ConfirmYes = 7,
    ConfirmDeleteYes = 8,
    ConfirmOpen = 9,
    AppQuit = 10
}

[DisallowMultipleComponent]
public sealed class TitleSeManager : MonoBehaviour
{
    [Header("共通設定")]
    [SerializeField, Tooltip("既定のSE再生先AudioSource。各項目のAudioSourceが未設定時に使います。")]
    private AudioSource defaultAudioSource;

    [Header("シーン遷移SE")]
    [SerializeField, Tooltip("シーン遷移開始時に鳴らすSE。")]
    private AudioClip transitionStartSeClip;
    [SerializeField, Tooltip("シーン遷移SEを鳴らすAudioSource。")]
    private AudioSource transitionStartSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("シーン遷移SEの基準音量。")]
    private float transitionStartSeVolume = 1f;

    [Header("設定画面 開閉SE")]
    [SerializeField, Tooltip("Configの開閉時に鳴らすSE。")]
    private AudioClip configToggleSeClip;
    [SerializeField, Tooltip("Config開閉SEを鳴らすAudioSource。")]
    private AudioSource configToggleSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("Config開閉SEの基準音量。")]
    private float configToggleSeVolume = 1f;

    [Header("設定画面 音量アップSE")]
    [SerializeField, Tooltip("Configの音量アップ操作時に鳴らすSE。")]
    private AudioClip configVolumeUpSeClip;
    [SerializeField, Tooltip("Config音量アップSEを鳴らすAudioSource。")]
    private AudioSource configVolumeUpSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("Config音量アップSEの基準音量。")]
    private float configVolumeUpSeVolume = 1f;

    [Header("設定画面 音量ダウンSE")]
    [SerializeField, Tooltip("Configの音量ダウン操作時に鳴らすSE。")]
    private AudioClip configVolumeDownSeClip;
    [SerializeField, Tooltip("Config音量ダウンSEを鳴らすAudioSource。")]
    private AudioSource configVolumeDownSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("Config音量ダウンSEの基準音量。")]
    private float configVolumeDownSeVolume = 1f;

    [Header("設定画面 タブ切替SE")]
    [SerializeField, Tooltip("Configのタブ押下時に鳴らすSE。")]
    private AudioClip configTabSwitchSeClip;
    [SerializeField, Tooltip("Configタブ切替SEを鳴らすAudioSource。")]
    private AudioSource configTabSwitchSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("Configタブ切替SEの基準音量。")]
    private float configTabSwitchSeVolume = 1f;

    [Header("スロット削除ボタンSE")]
    [SerializeField, Tooltip("スロット削除ボタン押下時に鳴らすSE。")]
    private AudioClip slotDeleteSeClip;
    [SerializeField, Tooltip("スロット削除SEを鳴らすAudioSource。")]
    private AudioSource slotDeleteSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("スロット削除SEの基準音量。")]
    private float slotDeleteSeVolume = 1f;

    [Header("確認 キャンセルSE")]
    [SerializeField, Tooltip("ConfirmPanel のキャンセル（No）押下時に鳴らすSE。")]
    private AudioClip confirmCancelSeClip;
    [SerializeField, Tooltip("確認キャンセルSEを鳴らすAudioSource。")]
    private AudioSource confirmCancelSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("確認キャンセルSEの基準音量。")]
    private float confirmCancelSeVolume = 1f;

    [Header("確認 YES SE")]
    [SerializeField, Tooltip("初期化確認の YES。遷移 SE（TransitionStart）と重なるため未設定（null）でよい。項目は残す。")]
    private AudioClip confirmYesSeClip;
    [SerializeField, Tooltip("確認 YES SEを鳴らすAudioSource。")]
    private AudioSource confirmYesSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("確認 YES SEの基準音量。")]
    private float confirmYesSeVolume = 1f;

    [Header("確認 削除YES SE")]
    [SerializeField, Tooltip("スロット削除確認で YES したときに鳴らすSE（削除ボタンSEとは別）。")]
    private AudioClip confirmDeleteYesSeClip;
    [SerializeField, Tooltip("削除確認 YES SEを鳴らすAudioSource。")]
    private AudioSource confirmDeleteYesSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("削除確認 YES SEの基準音量。")]
    private float confirmDeleteYesSeVolume = 1f;

    [Header("確認画面オープン SE")]
    [SerializeField, Tooltip("スロット選択で確認画面が開くときに鳴らすSE（即遷移時は鳴らさない）。")]
    private AudioClip confirmOpenSeClip;
    [SerializeField, Tooltip("確認オープンSEを鳴らすAudioSource。")]
    private AudioSource confirmOpenSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("確認オープンSEの基準音量。")]
    private float confirmOpenSeVolume = 1f;

    [Header("終了ボタン SE")]
    [SerializeField, Tooltip("EndButton 押下時に鳴らすSE。再生完了後にアプリ終了。")]
    private AudioClip appQuitSeClip;
    [SerializeField, Tooltip("終了SEを鳴らすAudioSource。")]
    private AudioSource appQuitSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("終了SEの基準音量。")]
    private float appQuitSeVolume = 1f;

    private void Awake()
    {
        if (defaultAudioSource == null)
        {
            defaultAudioSource = GetComponent<AudioSource>();
        }
    }

    public bool PlayByCue(TitleSeCue cue)
    {
        float unused;
        return PlayByCue(cue, out unused);
    }

    public bool PlayByCue(TitleSeCue cue, out float playedClipLengthSeconds)
    {
        return cue switch
        {
            TitleSeCue.TransitionStart => PlayInternal(transitionStartSeSource, transitionStartSeClip, transitionStartSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfigToggle => PlayInternal(configToggleSeSource, configToggleSeClip, configToggleSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfigVolumeUp => PlayInternal(configVolumeUpSeSource, configVolumeUpSeClip, configVolumeUpSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfigVolumeDown => PlayInternal(configVolumeDownSeSource, configVolumeDownSeClip, configVolumeDownSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfigTabSwitch => PlayInternal(configTabSwitchSeSource, configTabSwitchSeClip, configTabSwitchSeVolume, out playedClipLengthSeconds),
            TitleSeCue.SlotDelete => PlayInternal(slotDeleteSeSource, slotDeleteSeClip, slotDeleteSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfirmCancel => PlayInternal(confirmCancelSeSource, confirmCancelSeClip, confirmCancelSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfirmYes => PlayInternal(confirmYesSeSource, confirmYesSeClip, confirmYesSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfirmDeleteYes => PlayInternal(confirmDeleteYesSeSource, confirmDeleteYesSeClip, confirmDeleteYesSeVolume, out playedClipLengthSeconds),
            TitleSeCue.ConfirmOpen => PlayInternal(confirmOpenSeSource, confirmOpenSeClip, confirmOpenSeVolume, out playedClipLengthSeconds),
            TitleSeCue.AppQuit => PlayInternal(appQuitSeSource, appQuitSeClip, appQuitSeVolume, out playedClipLengthSeconds),
            _ => FailPlay(out playedClipLengthSeconds)
        };
    }

    /// <summary>
    /// 破棄済み参照でも例外にしない（C# の ?. は Unity 擬似 null を拾えない）。
    /// </summary>
    public static bool TryPlay(TitleSeManager manager, TitleSeCue cue)
    {
        float unused;
        return TryPlay(manager, cue, out unused);
    }

    public static bool TryPlay(TitleSeManager manager, TitleSeCue cue, out float playedClipLengthSeconds)
    {
        playedClipLengthSeconds = 0f;
        if (manager == null)
        {
            return false;
        }

        return manager.PlayByCue(cue, out playedClipLengthSeconds);
    }

    public bool PlayClipByCueSource(TitleSeCue cue, AudioClip clip, float baseVolume = 1f)
    {
        if (clip == null)
        {
            return false;
        }

        AudioSource source = cue switch
        {
            TitleSeCue.TransitionStart => ResolveSource(transitionStartSeSource),
            TitleSeCue.ConfigToggle => ResolveSource(configToggleSeSource),
            TitleSeCue.ConfigVolumeUp => ResolveSource(configVolumeUpSeSource),
            TitleSeCue.ConfigVolumeDown => ResolveSource(configVolumeDownSeSource),
            TitleSeCue.ConfigTabSwitch => ResolveSource(configTabSwitchSeSource),
            TitleSeCue.SlotDelete => ResolveSource(slotDeleteSeSource),
            TitleSeCue.ConfirmCancel => ResolveSource(confirmCancelSeSource),
            TitleSeCue.ConfirmYes => ResolveSource(confirmYesSeSource),
            TitleSeCue.ConfirmDeleteYes => ResolveSource(confirmDeleteYesSeSource),
            TitleSeCue.ConfirmOpen => ResolveSource(confirmOpenSeSource),
            TitleSeCue.AppQuit => ResolveSource(appQuitSeSource),
            _ => ResolveSource(null)
        };

        float cueVolume = cue switch
        {
            TitleSeCue.TransitionStart => transitionStartSeVolume,
            TitleSeCue.ConfigToggle => configToggleSeVolume,
            TitleSeCue.ConfigVolumeUp => configVolumeUpSeVolume,
            TitleSeCue.ConfigVolumeDown => configVolumeDownSeVolume,
            TitleSeCue.ConfigTabSwitch => configTabSwitchSeVolume,
            TitleSeCue.SlotDelete => slotDeleteSeVolume,
            TitleSeCue.ConfirmCancel => confirmCancelSeVolume,
            TitleSeCue.ConfirmYes => confirmYesSeVolume,
            TitleSeCue.ConfirmDeleteYes => confirmDeleteYesSeVolume,
            TitleSeCue.ConfirmOpen => confirmOpenSeVolume,
            TitleSeCue.AppQuit => appQuitSeVolume,
            _ => 1f
        };

        float unused;
        return PlayInternal(source, clip, baseVolume * cueVolume, out unused);
    }

    private static float ResolveSeGain()
    {
        return SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
    }

    private static bool FailPlay(out float playedClipLengthSeconds)
    {
        playedClipLengthSeconds = 0f;
        return false;
    }

    private bool PlayInternal(AudioSource source, AudioClip clip, float baseVolume, out float playedClipLengthSeconds)
    {
        playedClipLengthSeconds = 0f;
        source = ResolveSource(source);
        if (clip == null || source == null)
        {
            return false;
        }

        float finalVolume = Mathf.Clamp01(Mathf.Max(0f, baseVolume) * ResolveSeGain());
        if (finalVolume <= 0f)
        {
            return false;
        }

        source.PlayOneShot(clip, finalVolume);
        playedClipLengthSeconds = Mathf.Max(0f, clip.length);
        return true;
    }

    private AudioSource ResolveSource(AudioSource primary)
    {
        if (primary != null)
        {
            return primary;
        }

        if (defaultAudioSource != null)
        {
            return defaultAudioSource;
        }

        return GetComponent<AudioSource>();
    }
}
