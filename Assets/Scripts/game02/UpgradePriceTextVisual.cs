using TMPro;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// アップグレード価格 TMP を「左上ハイライト＋他3頂点を状態色」で表示する（TMP の Color Gradient と同一）。
    /// 色は <see cref="Game02EffectManager"/> の「販売色」で一元管理（未取得時は従来どおりの既定色）。
    /// </summary>
    public static class UpgradePriceTextVisual
    {
        private static readonly Color DefaultAffordableEdge = new Color(0.22745098f, 0.35686275f, 1f, 1f);
        private static readonly Color DefaultUnaffordableEdge = Color.red;
        private static readonly Color DefaultGradientTopLeft = new Color(0.81960785f, 0.81960785f, 0.81960785f, 1f);
        private static readonly Color DefaultSoldOutEdge = new Color(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color DefaultSoldOutTopLeft = new Color(0.38f, 0.38f, 0.40f, 1f);

        /// <summary>購入可否に応じた販売色グラデを適用する。</summary>
        public static void ApplySalePriceGradient(TMP_Text priceText, bool canAfford)
        {
            Game02EffectManager mgr = Game02EffectManager.TryGet();
            Color edge = canAfford
                ? (mgr != null ? mgr.SalePriceAffordableEdge : DefaultAffordableEdge)
                : (mgr != null ? mgr.SalePriceUnaffordableEdge : DefaultUnaffordableEdge);
            Color topLeft = mgr != null ? mgr.SalePriceGradientTopLeft : DefaultGradientTopLeft;
            ApplySalePriceGradient(priceText, edge, topLeft);
        }

        /// <summary>購入可否グラデ（明示色）。通常は <see cref="ApplySalePriceGradient(TMP_Text, bool)"/> を使う。</summary>
        public static void ApplySalePriceGradient(TMP_Text priceText, Color edgeColor, Color topLeftHighlight)
        {
            if (priceText == null)
            {
                return;
            }

            Color tl = topLeftHighlight;
            tl.a = edgeColor.a;
            priceText.enableVertexGradient = true;
            priceText.colorGradient = new VertexGradient(tl, edgeColor, edgeColor, edgeColor);
            priceText.color = Color.white;
        }

        /// <summary>
        /// 売り切れ表示をグラデで統一する（色は Game02EffectManager の販売色）。
        /// </summary>
        /// <param name="secondaryPriceText">別オブジェクトの Sold out ラベルなど（任意）。</param>
        public static void ApplySoldOutPriceStyle(TMP_Text primaryPriceText, TMP_Text secondaryPriceText = null)
        {
            ApplySoldOutPriceStyleOne(primaryPriceText);
            ApplySoldOutPriceStyleOne(secondaryPriceText);
        }

        private static void ApplySoldOutPriceStyleOne(TMP_Text priceText)
        {
            if (priceText == null)
            {
                return;
            }

            Game02EffectManager mgr = Game02EffectManager.TryGet();
            Color edge = mgr != null ? mgr.SalePriceSoldOutEdge : DefaultSoldOutEdge;
            Color tl = mgr != null ? mgr.SalePriceSoldOutTopLeft : DefaultSoldOutTopLeft;
            tl.a = edge.a;
            priceText.enableVertexGradient = true;
            priceText.colorGradient = new VertexGradient(tl, edge, edge, edge);
            priceText.color = Color.white;
        }
    }
}
