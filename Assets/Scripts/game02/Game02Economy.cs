using System;

namespace Game02
{
    /// <summary>
    /// game02 経済の共通係数（仕様: spec/game02/balance_timeline_60min_spec.md）。
    /// </summary>
    public static class Game02Economy
    {
        /// <summary>SidePanel 系アップグレードの販売価格に掛ける係数（ComputeSalePrice 後）。</summary>
        public const float SidePanelSalePriceScale = 0.329f;

        public static long ScaleSidePanelPrice(long computedSalePrice)
        {
            double raw = computedSalePrice * SidePanelSalePriceScale;
            return UpgradePriceUtility.NormalizeFinalSalePrice(raw);
        }

        public static long ComputeScaledSalePrice(long basePrice, double priceMultiplier, int level)
        {
            long computed = UpgradePriceUtility.ComputeSalePrice(basePrice, priceMultiplier, level);
            long scaled = ScaleSidePanelPrice(computed);
            double lateMultiplier = ResolveLateGamePriceMultiplierByPopularity();
            if (lateMultiplier <= 1d)
            {
                return scaled;
            }

            return UpgradePriceUtility.NormalizeFinalSalePrice(scaled * lateMultiplier);
        }

        public static long ComputeScaledSalePriceWithProgressiveMultiplier(
            long basePrice,
            double baseMultiplier,
            double multiplierIncrementPerLevel,
            int level)
        {
            int safeLevel = Math.Max(0, level);
            double safeBase = Math.Max(1d, basePrice);
            double dynamicMultiplier = Math.Max(1d, baseMultiplier + Math.Max(0d, multiplierIncrementPerLevel) * safeLevel);
            double logRaw = Math.Log(safeBase) + (safeLevel * Math.Log(dynamicMultiplier));
            double maxLog = Math.Log(long.MaxValue);
            double rawComputed = logRaw >= maxLog ? long.MaxValue : Math.Exp(logRaw);

            long computed = UpgradePriceUtility.NormalizeFinalSalePrice(rawComputed);
            long scaled = ScaleSidePanelPrice(computed);
            double lateMultiplier = ResolveLateGamePriceMultiplierByPopularity();
            if (lateMultiplier <= 1d)
            {
                return scaled;
            }

            return UpgradePriceUtility.NormalizeFinalSalePrice(scaled * lateMultiplier);
        }

        private static double ResolveLateGamePriceMultiplierByPopularity()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return 1d;
            }

            long popularity = Math.Max(0L, gm.CurrentPopularity);
            if (popularity >= 700_000L)
            {
                return 1.55d;
            }

            if (popularity >= 200_000L)
            {
                return 1.20d;
            }

            return 1d;
        }
    }
}
