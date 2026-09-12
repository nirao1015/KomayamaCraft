using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// game02 の <c>Game02EffectManager.PlayAddUi</c> と同型の単体 TMP 用加算演出（game03 スマホアイテム等）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03PopularAddUiPlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private TMP_Text targetUi;

    [Header("Animation (match Game02EffectManager AddUiBinding defaults for PopularAddUI)")]
    [SerializeField, Min(0.01f)] private float totalDisplaySeconds = 1.5f;
    [SerializeField] private float moveUpPixels = 15f;
    [SerializeField] private float moveRightPixels = 4f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.2f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.2f;
    [SerializeField] private bool useExplicitHoldSeconds = true;
    [SerializeField, Min(0f)] private float holdSeconds = 0.5f;

    private Coroutine activeCoroutine;
    private Color baseColor;
    private bool hasBaseColor;
    private Vector2 startAnchoredPosition;
    private bool hasStartAnchoredPosition;

    /// <summary>加算値を表示する。0 以下は何もしない。</summary>
    public void PlayAdd(long addValue)
    {
        if (targetUi == null || addValue <= 0L)
        {
            return;
        }

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        hasBaseColor = false;
        hasStartAnchoredPosition = false;

        if (!targetUi.gameObject.activeSelf)
        {
            targetUi.gameObject.SetActive(true);
        }

        targetUi.text = $"+{addValue.ToString("N0", CultureInfo.InvariantCulture)}";
        SetAddUiAlpha(0f);
        SetAddUiAnchoredPosition(0f);
        activeCoroutine = StartCoroutine(PlayAddUiAnimationCoroutine());
    }

    private IEnumerator PlayAddUiAnimationCoroutine()
    {
        TMP_Text ui = targetUi;
        if (ui == null)
        {
            activeCoroutine = null;
            yield break;
        }

        float fadeIn = Mathf.Max(0f, fadeInSeconds);
        float fadeOut = Mathf.Max(0f, fadeOutSeconds);
        float hold;
        if (useExplicitHoldSeconds)
        {
            hold = Mathf.Max(0f, holdSeconds);
        }
        else
        {
            float total = Mathf.Max(0.01f, totalDisplaySeconds);
            total = Mathf.Max(total, fadeIn + fadeOut);
            hold = Mathf.Max(0f, total - fadeIn - fadeOut);
        }

        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeIn > 0f ? Mathf.Clamp01(elapsed / fadeIn) : 1f;
            SetAddUiAlpha(t);
            SetAddUiAnchoredPosition(0f);
            yield return null;
        }

        SetAddUiAlpha(1f);
        SetAddUiAnchoredPosition(0f);

        elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
            SetAddUiAlpha(1f - t);
            SetAddUiAnchoredPosition(t);
            yield return null;
        }

        ui.text = string.Empty;
        SetAddUiAlpha(0f);
        SetAddUiAnchoredPosition(0f);
        if (ui.gameObject.activeSelf)
        {
            ui.gameObject.SetActive(false);
        }

        activeCoroutine = null;
    }

    private void SetAddUiAlpha(float alpha)
    {
        if (targetUi == null)
        {
            return;
        }

        if (!hasBaseColor)
        {
            baseColor = targetUi.color;
            hasBaseColor = true;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);
        targetUi.color = color;
    }

    private void SetAddUiAnchoredPosition(float normalized)
    {
        if (targetUi == null)
        {
            return;
        }

        RectTransform rect = targetUi.rectTransform;
        if (!hasStartAnchoredPosition)
        {
            startAnchoredPosition = rect.anchoredPosition;
            hasStartAnchoredPosition = true;
        }

        float t = Mathf.Clamp01(normalized);
        float x = startAnchoredPosition.x + moveRightPixels * t;
        float y = startAnchoredPosition.y + moveUpPixels * t;
        rect.anchoredPosition = new Vector2(x, y);
    }
}
