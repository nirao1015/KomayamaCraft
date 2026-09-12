using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Game01Manager : MonoBehaviour
{
    public static Game01Manager Instance { get; private set; }

    [Header("ステージ遷移情報")]
    [SerializeField] private string currentStageName = "stage_01";

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI lifeUI;
    [SerializeField] private GameOverPanelController gameOverPanelController;
    [SerializeField] private GameClearedPanelController gameClearedPanelController;
    [SerializeField] private Transform stageBoundsRoot;

    [Header("Audio管理参照")]
    [SerializeField] private Game01BgmManager game01BgmManager;
    [SerializeField] private Game01SeManager game01SeManager;
    [SerializeField] private Game01TransitionManager game01TransitionManager;

    [Header("進入禁止エリア")]
    [SerializeField] private float noEntryRadius = 2f;
    [SerializeField] private float noEntryBorderThickness = 1f;
    [SerializeField] private float noEntryReboundSeconds = 0.2f;
    [SerializeField] private float noEntryReboundDistance = 2f;
    [SerializeField] private float noEntryInitialReboundSpeed = 8f;
    [SerializeField] private Sprite noEntryReboundPlayerSprite;
    [Tooltip("進入禁止差し替えスプライト表示中、1 FixedUpdate あたりの回転量（度）。0 に近いほど小刻み。")]
    [SerializeField] private float noEntryReboundSpinStepDegrees = 8f;
    [Tooltip("true: 右回り（時計回り）、false: 左回り（反時計回り）。")]
    [SerializeField] private bool noEntryReboundSpinClockwise = true;

    [Header("Enemy01")]
    [SerializeField] private GameObject enemy01Prefab;
    [SerializeField] private int enemy01MaxSimultaneous = 5;

    [Header("Enemy11 初期ウェーブ")]
    [SerializeField] private GameObject enemy11Prefab;
    [SerializeField] private bool enableEnemy11InitialWave = true;
    [SerializeField] private float enemy11InitialDelaySeconds = 3f;
    [SerializeField] private int enemy11InitialCount = 3;
    [SerializeField] private float enemy11VerticalSpacing = 1.2f;
    [SerializeField] private float enemy11MoveSpeed = 4f;
    [SerializeField] private int enemy11MaxSimultaneous = 8;
    [SerializeField] private Sprite enemy11Sprite;

    [Header("Enemy12")]
    [SerializeField] private GameObject enemy12Prefab;
    [SerializeField] private int enemy12MaxSimultaneous = 8;

    [Header("＜初期ステージ経過時間＞（敵 JSON スポーン）")]
    [Tooltip("0: T0 からの実時間で spawnTime を判定。0 より大きい: タイムライン上この秒数まで進んだ状態で開始（elapsed に加算）。この値未満の spawnTime の行は生成せずスキップし、同時出現上限への一括スポーンを避ける（敵出現のみ。クリア時間は変わらない）。")]
    [SerializeField] private float initialStageElapsedTimeForEnemySpawnSeconds = 0f;

    [Header("ステージ開始演出")]
    [SerializeField] private bool waitForStageIntro = true;

    [Header("背景スケール演出（操作開始後）")]
    [SerializeField] private Transform stageBackground;
    [SerializeField] private float backgroundInitialScaleMultiplier = 1f;
    [SerializeField] private float backgroundScaleRatePerSecond = 0f;

    [Header("ステージクリア演出（プレイヤー）")]
    [SerializeField] private Sprite playerClearedSprite;
    [SerializeField] private float returnToGameplayStartSeconds = 0.35f;
    [SerializeField] private float moveToCenterSeconds = 1.2f;
    [SerializeField] private float clearedScaleMinimum = 0.35f;
    [SerializeField] private float clearTimeSeconds = 120f;
    [Tooltip("時間切れクリア時のみ。左右揺れの片側ピーク角（度）。仕様目安 35°。")]
    [SerializeField] private float timeClearParachuteMaxSwayDegrees = 35f;
    [Tooltip("時間切れクリア時のみ。左右 1 往復に要する秒数（連続 sin 振りの周期）。")]
    [SerializeField] private float timeClearParachuteSwayPeriodSeconds = 2.4f;

    private const string StageBoundsName = "StageBounds";
    private const string NoEntryZoneName = "no_entryzoone";
    private const string ObstaclesRootName = "ObstaclesRoot";
    private const string Enemy01Name = "Enemy01";
    private const string Enemy11Name = "Enemy11";
    private const string Enemy12Name = "Enemy12";
    private const string EnemyStageJsonFolder = "GameData/EnemySpawn";
    private const int NoEntryCircleSegments = 64;

    private GameObject noEntryZoneObject;
    private CircleCollider2D noEntryCollider;
    private LineRenderer noEntryLineRenderer;
    [SerializeField] private Game01PlayerController playerController;
    [SerializeField] private Game01PlayerLife playerLife;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CircleCollider2D playerCollider;
    private bool isRebounding;
    private Tween reboundTween;
    private Transform obstaclesRoot;
    private bool stageIntroComplete;
    private bool stageCleared;
    private bool hasAppliedBackgroundInitialScale;
    private bool hasControlStartTime;
    private float controlStartTimeSeconds;
    private bool enemy01SpawnPlanned;
    private bool enemy11SpawnPlanned;
    private bool enemy12SpawnPlanned;
    private bool enemy01SpawnCompleted;
    private bool enemy11SpawnCompleted;
    private bool enemy12SpawnCompleted;
    private bool hasAnyStageSpawn;
    private Vector2 gameplayStartPosition;
    private Vector3 gameplayStartScale = Vector3.one;
    private float gameplayStartRotationDeg;
    private SpriteRenderer playerSpriteRenderer;
    private bool noEntryReboundSpriteSwapActive;
    private Sprite noEntryReboundSpriteRestore;
    private float noEntryReboundRotationSaved;
    private Coroutine noEntryReboundSpinCoroutine;
    private FallVisualEffectController fallVisualEffectController;
    private IGame01MidgameBackgroundEnemySpawnSuppressor[] midgameBackgroundEnemySpawnSuppressors;
    private Coroutine stageClearRoutine;
    private bool canTransitionToMenuByClickAfterClear;
    private bool isClearMenuTransitioning;
    private Transform timeClearParachuteSwayPivot;
    private SpriteRenderer timeClearParachuteProxySpriteRenderer;

#if UNITY_EDITOR
    private int enemy01SpawnApproachLoggedForIndex = -1;
#endif

    public string CurrentStageName => currentStageName;
    public bool WaitForStageIntro => waitForStageIntro;
    public bool IsStageIntroComplete => stageIntroComplete;
    public bool IsStageCleared => stageCleared;

    /// <summary>時間切れクリアが有効なときの秒数（0 以下なら無効）。Inspector の clearTimeSeconds。</summary>
    public float ClearTimeLimitSeconds => clearTimeSeconds;

    public void ApplyDebugClearTimeSeconds(float debugClearTimeSeconds)
    {
        clearTimeSeconds = debugClearTimeSeconds;
    }

    public void ApplyDebugEnemySpawnTimelineStartSeconds(float debugStartSeconds)
    {
        initialStageElapsedTimeForEnemySpawnSeconds = Mathf.Max(0f, debugStartSeconds);
    }

    public void ApplyDebugSkipStageIntro(bool shouldSkipStageIntro)
    {
        waitForStageIntro = !shouldSkipStageIntro;
        if (shouldSkipStageIntro)
        {
            stageIntroComplete = true;
        }
    }

    /// <summary>操作可能開始（T0）が記録済みか。</summary>
    public bool HasGameplayControlStarted => hasControlStartTime;

    /// <summary>T0 からの経過秒（未開始は 0）。</summary>
    public float GameplayElapsedSinceControlSeconds =>
        hasControlStartTime ? Mathf.Max(0f, Time.time - controlStartTimeSeconds) : 0f;

    /// <summary>
    /// Fire / City 背景演出の前後（演出開始の lead 秒前〜演出終了）に JSON タイムラインの新規出現を抑止する。
    /// </summary>
    private bool IsEnemySpawnSuppressedByMidgameBackgroundPerformance()
    {
        if (!hasControlStartTime || midgameBackgroundEnemySpawnSuppressors == null)
        {
            return false;
        }

        float elapsed = GameplayElapsedSinceControlSeconds;
        for (int i = 0; i < midgameBackgroundEnemySpawnSuppressors.Length; i++)
        {
            IGame01MidgameBackgroundEnemySpawnSuppressor suppressor = midgameBackgroundEnemySpawnSuppressors[i];
            if (suppressor != null && suppressor.IsSuppressingEnemySpawns(elapsed))
            {
                return true;
            }
        }

        return false;
    }

    private static IGame01MidgameBackgroundEnemySpawnSuppressor[] CollectMidgameBackgroundEnemySpawnSuppressors()
    {
        FireScreenController[] fireScreens = FindObjectsByType<FireScreenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        CityScreenController[] cityScreens = FindObjectsByType<CityScreenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int count = fireScreens.Length + cityScreens.Length;
        if (count == 0)
        {
            return System.Array.Empty<IGame01MidgameBackgroundEnemySpawnSuppressor>();
        }

        var list = new IGame01MidgameBackgroundEnemySpawnSuppressor[count];
        int index = 0;
        for (int i = 0; i < fireScreens.Length; i++)
        {
            list[index++] = fireScreens[i];
        }

        for (int i = 0; i < cityScreens.Length; i++)
        {
            list[index++] = cityScreens[i];
        }

        return list;
    }

    private IEnumerator WaitWhileEnemySpawnSuppressedByMidgameBackgroundPerformance()
    {
        while (IsEnemySpawnSuppressedByMidgameBackgroundPerformance())
        {
            yield return null;
        }
    }

    /// <summary>JSON の spawnTime と比較する経過秒（Inspector の初期ステージ経過時間を加算）。</summary>
    private float EnemyJsonSpawnTimelineElapsedSeconds()
    {
        if (!hasControlStartTime)
        {
            return 0f;
        }

        float offset = Mathf.Max(0f, initialStageElapsedTimeForEnemySpawnSeconds);
        return Mathf.Max(0f, Time.time - controlStartTimeSeconds + offset);
    }

    /// <summary>
    /// 初期タイムラインシーク用: 閾値未満の spawnTime は生成せずインデックスだけ進める（シーク直後の大量 Instantiate を防ぐ）。
    /// </summary>
    private void AdvanceSpawnIndexPastInitialTimeline<TSpawn>(ref int index, TSpawn[] spawns, System.Func<TSpawn, float> getSpawnTime)
    {
        float threshold = Mathf.Max(0f, initialStageElapsedTimeForEnemySpawnSeconds);
        if (threshold <= 0f || spawns == null)
        {
            return;
        }

        while (index < spawns.Length && getSpawnTime(spawns[index]) < threshold)
        {
            index++;
        }
    }

    public void NotifyStageIntroComplete()
    {
        stageIntroComplete = true;
        SteamAchievementController.TryUnlock(SteamAchievementIds.Game01_01);
    }

    public void TriggerStageClear()
    {
        if (stageCleared || stageClearRoutine != null)
        {
            return;
        }

        stageClearRoutine = StartCoroutine(StageClearSequenceCoroutine());
    }

    private void Awake()
    {
        Instance = this;

        // menu_scene から受け取ったステージ名を保持する。
        if (!string.IsNullOrEmpty(SceneTransitionContext.StageName))
        {
            currentStageName = SceneTransitionContext.StageName;
        }

        if (lifeUI != null)
        {
            lifeUI.text = "3";
        }

        CachePlayerReferences();
        stageIntroComplete = !waitForStageIntro;
        if (playerRigidbody != null)
        {
            gameplayStartPosition = playerRigidbody.position;
        }
        else if (playerController != null)
        {
            gameplayStartPosition = playerController.transform.position;
        }

        if (playerController != null)
        {
            gameplayStartScale = playerController.transform.localScale;
            playerSpriteRenderer = playerController.GetComponent<SpriteRenderer>();
        }

        if (playerRigidbody != null)
        {
            gameplayStartRotationDeg = playerRigidbody.rotation;
        }
        else if (playerController != null)
        {
            gameplayStartRotationDeg = playerController.transform.eulerAngles.z;
        }
        SetupStageBgmVolume();
        SetupNoEntryZone();
        obstaclesRoot = GetOrCreateObstaclesRoot();
        SetupStageSpawnFromJson();
        ResolveBackgroundReference();
        // Inspector 設定前提。未設定時は null のまま扱う。
    }

    private void Start()
    {
        if (playerLife != null && gameOverPanelController != null)
        {
            playerLife.OnGameOver += gameOverPanelController.OnPlayerGameOver;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (playerLife != null && gameOverPanelController != null)
        {
            playerLife.OnGameOver -= gameOverPanelController.OnPlayerGameOver;
        }

        if (stageClearRoutine != null)
        {
            StopCoroutine(stageClearRoutine);
            stageClearRoutine = null;
        }

        DestroyObstacleEnemyInstancesUnderObstaclesRoot(false);
        Enemy01Controller.ResyncSimultaneousSpawnCountFromScene();

        CleanupTimeClearParachuteVisualRig();
    }

    private void SetupStageBgmVolume()
    {
        game01BgmManager?.ApplyStageBgmVolumeFromSettings();
        // シーン読み込み直後（開始演出より前）に明示的にステージBGMをキックする。
        game01BgmManager?.PlayStageBgmIfConfigured();
    }

    private void Update()
    {
        TryMarkControlStartTime();
        TryAutoStageClear();
        TryHandleStageClearMenuClickTransition();
        UpdateNoEntryZoneVisual();

        RefreshLifeUI();
        UpdateBackgroundScale();

        if (isRebounding || noEntryCollider == null || playerRigidbody == null)
        {
            return;
        }

        if (stageCleared)
        {
            return;
        }

        if (waitForStageIntro && !stageIntroComplete)
        {
            return;
        }

        Vector2 zoneCenter = noEntryCollider.bounds.center;
        float zoneRadius = noEntryCollider.radius * Mathf.Abs(noEntryCollider.transform.lossyScale.x);
        float playerRadius = playerCollider != null
            ? playerCollider.radius * Mathf.Abs(playerCollider.transform.lossyScale.x)
            : 0f;

        Vector2 toPlayer = playerRigidbody.position - zoneCenter;
        float overlapThreshold = zoneRadius + playerRadius;
        if (toPlayer.sqrMagnitude <= overlapThreshold * overlapThreshold)
        {
            StartCoroutine(ApplyNoEntryRebound(zoneCenter, toPlayer));
        }
    }

    private void CachePlayerReferences()
    {
        if (playerController != null)
        {
            if (playerRigidbody == null)
            {
                playerRigidbody = playerController.GetComponent<Rigidbody2D>();
            }

            if (playerCollider == null)
            {
                playerCollider = playerController.GetComponent<CircleCollider2D>();
            }
        }

        if (playerLife == null && playerController != null)
        {
            playerLife = playerController.GetComponent<Game01PlayerLife>();
        }
    }

    private void RefreshLifeUI()
    {
        if (lifeUI == null || playerLife == null)
        {
            return;
        }

        lifeUI.text = playerLife.CurrentLives.ToString();
    }

    private void ResolveBackgroundReference()
    {
        if (stageBackground != null)
        {
            return;
        }

        // Inspector 設定前提。名前探索は行わない。
    }

    private void UpdateBackgroundScale()
    {
        if (stageBackground == null)
        {
            return;
        }

        if (stageCleared)
        {
            return;
        }

        if (waitForStageIntro && !stageIntroComplete)
        {
            return;
        }

        if (!hasAppliedBackgroundInitialScale)
        {
            float initialMultiplier = Mathf.Max(0.01f, backgroundInitialScaleMultiplier);
            stageBackground.localScale *= initialMultiplier;
            hasAppliedBackgroundInitialScale = true;
        }

        if (Mathf.Approximately(backgroundScaleRatePerSecond, 0f))
        {
            return;
        }

        float perFrameMultiplier = 1f + (backgroundScaleRatePerSecond * Time.deltaTime);
        if (perFrameMultiplier <= 0f)
        {
            return;
        }

        stageBackground.localScale *= perFrameMultiplier;
    }

    private void SetupNoEntryZone()
    {
        if (stageBoundsRoot == null)
        {
            return;
        }

        Transform zoneTransform = stageBoundsRoot.Find(NoEntryZoneName);
        if (zoneTransform != null)
        {
            noEntryZoneObject = zoneTransform.gameObject;
        }
        else
        {
            noEntryZoneObject = new GameObject(NoEntryZoneName);
            noEntryZoneObject.transform.SetParent(stageBoundsRoot, false);
            noEntryZoneObject.transform.localPosition = Vector3.zero;
        }

        noEntryCollider = noEntryZoneObject.GetComponent<CircleCollider2D>();
        if (noEntryCollider == null)
        {
            noEntryCollider = noEntryZoneObject.AddComponent<CircleCollider2D>();
        }

        noEntryCollider.isTrigger = true;
        noEntryCollider.radius = Mathf.Max(0.1f, noEntryRadius);

        noEntryLineRenderer = noEntryZoneObject.GetComponent<LineRenderer>();
        if (noEntryLineRenderer == null)
        {
            noEntryLineRenderer = noEntryZoneObject.AddComponent<LineRenderer>();
        }

        noEntryLineRenderer.loop = true;
        noEntryLineRenderer.useWorldSpace = false;
        noEntryLineRenderer.positionCount = NoEntryCircleSegments;
        noEntryLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        noEntryLineRenderer.startColor = new Color(1f, 0f, 0f, 1f);
        noEntryLineRenderer.endColor = new Color(1f, 0f, 0f, 1f);
        float width = Mathf.Max(0.05f, noEntryBorderThickness * 0.05f);
        noEntryLineRenderer.startWidth = width;
        noEntryLineRenderer.endWidth = width;
        noEntryLineRenderer.sortingOrder = 200;

        float radius = Mathf.Max(0.1f, noEntryRadius);
        for (int i = 0; i < NoEntryCircleSegments; i++)
        {
            float t = (float)i / NoEntryCircleSegments;
            float angle = t * Mathf.PI * 2f;
            noEntryLineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        // 実ゲーム開始までは非表示。判定用 Collider は常時有効のまま維持する。
        noEntryLineRenderer.enabled = false;
    }

    private void UpdateNoEntryZoneVisual()
    {
        if (noEntryLineRenderer == null)
        {
            return;
        }

        bool shouldShow =
            hasControlStartTime &&
            !stageCleared &&
            (playerLife == null || !playerLife.IsGameOver);
        noEntryLineRenderer.enabled = shouldShow;
    }

    private IEnumerator ApplyNoEntryRebound(Vector2 zoneCenter, Vector2 toPlayer)
    {
        isRebounding = true;
        noEntryReboundSpriteSwapActive = false;
        noEntryReboundSpriteRestore = null;

        game01SeManager?.PlayNoEntrySe();

        Vector2 reboundDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;
        Vector2 startPosition = playerRigidbody.position;
        Vector2 targetPosition = startPosition + (reboundDirection * Mathf.Max(0.1f, noEntryReboundDistance));
        float duration = Mathf.Max(0.01f, noEntryReboundSeconds);

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        TryApplyNoEntryReboundPlayerSprite();

        noEntryReboundSpinCoroutine = null;
        if (noEntryReboundSpriteSwapActive && playerRigidbody != null &&
            Mathf.Abs(noEntryReboundSpinStepDegrees) > 0.0001f)
        {
            noEntryReboundRotationSaved = playerRigidbody.rotation;
            noEntryReboundSpinCoroutine = StartCoroutine(NoEntryReboundSpinStepRoutine());
        }

        yield return StartCoroutine(PlayReboundMotion(startPosition, targetPosition, duration));

        if (noEntryReboundSpinCoroutine != null)
        {
            StopCoroutine(noEntryReboundSpinCoroutine);
            noEntryReboundSpinCoroutine = null;
            if (playerRigidbody != null)
            {
                playerRigidbody.rotation = noEntryReboundRotationSaved;
            }
        }

        TryRestoreNoEntryReboundPlayerSprite();

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        isRebounding = false;
    }

    private void TryApplyNoEntryReboundPlayerSprite()
    {
        if (noEntryReboundPlayerSprite == null || playerSpriteRenderer == null)
        {
            return;
        }

        if (playerLife != null && playerLife.IsDamageFeedbackActive)
        {
            return;
        }

        noEntryReboundSpriteRestore = playerSpriteRenderer.sprite;
        playerSpriteRenderer.sprite = noEntryReboundPlayerSprite;
        noEntryReboundSpriteSwapActive = true;
    }

    private void TryRestoreNoEntryReboundPlayerSprite()
    {
        if (!noEntryReboundSpriteSwapActive || playerSpriteRenderer == null || noEntryReboundPlayerSprite == null)
        {
            noEntryReboundSpriteSwapActive = false;
            return;
        }

        if (playerSpriteRenderer.sprite != noEntryReboundPlayerSprite)
        {
            noEntryReboundSpriteSwapActive = false;
            return;
        }

        playerSpriteRenderer.sprite = noEntryReboundSpriteRestore;
        noEntryReboundSpriteSwapActive = false;
    }

    private IEnumerator PlayReboundMotion(Vector2 startPosition, Vector2 targetPosition, float duration)
    {
        bool finished = false;
        reboundTween?.Kill();
        float effectiveDuration = Mathf.Max(0.05f, duration);
        float overshoot = Mathf.Clamp(1.2f + (noEntryInitialReboundSpeed * 0.15f), 1.2f, 4f);

        reboundTween = playerRigidbody
            .DOMove(targetPosition, effectiveDuration)
            .SetEase(Ease.OutBack, overshoot)
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() => finished = true)
            .OnKill(() => finished = true);

        while (!finished)
        {
            yield return null;
        }
    }

    private IEnumerator NoEntryReboundSpinStepRoutine()
    {
        float step = Mathf.Abs(noEntryReboundSpinStepDegrees);
        if (step < 0.0001f || playerRigidbody == null)
        {
            yield break;
        }

        float deltaPerFixed = noEntryReboundSpinClockwise ? -step : step;

        while (playerRigidbody != null)
        {
            playerRigidbody.MoveRotation(playerRigidbody.rotation + deltaPerFixed);
            yield return new WaitForFixedUpdate();
        }
    }

    private void SetupStageSpawnFromJson()
    {
        DestroyObstacleEnemyInstancesUnderObstaclesRoot(true);
        Enemy01Controller.ResyncSimultaneousSpawnCountFromScene();

        enemy01SpawnPlanned = false;
        enemy01SpawnCompleted = false;
        enemy11SpawnPlanned = false;
        enemy11SpawnCompleted = false;
        enemy12SpawnPlanned = false;
        enemy12SpawnCompleted = false;
        hasAnyStageSpawn = false;

        if (!TryLoadStageSpawnPayload(currentStageName, out StageSpawnPayload payload))
        {
            Debug.LogWarning(
                $"[GameManager] Stage spawn JSON not found for '{currentStageName}'. " +
                $"Assign Resources/GameData/Game01EnemySpawnCatalog or place '{currentStageName}.json' under Assets/{EnemyStageJsonFolder}/.");
            enemy01SpawnCompleted = true;
            enemy11SpawnCompleted = true;
            enemy12SpawnCompleted = true;
            return;
        }

        if (payload == null)
        {
            Debug.LogWarning(
                $"[GameManager] Stage spawn JSON failed to parse or missing spawns array: '{currentStageName}'");
            enemy01SpawnCompleted = true;
            enemy11SpawnCompleted = true;
            enemy12SpawnCompleted = true;
            return;
        }

        int totalCount = (payload.Enemy01Entries?.Length ?? 0) +
            (payload.Enemy11Entries?.Length ?? 0) +
            (payload.Enemy12Entries?.Length ?? 0);
        if (totalCount == 0)
        {
            enemy01SpawnCompleted = true;
            enemy11SpawnCompleted = true;
            enemy12SpawnCompleted = true;
            return;
        }

        hasAnyStageSpawn = true;
        EnsureEnemy11Prefab();

        if (payload.Enemy01Entries != null && payload.Enemy01Entries.Length > 0)
        {
            enemy01SpawnPlanned = true;
            EnsureEnemy01Prefab();
            StartCoroutine(SpawnEnemy01Coroutine(payload.Enemy01Entries));
        }
        else
        {
            enemy01SpawnCompleted = true;
        }

        if (payload.Enemy11Entries != null && payload.Enemy11Entries.Length > 0)
        {
            enemy11SpawnPlanned = true;
            StartCoroutine(SpawnEnemy11Coroutine(payload.Enemy11Entries));
        }
        else
        {
            enemy11SpawnCompleted = true;
        }

        if (payload.Enemy12Entries != null && payload.Enemy12Entries.Length > 0)
        {
            enemy12SpawnPlanned = true;
            EnemyPlayerPositionSampler.EnsureExists();
            EnsureEnemy12Prefab();
            StartCoroutine(SpawnEnemy12Coroutine(payload.Enemy12Entries));
        }
        else
        {
            enemy12SpawnCompleted = true;
        }
    }

    private Transform GetOrCreateObstaclesRoot()
    {
        GameObject rootObject = GameObject.Find(ObstaclesRootName);
        if (rootObject != null)
        {
            return rootObject.transform;
        }

        rootObject = new GameObject(ObstaclesRootName);
        rootObject.transform.position = Vector3.zero;
        return rootObject.transform;
    }

    /// <summary>
    /// ObstaclesRoot 配下の敵インスタンスを除去（再プレイ・ドメイン再読込なし時の残留や、ステージクリア後の残骸対策）。
    /// </summary>
    /// <param name="useDestroyImmediate">true のとき即時破棄（Awake 内の再セットアップやクリア直後に同期を確実にする）。</param>
    private void DestroyObstacleEnemyInstancesUnderObstaclesRoot(bool useDestroyImmediate)
    {
        if (obstaclesRoot == null)
        {
            return;
        }

        Enemy01Controller[] c01 = obstaclesRoot.GetComponentsInChildren<Enemy01Controller>(true);
        for (int i = 0; i < c01.Length; i++)
        {
            if (c01[i] != null)
            {
                DestroyObstacleEnemyGameObject(c01[i].gameObject, useDestroyImmediate);
            }
        }

        Enemy11Controller[] c11 = obstaclesRoot.GetComponentsInChildren<Enemy11Controller>(true);
        for (int i = 0; i < c11.Length; i++)
        {
            if (c11[i] != null)
            {
                DestroyObstacleEnemyGameObject(c11[i].gameObject, useDestroyImmediate);
            }
        }

        Enemy12Controller[] c12 = obstaclesRoot.GetComponentsInChildren<Enemy12Controller>(true);
        for (int i = 0; i < c12.Length; i++)
        {
            if (c12[i] != null)
            {
                DestroyObstacleEnemyGameObject(c12[i].gameObject, useDestroyImmediate);
            }
        }
    }

    private static void DestroyObstacleEnemyGameObject(GameObject target, bool useDestroyImmediate)
    {
        if (target == null)
        {
            return;
        }

        if (useDestroyImmediate)
        {
            DestroyImmediate(target);
        }
        else
        {
            Destroy(target);
        }
    }

    private IEnumerator SpawnEnemy01Coroutine(EnemySpawnEntry[] spawns)
    {
        while (waitForStageIntro && !stageIntroComplete)
        {
            yield return null;
        }

        while (!hasControlStartTime)
        {
            yield return null;
        }

        int index = 0;
        System.Array.Sort(spawns, (a, b) => a.spawnTime.CompareTo(b.spawnTime));
        AdvanceSpawnIndexPastInitialTimeline(ref index, spawns, e => e.spawnTime);
#if UNITY_EDITOR
        enemy01SpawnApproachLoggedForIndex = -1;
#endif

        while (index < spawns.Length)
        {
            if (stageCleared)
            {
#if UNITY_EDITOR
                float ge = GameplayElapsedSinceControlSeconds;
                Debug.LogWarning(
                    $"[GameManager] Enemy01 spawn coroutine aborted (stageCleared): gameplayElapsed={ge:F2}s, next spawnTime={spawns[index].spawnTime:F2}s, index={index}/{spawns.Length}");
#endif
                yield break;
            }

            float elapsed = EnemyJsonSpawnTimelineElapsedSeconds();
            EnemySpawnEntry entry = spawns[index];
            if (elapsed < entry.spawnTime)
            {
#if UNITY_EDITOR
                float remain = entry.spawnTime - elapsed;
                if (remain > 0f && remain <= 0.5f && enemy01SpawnApproachLoggedForIndex != index)
                {
                    enemy01SpawnApproachLoggedForIndex = index;
                    Debug.Log(
                        $"[GameManager] Enemy01 spawn approaching (<=0.5s): remain={remain:F3}s spawnTime={entry.spawnTime:F3}s timeline={elapsed:F3}s index={index}/{spawns.Length}");
                }
#endif
                yield return null;
                continue;
            }

            yield return WaitWhileEnemySpawnSuppressedByMidgameBackgroundPerformance();

            if (entry.enemyType == Enemy01Name)
            {
#if UNITY_EDITOR
                Debug.Log(
                    $"[GameManager] Enemy01 spawn firing: spawnTime={entry.spawnTime:F3}s timeline={elapsed:F3}s gameplayReal={GameplayElapsedSinceControlSeconds:F3}s index={index}/{spawns.Length}");
#endif
                SpawnEnemy01(entry);
            }

            index++;
            yield return null;
        }

        enemy01SpawnCompleted = true;
    }

    private IEnumerator SpawnEnemy11InitialWaveCoroutine()
    {
        while (waitForStageIntro && !stageIntroComplete)
        {
            yield return null;
        }

        float delay = Mathf.Max(0f, enemy11InitialDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        int count = Mathf.Max(1, enemy11InitialCount);
        float spacing = Mathf.Max(0f, enemy11VerticalSpacing);
        float centerOffset = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float yOffset = (i - centerOffset) * spacing;
            SpawnEnemy11(yOffset);
        }
    }

    private IEnumerator SpawnEnemy11Coroutine(Enemy11SpawnEntry[] spawns)
    {
        while (waitForStageIntro && !stageIntroComplete)
        {
            yield return null;
        }

        while (!hasControlStartTime)
        {
            yield return null;
        }

        int index = 0;
        System.Array.Sort(spawns, (a, b) => a.spawnTime.CompareTo(b.spawnTime));
        AdvanceSpawnIndexPastInitialTimeline(ref index, spawns, e => e.spawnTime);

        while (index < spawns.Length)
        {
            if (stageCleared)
            {
                yield break;
            }

            float elapsed = EnemyJsonSpawnTimelineElapsedSeconds();
            Enemy11SpawnEntry entry = spawns[index];
            if (elapsed < entry.spawnTime)
            {
                yield return null;
                continue;
            }

            yield return WaitWhileEnemySpawnSuppressedByMidgameBackgroundPerformance();

            SpawnEnemy11(entry);
            index++;
            yield return null;
        }

        enemy11SpawnCompleted = true;
    }

    private IEnumerator SpawnEnemy12Coroutine(Enemy12SpawnEntry[] spawns)
    {
        while (waitForStageIntro && !stageIntroComplete)
        {
            yield return null;
        }

        while (!hasControlStartTime)
        {
            yield return null;
        }

        int index = 0;
        System.Array.Sort(spawns, (a, b) => a.spawnTime.CompareTo(b.spawnTime));
        AdvanceSpawnIndexPastInitialTimeline(ref index, spawns, e => e.spawnTime);

        while (index < spawns.Length)
        {
            if (stageCleared)
            {
                yield break;
            }

            float elapsed = EnemyJsonSpawnTimelineElapsedSeconds();
            Enemy12SpawnEntry entry = spawns[index];
            if (elapsed < entry.spawnTime)
            {
                yield return null;
                continue;
            }

            yield return WaitWhileEnemySpawnSuppressedByMidgameBackgroundPerformance();

            SpawnEnemy12(entry);
            index++;
            yield return null;
        }

        enemy12SpawnCompleted = true;
    }

    private sealed class StageSpawnPayload
    {
        public EnemySpawnEntry[] Enemy01Entries;
        public Enemy11SpawnEntry[] Enemy11Entries;
        public Enemy12SpawnEntry[] Enemy12Entries;
    }

    private static bool TryLoadStageSpawnPayload(string stageBaseName, out StageSpawnPayload payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(stageBaseName))
        {
            return false;
        }

        Game01EnemySpawnCatalog catalog = GameDataCatalogs.Game01EnemySpawn;
        string label = stageBaseName.Trim();
        if (catalog != null && catalog.TryGetStageJsonText(stageBaseName, out string jsonText))
        {
            payload = LoadStageSpawnsFromJsonText(jsonText, label);
            return payload != null;
        }

#if UNITY_EDITOR
        string assetsPath = Path.Combine(Application.dataPath, EnemyStageJsonFolder, $"{label}.json");
        if (File.Exists(assetsPath))
        {
            payload = LoadStageSpawnsFromJsonText(File.ReadAllText(assetsPath), label);
            return payload != null;
        }
#endif
        return false;
    }

    private StageSpawnPayload LoadStageSpawnsFromJson(string jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
        {
            return null;
        }

        return LoadStageSpawnsFromJsonText(File.ReadAllText(jsonPath), jsonPath);
    }

    private static StageSpawnPayload LoadStageSpawnsFromJsonText(string jsonText, string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return new StageSpawnPayload
            {
                Enemy01Entries = System.Array.Empty<EnemySpawnEntry>(),
                Enemy11Entries = System.Array.Empty<Enemy11SpawnEntry>(),
                Enemy12Entries = System.Array.Empty<Enemy12SpawnEntry>()
            };
        }

        string trimmed = jsonText.Trim();
        Match spawnsMatch = Regex.Match(trimmed, "\"spawns\"\\s*:\\s*\\[(.*)\\]", RegexOptions.Singleline);
        if (!spawnsMatch.Success)
        {
            Debug.LogError($"Stage spawn JSON format error (spawns array): {sourceLabel}");
            return null;
        }

        string arrayBody = spawnsMatch.Groups[1].Value;
        List<string> objectTexts = SplitTopLevelJsonObjects(arrayBody);
        if (objectTexts == null)
        {
            Debug.LogError($"Stage spawn JSON parse error: {sourceLabel}");
            return null;
        }

        var list01 = new List<EnemySpawnEntry>(objectTexts.Count);
        var list11 = new List<Enemy11SpawnEntry>(objectTexts.Count);
        var list12 = new List<Enemy12SpawnEntry>(objectTexts.Count);

        for (int i = 0; i < objectTexts.Count; i++)
        {
            string objectText = objectTexts[i];
            if (!TryReadRequiredJsonStringAnyKey(objectText, out string enemyType, "enemyType", "enemy_type"))
            {
                continue;
            }

            if (enemyType == Enemy01Name)
            {
                if (!TryReadRequiredJsonFloatAnyKey(objectText, out float spawnTime, "spawnTime", "spawn_time_sec") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float initialExpandTime, "initialExpandTime", "initial_expand_time") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float initialStopTime, "initialStopTime", "initial_stop_time") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float moveSpeed, "moveSpeed", "move_speed") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float objectDirection, "objectDirection", "object_direction") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float objectAngle, "objectAngle", "object_angle") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float objectThickness, "objectThickness", "object_thickness") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float scaleRate, "scaleRate", "scale_rate"))
                {
                    Debug.LogError($"Enemy01 JSON entry error at index {i}: required keys missing or invalid.");
                    continue;
                }

                list01.Add(new EnemySpawnEntry
                {
                    enemyType = Enemy01Name,
                    spawnTime = spawnTime,
                    initialExpandTime = initialExpandTime,
                    initialStopTime = initialStopTime,
                    moveSpeed = moveSpeed,
                    objectDirection = objectDirection,
                    objectAngle = objectAngle,
                    objectThickness = objectThickness,
                    scaleRate = scaleRate
                });
                continue;
            }

            if (enemyType == Enemy11Name)
            {
                float sizeMultiplier = 1f;
                TryReadOptionalJsonFloatAnyKey(objectText, 1f, out sizeMultiplier, "size");

                if (!TryReadRequiredJsonFloatAnyKey(objectText, out float spawnTime, "spawnTime", "spawn_time_sec") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float spawnX, "spawnX", "spawn_x") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float spawnY, "spawnY", "spawn_y") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float e11MoveSpeed, "moveSpeed", "speed", "move_speed") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float directionDeg, "directionDeg", "direction_deg"))
                {
                    Debug.LogError($"Enemy11 JSON entry error at index {i}: required keys missing or invalid.");
                    continue;
                }

                list11.Add(new Enemy11SpawnEntry
                {
                    spawnTime = spawnTime,
                    spawnX = spawnX,
                    spawnY = spawnY,
                    moveSpeed = e11MoveSpeed,
                    directionDeg = directionDeg,
                    size = sizeMultiplier
                });
                continue;
            }

            if (enemyType == Enemy12Name)
            {
                if (!TryReadRequiredJsonFloatAnyKey(objectText, out float spawnTime, "spawnTime", "spawn_time_sec") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float spawnX, "spawnX", "spawn_x") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float spawnY, "spawnY", "spawn_y") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float directionDeg, "moveDirectionDeg", "move_direction_deg") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float baseSpeed, "baseSpeed", "base_speed") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float homingStrength, "playerHomingStrength", "player_homing_strength") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float maxTurnRateDegPerSec, "maxTurnRateDegPerSec", "max_turn_rate_deg_per_sec") ||
                    !TryReadRequiredJsonFloatAnyKey(objectText, out float size, "size") ||
                    !TryReadRequiredJsonStringAnyKey(objectText, out string spriteName, "spriteName", "sprite_name"))
                {
                    Debug.LogError($"Enemy12 JSON entry error at index {i}: required keys missing or invalid.");
                    continue;
                }

                list12.Add(new Enemy12SpawnEntry
                {
                    spawnTime = spawnTime,
                    spawnX = spawnX,
                    spawnY = spawnY,
                    directionDeg = directionDeg,
                    baseSpeed = baseSpeed,
                    homingStrength = homingStrength,
                    maxTurnRateDegPerSec = maxTurnRateDegPerSec,
                    size = size,
                    spriteName = spriteName
                });
            }
        }

        EnemySpawnEntry[] arr01 = list01.ToArray();
        Enemy11SpawnEntry[] arr11 = list11.ToArray();
        Enemy12SpawnEntry[] arr12 = list12.ToArray();
        System.Array.Sort(arr01, (a, b) => a.spawnTime.CompareTo(b.spawnTime));
        System.Array.Sort(arr11, (a, b) => a.spawnTime.CompareTo(b.spawnTime));
        System.Array.Sort(arr12, (a, b) => a.spawnTime.CompareTo(b.spawnTime));

        var payload = new StageSpawnPayload
        {
            Enemy01Entries = arr01,
            Enemy11Entries = arr11,
            Enemy12Entries = arr12
        };

#if UNITY_EDITOR
        Debug.Log(
            $"[GameManager] Stage spawns loaded: {sourceLabel} | Enemy01={arr01.Length}, Enemy11={arr11.Length}, Enemy12={arr12.Length}" +
            (arr01.Length > 0
                ? $" | Enemy01 spawnTime [{arr01[0].spawnTime:F1} … {arr01[arr01.Length - 1].spawnTime:F1}]"
                : string.Empty));
#endif

        return payload;
    }

    private static List<string> SplitTopLevelJsonObjects(string arrayBody)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(arrayBody))
        {
            return results;
        }

        int depth = 0;
        int startIndex = -1;
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < arrayBody.Length; i++)
        {
            char c = arrayBody[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                if (depth == 0)
                {
                    startIndex = i;
                }

                depth++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                if (depth < 0)
                {
                    return null;
                }

                if (depth == 0 && startIndex >= 0)
                {
                    results.Add(arrayBody.Substring(startIndex, i - startIndex + 1));
                    startIndex = -1;
                }
            }
        }

        return depth == 0 ? results : null;
    }

    private static bool TryReadRequiredJsonFloat(string objectText, string key, out float value)
    {
        value = 0f;
        string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)";
        Match match = Regex.Match(objectText, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        return TryParseFloat(match.Groups[1].Value, out value);
    }

    private static bool TryReadRequiredJsonString(string objectText, string key, out string value)
    {
        value = null;
        string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"";
        Match match = Regex.Match(objectText, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        value = match.Groups[1].Value.Trim();
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryReadRequiredJsonFloatAnyKey(string objectText, out float value, params string[] keys)
    {
        value = 0f;
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryReadRequiredJsonFloat(objectText, keys[i], out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadRequiredJsonStringAnyKey(string objectText, out string value, params string[] keys)
    {
        value = null;
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryReadRequiredJsonString(objectText, keys[i], out value))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryReadOptionalJsonFloatAnyKey(string objectText, float defaultValue, out float value, params string[] keys)
    {
        value = defaultValue;
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryReadRequiredJsonFloat(objectText, keys[i], out float parsed))
            {
                value = parsed;
                return;
            }
        }
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void SpawnEnemy01(EnemySpawnEntry entry)
    {
        if (obstaclesRoot == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(
                $"[GameManager] SpawnEnemy01 skipped (obstaclesRoot null) spawnTime={entry.spawnTime:F2}s");
#endif
            return;
        }

        GameObject enemyObject;
        if (enemy01Prefab != null)
        {
            enemyObject = Instantiate(enemy01Prefab, obstaclesRoot);
            enemyObject.name = Enemy01Name;
            enemyObject.transform.localPosition = Vector3.zero;
            enemyObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            enemyObject = new GameObject(Enemy01Name);
            enemyObject.transform.SetParent(obstaclesRoot, false);
            enemyObject.transform.localPosition = Vector3.zero;
            enemyObject.AddComponent<PolygonCollider2D>();
            enemyObject.AddComponent<MeshFilter>();
            enemyObject.AddComponent<MeshRenderer>();
            enemyObject.AddComponent<Enemy01Controller>();
        }

        Enemy01Controller enemyController = enemyObject.GetComponent<Enemy01Controller>();
        if (enemyController == null)
        {
            enemyController = enemyObject.AddComponent<Enemy01Controller>();
        }

        enemyController.SetMaxSimultaneousCount(enemy01MaxSimultaneous);
        enemyController.ApplySpawnSettings(
            entry.initialExpandTime,
            entry.initialStopTime,
            entry.moveSpeed,
            entry.objectDirection,
            entry.objectAngle,
            entry.objectThickness,
            entry.scaleRate);
    }

    private void SpawnEnemy11(float yOffset)
    {
        if (obstaclesRoot == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        float zDist = Mathf.Abs(cam.transform.position.z);
        Vector3 leftCenter = cam.ViewportToWorldPoint(new Vector3(-0.1f, 0.5f, zDist));
        leftCenter.z = 0f;
        leftCenter.y += yOffset;

        GameObject enemyObject = enemy11Prefab != null
            ? Instantiate(enemy11Prefab, obstaclesRoot)
            : new GameObject(Enemy11Name);
        enemyObject.name = Enemy11Name;
        enemyObject.transform.SetParent(obstaclesRoot, false);
        enemyObject.transform.position = leftCenter;
        enemyObject.transform.localScale = Vector3.one;

        Enemy11Controller controller = enemyObject.GetComponent<Enemy11Controller>();
        if (controller == null)
        {
            controller = enemyObject.AddComponent<Enemy11Controller>();
        }
        controller.SetMaxSimultaneousCount(enemy11MaxSimultaneous);
        controller.SetupMovement(enemy11MoveSpeed, 0f);
        controller.ApplySizeMultiplier(1f);
    }

    private void SpawnEnemy11(Enemy11SpawnEntry entry)
    {
        if (obstaclesRoot == null)
        {
            return;
        }

        GameObject enemyObject = enemy11Prefab != null
            ? Instantiate(enemy11Prefab, obstaclesRoot)
            : new GameObject(Enemy11Name);
        enemyObject.name = Enemy11Name;
        enemyObject.transform.SetParent(obstaclesRoot, false);
        enemyObject.transform.position = new Vector3(entry.spawnX, entry.spawnY, 0f);
        enemyObject.transform.localScale = Vector3.one;

        Enemy11Controller controller = enemyObject.GetComponent<Enemy11Controller>();
        if (controller == null)
        {
            controller = enemyObject.AddComponent<Enemy11Controller>();
        }
        controller.SetMaxSimultaneousCount(enemy11MaxSimultaneous);
        controller.SetupMovement(entry.moveSpeed, entry.directionDeg);
        controller.ApplySizeMultiplier(entry.size);
    }

    private void SpawnEnemy12(Enemy12SpawnEntry entry)
    {
        if (obstaclesRoot == null)
        {
            return;
        }

        GameObject enemyObject = enemy12Prefab != null
            ? Instantiate(enemy12Prefab, obstaclesRoot)
            : new GameObject(Enemy12Name);
        enemyObject.name = Enemy12Name;
        enemyObject.transform.SetParent(obstaclesRoot, false);
        enemyObject.transform.position = new Vector3(entry.spawnX, entry.spawnY, 0f);
        enemyObject.transform.localScale = Vector3.one;

        Enemy11Controller legacyEnemy11Controller = enemyObject.GetComponent<Enemy11Controller>();
        if (legacyEnemy11Controller != null)
        {
            legacyEnemy11Controller.enabled = false;
            Destroy(legacyEnemy11Controller);
        }

        Enemy12Controller controller = enemyObject.GetComponent<Enemy12Controller>();
        if (controller == null)
        {
            if (enemyObject.GetComponent<SpriteRenderer>() == null)
            {
                enemyObject.AddComponent<SpriteRenderer>();
            }

            if (enemyObject.GetComponent<CircleCollider2D>() == null)
            {
                enemyObject.AddComponent<CircleCollider2D>();
            }

            controller = enemyObject.AddComponent<Enemy12Controller>();
        }

        controller.SetMaxSimultaneousCount(enemy12MaxSimultaneous);
        controller.SetupMovement(entry.baseSpeed, entry.directionDeg, entry.homingStrength, entry.maxTurnRateDegPerSec);
        controller.ApplySizeMultiplier(entry.size);

        Sprite sprite = ResolveEnemySpriteByName(entry.spriteName);
        if (sprite != null)
        {
            controller.ApplySprite(sprite);
        }
    }

    private IEnumerator StageClearSequenceCoroutine()
    {
        bool timeClearParachute = hasControlStartTime &&
            clearTimeSeconds > 0f &&
            (Time.time - controlStartTimeSeconds) >= clearTimeSeconds;

        stageCleared = true;
        canTransitionToMenuByClickAfterClear = false;
        isClearMenuTransitioning = false;
        isRebounding = false;
        reboundTween?.Kill();

        DestroyObstacleEnemyInstancesUnderObstaclesRoot(true);
        Enemy01Controller.ResyncSimultaneousSpawnCountFromScene();

        game01BgmManager?.StopStageBgm();

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        if (fallVisualEffectController != null)
        {
            fallVisualEffectController.HandleStageClear();
        }

        game01BgmManager?.PlayStageClearBgm();

        game01TransitionManager?.ShowGameClearedPanel();

        if (gameClearedPanelController != null)
        {
            gameClearedPanelController.ShowStageCleared(currentStageName);
        }

        if (playerRigidbody != null)
        {
            float toStart = Mathf.Max(0.01f, returnToGameplayStartSeconds);
            bool reachedStart = false;
            Sequence returnSequence = DOTween.Sequence();
            returnSequence
                .Join(playerRigidbody.DOMove(gameplayStartPosition, toStart).SetEase(Ease.InOutQuad).SetUpdate(UpdateType.Fixed))
                .Join(playerRigidbody.DORotate(gameplayStartRotationDeg, toStart).SetEase(Ease.InOutQuad).SetUpdate(UpdateType.Fixed))
                .OnComplete(() => reachedStart = true)
                .OnKill(() => reachedStart = true);

            while (!reachedStart)
            {
                yield return null;
            }
        }

        if (playerSpriteRenderer != null && playerClearedSprite != null)
        {
            playerSpriteRenderer.sprite = playerClearedSprite;
        }

        if (timeClearParachute)
        {
            EnsureTimeClearParachuteVisualRig();
        }

        Camera cam = Camera.main;
        Vector2 center = Vector2.zero;
        if (cam != null)
        {
            float playerZ = playerRigidbody != null ? playerRigidbody.transform.position.z : 0f;
            float zDist = Mathf.Abs(cam.transform.position.z - playerZ);
            Vector3 centerWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, zDist));
            center = new Vector2(centerWorld.x, centerWorld.y);
        }

        float moveDuration = Mathf.Max(0.01f, moveToCenterSeconds);
        float scaleMin = Mathf.Max(0.01f, clearedScaleMinimum);
        bool clearMotionFinished = false;
        canTransitionToMenuByClickAfterClear = true;
        Sequence seq = DOTween.Sequence();
        if (playerRigidbody != null)
        {
            seq.Join(playerRigidbody.DOMove(center, moveDuration).SetEase(Ease.InOutSine).SetUpdate(UpdateType.Fixed));
        }

        if (playerController != null)
        {
            Vector3 targetScale = gameplayStartScale * scaleMin;
            seq.Join(playerController.transform.DOScale(targetScale, moveDuration).SetEase(Ease.InOutSine));
        }

        seq.OnComplete(() => clearMotionFinished = true).OnKill(() => clearMotionFinished = true);
        float swayPhaseStartTime = Time.time;
        while (!clearMotionFinished)
        {
            if (timeClearParachute && timeClearParachuteSwayPivot != null)
            {
                float period = Mathf.Max(0.05f, timeClearParachuteSwayPeriodSeconds);
                float maxDeg = Mathf.Abs(timeClearParachuteMaxSwayDegrees);
                float elapsedSway = Time.time - swayPhaseStartTime;
                float omega = (Mathf.PI * 2f) / period;
                float zDeg = maxDeg * Mathf.Sin(elapsedSway * omega);
                timeClearParachuteSwayPivot.localEulerAngles = new Vector3(0f, 0f, zDeg);
            }

            yield return null;
        }

        CleanupTimeClearParachuteVisualRig();
        stageClearRoutine = null;
    }

    private void EnsureTimeClearParachuteVisualRig()
    {
        if (timeClearParachuteSwayPivot != null || playerController == null || playerSpriteRenderer == null)
        {
            return;
        }

        Transform root = playerController.transform;
        Bounds b = playerSpriteRenderer.bounds;
        float pivotWorldY = b.center.y + b.extents.y - 0.6f * b.size.y;
        Vector3 pivotWorld = new Vector3(b.center.x, pivotWorldY, b.center.z);
        Vector3 pivotLocal = root.InverseTransformPoint(pivotWorld);

        GameObject pivotObject = new GameObject("TimeClearParachuteSwayPivot");
        GameObject carryObject = new GameObject("TimeClearParachuteCarry");
        timeClearParachuteSwayPivot = pivotObject.transform;
        timeClearParachuteSwayPivot.SetParent(root, false);
        timeClearParachuteSwayPivot.localPosition = pivotLocal;
        timeClearParachuteSwayPivot.localRotation = Quaternion.identity;
        timeClearParachuteSwayPivot.localScale = Vector3.one;

        Transform carryTransform = carryObject.transform;
        carryTransform.SetParent(timeClearParachuteSwayPivot, false);
        carryTransform.localPosition = -pivotLocal;
        carryTransform.localRotation = Quaternion.identity;
        carryTransform.localScale = Vector3.one;

        timeClearParachuteProxySpriteRenderer = carryObject.AddComponent<SpriteRenderer>();
        CopySpriteRendererForParachuteProxy(playerSpriteRenderer, timeClearParachuteProxySpriteRenderer);
        playerSpriteRenderer.enabled = false;
    }

    private static void CopySpriteRendererForParachuteProxy(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.maskInteraction = source.maskInteraction;
        destination.material = source.material;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.spriteSortPoint = source.spriteSortPoint;
    }

    private void CleanupTimeClearParachuteVisualRig()
    {
        if (timeClearParachuteProxySpriteRenderer != null && playerSpriteRenderer != null)
        {
            playerSpriteRenderer.sprite = timeClearParachuteProxySpriteRenderer.sprite;
            playerSpriteRenderer.color = timeClearParachuteProxySpriteRenderer.color;
            playerSpriteRenderer.flipX = timeClearParachuteProxySpriteRenderer.flipX;
            playerSpriteRenderer.flipY = timeClearParachuteProxySpriteRenderer.flipY;
        }

        if (timeClearParachuteSwayPivot != null)
        {
            Destroy(timeClearParachuteSwayPivot.gameObject);
            timeClearParachuteSwayPivot = null;
        }

        timeClearParachuteProxySpriteRenderer = null;

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.enabled = true;
        }
    }

    private void TryHandleStageClearMenuClickTransition()
    {
        if (!stageCleared || !canTransitionToMenuByClickAfterClear || isClearMenuTransitioning)
        {
            return;
        }

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (!clicked)
        {
            return;
        }

        isClearMenuTransitioning = true;
        if (game01TransitionManager != null)
        {
            game01TransitionManager.TransitionToMenuScene();
        }
    }

    private void TryMarkControlStartTime()
    {
        if (hasControlStartTime)
        {
            return;
        }

        if (!waitForStageIntro || stageIntroComplete)
        {
            if (!waitForStageIntro)
            {
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game01_01);
            }

            hasControlStartTime = true;
            controlStartTimeSeconds = Time.time;
            midgameBackgroundEnemySpawnSuppressors = CollectMidgameBackgroundEnemySpawnSuppressors();
            NotifyFireScreenGameplayStarted();
            NotifyCityScreenGameplayStarted();
        }
    }

    private void NotifyFireScreenGameplayStarted()
    {
        FireScreenController[] fireScreens = FindObjectsByType<FireScreenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < fireScreens.Length; i++)
        {
            fireScreens[i].OnGameplayControlStarted(this);
        }
    }

    private void NotifyCityScreenGameplayStarted()
    {
        CityScreenController[] cityScreens = FindObjectsByType<CityScreenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cityScreens.Length; i++)
        {
            cityScreens[i].OnGameplayControlStarted(this);
        }
    }

    private void TryAutoStageClear()
    {
        if (stageCleared || stageClearRoutine != null)
        {
            return;
        }

        if (playerLife != null && playerLife.IsGameOver)
        {
            return;
        }

        bool clearByTime = hasControlStartTime &&
            clearTimeSeconds > 0f &&
            (Time.time - controlStartTimeSeconds) >= clearTimeSeconds;

        bool stageSpawnsCompleted = hasAnyStageSpawn &&
            (!enemy01SpawnPlanned || enemy01SpawnCompleted) &&
            (!enemy11SpawnPlanned || enemy11SpawnCompleted) &&
            (!enemy12SpawnPlanned || enemy12SpawnCompleted);
        bool noEnemyRemaining = AreAllEnemiesGone();
        bool clearBySpawnsAndEnemyGone = stageSpawnsCompleted && noEnemyRemaining;

        if (clearByTime || clearBySpawnsAndEnemyGone)
        {
            TriggerStageClear();
        }
    }

    private static bool AreAllEnemiesGone()
    {
        Enemy01Controller[] enemy01 = FindObjectsByType<Enemy01Controller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemy01 != null && enemy01.Length > 0)
        {
            return false;
        }

        Enemy11Controller[] enemy11 = FindObjectsByType<Enemy11Controller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemy11 != null && enemy11.Length > 0)
        {
            return false;
        }

        Enemy12Controller[] enemy12 = FindObjectsByType<Enemy12Controller>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return enemy12 == null || enemy12.Length == 0;
    }

    private void EnsureEnemy01Prefab()
    {
        if (enemy01Prefab != null)
        {
            return;
        }

#if UNITY_EDITOR
        enemy01Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy01.prefab");
#endif
    }

    private void EnsureEnemy11Sprite()
    {
        if (enemy11Sprite != null || enemy11Prefab == null)
        {
            return;
        }

#if UNITY_EDITOR
        enemy11Sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/enemy_02.png");
#endif
    }

    private void EnsureEnemy11Prefab()
    {
        if (enemy11Prefab != null)
        {
            return;
        }

#if UNITY_EDITOR
        enemy11Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy11.prefab");
#endif
    }

    private void EnsureEnemy12Prefab()
    {
        if (enemy12Prefab != null)
        {
            return;
        }

#if UNITY_EDITOR
        enemy12Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy12.prefab");
#endif
    }

    private static Sprite ResolveEnemySpriteByName(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return null;
        }

        string fileName = Path.GetFileName(spriteName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        Sprite sprite = Resources.Load<Sprite>($"enemy/{fileNameWithoutExtension}");
        if (sprite != null)
        {
            return sprite;
        }

#if UNITY_EDITOR
        if (!fileName.EndsWith(".png"))
        {
            fileName += ".png";
        }

        sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/enemy/{fileName}");
        if (sprite != null)
        {
            return sprite;
        }
#endif

        Debug.LogWarning($"Enemy sprite not found: {spriteName}");
        return null;
    }

    [System.Serializable]
    private class EnemySpawnEntry
    {
        public string enemyType;
        public float spawnTime;
        public float initialExpandTime;
        public float initialStopTime;
        public float moveSpeed;
        public float objectDirection;
        public float objectAngle;
        public float objectThickness;
        public float scaleRate;
    }

    [System.Serializable]
    private class Enemy11SpawnEntry
    {
        public float spawnTime;
        public float spawnX;
        public float spawnY;
        public float moveSpeed;
        public float directionDeg;
        public float size;
    }

    [System.Serializable]
    private class Enemy12SpawnEntry
    {
        public float spawnTime;
        public float spawnX;
        public float spawnY;
        public float directionDeg;
        public float baseSpeed;
        public float homingStrength;
        public float maxTurnRateDegPerSec;
        public float size;
        public string spriteName;
    }
}

// Backward-compatible alias for existing script asset GUID.
public class GameManager : Game01Manager
{
}
