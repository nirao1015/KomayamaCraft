using System;
using System.Collections.Generic;

/// <summary>
/// LvUp の UG 抽選プール（武器種 × 通常／アンコモン／レア）。投入枚数は <see cref="Game03LevelUpManager"/> が仕様どおり合算プールにする。仕様: spec/game03/武器ごとのUG.txt
/// </summary>
public static class Game03WeaponUpgradePools
{
    public enum Tier
    {
        Normal = 0,
        Uncommon = 1,
        Rare = 2
    }

    private static readonly Game03UpgradeType[] Empty = Array.Empty<Game03UpgradeType>();

    private static readonly Game03UpgradeType[] W1N =
    {
        Game03UpgradeType.AttackSpeedUp,
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W1U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };

    private static readonly Game03UpgradeType[] W1R = { Game03UpgradeType.ProjectileCountUp };

    private static readonly Game03UpgradeType[] W2N =
    {
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.DurationUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W2U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };

    private static readonly Game03UpgradeType[] W2R = { Game03UpgradeType.ProjectileCountUp };

    private static readonly Game03UpgradeType[] W3N =
    {
        Game03UpgradeType.AttackSpeedUp,
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.DurationUp,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W3U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };

    private static readonly Game03UpgradeType[] W3R = { Game03UpgradeType.ProjectileCountUp };

    private static readonly Game03UpgradeType[] W4N =
    {
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W4U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };

    private static readonly Game03UpgradeType[] W4R = { Game03UpgradeType.ProjectileCountUp };

    private static readonly Game03UpgradeType[] W5N =
    {
        Game03UpgradeType.AttackSpeedUp,
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    /// <summary>仕様: spec/game03/武器ごとのUG.txt Weapon05（アンコモンに弾数、レア枠は空）。</summary>
    private static readonly Game03UpgradeType[] W5U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.ProjectileCountUp
    };

    private static readonly Game03UpgradeType[] W5R = Empty;

    private static readonly Game03UpgradeType[] W6N =
    {
        Game03UpgradeType.AttackSpeedUp,
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.PierceUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W6U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };
    private static readonly Game03UpgradeType[] W6R = { Game03UpgradeType.ProjectileCountUp };

    private static readonly Game03UpgradeType[] W7N =
    {
        Game03UpgradeType.AttackSpeedUp,
        Game03UpgradeType.CooldownReduction,
        Game03UpgradeType.KnockbackUp,
        Game03UpgradeType.PierceUp,
        Game03UpgradeType.CriticalRateUp,
        Game03UpgradeType.CriticalDamageUp
    };

    private static readonly Game03UpgradeType[] W7U =
    {
        Game03UpgradeType.AttackPowerUp,
        Game03UpgradeType.AttackRangeUp
    };

    private static readonly Game03UpgradeType[] W7R = { Game03UpgradeType.ProjectileCountUp };

    public static Game03UpgradeType[] GetTierPool(int weaponNumber, Tier tier)
    {
        return tier switch
        {
            Tier.Normal => weaponNumber switch
            {
                1 => W1N,
                2 => W2N,
                3 => W3N,
                4 => W4N,
                5 => W5N,
                6 => W6N,
                7 => W7N,
                _ => Empty
            },
            Tier.Uncommon => weaponNumber switch
            {
                1 => W1U,
                2 => W2U,
                3 => W3U,
                4 => W4U,
                5 => W5U,
                6 => W6U,
                7 => W7U,
                _ => Empty
            },
            Tier.Rare => weaponNumber switch
            {
                1 => W1R,
                2 => W2R,
                3 => W3R,
                4 => W4R,
                5 => W5R,
                6 => W6R,
                7 => W7R,
                _ => Empty
            },
            _ => Empty
        };
    }

    public static bool IsEligibleForWeaponLottery(int weaponNumber, Game03UpgradeType type)
    {
        return Contains(GetTierPool(weaponNumber, Tier.Normal), type)
               || Contains(GetTierPool(weaponNumber, Tier.Uncommon), type)
               || Contains(GetTierPool(weaponNumber, Tier.Rare), type);
    }

    /// <summary>LvUp と同じ合算チケットプール（通常×3・アンコモン×2・レア×1）。重複抽選可。</summary>
    public static List<Game03UpgradeType> BuildCombinedUgChoiceBag(int weaponNumber)
    {
        var bag = new List<Game03UpgradeType>(48);
        AppendPoolTickets(bag, GetTierPool(weaponNumber, Tier.Normal), 3);
        AppendPoolTickets(bag, GetTierPool(weaponNumber, Tier.Uncommon), 2);
        AppendPoolTickets(bag, GetTierPool(weaponNumber, Tier.Rare), 1);
        return bag;
    }

    public static bool TryDrawRandomUpgradeFromCombinedPool(int weaponNumber, out Game03UpgradeType upgradeType)
    {
        upgradeType = default;
        List<Game03UpgradeType> bag = BuildCombinedUgChoiceBag(weaponNumber);
        if (bag.Count == 0)
        {
            return false;
        }

        upgradeType = bag[UnityEngine.Random.Range(0, bag.Count)];
        return true;
    }

    private static void AppendPoolTickets(List<Game03UpgradeType> bag, Game03UpgradeType[] pool, int copiesPerType)
    {
        if (pool == null || pool.Length == 0 || copiesPerType <= 0)
        {
            return;
        }

        for (int i = 0; i < pool.Length; i++)
        {
            for (int c = 0; c < copiesPerType; c++)
            {
                bag.Add(pool[i]);
            }
        }
    }

    private static bool Contains(Game03UpgradeType[] pool, Game03UpgradeType type)
    {
        if (pool == null)
        {
            return false;
        }

        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == type)
            {
                return true;
            }
        }

        return false;
    }
}
