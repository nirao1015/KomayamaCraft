using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public abstract class DirectionViewLoopPulseBase : MonoBehaviour, IWorkStreamingDirectionView
{
    [Header("Timing")]
    [SerializeField] protected float effectStartSeconds = 0.2f;
    [SerializeField] protected float fadeInSeconds = 0.4f;
    [SerializeField] protected float holdSeconds = 1.2f;
    [SerializeField] protected float fadeOutSeconds = 0.4f;
    [SerializeField] protected float repeatIntervalSeconds = 2.6f;

    [Header("Scale")]
    [SerializeField] protected float effectScaleMultiplier = 1.15f;

    private Image image;
    private Vector3 baseScale = Vector3.one;
    private float elapsedSinceCarrierActive;
    private bool isCarrierActive;

    public void SetCarrierActive(bool active)
    {
        isCarrierActive = active;
        if (!active)
        {
            elapsedSinceCarrierActive = 0f;
            ApplyHiddenVisual();
            gameObject.SetActive(false);
            return;
        }

        CacheRefsIfNeeded();
        elapsedSinceCarrierActive = 0f;
        gameObject.SetActive(true);
        ApplyHiddenVisual();
    }

    public void SetEffectStartOffsetSeconds(float offsetSeconds)
    {
        effectStartSeconds = Mathf.Max(0f, offsetSeconds);
    }

    private void Awake()
    {
        CacheRefsIfNeeded();
        ApplyHiddenVisual();
    }

    private void Update()
    {
        if (!isCarrierActive || IsPaused())
        {
            return;
        }

        elapsedSinceCarrierActive += Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        if (elapsedSinceCarrierActive < Mathf.Max(0f, effectStartSeconds))
        {
            ApplyHiddenVisual();
            return;
        }

        float visibleDuration = Mathf.Max(0.01f, fadeInSeconds) + Mathf.Max(0f, holdSeconds) + Mathf.Max(0.01f, fadeOutSeconds);
        float loopDuration = visibleDuration + Mathf.Max(0f, repeatIntervalSeconds);
        float localTime = (elapsedSinceCarrierActive - Mathf.Max(0f, effectStartSeconds)) % Mathf.Max(0.01f, loopDuration);
        if (localTime > visibleDuration)
        {
            ApplyHiddenVisual();
            return;
        }

        float alpha = EvaluateAlpha(localTime);
        float progress = Mathf.Clamp01(localTime / visibleDuration);
        float scaleMultiplier = Mathf.LerpUnclamped(1f, Mathf.Max(1f, effectScaleMultiplier), progress);
        ApplyVisual(alpha, scaleMultiplier);
    }

    private float EvaluateAlpha(float localTime)
    {
        float safeFadeIn = Mathf.Max(0.01f, fadeInSeconds);
        float safeHold = Mathf.Max(0f, holdSeconds);
        float safeFadeOut = Mathf.Max(0.01f, fadeOutSeconds);

        if (localTime < safeFadeIn)
        {
            return Mathf.Clamp01(localTime / safeFadeIn);
        }

        localTime -= safeFadeIn;
        if (localTime < safeHold)
        {
            return 1f;
        }

        localTime -= safeHold;
        return 1f - Mathf.Clamp01(localTime / safeFadeOut);
    }

    private void CacheRefsIfNeeded()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image != null)
        {
            baseScale = image.rectTransform.localScale;
        }
    }

    private bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
    }

    private void ApplyHiddenVisual()
    {
        ApplyVisual(0f, 1f);
    }

    private void ApplyVisual(float alpha, float scaleMultiplier)
    {
        if (image == null)
        {
            return;
        }

        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
        image.rectTransform.localScale = baseScale * Mathf.Max(0.01f, scaleMultiplier);
    }
}
