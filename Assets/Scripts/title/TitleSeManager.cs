using UnityEngine;

public enum TitleSeCue
{
    TransitionStart = 0,
    ConfigToggle = 1,
    ConfigVolumeUp = 2,
    ConfigVolumeDown = 3
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

    private void Awake()
    {
        if (defaultAudioSource == null)
        {
            defaultAudioSource = GetComponent<AudioSource>();
        }
    }

    public bool PlayByCue(TitleSeCue cue)
    {
        return cue switch
        {
            TitleSeCue.TransitionStart => PlayInternal(transitionStartSeSource, transitionStartSeClip, transitionStartSeVolume),
            TitleSeCue.ConfigToggle => PlayInternal(configToggleSeSource, configToggleSeClip, configToggleSeVolume),
            TitleSeCue.ConfigVolumeUp => PlayInternal(configVolumeUpSeSource, configVolumeUpSeClip, configVolumeUpSeVolume),
            TitleSeCue.ConfigVolumeDown => PlayInternal(configVolumeDownSeSource, configVolumeDownSeClip, configVolumeDownSeVolume),
            _ => false
        };
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
            _ => ResolveSource(null)
        };

        float cueVolume = cue switch
        {
            TitleSeCue.TransitionStart => transitionStartSeVolume,
            TitleSeCue.ConfigToggle => configToggleSeVolume,
            TitleSeCue.ConfigVolumeUp => configVolumeUpSeVolume,
            TitleSeCue.ConfigVolumeDown => configVolumeDownSeVolume,
            _ => 1f
        };

        return PlayInternal(source, clip, baseVolume * cueVolume);
    }

    private static float ResolveSeGain()
    {
        return SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
    }

    private bool PlayInternal(AudioSource source, AudioClip clip, float baseVolume)
    {
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
