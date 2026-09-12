using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AcceptedItemViewBounceController : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float effectStartSeconds = 0.3f;
    [SerializeField] private float repeatIntervalSeconds = 2.8f;

    [Header("Bounce")]
    [SerializeField] private float hopHeightPixels = 12f;
    [SerializeField] private float singleHopDurationSeconds = 0.12f;
    [SerializeField] private float hopGapSeconds = 0.04f;

    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private Vector2 initialAnchoredPosition;
    private Vector3 initialLocalScale;
    private Quaternion initialLocalRotation;
    private bool hasCapturedInitialTransform;
    private float elapsedSinceCarrierActive;
    private bool isCarrierActive;

    public void SetEffectStartOffsetSeconds(float offsetSeconds)
    {
        effectStartSeconds = Mathf.Max(0f, offsetSeconds);
    }

    public void SetCarrierActive(bool active)
    {
        isCarrierActive = active;
        if (!active)
        {
            elapsedSinceCarrierActive = 0f;
            ResetToInitialTransform();
            return;
        }

        CacheRefsIfNeeded();
        CaptureInitialTransformIfNeeded();
        ResetToInitialTransform();
        CaptureBasePosition();
        elapsedSinceCarrierActive = 0f;
        RestoreBasePosition();
    }

    private void Awake()
    {
        CacheRefsIfNeeded();
        CaptureInitialTransformIfNeeded();
        CaptureBasePosition();
        ResetToInitialTransform();
    }

    public void ForceResetToBaseTransform()
    {
        CacheRefsIfNeeded();
        CaptureInitialTransformIfNeeded();
        ResetToInitialTransform();
        CaptureBasePosition();
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
            RestoreBasePosition();
            return;
        }

        float safeRepeat = Mathf.Max(0.01f, repeatIntervalSeconds);
        float pulseTime = (elapsedSinceCarrierActive - Mathf.Max(0f, effectStartSeconds)) % safeRepeat;
        float yOffset = EvaluateDoubleHopOffset(pulseTime);
        rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(0f, yOffset);
    }

    private float EvaluateDoubleHopOffset(float pulseTime)
    {
        float safeHop = Mathf.Max(0.01f, singleHopDurationSeconds);
        float safeGap = Mathf.Max(0f, hopGapSeconds);
        float firstHopEnd = safeHop;
        float secondHopStart = firstHopEnd + safeGap;
        float secondHopEnd = secondHopStart + safeHop;
        float height = Mathf.Max(0f, hopHeightPixels);

        if (pulseTime < firstHopEnd)
        {
            float t = Mathf.Clamp01(pulseTime / safeHop);
            return Mathf.Sin(t * Mathf.PI) * height;
        }

        if (pulseTime >= secondHopStart && pulseTime < secondHopEnd)
        {
            float t = Mathf.Clamp01((pulseTime - secondHopStart) / safeHop);
            return Mathf.Sin(t * Mathf.PI) * height;
        }

        return 0f;
    }

    private bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
    }

    private void CacheRefsIfNeeded()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void CaptureBasePosition()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (hasCapturedInitialTransform)
        {
            baseAnchoredPosition = initialAnchoredPosition;
            return;
        }

        baseAnchoredPosition = rectTransform.anchoredPosition;
    }

    private void RestoreBasePosition()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = baseAnchoredPosition;
    }

    private void CaptureInitialTransformIfNeeded()
    {
        if (rectTransform == null || hasCapturedInitialTransform)
        {
            return;
        }

        initialAnchoredPosition = rectTransform.anchoredPosition;
        initialLocalScale = rectTransform.localScale;
        initialLocalRotation = rectTransform.localRotation;
        hasCapturedInitialTransform = true;
    }

    private void ResetToInitialTransform()
    {
        if (rectTransform == null || !hasCapturedInitialTransform)
        {
            return;
        }

        rectTransform.anchoredPosition = initialAnchoredPosition;
        rectTransform.localScale = initialLocalScale;
        rectTransform.localRotation = initialLocalRotation;
    }
}
