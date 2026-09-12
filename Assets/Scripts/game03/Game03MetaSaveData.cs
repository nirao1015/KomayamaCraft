using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// game03 ゲーム外成長の永続化 DTO（JsonUtility）。正本: spec/game03/game03_meta_growth_menu03_spec.md §9
/// </summary>
[Serializable]
public sealed class Game03MetaSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    [FormerlySerializedAs("savedAtUtc")]
    public string updatedAtUtc = string.Empty;
    public string buildVersion = string.Empty;

    public long metaCurrency;
    public long totalSpentOnUpgrades;

    public int attackPowerLevel;
    public int autoHealLevel;
    public int attackCountLevel;
    public int swiftnessLevel;
    public int rerollLevel;
    public int cooldownReductionLevel;
    public bool bossExtraOutfit02Unlocked;
    /// <summary>累積入手した仲間武器（02〜07）のビットマスク。<see cref="Game03CompanionWeaponMetaFlags"/>。</summary>
    public int acquiredCompanionWeaponMask;

    public static Game03MetaSaveData CreateDefault()
    {
        return new Game03MetaSaveData
        {
            version = CurrentVersion,
            updatedAtUtc = string.Empty,
            buildVersion = string.Empty,
            metaCurrency = 0,
            totalSpentOnUpgrades = 0,
            attackPowerLevel = 0,
            autoHealLevel = 0,
            attackCountLevel = 0,
            swiftnessLevel = 0,
            rerollLevel = 0,
            cooldownReductionLevel = 0,
            bossExtraOutfit02Unlocked = false,
            acquiredCompanionWeaponMask = 0
        };
    }
}
