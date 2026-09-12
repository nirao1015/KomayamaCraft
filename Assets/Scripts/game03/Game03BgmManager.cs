using UnityEngine;

[DisallowMultipleComponent]
public sealed class Game03BgmManager : MonoBehaviour
{
    private static Game03BgmManager instance;

    [Header("再生先")]
    [SerializeField, Tooltip("BGM を鳴らす AudioSource。未設定時はこのオブジェクトの AudioSource を使います。")]
    private AudioSource bgmAudioSource;

    [Header("BGM 音源")]
    [SerializeField, Tooltip("通常プレイ時に鳴らす BGM。未設定なら無音。")]
    private AudioClip mainBgmClip;
    [SerializeField, Tooltip("タイムクリア演出（発射以降）で鳴らす BGM。<c>Game03GameClearCeremonyController</c> から再生。未設定なら無音のまま。")]
    private AudioClip gameClearPresentationBgmClip;
    [SerializeField, Tooltip("クリア演出 BGM をループするか（短いジングルならオフ）。")]
    private bool gameClearPresentationBgmLoop = true;
    [SerializeField, Tooltip("ゲームオーバー演出で鳴らす BGM。<c>Game03GameOverPresentation</c> から再生。未設定なら無音。")]
    private AudioClip gameOverPresentationBgmClip;
    [SerializeField, Tooltip("ゲームオーバー演出 BGM をループするか。")]
    private bool gameOverPresentationBgmLoop = true;
    [SerializeField, Tooltip("UnitUG 演出で鳴らす BGM。未設定なら無音。")]
    private AudioClip unitUgPresentationBgmClip;
    [SerializeField, Tooltip("UnitUG 演出 BGM をループするか。")]
    private bool unitUgPresentationBgmLoop = true;
    [SerializeField, Tooltip("BossUG 演出で鳴らす BGM。未設定なら UnitUG と同じクリップを使用。")]
    private AudioClip bossUgPresentationBgmClip;
    [SerializeField, Tooltip("BossUG 演出 BGM をループするか。")]
    private bool bossUgPresentationBgmLoop = true;

    [Header("再生設定")]
    [SerializeField, Tooltip("再生開始時にループするか。")]
    private bool loop = true;
    [SerializeField, Tooltip("シーン開始時に mainBgmClip を再生するか。")]
    private bool playMainOnStart = true;

    public static Game03BgmManager TryGet()
    {
        if (instance != null)
        {
            return instance;
        }

        return FindObjectOfType<Game03BgmManager>(true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveAudioSourceIfNeeded();
        PrepareAudioSource();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        if (playMainOnStart)
        {
            PlayMainBgm();
        }
    }

    private void Update()
    {
        ApplyBgmGain();
    }

    public bool PlayMainBgm()
    {
        return PlayByClip(mainBgmClip);
    }

    /// <summary>クリア演出の発射タイミング以降に鳴らす BGM（通常 BGM は事前に <c>StopBgm</c> されている想定）。</summary>
    public bool PlayGameClearPresentationBgm()
    {
        ResolveAudioSourceIfNeeded();
        if (gameClearPresentationBgmClip == null || bgmAudioSource == null)
        {
            return false;
        }

        bgmAudioSource.clip = gameClearPresentationBgmClip;
        bgmAudioSource.loop = gameClearPresentationBgmLoop;
        ApplyBgmGain();
        bgmAudioSource.Play();
        return true;
    }

    /// <summary>UnitUG 演出 BGM（通常 BGM は事前に <c>StopBgm</c> されている想定）。</summary>
    public bool PlayUnitUgPresentationBgm()
    {
        ResolveAudioSourceIfNeeded();
        if (unitUgPresentationBgmClip == null || bgmAudioSource == null)
        {
            return false;
        }

        bgmAudioSource.clip = unitUgPresentationBgmClip;
        bgmAudioSource.loop = unitUgPresentationBgmLoop;
        ApplyBgmGain();
        bgmAudioSource.Play();
        return true;
    }

    /// <summary>BossUG 演出 BGM。未設定時は UnitUG クリップを使用。</summary>
    public bool PlayBossUgPresentationBgm()
    {
        AudioClip clip = bossUgPresentationBgmClip != null ? bossUgPresentationBgmClip : unitUgPresentationBgmClip;
        ResolveAudioSourceIfNeeded();
        if (clip == null || bgmAudioSource == null)
        {
            return false;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = bossUgPresentationBgmClip != null ? bossUgPresentationBgmLoop : unitUgPresentationBgmLoop;
        ApplyBgmGain();
        bgmAudioSource.Play();
        return true;
    }

    /// <summary>ゲームオーバー演出 BGM（通常 BGM は事前に <c>StopBgm</c> されている想定）。</summary>
    public bool PlayGameOverPresentationBgm()
    {
        ResolveAudioSourceIfNeeded();
        if (gameOverPresentationBgmClip == null || bgmAudioSource == null)
        {
            return false;
        }

        bgmAudioSource.clip = gameOverPresentationBgmClip;
        bgmAudioSource.loop = gameOverPresentationBgmLoop;
        ApplyBgmGain();
        bgmAudioSource.Play();
        return true;
    }

    public AudioClip CurrentClip
    {
        get
        {
            ResolveAudioSourceIfNeeded();
            return bgmAudioSource != null ? bgmAudioSource.clip : null;
        }
    }

    public bool PlayClip(AudioClip clip)
    {
        return PlayByClip(clip);
    }

    public void StopBgm()
    {
        ResolveAudioSourceIfNeeded();
        if (bgmAudioSource == null)
        {
            return;
        }

        bgmAudioSource.Stop();
        bgmAudioSource.clip = null;
        bgmAudioSource.time = 0f;
    }

    private bool PlayByClip(AudioClip clip)
    {
        ResolveAudioSourceIfNeeded();
        if (clip == null || bgmAudioSource == null)
        {
            Debug.LogError("Game03BgmManager無音");
            return false;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = loop;
        ApplyBgmGain();
        bgmAudioSource.Play();
        return true;
    }

    private void ResolveAudioSourceIfNeeded()
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = GetComponent<AudioSource>();
        }
    }

    private void PrepareAudioSource()
    {
        if (bgmAudioSource == null)
        {
            return;
        }

        bgmAudioSource.playOnAwake = false;
    }

    private void ApplyBgmGain()
    {
        if (bgmAudioSource == null)
        {
            return;
        }

        float bgmGain = SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetBgmGain01())
            : 1f;
        bgmAudioSource.volume = Mathf.Clamp01(bgmGain);
    }
}
