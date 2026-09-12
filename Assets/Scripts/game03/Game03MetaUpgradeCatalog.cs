/// <summary>
/// menu03 メタ UG の購入可能回数・段階価格の正本（spec/game03/game03_meta_growth_menu03_spec.md §7–8）。
/// </summary>
public static class Game03MetaUpgradeCatalog
{
    public enum UpgradeId
    {
        AttackPower = 1,
        AutoHeal = 2,
        AttackCount = 3,
        Swiftness = 4,
        Reroll = 5,
        CooldownReduction = 6,
        ExtraOutfitUnlock = 7
    }

    public readonly struct UpgradeDefinition
    {
        public readonly long[] TierPrices;

        public UpgradeDefinition(long[] tierPrices)
        {
            TierPrices = tierPrices ?? System.Array.Empty<long>();
        }

        public int PurchasableCount => TierPrices.Length;
    }

    public static readonly UpgradeDefinition AttackPower = new UpgradeDefinition(new long[] { 100, 300, 1000, 1500 });
    public static readonly UpgradeDefinition AutoHeal = new UpgradeDefinition(new long[] { 100, 800 });
    public static readonly UpgradeDefinition AttackCount = new UpgradeDefinition(new long[] { 100, 2000 });
    public static readonly UpgradeDefinition Swiftness = new UpgradeDefinition(new long[] { 500, 1000 });
    public static readonly UpgradeDefinition Reroll = new UpgradeDefinition(new long[] { 300, 400, 600, 900, 1500 });
    public static readonly UpgradeDefinition CooldownReduction = new UpgradeDefinition(new long[] { 100, 300, 1000 });
    public static readonly UpgradeDefinition ExtraOutfitUnlock = new UpgradeDefinition(new long[] { 3000 });

    public static UpgradeDefinition GetDefinition(UpgradeId id)
    {
        return id switch
        {
            UpgradeId.AttackPower => AttackPower,
            UpgradeId.AutoHeal => AutoHeal,
            UpgradeId.AttackCount => AttackCount,
            UpgradeId.Swiftness => Swiftness,
            UpgradeId.Reroll => Reroll,
            UpgradeId.CooldownReduction => CooldownReduction,
            UpgradeId.ExtraOutfitUnlock => ExtraOutfitUnlock,
            _ => default
        };
    }

    public static int GetPurchasableCount(UpgradeId id) => GetDefinition(id).PurchasableCount;
}
