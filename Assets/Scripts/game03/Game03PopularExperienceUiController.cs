using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// PopularUIBackground: PopularUI の数値テキストと ImageExp の進捗バーを更新する。
/// </summary>
/// <remarks>
/// <b>Popular 数値テキスト</b> — <see cref="Game03StatusManager.PopularDisplayExperienceTotal"/>（経験値総獲得量 + アイテム等の表示専用加算）。<br/>
/// <b>ImageExp バー</b> — <see cref="Game03StatusManager.GetNextLevelProgress01"/>（<see cref="Game03StatusManager.CurrentExp"/> ベースの次レベルまで進捗）。
/// </remarks>
[DisallowMultipleComponent]
public sealed class Game03PopularExperienceUiController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Game03StatusManager statusManager;
    [SerializeField] private TMP_Text popularExperienceText;
    [SerializeField] private RectTransform imageExpFill;

    private float cachedFullFillWidth;

    private void Awake()
    {
        CacheFillWidthIfNeeded();
        Refresh(forceText: true);
    }

    private void OnEnable()
    {
        if (statusManager != null)
        {
            statusManager.ExperienceChanged += OnExperienceChanged;
            statusManager.LevelChanged += OnLevelChanged;
        }

        CacheFillWidthIfNeeded();
        Refresh(forceText: true);
    }

    private void OnDisable()
    {
        if (statusManager != null)
        {
            statusManager.ExperienceChanged -= OnExperienceChanged;
            statusManager.LevelChanged -= OnLevelChanged;
        }
    }

    private void OnExperienceChanged(int _, int __, int ___)
    {
        Refresh(forceText: false);
    }

    private void OnLevelChanged(int _)
    {
        Refresh(forceText: true);
    }

    private void CacheFillWidthIfNeeded()
    {
        if (imageExpFill == null)
        {
            return;
        }

        if (cachedFullFillWidth <= 0f)
        {
            cachedFullFillWidth = Mathf.Max(0f, imageExpFill.sizeDelta.x);
        }
    }

    private void Refresh(bool forceText)
    {
        if (popularExperienceText != null && statusManager != null)
        {
            int displayPopular = Game03MetaEconomyRules.ComputePopularDisplayValue(
                statusManager.PopularDisplayExperienceTotal);
            string next = FormatWithCommaSeparators(displayPopular);
            if (forceText || popularExperienceText.text != next)
            {
                popularExperienceText.text = next;
            }
        }

        if (imageExpFill != null && statusManager != null)
        {
            CacheFillWidthIfNeeded();
            float p = statusManager.GetNextLevelProgress01();
            float w = cachedFullFillWidth * Mathf.Clamp01(p);
            Vector2 sd = imageExpFill.sizeDelta;
            sd.x = w;
            imageExpFill.sizeDelta = sd;
        }
    }

    private static string FormatWithCommaSeparators(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
