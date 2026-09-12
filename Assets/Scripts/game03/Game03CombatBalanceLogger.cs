using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// enableDebugConsole 時に 1 分ごと戦闘バランスをファイル出力する。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(55)]
public sealed class Game03CombatBalanceLogger : MonoBehaviour
{
    private sealed class MinuteBucket
    {
        public readonly Dictionary<string, int> SpawnsByEnemyKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<int, int> SpawnsBySpawnPatternId = new Dictionary<int, int>();
        public readonly Dictionary<string, int> DefeatsByEnemyKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> VanishByEnemyKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> DeleteByEnemyKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> ItemsByKind = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<int, int> PodsByType = new Dictionary<int, int>();
        public readonly Dictionary<int, int> WeaponDamage = new Dictionary<int, int>();
        public readonly Dictionary<int, int> WeaponKills = new Dictionary<int, int>();
        public int PlayerHits;
        public int RearFalloffDeletes;
        public int PhaseResetVanishCount;
        public int BombKills;
        public int ExperienceGained;
        public int MaxConcurrentEnemies;
        public bool GameOverOccurred;
        public float ScrollRightPx;
        public float ScrollLeftPx;
    }

    private static Game03CombatBalanceLogger instance;

    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03DebugManager game03DebugManager;
    [SerializeField] private Game03StatusManager statusManager;

    private string sessionDirectory;
    private int activeMinuteIndex = -1;
    private MinuteBucket bucket = new MinuteBucket();
    private bool wroteSessionReadme;
    private bool sessionGameOverOccurred;
    private int sessionPopularSnapshot;
    private int sessionLifetimeExpSnapshot;

    public static Game03CombatBalanceLogger TryGet()
    {
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        if (game03Manager == null)
        {
            game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
        }

        if (game03DebugManager == null)
        {
            game03DebugManager = FindAnyObjectByType<Game03DebugManager>(FindObjectsInactive.Include);
        }

        if (statusManager == null)
        {
            statusManager = FindAnyObjectByType<Game03StatusManager>(FindObjectsInactive.Include);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            FlushActiveMinute(force: true);
            WriteSessionSummary();
            instance = null;
        }
    }

    private void Update()
    {
        if (!IsLoggingEnabled() || game03Manager == null)
        {
            return;
        }

        int minute = Mathf.FloorToInt(game03Manager.GameplayElapsedSeconds / 60f);
        if (activeMinuteIndex < 0)
        {
            activeMinuteIndex = minute;
            EnsureSessionDirectory();
            return;
        }

        if (minute != activeMinuteIndex)
        {
            FlushActiveMinute(force: true);
            activeMinuteIndex = minute;
            bucket = new MinuteBucket();
        }
    }

    public bool IsLoggingEnabled()
    {
        return game03DebugManager != null && game03DebugManager.EffectiveEnableDebugConsole;
    }

    public void RecordScrollDelta(float localDeltaX)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        bucket.ScrollRightPx += Mathf.Max(0f, -localDeltaX);
        bucket.ScrollLeftPx += Mathf.Max(0f, localDeltaX);
    }

    public void RecordEnemySpawn(string enemyKind, int spawnPatternId)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        string key = NormalizeEnemyKind(enemyKind, spawnPatternId);
        Increment(bucket.SpawnsByEnemyKind, key);
        if (spawnPatternId >= 0)
        {
            Increment(bucket.SpawnsBySpawnPatternId, spawnPatternId);
        }
    }

    public void RecordEnemyCountSnapshot(int activeEnemyCount)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        if (activeEnemyCount > bucket.MaxConcurrentEnemies)
        {
            bucket.MaxConcurrentEnemies = activeEnemyCount;
        }
    }

    public void RecordEnemyRemoval(
        string enemyKind,
        int spawnPatternId,
        Game03EnemyRemovalKind kind,
        bool rearFalloffDelete,
        bool phaseResetVanish,
        bool bombKill)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        string key = NormalizeEnemyKind(enemyKind, spawnPatternId);
        switch (kind)
        {
            case Game03EnemyRemovalKind.DefeatWeapon:
            case Game03EnemyRemovalKind.DefeatWeaponSuppressLevelProgress:
                Increment(bucket.DefeatsByEnemyKind, key);
                if (bombKill)
                {
                    bucket.BombKills++;
                }

                break;
            case Game03EnemyRemovalKind.Vanish:
                Increment(bucket.VanishByEnemyKind, key);
                if (phaseResetVanish)
                {
                    bucket.PhaseResetVanishCount++;
                }

                break;
            case Game03EnemyRemovalKind.Delete:
                Increment(bucket.DeleteByEnemyKind, key);
                if (rearFalloffDelete)
                {
                    bucket.RearFalloffDeletes++;
                }

                break;
        }
    }

    public void RecordPlayerHit()
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        bucket.PlayerHits++;
    }

    public void RecordGameOver()
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        bucket.GameOverOccurred = true;
        sessionGameOverOccurred = true;
    }

    public void RecordExperienceGained(int amount)
    {
        if (!IsLoggingEnabled() || amount <= 0)
        {
            return;
        }

        bucket.ExperienceGained += amount;
    }

    public void RecordItemPickup(Game03CargoItemKind kind)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        Increment(bucket.ItemsByKind, kind.ToString());
    }

    public void RecordPodPickup(int podType)
    {
        if (!IsLoggingEnabled())
        {
            return;
        }

        Increment(bucket.PodsByType, podType);
    }

    public void RecordWeaponDamage(int weaponNumber, int damage)
    {
        if (!IsLoggingEnabled() || weaponNumber < 1 || weaponNumber > 7 || damage <= 0)
        {
            return;
        }

        Increment(bucket.WeaponDamage, weaponNumber, damage);
    }

    public void RecordWeaponKill(int weaponNumber, string enemyKind, int spawnPatternId)
    {
        if (!IsLoggingEnabled() || weaponNumber < 1 || weaponNumber > 7)
        {
            return;
        }

        Increment(bucket.WeaponKills, weaponNumber);
        _ = enemyKind;
        _ = spawnPatternId;
    }

    private void FlushActiveMinute(bool force)
    {
        if (!force && !IsLoggingEnabled())
        {
            return;
        }

        if (sessionDirectory == null)
        {
            EnsureSessionDirectory();
        }

        if (sessionDirectory == null)
        {
            return;
        }

        int minute = activeMinuteIndex < 0 ? 0 : activeMinuteIndex;
        float t0 = minute * 60f;
        float t1 = t0 + 60f;
        string path = Path.Combine(sessionDirectory, $"minute_{minute:000}.txt");
        try
        {
            int spawnPhase = game03Manager != null
                ? Game03EnemyPhaseTimeline.GetActivePhaseNumberFromGlobalElapsed(game03Manager.GameplayElapsedSeconds)
                : 1;
            int playerLevel = statusManager != null ? statusManager.Level : 1;
            int totalKills = statusManager != null ? statusManager.TotalKills : 0;
            File.WriteAllText(path, BuildMinuteReport(minute, t0, t1, spawnPhase, playerLevel, totalKills, bucket), Encoding.UTF8);
            CaptureEconomySnapshot();
            if (!wroteSessionReadme)
            {
                wroteSessionReadme = true;
                string readme = Path.Combine(sessionDirectory, "README.txt");
                File.WriteAllText(
                    readme,
                    "Game03 combat balance log. One file per gameplay minute while debug console is enabled.\n" +
                    "See session_summary.txt when the session ends (popular + meta grant totals).\n",
                    Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03CombatBalanceLogger] write failed: {ex.Message}");
        }
    }

    private void WriteSessionSummary()
    {
        if (sessionDirectory == null || !Directory.Exists(sessionDirectory))
        {
            return;
        }

        try
        {
            if (statusManager == null)
            {
                statusManager = FindAnyObjectByType<Game03StatusManager>(FindObjectsInactive.Include);
            }

            CaptureEconomySnapshot();
            int popularTotal = statusManager != null
                ? statusManager.PopularDisplayExperienceTotal
                : sessionPopularSnapshot;
            int lifetimeExp = statusManager != null
                ? statusManager.LifetimeTotalExperienceEarned
                : sessionLifetimeExpSnapshot;
            if (statusManager == null && sessionPopularSnapshot > popularTotal)
            {
                popularTotal = sessionPopularSnapshot;
                lifetimeExp = sessionLifetimeExpSnapshot;
            }
            if (game03Manager == null)
            {
                game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
            }

            float elapsedSeconds = game03Manager != null ? game03Manager.GameplayElapsedSeconds : 0f;
            int phaseAtEnd = Game03EnemyPhaseTimeline.GetActivePhaseNumberFromGlobalElapsed(elapsedSeconds);
            var popularityInputs = new Game03MetaEconomyRules.MetaPopularityGrantInputs(
                lifetimeExp,
                elapsedSeconds,
                phaseAtEnd);
            Game03MetaEconomyRules.DecomposeMetaCurrencyFromLifetimeExperience(
                popularityInputs,
                out float timeMultiplier,
                out float phaseMultiplier,
                out long metaFromLifetimeExp);
            long bossBonus = Game03RunSessionState.MidBossAcquisitionPointsTotal
                + Game03RunSessionState.BossAcquisitionPointsTotal;
            long metaBase = ResolveMetaCurrencyBaseReward();
            long metaProjected = metaBase + metaFromLifetimeExp + bossBonus;
            long metaGranted = Game03RunSessionState.LastMetaCurrencyGranted;

            string path = Path.Combine(sessionDirectory, "session_summary.txt");
            var sb = new StringBuilder(512);
            sb.AppendLine("# Game03 combat balance — session summary");
            sb.AppendLine($"game_over_occurred: {sessionGameOverOccurred}");
            sb.AppendLine($"run_ended_by_clear: {Game03RunSessionState.EndedByClear}");
            sb.AppendLine($"run_ended_by_game_over: {Game03RunSessionState.EndedByGameOver}");
            sb.AppendLine($"popular_display_experience_total: {popularTotal}");
            sb.AppendLine($"lifetime_experience_earned: {lifetimeExp}");
            sb.AppendLine($"popular_display_only_bonus: {popularTotal - lifetimeExp}");
            sb.AppendLine(
                $"popular_display_ui_value: {Game03MetaEconomyRules.ComputePopularDisplayValue(popularTotal)}");
            sb.AppendLine($"gameplay_elapsed_seconds: {elapsedSeconds:F1}");
            sb.AppendLine($"enemy_spawn_phase_at_session_end: {phaseAtEnd}");
            sb.AppendLine($"meta_popularity_time_multiplier: {timeMultiplier:F3}");
            sb.AppendLine($"meta_popularity_phase_multiplier: {phaseMultiplier:F3}");
            sb.AppendLine($"meta_currency_granted_total: {metaGranted}");
            sb.AppendLine($"meta_currency_projected_total: {metaProjected}");
            sb.AppendLine($"meta_currency_base_component: {metaBase}");
            sb.AppendLine($"meta_currency_from_lifetime_experience: {metaFromLifetimeExp}");
            sb.AppendLine($"meta_currency_boss_bonus_component: {bossBonus}");
            if (metaGranted != metaProjected)
            {
                sb.AppendLine(
                    "# meta_currency_granted_total differs from projected when the run did not end via clear/game over.");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03CombatBalanceLogger] session summary failed: {ex.Message}");
        }
    }

    private void CaptureEconomySnapshot()
    {
        if (statusManager == null)
        {
            return;
        }

        sessionPopularSnapshot = statusManager.PopularDisplayExperienceTotal;
        sessionLifetimeExpSnapshot = statusManager.LifetimeTotalExperienceEarned;
    }

    private static long ResolveMetaCurrencyBaseReward()
    {
        Game03MetaRunEndCoordinator coordinator =
            FindAnyObjectByType<Game03MetaRunEndCoordinator>(FindObjectsInactive.Include);
        return coordinator != null ? coordinator.MetaCurrencyBaseRewardOnRunEnd : 800L;
    }

    private void EnsureSessionDirectory()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        sessionDirectory = Path.Combine(Application.persistentDataPath, "Game03CombatBalance", $"session_{stamp}");
        try
        {
            Directory.CreateDirectory(sessionDirectory);
            Debug.Log($"[Game03CombatBalanceLogger] Logging to {sessionDirectory}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03CombatBalanceLogger] directory create failed: {ex.Message}");
            sessionDirectory = null;
        }
    }

    private static string BuildMinuteReport(
        int minute,
        float t0,
        float t1,
        int enemySpawnPhase,
        int playerLevel,
        int totalKillsSession,
        MinuteBucket b)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine($"# Game03 combat balance — minute {minute}");
        sb.AppendLine($"timeline: {t0:F0}s — {t1:F0}s");
        sb.AppendLine($"enemy_spawn_phase: {enemySpawnPhase}");
        sb.AppendLine($"player_level_at_minute_end: {playerLevel}");
        sb.AppendLine($"total_kills_session: {totalKillsSession}");
        sb.AppendLine($"player_hits: {b.PlayerHits}");
        sb.AppendLine($"rear_falloff_deletes: {b.RearFalloffDeletes}");
        sb.AppendLine($"phase_reset_vanish_count: {b.PhaseResetVanishCount}");
        sb.AppendLine($"bomb_kills: {b.BombKills}");
        sb.AppendLine($"experience_gained: {b.ExperienceGained}");
        sb.AppendLine($"max_concurrent_enemies: {b.MaxConcurrentEnemies}");
        sb.AppendLine($"game_over_occurred: {b.GameOverOccurred}");
        AppendScrollSummary(sb, b);
        AppendDictSection(sb, "spawns_by_enemy_kind", b.SpawnsByEnemyKind);
        AppendIntDictSection(sb, "spawns_by_spawn_pattern_id", b.SpawnsBySpawnPatternId, "pat");
        AppendDictSection(sb, "defeats_by_enemy_kind", b.DefeatsByEnemyKind);
        AppendDictSection(sb, "vanish_by_enemy_kind", b.VanishByEnemyKind);
        AppendDictSection(sb, "delete_by_enemy_kind", b.DeleteByEnemyKind);
        AppendDictSection(sb, "items_picked_up", b.ItemsByKind);
        AppendIntDictSection(sb, "pods_picked_up", b.PodsByType, "Pod");
        AppendIntDictSection(sb, "weapon_damage_total", b.WeaponDamage, "Weapon");
        AppendIntDictSection(sb, "weapon_kills", b.WeaponKills, "Weapon");
        return sb.ToString();
    }

    private static void AppendScrollSummary(StringBuilder sb, MinuteBucket b)
    {
        float diff = b.ScrollRightPx - b.ScrollLeftPx;
        string dominant = Mathf.Abs(diff) < 1f ? "even" : diff > 0f ? "right" : "left";
        sb.AppendLine($"scroll_right_px: {b.ScrollRightPx:F0}");
        sb.AppendLine($"scroll_left_px: {b.ScrollLeftPx:F0}");
        sb.AppendLine($"scroll_dominant: {dominant} (diff={diff:F0})");
    }

    private static void AppendDictSection(StringBuilder sb, string title, Dictionary<string, int> dict)
    {
        sb.AppendLine($"[{title}]");
        if (dict.Count == 0)
        {
            sb.AppendLine("  (none)");
            return;
        }

        foreach (KeyValuePair<string, int> kv in dict)
        {
            sb.AppendLine($"  {kv.Key}: {kv.Value}");
        }
    }

    private static void AppendIntDictSection(StringBuilder sb, string title, Dictionary<int, int> dict, string prefix)
    {
        sb.AppendLine($"[{title}]");
        if (dict.Count == 0)
        {
            sb.AppendLine("  (none)");
            return;
        }

        var keys = new List<int>(dict.Keys);
        keys.Sort();
        for (int i = 0; i < keys.Count; i++)
        {
            int k = keys[i];
            sb.AppendLine($"  {prefix}{k:00}: {dict[k]}");
        }
    }

    private static string NormalizeEnemyKind(string enemyKind, int spawnPatternId)
    {
        if (!string.IsNullOrWhiteSpace(enemyKind))
        {
            return enemyKind.Trim();
        }

        return spawnPatternId >= 0 ? $"Unknown_pat{spawnPatternId}" : "Unknown";
    }

    private static void Increment(Dictionary<string, int> dict, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            key = "Unknown";
        }

        dict.TryGetValue(key, out int v);
        dict[key] = v + 1;
    }

    private static void Increment(Dictionary<int, int> dict, int key, int add = 1)
    {
        dict.TryGetValue(key, out int v);
        dict[key] = v + add;
    }
}
