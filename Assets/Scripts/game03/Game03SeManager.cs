using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

/// <summary>game03 の UI／遷移SE。<see cref="Game03TransitionManager"/> と <see cref="Game03LevelUpManager"/> が使用。</summary>
public enum Game03SeCue
{
    TransitionStart = 0,
    TitleReturnButtonClick = 1,
    LvUpPanelOpen = 2,
    LvUpPanelSelect = 3,
    PausePanelOpen = 4,
    PausePanelResume = 5,
    GameOver = 6,
    GameOverPanelAdvance = 7
}

public enum Game03SeCategory
{
    UI = 0,
    ItemPickup = 1,
    PlayerDamaged = 2,
    ExpPickup = 3,
    WeaponSwing = 4,
    EnemyDamaged = 5
}

/// <summary>SE 再生プール区分。<see cref="Game03SeManager"/> がボイスごとに独立リストを持ち、空きのみで再生する。</summary>
public enum Game03SeVoiceBucket
{
    Ui = 0,
    Performance = 1,
    Weapon = 2,
    Enemy = 3
}

[DisallowMultipleComponent]
public sealed class Game03SeManager : MonoBehaviour
{
    private static Game03SeManager instance;
    [SerializeField] private Game03Manager game03Manager;

    [Header("再生先")]
    [SerializeField, Tooltip("4 プールすべて空のときのみ Ui プールへ 1 本追加されるフォールバック。通常は各プールに AudioSource を並べる。")]
    private AudioSource defaultAudioSource;

    [Header("再生プール（空きボイスのみ使用・全占有時は再生しない）")]
    [FormerlySerializedAs("pooledAudioSources")]
    [SerializeField, Tooltip("UI／遷移／LvUp／ポーズ／カーゴ等")]
    private List<AudioSource> uiVoiceAudioSources = new List<AudioSource>();
    [SerializeField, Tooltip("カットシーン・Pod 降下演出など")]
    private List<AudioSource> performanceVoiceAudioSources = new List<AudioSource>();
    [SerializeField, Tooltip("武器 SE")]
    private List<AudioSource> weaponVoiceAudioSources = new List<AudioSource>();
    [SerializeField, Tooltip("敵ヒット・撃破など")]
    private List<AudioSource> enemyVoiceAudioSources = new List<AudioSource>();

    [SerializeField, Tooltip("PlayCutsceneByCategory が参照するプール（公開API・現状シーンからは未使用のことが多い）。")]
    private Game03SeVoiceBucket cutsceneByCategoryVoiceBucket = Game03SeVoiceBucket.Performance;

    [Header("UI／遷移SE（Game03TransitionManager / LevelUpManager が参照）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket uiTransitionSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField, Tooltip("画面遷移開始時（TransitionStart）。")]
    private AudioClip transitionStartSeClip;
    [SerializeField, Range(0f, 1f)] private float transitionStartSeVolume = 1f;

    [SerializeField, Tooltip("TitleReturnButton 押下時。")]
    private AudioClip titleReturnButtonClickSeClip;
    [SerializeField, Range(0f, 1f)] private float titleReturnButtonClickSeVolume = 1f;

    [Header("LvUpPanel SE")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket lvUpPanelSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField] private AudioClip lvUpPanelOpenSeClip;
    [SerializeField, Range(0f, 1f)] private float lvUpPanelOpenSeVolume = 1f;
    [SerializeField] private AudioClip lvUpPanelSelectSeClip;
    [SerializeField, Range(0f, 1f)] private float lvUpPanelSelectSeVolume = 1f;

    [Header("PausePanel SE（Tab／ButtonStop トグル・パネル内再生ボタン。EnterPause の直後／Resume の解除直後）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket pausePanelSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField] private AudioClip pausePanelOpenSeClip;
    [SerializeField, Range(0f, 1f)] private float pausePanelOpenSeVolume = 1f;
    [SerializeField] private AudioClip pausePanelResumeSeClip;
    [SerializeField, Range(0f, 1f)] private float pausePanelResumeSeVolume = 1f;

    [Header("ゲームオーバー演出 SE")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket gameOverPresentationSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField] private AudioClip gameOverSeClip;
    [SerializeField, Range(0f, 1f)] private float gameOverSeVolume = 1f;
    [SerializeField] private AudioClip gameOverPanelAdvanceSeClip;
    [SerializeField, Range(0f, 1f)] private float gameOverPanelAdvanceSeVolume = 1f;

    [Header("ユニット被弾SE（敵接触・ループ）")]
    [SerializeField, Tooltip("被弾専用 AudioSource（プール外・1本固定）。PlayOnAwake OFF・Loop ON 推奨。")]
    private AudioSource playerUnitContactDamageLoopVoice;
    [SerializeField, Tooltip("接触中にループ再生するクリップ。未設定なら無音。")]
    private AudioClip playerUnitContactDamageSeClip;
    [SerializeField, Range(0f, 1f)] private float playerUnitContactDamageSeVolume = 1f;
    [SerializeField, Min(0f), Tooltip("接触が途切れても、この秒数（unscaled）は再生を続ける。0 で即停止。")]
    private float playerUnitContactDamageLoopMinPlaySeconds = 0.1f;

    [Header("敵撃破SE")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket enemyDefeatSeVoiceBucket = Game03SeVoiceBucket.Enemy;
    [SerializeField] private AudioClip enemyDefeatNormalSeClip;
    [SerializeField, Range(0f, 1f)] private float enemyDefeatNormalSeVolume = 1f;
    [SerializeField] private AudioClip enemyDefeatBossSeClip;
    [SerializeField, Range(0f, 1f)] private float enemyDefeatBossSeVolume = 1f;

    [Header("敵に武器ヒット時SE（離散ヒットのみ。continuousHit の継続ダメージでは鳴らさない）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket enemyWeaponHitSeVoiceBucket = Game03SeVoiceBucket.Enemy;
    [SerializeField] private AudioClip enemyWeaponHitNormalSeClip;
    [SerializeField, Range(0f, 1f)] private float enemyWeaponHitNormalSeVolume = 1f;
    [SerializeField] private AudioClip enemyWeaponHitBossSeClip;
    [SerializeField, Range(0f, 1f)] private float enemyWeaponHitBossSeVolume = 1f;
    [SerializeField, Min(0f), Tooltip("一般／ボスそれぞれ、同一ヒットSEをこの秒数以内に再再生しない（0.033 ≈ 60fps で約2フレーム）。")]
    private float enemyWeaponHitSeMinIntervalSeconds = 0.033f;

    [Header("敵出現（フェーズ・中BOSS湧き 種別51）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket midBossSpawnWarningSeVoiceBucket = Game03SeVoiceBucket.Performance;
    [SerializeField] private AudioClip midBossSpawnWarningSeClip;
    [SerializeField, Range(0f, 1f)] private float midBossSpawnWarningSeVolume = 1f;

    [Header("敵出現（フェーズ・BOSS湧き 種別61）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket finalBossSpawnWarningSeVoiceBucket = Game03SeVoiceBucket.Performance;
    [SerializeField] private AudioClip finalBossSpawnWarningSeClip;
    [SerializeField, Range(0f, 1f)] private float finalBossSpawnWarningSeVolume = 1f;

    [Header("Pod 降下演出 SE（カットシーン中。イントロは緩降下開始で再生し、急降下直前にフェードアウト）")]
    [SerializeField, Tooltip("このグループのクリップ・イントロ長押し再生が使用するプール。")]
    private Game03SeVoiceBucket podSequenceSeVoiceBucket = Game03SeVoiceBucket.Performance;
    [SerializeField] private AudioClip podDescentIntroSeClip;
    [SerializeField, Range(0f, 1f)] private float podDescentIntroVolume = 1f;
    [SerializeField, Tooltip("ON のときイントロ SE を緩降下中ループ（クリップが短い場合）。")]
    private bool podDescentIntroLoopDuringSlowFall = true;
    [SerializeField, Min(0.0001f), Tooltip("急降下開始直前にイントロ SE をこの秒数で音量 0 へ下げて停止する。")]
    private float podDescentIntroFadeOutSeconds = 0.05f;
    [SerializeField] private AudioClip podFastFallGroundImpactSeClip;
    [SerializeField, Range(0f, 1f)] private float podFastFallGroundImpactSeVolume = 1f;
    [SerializeField, Tooltip("WaitUnitAppearDelay 終了時に PodC1 のアルファが 1 になったタイミング。")]
    private AudioClip podC1ReachFullAlphaSeClip;
    [SerializeField, Range(0f, 1f)] private float podC1ReachFullAlphaSeVolume = 1f;
    [SerializeField, Tooltip("firstWalkOffsetY への移動（RunUnitMove 開始）時。")]
    private AudioClip podFirstWalkStartSeClip;
    [SerializeField, Range(0f, 1f)] private float podFirstWalkStartSeVolume = 1f;
    [SerializeField, Tooltip("secondWalkOffsetY への移動開始時。")]
    private AudioClip podSecondWalkStartSeClip;
    [SerializeField, Range(0f, 1f)] private float podSecondWalkStartSeVolume = 1f;

    [Header("エマージェンシーカーゴ（ItemCargo が画面上から落下開始したとき）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket emergencyCargoSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField] private AudioClip emergencyCargoFallStartSeClip;
    [SerializeField, Range(0f, 1f)] private float emergencyCargoFallStartSeVolume = 1f;

    [Header("ゲームクリア演出 SE（ポーズ中も再生）")]
    [SerializeField, Tooltip("レーザー／船横断で使用するプール。")]
    private Game03SeVoiceBucket gameClearCeremonySeVoiceBucket = Game03SeVoiceBucket.Performance;
    [SerializeField] private AudioClip gameClearLaserSeClip;
    [SerializeField, Range(0f, 1f)] private float gameClearLaserSeVolume = 1f;
    [SerializeField] private AudioClip gameClearShipCrossSeClip;
    [SerializeField, Range(0f, 1f)] private float gameClearShipCrossSeVolume = 1f;
    [SerializeField] private AudioClip gameClearLaunchSeClip;
    [SerializeField, Range(0f, 1f)] private float gameClearLaunchSeVolume = 1f;

    [Header("武器SE（仕様: spec/game03/武器ごとのUG.txt §Game03SeManager）")]
    [SerializeField, Tooltip("Weapon01〜07・Weapon02 着地など、このブロック全体が使用するプール。")]
    private Game03SeVoiceBucket weaponSeVoiceBucket = Game03SeVoiceBucket.Weapon;
    [Header("Weapon01 鎌")]
    [SerializeField, Tooltip("アタック回数ごと・振り開始時に鳴らす。")]
    private AudioClip weapon01SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon01SeVolume = 1f;

    [Header("Weapon02 燃焼粘液")]
    [SerializeField, Tooltip("クールダウン開始時・攻撃開始に1回（アタック数に関わらず）。")]
    private AudioClip weapon02SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon02SeVolume = 1f;
    [SerializeField, Tooltip("投射体が地面に着弾してフィールド化するとき（投射体ごと）。")]
    private AudioClip weapon02GroundImpactSeClip;
    [SerializeField, Range(0f, 1f)] private float weapon02GroundImpactSeVolume = 1f;

    [Header("Weapon03 黒球")]
    [SerializeField, Tooltip("軌道生成開始時に1回（球の個数に関わらず）。")]
    private AudioClip weapon03SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon03SeVolume = 1f;

    [Header("Weapon04 連撃")]
    [SerializeField, Tooltip("チェーンのヒットごと（Game03WeaponManager が再生）。")]
    private AudioClip weapon04SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon04SeVolume = 1f;

    [Header("Weapon05 針")]
    [SerializeField, Tooltip("攻撃グループ（最大5本単位の発射）ごとに1回。")]
    private AudioClip weapon05SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon05SeVolume = 1f;

    [Header("Weapon06 ドローン")]
    [SerializeField, Tooltip("ドローン1体スポーン開始ごと（アタック数ぶん）。")]
    private AudioClip weapon06SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon06SeVolume = 1f;
    [SerializeField, Min(0f), Tooltip("この秒数フル音量で鳴らしたあとフェードアウト開始（例: 0.8）。")]
    private float weapon06PlaybackHoldSeconds = 0.8f;
    [SerializeField, Min(0f), Tooltip("ホールド終了後、音量を0まで落として停止する秒数。短いほど急峻なフェードアウト。")]
    private float weapon06FadeOutSeconds = 0.08f;

    [Header("Weapon07 菌糸")]
    [SerializeField, Tooltip("斧1投げごと（アタック数ぶん）。")]
    private AudioClip weapon07SeClip;
    [SerializeField, Range(0f, 1f)] private float weapon07SeVolume = 1f;

    [Header("Consumable item pickup SE（救急箱／爆弾／スマホ）")]
    [SerializeField, Tooltip("このグループのクリップが使用するプール。")]
    private Game03SeVoiceBucket consumableItemPickupSeVoiceBucket = Game03SeVoiceBucket.Ui;
    [SerializeField] private AudioClip itemMedkitPickupSeClip;
    [SerializeField, Range(0f, 1f)] private float itemMedkitPickupSeVolume = 1f;
    [SerializeField] private AudioClip itemBombPickupSeClip;
    [SerializeField, Range(0f, 1f)] private float itemBombPickupSeVolume = 1f;
    [SerializeField] private AudioClip itemPhonePickupSeClip;
    [SerializeField, Range(0f, 1f)] private float itemPhonePickupSeVolume = 1f;

    private readonly List<AudioSource> runtimeUiVoices = new List<AudioSource>(4);
    private readonly List<AudioSource> runtimePerformanceVoices = new List<AudioSource>(4);
    private readonly List<AudioSource> runtimeWeaponVoices = new List<AudioSource>(4);
    private readonly List<AudioSource> runtimeEnemyVoices = new List<AudioSource>(4);

    private float lastEnemyWeaponHitNormalSeGameplayTime = -999f;
    private float lastEnemyWeaponHitBossSeGameplayTime = -999f;

    private sealed class Weapon06FadeVoice
    {
        public AudioSource Source;
        public float PeakVolume01;
        public double PlayStartDspTime;
        public float HoldSeconds;
        public float FadeOutSeconds;
    }

    private readonly List<Weapon06FadeVoice> weapon06FadeVoices = new List<Weapon06FadeVoice>(4);

    private AudioSource podDescentIntroVoice;
    private float podDescentIntroPeakVolume01 = 1f;

    private float playerUnitContactDamageLoopSegmentStartUnscaledTime = -1f;
    private bool playerUnitContactDamageLoopReleasePending;

    public static Game03SeManager TryGet()
    {
        if (instance != null)
        {
            return instance;
        }

        return FindObjectOfType<Game03SeManager>(true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (defaultAudioSource == null)
        {
            defaultAudioSource = GetComponent<AudioSource>();
        }

        InitializeVoiceBuckets();
        EnsurePlayerUnitContactDamageLoopVoiceConfigured();
    }

    private static bool TryComputeSeVolume01(float baseVolume, out float volume01)
    {
        float seGain = SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
        volume01 = Mathf.Clamp01(Mathf.Max(0f, baseVolume) * seGain);
        return volume01 > 0f;
    }

    private bool PlayClipOnConfiguredBucket(Game03SeVoiceBucket bucket, AudioClip clip, float baseVolume01)
    {
        if (clip == null || !TryComputeSeVolume01(baseVolume01, out float volume))
        {
            return false;
        }

        return PlayOneShotOnBucket(bucket, clip, volume);
    }

    /// <summary>指定プールの空きボイスのみで PlayOneShot（全占有時は再生しない）。</summary>
    private bool PlayOneShotOnBucket(Game03SeVoiceBucket bucket, AudioClip clip, float volume01)
    {
        if (clip == null || volume01 <= 0f)
        {
            return false;
        }

        AudioSource src = TryAcquireIdleVoice(bucket);
        if (src == null)
        {
            // PlayOneShot は同一 AudioSource 上で重ね再生できる。isPlaying 判定だけでは「空き無し」になりやすい。
            src = TryAcquireAnyVoice(bucket);
        }

        if (src == null)
        {
            return false;
        }

        src.PlayOneShot(clip, volume01);
        return true;
    }

    private static Game03SeVoiceBucket BucketForCategory(Game03SeCategory category)
    {
        return category switch
        {
            Game03SeCategory.WeaponSwing => Game03SeVoiceBucket.Weapon,
            Game03SeCategory.EnemyDamaged => Game03SeVoiceBucket.Enemy,
            _ => Game03SeVoiceBucket.Ui
        };
    }

    private void Update()
    {
        if (weapon06FadeVoices.Count > 0)
        {
            TickWeapon06FadeVoices();
        }

        if (playerUnitContactDamageLoopReleasePending)
        {
            TickPlayerUnitContactDamageLoopRelease();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool PlayByCue(Game03SeCue cue)
    {
        if (game03Manager != null && !AllowsCueDuringPauseAwareGameplayGate(cue))
        {
            return false;
        }

        return PlayCueCore(cue);
    }

    /// <summary>ポーズ中でも鳴らす UI／演出 SE（ゲームオーバー・リザルト遷移など）。</summary>
    public bool PlayPresentationCue(Game03SeCue cue)
    {
        return PlayCueCore(cue);
    }

    /// <summary>
    /// LvUp は <see cref="Game03LevelUpManager"/> が先にポーズしてからパネル表示／選択確定するため、
    /// <see cref="Game03Manager.CanPlayGameplaySe"/> が false のまま SE が呼ばれる。これらだけポーズ中も許可する。
    /// </summary>
    private bool AllowsCueDuringPauseAwareGameplayGate(Game03SeCue cue)
    {
        if (game03Manager == null || game03Manager.CanPlayGameplaySe)
        {
            return true;
        }

        return cue == Game03SeCue.LvUpPanelOpen
            || cue == Game03SeCue.LvUpPanelSelect
            || cue == Game03SeCue.PausePanelOpen
            || cue == Game03SeCue.PausePanelResume
            || cue == Game03SeCue.TransitionStart
            || cue == Game03SeCue.TitleReturnButtonClick;
    }

    public bool PlayCutsceneCue(Game03SeCue cue)
    {
        if (game03Manager != null && !game03Manager.CanPlayCutsceneSe)
        {
            return false;
        }

        return PlayCueCore(cue);
    }

    public void StopAllSeImmediately()
    {
        podDescentIntroVoice = null;

        StopAllInList(runtimeUiVoices);
        StopAllInList(runtimePerformanceVoices);
        StopAllInList(runtimeWeaponVoices);
        StopAllInList(runtimeEnemyVoices);

        StopIfPlaying(defaultAudioSource);
        StopPlayerUnitContactDamageLoopInternal();
        weapon06FadeVoices.Clear();
    }

    /// <summary>Pod 緩降下用イントロ。急降下前に <see cref="FadeOutCutscenePodDescentIntroAndWait"/> を yield すること。</summary>
    public bool TryPlayCutscenePodDescentIntroSe()
    {
        if (game03Manager != null && !game03Manager.CanPlayCutsceneSe)
        {
            return false;
        }

        if (podDescentIntroSeClip == null)
        {
            return false;
        }

        ReleasePodDescentIntroVoiceNoStopAll();

        float seGain = SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
        float peak = Mathf.Clamp01(Mathf.Max(0f, podDescentIntroVolume) * seGain);
        if (peak <= 0f)
        {
            return false;
        }

        AudioSource src = TryAcquireIdleVoice(podSequenceSeVoiceBucket);
        if (src == null)
        {
            return false;
        }

        RemoveWeapon06FadeVoicesUsingSource(src);
        StopIfPlaying(src);
        src.clip = podDescentIntroSeClip;
        src.loop = podDescentIntroLoopDuringSlowFall;
        src.volume = peak;
        src.time = 0f;
        src.Play();
        podDescentIntroVoice = src;
        podDescentIntroPeakVolume01 = peak;
        return true;
    }

    public float PodDescentIntroFadeOutSeconds => Mathf.Max(0.0001f, podDescentIntroFadeOutSeconds);

    public IEnumerator FadeOutCutscenePodDescentIntroAndWait(float fadeOutSeconds)
    {
        if (podDescentIntroVoice == null)
        {
            yield break;
        }

        AudioSource src = podDescentIntroVoice;
        float peak = Mathf.Max(0f, src.volume);
        if (peak <= 0.0001f)
        {
            peak = podDescentIntroPeakVolume01;
        }

        float dur = Mathf.Max(0.0001f, fadeOutSeconds);
        float t = 0f;
        while (t < dur && src != null && src.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            src.volume = Mathf.Lerp(peak, 0f, k);
            yield return null;
        }

        if (src != null)
        {
            StopIfPlaying(src);
            src.volume = 1f;
        }

        if (podDescentIntroVoice == src)
        {
            podDescentIntroVoice = null;
        }
    }

    public bool PlayCutscenePodFastFallGroundImpactSe()
    {
        return PlayCutscenePodOneShot(podFastFallGroundImpactSeClip, podFastFallGroundImpactSeVolume);
    }

    public bool PlayCutscenePodC1ReachFullAlphaSe()
    {
        return PlayCutscenePodOneShot(podC1ReachFullAlphaSeClip, podC1ReachFullAlphaSeVolume);
    }

    public bool PlayCutscenePodFirstWalkStartSe()
    {
        return PlayCutscenePodOneShot(podFirstWalkStartSeClip, podFirstWalkStartSeVolume);
    }

    public bool PlayCutscenePodSecondWalkStartSe()
    {
        return PlayCutscenePodOneShot(podSecondWalkStartSeClip, podSecondWalkStartSeVolume);
    }

    /// <summary><see cref="Game03EmergencyCargoDropController"/> が落下演出を開始した直後。</summary>
    public bool PlayEmergencyCargoFallStartSe()
    {
        if (emergencyCargoFallStartSeClip == null)
        {
            return false;
        }

        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(emergencyCargoSeVoiceBucket, emergencyCargoFallStartSeClip, emergencyCargoFallStartSeVolume);
    }

    /// <summary>ゲームクリアのレーザー発射 SE。<see cref="Game03Manager.CanPlayGameplaySe"/> は見ない。</summary>
    public bool PlayGameClearLaserSe()
    {
        return PlayClipOnConfiguredBucket(gameClearCeremonySeVoiceBucket, gameClearLaserSeClip, gameClearLaserSeVolume);
    }

    /// <summary>ゲームクリアの宇宙船横断開始 SE。</summary>
    public bool PlayGameClearShipCrossSe()
    {
        return PlayClipOnConfiguredBucket(gameClearCeremonySeVoiceBucket, gameClearShipCrossSeClip, gameClearShipCrossSeVolume);
    }

    /// <summary>ゲームクリアの発射／テキスト表示タイミング用 SE。</summary>
    public bool PlayGameClearLaunchSe()
    {
        return PlayClipOnConfiguredBucket(gameClearCeremonySeVoiceBucket, gameClearLaunchSeClip, gameClearLaunchSeVolume);
    }

    private bool PlayCutscenePodOneShot(AudioClip clip, float baseVolume01)
    {
        if (game03Manager != null && !game03Manager.CanPlayCutsceneSe)
        {
            return false;
        }

        if (clip == null)
        {
            return false;
        }

        if (!TryComputeSeVolume01(baseVolume01, out float vol))
        {
            return false;
        }

        return PlayOneShotOnBucket(podSequenceSeVoiceBucket, clip, vol);
    }

    private void ReleasePodDescentIntroVoiceNoStopAll()
    {
        if (podDescentIntroVoice == null)
        {
            return;
        }

        AudioSource src = podDescentIntroVoice;
        RemoveWeapon06FadeVoicesUsingSource(src);
        StopIfPlaying(src);
        src.volume = 1f;
        podDescentIntroVoice = null;
    }

    public bool PlayByCategory(Game03SeCategory category, AudioClip clip, float baseVolume = 1f)
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        return PlayCategoryCore(category, clip, baseVolume);
    }

    /// <summary>救急箱／爆弾／スマホの取得 SE。クリップ未設定なら false。</summary>
    public bool TryPlayConsumableItemPickupSe(Game03CargoItemKind kind)
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        AudioClip clip = null;
        float vol01 = 1f;
        switch (kind)
        {
            case Game03CargoItemKind.Medkit:
                clip = itemMedkitPickupSeClip;
                vol01 = itemMedkitPickupSeVolume;
                break;
            case Game03CargoItemKind.Bomb:
                clip = itemBombPickupSeClip;
                vol01 = itemBombPickupSeVolume;
                break;
            case Game03CargoItemKind.Phone:
                clip = itemPhonePickupSeClip;
                vol01 = itemPhonePickupSeVolume;
                break;
            default:
                return false;
        }

        if (clip == null || !TryComputeSeVolume01(vol01, out float volume01))
        {
            return false;
        }

        return PlayOneShotOnBucket(consumableItemPickupSeVoiceBucket, clip, volume01);
    }

    public bool PlayCutsceneByCategory(Game03SeCategory category, AudioClip clip, float baseVolume = 1f)
    {
        if (game03Manager != null && !game03Manager.CanPlayCutsceneSe)
        {
            return false;
        }

        if (clip == null || !TryComputeSeVolume01(baseVolume, out float volume))
        {
            return false;
        }

        return PlayOneShotOnBucket(cutsceneByCategoryVoiceBucket, clip, volume);
    }

    /// <summary>
    /// 敵接触中はループ再生。離れたら <see cref="playerUnitContactDamageLoopMinPlaySeconds"/> 経過後に停止。
    /// 毎フレームの当たり判定後に <paramref name="inDamagingContact"/> を渡す。
    /// </summary>
    public void SetPlayerUnitContactDamageLoopActive(bool inDamagingContact)
    {
        if (playerUnitContactDamageLoopVoice == null)
        {
            return;
        }

        if (!inDamagingContact)
        {
            RequestPlayerUnitContactDamageLoopRelease();
            return;
        }

        playerUnitContactDamageLoopReleasePending = false;

        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        if (playerUnitContactDamageSeClip == null)
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        EnsurePlayerUnitContactDamageLoopVoiceConfigured();
        if (!TryComputeSeVolume01(playerUnitContactDamageSeVolume, out float volume01))
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        playerUnitContactDamageLoopVoice.volume = volume01;
        if (!playerUnitContactDamageLoopVoice.isPlaying)
        {
            playerUnitContactDamageLoopSegmentStartUnscaledTime = Time.unscaledTime;
            playerUnitContactDamageLoopVoice.Play();
        }
    }

    private void RequestPlayerUnitContactDamageLoopRelease()
    {
        if (playerUnitContactDamageLoopVoice == null || !playerUnitContactDamageLoopVoice.isPlaying)
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        if (playerUnitContactDamageLoopMinPlaySeconds <= 0f)
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        playerUnitContactDamageLoopReleasePending = true;
        TickPlayerUnitContactDamageLoopRelease();
    }

    private void TickPlayerUnitContactDamageLoopRelease()
    {
        if (!playerUnitContactDamageLoopReleasePending)
        {
            return;
        }

        if (playerUnitContactDamageLoopVoice == null || !playerUnitContactDamageLoopVoice.isPlaying)
        {
            StopPlayerUnitContactDamageLoopInternal();
            return;
        }

        if (playerUnitContactDamageLoopSegmentStartUnscaledTime < 0f)
        {
            playerUnitContactDamageLoopSegmentStartUnscaledTime = Time.unscaledTime;
        }

        float elapsed = Time.unscaledTime - playerUnitContactDamageLoopSegmentStartUnscaledTime;
        if (elapsed >= playerUnitContactDamageLoopMinPlaySeconds)
        {
            StopPlayerUnitContactDamageLoopInternal();
        }
    }

    private void EnsurePlayerUnitContactDamageLoopVoiceConfigured()
    {
        if (playerUnitContactDamageLoopVoice == null)
        {
            return;
        }

        playerUnitContactDamageLoopVoice.playOnAwake = false;
        playerUnitContactDamageLoopVoice.loop = true;
        if (playerUnitContactDamageSeClip != null)
        {
            playerUnitContactDamageLoopVoice.clip = playerUnitContactDamageSeClip;
        }
    }

    private void StopPlayerUnitContactDamageLoopInternal()
    {
        playerUnitContactDamageLoopReleasePending = false;
        playerUnitContactDamageLoopSegmentStartUnscaledTime = -1f;
        if (playerUnitContactDamageLoopVoice != null && playerUnitContactDamageLoopVoice.isPlaying)
        {
            playerUnitContactDamageLoopVoice.Stop();
        }
    }

    /// <summary>敵撃破時（一般／ボス）。ボス用クリップが未設定なら一般クリップにフォールバック。</summary>
    public bool PlayEnemyDefeatSe(bool isBossEnemy)
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        AudioClip clip = isBossEnemy ? enemyDefeatBossSeClip : enemyDefeatNormalSeClip;
        float vol = isBossEnemy ? enemyDefeatBossSeVolume : enemyDefeatNormalSeVolume;
        if (clip == null && isBossEnemy)
        {
            clip = enemyDefeatNormalSeClip;
            vol = enemyDefeatNormalSeVolume;
        }

        if (clip == null)
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(enemyDefeatSeVoiceBucket, clip, vol);
    }

    /// <summary>武器が敵にヒットしてダメージが入ったとき（撃破時は別途 <see cref="PlayEnemyDefeatSe"/>）。ボス用クリップが未設定なら一般にフォールバック。</summary>
    public bool PlayEnemyWeaponHitSe(bool isBossEnemy)
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        AudioClip clip = isBossEnemy ? enemyWeaponHitBossSeClip : enemyWeaponHitNormalSeClip;
        float vol = isBossEnemy ? enemyWeaponHitBossSeVolume : enemyWeaponHitNormalSeVolume;
        if (clip == null && isBossEnemy)
        {
            clip = enemyWeaponHitNormalSeClip;
            vol = enemyWeaponHitNormalSeVolume;
        }

        if (clip == null)
        {
            return false;
        }

        if (!TryConsumeEnemyWeaponHitSeCooldown(isBossEnemy))
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(enemyWeaponHitSeVoiceBucket, clip, vol);
    }

    /// <summary>同フレーム／連続フレームの多重ヒットで SE が重ならないよう、一般とボスで別に間隔を取る。</summary>
    private bool TryConsumeEnemyWeaponHitSeCooldown(bool isBossEnemy)
    {
        float minInterval = Mathf.Max(0f, enemyWeaponHitSeMinIntervalSeconds);
        if (minInterval <= 0f)
        {
            return true;
        }

        float now = Time.time;
        if (isBossEnemy)
        {
            if (now - lastEnemyWeaponHitBossSeGameplayTime < minInterval)
            {
                return false;
            }

            lastEnemyWeaponHitBossSeGameplayTime = now;
            return true;
        }

        if (now - lastEnemyWeaponHitNormalSeGameplayTime < minInterval)
        {
            return false;
        }

        lastEnemyWeaponHitNormalSeGameplayTime = now;
        return true;
    }

    /// <summary>敵出現種別51（中BOSS湧き）出現時。クリップ未設定なら false。</summary>
    public bool TryPlayMidBossSpawnWarningSe()
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        if (midBossSpawnWarningSeClip == null)
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(midBossSpawnWarningSeVoiceBucket, midBossSpawnWarningSeClip, midBossSpawnWarningSeVolume);
    }

    /// <summary>敵出現種別61（BOSS湧き）出現時。クリップ未設定なら false。</summary>
    public bool TryPlayFinalBossSpawnWarningSe()
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        if (finalBossSpawnWarningSeClip == null)
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(finalBossSpawnWarningSeVoiceBucket, finalBossSpawnWarningSeClip, finalBossSpawnWarningSeVolume);
    }

    /// <summary>武器別「攻撃開始」系SE。<see cref="PlayWeapon02GroundImpactSe"/> は着地専用。</summary>
    public bool PlayWeaponSe(int weaponNumber)
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        return weaponNumber switch
        {
            1 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon01SeClip, weapon01SeVolume),
            2 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon02SeClip, weapon02SeVolume),
            3 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon03SeClip, weapon03SeVolume),
            4 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon04SeClip, weapon04SeVolume),
            5 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon05SeClip, weapon05SeVolume),
            6 => PlayWeapon06SeWithScheduledFade(),
            7 => PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon07SeClip, weapon07SeVolume),
            _ => false
        };
    }

    /// <summary>Weapon02 粘液が着弾して地面フィールドになるとき。</summary>
    public bool PlayWeapon02GroundImpactSe()
    {
        if (game03Manager != null && !game03Manager.CanPlayGameplaySe)
        {
            return false;
        }

        return PlayClipOnConfiguredBucket(weaponSeVoiceBucket, weapon02GroundImpactSeClip, weapon02GroundImpactSeVolume);
    }

    private bool PlayWeapon06SeWithScheduledFade()
    {
        if (weapon06SeClip == null)
        {
            return false;
        }

        AudioSource source = TryAcquireIdleVoice(weaponSeVoiceBucket);
        if (source == null)
        {
            return false;
        }

        RemoveWeapon06FadeVoicesUsingSource(source);

        float seGain = SoundSettingsManager.Instance != null
            ? Mathf.Max(0f, SoundSettingsManager.Instance.GetSeGain01())
            : 1f;
        float peak = Mathf.Clamp01(Mathf.Max(0f, weapon06SeVolume) * seGain);
        if (peak <= 0f)
        {
            return false;
        }

        source.Stop();
        source.clip = weapon06SeClip;
        source.loop = false;
        source.volume = peak;
        source.time = 0f;
        source.Play();

        weapon06FadeVoices.Add(new Weapon06FadeVoice
        {
            Source = source,
            PeakVolume01 = peak,
            PlayStartDspTime = AudioSettings.dspTime,
            HoldSeconds = Mathf.Max(0f, weapon06PlaybackHoldSeconds),
            FadeOutSeconds = Mathf.Max(0f, weapon06FadeOutSeconds)
        });

        return true;
    }

    private void RemoveWeapon06FadeVoicesUsingSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = weapon06FadeVoices.Count - 1; i >= 0; i--)
        {
            if (weapon06FadeVoices[i].Source == source)
            {
                weapon06FadeVoices.RemoveAt(i);
            }
        }
    }

    private void TickWeapon06FadeVoices()
    {
        double dspNow = AudioSettings.dspTime;

        for (int i = weapon06FadeVoices.Count - 1; i >= 0; i--)
        {
            Weapon06FadeVoice ev = weapon06FadeVoices[i];
            AudioSource src = ev.Source;
            if (src == null)
            {
                weapon06FadeVoices.RemoveAt(i);
                continue;
            }

            if (!src.isPlaying)
            {
                src.volume = 1f;
                weapon06FadeVoices.RemoveAt(i);
                continue;
            }

            double elapsed = dspNow - ev.PlayStartDspTime;

            if (elapsed <= ev.HoldSeconds)
            {
                src.volume = ev.PeakVolume01;
                continue;
            }

            if (ev.FadeOutSeconds <= 0f)
            {
                StopIfPlaying(src);
                src.volume = 1f;
                weapon06FadeVoices.RemoveAt(i);
                continue;
            }

            if (elapsed >= ev.HoldSeconds + ev.FadeOutSeconds)
            {
                StopIfPlaying(src);
                src.volume = 1f;
                weapon06FadeVoices.RemoveAt(i);
                continue;
            }

            float t = (float)((elapsed - ev.HoldSeconds) / ev.FadeOutSeconds);
            src.volume = ev.PeakVolume01 * (1f - Mathf.Clamp01(t));
        }
    }

    private bool PlayCueCore(Game03SeCue cue)
    {
        return cue switch
        {
            Game03SeCue.TransitionStart => PlayClipOnConfiguredBucket(uiTransitionSeVoiceBucket, transitionStartSeClip, transitionStartSeVolume),
            Game03SeCue.TitleReturnButtonClick => PlayClipOnConfiguredBucket(uiTransitionSeVoiceBucket, titleReturnButtonClickSeClip, titleReturnButtonClickSeVolume),
            Game03SeCue.LvUpPanelOpen => PlayClipOnConfiguredBucket(lvUpPanelSeVoiceBucket, lvUpPanelOpenSeClip, lvUpPanelOpenSeVolume),
            Game03SeCue.LvUpPanelSelect => PlayClipOnConfiguredBucket(lvUpPanelSeVoiceBucket, lvUpPanelSelectSeClip, lvUpPanelSelectSeVolume),
            Game03SeCue.PausePanelOpen => PlayClipOnConfiguredBucket(pausePanelSeVoiceBucket, pausePanelOpenSeClip, pausePanelOpenSeVolume),
            Game03SeCue.PausePanelResume => PlayClipOnConfiguredBucket(pausePanelSeVoiceBucket, pausePanelResumeSeClip, pausePanelResumeSeVolume),
            Game03SeCue.GameOver => PlayClipOnConfiguredBucket(gameOverPresentationSeVoiceBucket, gameOverSeClip, gameOverSeVolume),
            Game03SeCue.GameOverPanelAdvance => PlayClipOnConfiguredBucket(gameOverPresentationSeVoiceBucket, gameOverPanelAdvanceSeClip, gameOverPanelAdvanceSeVolume),
            _ => false
        };
    }

    private bool PlayCategoryCore(Game03SeCategory category, AudioClip clip, float baseVolume)
    {
        if (clip == null || !TryComputeSeVolume01(baseVolume, out float volume))
        {
            return false;
        }

        return PlayOneShotOnBucket(BucketForCategory(category), clip, volume);
    }

    private static void StopAllInList(List<AudioSource> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            StopIfPlaying(list[i]);
        }
    }

    private static void StopIfPlaying(AudioSource source)
    {
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
    }

    private void InitializeVoiceBuckets()
    {
        CopySerializedToRuntime(uiVoiceAudioSources, runtimeUiVoices);
        CopySerializedToRuntime(performanceVoiceAudioSources, runtimePerformanceVoices);
        CopySerializedToRuntime(weaponVoiceAudioSources, runtimeWeaponVoices);
        CopySerializedToRuntime(enemyVoiceAudioSources, runtimeEnemyVoices);

        int total = runtimeUiVoices.Count + runtimePerformanceVoices.Count + runtimeWeaponVoices.Count + runtimeEnemyVoices.Count;
        if (total == 0 && defaultAudioSource != null)
        {
            defaultAudioSource.playOnAwake = false;
            runtimeUiVoices.Add(defaultAudioSource);
        }
    }

    private static void CopySerializedToRuntime(List<AudioSource> serialized, List<AudioSource> runtime)
    {
        runtime.Clear();
        if (serialized == null)
        {
            return;
        }

        for (int i = 0; i < serialized.Count; i++)
        {
            AudioSource src = serialized[i];
            if (src == null)
            {
                continue;
            }

            src.playOnAwake = false;
            runtime.Add(src);
        }
    }

    private AudioSource TryAcquireIdleVoice(Game03SeVoiceBucket bucket)
    {
        List<AudioSource> list = bucket switch
        {
            Game03SeVoiceBucket.Ui => runtimeUiVoices,
            Game03SeVoiceBucket.Performance => runtimePerformanceVoices,
            Game03SeVoiceBucket.Weapon => runtimeWeaponVoices,
            Game03SeVoiceBucket.Enemy => runtimeEnemyVoices,
            _ => runtimeUiVoices
        };

        if (list.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < list.Count; i++)
        {
            AudioSource src = list[i];
            if (src == null)
            {
                continue;
            }

            if (!src.isPlaying)
            {
                return src;
            }
        }

        return null;
    }

    private AudioSource TryAcquireAnyVoice(Game03SeVoiceBucket bucket)
    {
        List<AudioSource> list = bucket switch
        {
            Game03SeVoiceBucket.Ui => runtimeUiVoices,
            Game03SeVoiceBucket.Performance => runtimePerformanceVoices,
            Game03SeVoiceBucket.Weapon => runtimeWeaponVoices,
            Game03SeVoiceBucket.Enemy => runtimeEnemyVoices,
            _ => runtimeUiVoices
        };

        for (int i = 0; i < list.Count; i++)
        {
            AudioSource src = list[i];
            if (src != null)
            {
                return src;
            }
        }

        return null;
    }
}
