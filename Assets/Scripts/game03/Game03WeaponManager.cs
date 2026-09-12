using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(60)]
public class Game03WeaponManager : MonoBehaviour
{
    public readonly struct UpgradeWeaponCandidate
    {
        public readonly int WeaponNumber;
        public readonly Sprite WeaponSprite;
        public readonly RectTransform EquippedUnitRect;

        public UpgradeWeaponCandidate(int weaponNumber, Sprite weaponSprite, RectTransform equippedUnitRect = null)
        {
            WeaponNumber = weaponNumber;
            WeaponSprite = weaponSprite;
            EquippedUnitRect = equippedUnitRect;
        }
    }

    private enum WeaponMode
    {
        None,
        Melee01,
        Ranged05,
        Mud02,
        Orbit03,
        Axe07,
        Magic06,
        Claw04
    }

    [System.Serializable]
    private sealed class UnitWeaponBinding
    {
        private enum WeaponSelect
        {
            None,
            Weapon01,
            Weapon02,
            Weapon03,
            Weapon04,
            Weapon05,
            Weapon06,
            Weapon07
        }

        [SerializeField] private RectTransform unitRect;
        [SerializeField] private RectTransform equippedUnitRect;
        [SerializeField] private WeaponSelect weaponSelect = WeaponSelect.None;

        public RectTransform UnitRect => unitRect;
        public RectTransform EquippedUnitRect => equippedUnitRect != null ? equippedUnitRect : unitRect;

        public WeaponMode SelectedMode
        {
            get
            {
                switch (weaponSelect)
                {
                    case WeaponSelect.Weapon01: return WeaponMode.Melee01;
                    case WeaponSelect.Weapon02: return WeaponMode.Mud02;
                    case WeaponSelect.Weapon03: return WeaponMode.Orbit03;
                    case WeaponSelect.Weapon04: return WeaponMode.Claw04;
                    case WeaponSelect.Weapon05: return WeaponMode.Ranged05;
                    case WeaponSelect.Weapon06: return WeaponMode.Magic06;
                    case WeaponSelect.Weapon07: return WeaponMode.Axe07;
                    default: return WeaponMode.None;
                }
            }
        }

        public void SetWeaponByNumber(int weaponNumber)
        {
            weaponSelect = weaponNumber switch
            {
                1 => WeaponSelect.Weapon01,
                2 => WeaponSelect.Weapon02,
                3 => WeaponSelect.Weapon03,
                4 => WeaponSelect.Weapon04,
                5 => WeaponSelect.Weapon05,
                6 => WeaponSelect.Weapon06,
                7 => WeaponSelect.Weapon07,
                _ => WeaponSelect.None
            };
        }

    }

    private sealed class WeaponRuntimeState
    {
        public UnitWeaponBinding Binding;
        public readonly HashSet<int> HitEnemyIdsInCurrentAttack = new HashSet<int>();
        public float AttackTimer;
        public float AttackEffectRemaining;
        public float AttackActiveRemaining;
        public int CurrentAttackFacing = 1;
        public float AttackActiveDuration;
        public Vector3 WeaponBaseScale = Vector3.one;
        public Vector3 ImageBaseScale = Vector3.one;
        public Vector2 ImageBaseAnchoredPosition;
        public Vector3 HitBaseScale = Vector3.one;
        public Vector2 HitBaseAnchoredPosition;
        public WeaponMode Mode;
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();
        public readonly List<MudProjectileState> MudProjectiles = new List<MudProjectileState>();
        public readonly List<MudZoneState> MudZones = new List<MudZoneState>();
        public readonly List<OrbitProjectileState> OrbitProjectiles = new List<OrbitProjectileState>();
        public readonly List<AxeProjectileState> AxeProjectiles = new List<AxeProjectileState>();
        public readonly List<MagicProjectileState> MagicProjectiles = new List<MagicProjectileState>();
        public bool ClawAttacking;
        public int ClawShotsFired;
        public float ClawChainTimer;
        /// <summary>最終チェーンの SE 後、描画を保ったまま待つ残り秒数。その後クールダウンへ。</summary>
        public float ClawWindDownSecondsRemaining;
        public readonly HashSet<int> ClawHitIdsPerShot = new HashSet<int>();
        public bool ClawUnitHidden;
        public int LastClawImageId = 0;
        public Vector2 LastClawVisualPos = new Vector2(float.NaN, float.NaN);
        public int PendingExtraMeleeShots;
        /// <summary>1振り終了後、終端角度で静止する残り秒数（2撃目開始前／次攻撃サイクル前）。</summary>
        public float MeleePostSwingHoldRemaining;
        public bool MeleeInPostSwingHold;
        public readonly List<PendingWeapon05SetSpawn> PendingWeapon05SetSpawns = new List<PendingWeapon05SetSpawn>();
    }

    private sealed class PendingWeapon05SetSpawn
    {
        public float TimeRemaining;
        public int FirstIndexInclusive;
        public int LastIndexInclusive;
    }

    private sealed class ProjectileState
    {
        public RectTransform Rect;
        public RectTransform HitRect;
        public Image Image;
        public Vector2 TravelDirection;
        public float TraveledPixels;
        public float AliveSeconds;
        public float ThrowDistancePixels;
        public float LifeSeconds;
        public float SpeedPixelsPerSecond;
        public HashSet<int> DamagedEnemyIds;
    }

    private sealed class MudProjectileState
    {
        public RectTransform Rect;
        public Weapon02Stats Stats;
        public Vector2 StartPos;
        public Vector2 EndPos;
        public float TravelSeconds;
        public float ArcMaxHeight;
        public float Elapsed;
    }

    private sealed class MudZoneState
    {
        public RectTransform Rect;
        public RectTransform HitRect;
        public float RemainingSeconds;
        public float TickTimer;
        public Weapon02Stats DamageSourceStats;
        public bool BossRangeExpand;
        public Vector3 LandingBaseScale;
        public float TotalDurationSeconds;
    }

    private sealed class OrbitProjectileState
    {
        public RectTransform Rect;
        public RectTransform HitRect;
        public float BaseAngleDeg;
        public float CurrentAngleDeg;
        public float CurrentRadius;
        public float SpawnRadius;
        public float TargetRadius;
        public float ReachSeconds;
        public float ElapsedSeconds;
        public float RemainingSeconds;
        public float RotationProgressAbs;
        public readonly HashSet<int> HitEnemyIdsInCurrentTurn = new HashSet<int>();
        public float OrbitKnockPulseAccumulator;
        public bool BossPersistent;
    }

    private sealed class AxeProjectileState
    {
        public RectTransform Rect;
        public RectTransform HitRect;
        public Vector2 Velocity;
        public float AliveSeconds;
        public HashSet<int> DamagedEnemyIds;
    }

    private sealed class MagicProjectileState
    {
        public RectTransform Rect;
        public RectTransform HitRect;
        public Vector2 Velocity;
        public Vector2 LastKnownTargetCenter;
        public bool HasTarget;
        public float AliveSeconds;
        public HashSet<int> DamagedEnemyIds;
    }

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03SeManager game03SeManager;
    [SerializeField] private Game03UnitManager game03UnitManager;
    [SerializeField] private Game03EnemyManager game03EnemyManager;
    [SerializeField] private RectTransform itemWeaponCanvasRoot;
    [SerializeField] private List<RectTransform> sharedWeaponRects = new List<RectTransform>(7);
    [SerializeField, Tooltip("Unit*Oj 残像用 Weapon01..07ImagePrefab（index 0 = Weapon01）。")]
    private RectTransform[] weaponAfterImagePrefabs = new RectTransform[7];

    [Header("武器ノックバック（全体調整）")]
    [SerializeField, Tooltip("全武器のノック基準値への加算（enemyRoot ピクセル相当）。武器ごとの実効値に単純加算してから敵耐性・群れ補正へ。")]
    private float globalWeaponKnockbackAddPx = 0f;

    [Header("Upgrade UI (共通)")]
    [SerializeField, Tooltip("LvUp／ポーズで、その武器の抽選対象外 UG のレベル表示に使う文言")]
    private string notLotteryEligibleUpgradeLevelDisplay = "-";

    /// <summary>LvUp／ポーズの「抽選対象外 UG」LV 表示。空なら "-"。</summary>
    public string NotLotteryEligibleUpgradeLevelDisplay =>
        string.IsNullOrEmpty(notLotteryEligibleUpgradeLevelDisplay) ? "-" : notLotteryEligibleUpgradeLevelDisplay;

    public static bool IsUpgradeTypeInWeaponLotteryPool(int weaponNumber, Game03UpgradeType type) =>
        weaponNumber > 0 && Game03WeaponUpgradePools.IsEligibleForWeaponLottery(weaponNumber, type);

    [Header("Main Unit Weapon")]
    [SerializeField] private UnitWeaponBinding mainUnitWeapon;

    [Header("Sub Unit Weapons (Optional)")]
    [SerializeField] private UnitWeaponBinding unit01Weapon;
    [SerializeField] private UnitWeaponBinding unit02Weapon;
    [SerializeField] private UnitWeaponBinding unit03Weapon;
    [SerializeField] private UnitWeaponBinding unit04Weapon;

    [Header("Debug")]
    [SerializeField, Tooltip(
        "鎌（Weapon01）のヒット処理が動いているか・enemyRoot 変換後の矩形が潰れていないかをログ。Game03EnemyManager.debugLogWeaponHitProbe と併用すると切り分けしやすい。通常は OFF。")]
    private bool debugLogMeleeWeaponHitProbe;

    private readonly List<WeaponRuntimeState> runtimeStates = new List<WeaponRuntimeState>();
    private readonly Dictionary<int, int> totalUpgradeCountByWeaponNumber = new Dictionary<int, int>();
    private readonly Dictionary<int, Dictionary<Game03UpgradeType, int>> upgradeCountByWeaponAndType = new Dictionary<int, Dictionary<Game03UpgradeType, int>>();
    private int lastBindingsSignature = int.MinValue;

    /// <summary>Pod 加入スロット別の即時ランダム UG 回数（index 1=Unit01 … 4=Unit04）。</summary>
    private static readonly int[] JoinSlotInstantRandomUpgradeCounts = { 0, 0, 1, 2, 5 };

    private const float BossWeapon02RangeExpandMultiplier = 1.520875f;

    public bool AssignSubUnitWeaponBySlotIndex(int slotIndex, int weaponNumber)
    {
        UnitWeaponBinding binding = slotIndex switch
        {
            1 => unit01Weapon,
            2 => unit02Weapon,
            3 => unit03Weapon,
            4 => unit04Weapon,
            _ => null
        };

        if (binding == null)
        {
            return false;
        }

        binding.SetWeaponByNumber(weaponNumber);
        EnsureRuntimeStatesCurrent();
        ApplyMetaStartingUpgradesForNewlyEquippedWeapon(weaponNumber);
        ApplyJoinSlotRandomUpgrades(slotIndex, weaponNumber);
        ReapplyBossWeaponEnhancementIfNeeded(weaponNumber);
        return true;
    }

    /// <summary>
    /// 仲間 Pod 加入時、スロットに応じた回数だけ LvUp と同確率の UG を即適用（演出なし・重複可）。
    /// Unit01=0, Unit02=1, Unit03=2, Unit04=5。メインユニット／ゲームスタート時は呼ばない。
    /// </summary>
    private void ApplyJoinSlotRandomUpgrades(int slotIndex, int weaponNumber)
    {
        int count = GetJoinSlotInstantRandomUpgradeCount(slotIndex);
        if (count <= 0 || weaponNumber <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!Game03WeaponUpgradePools.TryDrawRandomUpgradeFromCombinedPool(weaponNumber, out Game03UpgradeType upgradeType))
            {
                break;
            }

            ApplyLevelUpUpgrade(weaponNumber, upgradeType);
        }
    }

    private static int GetJoinSlotInstantRandomUpgradeCount(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex >= JoinSlotInstantRandomUpgradeCounts.Length)
        {
            return 0;
        }

        return JoinSlotInstantRandomUpgradeCounts[slotIndex];
    }

    /// <summary>
    /// Pod 加入などラン中に装備された武器へメタ UG を適用（開始時 <see cref="ApplyMetaStartingUpgrades"/> の漏れ補完）。
    /// </summary>
    public void ApplyMetaStartingUpgradesForNewlyEquippedWeapon(int weaponNumber)
    {
        if (weaponNumber <= 0)
        {
            return;
        }

        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null)
        {
            return;
        }

        ApplyMetaStartingUpgradesToWeapon(
            weaponNumber,
            meta.AttackPowerLevel,
            meta.AttackCountLevel,
            meta.CooldownReductionLevel);
    }

    /// <summary>
    /// slotIndex: 0=Main, 1..4=SubUnit01..04
    /// </summary>
    public bool TryGetEquippedWeaponForSlot(int slotIndex, out int weaponNumber, out Sprite weaponSprite)
    {
        weaponNumber = 0;
        weaponSprite = null;

        UnitWeaponBinding binding = slotIndex switch
        {
            0 => mainUnitWeapon,
            1 => unit01Weapon,
            2 => unit02Weapon,
            3 => unit03Weapon,
            4 => unit04Weapon,
            _ => null
        };

        if (binding == null)
        {
            return false;
        }

        WeaponMode mode = binding.SelectedMode;
        weaponNumber = ModeToWeaponNumber(mode);
        if (weaponNumber <= 0)
        {
            return false;
        }

        weaponSprite = GetWeaponUpgradeIconSprite(binding);
        return true;
    }

    /// <summary>装備武器番号（1..7）に対応する残像プレハブ（Weapon*ImagePrefab）。</summary>
    public bool TryGetWeaponAfterImagePrefab(int weaponNumber, out RectTransform prefab)
    {
        prefab = null;
        if (weaponNumber < 1 || weaponNumber > 7)
        {
            return false;
        }

        int index = weaponNumber - 1;
        if (weaponAfterImagePrefabs == null || index >= weaponAfterImagePrefabs.Length)
        {
            return false;
        }

        prefab = weaponAfterImagePrefabs[index];
        return prefab != null;
    }

    /// <summary>スロット装備武器の残像プレハブ（slotIndex 1..4 = Unit01..04）。未装備なら false。</summary>
    public bool TryGetAfterImagePrefabForEquippedSlot(int slotIndex, out RectTransform prefab)
    {
        prefab = null;
        if (!TryGetEquippedWeaponForSlot(slotIndex, out int weaponNumber, out _))
        {
            return false;
        }

        return TryGetWeaponAfterImagePrefab(weaponNumber, out prefab);
    }

    private void Start()
    {
        BuildRuntimeStates();
    }

    /// <summary>
    /// メタ UG をラン開始時に適用（in-run UG カウントには加算しない）。spec/game03/game03_meta_growth_menu03_spec.md §7
    /// </summary>
    public void ApplyMetaStartingUpgrades(int attackPowerMetaLevel, int attackCountMetaLevel, int cooldownReductionMetaLevel)
    {
        EnsureRuntimeStatesCurrent();
        if (attackPowerMetaLevel <= 0 && attackCountMetaLevel <= 0 && cooldownReductionMetaLevel <= 0)
        {
            return;
        }

        HashSet<int> weaponNumbers = new HashSet<int>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Binding == null)
            {
                continue;
            }

            int weaponNumber = ModeToWeaponNumber(state.Mode);
            if (weaponNumber > 0)
            {
                weaponNumbers.Add(weaponNumber);
            }
        }

        foreach (int weaponNumber in weaponNumbers)
        {
            ApplyMetaStartingUpgradesToWeapon(
                weaponNumber,
                attackPowerMetaLevel,
                attackCountMetaLevel,
                cooldownReductionMetaLevel);
        }
    }

    private void ApplyMetaStartingUpgradesToWeapon(
        int weaponNumber,
        int attackPowerMetaLevel,
        int attackCountMetaLevel,
        int cooldownReductionMetaLevel)
    {
        if (weaponNumber <= 0)
        {
            return;
        }

        for (int lv = 0; lv < attackPowerMetaLevel; lv++)
        {
            if (IsUpgradeTypeInWeaponLotteryPool(weaponNumber, Game03UpgradeType.AttackPowerUp))
            {
                ApplyUpgradeToWeaponWithoutCounting(weaponNumber, Game03UpgradeType.AttackPowerUp);
            }
        }

        for (int lv = 0; lv < attackCountMetaLevel; lv++)
        {
            if (IsUpgradeTypeInWeaponLotteryPool(weaponNumber, Game03UpgradeType.ProjectileCountUp))
            {
                ApplyUpgradeToWeaponWithoutCounting(weaponNumber, Game03UpgradeType.ProjectileCountUp);
            }
        }

        for (int lv = 0; lv < cooldownReductionMetaLevel; lv++)
        {
            if (IsUpgradeTypeInWeaponLotteryPool(weaponNumber, Game03UpgradeType.CooldownReduction))
            {
                ApplyUpgradeToWeaponWithoutCounting(weaponNumber, Game03UpgradeType.CooldownReduction);
            }
        }
    }

    /// <summary>Pod 加入後 LVup など、総合 UG 回数・Lv 集計に載せない適用。</summary>
    public bool ApplyLevelUpUpgradeWithoutCounting(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponWithoutCounting(weaponNumber, upgradeType);
    }

    private bool ApplyUpgradeToWeaponWithoutCounting(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return weaponNumber switch
        {
            1 => ApplyWeapon01Upgrade(weaponNumber, upgradeType),
            2 => ApplyWeapon02Upgrade(upgradeType),
            3 => ApplyWeapon03Upgrade(weaponNumber, upgradeType),
            4 => ApplyWeapon04Upgrade(weaponNumber, upgradeType),
            5 => ApplyWeapon05Upgrade(weaponNumber, upgradeType),
            6 => ApplyWeapon06Upgrade(weaponNumber, upgradeType),
            7 => ApplyWeapon07Upgrade(weaponNumber, upgradeType),
            _ => false
        };
    }

    public void GetUpgradeableWeaponCandidates(List<UpgradeWeaponCandidate> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        EnsureRuntimeStatesCurrent();
        HashSet<int> added = new HashSet<int>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Binding == null)
            {
                continue;
            }

            int weaponNumber = ModeToWeaponNumber(state.Mode);
            if (weaponNumber <= 0 || added.Contains(weaponNumber))
            {
                continue;
            }

            Sprite weaponIcon = GetWeaponUpgradeIconSprite(state.Binding);
            results.Add(new UpgradeWeaponCandidate(weaponNumber, weaponIcon, state.Binding.EquippedUnitRect));
            added.Add(weaponNumber);
        }
    }

    private Sprite GetWeaponUpgradeIconSprite(UnitWeaponBinding binding)
    {
        RectTransform weaponRect = binding != null ? GetSelectedWeaponRect(binding) : null;
        if (weaponRect == null)
        {
            return null;
        }

        // Prefer Weapon*Oj/**/WeaponBodyImage as requested (recursive search).
        Image[] images = weaponRect.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.name == "WeaponBodyImage" && image.sprite != null)
            {
                return image.sprite;
            }
        }

        Image fallback = weaponRect.GetComponent<Image>();
        if (fallback != null && fallback.sprite != null)
        {
            return fallback.sprite;
        }

        // Last fallback: any child Image sprite.
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.sprite != null)
            {
                return image.sprite;
            }
        }

        return null;
    }

    public int GetTotalUpgradeCountForWeapon(int weaponNumber)
    {
        return totalUpgradeCountByWeaponNumber.TryGetValue(weaponNumber, out int c) ? c : 0;
    }

    /// <summary>装備中の各武器（重複なし）の UG 回数合計。ResultTextLv 用。</summary>
    public int GetTotalUpgradeCountForAllEquippedWeapons()
    {
        EnsureRuntimeStatesCurrent();
        int sum = 0;
        HashSet<int> added = new HashSet<int>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Binding == null)
            {
                continue;
            }

            int weaponNumber = ModeToWeaponNumber(state.Mode);
            if (weaponNumber <= 0 || !added.Add(weaponNumber))
            {
                continue;
            }

            sum += GetTotalUpgradeCountForWeapon(weaponNumber);
        }

        return sum;
    }

    public int GetUpgradeCountForWeaponType(int weaponNumber, Game03UpgradeType upgradeType)
    {
        if (!upgradeCountByWeaponAndType.TryGetValue(weaponNumber, out Dictionary<Game03UpgradeType, int> map))
        {
            return 0;
        }

        return map.TryGetValue(upgradeType, out int c) ? c : 0;
    }

    /// <summary>
    /// 装備中の各武器（重複なし）に LvUp と同じ UG を 1 段階適用。リザルトの UG 集計にも反映される。
    /// </summary>
    /// <returns>実際に適用できた武器数。</returns>
    public int TryApplyLevelUpUpgradeToAllEquippedWeapons(Game03UpgradeType upgradeType)
    {
        EnsureRuntimeStatesCurrent();
        HashSet<int> weaponNumbers = new HashSet<int>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Binding == null)
            {
                continue;
            }

            int weaponNumber = ModeToWeaponNumber(state.Mode);
            if (weaponNumber > 0)
            {
                weaponNumbers.Add(weaponNumber);
            }
        }

        int appliedCount = 0;
        foreach (int weaponNumber in weaponNumbers)
        {
            if (!IsUpgradeTypeInWeaponLotteryPool(weaponNumber, upgradeType))
            {
                continue;
            }

            if (ApplyLevelUpUpgrade(weaponNumber, upgradeType))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    public bool ApplyLevelUpUpgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        bool applied = weaponNumber switch
        {
            1 => ApplyWeapon01Upgrade(weaponNumber, upgradeType),
            2 => ApplyWeapon02Upgrade(upgradeType),
            3 => ApplyWeapon03Upgrade(weaponNumber, upgradeType),
            4 => ApplyWeapon04Upgrade(weaponNumber, upgradeType),
            5 => ApplyWeapon05Upgrade(weaponNumber, upgradeType),
            6 => ApplyWeapon06Upgrade(weaponNumber, upgradeType),
            7 => ApplyWeapon07Upgrade(weaponNumber, upgradeType),
            _ => false
        };

        if (!applied)
        {
            return false;
        }

        totalUpgradeCountByWeaponNumber.TryGetValue(weaponNumber, out int total);
        totalUpgradeCountByWeaponNumber[weaponNumber] = total + 1;
        if (!upgradeCountByWeaponAndType.TryGetValue(weaponNumber, out Dictionary<Game03UpgradeType, int> map))
        {
            map = new Dictionary<Game03UpgradeType, int>();
            upgradeCountByWeaponAndType[weaponNumber] = map;
        }

        map.TryGetValue(upgradeType, out int typedCount);
        map[upgradeType] = typedCount + 1;

        if (weaponNumber == 3
            && upgradeType == Game03UpgradeType.ProjectileCountUp
            && Game03RunSessionState.IsBossWeaponEnhanced(3))
        {
            SpawnAdditionalBossOrbitProjectiles(1);
        }

        return true;
    }

    private void Update()
    {
        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        if (game03UnitManager == null || game03EnemyManager == null)
        {
            return;
        }

        EnsureRuntimeStatesCurrent();

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            float gameplayDt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
            UpdateWeapon(runtimeStates[i], gameplayDt);
        }
    }

    private void BuildRuntimeStates()
    {
        ClearAllRuntimeStates();
        runtimeStates.Clear();
        AddRuntimeStateIfValid(mainUnitWeapon);
        AddRuntimeStateIfValid(unit01Weapon);
        AddRuntimeStateIfValid(unit02Weapon);
        AddRuntimeStateIfValid(unit03Weapon);
        AddRuntimeStateIfValid(unit04Weapon);
        lastBindingsSignature = CalculateBindingsSignature();
    }

    private float ComposeWeaponKnockbackPx(float weaponIntrinsicKnockPx)
    {
        return Mathf.Max(0f, weaponIntrinsicKnockPx + Mathf.Max(0f, globalWeaponKnockbackAddPx));
    }

    private bool ApplyWeapon01Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        bool applied = false;
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Mode != WeaponMode.Melee01 || state.Binding == null)
            {
                continue;
            }

            Weapon01Stats stats = GetWeapon01Stats(state.Binding);
            if (stats == null)
            {
                continue;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(8);
                    break;
                case Game03UpgradeType.AttackSpeedUp:
                    stats.ApplyAttackSpeedDisplaySecondsMultiplier(0.86f);
                    break;
                case Game03UpgradeType.AttackRangeUp:
                    // 旧 1.08/段（Lv6≈1.59×）→ Lv3 で同程度: 1.08²/段
                    stats.ApplyRangeMultiplier(1.1664f);
                    break;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownIntervalMultiplier(0.88f);
                    break;
                case Game03UpgradeType.DurationUp:
                    stats.ApplyDurationMultiplier(1.15f);
                    break;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.15f);
                    break;
                case Game03UpgradeType.PierceUp:
                    stats.ApplyPierceBonus(1);
                    break;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    break;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    break;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyShotCountPlusOne();
                    break;
                default:
                    return false;
            }

            applied = true;
        }

        return applied;
    }

    private bool ApplyWeapon02Upgrade(Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Mud02, binding =>
        {
            Weapon02Stats stats = GetWeapon02Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(8);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    stats.ApplyAttackRangeScaleMultiplier(1.15f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.DurationUp:
                    stats.ApplyDurationMultiplier(1.15f);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelDeployed(GetUpgradeCountForWeaponType(2, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyWeapon03Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Orbit03, binding =>
        {
            Weapon03Stats stats = GetWeapon03Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(7);
                    return true;
                case Game03UpgradeType.AttackSpeedUp:
                    stats.ApplyOrbitTurnSecondsMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    stats.ApplyAttackRangeScaleMultiplier(1.15f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.DurationUp:
                    stats.ApplyDurationMultiplier(1.15f);
                    return true;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.06f);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyWeapon04Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Claw04, binding =>
        {
            Weapon04Stats stats = GetWeapon04Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(9);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    // 旧 1.12/段（Lv6≈1.97×）→ Lv3 で同程度: 1.12²/段
                    stats.ApplyRangeMultiplier(1.2544f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.9f);
                    return true;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.1f);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyWeapon05Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Ranged05, binding =>
        {
            Weapon05Stats stats = GetWeapon05Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(7);
                    return true;
                case Game03UpgradeType.AttackSpeedUp:
                    stats.ApplySpeedMultiplier(1.1f);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    stats.ApplyAttackRangeScaleMultiplier(1.12f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.1f);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyWeapon06Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Magic06, binding =>
        {
            Weapon06Stats stats = GetWeapon06Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(6);
                    return true;
                case Game03UpgradeType.AttackSpeedUp:
                    stats.ApplySpeedMultiplier(1.1f);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    stats.ApplyAttackRangeScaleMultiplier(1.12f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.12f);
                    return true;
                case Game03UpgradeType.PierceUp:
                    stats.ApplyPierceKillBonus(1);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyWeapon07Upgrade(int weaponNumber, Game03UpgradeType upgradeType)
    {
        return ApplyUpgradeToWeaponMode(WeaponMode.Axe07, binding =>
        {
            Weapon07Stats stats = GetWeapon07Stats(binding);
            if (stats == null)
            {
                return false;
            }

            switch (upgradeType)
            {
                case Game03UpgradeType.AttackPowerUp:
                    stats.ApplyAttackPowerFlat(7);
                    return true;
                case Game03UpgradeType.AttackSpeedUp:
                    stats.ApplyInitialSpeedMultiplier(1.08f);
                    return true;
                case Game03UpgradeType.AttackRangeUp:
                    stats.ApplyAttackRangeScaleMultiplier(1.12f);
                    return true;
                case Game03UpgradeType.CooldownReduction:
                    stats.ApplyCooldownMultiplier(0.88f);
                    return true;
                case Game03UpgradeType.KnockbackUp:
                    stats.ApplyKnockbackMultiplier(1.1f);
                    return true;
                case Game03UpgradeType.PierceUp:
                    stats.ApplyMaxPierceBonus(1);
                    return true;
                case Game03UpgradeType.CriticalRateUp:
                    stats.ApplyCriticalRateLevelRule(GetUpgradeCountForWeaponType(weaponNumber, Game03UpgradeType.CriticalRateUp) + 1);
                    return true;
                case Game03UpgradeType.CriticalDamageUp:
                    stats.ApplyCriticalDamageMultiplierPlusOne();
                    return true;
                case Game03UpgradeType.ProjectileCountUp:
                    stats.ApplyAttackCountPlusOne();
                    return true;
                default:
                    return false;
            }
        });
    }

    private bool ApplyUpgradeToWeaponMode(WeaponMode mode, System.Func<UnitWeaponBinding, bool> applyToBinding)
    {
        bool applied = false;
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            if (state == null || state.Mode != mode || state.Binding == null)
            {
                continue;
            }

            if (applyToBinding(state.Binding))
            {
                applied = true;
            }
        }

        return applied;
    }

    private void EnsureRuntimeStatesCurrent()
    {
        int current = CalculateBindingsSignature();
        if (current == lastBindingsSignature)
        {
            return;
        }

        BuildRuntimeStates();
    }

    private int CalculateBindingsSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + CalcBindingSignature(mainUnitWeapon);
            hash = (hash * 31) + CalcBindingSignature(unit01Weapon);
            hash = (hash * 31) + CalcBindingSignature(unit02Weapon);
            hash = (hash * 31) + CalcBindingSignature(unit03Weapon);
            hash = (hash * 31) + CalcBindingSignature(unit04Weapon);

            if (sharedWeaponRects != null)
            {
                for (int i = 0; i < sharedWeaponRects.Count; i++)
                {
                    RectTransform r = sharedWeaponRects[i];
                    hash = (hash * 31) + (r != null ? r.GetInstanceID() : 0);
                }
            }

            return hash;
        }
    }

    private static int CalcBindingSignature(UnitWeaponBinding binding)
    {
        if (binding == null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (binding.UnitRect != null ? binding.UnitRect.GetInstanceID() : 0);
            hash = (hash * 31) + (int)binding.SelectedMode;
            return hash;
        }
    }

    private void ClearAllRuntimeStates()
    {
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState state = runtimeStates[i];
            ClearProjectiles(state);
            ClearMudProjectiles(state);
            ClearMudZones(state);
            ClearOrbitProjectiles(state);
            ClearAxeProjectiles(state);
            ClearMagicProjectiles(state);
            ClearClawVisual(state);
        }
    }

    private RectTransform GetSelectedWeaponRect(UnitWeaponBinding binding)
    {
        if (binding == null || sharedWeaponRects == null)
        {
            return null;
        }

        int index = ModeToSharedIndex(binding.SelectedMode);
        if (index < 0 || index >= sharedWeaponRects.Count)
        {
            return null;
        }

        return sharedWeaponRects[index];
    }

    private static int ModeToSharedIndex(WeaponMode mode)
    {
        switch (mode)
        {
            case WeaponMode.Melee01: return 0;
            case WeaponMode.Mud02: return 1;
            case WeaponMode.Orbit03: return 2;
            case WeaponMode.Claw04: return 3;
            case WeaponMode.Ranged05: return 4;
            case WeaponMode.Magic06: return 5;
            case WeaponMode.Axe07: return 6;
            default: return -1;
        }
    }

    private static int ModeToWeaponNumber(WeaponMode mode)
    {
        switch (mode)
        {
            case WeaponMode.Melee01: return 1;
            case WeaponMode.Mud02: return 2;
            case WeaponMode.Orbit03: return 3;
            case WeaponMode.Claw04: return 4;
            case WeaponMode.Ranged05: return 5;
            case WeaponMode.Magic06: return 6;
            case WeaponMode.Axe07: return 7;
            default: return 0;
        }
    }

    private Weapon01Stats GetWeapon01Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon01Stats>() : null;
    private Weapon02Stats GetWeapon02Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon02Stats>() : null;
    private Weapon03Stats GetWeapon03Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon03Stats>() : null;
    private Weapon04Stats GetWeapon04Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon04Stats>() : null;
    private Weapon05Stats GetWeapon05Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon05Stats>() : null;
    private Weapon06Stats GetWeapon06Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon06Stats>() : null;
    private Weapon07Stats GetWeapon07Stats(UnitWeaponBinding binding) => GetSelectedWeaponRect(binding) != null ? GetSelectedWeaponRect(binding).GetComponent<Weapon07Stats>() : null;

    private bool HasSelectedStats(UnitWeaponBinding binding)
    {
        switch (binding.SelectedMode)
        {
            case WeaponMode.Melee01: return GetWeapon01Stats(binding) != null;
            case WeaponMode.Mud02: return GetWeapon02Stats(binding) != null;
            case WeaponMode.Orbit03: return GetWeapon03Stats(binding) != null;
            case WeaponMode.Claw04: return GetWeapon04Stats(binding) != null;
            case WeaponMode.Ranged05: return GetWeapon05Stats(binding) != null;
            case WeaponMode.Magic06: return GetWeapon06Stats(binding) != null;
            case WeaponMode.Axe07: return GetWeapon07Stats(binding) != null;
            default: return false;
        }
    }

    private void AddRuntimeStateIfValid(UnitWeaponBinding binding)
    {
        if (binding == null || binding.UnitRect == null || GetSelectedWeaponRect(binding) == null)
        {
            return;
        }

        if (!HasSelectedStats(binding))
        {
            return;
        }

        runtimeStates.Add(CreateState(binding));
    }

    private WeaponRuntimeState CreateState(UnitWeaponBinding binding)
    {
        WeaponRuntimeState state = new WeaponRuntimeState
        {
            Binding = binding
        };

        RectTransform weaponRect = GetSelectedWeaponRect(binding);
        Weapon01Stats meleeStats = GetWeapon01Stats(binding);
        state.Mode = binding.SelectedMode;

        if (weaponRect != null)
        {
            state.WeaponBaseScale = weaponRect.localScale;
        }

        if (meleeStats != null && meleeStats.Wo1ImageRect != null)
        {
            EnsureWeapon01HitRectFollowsImageHierarchy(meleeStats);
            meleeStats.Wo1ImageRect.pivot = meleeStats.GetSwingPivotNormalized(meleeStats.Wo1ImageRect);
            state.ImageBaseScale = meleeStats.Wo1ImageRect.localScale;
            state.ImageBaseAnchoredPosition = meleeStats.Wo1ImageRect.anchoredPosition;
            meleeStats.Wo1ImageRect.localRotation = Quaternion.identity;
        }

        if (meleeStats != null && meleeStats.Wo01HitRect != null)
        {
            state.HitBaseScale = meleeStats.Wo01HitRect.localScale;
            state.HitBaseAnchoredPosition = meleeStats.Wo01HitRect.anchoredPosition;
        }

        if ((state.Mode == WeaponMode.Ranged05 || state.Mode == WeaponMode.Mud02 || state.Mode == WeaponMode.Orbit03 || state.Mode == WeaponMode.Axe07 || state.Mode == WeaponMode.Magic06 || state.Mode == WeaponMode.Claw04) && weaponRect != null)
        {
            weaponRect.gameObject.SetActive(false);
        }

        SetWeaponVisible(state, false);
        InitializeWeaponRuntimeColdStart(state);
        return state;
    }

    /// <summary>
    /// シーンロード・ランタイム状態の再構築・武器取得直後など、常に「クールダウン先頭」から始める。
    /// </summary>
    private static void InitializeWeaponRuntimeColdStart(WeaponRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        state.AttackTimer = 0f;
        state.AttackEffectRemaining = 0f;
        state.AttackActiveRemaining = 0f;
        state.AttackActiveDuration = 0f;
        state.PendingExtraMeleeShots = 0;
        state.MeleePostSwingHoldRemaining = 0f;
        state.MeleeInPostSwingHold = false;
        state.PendingWeapon05SetSpawns.Clear();

        state.ClawAttacking = false;
        state.ClawShotsFired = 0;
        state.ClawChainTimer = 0f;
        state.ClawWindDownSecondsRemaining = 0f;
        state.ClawHitIdsPerShot.Clear();
        state.ClawUnitHidden = false;
        state.LastClawImageId = 0;
        state.LastClawVisualPos = new Vector2(float.NaN, float.NaN);

        state.HitEnemyIdsInCurrentAttack.Clear();
    }

    private void UpdateWeapon(WeaponRuntimeState state, float deltaTime)
    {
        UnitWeaponBinding binding = state.Binding;
        if (binding == null)
        {
            return;
        }

        RectTransform unitRect = binding.UnitRect;
        RectTransform weaponRect = GetSelectedWeaponRect(binding);
        if (weaponRect == null || !HasSelectedStats(binding))
        {
            return;
        }

        if (unitRect == null || !unitRect.gameObject.activeInHierarchy)
        {
            SetWeaponVisible(state, false);
            ClearProjectiles(state);
            ClearMudProjectiles(state);
            ClearMudZones(state);
            ClearOrbitProjectiles(state);
            ClearAxeProjectiles(state);
            ClearMagicProjectiles(state);
            ClearClawVisual(state);
            state.AttackActiveRemaining = 0f;
            state.AttackEffectRemaining = 0f;
            return;
        }

        if (state.Mode == WeaponMode.Ranged05)
        {
            UpdateRangedWeapon(state, GetWeapon05Stats(binding), deltaTime);
            return;
        }

        if (state.Mode == WeaponMode.Mud02)
        {
            UpdateMudWeapon(state, GetWeapon02Stats(binding), deltaTime);
            return;
        }

        if (state.Mode == WeaponMode.Orbit03)
        {
            UpdateOrbitWeapon(state, GetWeapon03Stats(binding), deltaTime);
            return;
        }

        if (state.Mode == WeaponMode.Axe07)
        {
            UpdateAxeWeapon(state, GetWeapon07Stats(binding), deltaTime);
            return;
        }

        if (state.Mode == WeaponMode.Magic06)
        {
            UpdateMagicWeapon(state, GetWeapon06Stats(binding), deltaTime);
            return;
        }

        if (state.Mode == WeaponMode.Claw04)
        {
            UpdateClawWeapon(state, GetWeapon04Stats(binding), deltaTime);
            return;
        }

        Weapon01Stats meleeStats = GetWeapon01Stats(binding);
        if (meleeStats == null)
        {
            return;
        }

        RectTransform targetParent = weaponRect.parent as RectTransform;
        if (targetParent != null && game03UnitManager.TryGetUnitCenterOnRect(unitRect, targetParent, out Vector2 localCenter))
        {
            int displayFacing = (state.AttackEffectRemaining > 0f || state.AttackActiveRemaining > 0f)
                ? state.CurrentAttackFacing
                : game03UnitManager.LastHorizontalFacing;
            Vector2 offset = meleeStats.DisplayOffsetFromUnit;
            float offsetX = Mathf.Abs(meleeStats.DisplayOffsetXFromUnit);
            offset.x = offsetX * (displayFacing >= 0 ? 1f : -1f);
            weaponRect.anchoredPosition = localCenter + offset;
        }

        // 振り・静止・追加振り・表示中は間隔タイマーを進めない（終了後フル間隔から）。
        bool meleeCooldownIdle = IsMeleeAttackSequenceComplete(state);
        if (meleeCooldownIdle)
        {
            state.AttackTimer += deltaTime;
            if (state.AttackTimer >= Mathf.Max(0.01f, meleeStats.AttackIntervalSeconds))
            {
                state.AttackTimer = 0f;
                StartAttack(state);
            }
        }

        if (state.AttackEffectRemaining > 0f)
        {
            state.AttackEffectRemaining -= deltaTime;
            if (state.AttackEffectRemaining < 0f)
            {
                state.AttackEffectRemaining = 0f;
            }
        }

        if (state.MeleeInPostSwingHold)
        {
            state.MeleePostSwingHoldRemaining -= deltaTime;
            ApplySwingEndRotation(state);
            if (state.MeleePostSwingHoldRemaining <= 0f)
            {
                state.MeleeInPostSwingHold = false;
                state.MeleePostSwingHoldRemaining = 0f;
                if (state.PendingExtraMeleeShots > 0)
                {
                    state.PendingExtraMeleeShots--;
                    StartExtraMeleeShot(state);
                }
            }
        }
        else if (state.AttackActiveRemaining > 0f)
        {
            state.AttackActiveRemaining -= deltaTime;
            UpdateSwingRotation(state);
            ApplyAttackHit(state);
            if (state.AttackActiveRemaining <= 0f)
            {
                state.AttackActiveRemaining = 0f;
                BeginMeleePostSwingHold(state);
            }
        }
        else if (state.AttackEffectRemaining > 0f)
        {
            ApplySwingEndRotation(state);
        }
        else
        {
            ApplyIdleRotation(state);
        }

        if (IsMeleeAttackSequenceComplete(state))
        {
            SetWeaponVisible(state, false);
        }
    }

    /// <summary>振り終了・静止・2撃目待ちがすべて終わったら true（次の attackInterval へ）。</summary>
    private static bool IsMeleeAttackSequenceComplete(WeaponRuntimeState state)
    {
        if (state == null)
        {
            return false;
        }

        return state.AttackEffectRemaining <= 0f
            && state.AttackActiveRemaining <= 0f
            && !state.MeleeInPostSwingHold
            && state.PendingExtraMeleeShots <= 0;
    }

    /// <summary>1撃ごと: active で振り切り → max(0, display−active) 静止 → 次の一撃／次攻撃。</summary>
    private static void BeginMeleeSwingPhase(WeaponRuntimeState state, Weapon01Stats stats)
    {
        if (state == null || stats == null)
        {
            return;
        }

        float displaySec = Mathf.Max(0f, stats.AttackEffectDisplaySeconds);
        float activeSec = Mathf.Max(0.0001f, stats.AttackActiveSeconds);
        state.AttackActiveDuration = activeSec;
        state.AttackActiveRemaining = activeSec;
        state.MeleeInPostSwingHold = false;
        state.MeleePostSwingHoldRemaining = Mathf.Max(0f, displaySec - activeSec);
        state.AttackEffectRemaining = Mathf.Max(displaySec, activeSec);
    }

    /// <summary>1振り分の active を終えたあと、終端角度で静止してから 2撃目／次サイクルへ。</summary>
    private void BeginMeleePostSwingHold(WeaponRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        ApplySwingEndRotation(state);
        if (state.MeleePostSwingHoldRemaining > 0f)
        {
            state.MeleeInPostSwingHold = true;
            return;
        }

        if (state.PendingExtraMeleeShots > 0)
        {
            state.PendingExtraMeleeShots--;
            StartExtraMeleeShot(state);
        }
    }

    private void StartAttack(WeaponRuntimeState state)
    {
        UnitWeaponBinding binding = state.Binding;
        Weapon01Stats stats = binding != null ? GetWeapon01Stats(binding) : null;
        if (stats == null)
        {
            return;
        }

        state.CurrentAttackFacing = game03UnitManager.LastHorizontalFacing;
        state.HitEnemyIdsInCurrentAttack.Clear();

        ApplyWeaponDirectionAndScale(state, state.CurrentAttackFacing, Mathf.Max(0.01f, stats.AttackRangeBaseScale));
        SetWeaponVisible(state, true);
        int shotCount = Mathf.Max(1, stats.ShotCount);
        state.PendingExtraMeleeShots = Mathf.Max(0, shotCount - 1);
        BeginMeleeSwingPhase(state, stats);
        UpdateSwingRotation(state);
        TryPlayWeaponAttackStartSe(1);
    }

    private void StartExtraMeleeShot(WeaponRuntimeState state)
    {
        state.CurrentAttackFacing = -state.CurrentAttackFacing;
        state.HitEnemyIdsInCurrentAttack.Clear();
        UnitWeaponBinding binding = state.Binding;
        Weapon01Stats stats = binding != null ? GetWeapon01Stats(binding) : null;
        if (stats == null)
        {
            return;
        }

        ApplyWeaponDirectionAndScale(state, state.CurrentAttackFacing, Mathf.Max(0.01f, stats.AttackRangeBaseScale));
        BeginMeleeSwingPhase(state, stats);
        UpdateSwingRotation(state);
        TryPlayWeaponAttackStartSe(1);
    }

    private void ApplyAttackHit(WeaponRuntimeState state)
    {
        UnitWeaponBinding binding = state.Binding;
        Weapon01Stats stats = binding != null ? GetWeapon01Stats(binding) : null;
        if (stats == null)
        {
            if (debugLogMeleeWeaponHitProbe)
            {
                Debug.Log($"[Game03MeleeHit] frame={Time.frameCount} Weapon01Stats が取得できません（バインディングまたはコンポーネント）");
            }

            return;
        }

        RectTransform hitRect = stats.Wo01HitRect != null ? stats.Wo01HitRect : stats.Wo1ImageRect;
        if (hitRect == null)
        {
            if (debugLogMeleeWeaponHitProbe)
            {
                Debug.Log($"[Game03MeleeHit] frame={Time.frameCount} Wo01HitRect / Wo1ImageRect が未設定のためヒットしません");
            }

            return;
        }

        if (game03EnemyManager == null)
        {
            if (debugLogMeleeWeaponHitProbe)
            {
                Debug.Log($"[Game03MeleeHit] frame={Time.frameCount} Game03EnemyManager が未設定です");
            }

            return;
        }

        Rect hitRectOnEnemyRoot = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
        int dmg = Mathf.Max(1, stats.RollAttackDamage());
        if (debugLogMeleeWeaponHitProbe)
        {
            Debug.Log(
                $"[Game03MeleeHit] frame={Time.frameCount} dmg={dmg} continuous={stats.ContinuousHit} " +
                $"hitRect(enemyRoot)=({hitRectOnEnemyRoot.xMin:F1},{hitRectOnEnemyRoot.yMin:F1})-({hitRectOnEnemyRoot.xMax:F1},{hitRectOnEnemyRoot.yMax:F1}) " +
                $"size=({hitRectOnEnemyRoot.width:F1}x{hitRectOnEnemyRoot.height:F1}) src={(stats.Wo01HitRect != null ? "Wo01HitRect" : "Wo1ImageRect")}");
        }

        game03EnemyManager.ApplyWeaponHitRect(
            hitRectOnEnemyRoot,
            stats.ContinuousHit,
            state.HitEnemyIdsInCurrentAttack,
            dmg,
            ComposeWeaponKnockbackPx(stats.KisoKnockbackStrengthPx),
            out _,
            1);
    }

    /// <summary>
    /// W01hit が画像と兄弟だと <see cref="UpdateSwingRotation"/> の回転がヒットに伝わらないため、画像の子へ移す。
    /// ピボット設定より<strong>前</strong>に呼ぶ。画像ピボット変更後の <c>worldPositionStays</c> だとローカル座標が刃方向へ大きくずれるため、
    /// 兄弟時の <c>anchoredPosition</c> 差分を画像ローカルのオフセットとして維持する。
    /// </summary>
    private static void EnsureWeapon01HitRectFollowsImageHierarchy(Weapon01Stats meleeStats)
    {
        if (meleeStats == null)
        {
            return;
        }

        RectTransform imageRt = meleeStats.Wo1ImageRect;
        RectTransform hitRt = meleeStats.Wo01HitRect;
        if (imageRt == null || hitRt == null || hitRt.parent == imageRt)
        {
            return;
        }

        Transform imageParent = imageRt.parent;
        if (hitRt.parent != imageParent || imageParent == null)
        {
            return;
        }

        Vector2 hitAnchoredMinusImage = hitRt.anchoredPosition - imageRt.anchoredPosition;
        Vector3 hitLocalScale = hitRt.localScale;
        hitRt.SetParent(imageRt, false);
        hitRt.localScale = hitLocalScale;
        hitRt.localRotation = Quaternion.identity;
        hitRt.anchoredPosition = hitAnchoredMinusImage;
    }

    private void ApplyWeaponDirectionAndScale(WeaponRuntimeState state, int facing, float rangeScale)
    {
        UnitWeaponBinding binding = state.Binding;
        RectTransform weaponRect = binding != null ? GetSelectedWeaponRect(binding) : null;
        Weapon01Stats stats = binding != null ? GetWeapon01Stats(binding) : null;
        if (weaponRect != null)
        {
            Vector3 s = state.WeaponBaseScale;
            s.x = Mathf.Abs(state.WeaponBaseScale.x);
            s.y = Mathf.Abs(state.WeaponBaseScale.y);
            weaponRect.localScale = s;
            weaponRect.localRotation = Quaternion.identity;
        }

        if (stats == null)
        {
            return;
        }

        RectTransform wo1ImageRect = stats.Wo1ImageRect;
        if (wo1ImageRect != null)
        {
            wo1ImageRect.pivot = stats.GetSwingPivotNormalized(wo1ImageRect);
            wo1ImageRect.localScale = state.ImageBaseScale * rangeScale;
            Vector2 p = state.ImageBaseAnchoredPosition;
            p += new Vector2(
                stats.SwingPivotOffsetPixels.x * (facing >= 0 ? 1f : -1f),
                stats.SwingPivotOffsetPixels.y);
            p.x = Mathf.Abs(p.x) * (facing >= 0 ? 1f : -1f);
            wo1ImageRect.anchoredPosition = p;
            Vector3 imageScale = wo1ImageRect.localScale;
            imageScale.y = Mathf.Abs(imageScale.y) * (facing >= 0 ? 1f : -1f);
            wo1ImageRect.localScale = imageScale;
        }

        RectTransform hitRect = stats.Wo01HitRect;
        if (hitRect != null)
        {
            Vector3 hs = state.HitBaseScale * rangeScale;
            hs.x = Mathf.Abs(hs.x);
            hs.y = Mathf.Abs(hs.y);
            hitRect.localScale = hs;

            Vector2 hitPos = state.HitBaseAnchoredPosition;
            // 画像の子のときは向きは親の scale / position で反映済みのため、兄弟時代の X ミラーを二重にかけない。
            if (wo1ImageRect == null || hitRect.parent != wo1ImageRect)
            {
                hitPos.x = Mathf.Abs(state.HitBaseAnchoredPosition.x) * (facing >= 0 ? 1f : -1f);
            }

            hitRect.anchoredPosition = hitPos;
        }
    }

    private void UpdateSwingRotation(WeaponRuntimeState state)
    {
        Weapon01Stats stats = state.Binding != null ? GetWeapon01Stats(state.Binding) : null;
        if (stats == null || stats.Wo1ImageRect == null)
        {
            return;
        }

        GetSwingAngles(stats, out float startRel, out float endRel);
        float t = 1f - Mathf.Clamp01(state.AttackActiveRemaining / Mathf.Max(0.0001f, state.AttackActiveDuration));
        float rel = Mathf.Lerp(startRel, endRel, t);
        ApplyWeapon01SwingLocalRotation(stats, state.CurrentAttackFacing, rel);
    }

    private void ApplyIdleRotation(WeaponRuntimeState state)
    {
        Weapon01Stats stats = state.Binding != null ? GetWeapon01Stats(state.Binding) : null;
        if (stats == null || stats.Wo1ImageRect == null)
        {
            return;
        }

        int idleFacing = game03UnitManager != null ? game03UnitManager.LastHorizontalFacing : state.CurrentAttackFacing;
        float baseAngle = idleFacing >= 0 ? 0f : 180f;
        stats.Wo1ImageRect.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
    }

    private void ApplySwingEndRotation(WeaponRuntimeState state)
    {
        Weapon01Stats stats = state.Binding != null ? GetWeapon01Stats(state.Binding) : null;
        if (stats == null || stats.Wo1ImageRect == null)
        {
            return;
        }

        GetSwingAngles(stats, out _, out float endRel);
        ApplyWeapon01SwingLocalRotation(stats, state.CurrentAttackFacing, endRel);
    }

    /// <summary>
    /// 右: base 0° + 相対角（例 40°→-60° で上から下へ）。
    /// 左（2撃目）: base 180° + 相対角の符号反転（例 -40°→+60° 相当で 140°→240°、画面でも上から下）。
    /// </summary>
    private static void ApplyWeapon01SwingLocalRotation(Weapon01Stats stats, int facing, float relDegrees)
    {
        if (stats == null || stats.Wo1ImageRect == null)
        {
            return;
        }

        if (facing < 0)
        {
            relDegrees = -relDegrees;
        }

        float baseAngle = facing >= 0 ? 0f : 180f;
        stats.Wo1ImageRect.localRotation = Quaternion.Euler(0f, 0f, baseAngle + relDegrees);
    }

    /// <summary>Inspector の start/end を範囲 UG 倍率込みで返す（向きによる反転は <see cref="ApplyWeapon01SwingLocalRotation"/>）。</summary>
    private static void GetSwingAngles(Weapon01Stats stats, out float startRel, out float endRel)
    {
        float defaultStart = stats.AttackStartAngleDegrees;
        float defaultEnd = stats.AttackEndAngleDegrees;
        float center = (defaultStart + defaultEnd) * 0.5f;
        float width = (defaultStart - defaultEnd) * Mathf.Max(0f, stats.SwingAngleWidthMultiplier);
        float scaledStart = center + (width * 0.5f);
        float scaledEnd = center - (width * 0.5f);

        startRel = scaledStart;
        endRel = scaledEnd;
    }

    private void SetWeaponVisible(WeaponRuntimeState state, bool visible)
    {
        RectTransform weaponRect = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        Weapon01Stats stats = state.Binding != null ? GetWeapon01Stats(state.Binding) : null;

        if (state.Mode != WeaponMode.Melee01)
        {
            return;
        }

        if (weaponRect != null && weaponRect.gameObject.activeSelf != visible)
        {
            weaponRect.gameObject.SetActive(visible);
        }

        if (stats != null && stats.Wo1Image != null)
        {
            stats.Wo1Image.enabled = visible;
        }
    }

    private void UpdateRangedWeapon(WeaponRuntimeState state, Weapon05Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        ProcessWeapon05PendingSetSpawns(state, stats, deltaTime);

        // 針の存続中・段階スポーン保留中はクールダウンを進めない（ロード／取得直後も波の後フルCD）。
        bool rangedCooldownIdle = state.Projectiles.Count == 0 && state.PendingWeapon05SetSpawns.Count == 0;
        if (rangedCooldownIdle)
        {
            state.AttackTimer += deltaTime;
            if (state.AttackTimer >= Mathf.Max(0.01f, stats.CooldownSeconds))
            {
                state.AttackTimer = 0f;
                SpawnRangedProjectiles(state, stats);
            }
        }

        Vector2 localScrollDelta = Vector2.zero;
        RectTransform weaponParent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;
        Vector2 worldDelta = game03UnitManager != null ? game03UnitManager.LastAppliedFieldDeltaWorld : Vector2.zero;
        if (worldDelta.sqrMagnitude > 0.0000001f && weaponParent != null)
        {
            game03UnitManager.TryConvertWorldDeltaToLocalOnRect(weaponParent, worldDelta, out localScrollDelta);
        }

        for (int i = state.Projectiles.Count - 1; i >= 0; i--)
        {
            ProjectileState p = state.Projectiles[i];
            if (p == null || p.Rect == null)
            {
                state.Projectiles.RemoveAt(i);
                continue;
            }

            float move = stats.SpeedPixelsPerSecond * deltaTime;
            p.TraveledPixels += move;
            p.AliveSeconds += deltaTime;
            Vector2 dir = p.TravelDirection.sqrMagnitude > 0.0001f ? p.TravelDirection.normalized : Vector2.right;
            Vector2 pos = p.Rect.anchoredPosition;
            pos += localScrollDelta;
            pos += dir * move;
            p.Rect.anchoredPosition = pos;

            if (TryHitWithProjectile(state, stats, p, out bool shouldDestroy) && shouldDestroy)
            {
                DestroyProjectile(state, i);
                continue;
            }

            if (p.ThrowDistancePixels > 0f && p.TraveledPixels >= p.ThrowDistancePixels)
            {
                DestroyProjectile(state, i);
                continue;
            }

            if (p.LifeSeconds > 0f && p.AliveSeconds >= p.LifeSeconds)
            {
                DestroyProjectile(state, i);
                continue;
            }

            if (IsOutOfScreenArea(p.Rect))
            {
                DestroyProjectile(state, i);
            }
        }
    }

    private void UpdateMudWeapon(WeaponRuntimeState state, Weapon02Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform parent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;

        Vector2 localScrollDelta = Vector2.zero;
        Vector2 worldDelta = game03UnitManager != null ? game03UnitManager.LastAppliedFieldDeltaWorld : Vector2.zero;
        if (worldDelta.sqrMagnitude > 0.0000001f && parent != null)
        {
            game03UnitManager.TryConvertWorldDeltaToLocalOnRect(parent, worldDelta, out localScrollDelta);
        }

        UpdateMudProjectiles(state, stats, deltaTime, localScrollDelta);
        UpdateMudZones(state, deltaTime, worldDelta);

        if (state.MudProjectiles.Count > 0 || state.MudZones.Count > 0)
        {
            return;
        }

        state.AttackTimer += deltaTime;
        if (state.AttackTimer >= Mathf.Max(0.01f, stats.CooldownSeconds))
        {
            state.AttackTimer = 0f;
            SpawnMudProjectiles(state, stats, parent);
        }
    }

    private void SpawnRangedProjectiles(WeaponRuntimeState state, Weapon05Stats stats)
    {
        UnitWeaponBinding binding = state.Binding;
        if (binding == null || binding.UnitRect == null || GetSelectedWeaponRect(binding) == null)
        {
            return;
        }

        RectTransform parent = GetSelectedWeaponRect(binding).parent as RectTransform;
        if (parent == null || !game03UnitManager.TryGetUnitCenterOnRect(binding.UnitRect, parent, out _))
        {
            return;
        }

        state.PendingWeapon05SetSpawns.Clear();

        int count = Mathf.Max(1, stats.AttackCount);
        int setCount = Mathf.Max(1, Mathf.CeilToInt(count / 5f));

        int firstLastInclusive = Mathf.Min(4, count - 1);
        SpawnWeapon05ProjectileIndexRange(state, stats, 0, firstLastInclusive);

        float stagger = Mathf.Max(0f, stats.ExtraGroupSpawnDelaySeconds);
        for (int setIdx = 1; setIdx < setCount; setIdx++)
        {
            int first = setIdx * 5;
            int last = Mathf.Min(first + 4, count - 1);
            state.PendingWeapon05SetSpawns.Add(new PendingWeapon05SetSpawn
            {
                TimeRemaining = stagger * setIdx,
                FirstIndexInclusive = first,
                LastIndexInclusive = last
            });
        }
    }

    private void ProcessWeapon05PendingSetSpawns(WeaponRuntimeState state, Weapon05Stats stats, float deltaTime)
    {
        if (state.PendingWeapon05SetSpawns.Count == 0)
        {
            return;
        }

        for (int i = state.PendingWeapon05SetSpawns.Count - 1; i >= 0; i--)
        {
            PendingWeapon05SetSpawn pend = state.PendingWeapon05SetSpawns[i];
            pend.TimeRemaining -= deltaTime;
            if (pend.TimeRemaining > 0f)
            {
                continue;
            }

            SpawnWeapon05ProjectileIndexRange(state, stats, pend.FirstIndexInclusive, pend.LastIndexInclusive);
            state.PendingWeapon05SetSpawns.RemoveAt(i);
        }
    }

    private Vector2 GetWeapon05TravelForward()
    {
        if (game03UnitManager != null)
        {
            Vector2 dir = game03UnitManager.LastMovementIntentDirection;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }

        return Vector2.right;
    }

    private void SpawnWeapon05ProjectileIndexRange(WeaponRuntimeState state, Weapon05Stats stats, int firstIndexInclusive, int lastIndexInclusive)
    {
        UnitWeaponBinding binding = state.Binding;
        RectTransform parent = GetSelectedWeaponRect(binding)?.parent as RectTransform;
        if (binding == null || binding.UnitRect == null || parent == null
            || !game03UnitManager.TryGetUnitCenterOnRect(binding.UnitRect, parent, out Vector2 center))
        {
            return;
        }

        TryPlayWeaponAttackStartSe(5);

        Vector2 forward = GetWeapon05TravelForward();
        Vector2 leftHand = new Vector2(-forward.y, forward.x);

        int count = Mathf.Max(1, stats.AttackCount);
        int setCount = Mathf.Max(1, Mathf.CeilToInt(count / 5f));

        List<(ProjectileState p, int order)> spawned = new List<(ProjectileState p, int order)>(lastIndexInclusive - firstIndexInclusive + 1);
        for (int i = firstIndexInclusive; i <= lastIndexInclusive; i++)
        {
            int setIndex = i / 5;
            int inSet = i % 5;
            float baseDistance = GetSetBaseDistance(stats, setIndex, setCount);
            Vector2 local = center + forward * baseDistance;
            local += GetInSetOffset(inSet, forward, leftHand, stats.PlacementOffsetX, stats.PlacementOffsetY);

            ProjectileState p = CreateProjectile(state, stats, parent, local, forward);
            if (p == null)
            {
                continue;
            }

            int orderKey = (setIndex * 10) + (5 - Mathf.Min(inSet + 1, 5));
            spawned.Add((p, orderKey));
            state.Projectiles.Add(p);
        }

        spawned.Sort((a, b) => a.order.CompareTo(b.order));
        for (int i = 0; i < spawned.Count; i++)
        {
            spawned[i].p.Rect.SetAsLastSibling();
        }
    }

    private static float GetSetBaseDistance(Weapon05Stats stats, int setIndex, int setCount)
    {
        float maxD = Mathf.Max(stats.MinPlacementDistance, stats.MaxPlacementDistance);
        float minD = Mathf.Min(stats.MinPlacementDistance, stats.MaxPlacementDistance);
        return maxD - ((setIndex + 1f) * (maxD - minD) / (setCount + 1f));
    }

    private static Vector2 GetInSetOffset(int inSet, Vector2 forward, Vector2 leftHand, float offsetX, float offsetY)
    {
        if (inSet <= 0)
        {
            return Vector2.zero;
        }

        Vector2[] anchors = new Vector2[5];
        anchors[0] = Vector2.zero;
        anchors[1] = anchors[0] + leftHand * offsetY - forward * offsetX;
        anchors[2] = anchors[0] - leftHand * offsetY - forward * offsetX;
        anchors[3] = anchors[1] + leftHand * offsetY - forward * offsetX;
        anchors[4] = anchors[2] - leftHand * offsetY - forward * offsetX;
        return anchors[Mathf.Clamp(inSet, 0, 4)];
    }

    private ProjectileState CreateProjectile(WeaponRuntimeState state, Weapon05Stats stats, RectTransform parent, Vector2 localPos, Vector2 travelForward)
    {
        RectTransform templateRect = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (templateRect == null)
        {
            return null;
        }

        Vector2 dir = travelForward.sqrMagnitude > 0.0001f ? travelForward.normalized : Vector2.right;
        RectTransform instance = Instantiate(templateRect, parent);
        instance.gameObject.SetActive(true);
        instance.anchoredPosition = localPos;
        Vector3 scale = templateRect.localScale;
        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);
        instance.localScale = scale * Mathf.Max(0.01f, stats.AttackRangeScale <= 0f ? 1f : stats.AttackRangeScale);
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        instance.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        Weapon05Stats instanceStats = instance.GetComponent<Weapon05Stats>();
        RectTransform hitRect = instanceStats != null && instanceStats.Wo5HitRect != null ? instanceStats.Wo5HitRect : instanceStats?.Wo5ImageRect;
        Image image = instanceStats != null ? instanceStats.Wo5Image : instance.GetComponentInChildren<Image>();
        if (image != null)
        {
            image.enabled = true;
        }

        return new ProjectileState
        {
            Rect = instance,
            HitRect = hitRect,
            Image = image,
            TravelDirection = dir,
            ThrowDistancePixels = stats.ThrowDistancePixels,
            LifeSeconds = stats.LifeSeconds,
            SpeedPixelsPerSecond = stats.SpeedPixelsPerSecond
        };
    }

    private bool TryHitWithProjectile(WeaponRuntimeState state, Weapon05Stats stats, ProjectileState projectile, out bool shouldDestroy)
    {
        shouldDestroy = false;
        RectTransform hitRect = projectile.HitRect != null ? projectile.HitRect : projectile.Rect;
        if (hitRect == null)
        {
            return false;
        }

        Rect hitRectOnEnemyRoot = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
        int dmg = Mathf.Max(1, stats.RollAttackDamage());
        int pierceBudget = stats.DistinctEnemyPierceBudget;
        if (pierceBudget <= 1)
        {
            int killed = game03EnemyManager.ApplyWeaponHitRect(hitRectOnEnemyRoot, false, null, dmg, ComposeWeaponKnockbackPx(stats.KnockbackSmall), out int chip, 5);
            bool hit = killed > 0 || chip > 0;
            shouldDestroy = hit;
            return hit;
        }

        if (projectile.DamagedEnemyIds == null)
        {
            projectile.DamagedEnemyIds = new HashSet<int>();
        }

        int killedPierce = game03EnemyManager.ApplyWeaponHitRect(
            hitRectOnEnemyRoot,
            false,
            projectile.DamagedEnemyIds,
            dmg,
            ComposeWeaponKnockbackPx(stats.KnockbackSmall),
            out int chipPierce,
            5);
        bool hitPierce = killedPierce > 0 || chipPierce > 0;
        if (!hitPierce)
        {
            return false;
        }

        shouldDestroy = projectile.DamagedEnemyIds.Count >= pierceBudget;
        return true;
    }

    private static void DestroyProjectile(WeaponRuntimeState state, int index)
    {
        ProjectileState p = state.Projectiles[index];
        state.Projectiles.RemoveAt(index);
        if (p != null && p.Rect != null)
        {
            Destroy(p.Rect.gameObject);
        }
    }

    private static void ClearProjectiles(WeaponRuntimeState state)
    {
        for (int i = state.Projectiles.Count - 1; i >= 0; i--)
        {
            ProjectileState p = state.Projectiles[i];
            if (p != null && p.Rect != null)
            {
                Destroy(p.Rect.gameObject);
            }
        }

        state.Projectiles.Clear();
        state.PendingWeapon05SetSpawns.Clear();
    }

    private void SpawnMudProjectiles(WeaponRuntimeState state, Weapon02Stats stats, RectTransform parent)
    {
        UnitWeaponBinding binding = state.Binding;
        if (binding == null || binding.UnitRect == null || GetSelectedWeaponRect(binding) == null || parent == null)
        {
            return;
        }

        if (!game03UnitManager.TryGetUnitCenterOnRect(binding.UnitRect, parent, out Vector2 center))
        {
            return;
        }

        TryPlayWeaponAttackStartSe(2);

        List<float> angles = GenerateMudAngles(Mathf.Max(1, stats.AttackCount), Mathf.Max(0, stats.TargetSelectionSkipCount));
        for (int i = 0; i < angles.Count; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            Vector2 target = center + dir * stats.ThrowDistancePixels;
            MudProjectileState projectile = CreateMudProjectile(state, stats, parent, center, target);
            if (projectile != null)
            {
                state.MudProjectiles.Add(projectile);
            }
        }
    }

    private MudProjectileState CreateMudProjectile(WeaponRuntimeState state, Weapon02Stats stats, RectTransform parent, Vector2 startPos, Vector2 endPos)
    {
        RectTransform templateRect = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (templateRect == null)
        {
            return null;
        }
        RectTransform instance = Instantiate(templateRect, parent);
        instance.gameObject.SetActive(true);
        instance.anchoredPosition = startPos;
        instance.localScale = templateRect.localScale;
        instance.localRotation = Quaternion.identity;

        Weapon02Stats s = instance.GetComponent<Weapon02Stats>();
        if (s == null)
        {
            Destroy(instance.gameObject);
            return null;
        }

        SetMudVisualAsProjectile(s);
        return new MudProjectileState
        {
            Rect = instance,
            Stats = s,
            StartPos = startPos,
            EndPos = endPos,
            TravelSeconds = Mathf.Max(0.01f, stats.TravelSeconds),
            ArcMaxHeight = Mathf.Max(0f, stats.ArcMaxHeightPixels)
        };
    }

    private void UpdateMudProjectiles(WeaponRuntimeState state, Weapon02Stats stats, float deltaTime, Vector2 localScrollDelta)
    {
        for (int i = state.MudProjectiles.Count - 1; i >= 0; i--)
        {
            MudProjectileState p = state.MudProjectiles[i];
            if (p == null || p.Rect == null || p.Stats == null)
            {
                state.MudProjectiles.RemoveAt(i);
                continue;
            }

            p.StartPos += localScrollDelta;
            p.EndPos += localScrollDelta;
            p.Elapsed += deltaTime;
            float t = Mathf.Clamp01(p.Elapsed / p.TravelSeconds);
            Vector2 linear = Vector2.Lerp(p.StartPos, p.EndPos, t);
            float arc = 4f * p.ArcMaxHeight * t * (1f - t);
            p.Rect.anchoredPosition = linear + Vector2.up * arc;

            if (t >= 1f)
            {
                CreateMudZoneFromProjectile(state, stats, p);
                state.MudProjectiles.RemoveAt(i);
            }
        }
    }

    private void CreateMudZoneFromProjectile(WeaponRuntimeState state, Weapon02Stats baseStats, MudProjectileState projectile)
    {
        Weapon02Stats s = projectile.Stats;
        if (s == null || projectile.Rect == null)
        {
            return;
        }

        projectile.Rect.anchoredPosition = projectile.EndPos;
        if (itemWeaponCanvasRoot != null && projectile.Rect.parent != itemWeaponCanvasRoot)
        {
            projectile.Rect.SetParent(itemWeaponCanvasRoot, true);
        }

        SetMudVisualAsField(s);
        float rangeScale = Mathf.Max(0.01f, baseStats.AttackRangeScale);
        projectile.Rect.localScale *= rangeScale;

        TryPlayWeapon02GroundImpactSe();

        RectTransform hitRect = s.W02HitRect != null ? s.W02HitRect : s.W02ImageFieldRect;
        float durationSeconds = Mathf.Max(0.01f, baseStats.DurationSeconds);
        bool bossRangeExpand = Game03RunSessionState.IsBossWeaponEnhanced(2);
        state.MudZones.Add(new MudZoneState
        {
            Rect = projectile.Rect,
            HitRect = hitRect != null ? hitRect : projectile.Rect,
            RemainingSeconds = durationSeconds,
            TickTimer = 0f,
            DamageSourceStats = baseStats,
            BossRangeExpand = bossRangeExpand,
            LandingBaseScale = projectile.Rect.localScale,
            TotalDurationSeconds = durationSeconds
        });
    }

    private void UpdateMudZones(WeaponRuntimeState state, float deltaTime, Vector2 worldDelta)
    {
        for (int i = state.MudZones.Count - 1; i >= 0; i--)
        {
            MudZoneState z = state.MudZones[i];
            if (z == null || z.Rect == null)
            {
                state.MudZones.RemoveAt(i);
                continue;
            }

            Vector2 localScrollDelta = Vector2.zero;
            RectTransform zoneParent = z.Rect.parent as RectTransform;
            if (worldDelta.sqrMagnitude > 0.0000001f && zoneParent != null)
            {
                game03UnitManager.TryConvertWorldDeltaToLocalOnRect(zoneParent, worldDelta, out localScrollDelta);
            }

            z.Rect.anchoredPosition += localScrollDelta;

            if (z.BossRangeExpand && z.TotalDurationSeconds > 0.0001f)
            {
                float t = 1f - Mathf.Clamp01(z.RemainingSeconds / z.TotalDurationSeconds);
                z.Rect.localScale = z.LandingBaseScale * Mathf.Lerp(1f, BossWeapon02RangeExpandMultiplier, t);
            }

            z.RemainingSeconds -= deltaTime;
            z.TickTimer += deltaTime;

            while (z.TickTimer >= 0.5f)
            {
                z.TickTimer -= 0.5f;
                ApplyMudZoneDamage(z);
            }

            if (z.RemainingSeconds <= 0f)
            {
                Destroy(z.Rect.gameObject);
                state.MudZones.RemoveAt(i);
            }
        }
    }

    private void ApplyMudZoneDamage(MudZoneState zone)
    {
        RectTransform hitRect = zone.HitRect != null ? zone.HitRect : zone.Rect;
        if (hitRect == null)
        {
            return;
        }

        Rect r = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
        Vector2 center = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;
        int dmg = zone.DamageSourceStats != null
            ? Mathf.Max(1, zone.DamageSourceStats.RollAttackDamage())
            : 1;
        game03EnemyManager.ApplyWeaponHitCircle(center, radius, true, null, dmg, out _, 2, 0f);
    }

    private static void SetMudVisualAsProjectile(Weapon02Stats s)
    {
        if (s.W02Image != null)
        {
            s.W02Image.enabled = true;
        }

        if (s.W02ImageField != null)
        {
            s.W02ImageField.enabled = false;
        }

        if (s.W02HitRect != null)
        {
            s.W02HitRect.gameObject.SetActive(false);
        }
    }

    private static void SetMudVisualAsField(Weapon02Stats s)
    {
        if (s.W02Image != null)
        {
            s.W02Image.enabled = false;
        }

        if (s.W02ImageField != null)
        {
            s.W02ImageField.enabled = true;
        }

        if (s.W02HitRect != null)
        {
            s.W02HitRect.gameObject.SetActive(true);
        }
    }

    private static List<float> GenerateMudAngles(int count, int skipCount)
    {
        List<float> result = new List<float>(count);
        List<float> selected = new List<float>(count);
        int baseOffset = 0;

        for (int i = 0; i < count; i++)
        {
            float chosen;
            if (!TryPickAngle(selected, skipCount, baseOffset, out chosen))
            {
                bool found = false;
                for (int shift = 1; shift < 360; shift++)
                {
                    int offset = (baseOffset + shift) % 360;
                    if (TryPickAngle(selected, skipCount, offset, out chosen))
                    {
                        baseOffset = offset;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    chosen = UnityEngine.Random.Range(0f, 360f);
                }
            }

            selected.Add(chosen);
            result.Add(chosen);
        }

        return result;
    }

    private static bool TryPickAngle(List<float> selected, int skipCount, int baseOffset, out float chosen)
    {
        List<float> candidates = new List<float>(72);
        for (int step = 0; step < 72; step++)
        {
            float a = (baseOffset + (step * 5f)) % 360f;
            bool blocked = false;
            for (int i = 0; i < selected.Count; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(a, selected[i]));
                if (d <= skipCount * 5f)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                candidates.Add(a);
            }
        }

        if (candidates.Count == 0)
        {
            chosen = 0f;
            return false;
        }

        chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private static void ClearMudProjectiles(WeaponRuntimeState state)
    {
        for (int i = state.MudProjectiles.Count - 1; i >= 0; i--)
        {
            MudProjectileState p = state.MudProjectiles[i];
            if (p != null && p.Rect != null)
            {
                Destroy(p.Rect.gameObject);
            }
        }

        state.MudProjectiles.Clear();
    }

    private static void ClearMudZones(WeaponRuntimeState state)
    {
        for (int i = state.MudZones.Count - 1; i >= 0; i--)
        {
            MudZoneState z = state.MudZones[i];
            if (z != null && z.Rect != null)
            {
                Destroy(z.Rect.gameObject);
            }
        }

        state.MudZones.Clear();
    }

    private void UpdateOrbitWeapon(WeaponRuntimeState state, Weapon03Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform parent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;
        if (parent == null)
        {
            return;
        }

        // 黒球が回転中はクールダウンを進めない（終了後フルCD）。
        if (state.OrbitProjectiles.Count == 0)
        {
            state.AttackTimer += deltaTime;
            if (state.AttackTimer >= Mathf.Max(0.01f, stats.CooldownSeconds))
            {
                state.AttackTimer = 0f;
                SpawnOrbitProjectiles(state, stats, parent);
            }
        }

        if (state.Binding == null
            || state.Binding.UnitRect == null
            || !game03UnitManager.TryGetUnitCenterOnRect(state.Binding.UnitRect, parent, out Vector2 center))
        {
            return;
        }

        float turnSeconds = Mathf.Max(0.01f, stats.AttackSpeedSecondsPerTurn);
        float deltaAngle = 360f * deltaTime / turnSeconds;
        float signedDelta = stats.ReverseRotation ? deltaAngle : -deltaAngle;

        for (int i = state.OrbitProjectiles.Count - 1; i >= 0; i--)
        {
            OrbitProjectileState p = state.OrbitProjectiles[i];
            if (p == null || p.Rect == null)
            {
                state.OrbitProjectiles.RemoveAt(i);
                continue;
            }

            p.ElapsedSeconds += deltaTime;
            if (!p.BossPersistent)
            {
                p.RemainingSeconds -= deltaTime;
            }
            float t = Mathf.Clamp01(p.ElapsedSeconds / Mathf.Max(0.01f, p.ReachSeconds));
            p.CurrentRadius = Mathf.Lerp(p.SpawnRadius, p.TargetRadius, t);
            p.CurrentAngleDeg += signedDelta;
            p.RotationProgressAbs += Mathf.Abs(signedDelta);
            while (p.RotationProgressAbs >= 360f)
            {
                p.RotationProgressAbs -= 360f;
                p.HitEnemyIdsInCurrentTurn.Clear();
            }

            Vector2 pos = center + AngleToDirection(p.CurrentAngleDeg) * p.CurrentRadius;
            p.Rect.anchoredPosition = pos;

            RectTransform hitRect = p.HitRect != null ? p.HitRect : p.Rect;
            Rect r = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            int dmg = Mathf.Max(1, stats.RollAttackDamage());
            game03EnemyManager.ApplyWeaponHitCircle(r.center, radius, false, p.HitEnemyIdsInCurrentTurn, dmg, out _, 3, 0f);

            p.OrbitKnockPulseAccumulator += deltaTime;
            float pulseInterval = stats.OrbitKnockbackPulseIntervalSeconds;
            float perPulse = ComposeWeaponKnockbackPx(stats.KnockbackSmall) / stats.OrbitKnockbackSubdivisions;
            while (p.OrbitKnockPulseAccumulator >= pulseInterval)
            {
                p.OrbitKnockPulseAccumulator -= pulseInterval;
                if (perPulse > 0.0001f)
                {
                    game03EnemyManager.ApplyWeaponKnockbackCircle(r.center, radius, perPulse);
                }
            }

            if (!p.BossPersistent && p.RemainingSeconds <= 0f)
            {
                Destroy(p.Rect.gameObject);
                state.OrbitProjectiles.RemoveAt(i);
            }
        }
    }

    private void SpawnOrbitProjectiles(WeaponRuntimeState state, Weapon03Stats stats, RectTransform parent)
    {
        RectTransform template = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (template == null)
        {
            return;
        }

        TryPlayWeaponAttackStartSe(3);

        int count = Mathf.Max(1, stats.AttackCount);
        float step = 360f / count;
        bool bossPersistent = Game03RunSessionState.IsBossWeaponEnhanced(3);
        for (int i = 0; i < count; i++)
        {
            float baseAngle = step * i;
            if (TryCreateOrbitProjectile(stats, parent, template, baseAngle, bossPersistent, out OrbitProjectileState orbit))
            {
                state.OrbitProjectiles.Add(orbit);
            }
        }
    }

    private bool TryCreateOrbitProjectile(
        Weapon03Stats stats,
        RectTransform parent,
        RectTransform template,
        float baseAngleDeg,
        bool bossPersistent,
        out OrbitProjectileState orbit)
    {
        orbit = null;
        if (template == null || stats == null)
        {
            return false;
        }

        RectTransform instance = Instantiate(template, parent);
        instance.gameObject.SetActive(true);
        instance.localRotation = Quaternion.identity;
        instance.localScale = template.localScale * Mathf.Max(0.01f, stats.AttackRangeScale);

        Weapon03Stats instStats = instance.GetComponent<Weapon03Stats>();
        if (instStats != null && instStats.W03Image != null)
        {
            instStats.W03Image.enabled = true;
        }

        RectTransform hitRect = instStats != null && instStats.W03HitRect != null
            ? instStats.W03HitRect
            : (instStats != null ? instStats.W03ImageRect : null);

        orbit = new OrbitProjectileState
        {
            Rect = instance,
            HitRect = hitRect,
            BaseAngleDeg = baseAngleDeg,
            CurrentAngleDeg = baseAngleDeg,
            CurrentRadius = stats.SpawnDistancePixels,
            SpawnRadius = stats.SpawnDistancePixels,
            TargetRadius = Mathf.Max(0f, stats.ThrowDistancePixels),
            ReachSeconds = Mathf.Max(0.01f, stats.ReachTimeSeconds),
            ElapsedSeconds = 0f,
            RemainingSeconds = Mathf.Max(0.01f, stats.DurationSeconds),
            RotationProgressAbs = 0f,
            OrbitKnockPulseAccumulator = 0f,
            BossPersistent = bossPersistent
        };
        return true;
    }

    private void SpawnAdditionalBossOrbitProjectiles(int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureRuntimeStatesCurrent();
        for (int s = 0; s < runtimeStates.Count; s++)
        {
            WeaponRuntimeState state = runtimeStates[s];
            if (state == null || state.Mode != WeaponMode.Orbit03 || state.Binding == null)
            {
                continue;
            }

            Weapon03Stats stats = GetWeapon03Stats(state.Binding);
            RectTransform parent = GetSelectedWeaponRect(state.Binding)?.parent as RectTransform;
            RectTransform template = GetSelectedWeaponRect(state.Binding);
            if (stats == null || parent == null || template == null)
            {
                continue;
            }

            int attackCount = Mathf.Max(1, stats.AttackCount);
            float step = 360f / attackCount;
            for (int i = 0; i < count; i++)
            {
                int slotIndex = state.OrbitProjectiles.Count + i;
                float baseAngle = step * slotIndex;
                if (TryCreateOrbitProjectile(stats, parent, template, baseAngle, true, out OrbitProjectileState orbit))
                {
                    state.OrbitProjectiles.Add(orbit);
                }
            }
        }
    }

    private void PromoteExistingOrbitProjectilesToBossPersistent()
    {
        EnsureRuntimeStatesCurrent();
        for (int s = 0; s < runtimeStates.Count; s++)
        {
            WeaponRuntimeState state = runtimeStates[s];
            if (state == null || state.Mode != WeaponMode.Orbit03)
            {
                continue;
            }

            for (int i = 0; i < state.OrbitProjectiles.Count; i++)
            {
                OrbitProjectileState p = state.OrbitProjectiles[i];
                if (p != null)
                {
                    p.BossPersistent = true;
                }
            }
        }
    }

    private static Vector2 AngleToDirection(float degFromUpClockwise)
    {
        float rad = degFromUpClockwise * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private void UpdateAxeWeapon(WeaponRuntimeState state, Weapon07Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform parent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;
        if (parent == null)
        {
            return;
        }

        if (state.AxeProjectiles.Count == 0)
        {
            state.AttackTimer += deltaTime;
            if (state.AttackTimer >= Mathf.Max(0.01f, stats.CooldownSeconds))
            {
                state.AttackTimer = 0f;
                SpawnAxeProjectiles(state, stats, parent);
            }

            return;
        }

        Vector2 localScrollDelta = GetWorldScrollDeltaOnRect(parent);

        for (int i = state.AxeProjectiles.Count - 1; i >= 0; i--)
        {
            AxeProjectileState axe = state.AxeProjectiles[i];
            if (axe == null || axe.Rect == null)
            {
                state.AxeProjectiles.RemoveAt(i);
                continue;
            }

            axe.AliveSeconds += deltaTime;
            axe.Velocity += Vector2.down * Mathf.Max(0.01f, stats.GravityPixelsPerSecond2) * deltaTime;
            Vector2 pos = axe.Rect.anchoredPosition;
            pos += localScrollDelta;
            pos += axe.Velocity * deltaTime;
            axe.Rect.anchoredPosition = pos;

            float visualAngle = Mathf.Atan2(axe.Velocity.y, axe.Velocity.x) * Mathf.Rad2Deg - 90f;
            axe.Rect.localRotation = Quaternion.Euler(0f, 0f, visualAngle);

            RectTransform hitRect = axe.HitRect != null ? axe.HitRect : axe.Rect;
            Rect hitRectOnEnemyRoot = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
            int dmg = Mathf.Max(1, stats.RollAttackDamage());
            if (axe.DamagedEnemyIds == null)
            {
                axe.DamagedEnemyIds = new HashSet<int>();
            }

            game03EnemyManager.ApplyWeaponHitRect(hitRectOnEnemyRoot, false, axe.DamagedEnemyIds, dmg, ComposeWeaponKnockbackPx(stats.KnockbackLarge), out _, 7);

            bool destroy = axe.DamagedEnemyIds.Count >= stats.AxeDistinctEnemyPierceBudget;
            destroy |= stats.LifeSeconds > 0f && axe.AliveSeconds >= stats.LifeSeconds;
            destroy |= IsBelowScreenArea(axe.Rect, 150f);
            if (destroy)
            {
                Destroy(axe.Rect.gameObject);
                state.AxeProjectiles.RemoveAt(i);
            }
        }
    }

    private void SpawnAxeProjectiles(WeaponRuntimeState state, Weapon07Stats stats, RectTransform parent)
    {
        RectTransform template = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        RectTransform unitRect = state.Binding?.UnitRect;
        if (template == null || unitRect == null)
        {
            return;
        }

        if (!game03UnitManager.TryGetUnitCenterOnRect(unitRect, parent, out Vector2 center))
        {
            return;
        }

        int count = Mathf.Max(1, stats.AttackCount);
        for (int i = 0; i < count; i++)
        {
            float offset = GetBiasedAxeOffset(stats.HorizontalBiasPower);
            float minDeg = Mathf.Min(stats.LaunchAngleMinDeg, stats.LaunchAngleMaxDeg);
            float maxDeg = Mathf.Max(stats.LaunchAngleMinDeg, stats.LaunchAngleMaxDeg);
            float angleFromUp = Mathf.Lerp(minDeg, maxDeg, (offset + 1f) * 0.5f);
            Vector2 dir = AngleToDirection(angleFromUp);
            Vector2 velocity = dir * Mathf.Max(0.01f, stats.InitialSpeedPixelsPerSecond);
            float upwardKick = stats.AttackPower * Mathf.Max(0f, stats.AttackPowerInitialUpwardPixelsPerSecondPerPower);
            velocity.y += upwardKick;

            RectTransform inst = Instantiate(template, parent);
            inst.gameObject.SetActive(true);
            inst.anchoredPosition = center;
            inst.localScale = template.localScale * Mathf.Max(0.01f, stats.AttackRangeScale);
            inst.localRotation = Quaternion.identity;

            Weapon07Stats instStats = inst.GetComponent<Weapon07Stats>();
            if (instStats != null && instStats.W07Image != null)
            {
                instStats.W07Image.enabled = true;
            }

            RectTransform hitRect = instStats != null && instStats.W07HitRect != null
                ? instStats.W07HitRect
                : (instStats != null ? instStats.W07ImageRect : null);

            state.AxeProjectiles.Add(new AxeProjectileState
            {
                Rect = inst,
                HitRect = hitRect,
                Velocity = velocity,
                AliveSeconds = 0f,
                DamagedEnemyIds = new HashSet<int>()
            });
            TryPlayWeaponAttackStartSe(7);
        }
    }

    private static float GetBiasedAxeOffset(float biasPower)
    {
        float u = UnityEngine.Random.Range(-1f, 1f);
        float p = Mathf.Max(0.01f, biasPower);
        float abs = Mathf.Pow(Mathf.Abs(u), 1f / p);
        return Mathf.Sign(u) * abs;
    }

    private void UpdateMagicWeapon(WeaponRuntimeState state, Weapon06Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform parent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;
        RectTransform unitRect = state.Binding?.UnitRect;
        if (parent == null || unitRect == null)
        {
            return;
        }

        // ドローン存続中はクールダウンを進めない。
        if (state.MagicProjectiles.Count == 0)
        {
            state.AttackTimer += deltaTime;
            if (state.AttackTimer >= Mathf.Max(0.01f, stats.CooldownSeconds))
            {
                state.AttackTimer = 0f;
                SpawnMagicProjectiles(state, stats, parent, unitRect);
            }
        }

        Vector2 localScrollDelta = Vector2.zero;
        Vector2 worldDelta = game03UnitManager != null ? game03UnitManager.LastAppliedFieldDeltaWorld : Vector2.zero;
        if (worldDelta.sqrMagnitude > 0.0000001f)
        {
            game03UnitManager.TryConvertWorldDeltaToLocalOnRect(parent, worldDelta, out localScrollDelta);
        }

        for (int i = state.MagicProjectiles.Count - 1; i >= 0; i--)
        {
            MagicProjectileState p = state.MagicProjectiles[i];
            if (p == null || p.Rect == null)
            {
                state.MagicProjectiles.RemoveAt(i);
                continue;
            }

            p.AliveSeconds += deltaTime;
            if (TryGetNearestEnemyCenterOnParent(parent, p.Rect.anchoredPosition, out Vector2 targetCenter))
            {
                p.LastKnownTargetCenter = targetCenter;
                p.HasTarget = true;
            }
            else if (!stats.ReacquireTarget)
            {
                p.HasTarget = false;
            }

            if (p.HasTarget)
            {
                Vector2 desiredDir = (p.LastKnownTargetCenter - p.Rect.anchoredPosition).normalized;
                Vector2 currentDir = p.Velocity.sqrMagnitude > 0.00001f ? p.Velocity.normalized : desiredDir;
                float maxTurnRad = Mathf.Deg2Rad * Mathf.Max(0f, stats.TurnRateDegPerSecond) * deltaTime;
                Vector3 nextDir3 = Vector3.RotateTowards(
                    new Vector3(currentDir.x, currentDir.y, 0f),
                    new Vector3(desiredDir.x, desiredDir.y, 0f),
                    maxTurnRad,
                    0f);
                Vector2 nextDir = new Vector2(nextDir3.x, nextDir3.y).normalized;
                p.Velocity = nextDir * Mathf.Max(0.01f, stats.SpeedPixelsPerSecond);
            }

            Vector2 pos = p.Rect.anchoredPosition + localScrollDelta + (p.Velocity * deltaTime);
            p.Rect.anchoredPosition = pos;
            float visualAngle = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg - 90f;
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, visualAngle);

            RectTransform hitRect = p.HitRect != null ? p.HitRect : p.Rect;
            Rect hitRectOnEnemyRoot = game03EnemyManager.ConvertRectToEnemyRootSpace(hitRect);
            int dmg = Mathf.Max(1, stats.RollAttackDamage());
            if (p.DamagedEnemyIds == null)
            {
                p.DamagedEnemyIds = new HashSet<int>();
            }

            game03EnemyManager.ApplyWeaponHitRect(hitRectOnEnemyRoot, false, p.DamagedEnemyIds, dmg, ComposeWeaponKnockbackPx(stats.KnockbackSmall), out _, 6);
            bool destroy = p.DamagedEnemyIds.Count >= stats.MagicEnemyPierceBudget;
            destroy |= stats.LifeSeconds > 0f && p.AliveSeconds >= stats.LifeSeconds;
            destroy |= IsOutOfScreenArea(p.Rect);
            if (destroy)
            {
                Destroy(p.Rect.gameObject);
                state.MagicProjectiles.RemoveAt(i);
            }
        }
    }

    private void SpawnMagicProjectiles(WeaponRuntimeState state, Weapon06Stats stats, RectTransform parent, RectTransform unitRect)
    {
        if (!game03UnitManager.TryGetUnitCenterOnRect(unitRect, parent, out Vector2 center))
        {
            return;
        }

        RectTransform template = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (template == null)
        {
            return;
        }

        int count = Mathf.Max(1, stats.AttackCount);
        for (int i = 0; i < count; i++)
        {
            if (!TryGetNearestEnemyCenterOnParent(parent, center, out Vector2 targetCenter))
            {
                break;
            }

            RectTransform inst = Instantiate(template, parent);
            inst.gameObject.SetActive(true);
            inst.anchoredPosition = center;
            inst.localScale = template.localScale * Mathf.Max(0.01f, stats.AttackRangeScale);
            Vector2 dir = (targetCenter - center).normalized;
            inst.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

            Weapon06Stats instStats = inst.GetComponent<Weapon06Stats>();
            if (instStats != null && instStats.W06Image != null)
            {
                instStats.W06Image.enabled = true;
            }

            RectTransform hitRect = instStats != null && instStats.W06HitRect != null
                ? instStats.W06HitRect
                : (instStats != null ? instStats.W06ImageRect : null);

            state.MagicProjectiles.Add(new MagicProjectileState
            {
                Rect = inst,
                HitRect = hitRect,
                Velocity = dir * Mathf.Max(0.01f, stats.SpeedPixelsPerSecond),
                LastKnownTargetCenter = targetCenter,
                HasTarget = true,
                AliveSeconds = 0f,
                DamagedEnemyIds = new HashSet<int>()
            });
            TryPlayWeaponAttackStartSe(6);
        }
    }

    private bool TryGetNearestEnemyCenterOnParent(RectTransform parent, Vector2 originOnParent, out Vector2 centerOnParent)
    {
        centerOnParent = Vector2.zero;
        if (parent == null)
        {
            return false;
        }

        RectTransform enemyRoot = GetEnemyRootRect();
        if (enemyRoot == null)
        {
            return false;
        }

        Vector2 originOnEnemyRoot = ConvertLocalPointBetweenRects(originOnParent, parent, enemyRoot);
        if (!game03EnemyManager.TryGetNearestEnemyCenter(originOnEnemyRoot, out Vector2 centerOnEnemyRoot))
        {
            return false;
        }

        centerOnParent = ConvertLocalPointBetweenRects(centerOnEnemyRoot, enemyRoot, parent);
        return true;
    }

    private static Vector2 ConvertLocalPointBetweenRects(Vector2 point, RectTransform fromRect, RectTransform toRect)
    {
        if (fromRect == null || toRect == null)
        {
            return Vector2.zero;
        }

        Vector3 world = fromRect.TransformPoint(point);
        return toRect.InverseTransformPoint(world);
    }

    private RectTransform GetEnemyRootRect()
    {
        if (game03EnemyManager == null)
        {
            return null;
        }

        return game03EnemyManager.EnemyRoot;
    }

    private static void ClearOrbitProjectiles(WeaponRuntimeState state)
    {
        for (int i = state.OrbitProjectiles.Count - 1; i >= 0; i--)
        {
            OrbitProjectileState p = state.OrbitProjectiles[i];
            if (p != null && p.Rect != null)
            {
                Destroy(p.Rect.gameObject);
            }
        }

        state.OrbitProjectiles.Clear();
    }

    private static void ClearAxeProjectiles(WeaponRuntimeState state)
    {
        for (int i = state.AxeProjectiles.Count - 1; i >= 0; i--)
        {
            AxeProjectileState p = state.AxeProjectiles[i];
            if (p != null && p.Rect != null)
            {
                Destroy(p.Rect.gameObject);
            }
        }

        state.AxeProjectiles.Clear();
    }

    private static void ClearMagicProjectiles(WeaponRuntimeState state)
    {
        for (int i = state.MagicProjectiles.Count - 1; i >= 0; i--)
        {
            MagicProjectileState p = state.MagicProjectiles[i];
            if (p != null && p.Rect != null)
            {
                Destroy(p.Rect.gameObject);
            }
        }

        state.MagicProjectiles.Clear();
    }

    /// <summary>
    /// 共有 Weapon04 矩形を、別スロットがチェーン攻撃中に使っていないか。
    /// 複数ユニットが同じ sharedWeaponRects[Claw] を参照するとき、アイドル側が Hide/Deactivate するとチェーン表示が即潰れるため判定に使う。
    /// </summary>
    private bool IsWeaponRectHeldByAnyClawAttack(RectTransform weaponRect)
    {
        if (weaponRect == null)
        {
            return false;
        }

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            WeaponRuntimeState other = runtimeStates[i];
            if (other == null || other.Mode != WeaponMode.Claw04 || !other.ClawAttacking || other.Binding == null)
            {
                continue;
            }

            RectTransform otherRect = GetSelectedWeaponRect(other.Binding);
            if (otherRect == weaponRect)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Weapon04: 各チェーンで同一フレームに「ヒット判定・W04Image（ベース）・ランダムバリアント・SE」をまとめて実行し、
    /// <see cref="Weapon04Stats.ChainIntervalSeconds"/> だけ待ってから次のチェーンを <see cref="Weapon04Stats.AttackCount"/> 回まで繰り返す。
    /// 最後のチェーンの後も同じ間隔だけ待ってから描画を戻しクールダウンへ移る。
    /// </summary>
    private void UpdateClawWeapon(WeaponRuntimeState state, Weapon04Stats stats, float deltaTime)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform parent = state.Binding != null ? GetSelectedWeaponRect(state.Binding)?.parent as RectTransform : null;
        RectTransform unitRect = state.Binding?.UnitRect;
        if (parent == null || unitRect == null)
        {
            return;
        }

        RectTransform visualRect = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (state.ClawAttacking)
        {
            if (visualRect != null && !visualRect.gameObject.activeSelf)
            {
                visualRect.gameObject.SetActive(true);
            }

            SetClawUnitVisible(state, false);
            float interval = Mathf.Max(0.01f, stats.ChainIntervalSeconds);

            if (state.ClawWindDownSecondsRemaining > 0f)
            {
                SyncClawPersistentVisual(state, stats, parent, unitRect);
                state.ClawWindDownSecondsRemaining -= deltaTime;
                if (state.ClawWindDownSecondsRemaining <= 0f)
                {
                    state.ClawWindDownSecondsRemaining = 0f;
                    FinishClawAttack(state, stats, visualRect);
                }

                return;
            }

            // チェーン間待機中もプレイヤーに追従させておく（次チェーンの矩形がずれないように）。
            SyncClawPersistentVisual(state, stats, parent, unitRect);
            state.ClawChainTimer += deltaTime;
            if (state.ClawChainTimer >= interval)
            {
                state.ClawChainTimer -= interval;
                ExecuteClawHit(state, stats, parent, unitRect);
                state.ClawShotsFired++;
                if (state.ClawShotsFired >= Mathf.Max(1, stats.AttackCount))
                {
                    state.ClawWindDownSecondsRemaining = interval;
                }
            }

            return;
        }

        bool clawBusyElsewhere = IsWeaponRectHeldByAnyClawAttack(visualRect);
        if (!clawBusyElsewhere)
        {
            SetClawUnitVisible(state, true);
            HideAllW04Images(stats);
            if (visualRect != null)
            {
                visualRect.gameObject.SetActive(false);
            }
        }

        float cooldownSec = Mathf.Max(0.01f, stats.CooldownSeconds);
        state.AttackTimer += deltaTime;
        if (state.AttackTimer < cooldownSec)
        {
            return;
        }

        // 共有矩形が他スロットのチェーンで使用中なら、発火をその終わりまで保留（見た目が潰れないように）。
        if (clawBusyElsewhere)
        {
            state.AttackTimer = cooldownSec;
            return;
        }

        state.AttackTimer = 0f;
        state.ClawAttacking = true;
        state.CurrentAttackFacing = game03UnitManager != null ? game03UnitManager.LastHorizontalFacing : 1;
        state.ClawShotsFired = 0;
        state.ClawChainTimer = 0f;
        state.ClawWindDownSecondsRemaining = 0f;
        if (visualRect != null && !visualRect.gameObject.activeSelf)
        {
            visualRect.gameObject.SetActive(true);
        }

        // 1発目: クールダウン終了フレームで待機なし（表示・ランダム・ヒット・SE を同時）。
        ExecuteClawHit(state, stats, parent, unitRect);
        state.ClawShotsFired++;
        if (state.ClawShotsFired >= Mathf.Max(1, stats.AttackCount))
        {
            state.ClawWindDownSecondsRemaining = Mathf.Max(0.01f, stats.ChainIntervalSeconds);
        }
    }

    /// <summary>
    /// Weapon04 の1チェーン分: W04Image（ベース）とヒット枠を同期 → ランダムバリアント表示 → ヒット判定 → SE（同一フレーム）。
    /// </summary>
    private void ExecuteClawHit(WeaponRuntimeState state, Weapon04Stats stats, RectTransform parent, RectTransform unitRect)
    {
        if (!game03UnitManager.TryGetUnitCenterOnRect(unitRect, parent, out Vector2 center))
        {
            return;
        }

        Rect localRect = BuildClawLocalRect(stats, center, state.CurrentAttackFacing);
        SyncClawPersistentVisual(state, stats, parent, unitRect);
        UpdateClawVisualRandom(state, stats, parent, localRect);

        Rect hitRectOnEnemyRoot = ConvertLocalRectToEnemyRootRect(localRect, parent);
        state.ClawHitIdsPerShot.Clear();
        int dmg = Mathf.Max(1, stats.RollAttackDamage());
        game03EnemyManager.ApplyWeaponHitRect(hitRectOnEnemyRoot, false, state.ClawHitIdsPerShot, dmg, ComposeWeaponKnockbackPx(stats.KnockbackSmall), out _, 4);
        PlayWeapon04HitSe(stats);
    }

    private void PlayWeapon04HitSe(Weapon04Stats stats)
    {
        if (stats == null || game03SeManager == null)
        {
            return;
        }

        game03SeManager.PlayWeaponSe(4);
    }

    private void TryPlayWeaponAttackStartSe(int weaponNumber)
    {
        if (game03SeManager != null)
        {
            game03SeManager.PlayWeaponSe(weaponNumber);
        }
    }

    private void TryPlayWeapon02GroundImpactSe()
    {
        if (game03SeManager != null)
        {
            game03SeManager.PlayWeapon02GroundImpactSe();
        }
    }

    private void SyncClawPersistentVisual(WeaponRuntimeState state, Weapon04Stats stats, RectTransform parent, RectTransform unitRect)
    {
        if (!game03UnitManager.TryGetUnitCenterOnRect(unitRect, parent, out Vector2 center))
        {
            return;
        }

        Rect localRect = BuildClawLocalRect(stats, center, state.CurrentAttackFacing);
        int facing = state.CurrentAttackFacing >= 0 ? 1 : -1;
        float directionSign = facing >= 0 ? 1f : -1f;
        // All W04 visuals use left-edge center as the common origin.
        float originX = localRect.xMin;
        Vector2 origin = new Vector2(originX, localRect.center.y);

        // Keep W04Image visible for the full attack window.
        if (stats.W04Image != null)
        {
            stats.W04Image.enabled = true;
        }

        if (stats.W04ImageRect != null)
        {
            stats.W04ImageRect.pivot = new Vector2(0f, 0.5f);
            stats.W04ImageRect.anchoredPosition = origin;
            float imageTargetWidth = Mathf.Max(1f, localRect.width * 0.97f);
            float imageTargetHeight = Mathf.Max(1f, localRect.height * 0.97f);
            ApplyRectPixelSize(stats.W04ImageRect, imageTargetWidth, imageTargetHeight);
        }

        if (stats.W04HitRect != null)
        {
            stats.W04HitRect.pivot = new Vector2(0f, 0.5f);
            stats.W04HitRect.anchoredPosition = origin;
            ApplyRectPixelSize(stats.W04HitRect, Mathf.Max(1f, localRect.width), Mathf.Max(1f, localRect.height));
        }

        if (stats.W04HitViewRect != null)
        {
            stats.W04HitViewRect.pivot = new Vector2(0f, 0.5f);
            float viewForwardOffset = Mathf.Abs(localRect.width) * 0.25f * directionSign;
            stats.W04HitViewRect.anchoredPosition = new Vector2(origin.x + viewForwardOffset, origin.y);
            float viewTargetWidth = Mathf.Max(1f, localRect.width * 0.5f);
            float viewTargetHeight = Mathf.Max(1f, localRect.height * 0.7f);
            ApplyRectPixelSize(stats.W04HitViewRect, viewTargetWidth, viewTargetHeight);
        }
    }

    private Rect BuildClawLocalRect(Weapon04Stats stats, Vector2 center, int facing)
    {
        float width = Mathf.Max(1f, stats.RangeWidth);
        float halfW = width * 0.5f;
        float halfH = Mathf.Max(0.5f, stats.RangeHeight * 0.5f);
        float directionSign = facing >= 0 ? 1f : -1f;
        float originX = center.x + Mathf.Abs(stats.RangeOffsetX) * directionSign;
        float hitCenterX = originX + (halfW * directionSign);
        Vector2 hitCenter = new Vector2(hitCenterX, center.y);
        return Rect.MinMaxRect(hitCenter.x - halfW, hitCenter.y - halfH, hitCenter.x + halfW, hitCenter.y + halfH);
    }

    private void UpdateClawVisualRandom(WeaponRuntimeState state, Weapon04Stats stats, RectTransform parent, Rect rangeRect)
    {
        RectTransform visualRoot = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (visualRoot == null)
        {
            return;
        }

        RectTransform imageRect = GetRandomW04ImageRect(stats, state.LastClawImageId);
        if (imageRect == null)
        {
            HideAllW04Images(stats);
            return;
        }

        float halfW = Mathf.Abs(imageRect.rect.width * imageRect.localScale.x) * 0.5f;
        float halfH = Mathf.Abs(imageRect.rect.height * imageRect.localScale.y) * 0.5f;
        float xMin = rangeRect.xMin + halfW;
        float xMax = rangeRect.xMax - halfW;
        float yMin = rangeRect.yMin + halfH;
        float yMax = rangeRect.yMax - halfH;
        float minDistance = Mathf.Max(8f, Mathf.Min(rangeRect.width, rangeRect.height) * 0.15f);
        float minDistanceSqr = minDistance * minDistance;
        Vector2 randomPos = state.LastClawVisualPos;
        const int maxAttempts = 8;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            randomPos = new Vector2(
                xMin <= xMax ? UnityEngine.Random.Range(xMin, xMax) : rangeRect.center.x,
                yMin <= yMax ? UnityEngine.Random.Range(yMin, yMax) : rangeRect.center.y);
            if (!float.IsNaN(state.LastClawVisualPos.x) && (randomPos - state.LastClawVisualPos).sqrMagnitude < minDistanceSqr)
            {
                continue;
            }

            break;
        }
        // Keep Weapon04 root fixed; move only the selected variant.
        Vector2 randomPosOnRoot = randomPos - visualRoot.anchoredPosition;
        imageRect.anchoredPosition = randomPosOnRoot;
        state.LastClawVisualPos = randomPos;
        state.LastClawImageId = imageRect.GetInstanceID();

        SetOnlySelectedW04ImageVisible(stats, imageRect);
        Vector3 s = imageRect.localScale;
        s.y = Mathf.Abs(s.y) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        imageRect.localScale = s;
    }

    private static RectTransform GetRandomW04ImageRect(Weapon04Stats stats, int avoidInstanceId)
    {
        RectTransform[] variants = stats.W04ImageVariants;
        if (variants == null || variants.Length == 0)
        {
            return null;
        }

        List<RectTransform> valid = new List<RectTransform>(variants.Length);
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] != null)
            {
                valid.Add(variants[i]);
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        if (valid.Count == 1)
        {
            return valid[0];
        }

        RectTransform selected = valid[UnityEngine.Random.Range(0, valid.Count)];
        if (avoidInstanceId != 0 && selected.GetInstanceID() == avoidInstanceId)
        {
            selected = valid[(valid.IndexOf(selected) + 1) % valid.Count];
        }

        return selected;
    }

    private static void SetOnlySelectedW04ImageVisible(Weapon04Stats stats, RectTransform selected)
    {
        RectTransform[] variants = stats.W04ImageVariants;
        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                RectTransform r = variants[i];
                if (r == null)
                {
                    continue;
                }

                Image img = r.GetComponent<Image>();
                if (img != null)
                {
                    img.enabled = (r == selected);
                }
            }
        }
    }

    private static void HideAllW04Images(Weapon04Stats stats)
    {
        if (stats == null)
        {
            return;
        }

        RectTransform[] variants = stats.W04ImageVariants;
        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                RectTransform r = variants[i];
                if (r == null)
                {
                    continue;
                }

                Image img = r.GetComponent<Image>();
                if (img != null)
                {
                    img.enabled = false;
                }
            }
        }

        if (stats.W04Image != null)
        {
            stats.W04Image.enabled = false;
        }
    }

    private void FinishClawAttack(WeaponRuntimeState state, Weapon04Stats stats, RectTransform visualRect)
    {
        state.ClawWindDownSecondsRemaining = 0f;
        state.ClawAttacking = false;
        SetClawUnitVisible(state, true);
        HideAllW04Images(stats);
        if (visualRect != null)
        {
            visualRect.gameObject.SetActive(false);
        }
    }

    private static void ApplyRectPixelSize(RectTransform rect, float width, float height)
    {
        if (rect == null)
        {
            return;
        }

        float w = Mathf.Max(1f, width);
        float h = Mathf.Max(1f, height);
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(w, h);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }

    private void SetClawUnitVisible(WeaponRuntimeState state, bool visible)
    {
        RectTransform unitRect = state.Binding != null ? state.Binding.EquippedUnitRect : null;
        if (unitRect == null)
        {
            return;
        }

        if (state.ClawUnitHidden == !visible)
        {
            return;
        }

        CanvasGroup cg = unitRect.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = unitRect.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = visible ? 1f : 0f;
        state.ClawUnitHidden = !visible;
    }

    private Rect ConvertLocalRectToEnemyRootRect(Rect localRect, RectTransform sourceRoot)
    {
        RectTransform enemyRoot = GetEnemyRootRect();
        if (sourceRoot == null || enemyRoot == null)
        {
            return default;
        }

        Vector3 w0 = sourceRoot.TransformPoint(new Vector3(localRect.xMin, localRect.yMin, 0f));
        Vector3 w1 = sourceRoot.TransformPoint(new Vector3(localRect.xMax, localRect.yMax, 0f));
        Vector2 e0 = enemyRoot.InverseTransformPoint(w0);
        Vector2 e1 = enemyRoot.InverseTransformPoint(w1);
        return Rect.MinMaxRect(Mathf.Min(e0.x, e1.x), Mathf.Min(e0.y, e1.y), Mathf.Max(e0.x, e1.x), Mathf.Max(e0.y, e1.y));
    }

    private void ClearClawVisual(WeaponRuntimeState state)
    {
        RectTransform visualRoot = state.Binding != null ? GetSelectedWeaponRect(state.Binding) : null;
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }

        Weapon04Stats stats = state.Binding != null ? GetWeapon04Stats(state.Binding) : null;
        HideAllW04Images(stats);
        SetClawUnitVisible(state, true);

        state.ClawAttacking = false;
        state.ClawShotsFired = 0;
        state.ClawChainTimer = 0f;
        state.ClawWindDownSecondsRemaining = 0f;
        state.ClawHitIdsPerShot.Clear();
        state.LastClawImageId = 0;
        state.LastClawVisualPos = new Vector2(float.NaN, float.NaN);
    }

    private bool IsOutOfScreenArea(RectTransform rect)
    {
        RectTransform root = rect != null ? rect.parent as RectTransform : null;
        if (root == null || rect == null)
        {
            return true;
        }

        Vector2 p = rect.anchoredPosition;
        Rect r = root.rect;
        const float margin = 300f;
        return p.x < r.xMin - margin || p.x > r.xMax + margin || p.y < r.yMin - margin || p.y > r.yMax + margin;
    }

    private Vector2 GetWorldScrollDeltaOnRect(RectTransform targetRect)
    {
        if (targetRect == null || game03UnitManager == null)
        {
            return Vector2.zero;
        }

        Vector2 worldDelta = game03UnitManager.LastAppliedFieldDeltaWorld;
        if (worldDelta.sqrMagnitude <= 0.0000001f)
        {
            return Vector2.zero;
        }

        if (game03UnitManager.TryConvertWorldDeltaToLocalOnRect(targetRect, worldDelta, out Vector2 localDelta))
        {
            return localDelta;
        }

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return Vector2.zero;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, Vector2.zero, null, out Vector2 p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, new Vector2(Screen.width, Screen.height), null, out Vector2 p1);
        float localHeight = Mathf.Abs(p1.y - p0.y);
        float worldHeight = cam.orthographicSize * 2f;
        if (localHeight <= 0.0001f || worldHeight <= 0.0001f)
        {
            return Vector2.zero;
        }

        float localUnitsPerWorld = localHeight / worldHeight;
        return worldDelta * localUnitsPerWorld;
    }

    private static bool IsBelowScreenArea(RectTransform rect, float margin)
    {
        RectTransform root = rect != null ? rect.parent as RectTransform : null;
        if (root == null || rect == null)
        {
            return true;
        }

        return rect.anchoredPosition.y < root.rect.yMin - margin;
    }

    private const string ExtraOutfitImageNamePrefix = "ImageExtraOutfit";
    private const string ExtWeaponImageChildName = "ExtWImage";

    /// <summary>BossUG 確定時: 武器見た目＋性能（UG カウント外）。初回のみ性能適用、見た目は毎回。</summary>
    public bool TryApplyBossWeaponEnhancement(int weaponNumber)
    {
        if (weaponNumber <= 0 || weaponNumber > 7)
        {
            return false;
        }

        bool firstTime = Game03RunSessionState.TryMarkBossWeaponEnhanced(weaponNumber);
        if (firstTime)
        {
            ApplyBossWeaponStatsInternal(weaponNumber);
            if (weaponNumber == 3)
            {
                PromoteExistingOrbitProjectilesToBossPersistent();
            }
        }

        ApplyBossWeaponVisualInternal(weaponNumber);
        return true;
    }

    /// <summary>装備時など、ラン中に既に Boss 強化済みなら見た目・性能を再適用。</summary>
    public void ReapplyBossWeaponEnhancementIfNeeded(int weaponNumber)
    {
        if (!Game03RunSessionState.IsBossWeaponEnhanced(weaponNumber))
        {
            return;
        }

        ApplyBossWeaponVisualInternal(weaponNumber);
    }

    private void ApplyBossWeaponStatsInternal(int weaponNumber)
    {
        if (!TryGetWeaponCanvasRootForWeaponNumber(weaponNumber, out RectTransform weaponRoot))
        {
            return;
        }

        switch (weaponNumber)
        {
            case 1:
            {
                Weapon01Stats stats = weaponRoot.GetComponent<Weapon01Stats>();
                if (stats != null)
                {
                    stats.ApplyAttackPowerFlat(8);
                    stats.ApplyRangeMultiplier(1.1664f);
                }

                break;
            }
            case 4:
            {
                Weapon04Stats stats = weaponRoot.GetComponent<Weapon04Stats>();
                if (stats != null)
                {
                    stats.ApplyAttackPowerFlat(9);
                    stats.ApplyAttackPowerFlat(9);
                }

                break;
            }
            case 5:
            {
                Weapon05Stats stats = weaponRoot.GetComponent<Weapon05Stats>();
                if (stats != null)
                {
                    stats.ApplyAttackCountPlusOne();
                    stats.ApplyAttackCountPlusOne();
                    stats.ApplyDistinctEnemyPierceBonus(1);
                }

                break;
            }
            case 6:
            {
                Weapon06Stats stats = weaponRoot.GetComponent<Weapon06Stats>();
                if (stats != null)
                {
                    stats.ApplyAttackPowerFlat(6);
                    stats.ApplyCooldownMultiplier(0.88f);
                }

                break;
            }
            case 7:
            {
                Weapon07Stats stats = weaponRoot.GetComponent<Weapon07Stats>();
                if (stats != null)
                {
                    stats.ApplyMaxPierceBonus(99);
                    stats.ApplyAttackRangeScaleMultiplier(1.12f);
                    stats.ApplyAttackRangeScaleMultiplier(1.12f);
                }

                break;
            }
        }
    }

    private void ApplyBossWeaponVisualInternal(int weaponNumber)
    {
        if (!TryGetWeaponCanvasRootForWeaponNumber(weaponNumber, out RectTransform weaponRoot))
        {
            return;
        }

        Image extImage = FindChildImageByName(weaponRoot, ExtWeaponImageChildName);
        if (extImage == null)
        {
            return;
        }

        Image targetImage = ResolveBossWeaponVisualTargetImage(weaponNumber, weaponRoot);
        if (targetImage == null)
        {
            return;
        }

        CopyImageSpriteAndColor(extImage, targetImage);
    }

    private static Image ResolveBossWeaponVisualTargetImage(int weaponNumber, Transform weaponRoot)
    {
        switch (weaponNumber)
        {
            case 1:
                return FindChildImageByName(weaponRoot, "W01Image")
                    ?? weaponRoot.GetComponent<Weapon01Stats>()?.Wo1Image;
            case 2:
                return weaponRoot.GetComponent<Weapon02Stats>()?.W02ImageField
                    ?? FindChildImageByName(weaponRoot, "W02ImageField");
            case 3:
                return weaponRoot.GetComponent<Weapon03Stats>()?.W03Image
                    ?? FindChildImageByName(weaponRoot, "W03Image");
            case 4:
                return weaponRoot.GetComponent<Weapon04Stats>()?.W04Image
                    ?? FindChildImageByName(weaponRoot, "W04Image");
            case 5:
                return weaponRoot.GetComponent<Weapon05Stats>()?.Wo5Image
                    ?? FindChildImageByName(weaponRoot, "Wo5Image");
            case 6:
                return weaponRoot.GetComponent<Weapon06Stats>()?.W06Image
                    ?? FindChildImageByName(weaponRoot, "W06Image");
            case 7:
                return weaponRoot.GetComponent<Weapon07Stats>()?.W07Image
                    ?? FindChildImageByName(weaponRoot, "W07Image");
            default:
                return null;
        }
    }

    private static void CopyImageSpriteAndColor(Image source, Image destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.sprite = source.sprite;
        destination.color = source.color;
    }

    /// <summary>BossUG 用。装備中武器から、除外セットに含まない候補を集める。</summary>
    public void CollectEquippedWeaponCandidatesExcluding(HashSet<int> excludedWeaponNumbers, List<UpgradeWeaponCandidate> results)
    {
        GetUpgradeableWeaponCandidates(results);
        if (excludedWeaponNumbers == null || excludedWeaponNumbers.Count == 0 || results == null)
        {
            return;
        }

        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (excludedWeaponNumbers.Contains(results[i].WeaponNumber))
            {
                results.RemoveAt(i);
            }
        }
    }

    public bool TryPickRandomExtraOutfitSprite(int weaponNumber, out Sprite outfitSprite, out int outfitIndex)
    {
        return TryPickBossExtraOutfitSprite(weaponNumber, out outfitSprite, out outfitIndex);
    }

    /// <summary>BossUG 用。メタ UG07 状態に応じ ImageExtraOutfit01 または 02 を固定選択（抽選なし）。</summary>
    public bool TryPickBossExtraOutfitSprite(int weaponNumber, out Sprite outfitSprite, out int outfitIndex)
    {
        outfitSprite = null;
        outfitIndex = 0;
        if (!TryGetWeaponCanvasRootForWeaponNumber(weaponNumber, out RectTransform weaponRoot))
        {
            return false;
        }

        int targetIndex = ResolveBossExtraOutfitIndexForMetaUnlock();
        Image picked = FindExtraOutfitImageByIndex(weaponRoot, targetIndex);
        if (picked != null && picked.sprite != null)
        {
            outfitIndex = targetIndex;
            outfitSprite = picked.sprite;
            return true;
        }

        if (targetIndex == 2)
        {
            Debug.LogWarning($"[Game03WeaponManager] Weapon{weaponNumber:D2} の ImageExtraOutfit02 が未設定です。", this);
            return false;
        }

        Image fallback = FindExtraOutfitImageByIndex(weaponRoot, 1);
        if (fallback != null && fallback.sprite != null)
        {
            outfitIndex = 1;
            outfitSprite = fallback.sprite;
            return true;
        }

        outfitIndex = 0;
        return false;
    }

    private static int ResolveBossExtraOutfitIndexForMetaUnlock()
    {
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        return meta != null && meta.BossExtraOutfit02Unlocked ? 2 : 1;
    }

    public bool ApplyOutfitToEquippedUnit(int weaponNumber, Sprite outfitSprite)
    {
        if (outfitSprite == null)
        {
            return false;
        }

        if (!TryGetEquippedUnitRectForWeapon(weaponNumber, out RectTransform unitRoot) || unitRoot == null)
        {
            return false;
        }

        return ApplyOutfitToUnitRect(unitRoot, outfitSprite);
    }

    public bool ApplyOutfitToUnitRect(RectTransform unitRoot, Sprite outfitSprite)
    {
        if (outfitSprite == null || unitRoot == null)
        {
            return false;
        }

        return ApplyOutfitSpritesToUnitRoot(unitRoot, outfitSprite);
    }

    public bool TryGetEquippedUnitRectForWeapon(int weaponNumber, out RectTransform unitRect)
    {
        unitRect = null;
        if (!TryGetBindingForEquippedWeapon(weaponNumber, out UnitWeaponBinding binding))
        {
            return false;
        }

        unitRect = binding.EquippedUnitRect;
        return unitRect != null;
    }

    private bool TryGetWeaponCanvasRootForWeaponNumber(int weaponNumber, out RectTransform weaponRoot)
    {
        weaponRoot = null;
        int index = weaponNumber - 1;
        if (sharedWeaponRects == null || index < 0 || index >= sharedWeaponRects.Count)
        {
            return false;
        }

        weaponRoot = sharedWeaponRects[index];
        return weaponRoot != null;
    }

    private bool TryGetBindingForEquippedWeapon(int weaponNumber, out UnitWeaponBinding binding)
    {
        binding = null;
        WeaponMode mode = WeaponNumberToMode(weaponNumber);
        if (mode == WeaponMode.None)
        {
            return false;
        }

        if (weaponNumber == 1)
        {
            if (mainUnitWeapon != null && mainUnitWeapon.SelectedMode == mode)
            {
                binding = mainUnitWeapon;
                return true;
            }
        }
        else
        {
            if (TryGetFirstSubUnitBindingWithMode(mode, out binding))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetFirstSubUnitBindingWithMode(WeaponMode mode, out UnitWeaponBinding binding)
    {
        binding = null;
        UnitWeaponBinding[] subBindings = { unit01Weapon, unit02Weapon, unit03Weapon, unit04Weapon };
        for (int i = 0; i < subBindings.Length; i++)
        {
            UnitWeaponBinding candidate = subBindings[i];
            if (candidate != null && candidate.SelectedMode == mode)
            {
                binding = candidate;
                return true;
            }
        }

        return false;
    }

    private static WeaponMode WeaponNumberToMode(int weaponNumber)
    {
        switch (weaponNumber)
        {
            case 1: return WeaponMode.Melee01;
            case 2: return WeaponMode.Mud02;
            case 3: return WeaponMode.Orbit03;
            case 4: return WeaponMode.Claw04;
            case 5: return WeaponMode.Ranged05;
            case 6: return WeaponMode.Magic06;
            case 7: return WeaponMode.Axe07;
            default: return WeaponMode.None;
        }
    }

    private static bool ApplyOutfitSpritesToUnitRoot(RectTransform unitRoot, Sprite outfitSprite)
    {
        bool applied = false;
        Image mainImg = FindChildImageByName(unitRoot, "MainUnitImg");
        Image frontImg = FindChildImageByName(unitRoot, "MainUnitFrontImg");
        if (mainImg != null)
        {
            mainImg.sprite = outfitSprite;
            applied = true;
        }

        if (frontImg != null)
        {
            frontImg.sprite = outfitSprite;
            applied = true;
        }

        return applied;
    }

    private static Image FindChildImageByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                Image image = child.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }
        }

        return null;
    }

    private static void CollectExtraOutfitIndices(Transform weaponRoot, List<int> destination)
    {
        if (weaponRoot == null || destination == null)
        {
            return;
        }

        int maxIndex = 0;
        Image[] images = weaponRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || !TryParseExtraOutfitIndex(image.name, out int index))
            {
                continue;
            }

            maxIndex = Mathf.Max(maxIndex, index);
        }

        if (maxIndex <= 0)
        {
            return;
        }

        for (int index = 1; index <= maxIndex; index++)
        {
            destination.Add(index);
        }
    }

    private static Image FindExtraOutfitImageByIndex(Transform weaponRoot, int outfitIndex)
    {
        if (weaponRoot == null)
        {
            return null;
        }

        string targetName = ExtraOutfitImageNamePrefix + outfitIndex.ToString("00");
        Image[] images = weaponRoot.GetComponentsInChildren<Image>(true);
        Image fallback01 = null;
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || !image.name.StartsWith(ExtraOutfitImageNamePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (image.name == ExtraOutfitImageNamePrefix + "01")
            {
                fallback01 = image;
            }

            if (image.name == targetName)
            {
                return image;
            }
        }

        return fallback01;
    }

    private static bool TryParseExtraOutfitIndex(string objectName, out int outfitIndex)
    {
        outfitIndex = 0;
        if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith(ExtraOutfitImageNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = objectName.Substring(ExtraOutfitImageNamePrefix.Length);
        return int.TryParse(suffix, out outfitIndex) && outfitIndex > 0;
    }
}
