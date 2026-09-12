using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DirectionView04Controller : MonoBehaviour, IWorkStreamingDirectionView
{
    [Header("Timing")]
    [SerializeField] private float effectStartSeconds = 0.6f;
    [SerializeField] private float fadeInSeconds = 0.8f;
    [SerializeField] private float repeatIntervalSeconds = 2.4f;
    [SerializeField] private float pulseDurationSeconds = 0.48f;

    [Header("Scale")]
    [SerializeField] private float effectScaleMultiplier = 1.18f;
    [SerializeField] private float pulseContractMultiplier = 1.0f;

    private Image image;
    private Vector3 baseScale = Vector3.one;
    private float elapsedSinceCarrierActive;
    private bool isCarrierActive;

    public void SetEffectStartOffsetSeconds(float offsetSeconds)
    {
        effectStartSeconds = Mathf.Max(0f, offsetSeconds);
    }

    public void SetDisplaySprite(Sprite sprite)
    {
        CacheRefsIfNeeded();
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.preserveAspect = true;
    }

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

        float tAfterStart = elapsedSinceCarrierActive - Mathf.Max(0f, effectStartSeconds);
        float fadeT = Mathf.Clamp01(tAfterStart / Mathf.Max(0.01f, fadeInSeconds));
        float alpha = fadeT;
        float scaleMultiplier = 1f;
        if (fadeT >= 1f)
        {
            scaleMultiplier = EvaluatePulseScale(tAfterStart - Mathf.Max(0.01f, fadeInSeconds));
            alpha = 1f;
        }

        ApplyVisual(alpha, scaleMultiplier);
    }

    private float EvaluatePulseScale(float afterFadeTime)
    {
        float safeRepeat = Mathf.Max(0.01f, repeatIntervalSeconds);
        float safePulseDuration = Mathf.Clamp(pulseDurationSeconds, 0.01f, safeRepeat);
        float localPulse = afterFadeTime % safeRepeat;
        if (localPulse >= safePulseDuration)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(localPulse / safePulseDuration);
        float contract = Mathf.Clamp(pulseContractMultiplier, 0.01f, 1f);
        float expand = Mathf.Max(1f, effectScaleMultiplier);
        if (t < 1f / 3f)
        {
            return Mathf.LerpUnclamped(1f, contract, t * 3f);
        }

        if (t < 2f / 3f)
        {
            return Mathf.LerpUnclamped(contract, expand, (t - (1f / 3f)) * 3f);
        }

        return Mathf.LerpUnclamped(expand, 1f, (t - (2f / 3f)) * 3f);
    }

    private bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
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
