using UnityEngine;

/// <summary>
/// AudioSource 側の Play On Awake / 旧 Resource 参照だけで鳴るのを防ぎ、
/// mainBgmClip が設定されているときのみ <see cref="PlayMainBgm"/> で再生します。
/// </summary>
[DefaultExecutionOrder(-32000)]
public sealed class TitleBgmManager : MonoBehaviour
{
    [Header("BGM AudioSource")]
    [SerializeField, Tooltip("タイトル通常BGMの再生に使う AudioSource。未設定時は無音になります。")]
    private AudioSource mainBgmAudioSource;

    [Header("BGM Clips")]
    [SerializeField, Tooltip("タイトル画面で通常再生するBGM。")]
    private AudioClip mainBgmClip;

    [Header("Playback")]
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
    }

    /// <summary>
    /// Inspector に残ったクリップや Play On Awake による自動再生を抑止します。
    /// </summary>
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

    public void StopMainBgm()
    {
        if (mainBgmAudioSource != null)
        {
            mainBgmAudioSource.Stop();
            mainBgmAudioSource.clip = null;
        }
    }

    public bool IsMainBgmPlaying()
    {
        return mainBgmAudioSource != null && mainBgmAudioSource.isPlaying;
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
