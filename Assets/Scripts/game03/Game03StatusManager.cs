using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class Game03ExperienceMilestoneDefinition
{
    public int thresholdTotalLifetimeExp = 100;
    public int spawnCount = 1;
}

public readonly struct Game03ExperienceMilestoneReachedArgs
{
    public readonly int ThresholdTotalLifetimeExp;
    public readonly int SpawnCount;

    public Game03ExperienceMilestoneReachedArgs(int thresholdTotalLifetimeExp, int spawnCount)
    {
        ThresholdTotalLifetimeExp = thresholdTotalLifetimeExp;
        SpawnCount = Mathf.Max(1, spawnCount);
    }
}

/// <summary>
/// 視聴者数(経験値)、レベル、撃破数、経験値アイテム取得数、武器アップグレード回数、UGリオロール残り回数を一元管理する。
/// </summary>
/// <remarks>
/// Popular 周りの数値定義（表示・総獲得・LV用）:
/// <list type="bullet">
/// <item><description><b>Popular 表示用数値</b> — <see cref="PopularDisplayExperienceTotal"/>（経験値総獲得量 + アイテム等の表示専用加算。LV 計算には使わない）</description></item>
/// <item><description><b>経験値総獲得量</b> — <see cref="LifetimeTotalExperienceEarned"/>（<c>AddExperience</c> 由来の累計のみ）</description></item>
/// <item><description><b>現在経験値</b> — <see cref="CurrentExp"/>（Lv3 以降のレベルアップ消化に使用）</description></item>
/// </list>
/// </remarks>
public class Game03StatusManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField, Tooltip("任意。未設定なら Awake で検索。接触撃破デバッグON時は経験値取得してもLVのみ上げない（ドロップ取得も含む）。")]
    private Game03DebugManager game03DebugManager;

    [Header("Pickup Gates (fixed)")]
    [SerializeField, Min(1)] private int killsRequiredForLevel2 = 2;
    [SerializeField, Min(1)] private int killsRequiredForLevel3 = 5;

    [Header("Experience Drop — tier bases (緑・黄・赤・紫)")]
    [SerializeField, Min(1)] private int tierBaseGreen = 1;
    [SerializeField, Min(1)] private int tierBaseYellow = 2;
    [SerializeField, Min(1)] private int tierBaseRed = 4;
    [SerializeField, Min(1)] private int tierBasePurple = 6;

    [Header("Catch-up multipliers")]
    [SerializeField, Min(1f)] private float maxLevelCatchupMultiplier = 1.2f;
    [SerializeField, Min(1f)] private float maxUpgradeCatchupMultiplier = 1.15f;
    [SerializeField, Min(1)] private int expectedKillsPerLevelLate = 52;

    [Header("Lifetime exp milestones (field consumables)")]
    [SerializeField, Tooltip("消耗アイテム用。Pod 加入タイミングは Game03FieldItemCoordinator のゲーム内秒スケジュール。")]
    private List<Game03ExperienceMilestoneDefinition> experienceMilestones = new List<Game03ExperienceMilestoneDefinition>
    {
        new Game03ExperienceMilestoneDefinition { thresholdTotalLifetimeExp = 6000, spawnCount = 1 },
        new Game03ExperienceMilestoneDefinition { thresholdTotalLifetimeExp = 12000, spawnCount = 1 },
    };

    [SerializeField, Tooltip("経験値 milestone アイテム経由で Pod 加入が 1 度でも完了すると ON")]
    private bool hasExperienceMilestonePodJoinCompleted;

    private readonly List<bool> milestoneConsumedFlagsRuntime = new List<bool>();

    private int level = 1;
    private int currentExp;
    /// <summary>経験値総獲得量: <c>AddExperience</c> で加算した量の通算（レベルアップで <see cref="CurrentExp"/> が減ってもここは減らない）。</summary>
    private int lifetimeTotalExperienceEarned;
    private int totalKills;
    private int totalExperiencePickupCount;
    private readonly Dictionary<string, int> weaponUpgradeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private int ugRerollChargesRemaining;
    /// <summary>Popular 表示専用の加算（スマホ等）。<see cref="LifetimeTotalExperienceEarned"/> には含めず、<see cref="CurrentExp"/> にも加算しない（仕様 ■3）。</summary>
    private int popularDisplayOnlyBonus;

    public int Level => level;
    /// <summary>現在経験値（Lv3 以降のレベルアップに使用。取得時に加算され、レベルアップで減算される）。</summary>
    public int CurrentExp => currentExp;
    /// <summary>経験値総獲得量（経験値ドロップの <c>AddExperience</c> 分のみの累計。レベル消化では減らない）。</summary>
    public int LifetimeTotalExperienceEarned => lifetimeTotalExperienceEarned;

    /// <summary>Popular 表示用数値: 経験値総獲得量 + アイテム等で増えた表示専用値（画像／テキスト用。LV 計算には使わない）。</summary>
    public int PopularDisplayExperienceTotal => lifetimeTotalExperienceEarned + popularDisplayOnlyBonus;
    public int TotalKills => totalKills;
    public int TotalExperiencePickupCount => totalExperiencePickupCount;
    public int TotalWeaponUpgradeSelections => SumWeaponUpgrades();

    /// <summary>LvUpパネルでUG対象武器を抽選し直せる残り回数。初期0（アイテム等で加算）。</summary>
    public int UgRerollChargesRemaining => ugRerollChargesRemaining;

    public event Action<int> LevelChanged;
    public event Action<int, int, int> ExperienceChanged;
    public event Action<Game03ExperienceMilestoneReachedArgs> ExperienceMilestoneReached;

    public bool HasExperienceMilestonePodJoinCompleted => hasExperienceMilestonePodJoinCompleted;

    public int GetRequiredExpForNextLevel()
    {
        if (level < 3)
        {
            return 0;
        }

        return ComputeRequiredExpForLevel(level);
    }

    /// <summary>スマホアイテムで付与する量（次レベル必要量の 100% 相当。Lv3 未満はゲート幅の目安）。</summary>
    public int ComputePhoneItemViewerBonusAmount()
    {
        if (level >= 3)
        {
            return Mathf.Max(1, GetRequiredExpForNextLevel());
        }

        if (level <= 1)
        {
            return Mathf.Max(1, killsRequiredForLevel2);
        }

        return Mathf.Max(1, killsRequiredForLevel3 - killsRequiredForLevel2);
    }

    /// <summary>スマホ: 視聴者数のみ加算（経験値・レベルゲートには影響しない）。</summary>
    public void AddPhoneItemViewerReward()
    {
        TryAddPhoneItemViewerReward(out _);
    }

    /// <summary>スマホ報酬を加算できたときだけ true。<paramref name="addedAmount"/> は実際に加算した値。</summary>
    public bool TryAddPhoneItemViewerReward(out int addedAmount)
    {
        addedAmount = 0;
        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return false;
        }

        int amount = ComputePhoneItemViewerBonusAmount();
        if (amount <= 0)
        {
            return false;
        }

        popularDisplayOnlyBonus += amount;
        addedAmount = amount;
        RaiseExperienceChanged();
        return true;
    }

    public void NotifyEnemyKilled()
    {
        totalKills++;
    }

    public void RegisterWeaponUpgrade(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            return;
        }

        weaponUpgradeCounts.TryGetValue(weaponId, out int count);
        weaponUpgradeCounts[weaponId] = count + 1;
    }

    public int GetWeaponUpgradeCount(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            return 0;
        }

        return weaponUpgradeCounts.TryGetValue(weaponId, out int c) ? c : 0;
    }

    public void AddUgRerollCharges(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ugRerollChargesRemaining += amount;
    }

    public bool TryConsumeUgRerollCharge()
    {
        if (ugRerollChargesRemaining <= 0)
        {
            return false;
        }

        ugRerollChargesRemaining--;
        return true;
    }

    /// <summary>
    /// 次のレベルまでの進捗 0〜1。Lv1〜2 は経験値アイテム取得数ゲート、Lv3以降は経験値。
    /// </summary>
    public float GetNextLevelProgress01()
    {
        if (level >= 3)
        {
            int need = ComputeRequiredExpForLevel(level);
            if (need <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)currentExp / need);
        }

        if (level <= 1)
        {
            return Mathf.Clamp01((float)totalExperiencePickupCount / Mathf.Max(1, killsRequiredForLevel2));
        }

        int span = Mathf.Max(1, killsRequiredForLevel3 - killsRequiredForLevel2);
        int progressed = Mathf.Clamp(totalExperiencePickupCount - killsRequiredForLevel2, 0, span);
        return Mathf.Clamp01((float)progressed / span);
    }

    public int ComputeExperienceDropAmount(Game03ExpTier tier)
    {
        return ComputeExperienceDropAmount(GetExperienceTierBase(tier));
    }

    /// <summary>プレハブ基準値 × フェーズ経験値係数を適用済みの値に、レベル／UG 追いつき補正を掛ける。</summary>
    public int ComputeExperienceDropAmount(int prefabExperienceWithPhaseMult)
    {
        float levelCatchup = ComputeLevelCatchupMultiplier();
        float upgradeCatchup = ComputeUpgradeCatchupMultiplier();
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, prefabExperienceWithPhaseMult) * levelCatchup * upgradeCatchup));
    }

    public int GetExperienceTierBase(Game03ExpTier tier)
    {
        return GetTierBase(tier);
    }

    public void AddExperience(int amount, bool suppressLevelProgress = false)
    {
        if (amount <= 0)
        {
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        bool suppress = suppressLevelProgress || ShouldSuppressLevelProgressForDebugContact();

        lifetimeTotalExperienceEarned += amount;
        Game03CombatBalanceLogger.TryGet()?.RecordExperienceGained(amount);
        ProcessLifetimeExperienceMilestonesIfNeeded();
        currentExp += amount;

        if (suppress)
        {
            RaiseExperienceChanged();
            return;
        }

        totalExperiencePickupCount++;
        TryLevelUpFromPickupCountGates();
        while (level >= 3)
        {
            int need = ComputeRequiredExpForLevel(level);
            if (need <= 0 || currentExp < need)
            {
                break;
            }

            currentExp -= need;
            level++;
            LevelChanged?.Invoke(level);
        }

        RaiseExperienceChanged();
    }

    private bool ShouldSuppressLevelProgressForDebugContact()
    {
        return game03DebugManager != null && game03DebugManager.EffectiveKillEnemyOnPlayerContact;
    }

    private void TryLevelUpFromPickupCountGates()
    {
        if (level == 1 && totalExperiencePickupCount >= killsRequiredForLevel2)
        {
            level = 2;
            LevelChanged?.Invoke(level);
            RaiseExperienceChanged();
        }

        if (level == 2 && totalExperiencePickupCount >= killsRequiredForLevel3)
        {
            level = 3;
            LevelChanged?.Invoke(level);
            RaiseExperienceChanged();
        }
    }

    private static int ComputeRequiredExpForLevel(int currentLevel)
    {
        return 12 + currentLevel * 4 + (currentLevel / 5) * 8;
    }

    private int GetTierBase(Game03ExpTier tier)
    {
        return tier switch
        {
            Game03ExpTier.Green => tierBaseGreen,
            Game03ExpTier.Yellow => tierBaseYellow,
            Game03ExpTier.Red => tierBaseRed,
            Game03ExpTier.Purple => tierBasePurple,
            _ => tierBaseRed
        };
    }

    private float ComputeLevelCatchupMultiplier()
    {
        if (expectedKillsPerLevelLate <= 0)
        {
            return 1f;
        }

        float expectedLevel = 3f + (Mathf.Max(0, totalKills - killsRequiredForLevel3) / (float)expectedKillsPerLevelLate);
        if (expectedLevel <= level)
        {
            return 1f;
        }

        float t = Mathf.Clamp01((expectedLevel - level) / 6f);
        return Mathf.Min(maxLevelCatchupMultiplier, 1f + (maxLevelCatchupMultiplier - 1f) * t);
    }

    private float ComputeUpgradeCatchupMultiplier()
    {
        int upgrades = SumWeaponUpgrades();
        float expected = Mathf.Max(0f, (level - 3) * 0.75f);
        if (level < 3 || upgrades >= expected)
        {
            return 1f;
        }

        float t = Mathf.Clamp01((expected - upgrades) / 10f);
        return Mathf.Min(maxUpgradeCatchupMultiplier, 1f + (maxUpgradeCatchupMultiplier - 1f) * t);
    }

    private int SumWeaponUpgrades()
    {
        int sum = 0;
        foreach (KeyValuePair<string, int> pair in weaponUpgradeCounts)
        {
            sum += pair.Value;
        }

        return sum;
    }

    private void RaiseExperienceChanged()
    {
        int need = GetRequiredExpForNextLevel();
        ExperienceChanged?.Invoke(currentExp, need, level);
    }

    public void NotifyExperienceMilestonePodJoinCompleted()
    {
        hasExperienceMilestonePodJoinCompleted = true;
    }

    private void Awake()
    {
        EnsureMilestoneConsumedFlagSize();
        if (game03DebugManager == null)
        {
            game03DebugManager = FindAnyObjectByType<Game03DebugManager>(FindObjectsInactive.Include);
        }
    }

    private void Start()
    {
        RaiseExperienceChanged();
    }

    private void EnsureMilestoneConsumedFlagSize()
    {
        if (experienceMilestones == null)
        {
            experienceMilestones = new List<Game03ExperienceMilestoneDefinition>();
        }

        while (milestoneConsumedFlagsRuntime.Count < experienceMilestones.Count)
        {
            milestoneConsumedFlagsRuntime.Add(false);
        }

        while (milestoneConsumedFlagsRuntime.Count > experienceMilestones.Count)
        {
            milestoneConsumedFlagsRuntime.RemoveAt(milestoneConsumedFlagsRuntime.Count - 1);
        }
    }

    private void ProcessLifetimeExperienceMilestonesIfNeeded()
    {
        EnsureMilestoneConsumedFlagSize();
        if (experienceMilestones == null || experienceMilestones.Count == 0)
        {
            return;
        }

        for (int i = 0; i < experienceMilestones.Count; i++)
        {
            if (milestoneConsumedFlagsRuntime[i])
            {
                continue;
            }

            if (lifetimeTotalExperienceEarned >= experienceMilestones[i].thresholdTotalLifetimeExp)
            {
                milestoneConsumedFlagsRuntime[i] = true;
                Game03ExperienceMilestoneReachedArgs payload = new Game03ExperienceMilestoneReachedArgs(
                    experienceMilestones[i].thresholdTotalLifetimeExp,
                    experienceMilestones[i].spawnCount);
                ExperienceMilestoneReached?.Invoke(payload);
            }
        }
    }
}
