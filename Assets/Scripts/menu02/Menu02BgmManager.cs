using UnityEngine;

[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class Menu02BgmManager : MonoBehaviour
{
    [Header("BGM AudioSource")]
    [SerializeField, Tooltip("menu02 のBGM再生に使うAudioSource。未設定時は無音になります。")]
    private AudioSource mainBgmAudioSource;

    [Header("BGM Clips")]
    [SerializeField, Tooltip("menu02 で通常再生するBGM。")]
    private AudioClip mainBgmClip;

    [Header("Playback")]
    [SerializeField, Tooltip("通常BGMをループ再生するか。")]
    private bool loopMainBgm = true;

    private void Awake()
    {
        if (mainBgmAudioSource == null)
        {
            mainBgmAudioSource = GetComponent<AudioSource>();
        }

        PrepareSource(mainBgmAudioSource);
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

    public bool PlayMainBgm()
    {
        return PlayOnSource(mainBgmAudioSource, mainBgmClip, loopMainBgm);
    }

    public void StopMainBgm()
    {
        if (mainBgmAudioSource == null)
        {
            return;
        }

        mainBgmAudioSource.Stop();
        mainBgmAudioSource.clip = null;
    }

    public void ApplyBgmVolumeFromSettings()
    {
        if (mainBgmAudioSource == null)
        {
            return;
        }

        mainBgmAudioSource.volume = ResolveBgmGain();
    }

    private static void PrepareSource(AudioSource source)
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
