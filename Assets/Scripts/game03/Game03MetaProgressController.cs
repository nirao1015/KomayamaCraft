using UnityEngine;

/// <summary>
/// game03 ゲーム外成長データの DontDestroyOnLoad 単一インスタンス。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03MetaProgressController : MonoBehaviour
{
    public static int AttackPowerLevelMax => Game03MetaUpgradeCatalog.AttackPower.PurchasableCount;
    public static int AutoHealLevelMax => Game03MetaUpgradeCatalog.AutoHeal.PurchasableCount;
    public static int AttackCountLevelMax => Game03MetaUpgradeCatalog.AttackCount.PurchasableCount;
    public static int SwiftnessLevelMax => Game03MetaUpgradeCatalog.Swiftness.PurchasableCount;
    public static int RerollLevelMax => Game03MetaUpgradeCatalog.Reroll.PurchasableCount;
    public static int CooldownReductionLevelMax => Game03MetaUpgradeCatalog.CooldownReduction.PurchasableCount;
    public static int ExtraOutfitUnlockLevelMax => Game03MetaUpgradeCatalog.ExtraOutfitUnlock.PurchasableCount;

    public static Game03MetaProgressController Instance { get; private set; }

    private readonly Game03MetaSaveService saveService = new Game03MetaSaveService();

    private bool sessionInitialized;
    private long metaCurrency;
    private long totalSpentOnUpgrades;
    private int attackPowerLevel;
    private int autoHealLevel;
    private int attackCountLevel;
    private int swiftnessLevel;
    private int rerollLevel;
    private int cooldownReductionLevel;
    private bool bossExtraOutfit02Unlocked;
    private int acquiredCompanionWeaponMask;

    public long MetaCurrency => metaCurrency;
    public long TotalSpentOnUpgrades => totalSpentOnUpgrades;
    public int AttackPowerLevel => attackPowerLevel;
    public int AutoHealLevel => autoHealLevel;
    public int AttackCountLevel => attackCountLevel;
    public int SwiftnessLevel => swiftnessLevel;
    public int RerollLevel => rerollLevel;
    public int CooldownReductionLevel => cooldownReductionLevel;
    public bool BossExtraOutfit02Unlocked => bossExtraOutfit02Unlocked;

    public bool HasAnyUpgradeLevels =>
        attackPowerLevel > 0
        || autoHealLevel > 0
        || attackCountLevel > 0
        || swiftnessLevel > 0
        || rerollLevel > 0
        || cooldownReductionLevel > 0
        || bossExtraOutfit02Unlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void EnsureInitializedFromDisk(bool writeNewSaveIfMissing)
    {
        if (sessionInitialized)
        {
            return;
        }

        sessionInitialized = true;
        if (saveService.TryLoad(out Game03MetaSaveData loaded))
        {
            ApplyFromDto(loaded);
            TryUnlockAllCompanionWeaponsAchievementIfEligible();
            return;
        }

        ApplyFromDto(Game03MetaSaveData.CreateDefault());
        if (writeNewSaveIfMissing)
        {
            PersistCurrentToDisk();
        }
    }

    /// <summary>Pod 仲間加入などで Weapon02〜07 を累積記録し、初入手時のみセーブする。</summary>
    public bool TryRecordCompanionWeaponAcquired(int weaponNumber)
    {
        if (!Game03CompanionWeaponMetaFlags.IsCompanionWeaponNumber(weaponNumber))
        {
            return false;
        }

        if (Game03CompanionWeaponMetaFlags.HasWeapon(acquiredCompanionWeaponMask, weaponNumber))
        {
            return false;
        }

        acquiredCompanionWeaponMask = Game03CompanionWeaponMetaFlags.AddWeapon(
            acquiredCompanionWeaponMask,
            weaponNumber);
        PersistCurrentToDisk();
        return true;
    }

    public bool HasAllCompanionWeaponsAcquired() =>
        Game03CompanionWeaponMetaFlags.HasAllWeapons(acquiredCompanionWeaponMask);

    public void TryUnlockAllCompanionWeaponsAchievementIfEligible()
    {
        if (!HasAllCompanionWeaponsAcquired())
        {
            return;
        }

        SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_05);
    }

    public void AddMetaCurrencyAndPersist(long delta)
    {
        if (delta == 0)
        {
            return;
        }

        metaCurrency = ClampCurrency(metaCurrency + delta);
        PersistCurrentToDisk();
    }

    public void PersistCurrentToDisk()
    {
        Game03MetaSaveData dto = CaptureDto();
        saveService.TrySave(dto);
    }

    public bool TrySpendMetaCurrency(long cost)
    {
        if (cost <= 0)
        {
            return true;
        }

        if (metaCurrency < cost)
        {
            return false;
        }

        metaCurrency -= cost;
        return true;
    }

    public bool TryPurchaseAttackPowerUpgrade(long price) => TryPurchaseTier(ref attackPowerLevel, AttackPowerLevelMax, price);
    public bool TryPurchaseAutoHealUpgrade(long price) => TryPurchaseTier(ref autoHealLevel, AutoHealLevelMax, price);
    public bool TryPurchaseAttackCountUpgrade(long price) => TryPurchaseTier(ref attackCountLevel, AttackCountLevelMax, price);
    public bool TryPurchaseSwiftnessUpgrade(long price) => TryPurchaseTier(ref swiftnessLevel, SwiftnessLevelMax, price);
    public bool TryPurchaseRerollUpgrade(long price) => TryPurchaseTier(ref rerollLevel, RerollLevelMax, price);
    public bool TryPurchaseCooldownReductionUpgrade(long price) => TryPurchaseTier(ref cooldownReductionLevel, CooldownReductionLevelMax, price);
    public bool TryPurchaseExtraOutfitUnlock(long price) => TryPurchaseExtraOutfitUnlockInternal(price);

    public bool TryRefundAllUpgrades()
    {
        if (!HasAnyUpgradeLevels && totalSpentOnUpgrades <= 0)
        {
            return false;
        }

        metaCurrency = ClampCurrency(metaCurrency + totalSpentOnUpgrades);
        totalSpentOnUpgrades = 0;
        attackPowerLevel = 0;
        autoHealLevel = 0;
        attackCountLevel = 0;
        swiftnessLevel = 0;
        rerollLevel = 0;
        cooldownReductionLevel = 0;
        bossExtraOutfit02Unlocked = false;
        PersistCurrentToDisk();
        return true;
    }

    public void ApplyMenuDebugOverrides(
        long? metaCurrencyOverride,
        int? attackPowerLevelOverride,
        int? autoHealLevelOverride,
        int? attackCountLevelOverride,
        int? swiftnessLevelOverride,
        int? rerollLevelOverride,
        int? cooldownReductionLevelOverride,
        long? totalSpentOnUpgradesOverride)
    {
        if (metaCurrencyOverride.HasValue)
        {
            metaCurrency = ClampCurrency(metaCurrencyOverride.Value);
        }

        if (attackPowerLevelOverride.HasValue)
        {
            attackPowerLevel = ClampLevel(attackPowerLevelOverride.Value, AttackPowerLevelMax);
        }

        if (autoHealLevelOverride.HasValue)
        {
            autoHealLevel = ClampLevel(autoHealLevelOverride.Value, AutoHealLevelMax);
        }

        if (attackCountLevelOverride.HasValue)
        {
            attackCountLevel = ClampLevel(attackCountLevelOverride.Value, AttackCountLevelMax);
        }

        if (swiftnessLevelOverride.HasValue)
        {
            swiftnessLevel = ClampLevel(swiftnessLevelOverride.Value, SwiftnessLevelMax);
        }

        if (rerollLevelOverride.HasValue)
        {
            rerollLevel = ClampLevel(rerollLevelOverride.Value, RerollLevelMax);
        }

        if (cooldownReductionLevelOverride.HasValue)
        {
            cooldownReductionLevel = ClampLevel(cooldownReductionLevelOverride.Value, CooldownReductionLevelMax);
        }

        if (totalSpentOnUpgradesOverride.HasValue)
        {
            totalSpentOnUpgrades = totalSpentOnUpgradesOverride.Value < 0 ? 0 : totalSpentOnUpgradesOverride.Value;
        }
    }

    public void SetAttackPowerLevel(int level) => attackPowerLevel = ClampLevel(level, AttackPowerLevelMax);
    public void SetAutoHealLevel(int level) => autoHealLevel = ClampLevel(level, AutoHealLevelMax);
    public void SetAttackCountLevel(int level) => attackCountLevel = ClampLevel(level, AttackCountLevelMax);
    public void SetSwiftnessLevel(int level) => swiftnessLevel = ClampLevel(level, SwiftnessLevelMax);
    public void SetRerollLevel(int level) => rerollLevel = ClampLevel(level, RerollLevelMax);
    public void SetCooldownReductionLevel(int level) => cooldownReductionLevel = ClampLevel(level, CooldownReductionLevelMax);

    internal static void ClampSaveFields(Game03MetaSaveData data)
    {
        if (data == null)
        {
            return;
        }

        data.metaCurrency = ClampCurrency(data.metaCurrency);
        data.totalSpentOnUpgrades = data.totalSpentOnUpgrades < 0 ? 0 : data.totalSpentOnUpgrades;
        data.attackPowerLevel = ClampLevel(data.attackPowerLevel, AttackPowerLevelMax);
        data.autoHealLevel = ClampLevel(data.autoHealLevel, AutoHealLevelMax);
        data.attackCountLevel = ClampLevel(data.attackCountLevel, AttackCountLevelMax);
        data.swiftnessLevel = ClampLevel(data.swiftnessLevel, SwiftnessLevelMax);
        data.rerollLevel = ClampLevel(data.rerollLevel, RerollLevelMax);
        data.cooldownReductionLevel = ClampLevel(data.cooldownReductionLevel, CooldownReductionLevelMax);
        data.acquiredCompanionWeaponMask = Game03CompanionWeaponMetaFlags.ClampMask(data.acquiredCompanionWeaponMask);
    }

    private bool TryPurchaseExtraOutfitUnlockInternal(long price)
    {
        if (bossExtraOutfit02Unlocked || price <= 0 || metaCurrency < price)
        {
            return false;
        }

        metaCurrency -= price;
        totalSpentOnUpgrades += price;
        bossExtraOutfit02Unlocked = true;
        PersistCurrentToDisk();
        return true;
    }

    private bool TryPurchaseTier(ref int level, int maxLevel, long price)
    {
        if (level >= maxLevel || price <= 0 || metaCurrency < price)
        {
            return false;
        }

        metaCurrency -= price;
        totalSpentOnUpgrades += price;
        level++;
        PersistCurrentToDisk();
        return true;
    }

    private void ApplyFromDto(Game03MetaSaveData data)
    {
        if (data == null)
        {
            data = Game03MetaSaveData.CreateDefault();
        }

        ClampSaveFields(data);
        metaCurrency = data.metaCurrency;
        totalSpentOnUpgrades = data.totalSpentOnUpgrades;
        attackPowerLevel = data.attackPowerLevel;
        autoHealLevel = data.autoHealLevel;
        attackCountLevel = data.attackCountLevel;
        swiftnessLevel = data.swiftnessLevel;
        rerollLevel = data.rerollLevel;
        cooldownReductionLevel = data.cooldownReductionLevel;
        bossExtraOutfit02Unlocked = data.bossExtraOutfit02Unlocked;
        acquiredCompanionWeaponMask = Game03CompanionWeaponMetaFlags.ClampMask(data.acquiredCompanionWeaponMask);
    }

    private Game03MetaSaveData CaptureDto()
    {
        return new Game03MetaSaveData
        {
            version = Game03MetaSaveData.CurrentVersion,
            updatedAtUtc = string.Empty,
            metaCurrency = metaCurrency,
            totalSpentOnUpgrades = totalSpentOnUpgrades,
            attackPowerLevel = attackPowerLevel,
            autoHealLevel = autoHealLevel,
            attackCountLevel = attackCountLevel,
            swiftnessLevel = swiftnessLevel,
            rerollLevel = rerollLevel,
            cooldownReductionLevel = cooldownReductionLevel,
            bossExtraOutfit02Unlocked = bossExtraOutfit02Unlocked,
            acquiredCompanionWeaponMask = Game03CompanionWeaponMetaFlags.ClampMask(acquiredCompanionWeaponMask)
        };
    }

    private static long ClampCurrency(long value) => value < 0 ? 0 : value;

    private static int ClampLevel(int level, int max)
    {
        if (level < 0)
        {
            return 0;
        }

        return level > max ? max : level;
    }
}
