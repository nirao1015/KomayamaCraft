using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// ResultPanel のサマリー／武器行をラン終了時の値で埋める（ゲームオーバー・タイムクリア共通）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03ResultPanelPopulator : MonoBehaviour
{
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03StatusManager statusManager;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private Game03RunWeaponDamageTracker weaponDamageTracker;

    [SerializeField] private TextMeshProUGUI resultTextExp;
    [SerializeField] private TextMeshProUGUI resultTextAlive;
    [SerializeField] private TextMeshProUGUI resultTextPopular;
    [SerializeField] private TextMeshProUGUI resultTextLv;
    [SerializeField] private TextMeshProUGUI resultTextEnemyCount;

    [SerializeField, Tooltip("OjStattusWMain, OjStattusW01 … OjStattusW04（スロット 0〜4）")]
    private RectTransform[] resultWeaponRowRoots = new RectTransform[5];

    [SerializeField] private bool hideSubWeaponRowsWhenSlotEmpty = true;

    private Game03WeaponStatusRowBinder.ParsedWeaponRow[] parsedWeaponRows;
    private bool weaponRowsParsed;

    private void Awake()
    {
        EnsureWeaponRowsParsed();
    }

    public void PopulateFromCurrentRun()
    {
        EnsureWeaponRowsParsed();

        int popular = statusManager != null
            ? Game03MetaEconomyRules.ComputePopularDisplayValue(statusManager.PopularDisplayExperienceTotal)
            : 0;
        int kills = statusManager != null ? statusManager.TotalKills : 0;
        int ugTotal = weaponManager != null ? weaponManager.GetTotalUpgradeCountForAllEquippedWeapons() : 0;
        int elapsedWholeSeconds = game03Manager != null
            ? Mathf.FloorToInt(Mathf.Max(0f, game03Manager.GameplayElapsedSeconds))
            : 0;

        long metaGrant = Game03RunSessionState.LastMetaCurrencyGranted;
        SetTmpText(resultTextExp, FormatIntegerWithCommas(metaGrant));
        SetTmpText(resultTextAlive, Game03ElapsedTimeUiFormatter.FormatElapsed(elapsedWholeSeconds));
        SetTmpText(resultTextPopular, FormatIntegerWithCommas(popular));
        SetTmpText(resultTextLv, ugTotal.ToString(CultureInfo.InvariantCulture));
        SetTmpText(resultTextEnemyCount, kills.ToString(CultureInfo.InvariantCulture));

        Game03RunWeaponDamageTracker tracker = weaponDamageTracker != null
            ? weaponDamageTracker
            : Game03RunWeaponDamageTracker.TryGet();
        if (weaponRowsParsed && weaponManager != null)
        {
            Game03WeaponStatusRowBinder.RefreshRows(
                parsedWeaponRows,
                weaponManager,
                hideSubWeaponRowsWhenSlotEmpty,
                tracker);
        }
    }

    private void EnsureWeaponRowsParsed()
    {
        if (weaponRowsParsed)
        {
            return;
        }

        parsedWeaponRows = Game03WeaponStatusRowBinder.ParseRows(resultWeaponRowRoots, bindDamageText: true);
        weaponRowsParsed = parsedWeaponRows.Length > 0;
    }

    private static void SetTmpText(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null)
        {
            tmp.text = value;
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
}
