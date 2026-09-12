using UnityEngine;

/// <summary>
/// menu_scene 内の BGM をインスペクターで一元管理します。
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class MenuBgmManager : MonoBehaviour
{
    [Header("通常BGM")]
    [SerializeField, Tooltip("メニュー画面の通常BGM用 AudioSource。未設定時は無音扱いです。")]
    private AudioSource mainBgmAudioSource;
    [SerializeField, Tooltip("メニュー画面で通常再生するBGMクリップ。")]
    private AudioClip mainBgmClip;
    [SerializeField, Tooltip("通常BGMをループ再生するか。")]
    private bool loopMainBgm = true;

    private void Awake()
    {
        PrepareManagedAudioSources();
    }

    private void Start()
    {
        if (mainBgmClip != null)
        {
            PlayMainBgm();
        }
        else
        {
            ApplyBgmVolumeFromSettings();
        }
    }

    private void PrepareManagedAudioSources()
    {
        PrepareSingleSource(mainBgmAudioSource);
    }

    private static void PrepareSingleSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.Stop();
        source.clip = null;
        source.time = 0f;
    }

    public bool PlayMainBgm()
    {
        return PlayOnSource(mainBgmAudioSource, mainBgmClip, loopMainBgm);
    }

    public void ApplyBgmVolumeFromSettings()
    {
        float gain = ResolveBgmGain();
        if (mainBgmAudioSource != null)
        {
            mainBgmAudioSource.volume = gain;
        }
    }

    private bool PlayOnSource(AudioSource source, AudioClip clip, bool loop)
    {
        if (source == null || clip == null)
        {
            return false;
        }

        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = ResolveBgmGain();
        source.Play();

        return true;
    }

    private static float ResolveBgmGain()
    {
        if (SoundSettingsManager.Instance == null)
        {
            return 1f;
        }

        return Mathf.Max(0f, SoundSettingsManager.Instance.GetBgmGain01());
    }
}
