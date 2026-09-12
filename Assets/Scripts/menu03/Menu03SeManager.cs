using UnityEngine;

public enum Menu03SeCue
{
    TransitionStart = 0,
    PlayManualToggle = 1,
    TitleReturnTransition = 2,
    UgPurchaseButton = 3,
    UgClearRefund = 4
}

[DisallowMultipleComponent]
public sealed class Menu03SeManager : MonoBehaviour
{
    [Header("共通設定")]
    [SerializeField, Tooltip("既定のSE再生先AudioSource。各項目のAudioSourceが未設定時に使います。")]
    private AudioSource defaultAudioSource;

    [Header("遷移SE")]
    [SerializeField, Tooltip("Start/Continue 押下時に鳴らすSE。")]
    private AudioClip transitionStartSeClip;
    [SerializeField, Tooltip("遷移SEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource transitionStartSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("遷移SEの基準音量。")]
    private float transitionStartSeVolume = 1f;

    [Header("遊び方 開閉SE")]
    [SerializeField, Tooltip("PlayManualCanvas を開閉する時に鳴らすSE。")]
    private AudioClip playManualToggleSeClip;
    [SerializeField, Tooltip("遊び方 開閉SEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource playManualToggleSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("遊び方 開閉SEの基準音量。")]
    private float playManualToggleSeVolume = 1f;

    [Header("タイトル戻りSE")]
    [SerializeField, Tooltip("TitleReturnButton 押下時に鳴らすSE。")]
    private AudioClip titleReturnSeClip;
    [SerializeField, Tooltip("タイトル戻りSEを鳴らすAudioSource。未設定時は共通AudioSourceを使います。")]
    private AudioSource titleReturnSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("タイトル戻りSEの基準音量。")]
    private float titleReturnSeVolume = 1f;

    [Header("UG 購入ボタン SE")]
    [SerializeField, Tooltip("ButtonUG01〜05 共通の押下 SE。")]
    private AudioClip ugPurchaseButtonSeClip;
    [SerializeField, Tooltip("UG 購入 SE を鳴らす AudioSource。未設定時は共通 AudioSource。")]
    private AudioSource ugPurchaseButtonSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("UG 購入 SE の基準音量。")]
    private float ugPurchaseButtonSeVolume = 1f;

    [Header("UG 払い戻し SE")]
    [SerializeField, Tooltip("ButtonClear 押下時。")]
    private AudioClip ugClearRefundSeClip;
    [SerializeField, Tooltip("払い戻し SE を鳴らす AudioSource。未設定時は共通 AudioSource。")]
    private AudioSource ugClearRefundSeSource;
    [SerializeField, Range(0f, 1f), Tooltip("払い戻し SE の基準音量。")]
    private float ugClearRefundSeVolume = 1f;

    private void Awake()
    {
        if (defaultAudioSource == null)
        {
            defaultAudioSource = GetComponent<AudioSource>();
        }
    }

    public bool PlayByCue(Menu03SeCue cue)
    {
        return cue switch
        {
            Menu03SeCue.TransitionStart => PlayInternal(transitionStartSeSource, transitionStartSeClip, transitionStartSeVolume),
            Menu03SeCue.PlayManualToggle => PlayInternal(playManualToggleSeSource, playManualToggleSeClip, playManualToggleSeVolume),
            Menu03SeCue.TitleReturnTransition => PlayInternal(titleReturnSeSource, titleReturnSeClip, titleReturnSeVolume),
            Menu03SeCue.UgPurchaseButton => PlayInternal(ugPurchaseButtonSeSource, ugPurchaseButtonSeClip, ugPurchaseButtonSeVolume),
            Menu03SeCue.UgClearRefund => PlayInternal(ugClearRefundSeSource, ugClearRefundSeClip, ugClearRefundSeVolume),
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
