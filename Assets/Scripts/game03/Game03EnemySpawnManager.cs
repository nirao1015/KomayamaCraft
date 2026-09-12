using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フェーズ別敵出現。EnemySpawnPhaseNN.json を読み、出現種別 ID ごとにディスパッチする。
/// </summary>
public sealed class Game03EnemySpawnManager : MonoBehaviour, IGame03EnemySpawnAliveSink
{
    private sealed class SpawnEntryTrack
    {
        public int EntryIndex;
        public Game03EnemyPhaseSpawnEntryJson Entry;
        public float NextEligibleTime;
        public bool NextWaveFromRight = true;
        public bool HadFirstSuccessfulSpawn;
        public bool SingleShotWindowDone;
        public float Pattern14AngleRad;
        public float Pattern11AngleRad;
        /// <summary>種別04/05 + <see cref="Game03EnemyPhaseSpawnEntryJson.spawnSpacingPixels"/> 用。隊列端の X 基準（04: minX, 05: maxX）。</summary>
        public float Pattern0405DistanceMilestoneX;
        public bool Pattern0405DistanceMilestoneValid;
    }

    [Header("References")]
    [SerializeField] private Game03EnemyManager enemyManager;
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03UnitManager game03UnitManager;
    [SerializeField, Tooltip("未設定でも可。設定時、デバッグの敵非出現・フェーズ開始・接触撃破を参照する。")]
    private Game03DebugManager game03DebugManager;

    [Header("出現種別01・06・13（左右／壁／前方の画面外オフセット）")]
    [SerializeField, Min(40f), Tooltip("左右画面外に湧かせる際の可視矩形端からの距離（enemyRoot ローカル px）。")]
    private float spawnOutsideMarginLocal = 120f;

    [Header("出現種別02（周辺散らばり）")]
    [SerializeField, Min(0f), Tooltip("可視矩形の半対角線からの最小オフセット（px）。仕様の外周+150 相当。")]
    private float pattern02RingMinExtraPx = 150f;
    [SerializeField, Min(0f), Tooltip("可視矩形の半対角線からの最大オフセット（px）。仕様の外周+1000 相当。")]
    private float pattern02RingMaxExtraPx = 1000f;
    [SerializeField, Range(4, 48), Tooltip("1 体あたりの重なり回避試行回数。")]
    private int pattern02PlacementMaxAttempts = 18;

    [Header("出現種別03（上から塊）")]
    [SerializeField, Min(20f), Tooltip("群れの基準点を可視矩形上辺よりさらに上へ逃がす量（px）。")]
    private float pattern03TopExtraMarginLocal = 96f;
    [SerializeField, Min(24f), Tooltip("種別03の画面外「削除」判定マージン（可視矩形外側 px）。")]
    private float pattern03OffscreenCullMarginLocal = 140f;

    [Header("出現種別12（後方追跡・横画面外クラスタ）")]
    [SerializeField, Min(20f), Tooltip("群れの基準点を可視矩形の後ろ側の辺よりさらに外へ逃がす量（px）。種別03 の上方向オフセットに相当。")]
    private float pattern12SideClusterDepthLocal = 96f;

    [Header("出現種別04・05（一直線）")]
    [SerializeField, Min(24f), Tooltip("種別04/05 の画面外「削除」判定マージン（px）。")]
    private float pattern04OffscreenCullMarginLocal = 120f;

    [Header("出現種別14（画面外一直線）")]
    [SerializeField, Min(24f), Tooltip("種別14 の画面外「消滅」判定マージン（px）。")]
    private float pattern14OffscreenCullMarginLocal = 120f;

    [Header("出現種別08（左から分散・周辺帯）")]
    [SerializeField, Range(4, 48), Tooltip("種別08 の配置試行回数（1 体あたり）。")]
    private int pattern08PlacementMaxAttempts = 18;

    [Header("出現種別10（右から分散・周辺帯）")]
    [SerializeField, Range(4, 48), Tooltip("種別10 の配置試行回数（1 体あたり）。")]
    private int pattern10PlacementMaxAttempts = 18;

    [Header("出現種別11（ランダム瞬間湧き）")]
    [SerializeField, Min(0.04f), Tooltip("フェードイン秒数。")]
    private float pattern11FadeInSeconds = 0.22f;
    [SerializeField, Min(0f), Tooltip("出現後この秒数移動しない（仕様 0.5）。")]
    private float pattern11MoveHoldSeconds = 0.5f;
    [SerializeField, Min(80f), Tooltip("spawnOffsetPx が 0 のとき使う最低距離（px）。")]
    private float pattern11DefaultMinDistancePx = 160f;

    [Header("出現種別51（中BOSS湧き）")]
    [SerializeField, Min(80f), Tooltip("通常の spawnOutsideMargin に上乗せする分で、大柄ボスをさらに外に置く（px）。")]
    private float pattern51ExtraOutsideMarginLocal = 160f;

    [Header("出現種別61（BOSS湧き）")]
    [SerializeField, Min(80f), Tooltip("種別51 よりさらに外側に置く上乗せマージン（px）。")]
    private float pattern61ExtraOutsideMarginLocal = 260f;

    private Game03EnemySpawnPhase01Json phaseData;
    private readonly List<SpawnEntryTrack> spawnTracks = new List<SpawnEntryTrack>(32);
    private int[] aliveByEntryIndex;
    private bool loadFailed;
    private float phaseGlobalStartOffset;
    private int activePhaseNumber = 1;
    private int lastFailedPromoteTargetPhase = -1;

    private void Awake()
    {
        if (enemyManager == null)
        {
            enemyManager = GetComponent<Game03EnemyManager>();
        }

        if (enemyManager == null)
        {
            Debug.LogError("[Game03EnemySpawnManager] Game03EnemyManager が見つかりません。");
            loadFailed = true;
        }
    }

    private void Start()
    {
        if (loadFailed || enemyManager == null)
        {
            return;
        }

        int startPhase = game03DebugManager != null
            ? game03DebugManager.GetEffectiveEnemySpawnStartPhaseNumber()
            : 1;
        phaseGlobalStartOffset = Game03EnemyPhaseTimeline.GetGlobalStartSecondsForPhase(startPhase);

        if (game03Manager != null && phaseGlobalStartOffset > 0.0001f)
        {
            game03Manager.DebugSeedGameplayElapsedSeconds(phaseGlobalStartOffset);
        }

        if (!TryActivatePhase(startPhase, isInitialLoad: true))
        {
            loadFailed = true;
        }
    }

    /// <summary>
    /// 指定フェーズの JSON を読み、出現トラックを組み直す。失敗時は activePhaseNumber を変えない。
    /// </summary>
    private bool TryActivatePhase(int phase1Based, bool isInitialLoad)
    {
        string fileName = Game03EnemyPhaseTimeline.FormatPhaseJsonFileName(phase1Based);
        if (!Game03EnemyPhaseSpawnJsonLoader.TryLoad(fileName, out Game03EnemySpawnPhase01Json newData, out string err))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] {err}");
            return false;
        }

        if (newData.entries == null)
        {
            Debug.LogWarning("[Game03EnemySpawnManager] entries が null です。");
            return false;
        }

        if (!HasAtLeastOneSupportedSpawnEntry(newData))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] {fileName} に対応済み出現種別のエントリがありません。");
            return false;
        }

        phaseData = newData;

        if (phaseData.phaseResetFlag && enemyManager != null)
        {
            enemyManager.RemoveAllActiveEnemies(Game03EnemyRemovalKind.Vanish, phaseResetVanish: true);
        }
        else if (enemyManager != null && !isInitialLoad)
        {
            enemyManager.ClearPhaseSpawnTrackingOnAllEnemies();
        }

        activePhaseNumber = phase1Based;
        phaseGlobalStartOffset = Game03EnemyPhaseTimeline.GetGlobalStartSecondsForPhase(phase1Based);

        RebuildSpawnTracksFromCurrentPhaseData();

        enemyManager.SetPhaseSpawnAliveSink(this);
        return true;
    }

    private static bool HasAtLeastOneSupportedSpawnEntry(Game03EnemySpawnPhase01Json data)
    {
        for (int i = 0; i < data.entries.Length; i++)
        {
            Game03EnemyPhaseSpawnEntryJson e = data.entries[i];
            if (e != null && IsSupportedSpawnPatternId(e.spawnPatternId))
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildSpawnTracksFromCurrentPhaseData()
    {
        aliveByEntryIndex = new int[phaseData.entries.Length];
        spawnTracks.Clear();
        for (int i = 0; i < phaseData.entries.Length; i++)
        {
            Game03EnemyPhaseSpawnEntryJson e = phaseData.entries[i];
            if (e == null)
            {
                continue;
            }

            int pid = e.spawnPatternId;
            if (!IsSupportedSpawnPatternId(pid))
            {
                Debug.LogWarning($"[Game03EnemySpawnManager] 未対応の出現種別 {pid}（エントリ index={i}）。スキップします。");
                continue;
            }

            spawnTracks.Add(new SpawnEntryTrack
            {
                EntryIndex = i,
                Entry = e,
                NextEligibleTime = e.spawnStartTime,
                NextWaveFromRight = true,
                Pattern14AngleRad = Random.Range(0f, Mathf.PI * 2f),
                Pattern11AngleRad = Random.Range(0f, Mathf.PI * 2f)
            });
        }
    }

    private void TryPromotePhaseChain(float globalElapsedSeconds)
    {
        int desired = Game03EnemyPhaseTimeline.GetActivePhaseNumberFromGlobalElapsed(globalElapsedSeconds);
        if (desired <= activePhaseNumber)
        {
            return;
        }

        while (activePhaseNumber < desired && activePhaseNumber < Game03EnemyPhaseTimeline.MaxEnemySpawnPhase)
        {
            int next = activePhaseNumber + 1;
            if (next == lastFailedPromoteTargetPhase)
            {
                break;
            }

            if (!TryActivatePhase(next, isInitialLoad: false))
            {
                lastFailedPromoteTargetPhase = next;
                break;
            }

            lastFailedPromoteTargetPhase = -1;
        }
    }

    private static bool IsSupportedSpawnPatternId(int pid)
    {
        return pid is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or 14 or 51 or 61;
    }

    private void OnDestroy()
    {
        if (enemyManager != null)
        {
            enemyManager.SetPhaseSpawnAliveSink(null);
        }
    }

    public void OnPhaseSpawnedEnemyRemoved(int entryIndex)
    {
        if (aliveByEntryIndex == null || entryIndex < 0 || entryIndex >= aliveByEntryIndex.Length)
        {
            return;
        }

        int prev = aliveByEntryIndex[entryIndex];
        aliveByEntryIndex[entryIndex] = Mathf.Max(0, prev - 1);
        if (prev > 0 && aliveByEntryIndex[entryIndex] == 0)
        {
            ResetPattern0405DistanceStateAfterEntryWiped(entryIndex);
        }
    }

    private void ResetPattern0405DistanceStateAfterEntryWiped(int entryIndex)
    {
        if (spawnTracks == null || spawnTracks.Count == 0 || game03Manager == null)
        {
            return;
        }

        float localElapsed = game03Manager.GameplayElapsedSeconds - phaseGlobalStartOffset;
        for (int i = 0; i < spawnTracks.Count; i++)
        {
            SpawnEntryTrack t = spawnTracks[i];
            if (t.EntryIndex != entryIndex || t.Entry == null)
            {
                continue;
            }

            if (t.Entry.spawnSpacingPixels <= 0.0001f)
            {
                continue;
            }

            if (t.Entry.spawnPatternId != 4 && t.Entry.spawnPatternId != 5)
            {
                continue;
            }

            t.Pattern0405DistanceMilestoneValid = false;
            t.NextEligibleTime = localElapsed + Mathf.Max(0.05f, t.Entry.spawnIntervalSeconds);
        }
    }

    private void Update()
    {
        if (loadFailed || phaseData == null || spawnTracks.Count == 0)
        {
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        if (game03DebugManager != null && game03DebugManager.EffectiveNoEnemySpawns)
        {
            return;
        }

        if (enemyManager == null || game03UnitManager == null)
        {
            return;
        }

        float globalElapsed = game03Manager != null ? game03Manager.GameplayElapsedSeconds : Time.time;
        TryPromotePhaseChain(globalElapsed);

        float localElapsed = globalElapsed - phaseGlobalStartOffset;
        if (localElapsed < 0f)
        {
            return;
        }

        bool isFinalEnemyPhase = activePhaseNumber >= Game03EnemyPhaseTimeline.MaxEnemySpawnPhase;
        if (isFinalEnemyPhase && localElapsed > phaseData.phaseDurationSeconds)
        {
            return;
        }

        if (!game03UnitManager.TryGetUnitCenterOnRect(enemyManager.EnemyRoot, out Vector2 playerCenter))
        {
            return;
        }

        Rect vis = enemyManager.GetVisibleLocalRectOnEnemyRoot();
        for (int i = 0; i < spawnTracks.Count; i++)
        {
            SpawnEntryTrack track = spawnTracks[i];
            switch (track.Entry.spawnPatternId)
            {
                case 1:
                    TickPattern01(track, localElapsed, vis, playerCenter);
                    break;
                case 2:
                    TickPattern02(track, localElapsed, vis, playerCenter);
                    break;
                case 3:
                    TickPattern03(track, localElapsed, vis, playerCenter);
                    break;
                case 4:
                    TickPattern04(track, localElapsed, vis, playerCenter);
                    break;
                case 5:
                    TickPattern05(track, localElapsed, vis, playerCenter);
                    break;
                case 6:
                    TickPattern06(track, localElapsed, vis, playerCenter);
                    break;
                case 7:
                    TickPattern07(track, localElapsed, vis, playerCenter);
                    break;
                case 8:
                    TickPattern08(track, localElapsed, vis, playerCenter);
                    break;
                case 10:
                    TickPattern10(track, localElapsed, vis, playerCenter);
                    break;
                case 11:
                    TickPattern11(track, localElapsed, vis, playerCenter);
                    break;
                case 12:
                    TickPattern12(track, localElapsed, vis, playerCenter);
                    break;
                case 13:
                    TickPattern13(track, localElapsed, vis, playerCenter);
                    break;
                case 14:
                    TickPattern14(track, localElapsed, vis, playerCenter);
                    break;
                case 51:
                    TickPattern51(track, localElapsed, vis, playerCenter);
                    break;
                case 61:
                    TickPattern61(track, localElapsed, vis, playerCenter);
                    break;
            }
        }
    }

    private static bool IsInstantSpawnWindow(Game03EnemyPhaseSpawnEntryJson e)
    {
        return Mathf.Abs(e.spawnEndTime - e.spawnStartTime) < 0.001f;
    }

    private static bool ShouldSkipSpawnWindow(SpawnEntryTrack track, float elapsed, Game03EnemyPhaseSpawnEntryJson e)
    {
        if (IsInstantSpawnWindow(e))
        {
            return track.SingleShotWindowDone || elapsed < e.spawnStartTime;
        }

        return elapsed < e.spawnStartTime || elapsed > e.spawnEndTime;
    }

    /// <summary>
    /// 種別04/05: コア半径ぶん可視矩形と重ならないよう、スポーン時点の X を必ず画面外へ寄せる（背景中心基準で内側に入った場合の補正）。
    /// </summary>
    private static Vector2 ClampSpawnPosFullyOutsideHorizontal(
        Rect vis,
        float outsideMarginLocal,
        float coreRadiusPx,
        bool spawnFromLeft,
        Vector2 spawnPos)
    {
        float r = Mathf.Max(1f, coreRadiusPx);
        float m = Mathf.Max(0f, outsideMarginLocal);
        if (spawnFromLeft)
        {
            float maxXFullyOffLeft = vis.xMin - m - r;
            if (spawnPos.x > maxXFullyOffLeft)
            {
                spawnPos.x = maxXFullyOffLeft;
            }
        }
        else
        {
            float minXFullyOffRight = vis.xMax + m + r;
            if (spawnPos.x < minXFullyOffRight)
            {
                spawnPos.x = minXFullyOffRight;
            }
        }

        return spawnPos;
    }

    private int GetWaveSpawnCount(SpawnEntryTrack track, Game03EnemyPhaseSpawnEntryJson e)
    {
        if (!track.HadFirstSuccessfulSpawn)
        {
            return e.initialSpawnCount > 0 ? e.initialSpawnCount : Mathf.Max(1, e.spawnCountPerWave);
        }

        return Mathf.Max(1, e.spawnCountPerWave);
    }

    private bool CanSpawnWave(SpawnEntryTrack track, Game03EnemyPhaseSpawnEntryJson e, int waveCount)
    {
        int max = Mathf.Max(0, e.maxAlive);
        if (max <= 0)
        {
            return true;
        }

        int alive = aliveByEntryIndex != null && track.EntryIndex < aliveByEntryIndex.Length
            ? aliveByEntryIndex[track.EntryIndex]
            : 0;
        return alive + waveCount <= max;
    }

    private int ComputeEffectiveHp(int prefabMaxLife)
    {
        float mult = phaseData != null ? phaseData.hpMult : 1f;
        if (mult <= 1f)
        {
            mult = 1f;
        }

        float flat = phaseData != null ? phaseData.hpFlatBonus : 0f;
        if (flat < 0f)
        {
            flat = 0f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(prefabMaxLife * mult + flat));
    }

    private int ComputeEffectiveExperience(int prefabExperience)
    {
        float mult = phaseData != null ? phaseData.expMult : 1f;
        if (mult <= 1f)
        {
            mult = 1f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, prefabExperience) * mult));
    }

    private float GetEntryLayoutCoreRadiusPx(int prefabSlot, RectTransform prefabOverride = null)
    {
        if (enemyManager.TryReadPrefabSpawnTemplate(prefabSlot, prefabOverride, out Game03EnemyPrefabSpawnTemplate template))
        {
            return Mathf.Max(1f, template.CoreRadius);
        }

        return 26f;
    }

    private bool TrySpawnPhaseEnemyForEntry(
        int prefabSlot,
        RectTransform prefabOverride,
        Vector2 pos,
        Vector2 dir,
        Game03EnemyPhaseSpawnEntryJson e,
        int steering,
        int trackEntryIndex,
        Game03EnemyOffscreenCullMode offscreenCull = Game03EnemyOffscreenCullMode.None,
        float offscreenCullMarginLocal = 0f,
        float spawnHoldMoveSeconds = 0f,
        float spawnFadeInSeconds = 0f)
    {
        if (!enemyManager.TryReadPrefabSpawnTemplate(prefabSlot, prefabOverride, out Game03EnemyPrefabSpawnTemplate template))
        {
            return false;
        }

        int hpEff = ComputeEffectiveHp(template.MaxLife);
        int expDrop = ComputeEffectiveExperience(template.ExperiencePoints);
        return enemyManager.TrySpawnPhaseEnemy(
            prefabSlot,
            pos,
            dir,
            e.lifetimeSeconds,
            hpEff,
            expDrop,
            steering,
            e.speedTierMult,
            e.kSpeed,
            trackEntryIndex,
            offscreenCull,
            offscreenCullMarginLocal,
            prefabOverride,
            spawnHoldMoveSeconds,
            spawnFadeInSeconds,
            e.enemyKind,
            e.spawnPatternId);
    }

    private void AdvanceSchedule(
        SpawnEntryTrack track,
        float elapsed,
        Game03EnemyPhaseSpawnEntryJson e,
        int spawned,
        bool pattern0405DistancePost = false,
        bool pattern04MarchPositiveX = true)
    {
        if (spawned <= 0)
        {
            return;
        }

        track.HadFirstSuccessfulSpawn = true;
        aliveByEntryIndex[track.EntryIndex] += spawned;
        if (IsInstantSpawnWindow(e))
        {
            track.SingleShotWindowDone = true;
            track.NextEligibleTime = float.MaxValue;
            return;
        }

        if (pattern0405DistancePost)
        {
            track.NextEligibleTime = elapsed;
            if (pattern04MarchPositiveX && enemyManager.TryGetMinAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float mn))
            {
                track.Pattern0405DistanceMilestoneX = mn;
                track.Pattern0405DistanceMilestoneValid = true;
            }
            else if (!pattern04MarchPositiveX && enemyManager.TryGetMaxAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float mx))
            {
                track.Pattern0405DistanceMilestoneX = mx;
                track.Pattern0405DistanceMilestoneValid = true;
            }

            return;
        }

        track.NextEligibleTime = elapsed + Mathf.Max(0.05f, e.spawnIntervalSeconds);
    }

    private void TickPattern01(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float step = Mathf.Max(8f, layoutCoreR * 2f);
        bool fromRight = track.NextWaveFromRight;
        track.NextWaveFromRight = !fromRight;

        float centerY = playerCenter.y + e.spawnOffsetPx;
        float sideX = fromRight ? vis.xMax + spawnOutsideMarginLocal : vis.xMin - spawnOutsideMarginLocal;

        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, step, n);
            Vector2 pos = new Vector2(sideX, y);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern02(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        int alive = aliveByEntryIndex[track.EntryIndex];
        int max = Mathf.Max(0, e.maxAlive);
        if (max > 0)
        {
            waveCount = Mathf.Min(waveCount, Mathf.Max(0, max - alive));
        }

        if (waveCount <= 0 || !CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        float minExtra = Mathf.Min(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        float maxExtra = Mathf.Max(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        Vector2 annulusCenter = playerCenter;
        float halfDiag = new Vector2(vis.width, vis.height).magnitude * 0.5f;

        var placed = new List<Vector2>(waveCount);
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            if (!TrySampleAnnulusNonOverlapping(annulusCenter, halfDiag + minExtra, halfDiag + maxExtra, coreR, placed, pattern02PlacementMaxAttempts, out Vector2 pos))
            {
                break;
            }

            placed.Add(pos);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern03(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        float clusterY = vis.yMax + pattern03TopExtraMarginLocal + coreR * 2f + (playerCenter.y - vis.center.y);
        Vector2 clusterCenter = new Vector2(playerCenter.x + e.spawnOffsetPx, clusterY);

        const int steeringDirectional = 1;
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            Vector2 pos = ComputeGoldenDiskOffset(clusterCenter, coreR, n);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    steeringDirectional,
                    track.EntryIndex,
                    Game03EnemyOffscreenCullMode.DeleteBeyondVisibleMargin,
                    pattern03OffscreenCullMarginLocal))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern04(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        bool distanceMode = e.spawnSpacingPixels > 0.0001f;
        float spacing = Mathf.Max(1f, e.spawnSpacingPixels);
        int alive = aliveByEntryIndex != null && track.EntryIndex < aliveByEntryIndex.Length
            ? aliveByEntryIndex[track.EntryIndex]
            : 0;

        if (distanceMode)
        {
            if (alive == 0)
            {
                if (elapsed < track.NextEligibleTime)
                {
                    return;
                }
            }
            else if (track.Pattern0405DistanceMilestoneValid)
            {
                if (!enemyManager.TryGetMinAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float minXGate))
                {
                    return;
                }

                if (minXGate < track.Pattern0405DistanceMilestoneX + spacing - 0.01f)
                {
                    return;
                }
            }
        }
        else if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        Vector2 viewCenter = vis.center;
        Vector2 bgCenter = viewCenter;
        if (game03UnitManager.TryGetFieldRootCenterOnRect(enemyManager.EnemyRoot, out Vector2 fieldCenterLocal))
        {
            bgCenter = fieldCenterLocal;
        }

        Vector2 oldAnchor = new Vector2(vis.xMin - spawnOutsideMarginLocal, playerCenter.y + e.spawnOffsetPx);
        Vector2 newAnchor = bgCenter + (oldAnchor - viewCenter);
        float sideX = newAnchor.x;
        if (distanceMode && alive > 0 && enemyManager.TryGetMinAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float minXSpawn))
        {
            sideX = minXSpawn - spacing;
        }

        float centerY = newAnchor.y;
        const int steeringDirectional = 1;
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, Mathf.Max(8f, layoutCoreR * 2f), n);
            Vector2 spawnPos = new Vector2(sideX, y);
            spawnPos = ClampSpawnPosFullyOutsideHorizontal(vis, spawnOutsideMarginLocal, layoutCoreR, true, spawnPos);
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    spawnPos,
                    Vector2.right,
                    e,
                    steeringDirectional,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned, distanceMode && spawned > 0, true);
    }

    private void TickPattern05(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        bool distanceMode = e.spawnSpacingPixels > 0.0001f;
        float spacing = Mathf.Max(1f, e.spawnSpacingPixels);
        int alive = aliveByEntryIndex != null && track.EntryIndex < aliveByEntryIndex.Length
            ? aliveByEntryIndex[track.EntryIndex]
            : 0;

        if (distanceMode)
        {
            if (alive == 0)
            {
                if (elapsed < track.NextEligibleTime)
                {
                    return;
                }
            }
            else if (track.Pattern0405DistanceMilestoneValid)
            {
                if (!enemyManager.TryGetMaxAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float maxXGate))
                {
                    return;
                }

                if (maxXGate > track.Pattern0405DistanceMilestoneX - spacing + 0.01f)
                {
                    return;
                }
            }
        }
        else if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        Vector2 viewCenter = vis.center;
        Vector2 bgCenter = viewCenter;
        if (game03UnitManager.TryGetFieldRootCenterOnRect(enemyManager.EnemyRoot, out Vector2 fieldCenterLocal))
        {
            bgCenter = fieldCenterLocal;
        }

        Vector2 oldAnchor = new Vector2(vis.xMax + spawnOutsideMarginLocal, playerCenter.y + e.spawnOffsetPx);
        Vector2 newAnchor = bgCenter + (oldAnchor - viewCenter);
        float sideX = newAnchor.x;
        if (distanceMode && alive > 0 && enemyManager.TryGetMaxAnchoredXForPhaseSpawnEntry(track.EntryIndex, out float maxXSpawn))
        {
            sideX = maxXSpawn + spacing;
        }

        float centerY = newAnchor.y;
        const int steeringDirectional = 1;
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, Mathf.Max(8f, layoutCoreR * 2f), n);
            Vector2 spawnPos = new Vector2(sideX, y);
            spawnPos = ClampSpawnPosFullyOutsideHorizontal(vis, spawnOutsideMarginLocal, layoutCoreR, false, spawnPos);
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    spawnPos,
                    Vector2.left,
                    e,
                    steeringDirectional,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned, distanceMode && spawned > 0, false);
    }

    private void TickPattern06(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        int leftCount = (waveCount + 1) / 2;
        int rightCount = waveCount - leftCount;
        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float step = Mathf.Max(8f, layoutCoreR * 2f);
        float centerY = playerCenter.y + e.spawnOffsetPx;
        float leftX = vis.xMin - spawnOutsideMarginLocal;
        float rightX = vis.xMax + spawnOutsideMarginLocal;
        int spawned = 0;

        for (int n = 0; n < leftCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, step, n);
            Vector2 pos = new Vector2(leftX, y);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        for (int n = 0; n < rightCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, step, n);
            Vector2 pos = new Vector2(rightX, y);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.left;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern07(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        GetPattern07EllipseRadiiPx(e.spawnOffsetPx, out float vR, out float hR);

        var ring = new List<Vector2>(waveCount);
        if (!TryComputeEllipseSurroundPositions(playerCenter, vR, hR, waveCount, coreR, ring))
        {
            Debug.LogWarning("[Game03EnemySpawnManager] 種別07 楕円配置に失敗しました。");
            return;
        }

        int spawned = 0;
        for (int n = 0; n < ring.Count; n++)
        {
            Vector2 pos = ring[n];
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern08(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        TickPatternAnnulusHalfPlane(track, elapsed, vis, playerCenter, Mathf.PI * 0.5f, Mathf.PI * 1.5f, pattern08PlacementMaxAttempts);
    }

    private void TickPattern10(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        TickPatternAnnulusHalfPlane(track, elapsed, vis, playerCenter, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, pattern10PlacementMaxAttempts);
    }

    private void TickPatternAnnulusHalfPlane(
        SpawnEntryTrack track,
        float elapsed,
        Rect vis,
        Vector2 playerCenter,
        float angMinRad,
        float angMaxRad,
        int placementMaxAttempts)
    {
        _ = vis;
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        int alive = aliveByEntryIndex[track.EntryIndex];
        int max = Mathf.Max(0, e.maxAlive);
        if (max > 0)
        {
            waveCount = Mathf.Min(waveCount, Mathf.Max(0, max - alive));
        }

        if (waveCount <= 0 || !CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        float minExtra = Mathf.Min(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        float maxExtra = Mathf.Max(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        Vector2 annulusCenter = playerCenter;
        float halfDiag = new Vector2(vis.width, vis.height).magnitude * 0.5f;

        var placed = new List<Vector2>(waveCount);
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            if (!TrySampleAnnulusAngularNonOverlapping(
                    annulusCenter,
                    halfDiag + minExtra,
                    halfDiag + maxExtra,
                    coreR,
                    placed,
                    placementMaxAttempts,
                    angMinRad,
                    angMaxRad,
                    out Vector2 pos))
            {
                break;
            }

            placed.Add(pos);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern11(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        _ = vis;
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        int alive = aliveByEntryIndex[track.EntryIndex];
        int max = Mathf.Max(0, e.maxAlive);
        if (max > 0)
        {
            waveCount = Mathf.Min(waveCount, Mathf.Max(0, max - alive));
        }

        if (waveCount <= 0 || !CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        const float stepRad = 40f * Mathf.Deg2Rad;
        float baseMinDist = e.spawnOffsetPx > 0.001f ? e.spawnOffsetPx : pattern11DefaultMinDistancePx;
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            float ang = track.Pattern11AngleRad + Random.Range(0f, stepRad);
            float dist = baseMinDist + Random.Range(0f, 200f);
            Vector2 pos = playerCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            track.Pattern11AngleRad += stepRad;
            Vector2 moveDir = Random.insideUnitCircle;
            if (moveDir.sqrMagnitude < 0.0001f)
            {
                moveDir = Vector2.right;
            }
            else
            {
                moveDir.Normalize();
            }

            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    moveDir,
                    e,
                    e.steering,
                    track.EntryIndex,
                    Game03EnemyOffscreenCullMode.None,
                    0f,
                    pattern11MoveHoldSeconds,
                    pattern11FadeInSeconds))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern51(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        if (!CanSpawnWave(track, e, 1))
        {
            return;
        }

        RectTransform bossPrefab = enemyManager.PhaseMidBossSpawnPrefab;
        if (bossPrefab == null)
        {
            Debug.LogWarning("[Game03EnemySpawnManager] 中BOSS 用プレハブ（Game03EnemyManager.bossMiddle01ImagePrefab／BossMiddle01ImagePrefab）が未設定です。種別51 をスキップします。");
            track.SingleShotWindowDone = true;
            track.NextEligibleTime = float.MaxValue;
            return;
        }

        bool fromRight = Random.value >= 0.5f;
        Vector2 pos;
        if (Mathf.Abs(e.spawnOffsetPx) > 0.001f)
        {
            GetPattern07EllipseRadiiPx(e.spawnOffsetPx, out float vR, out float hR);
            float angleRad = fromRight ? 0f : Mathf.PI;
            pos = ComputeEllipseSurroundPoint(playerCenter, vR, hR, angleRad);
        }
        else
        {
            float margin = spawnOutsideMarginLocal + pattern51ExtraOutsideMarginLocal;
            float x = fromRight ? vis.xMax + margin : vis.xMin - margin;
            float centerY = playerCenter.y;
            pos = new Vector2(x, centerY);
        }

        Vector2 toPlayer = playerCenter - pos;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        int spawned = 0;
        if (TrySpawnPhaseEnemyForEntry(
                0,
                bossPrefab,
                pos,
                dir,
                e,
                e.steering,
                track.EntryIndex))
        {
            spawned++;
            enemyManager.TryPlayMidBossSpawnAppearSe();
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern61(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        if (!CanSpawnWave(track, e, 1))
        {
            return;
        }

        if (!IsBoss51EnemyKind(e.enemyKind))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 種別61 は敵種別 Boss51 を想定していますが: {e.enemyKind}");
        }

        RectTransform bossPrefab = enemyManager.PhaseFinalBossSpawnPrefab;
        if (bossPrefab == null)
        {
            Debug.LogWarning("[Game03EnemySpawnManager] 大BOSS 用プレハブ（Game03EnemyManager.boss01ImagePrefab／Boss01ImagePrefab）が未設定です。種別61 をスキップします。");
            track.SingleShotWindowDone = true;
            track.NextEligibleTime = float.MaxValue;
            return;
        }

        bool fromRight = Random.value >= 0.5f;
        Vector2 pos;
        if (Mathf.Abs(e.spawnOffsetPx) > 0.001f)
        {
            GetPattern07EllipseRadiiPx(e.spawnOffsetPx, out float vR, out float hR);
            float angleRad = fromRight ? 0f : Mathf.PI;
            pos = ComputeEllipseSurroundPoint(playerCenter, vR, hR, angleRad);
        }
        else
        {
            float margin = spawnOutsideMarginLocal + pattern61ExtraOutsideMarginLocal;
            float x = fromRight ? vis.xMax + margin : vis.xMin - margin;
            pos = new Vector2(x, playerCenter.y);
        }

        Vector2 toPlayer = playerCenter - pos;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        int spawned = 0;
        if (TrySpawnPhaseEnemyForEntry(
                0,
                bossPrefab,
                pos,
                dir,
                e,
                e.steering,
                track.EntryIndex))
        {
            spawned++;
            enemyManager.TryPlayFinalBossSpawnAppearSe();
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern12(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        int facing = game03UnitManager.LastHorizontalFacing >= 0 ? 1 : -1;
        float marginDepth = spawnOutsideMarginLocal + pattern12SideClusterDepthLocal;
        float clusterX = facing >= 0 ? vis.xMin - marginDepth : vis.xMax + marginDepth;
        Vector2 clusterCenter = new Vector2(clusterX, playerCenter.y + e.spawnOffsetPx);

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            Vector2 pos = ComputeGoldenDiskOffset(clusterCenter, coreR, n);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : (facing >= 0 ? Vector2.right : Vector2.left);
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern13(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        if (!CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        int facing = game03UnitManager.LastHorizontalFacing >= 0 ? 1 : -1;
        float sideX = facing >= 0 ? vis.xMax + spawnOutsideMarginLocal : vis.xMin - spawnOutsideMarginLocal;

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float step = Mathf.Max(8f, layoutCoreR * 2f);
        float centerY = playerCenter.y + e.spawnOffsetPx;
        int spawned = 0;
        for (int n = 0; n < waveCount; n++)
        {
            float y = ComputeAlternateStackY(centerY, step, n);
            Vector2 pos = new Vector2(sideX, y);
            Vector2 toPlayer = playerCenter - pos;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    pos,
                    dir,
                    e,
                    e.steering,
                    track.EntryIndex))
            {
                spawned++;
            }
        }

        AdvanceSchedule(track, elapsed, e, spawned);
    }

    private void TickPattern14(SpawnEntryTrack track, float elapsed, Rect vis, Vector2 playerCenter)
    {
        Game03EnemyPhaseSpawnEntryJson e = track.Entry;
        if (ShouldSkipSpawnWindow(track, elapsed, e))
        {
            return;
        }

        if (elapsed < track.NextEligibleTime)
        {
            return;
        }

        int waveCount = GetWaveSpawnCount(track, e);
        int alive = aliveByEntryIndex[track.EntryIndex];
        int max = Mathf.Max(0, e.maxAlive);
        if (max > 0)
        {
            waveCount = Mathf.Min(waveCount, Mathf.Max(0, max - alive));
        }

        if (waveCount <= 0 || !CanSpawnWave(track, e, waveCount))
        {
            return;
        }

        if (!TryParseEnemyKindSlot(e.enemyKind, out int prefabSlot))
        {
            Debug.LogWarning($"[Game03EnemySpawnManager] 不明な敵種別: {e.enemyKind}");
            return;
        }

        float layoutCoreR = GetEntryLayoutCoreRadiusPx(prefabSlot);
        float coreR = Mathf.Max(8f, layoutCoreR);
        float minExtra = Mathf.Min(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        float maxExtra = Mathf.Max(pattern02RingMinExtraPx, pattern02RingMaxExtraPx);
        Vector2 burstCenter = playerCenter;
        float halfDiag = new Vector2(vis.width, vis.height).magnitude * 0.5f;
        const int steeringDirectional = 1;
        const float stepRad = (40f * Mathf.Deg2Rad);

        var placed = new List<Vector2>(waveCount);
        int spawned = 0;
        float minSepSq = (coreR * 2.05f) * (coreR * 2.05f);
        float rMin = halfDiag + minExtra;
        float rMax = halfDiag + maxExtra;
        for (int n = 0; n < waveCount; n++)
        {
            Vector2 candidate = default;
            bool placedOk = false;
            for (int a = 0; a < pattern02PlacementMaxAttempts; a++)
            {
                float ang = track.Pattern14AngleRad + Random.Range(0f, stepRad);
                float rad = Random.Range(rMin, rMax);
                candidate = burstCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                bool ok = true;
                for (int i = 0; i < placed.Count; i++)
                {
                    if ((placed[i] - candidate).sqrMagnitude < minSepSq)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    placedOk = true;
                    break;
                }
            }

            if (!placedOk)
            {
                break;
            }

            placed.Add(candidate);
            Vector2 toPlayer = playerCenter - candidate;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;
            if (TrySpawnPhaseEnemyForEntry(
                    prefabSlot,
                    null,
                    candidate,
                    dir,
                    e,
                    steeringDirectional,
                    track.EntryIndex,
                    Game03EnemyOffscreenCullMode.VanishBeyondVisibleMargin,
                    pattern14OffscreenCullMarginLocal))
            {
                spawned++;
            }
        }

        track.Pattern14AngleRad += stepRad;
        AdvanceSchedule(track, elapsed, e, spawned);
    }

    /// <summary>種別07 と同じ楕円（縦＝|spawnOffsetPx|、横＝縦×1.4）。種別51 の円内配置でも使用。</summary>
    private static void GetPattern07EllipseRadiiPx(float spawnOffsetPx, out float verticalRadiusPx, out float horizontalRadiusPx)
    {
        verticalRadiusPx = Mathf.Max(40f, Mathf.Abs(spawnOffsetPx));
        horizontalRadiusPx = verticalRadiusPx * 1.4f;
    }

    private static Vector2 ComputeEllipseSurroundPoint(
        Vector2 center,
        float verticalRadiusPx,
        float horizontalRadiusPx,
        float angleRad)
    {
        return center + new Vector2(
            horizontalRadiusPx * Mathf.Cos(angleRad),
            verticalRadiusPx * Mathf.Sin(angleRad));
    }

    private static bool TryComputeEllipseSurroundPositions(
        Vector2 center,
        float verticalRadiusPx,
        float horizontalRadiusPx,
        int count,
        float coreRadius,
        List<Vector2> dest)
    {
        dest.Clear();
        if (count <= 0)
        {
            return false;
        }

        float vR = Mathf.Max(40f, Mathf.Abs(verticalRadiusPx));
        float hR = Mathf.Max(40f, horizontalRadiusPx > 0.001f ? horizontalRadiusPx : vR * 1.4f);
        float minSepSq = (Mathf.Max(8f, coreRadius * 2.05f)) * (Mathf.Max(8f, coreRadius * 2.05f));

        for (int expand = 0; expand < 18; expand++)
        {
            dest.Clear();
            bool allOk = true;
            for (int i = 0; i < count; i++)
            {
                float t = (-Mathf.PI * 0.5f) + (Mathf.PI * 2f) * i / count;
                Vector2 p = center + new Vector2(hR * Mathf.Cos(t), vR * Mathf.Sin(t));
                bool ok = true;
                for (int j = 0; j < dest.Count; j++)
                {
                    if ((dest[j] - p).sqrMagnitude < minSepSq)
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    allOk = false;
                    break;
                }

                dest.Add(p);
            }

            if (allOk && dest.Count == count)
            {
                return true;
            }

            vR *= 1.08f;
            hR *= 1.08f;
        }

        return false;
    }

    private static bool TrySampleAnnulusAngularNonOverlapping(
        Vector2 center,
        float rMin,
        float rMax,
        float coreR,
        List<Vector2> placed,
        int maxAttempts,
        float angMinRad,
        float angMaxRad,
        out Vector2 pos)
    {
        float minSepSq = (coreR * 2.05f) * (coreR * 2.05f);
        for (int a = 0; a < maxAttempts; a++)
        {
            float ang = Random.Range(angMinRad, angMaxRad);
            float rad = Random.Range(rMin, rMax);
            Vector2 candidate = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            bool ok = true;
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - candidate).sqrMagnitude < minSepSq)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                pos = candidate;
                return true;
            }
        }

        pos = default;
        return false;
    }

    private static bool TrySampleAnnulusNonOverlapping(
        Vector2 center,
        float rMin,
        float rMax,
        float coreR,
        List<Vector2> placed,
        int maxAttempts,
        out Vector2 pos)
    {
        float minSepSq = (coreR * 2.05f) * (coreR * 2.05f);
        for (int a = 0; a < maxAttempts; a++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float rad = Random.Range(rMin, rMax);
            Vector2 candidate = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            bool ok = true;
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - candidate).sqrMagnitude < minSepSq)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                pos = candidate;
                return true;
            }
        }

        pos = default;
        return false;
    }

    private static Vector2 ComputeGoldenDiskOffset(Vector2 center, float coreR, int index)
    {
        float minSep = Mathf.Max(8f, coreR * 2.05f);
        if (index == 0)
        {
            return center;
        }

        float golden = 2.39996323f;
        float ang = index * golden;
        float radius = minSep * 0.9f * Mathf.Sqrt(index);
        return center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
    }

    private static float ComputeAlternateStackY(float centerY, float step, int index)
    {
        if (index == 0)
        {
            return centerY;
        }

        if ((index & 1) == 1)
        {
            return centerY + ((index + 1) / 2) * step;
        }

        return centerY - (index / 2) * step;
    }

    private static bool IsBoss51EnemyKind(string enemyKind)
    {
        return string.Equals(enemyKind?.Trim(), "Boss51", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseEnemyKindSlot(string enemyKind, out int slot0Based)
    {
        slot0Based = 0;
        if (string.IsNullOrWhiteSpace(enemyKind))
        {
            return false;
        }

        string t = enemyKind.Trim();
        if (t.Length < 6)
        {
            return false;
        }

        if (!t.StartsWith("Enemy", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(t.Substring(5), out int num))
        {
            return false;
        }

        slot0Based = Mathf.Max(0, num);
        return true;
    }
}
