using UnityEngine;

[DisallowMultipleComponent]
public sealed class AcceptedItemViewBreathingScaleController : MonoBehaviour
{
    [Header("Breathing Scale")]
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float maxScaleMultiplier = 1.03f;
    [SerializeField] private float cycleSeconds = 2.5f;

    private RectTransform rectTransform;
    private Vector3 baseScale = Vector3.one;
    private float elapsed;
    private bool isCarrierActive;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        CaptureBaseScale();
        ApplyBaseScale();
    }

    private void OnEnable()
    {
        rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
        CaptureBaseScale();
        ApplyBaseScale();
    }

    public void SetCarrierActive(bool active)
    {
        isCarrierActive = active;
        if (!active)
        {
            elapsed = 0f;
            ApplyBaseScale();
        }
    }

    public void SetBreathingEnabled(bool enabled)
    {
        enableBreathing = enabled;
        if (!enabled)
        {
            ApplyBaseScale();
        }
    }

    public void SetBreathingScaleMultiplier(float multiplier)
    {
        maxScaleMultiplier = Mathf.Max(1f, multiplier);
    }

    public void SetBreathingCycleSeconds(float seconds)
    {
        cycleSeconds = Mathf.Max(0.01f, seconds);
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (!isCarrierActive || !enableBreathing || IsPaused())
        {
            if (!isCarrierActive || !enableBreathing)
            {
                ApplyBaseScale();
            }

            return;
        }

        elapsed += Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        float safeCycle = Mathf.Max(0.01f, cycleSeconds);
        float phase = (elapsed % safeCycle) / safeCycle;
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f); // 0..1
        float mul = Mathf.Lerp(1f, Mathf.Max(1f, maxScaleMultiplier), wave);
        rectTransform.localScale = baseScale * mul;
    }

    private void CaptureBaseScale()
    {
        if (rectTransform != null)
        {
            baseScale = rectTransform.localScale;
        }
    }

    private void ApplyBaseScale()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = baseScale;
        }
    }

    private static bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
    }
}
