using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// menu03 UGCanvas.UGOj 配下のメタ UG 購入・払い戻し・所持表示。
/// 仕様: spec/game03/game03_meta_growth_menu03_spec.md
/// ButtonUG の OnClick は Inspector で OnClickUg01〜07 / OnClickClearRefund に接続する。
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class Menu03UgShopController : MonoBehaviour
{
    private static readonly Regex OjChkOrderPattern = new Regex(@"^OjChk(?:\s*\((\d+)\))?$", RegexOptions.CultureInvariant);

    [Serializable]
    private sealed class UgButtonBindings
    {
        public Button button;
        public Graphic hoverOverlayHighlightGraphic;
        public GameObject hoverOverlayGrayRoot;
        public TMP_Text priceLabel;
        public GameObject[] orderedCheckImages = Array.Empty<GameObject>();
    }

    [Header("Refs")]
    [SerializeField] private TMP_Text ugMoneyText;
    [SerializeField] private Button buttonClear;
    [SerializeField] private Menu03SeManager menu03SeManager;

    [Header("表示文言")]
    [SerializeField] private string soldOutDisplayText = "SoldeOut";

    [SerializeField] private UgButtonBindings ug01;
    [SerializeField] private UgButtonBindings ug02;
    [SerializeField] private UgButtonBindings ug03;
    [SerializeField] private UgButtonBindings ug04;
    [SerializeField] private UgButtonBindings ug05;
    [SerializeField] private UgButtonBindings ug06;
    [SerializeField] private UgButtonBindings ug07;

    private void Awake()
    {
        ResolveStandardChildRefs(ug01);
        ResolveStandardChildRefs(ug02);
        ResolveStandardChildRefs(ug03);
        ResolveStandardChildRefs(ug04);
        ResolveStandardChildRefs(ug05);
        ResolveStandardChildRefs(ug06);
        ResolveStandardChildRefs(ug07);
        ParseOjChksForAllBindings();
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    private void Start()
    {
        RefreshAll();
    }

    public void OnClickUg01() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.AttackPower, p => Game03MetaProgressController.Instance?.TryPurchaseAttackPowerUpgrade(p) ?? false);
    public void OnClickUg02() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.AutoHeal, p => Game03MetaProgressController.Instance?.TryPurchaseAutoHealUpgrade(p) ?? false);
    public void OnClickUg03() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.AttackCount, p => Game03MetaProgressController.Instance?.TryPurchaseAttackCountUpgrade(p) ?? false);
    public void OnClickUg04() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.Swiftness, p => Game03MetaProgressController.Instance?.TryPurchaseSwiftnessUpgrade(p) ?? false);
    public void OnClickUg05() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.Reroll, p => Game03MetaProgressController.Instance?.TryPurchaseRerollUpgrade(p) ?? false);
    public void OnClickUg06() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.CooldownReduction, p => Game03MetaProgressController.Instance?.TryPurchaseCooldownReductionUpgrade(p) ?? false);
    public void OnClickUg07() => TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId.ExtraOutfitUnlock, p => Game03MetaProgressController.Instance?.TryPurchaseExtraOutfitUnlock(p) ?? false);

    public void OnClickClearRefund()
    {
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null || !meta.HasAnyUpgradeLevels)
        {
            return;
        }

        if (!meta.TryRefundAllUpgrades())
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.UgClearRefund);
        RefreshAll();
    }

    public void RefreshDisplay()
    {
        RefreshAll();
    }

    private void TryPurchaseCatalog(Game03MetaUpgradeCatalog.UpgradeId upgradeId, Func<long, bool> tryPurchase)
    {
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null)
        {
            return;
        }

        Game03MetaUpgradeCatalog.UpgradeDefinition def = Game03MetaUpgradeCatalog.GetDefinition(upgradeId);
        int currentLevel = GetPurchasedLevel(meta, upgradeId);
        if (def.TierPrices == null || currentLevel >= def.PurchasableCount)
        {
            return;
        }

        long price = def.TierPrices[currentLevel];
        if (price <= 0 || !tryPurchase(price))
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.UgPurchaseButton);
        RefreshAll();
    }

    private static int GetPurchasedLevel(Game03MetaProgressController meta, Game03MetaUpgradeCatalog.UpgradeId upgradeId)
    {
        return upgradeId switch
        {
            Game03MetaUpgradeCatalog.UpgradeId.AttackPower => meta.AttackPowerLevel,
            Game03MetaUpgradeCatalog.UpgradeId.AutoHeal => meta.AutoHealLevel,
            Game03MetaUpgradeCatalog.UpgradeId.AttackCount => meta.AttackCountLevel,
            Game03MetaUpgradeCatalog.UpgradeId.Swiftness => meta.SwiftnessLevel,
            Game03MetaUpgradeCatalog.UpgradeId.Reroll => meta.RerollLevel,
            Game03MetaUpgradeCatalog.UpgradeId.CooldownReduction => meta.CooldownReductionLevel,
            Game03MetaUpgradeCatalog.UpgradeId.ExtraOutfitUnlock => meta.BossExtraOutfit02Unlocked ? 1 : 0,
            _ => 0
        };
    }

    private void RefreshAll()
    {
        RefreshMoneyLabel();
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        string soldOutLabel = soldOutDisplayText ?? string.Empty;
        RefreshUgCatalog(meta, ug01, Game03MetaUpgradeCatalog.UpgradeId.AttackPower, soldOutLabel);
        RefreshUgCatalog(meta, ug02, Game03MetaUpgradeCatalog.UpgradeId.AutoHeal, soldOutLabel);
        RefreshUgCatalog(meta, ug03, Game03MetaUpgradeCatalog.UpgradeId.AttackCount, soldOutLabel);
        RefreshUgCatalog(meta, ug04, Game03MetaUpgradeCatalog.UpgradeId.Swiftness, soldOutLabel);
        RefreshUgCatalog(meta, ug05, Game03MetaUpgradeCatalog.UpgradeId.Reroll, soldOutLabel);
        RefreshUgCatalog(meta, ug06, Game03MetaUpgradeCatalog.UpgradeId.CooldownReduction, soldOutLabel);
        RefreshUgCatalog(meta, ug07, Game03MetaUpgradeCatalog.UpgradeId.ExtraOutfitUnlock, soldOutLabel);
        RefreshClearButton(meta);
    }

    private void RefreshMoneyLabel()
    {
        if (ugMoneyText == null)
        {
            return;
        }

        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        long amount = meta != null ? meta.MetaCurrency : 0;
        ugMoneyText.text = FormatIntegerWithCommas(amount);
    }

    private void RefreshClearButton(Game03MetaProgressController meta)
    {
        if (buttonClear == null)
        {
            return;
        }

        buttonClear.interactable = meta != null && meta.HasAnyUpgradeLevels;
    }

    private static void RefreshUgCatalog(Game03MetaProgressController meta, UgButtonBindings b, Game03MetaUpgradeCatalog.UpgradeId upgradeId, string soldOutLabel)
    {
        if (b?.button == null)
        {
            return;
        }

        Game03MetaUpgradeCatalog.UpgradeDefinition def = Game03MetaUpgradeCatalog.GetDefinition(upgradeId);
        int level = meta != null ? GetPurchasedLevel(meta, upgradeId) : 0;
        int purchasableCount = def.PurchasableCount;
        bool maxed = purchasableCount <= 0 || level >= purchasableCount;
        long price = !maxed && level >= 0 && level < def.TierPrices.Length ? def.TierPrices[level] : 0;
        bool canBuy = !maxed && meta != null && price > 0 && meta.MetaCurrency >= price;
        ApplyUgPriceLabel(b.priceLabel, maxed, price, soldOutLabel);
        ApplyUgVisual(b, canBuy);
        ApplyOjChkVisuals(b, level, purchasableCount);
    }

    private static void ApplyOjChkVisuals(UgButtonBindings b, int purchasedLevel, int maxLevel)
    {
        if (b?.button == null)
        {
            return;
        }

        Transform rowRoot = b.button.transform;
        for (int c = 0; c < rowRoot.childCount; c++)
        {
            Transform ojChkRoot = rowRoot.GetChild(c);
            if (!TryParseOjChkOrder(ojChkRoot.name, out int order))
            {
                continue;
            }

            bool slotInUse = order < maxLevel;
            ojChkRoot.gameObject.SetActive(slotInUse);

            GameObject imageChkGo = ResolveImageChkGameObject(ojChkRoot);
            if (imageChkGo == null)
            {
                continue;
            }

            bool showCheck = slotInUse && order < purchasedLevel;
            imageChkGo.SetActive(showCheck);
        }
    }

    private static void ApplyUgPriceLabel(TMP_Text label, bool maxed, long price, string soldOutLabel)
    {
        if (label == null)
        {
            return;
        }

        label.text = maxed ? soldOutLabel : FormatIntegerWithCommas(price);
    }

    private static void ApplyUgVisual(UgButtonBindings b, bool canPurchase)
    {
        if (b?.button == null)
        {
            return;
        }

        b.button.interactable = canPurchase;

        GameObject grayRoot = b.hoverOverlayGrayRoot;
        if (grayRoot != null)
        {
            grayRoot.SetActive(!canPurchase);
        }

        Graphic highlight = b.hoverOverlayHighlightGraphic;
        if (highlight != null)
        {
            highlight.gameObject.SetActive(canPurchase);
        }
    }

    private static string FormatIntegerWithCommas(long value)
    {
        if (value < 0)
        {
            value = 0;
        }

        if (value > int.MaxValue)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return ((int)value).ToString("N0", CultureInfo.InvariantCulture);
    }

    private void ParseOjChksForAllBindings()
    {
        TryParseOjChks(ug01);
        TryParseOjChks(ug02);
        TryParseOjChks(ug03);
        TryParseOjChks(ug04);
        TryParseOjChks(ug05);
        TryParseOjChks(ug06);
        TryParseOjChks(ug07);
    }

    private static void ResolveStandardChildRefs(UgButtonBindings bindings)
    {
        if (bindings?.button == null)
        {
            return;
        }

        if (bindings.priceLabel == null)
        {
            bindings.priceLabel = FindChildComponentByName<TMP_Text>(bindings.button.transform, "UGPriceText");
        }

        if (bindings.hoverOverlayHighlightGraphic == null)
        {
            Transform hover = bindings.button.transform.Find("HoverOverlay ");
            if (hover == null)
            {
                hover = bindings.button.transform.Find("HoverOverlay");
            }

            if (hover != null)
            {
                bindings.hoverOverlayHighlightGraphic = hover.GetComponent<Graphic>();
            }
        }
    }

    private static T FindChildComponentByName<T>(Transform root, string nameContains)
        where T : Component
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform ch = root.GetChild(i);
            if (ch.name.IndexOf(nameContains, StringComparison.Ordinal) >= 0)
            {
                T found = ch.GetComponent<T>();
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void TryParseOjChks(UgButtonBindings bindings)
    {
        if (bindings?.button == null)
        {
            return;
        }

        bindings.orderedCheckImages = CollectOrderedOjChkImages(bindings.button.transform);
    }

    private static bool TryParseOjChkOrder(string objectName, out int order)
    {
        order = 0;
        Match m = OjChkOrderPattern.Match(objectName);
        if (!m.Success)
        {
            return false;
        }

        order = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        return true;
    }

    private static GameObject ResolveImageChkGameObject(Transform ojChkRoot)
    {
        Transform imageChk = ojChkRoot.Find("ImageChk");
        if (imageChk != null)
        {
            return imageChk.gameObject;
        }

        Image[] images = ojChkRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name.IndexOf("ImageChk", StringComparison.Ordinal) >= 0)
            {
                return images[i].gameObject;
            }
        }

        return null;
    }

    private static GameObject[] CollectOrderedOjChkImages(Transform rowRoot)
    {
        int maxOrder = -1;
        for (int c = 0; c < rowRoot.childCount; c++)
        {
            if (TryParseOjChkOrder(rowRoot.GetChild(c).name, out int order) && order > maxOrder)
            {
                maxOrder = order;
            }
        }

        if (maxOrder < 0)
        {
            return Array.Empty<GameObject>();
        }

        var result = new GameObject[maxOrder + 1];
        for (int c = 0; c < rowRoot.childCount; c++)
        {
            Transform ch = rowRoot.GetChild(c);
            if (!TryParseOjChkOrder(ch.name, out int order))
            {
                continue;
            }

            GameObject imageChkGo = ResolveImageChkGameObject(ch);
            if (imageChkGo != null && order >= 0 && order < result.Length)
            {
                result[order] = imageChkGo;
            }
        }

        return result;
    }
}
