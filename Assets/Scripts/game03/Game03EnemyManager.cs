using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct Game03EnemyPrefabSpawnTemplate
{
    public readonly float CoreRadius;
    public readonly int MaxLife;
    public readonly int TouchDamage;
    public readonly int ExperiencePoints;
    public readonly Game03ExpTier ExperienceTier;
    public readonly bool IsBossEnemy;

    public Game03EnemyPrefabSpawnTemplate(
        float coreRadius,
        int maxLife,
        int touchDamage,
        int experiencePoints,
        Game03ExpTier experienceTier,
        bool isBossEnemy)
    {
        CoreRadius = coreRadius;
        MaxLife = maxLife;
        TouchDamage = touchDamage;
        ExperiencePoints = experiencePoints;
        ExperienceTier = experienceTier;
        IsBossEnemy = isBossEnemy;
    }
}

public class Game03EnemyManager : MonoBehaviour
{
    private enum MoveSteering
    {
        Homing = 0,
        Directional = 1,
        LightHoming = 2
    }

    private sealed class EnemyState
    {
        public int Id;
        public RectTransform Rect;
        public float CoreRadius;
        public float MoveSpeedWorld;
        public int TouchDamage;
        public int RemainingHp;
        public MoveSteering Steering;
        public Vector2 FixedDirection;
        public float LightHomingBlend;
        public bool P5Fast;
        public float LifetimeRemaining;
        public float ExtraSpeedMultiplier;
        /// <summary>フェーズ JSON の entries 添字。-1 は未追跡。</summary>
        public int PhaseSpawnEntryIndex = -1;
        public Game03EnemyOffscreenCullMode OffscreenCullMode;
        public float OffscreenCullMarginLocal;
        public float SpawnMoveHoldRemaining;
        public float SpawnFadeInRemaining;
        public float SpawnFadeInTotal;
        public int ExperienceDropAmount;
        public Game03ExpTier ExpTier;
        public string SpawnEnemyKind = string.Empty;
        public int SpawnPatternId = -1;
        public int PendingKillWeaponNumber;
        public float KnockbackResistancePercent;
        public bool IsBossEnemy;
    }

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03UnitManager game03UnitManager;
    [SerializeField, Tooltip("未設定でも可。設定時、デバッグの敵非出現モードを参照する。")]
    private Game03DebugManager game03DebugManager;
    [SerializeField] private RectTransform enemyRoot;
    [SerializeField] private RectTransform enemyTemplate;
    [SerializeField, Tooltip("Enemy00ImagePrefab〜（0番＝Enemy00、JSON enemyKind の番号と一致）。未設定・範囲外スロットは enemyTemplate にフォールバック。クランプしない。")]
    private RectTransform[] enemyPrefabsByEnemyNumber;
    [SerializeField, Tooltip("中BOSS 湧き（出現種別51／JSON の Boss01）。Assets/Prefabs/game03/BossMiddle01ImagePrefab を割り当てる。")]
    private RectTransform bossMiddle01ImagePrefab;
    [SerializeField, Tooltip("大BOSS 湧き（出現種別61／JSON の Boss51）。Assets/Prefabs/game03/Boss01ImagePrefab を割り当てる。")]
    private RectTransform boss01ImagePrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Game03ExperienceFieldController experienceFieldController;
    [SerializeField, Tooltip("出現種別51・61 撃破時の ItemBossCanvas ドロップ。")]
    private Game03BossDropItemFieldController bossDropItemFieldController;
    [SerializeField] private Game03StatusManager statusManager;
    [SerializeField] private Game03SeManager game03SeManager;

    [Header("Enemy Settings")]
    [SerializeField, Min(0.01f)] private float fallbackEnemyCoreRadius = 30f;
    [SerializeField, Tooltip("Game03EnemyStats が無い場合のプレイヤー速度比（段階）。")]
    private Game03EnemyMoveSpeedTier fallbackEnemyMoveSpeedTier = Game03EnemyMoveSpeedTier.Medium;
    [SerializeField, Min(1)] private int fallbackEnemyTouchDamage = 5;
    [SerializeField] private bool resolveEnemyEnemyOverlap = true;
    [SerializeField, Min(0)] private int maxEnemyEnemyPairChecksPerFrame = 800;

    [Header("武器ノックバック調整")]
    [SerializeField, Range(0f, 1f), Tooltip("ボス敵に対する武器ノックバック距離の倍率（0 で無効）。")]
    private float bossWeaponKnockbackMultiplier = 0.25f;
    [SerializeField, Min(0f), Tooltip("コア同士がこの倍率×(r1+r2)以内の別敵を「群れ」と数え、ノックバックを減衰させる。")]
    private float knockbackCrowdNeighborTouchScale = 1.05f;
    [SerializeField, Min(0f), Tooltip("群れ1体あたりのノックペナルティ係数 k。倍率 1/(1+k×隣接数）。")]
    private float knockbackCrowdPenaltyPerNeighbor = 0.22f;

    [Header("Enemy defeat presentation")]
    [SerializeField, Min(0.05f)] private float defeatBodyDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float defeatFragmentLifetime = 0.4f;
    [SerializeField, Min(0.5f)] private float defeatSquashWidthMul = 1.18f;
    [SerializeField, Min(0.02f)] private float defeatSquashHeightMul = 0.14f;
    [SerializeField, Range(2, 12)] private int defeatFragmentCount = 5;
    [SerializeField, Range(0, 8)] private int defeatBossExtraFragments = 3;
    [SerializeField] private Vector2 defeatFragmentSizeRange = new Vector2(9f, 16f);
    [SerializeField, Min(0f)] private float defeatFragmentSpeedMin = 90f;
    [SerializeField, Min(0f)] private float defeatFragmentSpeedMax = 240f;
    [SerializeField, Min(0f)] private float defeatFragmentVelocityDrag = 1.35f;

    [Header("後方フォールオフ削除（【11b】）")]
    [SerializeField] private bool enableRearFalloffDelete = true;
    [SerializeField, Min(5f)] private float rearFalloffHistoryWindowSeconds = 20f;
    [SerializeField, Min(0.5f)] private float rearFalloffDirectionEvaluateIntervalSeconds = 3f;
    [SerializeField, Min(1f)] private float rearFalloffDominanceThresholdPx = 480f;
    [SerializeField, Min(0f)] private float rearFalloffBehindMarginPx = 80f;
    [SerializeField, Range(0.25f, 1f)] private float rearFalloffMinSeparationWidthFraction = 0.5f;
    [SerializeField, Min(120f)] private float rearFalloffMinSeparationPxFloor = 320f;

    [Header("Debug")]
    [SerializeField, Tooltip(
        "武器ヒット判定を Console に出力（鎌など「当たってないのか／ダメージだけか」の切り分け用）。ヒット 0 件のフレームは各敵の距離も出します。通常は OFF。")]
    private bool debugLogWeaponHitProbe;

    private readonly Game03ScrollProgressTracker scrollProgressTracker = new Game03ScrollProgressTracker();
    private readonly List<EnemyState> enemies = new List<EnemyState>(128);
    private readonly List<RectTransform> defeatScrollRects = new List<RectTransform>(64);
    private float localUnitsPerWorldUnit = 1f;
    private int nextEnemyId = 1;
    private IGame03EnemySpawnAliveSink phaseSpawnAliveSink;

    public RectTransform EnemyRoot => enemyRoot;

    /// <summary>フェーズスポーン（種別51・中BOSS）用。未設定なら null。</summary>
    public RectTransform PhaseMidBossSpawnPrefab => bossMiddle01ImagePrefab;

    /// <summary>フェーズスポーン（種別61・大BOSS）用。未設定なら null。</summary>
    public RectTransform PhaseFinalBossSpawnPrefab => boss01ImagePrefab;

    public Rect GetVisibleLocalRectOnEnemyRoot()
    {
        return GetRootVisibleLocalRect();
    }

    public string RearFalloffScrollDebugLabel => scrollProgressTracker.LockedAxisDebugLabel;

    public int GetActiveEnemyCount()
    {
        return enemies.Count;
    }

    /// <summary>フェーズ出現のエントリ別生存数カウント用。Game03EnemySpawnManager から登録する。</summary>
    public void SetPhaseSpawnAliveSink(IGame03EnemySpawnAliveSink sink)
    {
        phaseSpawnAliveSink = sink;
    }

    /// <summary>
    /// フェーズ切替（phaseResetFlag が false）時に、生存中の敵を新 JSON の entries 添字から切り離す。
    /// 消滅通知は送らない（SpawnManager 側で alive 配列を組み直すため）。
    /// </summary>
    public void ClearPhaseSpawnTrackingOnAllEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy != null)
            {
                enemy.PhaseSpawnEntryIndex = -1;
            }
        }
    }

    /// <summary>
    /// 新フェーズ出現用。prefabSlot0Based は Enemy00→0, Enemy01→1 … と enemyPrefabsByEnemyNumber の添字に対応（シーン配列も同じ順で割り当てること）。
    /// prefabOverride が非 null のときはスロットの代わりにそのプレハブを複製する（中BOSS／大BOSS 等）。
    /// </summary>
    public bool TryReadPrefabSpawnTemplate(
        int prefabSlot0Based,
        RectTransform prefabOverride,
        out Game03EnemyPrefabSpawnTemplate template)
    {
        template = default;
        RectTransform prefab = prefabOverride != null ? prefabOverride : GetEnemyPrefabBySlot(prefabSlot0Based);
        if (prefab == null)
        {
            return false;
        }

        Game03EnemyStats stats = prefab.GetComponent<Game03EnemyStats>();
        if (stats == null)
        {
            return false;
        }

        int experiencePoints = stats.ExperienceValue;
        if (experiencePoints <= 0)
        {
            experiencePoints = statusManager != null
                ? statusManager.GetExperienceTierBase(stats.ExperienceTier)
                : 1;
        }

        template = new Game03EnemyPrefabSpawnTemplate(
            stats.CoreRadius,
            stats.MaxLife,
            stats.TouchDamage,
            experiencePoints,
            stats.ExperienceTier,
            stats.IsBossEnemy);
        return true;
    }

    public bool TrySpawnPhaseEnemy(
        int prefabSlot0Based,
        Vector2 anchoredPosition,
        Vector2 direction,
        float lifetimeSeconds,
        int effectiveMaxLife,
        int experienceDropAmount,
        int steering,
        float speedTierMult,
        float kSpeed,
        int phaseSpawnEntryIndex = -1,
        Game03EnemyOffscreenCullMode offscreenCull = Game03EnemyOffscreenCullMode.None,
        float offscreenCullMarginLocal = 120f,
        RectTransform prefabOverride = null,
        float spawnHoldMoveSeconds = 0f,
        float spawnFadeInSeconds = 0f,
        string spawnEnemyKind = "",
        int spawnPatternId = -1)
    {
        if (enemyRoot == null || game03UnitManager == null)
        {
            return false;
        }

        RectTransform rect = prefabOverride != null
            ? InstantiatePhaseEnemyFromPrefab(prefabOverride, "PhaseBoss")
            : CreateEnemyRectFromPrefabSlot(prefabSlot0Based, "Phase");
        if (rect == null)
        {
            return false;
        }

        rect.gameObject.SetActive(true);
        rect.anchoredPosition = anchoredPosition;
        float vp = Mathf.Max(1f, game03UnitManager.MoveSpeedWorldPerSecond);
        Game03EnemyStats stats = rect.GetComponent<Game03EnemyStats>();
        float tierRatio = stats != null
            ? stats.PlayerSpeedRatio
            : Game03EnemyMoveSpeedTierUtil.ToPlayerSpeedRatio(fallbackEnemyMoveSpeedTier);
        float ve = vp * Mathf.Max(0.01f, tierRatio) * Mathf.Max(0.01f, speedTierMult) * Mathf.Max(0.01f, kSpeed);
        ve *= 1f;

        if (stats != null)
        {
            stats.ApplyRuntimeSpawnConfig(ve, effectiveMaxLife);
        }

        Game03ExpTier tier = stats != null ? stats.ExperienceTier : Game03ExpTier.Red;
        MoveSteering st = (MoveSteering)Mathf.Clamp(steering, 0, 2);
        Vector2 dirN = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float fadeTotal = Mathf.Max(0f, spawnFadeInSeconds);
        if (fadeTotal > 0.0001f)
        {
            SetEnemyImagesAlphaRecursive(rect, 0f);
        }

        enemies.Add(new EnemyState
        {
            Id = nextEnemyId++,
            Rect = rect,
            CoreRadius = stats != null ? Mathf.Max(0.01f, stats.CoreRadius) : 30f,
            MoveSpeedWorld = ve,
            TouchDamage = stats != null ? Mathf.Max(1, stats.TouchDamage) : 1,
            RemainingHp = Mathf.Max(1, effectiveMaxLife),
            ExperienceDropAmount = Mathf.Max(1, experienceDropAmount),
            ExpTier = tier,
            Steering = st,
            FixedDirection = dirN,
            LightHomingBlend = st == MoveSteering.LightHoming ? 0.18f : 0f,
            P5Fast = false,
            LifetimeRemaining = lifetimeSeconds <= 0f ? -1f : lifetimeSeconds,
            ExtraSpeedMultiplier = 1f,
            PhaseSpawnEntryIndex = phaseSpawnEntryIndex,
            OffscreenCullMode = offscreenCull,
            OffscreenCullMarginLocal = offscreenCullMarginLocal,
            SpawnMoveHoldRemaining = Mathf.Max(0f, spawnHoldMoveSeconds),
            SpawnFadeInRemaining = fadeTotal,
            SpawnFadeInTotal = fadeTotal > 0.0001f ? fadeTotal : 0f,
            SpawnEnemyKind = spawnEnemyKind ?? string.Empty,
            SpawnPatternId = spawnPatternId,
            KnockbackResistancePercent = stats != null ? Mathf.Clamp(stats.KnockbackResistancePercent, 0f, 100f) : 0f,
            IsBossEnemy = stats != null && stats.IsBossEnemy
        });

        Game03CombatBalanceLogger.TryGet()?.RecordEnemySpawn(spawnEnemyKind, spawnPatternId);
        return true;
    }

    /// <summary>敵出現種別51 用 SE。クリップ未設定なら何もしない。</summary>
    public void TryPlayMidBossSpawnAppearSe()
    {
        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.TryPlayMidBossSpawnWarningSe();
    }

    /// <summary>敵出現種別61 用 SE。クリップ未設定なら何もしない。</summary>
    public void TryPlayFinalBossSpawnAppearSe()
    {
        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.TryPlayFinalBossSpawnWarningSe();
    }

    private RectTransform GetEnemyPrefabBySlot(int prefabSlot0Based)
    {
        if (prefabSlot0Based < 0)
        {
            return enemyTemplate;
        }

        if (enemyPrefabsByEnemyNumber != null
            && prefabSlot0Based < enemyPrefabsByEnemyNumber.Length)
        {
            RectTransform prefab = enemyPrefabsByEnemyNumber[prefabSlot0Based];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return enemyTemplate;
    }

    private void Awake()
    {
        if (enemyRoot == null || game03UnitManager == null)
        {
            return;
        }

        PrepareTemplate();
    }

    private void Start()
    {
        if (enemyRoot == null || game03UnitManager == null)
        {
            return;
        }

        UpdateLocalUnitsPerWorldUnit();
    }

    private void Update()
    {
        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            SetPlayerUnitContactDamageLoop(false);
            return;
        }

        if (enemyRoot == null || game03UnitManager == null)
        {
            SetPlayerUnitContactDamageLoop(false);
            return;
        }

        UpdateLocalUnitsPerWorldUnit();
        ApplyWorldScrollFromPlayerMove();

        if (!game03UnitManager.TryGetUnitCenterOnRect(enemyRoot, out Vector2 playerCenter))
        {
            return;
        }

        float dt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;

        TickScrollProgressTracker(dt);
        MoveEnemies(playerCenter, dt);
        ProcessOffscreenCullDeletes();
        ProcessRearFalloffCullDeletes(playerCenter);
        if (resolveEnemyEnemyOverlap)
        {
            ResolveEnemyEnemyOverlap();
        }

        ResolvePlayerEnemyOverlapAndDamage(playerCenter);
        DespawnExpiredEnemies(dt);
        Game03CombatBalanceLogger.TryGet()?.RecordEnemyCountSnapshot(enemies.Count);
    }

    private void PrepareTemplate()
    {
        if (enemyTemplate == null)
        {
            enemyTemplate = enemyRoot;
        }

        if (enemyTemplate != null)
        {
            enemyTemplate.gameObject.SetActive(false);
        }
    }

    private RectTransform CreateEnemyRectFromPrefabSlot(int prefabSlot0Based, string debugLabel)
    {
        RectTransform prefab = GetEnemyPrefabBySlot(prefabSlot0Based);
        string label = string.IsNullOrEmpty(debugLabel) ? $"Enemy{prefabSlot0Based:D2}" : debugLabel;
        if (prefab != null)
        {
            return InstantiatePhaseEnemyFromPrefab(prefab, label);
        }

        return CreateEnemyRectFromPrefabIndex(0, label);
    }

    private RectTransform CreateEnemyRectFromPrefabIndex(int prefabIndex0Based, string debugLabel)
    {
        RectTransform prefab = null;
        if (enemyPrefabsByEnemyNumber != null &&
            prefabIndex0Based >= 0 &&
            prefabIndex0Based < enemyPrefabsByEnemyNumber.Length)
        {
            prefab = enemyPrefabsByEnemyNumber[prefabIndex0Based];
        }

        RectTransform created = null;
        if (prefab != null)
        {
            created = Instantiate(prefab, enemyRoot);
        }
        else if (enemyTemplate != null && enemyTemplate != enemyRoot)
        {
            created = Instantiate(enemyTemplate, enemyRoot);
        }
        else
        {
            GameObject go = new GameObject($"{debugLabel}_{nextEnemyId:000}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = LayerMask.NameToLayer("Enemy");
            created = go.GetComponent<RectTransform>();
            created.SetParent(enemyRoot, false);
            created.sizeDelta = new Vector2(80f, 80f);
            Image image = go.GetComponent<Image>();
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            image.color = new Color(0.95f, 0.35f, 0.35f, 1f);
        }

        created.gameObject.layer = LayerMask.NameToLayer("Enemy");
        created.localScale = Vector3.one;
        if (created.GetComponent<Game03EnemyStats>() == null)
        {
            created.gameObject.AddComponent<Game03EnemyStats>();
        }

        return created;
    }

    private RectTransform InstantiatePhaseEnemyFromPrefab(RectTransform srcPrefab, string debugLabel)
    {
        if (srcPrefab == null || enemyRoot == null)
        {
            return null;
        }

        RectTransform created = Instantiate(srcPrefab, enemyRoot);
        created.gameObject.layer = LayerMask.NameToLayer("Enemy");
        created.localScale = Vector3.one;
        if (created.GetComponent<Game03EnemyStats>() == null)
        {
            created.gameObject.AddComponent<Game03EnemyStats>();
        }

        return created;
    }

    private static void SetEnemyImagesAlphaRecursive(RectTransform root, float alpha01)
    {
        if (root == null)
        {
            return;
        }

        var images = root.GetComponentsInChildren<Image>(true);
        float a = Mathf.Clamp01(alpha01);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
            {
                continue;
            }

            Color c = img.color;
            c.a = a;
            img.color = c;
        }

        var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text t = tmpTexts[i];
            if (t == null)
            {
                continue;
            }

            Color c = t.color;
            c.a = a;
            t.color = c;
        }
    }

    private void MoveEnemies(Vector2 playerCenter, float dt)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            if (enemy.SpawnFadeInTotal > 0.0001f)
            {
                if (enemy.SpawnFadeInRemaining > 0f)
                {
                    enemy.SpawnFadeInRemaining -= dt;
                    float u = 1f - Mathf.Clamp01(enemy.SpawnFadeInRemaining / enemy.SpawnFadeInTotal);
                    SetEnemyImagesAlphaRecursive(enemy.Rect, u);
                }
                else
                {
                    SetEnemyImagesAlphaRecursive(enemy.Rect, 1f);
                    enemy.SpawnFadeInTotal = 0f;
                }
            }

            if (enemy.SpawnMoveHoldRemaining > 0f)
            {
                enemy.SpawnMoveHoldRemaining -= dt;
                continue;
            }

            float speed = enemy.MoveSpeedWorld * Mathf.Max(1f, enemy.ExtraSpeedMultiplier);
            float localStep = speed * localUnitsPerWorldUnit * dt;
            Vector2 current = enemy.Rect.anchoredPosition;
            Vector2 delta;
            switch (enemy.Steering)
            {
                case MoveSteering.Directional:
                    delta = enemy.FixedDirection * localStep;
                    break;
                case MoveSteering.LightHoming:
                {
                    Vector2 toPlayer = playerCenter - current;
                    Vector2 hom = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : enemy.FixedDirection;
                    Vector2 blended = Vector2.Lerp(enemy.FixedDirection, hom, enemy.LightHomingBlend).normalized;
                    delta = blended * localStep;
                    break;
                }
                default:
                {
                    Vector2 toPlayer = playerCenter - current;
                    if (toPlayer.sqrMagnitude <= 0.0001f)
                    {
                        delta = Vector2.zero;
                    }
                    else
                    {
                        delta = toPlayer.normalized * localStep;
                    }

                    break;
                }
            }

            enemy.Rect.anchoredPosition = current + delta;
        }
    }

    private void TickScrollProgressTracker(float gameplayDeltaTime)
    {
        if (game03UnitManager == null)
        {
            return;
        }

        Vector2 worldDelta = game03UnitManager.LastAppliedFieldDeltaWorld;
        float localDeltaX = worldDelta.x * localUnitsPerWorldUnit;
        scrollProgressTracker.Tick(
            gameplayDeltaTime,
            localDeltaX,
            rearFalloffHistoryWindowSeconds,
            rearFalloffDirectionEvaluateIntervalSeconds,
            rearFalloffDominanceThresholdPx);
        Game03CombatBalanceLogger.TryGet()?.RecordScrollDelta(localDeltaX);
    }

    private void ProcessOffscreenCullDeletes()
    {
        if (enemyRoot == null)
        {
            return;
        }

        Rect vis = GetRootVisibleLocalRect();
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.OffscreenCullMode != Game03EnemyOffscreenCullMode.DeleteBeyondVisibleMargin &&
                enemy.OffscreenCullMode != Game03EnemyOffscreenCullMode.VanishBeyondVisibleMargin)
            {
                continue;
            }

            float m = Mathf.Max(24f, enemy.OffscreenCullMarginLocal);
            Vector2 p = enemy.Rect.anchoredPosition;
            float cr = enemy.CoreRadius;
            if (p.x + cr < vis.xMin - m || p.x - cr > vis.xMax + m || p.y + cr < vis.yMin - m || p.y - cr > vis.yMax + m)
            {
                Game03EnemyRemovalKind kind = enemy.OffscreenCullMode == Game03EnemyOffscreenCullMode.VanishBeyondVisibleMargin
                    ? Game03EnemyRemovalKind.Vanish
                    : Game03EnemyRemovalKind.Delete;
                RemoveTrackedEnemyAtIndex(i, kind);
            }
        }
    }

    /// <summary>出現種別04/05/06/07/12は後方フォールオフ削除の対象外（画面外・後方の自動消滅なし方針）。</summary>
    private static bool IsSpawnPatternExemptFromRearFalloffDelete(int spawnPatternId)
    {
        return spawnPatternId is 4 or 5 or 6 or 7 or 12;
    }

    private void ProcessRearFalloffCullDeletes(Vector2 playerCenter)
    {
        if (!enableRearFalloffDelete || enemyRoot == null)
        {
            return;
        }

        Game03ScrollProgressTracker.DominantAxis axis = scrollProgressTracker.LockedAxis;
        if (axis == Game03ScrollProgressTracker.DominantAxis.None)
        {
            return;
        }

        Rect vis = GetRootVisibleLocalRect();
        float minSep = Mathf.Max(rearFalloffMinSeparationPxFloor, vis.width * rearFalloffMinSeparationWidthFraction);
        float behindMargin = Mathf.Max(0f, rearFalloffBehindMarginPx);

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            if (enemy.OffscreenCullMode != Game03EnemyOffscreenCullMode.None)
            {
                continue;
            }

            if (enemy.Steering == MoveSteering.Directional)
            {
                continue;
            }

            if (IsSpawnPatternExemptFromRearFalloffDelete(enemy.SpawnPatternId))
            {
                continue;
            }

            Game03EnemyStats stats = enemy.Rect.GetComponent<Game03EnemyStats>();
            if (stats != null && stats.IsBossEnemy)
            {
                continue;
            }

            Vector2 pos = enemy.Rect.anchoredPosition;
            Vector2 delta = pos - playerCenter;
            if (delta.sqrMagnitude < minSep * minSep)
            {
                continue;
            }

            bool isBehind = axis == Game03ScrollProgressTracker.DominantAxis.Right
                ? pos.x <= playerCenter.x - behindMargin
                : pos.x >= playerCenter.x + behindMargin;
            if (!isBehind)
            {
                continue;
            }

            RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.Delete, rearFalloffDelete: true);
        }
    }

    private void NotifyPhaseSpawnSink(EnemyState enemy)
    {
        if (phaseSpawnAliveSink == null || enemy == null || enemy.PhaseSpawnEntryIndex < 0)
        {
            return;
        }

        phaseSpawnAliveSink.OnPhaseSpawnedEnemyRemoved(enemy.PhaseSpawnEntryIndex);
    }

    private void RemoveTrackedEnemyAtIndex(
        int index,
        Game03EnemyRemovalKind kind,
        bool rearFalloffDelete = false,
        int weaponNumberForKill = 0,
        bool phaseResetVanish = false,
        bool bombKill = false)
    {
        EnemyState enemy = enemies[index];
        RectTransform rt = enemy.Rect;
        Game03EnemyStats stats = rt != null ? rt.GetComponent<Game03EnemyStats>() : null;
        if (weaponNumberForKill > 0)
        {
            enemy.PendingKillWeaponNumber = weaponNumberForKill;
        }

        ReportEnemyRemovalTelemetry(enemy, kind, rearFalloffDelete, phaseResetVanish, bombKill);
        NotifyPhaseSpawnSink(enemy);
        enemies.RemoveAt(index);
        ApplyEnemyRemovalKind(rt, stats, kind, enemy);
    }

    private static void ReportEnemyRemovalTelemetry(
        EnemyState enemy,
        Game03EnemyRemovalKind kind,
        bool rearFalloffDelete,
        bool phaseResetVanish,
        bool bombKill)
    {
        if (enemy == null)
        {
            return;
        }

        Game03CombatBalanceLogger logger = Game03CombatBalanceLogger.TryGet();
        if (logger == null)
        {
            return;
        }

        logger.RecordEnemyRemoval(
            enemy.SpawnEnemyKind,
            enemy.SpawnPatternId,
            kind,
            rearFalloffDelete,
            phaseResetVanish,
            bombKill);
        if (!bombKill &&
            enemy.PendingKillWeaponNumber > 0 &&
            (kind == Game03EnemyRemovalKind.DefeatWeapon || kind == Game03EnemyRemovalKind.DefeatWeaponSuppressLevelProgress))
        {
            logger.RecordWeaponKill(enemy.PendingKillWeaponNumber, enemy.SpawnEnemyKind, enemy.SpawnPatternId);
        }
    }

    private void DespawnExpiredEnemies(float dt)
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.LifetimeRemaining < 0f)
            {
                continue;
            }

            enemy.LifetimeRemaining -= dt;
            if (enemy.LifetimeRemaining <= 0f)
            {
                RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.Vanish);
            }
        }
    }

    private void ResolveEnemyEnemyOverlap()
    {
        int checkedPairs = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState a = enemies[i];
            if (a?.Rect == null)
            {
                continue;
            }

            for (int j = i + 1; j < enemies.Count; j++)
            {
                if (checkedPairs >= maxEnemyEnemyPairChecksPerFrame)
                {
                    return;
                }

                checkedPairs++;
                EnemyState b = enemies[j];
                if (b?.Rect == null)
                {
                    continue;
                }

                Vector2 delta = b.Rect.anchoredPosition - a.Rect.anchoredPosition;
                float minDistance = a.CoreRadius + b.CoreRadius;
                float sqr = delta.sqrMagnitude;
                if (sqr <= 0.000001f || sqr >= minDistance * minDistance)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(sqr);
                Vector2 normal = delta / distance;
                float push = (minDistance - distance) * 0.5f;
                a.Rect.anchoredPosition -= normal * push;
                b.Rect.anchoredPosition += normal * push;
            }
        }
    }

    private void ResolvePlayerEnemyOverlapAndDamage(Vector2 playerCenter)
    {
        float playerCore = game03UnitManager.PlayerCoreRadius;
        bool killOnContact = game03DebugManager != null && game03DebugManager.EffectiveKillEnemyOnPlayerContact;
        bool inDamagingContact = false;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            Vector2 delta = enemy.Rect.anchoredPosition - playerCenter;
            float minDistance = enemy.CoreRadius + playerCore;
            float sqr = delta.sqrMagnitude;
            if (sqr < 0.000001f)
            {
                delta = Vector2.right;
                sqr = 1f;
            }

            if (sqr < minDistance * minDistance)
            {
                if (killOnContact)
                {
                    RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.DefeatWeaponSuppressLevelProgress);
                    continue;
                }

                if (enemy.TouchDamage > 0)
                {
                    inDamagingContact = true;
                }

                float distance = Mathf.Sqrt(sqr);
                Vector2 normal = delta / distance;
                float push = minDistance - distance;
                enemy.Rect.anchoredPosition += normal * push;
                if (enemy.TouchDamage > 0)
                {
                    game03UnitManager.ApplyDamage(enemy.TouchDamage);
                }
            }
        }

        SetPlayerUnitContactDamageLoop(inDamagingContact);
    }

    private void SetPlayerUnitContactDamageLoop(bool inDamagingContact)
    {
        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.SetPlayerUnitContactDamageLoopActive(inDamagingContact);
    }

    private Rect GetRootVisibleLocalRect()
    {
        Camera eventCamera = GetUiEventCamera(enemyRoot);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            enemyRoot,
            new Vector2(0f, 0f),
            eventCamera,
            out Vector2 bottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            enemyRoot,
            new Vector2(Screen.width, Screen.height),
            eventCamera,
            out Vector2 topRight);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    private Camera GetUiEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return worldCamera;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
    }

    private void UpdateLocalUnitsPerWorldUnit()
    {
        if (enemyRoot == null || worldCamera == null || !worldCamera.orthographic)
        {
            localUnitsPerWorldUnit = 1f;
            return;
        }

        Rect localVisible = GetRootVisibleLocalRect();
        float worldVisibleHeight = worldCamera.orthographicSize * 2f;
        if (worldVisibleHeight <= 0.0001f || Mathf.Abs(localVisible.height) <= 0.0001f)
        {
            localUnitsPerWorldUnit = 1f;
            return;
        }

        localUnitsPerWorldUnit = Mathf.Abs(localVisible.height) / worldVisibleHeight;
    }

    private void ApplyWorldScrollFromPlayerMove()
    {
        Vector2 worldDelta = game03UnitManager.LastAppliedFieldDeltaWorld;
        if (worldDelta.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        Vector2 localDelta = worldDelta * localUnitsPerWorldUnit;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            enemy.Rect.anchoredPosition += localDelta;
        }

        for (int i = defeatScrollRects.Count - 1; i >= 0; i--)
        {
            RectTransform r = defeatScrollRects[i];
            if (r == null)
            {
                defeatScrollRects.RemoveAt(i);
                continue;
            }

            r.anchoredPosition += localDelta;
        }
    }

    public Rect ConvertRectToEnemyRootSpace(RectTransform rect)
    {
        if (enemyRoot == null || rect == null)
        {
            return default;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera eventCamera = GetUiEventCamera(enemyRoot);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(enemyRoot, RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]), eventCamera, out Vector2 p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(enemyRoot, RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]), eventCamera, out Vector2 p2);
        return Rect.MinMaxRect(Mathf.Min(p0.x, p2.x), Mathf.Min(p0.y, p2.y), Mathf.Max(p0.x, p2.x), Mathf.Max(p0.y, p2.y));
    }

    private void TryApplyWeaponKnockbackFromPlayer(EnemyState enemy, float knockbackStrengthPx)
    {
        if (knockbackStrengthPx <= 0.0001f || enemy?.Rect == null || enemyRoot == null || game03UnitManager == null)
        {
            return;
        }

        float k = knockbackStrengthPx;
        if (enemy.IsBossEnemy)
        {
            k *= Mathf.Clamp01(bossWeaponKnockbackMultiplier);
        }

        k *= 1f - Mathf.Clamp01(enemy.KnockbackResistancePercent / 100f);
        int crowdNeighbors = CountKnockbackCrowdingNeighbors(enemy);
        if (crowdNeighbors > 0)
        {
            k /= 1f + knockbackCrowdPenaltyPerNeighbor * crowdNeighbors;
        }

        if (k <= 0.0001f)
        {
            return;
        }

        if (!game03UnitManager.TryGetUnitCenterOnRect(enemyRoot, out Vector2 playerCenter))
        {
            return;
        }

        Vector2 pos = enemy.Rect.anchoredPosition;
        Vector2 away = pos - playerCenter;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.right;
        }

        enemy.Rect.anchoredPosition = pos + away.normalized * k;
    }

    private int CountKnockbackCrowdingNeighbors(EnemyState self)
    {
        if (self?.Rect == null)
        {
            return 0;
        }

        Vector2 p = self.Rect.anchoredPosition;
        float selfR = Mathf.Max(0.01f, self.CoreRadius);
        float scale = Mathf.Max(0.01f, knockbackCrowdNeighborTouchScale);
        int n = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState o = enemies[i];
            if (o == null || o.Id == self.Id || o.Rect == null)
            {
                continue;
            }

            float otherR = Mathf.Max(0.01f, o.CoreRadius);
            float threshold = (selfR + otherR) * scale;
            float d = Vector2.Distance(p, o.Rect.anchoredPosition);
            if (d < threshold)
            {
                n++;
            }
        }

        return n;
    }

    public int ApplyWeaponHitRect(
        Rect hitRectOnEnemyRoot,
        bool continuousHit,
        HashSet<int> alreadyHitEnemyIds,
        int damagePerHit,
        float knockbackStrengthPx,
        out int damagedButAliveCount,
        int telemetryWeaponNumber = 0)
    {
        damagedButAliveCount = 0;
        int killed = 0;

        List<(int id, Vector2 pos, float coreR, int hp)> probeSnap = null;
        if (debugLogWeaponHitProbe)
        {
            probeSnap = new List<(int, Vector2, float, int)>(enemies.Count);
            for (int s = 0; s < enemies.Count; s++)
            {
                EnemyState e = enemies[s];
                if (e?.Rect == null)
                {
                    continue;
                }

                probeSnap.Add((e.Id, e.Rect.anchoredPosition, e.CoreRadius, e.RemainingHp));
            }
        }

        int dupSkips = 0;
        int geoMisses = 0;
        int damageApplications = 0;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            if (!continuousHit && alreadyHitEnemyIds != null && alreadyHitEnemyIds.Contains(enemy.Id))
            {
                dupSkips++;
                continue;
            }

            Vector2 center = enemy.Rect.anchoredPosition;
            float closestX = Mathf.Clamp(center.x, hitRectOnEnemyRoot.xMin, hitRectOnEnemyRoot.xMax);
            float closestY = Mathf.Clamp(center.y, hitRectOnEnemyRoot.yMin, hitRectOnEnemyRoot.yMax);
            float dx = center.x - closestX;
            float dy = center.y - closestY;
            if (dx * dx + dy * dy > enemy.CoreRadius * enemy.CoreRadius)
            {
                geoMisses++;
                continue;
            }

            if (alreadyHitEnemyIds != null)
            {
                alreadyHitEnemyIds.Add(enemy.Id);
            }

            int dmg = Mathf.Max(1, damagePerHit);
            int hpBefore = enemy.RemainingHp;
            RecordWeaponDamageTelemetry(telemetryWeaponNumber, dmg, hpBefore);
            enemy.RemainingHp -= dmg;
            damageApplications++;

            TryPlayEnemyWeaponHitSe(continuousHit, enemy.Rect);
            TryApplyWeaponKnockbackFromPlayer(enemy, knockbackStrengthPx);

            if (enemy.RemainingHp <= 0)
            {
                RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.DefeatWeapon, weaponNumberForKill: telemetryWeaponNumber);
                killed++;
            }
            else
            {
                damagedButAliveCount++;
            }
        }

        if (debugLogWeaponHitProbe)
        {
            float rw = hitRectOnEnemyRoot.width;
            float rh = hitRectOnEnemyRoot.height;
            bool rectDegenerate = rw <= 0.001f || rh <= 0.001f;
            Debug.Log(
                $"[Game03WeaponHit][Rect] frame={Time.frameCount} dmg={damagePerHit} knockPx={knockbackStrengthPx:F1} continuous={continuousHit} " +
                $"rect=({hitRectOnEnemyRoot.xMin:F1},{hitRectOnEnemyRoot.yMin:F1})-({hitRectOnEnemyRoot.xMax:F1},{hitRectOnEnemyRoot.yMax:F1}) " +
                $"size=({rw:F1}x{rh:F1}) degenerate={rectDegenerate} listedEnemies={probeSnap?.Count ?? 0} dupSkip={dupSkips} geoMiss={geoMisses} " +
                $"dmgApply={damageApplications} killed={killed} chipped={damagedButAliveCount}");

            if (damageApplications == 0 && probeSnap != null && probeSnap.Count > 0)
            {
                var sb = new StringBuilder(256);
                sb.Append("[Game03WeaponHit][Rect][Probe zero hits] per enemy (enemyRoot space):\n");
                for (int p = 0; p < probeSnap.Count; p++)
                {
                    (int id, Vector2 pos, float coreR, int hp) = probeSnap[p];
                    bool isDupSkip = !continuousHit && alreadyHitEnemyIds != null && alreadyHitEnemyIds.Contains(id);
                    float cx = Mathf.Clamp(pos.x, hitRectOnEnemyRoot.xMin, hitRectOnEnemyRoot.xMax);
                    float cy = Mathf.Clamp(pos.y, hitRectOnEnemyRoot.yMin, hitRectOnEnemyRoot.yMax);
                    float edx = pos.x - cx;
                    float edy = pos.y - cy;
                    float distSq = edx * edx + edy * edy;
                    float thrSq = coreR * coreR;
                    bool geoWouldHit = distSq <= thrSq;
                    sb.Append(
                        $"  id={id} hp={hp} pos=({pos.x:F1},{pos.y:F1}) coreR={coreR:F1} dupSkip={isDupSkip} distSq={distSq:F0} thrSq={thrSq:F0} geoWouldHit={geoWouldHit}\n");
                }

                sb.Append(
                    $"  → dupSkip は「同一スイング内すでにヒット済み」。geoWouldHit=false が続くなら当たり矩形と敵コアが離れています。");
                Debug.Log(sb.ToString());
            }
        }

        return killed;
    }

    public int ApplyWeaponHitCircle(
        Vector2 centerOnEnemyRoot,
        float radiusOnEnemyRoot,
        bool continuousHit,
        HashSet<int> alreadyHitEnemyIds,
        int damagePerHit,
        out int damagedButAliveCount,
        int telemetryWeaponNumber = 0,
        float knockbackStrengthPx = 0f)
    {
        damagedButAliveCount = 0;
        int killed = 0;
        float zoneRadius = Mathf.Max(0.0001f, radiusOnEnemyRoot);
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            if (!continuousHit && alreadyHitEnemyIds != null && alreadyHitEnemyIds.Contains(enemy.Id))
            {
                continue;
            }

            Vector2 delta = enemy.Rect.anchoredPosition - centerOnEnemyRoot;
            float hitRadius = zoneRadius + enemy.CoreRadius;
            if (delta.sqrMagnitude > hitRadius * hitRadius)
            {
                continue;
            }

            if (alreadyHitEnemyIds != null)
            {
                alreadyHitEnemyIds.Add(enemy.Id);
            }

            int dmg = Mathf.Max(1, damagePerHit);
            int hpBefore = enemy.RemainingHp;
            RecordWeaponDamageTelemetry(telemetryWeaponNumber, dmg, hpBefore);
            enemy.RemainingHp -= dmg;

            TryPlayEnemyWeaponHitSe(continuousHit, enemy.Rect);
            TryApplyWeaponKnockbackFromPlayer(enemy, knockbackStrengthPx);
            if (enemy.RemainingHp <= 0)
            {
                RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.DefeatWeapon, weaponNumberForKill: telemetryWeaponNumber);
                killed++;
            }
            else
            {
                damagedButAliveCount++;
            }
        }

        return killed;
    }

    /// <summary>
    /// ダメージなし。黒球のパルスノックなど用。
    /// </summary>
    public void ApplyWeaponKnockbackCircle(Vector2 centerOnEnemyRoot, float radiusOnEnemyRoot, float knockbackStrengthPx)
    {
        if (knockbackStrengthPx <= 0.0001f)
        {
            return;
        }

        float zoneRadius = Mathf.Max(0.0001f, radiusOnEnemyRoot);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            Vector2 delta = enemy.Rect.anchoredPosition - centerOnEnemyRoot;
            float hitRadius = zoneRadius + enemy.CoreRadius;
            if (delta.sqrMagnitude > hitRadius * hitRadius)
            {
                continue;
            }

            TryApplyWeaponKnockbackFromPlayer(enemy, knockbackStrengthPx);
        }
    }

    private void TryPlayEnemyWeaponHitSe(bool continuousHit, RectTransform enemyRect)
    {
        if (continuousHit || enemyRect == null)
        {
            return;
        }

        Game03EnemyStats stats = enemyRect.GetComponent<Game03EnemyStats>();
        bool isBoss = stats != null && stats.IsBossEnemy;
        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.PlayEnemyWeaponHitSe(isBoss);
    }

    private static Game03ExpTier ResolveEnemyExpTier(EnemyState enemy, Game03EnemyStats stats)
    {
        if (enemy != null)
        {
            return enemy.ExpTier;
        }

        return stats != null ? stats.ExperienceTier : Game03ExpTier.Red;
    }

    private static int ResolveEnemyExperienceDropBase(EnemyState enemy, Game03EnemyStats stats)
    {
        if (enemy != null && enemy.ExperienceDropAmount > 0)
        {
            return enemy.ExperienceDropAmount;
        }

        if (stats != null && stats.ExperienceValue > 0)
        {
            return stats.ExperienceValue;
        }

        return 1;
    }

    private void TrySpawnBossRewardDrop(RectTransform enemyRect, EnemyState enemyState)
    {
        if (bossDropItemFieldController == null || enemyRect == null || enemyState == null)
        {
            return;
        }

        int spawnPatternId = enemyState.SpawnPatternId;
        if (spawnPatternId is not (51 or 61))
        {
            return;
        }

        bossDropItemFieldController.TrySpawnDropForDefeatedEnemy(enemyRect, spawnPatternId);
    }

    private void ApplyEnemyRemovalKind(
        RectTransform enemyRect,
        Game03EnemyStats stats,
        Game03EnemyRemovalKind kind,
        EnemyState enemyState)
    {
        if (enemyRect == null)
        {
            return;
        }

        switch (kind)
        {
            case Game03EnemyRemovalKind.DefeatWeapon:
                if (statusManager != null)
                {
                    statusManager.NotifyEnemyKilled();
                }

                {
                    bool isBoss = stats != null && stats.IsBossEnemy;
                    Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
                    se?.PlayEnemyDefeatSe(isBoss);
                }

                if (experienceFieldController != null)
                {
                    Game03ExpTier tier = ResolveEnemyExpTier(enemyState, stats);
                    int prefabBase = ResolveEnemyExperienceDropBase(enemyState, stats);
                    int amount = statusManager != null
                        ? statusManager.ComputeExperienceDropAmount(prefabBase)
                        : Mathf.Max(1, prefabBase);
                    experienceFieldController.SpawnPickup(enemyRect, amount, tier);
                }

                TrySpawnBossRewardDrop(enemyRect, enemyState);

                StartCoroutine(EnemyDefeatEffectRoutine(enemyRect, stats != null && stats.IsBossEnemy));
                break;

            case Game03EnemyRemovalKind.DefeatWeaponSuppressLevelProgress:
                if (statusManager != null)
                {
                    statusManager.NotifyEnemyKilled();
                    Game03ExpTier tier = ResolveEnemyExpTier(enemyState, stats);
                    int prefabBase = ResolveEnemyExperienceDropBase(enemyState, stats);
                    int amount = statusManager.ComputeExperienceDropAmount(prefabBase);
                    statusManager.AddExperience(amount, suppressLevelProgress: true);
                }

                {
                    bool isBoss = stats != null && stats.IsBossEnemy;
                    Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
                    se?.PlayEnemyDefeatSe(isBoss);
                }

                StartCoroutine(EnemyDefeatEffectRoutine(enemyRect, stats != null && stats.IsBossEnemy));
                break;

            case Game03EnemyRemovalKind.Vanish:
                StartCoroutine(EnemyDefeatEffectRoutine(enemyRect, stats != null && stats.IsBossEnemy));
                break;

            case Game03EnemyRemovalKind.Delete:
                UnregisterDefeatScrollTarget(enemyRect);
                Destroy(enemyRect.gameObject);
                break;
        }
    }

    /// <summary>アクティブな敵をすべて除去（内部リストから外してから各経路を適用）。</summary>
    public void RemoveAllActiveEnemies(Game03EnemyRemovalKind kind, bool phaseResetVanish = false)
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                NotifyPhaseSpawnSink(enemy);
                enemies.RemoveAt(i);
                continue;
            }

            RemoveTrackedEnemyAtIndex(i, kind, phaseResetVanish: phaseResetVanish && kind == Game03EnemyRemovalKind.Vanish);
        }
    }

    /// <summary>指定 Rect がアクティブ敵ならリストから外し、経路を適用する。</summary>
    public bool TryRemoveActiveEnemy(RectTransform enemyRect, Game03EnemyRemovalKind kind)
    {
        if (enemyRect == null)
        {
            return false;
        }

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i]?.Rect != enemyRect)
            {
                continue;
            }

            RemoveTrackedEnemyAtIndex(i, kind);
            return true;
        }

        return false;
    }

    private float GetDefeatEffectDeltaTime()
    {
        if (game03Manager != null && game03Manager.GameplayDeltaTime > 0.000001f)
        {
            return game03Manager.GameplayDeltaTime;
        }

        return Time.unscaledDeltaTime;
    }

    private void RegisterDefeatScrollTarget(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        defeatScrollRects.Add(rect);
    }

    private void UnregisterDefeatScrollTarget(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        defeatScrollRects.Remove(rect);
    }

    private IEnumerator EnemyDefeatEffectRoutine(RectTransform enemyRect, bool isBoss)
    {
        if (enemyRect == null || enemyRoot == null)
        {
            yield break;
        }

        enemyRect.SetAsLastSibling();

        Vector3 scaleStart = enemyRect.localScale;
        Vector3 scaleEnd = new Vector3(
            scaleStart.x * defeatSquashWidthMul,
            scaleStart.y * defeatSquashHeightMul,
            scaleStart.z);

        CanvasGroup enemyGroup = enemyRect.GetComponent<CanvasGroup>();
        if (enemyGroup == null)
        {
            enemyGroup = enemyRect.gameObject.AddComponent<CanvasGroup>();
        }

        enemyGroup.blocksRaycasts = false;
        enemyGroup.interactable = false;

        Image sourceImage = enemyRect.GetComponentInChildren<Image>(true);
        Sprite fragSprite = sourceImage != null ? sourceImage.sprite : null;
        Color fragColor = sourceImage != null ? sourceImage.color : new Color(0.95f, 0.35f, 0.35f, 1f);
        if (fragSprite == null)
        {
            fragSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        RegisterDefeatScrollTarget(enemyRect);

        int fragCount = Mathf.Clamp(defeatFragmentCount + (isBoss ? defeatBossExtraFragments : 0), 2, 16);
        float fragMin = Mathf.Min(defeatFragmentSizeRange.x, defeatFragmentSizeRange.y);
        float fragMax = Mathf.Max(defeatFragmentSizeRange.x, defeatFragmentSizeRange.y);
        float spdMin = Mathf.Min(defeatFragmentSpeedMin, defeatFragmentSpeedMax);
        float spdMax = Mathf.Max(defeatFragmentSpeedMin, defeatFragmentSpeedMax);

        var fragmentRects = new List<RectTransform>(fragCount);
        var fragmentGroups = new List<CanvasGroup>(fragCount);
        var fragmentVels = new List<Vector2>(fragCount);

        for (int f = 0; f < fragCount; f++)
        {
            GameObject go = new GameObject("DefeatFragment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = enemyRect.gameObject.layer;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(enemyRoot, false);
            rt.SetAsLastSibling();
            rt.anchorMin = enemyRect.anchorMin;
            rt.anchorMax = enemyRect.anchorMax;
            rt.pivot = enemyRect.pivot;
            rt.rotation = enemyRect.rotation;
            rt.anchoredPosition = enemyRect.anchoredPosition;
            float fs = Random.Range(fragMin, fragMax);
            rt.sizeDelta = new Vector2(fs, fs * Random.Range(0.55f, 1f));

            Image img = go.GetComponent<Image>();
            img.sprite = fragSprite;
            img.color = fragColor;
            img.raycastTarget = false;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            float ang = Random.Range(0f, Mathf.PI * 2f);
            float spd = Random.Range(spdMin, spdMax);
            fragmentVels.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd);
            fragmentRects.Add(rt);
            fragmentGroups.Add(cg);
            RegisterDefeatScrollTarget(rt);
        }

        float bodyDur = Mathf.Max(0.05f, defeatBodyDuration);
        float fragLife = Mathf.Max(0.05f, defeatFragmentLifetime);
        float t = 0f;

        while (t < fragLife || enemyRect != null)
        {
            float dt = GetDefeatEffectDeltaTime();
            t += dt;

            if (enemyRect != null)
            {
                float u = Mathf.Clamp01(t / bodyDur);
                float sm = u * u * (3f - 2f * u);
                enemyRect.localScale = Vector3.Lerp(scaleStart, scaleEnd, sm);
                enemyGroup.alpha = Mathf.Lerp(1f, 0f, sm);
                if (u >= 1f)
                {
                    UnregisterDefeatScrollTarget(enemyRect);
                    Destroy(enemyRect.gameObject);
                    enemyRect = null;
                }
            }

            float fragAlpha = 1f - Mathf.Clamp01(t / fragLife);
            for (int i = 0; i < fragmentRects.Count; i++)
            {
                RectTransform fr = fragmentRects[i];
                if (fr == null)
                {
                    continue;
                }

                Vector2 v = fragmentVels[i];
                fr.anchoredPosition += v * dt;
                if (defeatFragmentVelocityDrag > 0.0001f)
                {
                    v *= Mathf.Exp(-defeatFragmentVelocityDrag * dt);
                }

                fragmentVels[i] = v;
                CanvasGroup fcg = fragmentGroups[i];
                if (fcg != null)
                {
                    fcg.alpha = fragAlpha;
                }
            }

            yield return null;
        }

        for (int i = 0; i < fragmentRects.Count; i++)
        {
            RectTransform fr = fragmentRects[i];
            if (fr != null)
            {
                UnregisterDefeatScrollTarget(fr);
                Destroy(fr.gameObject);
            }
        }
    }

    /// <summary>
    /// 爆弾アイテム: ビューポート内の通常敵のみ撃破扱い（経験値ドロップあり）。<see cref="Game03EnemyStats.IsBossEnemy"/> は対象外。
    /// </summary>
    public void InstantKillVisibleNormalEnemiesForBombItem(Camera viewportCamera, float viewportMargin)
    {
        Camera cam = viewportCamera != null ? viewportCamera : worldCamera;
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            return;
        }

        float m = Mathf.Clamp01(viewportMargin);

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            Game03EnemyStats stats = enemy.Rect.GetComponent<Game03EnemyStats>();
            if (stats != null && stats.IsBossEnemy)
            {
                continue;
            }

            Vector3 vp = cam.WorldToViewportPoint(enemy.Rect.position);
            if (vp.z <= 0f)
            {
                continue;
            }

            if (vp.x < m || vp.x > 1f - m || vp.y < m || vp.y > 1f - m)
            {
                continue;
            }

            RemoveTrackedEnemyAtIndex(i, Game03EnemyRemovalKind.DefeatWeapon, bombKill: true);
        }
    }

    public bool TryGetNearestEnemyCenter(Vector2 originOnEnemyRoot, out Vector2 centerOnEnemyRoot)
    {
        centerOnEnemyRoot = Vector2.zero;
        float bestSqr = float.MaxValue;
        bool found = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null)
            {
                continue;
            }

            Vector2 p = enemy.Rect.anchoredPosition;
            float sqr = (p - originOnEnemyRoot).sqrMagnitude;
            if (sqr >= bestSqr)
            {
                continue;
            }

            bestSqr = sqr;
            centerOnEnemyRoot = p;
            found = true;
        }

        return found;
    }

    /// <summary>フェーズ JSON 同一 entry 由来の敵の <c>anchoredPosition.x</c> 最小（種別04 距離スポーン用）。</summary>
    public bool TryGetMinAnchoredXForPhaseSpawnEntry(int entryIndex, out float minX)
    {
        minX = 0f;
        bool found = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null || enemy.PhaseSpawnEntryIndex != entryIndex)
            {
                continue;
            }

            float x = enemy.Rect.anchoredPosition.x;
            if (!found || x < minX)
            {
                minX = x;
                found = true;
            }
        }

        return found;
    }

    /// <summary>フェーズ JSON 同一 entry 由来の敵の <c>anchoredPosition.x</c> 最大（種別05 距離スポーン用）。</summary>
    public bool TryGetMaxAnchoredXForPhaseSpawnEntry(int entryIndex, out float maxX)
    {
        maxX = 0f;
        bool found = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (enemy?.Rect == null || enemy.PhaseSpawnEntryIndex != entryIndex)
            {
                continue;
            }

            float x = enemy.Rect.anchoredPosition.x;
            if (!found || x > maxX)
            {
                maxX = x;
                found = true;
            }
        }

        return found;
    }

    private static void RecordWeaponDamageTelemetry(int telemetryWeaponNumber, int damagePerHit, int remainingHpBeforeHit)
    {
        if (telemetryWeaponNumber <= 0)
        {
            return;
        }

        int hpRemoved = Game03RunWeaponDamageTracker.ComputeActualHpRemoved(damagePerHit, remainingHpBeforeHit);
        if (hpRemoved <= 0)
        {
            return;
        }

        Game03RunWeaponDamageTracker.TryGet()?.RecordWeaponHitDamage(telemetryWeaponNumber, hpRemoved);
        Game03CombatBalanceLogger.TryGet()?.RecordWeaponDamage(telemetryWeaponNumber, hpRemoved);
    }
}
