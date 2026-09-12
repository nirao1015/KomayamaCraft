using UnityEngine;

/// <summary>
/// game_stage_scene の SE 管理専用。
/// 実装してよい: SE クリップ管理、再生 AudioSource の切り替え、再生開始/停止。
/// 実装してはいけない: BGM 管理、Canvas 開閉、シーン遷移。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game01SeManager : MonoBehaviour
{
    [Header("参照: AudioSource")]
    [SerializeField, Tooltip("共通 SE を再生する AudioSource。未設定時は無音。")]
    private AudioSource commonSeAudioSource;
    [SerializeField, Tooltip("炎演出ループ SE 専用 AudioSource。未設定時は無音。")]
    private AudioSource fireLoopSeAudioSource;

    [Header("SE音源: ゲーム進行")]
    [SerializeField, Tooltip("進入禁止エリアに触れた時の SE。未設定時は鳴らしません。")]
    private AudioClip noEntrySe;
    [SerializeField, Tooltip("プレイヤー被弾時の SE。未設定時は鳴らしません。")]
    private AudioClip playerDamageSe;
    [SerializeField, Tooltip("プレイヤー反発時の SE。未設定時は鳴らしません。")]
    private AudioClip playerReboundSe;
    [SerializeField, Tooltip("ゲームオーバー Retry ボタン押下時の SE。未設定時は鳴らしません。")]
    private AudioClip gameOverRetryButtonSe;
    [SerializeField, Tooltip("ゲームオーバー GiveUp ボタン押下時の SE。未設定時は鳴らしません。")]
    private AudioClip gameOverGiveUpButtonSe;

    [Header("SE音源: 炎演出")]
    [SerializeField, Tooltip("炎演出のループ SE。未設定時は鳴らしません。")]
    private AudioClip fireLoopSe;

    public void PlayNoEntrySe()
    {
        PlayOneShot(commonSeAudioSource, noEntrySe, GetSeGain01Safe());
    }

    public void PlayPlayerDamageSe()
    {
        PlayOneShot(commonSeAudioSource, playerDamageSe, GetSeGain01Safe());
    }

    public void PlayPlayerReboundSe()
    {
        PlayOneShot(commonSeAudioSource, playerReboundSe, GetSeGain01Safe());
    }

    public void PlayGameOverRetryButtonSe()
    {
        PlayOneShot(commonSeAudioSource, gameOverRetryButtonSe, GetSeGain01Safe());
    }

    public void PlayGameOverGiveUpButtonSe()
    {
        PlayOneShot(commonSeAudioSource, gameOverGiveUpButtonSe, GetSeGain01Safe());
    }

    public void PlayFireLoopSe()
    {
        if (fireLoopSeAudioSource == null || fireLoopSe == null)
        {
            return;
        }

        bool shouldRestart =
            !fireLoopSeAudioSource.isPlaying ||
            fireLoopSeAudioSource.clip != fireLoopSe ||
            !fireLoopSeAudioSource.loop;

        fireLoopSeAudioSource.playOnAwake = false;
        fireLoopSeAudioSource.loop = true;
        fireLoopSeAudioSource.clip = fireLoopSe;
        fireLoopSeAudioSource.volume = 0f;
        if (shouldRestart)
        {
            fireLoopSeAudioSource.Stop();
            fireLoopSeAudioSource.Play();
        }
    }

    public void SetFireLoopSeVolume01(float normalizedVolume)
    {
        if (fireLoopSeAudioSource == null || fireLoopSeAudioSource.clip == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedVolume);
        fireLoopSeAudioSource.volume = clamped * GetSeGain01Safe();
    }

    public void StopFireLoopSe()
    {
        if (fireLoopSeAudioSource == null)
        {
            return;
        }

        if (fireLoopSeAudioSource.isPlaying)
        {
            fireLoopSeAudioSource.Stop();
        }
    }

    private static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private static float GetSeGain01Safe()
    {
        SoundSettingsManager manager = SoundSettingsManager.Instance;
        return manager != null ? manager.GetSeGain01() : 1f;
    }
}
