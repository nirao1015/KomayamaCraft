using System;

namespace Game02
{
    /// <summary>
    /// アップグレード販売価格（side_panel_upgrade_spec.md）。
    /// </summary>
    public static class UpgradePriceUtility
    {
        /// <summary>
        /// 最終販売価格の共通丸め。百の位切り捨て、1000未満は1。
        /// </summary>
        public static long NormalizeFinalSalePrice(double rawPrice)
        {
            if (rawPrice < 0d || double.IsNaN(rawPrice) || double.IsInfinity(rawPrice))
            {
                return 1L;
            }

            long truncated = (long)(Math.Floor(rawPrice / 100d) * 100d);
            if (truncated < 1000L)
            {
                return 1L;
            }

            return truncated;
        }

        /// <summary>
        /// レベル <paramref name="level"/>（初回購入=0）での販売価格。百の位切り捨て、1000未満は1。
        /// </summary>
        public static long ComputeSalePrice(long basePrice, double priceMultiplier, int level)
        {
            level = Math.Max(0, level);
            double raw = basePrice * Math.Pow(priceMultiplier, level);
            return NormalizeFinalSalePrice(raw);
        }
    }
}
