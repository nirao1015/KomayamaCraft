using UnityEngine;

/// <summary>
/// ConfigCanvas プレハブ共用の設定 SE。
/// クリップはタイトル <see cref="TitleSeManager"/> と同じものをプレハブに載せ、クラフトでも同じ設定を使う。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class ConfigSePlayer : MonoBehaviour
{
    [Header("タイトル TitleSeManager と同クリップを割り当て（プレハブ共通）")]
    [SerializeField] private AudioClip configTabSwitchSeClip;
    [SerializeField, Range(0f, 1f)] private float configTabSwitchSeVolume = 1f;

    [SerializeField] private AudioClip configVolumeUpSeClip;
    [SerializeField, Range(0f, 1f)] private float configVolumeUpSeVolume = 1f;

    [SerializeField] private AudioClip configVolumeDownSeClip;
    [SerializeField, Range(0f, 1f)] private float configVolumeDownSeVolume = 1f;

    [SerializeField] private AudioSource playbackSource;

    private void Awake()
    {
        if (playbackSource == null)
        {
            playbackSource = GetComponent<AudioSource>();
        }
    }

    public bool Play(TitleSeCue cue)
    {
        return cue switch
        {
            TitleSeCue.ConfigTabSwitch => PlayClip(configTabSwitchSeClip, configTabSwitchSeVolume),
            TitleSeCue.ConfigVolumeUp => PlayClip(configVolumeUpSeClip, configVolumeUpSeVolume),
            TitleSeCue.ConfigVolumeDown => PlayClip(configVolumeDownSeClip, configVolumeDownSeVolume),
            _ => false
        };
    }

    public static bool TryPlay(ConfigSePlayer player, TitleSeCue cue)
    {
        return player != null && player.Play(cue);
    }

    private bool PlayClip(AudioClip clip, float cueVolume)
    {
        if (clip == null)
        {
            return false;
        }

        if (playbackSource == null)
        {
            playbackSource = GetComponent<AudioSource>();
        }

        if (playbackSource == null)
        {
            return false;
        }

        float settingsGain = SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
        float finalVolume = Mathf.Clamp01(Mathf.Max(0f, cueVolume) * settingsGain);
        if (finalVolume <= 0f)
        {
            return false;
        }

        playbackSource.PlayOneShot(clip, finalVolume);
        return true;
    }

#if UNITY_EDITOR
    public void EditorApplyFromTitleSeManager(TitleSeManager titleSe)
    {
        if (titleSe == null)
        {
            return;
        }

        SerializedCopy(titleSe);
    }

    private void SerializedCopy(TitleSeManager titleSe)
    {
        var soSrc = new UnityEditor.SerializedObject(titleSe);
        var soDst = new UnityEditor.SerializedObject(this);
        CopyClip(soSrc, soDst, "configTabSwitchSeClip", "configTabSwitchSeVolume");
        CopyClip(soSrc, soDst, "configVolumeUpSeClip", "configVolumeUpSeVolume");
        CopyClip(soSrc, soDst, "configVolumeDownSeClip", "configVolumeDownSeVolume");
        soDst.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyClip(
        UnityEditor.SerializedObject src,
        UnityEditor.SerializedObject dst,
        string clipProp,
        string volumeProp)
    {
        dst.FindProperty(clipProp).objectReferenceValue =
            src.FindProperty(clipProp).objectReferenceValue;
        dst.FindProperty(volumeProp).floatValue =
            src.FindProperty(volumeProp).floatValue;
    }
#endif
}
