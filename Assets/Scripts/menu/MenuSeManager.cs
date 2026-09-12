using UnityEngine;

public enum MenuSeCue
{
    TransitionStart = 0,
    TitleReturnTransition = 1,
    PlayManualToggle = 2
}

/// <summary>
/// menu_scene 内の SE をインスペクターで一元管理します。
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuSeManager : MonoBehaviour
{
    [Header("共通設定")]
    [SerializeField, Tooltip("既定のSE再生先AudioSource。各項目のAudioSourceが未設定時に使います。")]
    private AudioSource defaultAudioSource;

    [Header("シーン遷移SE")]
    [SerializeField, Tooltip("スタート／Game02 等の遷移開始時に鳴らすSE。")]
    private AudioClip transitionStartSeClip;
    [SerializeField, Tooltip("遷移SEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource transitionStartSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("遷移SEの基準音量。")]
    private float transitionStartSeVolume = 1f;

    [Header("タイトル戻りSE")]
    [SerializeField, Tooltip("TitleReturnButton 押下時に鳴らすSE（menu02 と同様に遷移SEとは別）。")]
    private AudioClip titleReturnSeClip;
    [SerializeField, Tooltip("タイトル戻りSEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource titleReturnSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("タイトル戻りSEの基準音量。")]
    private float titleReturnSeVolume = 1f;

    [Header("遊び方 開閉SE")]
    [SerializeField, Tooltip("PlayManualCanvas を開閉する時に鳴らすSE。")]
    private AudioClip playManualToggleSeClip;
    [SerializeField, Tooltip("遊び方 開閉SEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource playManualToggleSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("遊び方 開閉SEの基準音量。")]
    private float playManualToggleSeVolume = 1f;

    private void Awake()
    {
        if (defaultAudioSource == null)
        {
            defaultAudioSource = GetComponent<AudioSource>();
        }
    }

    public bool PlayByCue(MenuSeCue cue)
    {
        return cue switch
        {
            MenuSeCue.TransitionStart => PlayInternal(transitionStartSeSource, transitionStartSeClip, transitionStartSeVolume),
            MenuSeCue.TitleReturnTransition => PlayInternal(titleReturnSeSource, titleReturnSeClip, titleReturnSeVolume),
            MenuSeCue.PlayManualToggle => PlayInternal(playManualToggleSeSource, playManualToggleSeClip, playManualToggleSeVolume),
            _ => false
        };
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
        if (source == null || clip == null)
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
