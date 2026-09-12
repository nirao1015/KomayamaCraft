using UnityEngine;

/// <summary>
/// game_stage_scene の BGM 管理専用。
/// 実装してよい: BGM 音源管理、再生 AudioSource 管理、再生/停止/音量反映。
/// 実装してはいけない: SE 管理、Canvas 開閉、シーン遷移。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game01BgmManager : MonoBehaviour
{
    [Header("参照: AudioSource")]
    [SerializeField, Tooltip("ステージ中 BGM を再生する AudioSource。未設定時は無音。")]
    private AudioSource stageBgmAudioSource;

    [Header("BGM音源")]
    [SerializeField, Tooltip("ステージ中 BGM。未設定時は既存クリップを維持します。")]
    private AudioClip stageBgmClip;
    [SerializeField, Tooltip("ステージクリア時 BGM。未設定時は再生しません。")]
    private AudioClip stageClearBgmClip;
    [SerializeField, Tooltip("ゲームオーバー BGM。未設定時は再生しません。")]
    private AudioClip gameOverBgmClip;

    public void ApplyStageBgmVolumeFromSettings()
    {
        if (stageBgmAudioSource == null)
        {
            return;
        }

        stageBgmAudioSource.volume = GetBgmGain01Safe();
    }

    public void PlayStageBgmIfConfigured()
    {
        if (stageBgmAudioSource == null)
        {
            return;
        }

        if (stageBgmClip != null)
        {
            stageBgmAudioSource.clip = stageBgmClip;
        }

        if (stageBgmAudioSource.clip == null)
        {
            return;
        }

        stageBgmAudioSource.loop = true;
        stageBgmAudioSource.volume = GetBgmGain01Safe();
        if (!stageBgmAudioSource.isPlaying)
        {
            stageBgmAudioSource.Play();
        }
    }

    public void StopStageBgm()
    {
        if (stageBgmAudioSource != null && stageBgmAudioSource.isPlaying)
        {
            stageBgmAudioSource.Stop();
        }
    }

    public void PlayStageClearBgm()
    {
        if (stageBgmAudioSource == null || stageClearBgmClip == null)
        {
            return;
        }

        stageBgmAudioSource.loop = false;
        stageBgmAudioSource.clip = stageClearBgmClip;
        stageBgmAudioSource.volume = GetBgmGain01Safe();
        stageBgmAudioSource.Play();
    }

    public void PlayGameOverBgm()
    {
        if (stageBgmAudioSource == null || gameOverBgmClip == null)
        {
            return;
        }

        stageBgmAudioSource.loop = true;
        stageBgmAudioSource.clip = gameOverBgmClip;
        stageBgmAudioSource.volume = GetBgmGain01Safe();
        stageBgmAudioSource.Play();
    }

    private static float GetBgmGain01Safe()
    {
        SoundSettingsManager manager = SoundSettingsManager.Instance;
        return manager != null ? manager.GetBgmGain01() : 1f;
    }
}
