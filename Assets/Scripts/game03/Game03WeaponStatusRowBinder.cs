using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PausePanel の StattusW* / ResultPanel の OjStattusW* 行 UI（装備アイコン・UG レベル・任意で与ダメージ）を更新する。
/// </summary>
public static class Game03WeaponStatusRowBinder
{
    private static readonly Regex ImageUgOrderPattern = new Regex(@"^ImageUg(\d{1,2})", RegexOptions.CultureInvariant);

    public sealed class ParsedWeaponRow
    {
        public RectTransform Root;
        public Image WeaponIcon;
        public TMP_Text TotalLevelText;
        public TMP_Text DamageText;
        public TMP_Text[] OrderedUpgradeLevels = new TMP_Text[10];
    }

    public static bool TryParseRow(RectTransform rowRoot, bool bindDamageText, out ParsedWeaponRow row)
    {
        row = null;
        if (rowRoot == null)
        {
            return false;
        }

        row = new ParsedWeaponRow { Root = rowRoot };
        row.WeaponIcon = FindDirectChildImage(rowRoot, "AcceptedItemView");
        row.TotalLevelText = FindDirectChildTmpStartingWith(rowRoot, "UgTarrgetLVText");
        if (bindDamageText)
        {
            row.DamageText = FindDirectChildTmpExact(rowRoot, "ResultTextDamage");
        }

        var ordered = new List<(int order, TMP_Text txt)>();
        for (int c = 0; c < rowRoot.childCount; c++)
        {
            Transform ch = rowRoot.GetChild(c);
            Match m = ImageUgOrderPattern.Match(ch.name);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int ord))
            {
                continue;
            }

            TMP_Text lvlTmp = ch.GetComponentInChildren<TMP_Text>(true);
            if (lvlTmp == null)
            {
                continue;
            }

            ordered.Add((ord, lvlTmp));
        }

        ordered.Sort((a, b) => a.order.CompareTo(b.order));
        for (int i = 0; i < ordered.Count; i++)
        {
            int idx = ordered[i].order - 1;
            if (idx >= 0 && idx < row.OrderedUpgradeLevels.Length)
            {
                row.OrderedUpgradeLevels[idx] = ordered[i].txt;
            }
        }

        return row.WeaponIcon != null || row.TotalLevelText != null || ordered.Count > 0 || row.DamageText != null;
    }

    public static ParsedWeaponRow[] ParseRows(RectTransform[] rowRoots, bool bindDamageText)
    {
        if (rowRoots == null || rowRoots.Length == 0)
        {
            return Array.Empty<ParsedWeaponRow>();
        }

        var parsed = new ParsedWeaponRow[rowRoots.Length];
        for (int i = 0; i < rowRoots.Length; i++)
        {
            RectTransform rt = rowRoots[i];
            if (rt == null || !TryParseRow(rt, bindDamageText, out ParsedWeaponRow row))
            {
                parsed[i] = null;
                continue;
            }

            parsed[i] = row;
        }

        return parsed;
    }

    public static void RefreshRows(
        ParsedWeaponRow[] rows,
        Game03WeaponManager weaponManager,
        bool hideSubRowsWhenSlotEmpty,
        Game03RunWeaponDamageTracker damageTracker)
    {
        if (rows == null || weaponManager == null)
        {
            return;
        }

        string dash = weaponManager.NotLotteryEligibleUpgradeLevelDisplay;
        for (int slot = 0; slot < rows.Length; slot++)
        {
            ParsedWeaponRow row = rows[slot];
            if (row == null || row.Root == null)
            {
                continue;
            }

            bool equipped = weaponManager.TryGetEquippedWeaponForSlot(slot, out int weaponNumber, out Sprite sprite);
            bool hideSub = hideSubRowsWhenSlotEmpty && slot > 0;
            if (hideSub && !equipped)
            {
                row.Root.gameObject.SetActive(false);
                continue;
            }

            row.Root.gameObject.SetActive(true);

            if (row.WeaponIcon != null)
            {
                if (equipped && sprite != null)
                {
                    row.WeaponIcon.sprite = sprite;
                    row.WeaponIcon.enabled = true;
                }
                else
                {
                    row.WeaponIcon.enabled = false;
                }
            }

            if (row.TotalLevelText != null)
            {
                row.TotalLevelText.text = equipped && weaponNumber > 0
                    ? weaponManager.GetTotalUpgradeCountForWeapon(weaponNumber).ToString()
                    : "0";
            }

            if (row.DamageText != null)
            {
                long dmg = equipped && weaponNumber > 0 && damageTracker != null
                    ? damageTracker.GetDamageForWeapon(weaponNumber)
                    : 0L;
                row.DamageText.text = FormatIntegerWithCommas((int)Mathf.Clamp(dmg, 0, int.MaxValue));
            }

            for (int ti = 0; ti < row.OrderedUpgradeLevels.Length; ti++)
            {
                TMP_Text t = row.OrderedUpgradeLevels[ti];
                if (t == null)
                {
                    continue;
                }

                if (!equipped || weaponNumber <= 0)
                {
                    t.text = "0";
                    continue;
                }

                var ugType = (Game03UpgradeType)ti;
                if (!Game03WeaponManager.IsUpgradeTypeInWeaponLotteryPool(weaponNumber, ugType))
                {
                    t.text = dash;
                    continue;
                }

                int lv = weaponManager.GetUpgradeCountForWeaponType(weaponNumber, ugType);
                t.text = lv.ToString();
            }
        }
    }

    private static string FormatIntegerWithCommas(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static Image FindDirectChildImage(RectTransform rowRoot, string exactName)
    {
        for (int i = 0; i < rowRoot.childCount; i++)
        {
            Transform ch = rowRoot.GetChild(i);
            if (ch.name == exactName)
            {
                return ch.GetComponent<Image>();
            }
        }

        return null;
    }

    private static TMP_Text FindDirectChildTmpStartingWith(RectTransform rowRoot, string prefix)
    {
        for (int i = 0; i < rowRoot.childCount; i++)
        {
            Transform ch = rowRoot.GetChild(i);
            if (ch.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return ch.GetComponent<TMP_Text>();
            }
        }

        return null;
    }

    private static TMP_Text FindDirectChildTmpExact(RectTransform rowRoot, string exactName)
    {
        for (int i = 0; i < rowRoot.childCount; i++)
        {
            Transform ch = rowRoot.GetChild(i);
            if (ch.name == exactName)
            {
                return ch.GetComponent<TMP_Text>();
            }
        }

        return null;
    }
}
