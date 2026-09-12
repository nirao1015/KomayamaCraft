using UnityEngine;

/// <summary>
/// バズ連動で再生する拡張系エフェクト（BuzzEffect02）。
/// 複数配置を想定し、同一スロット内でコピーして使える。
/// </summary>
public sealed class WorkMovieBuzzEffect02Controller : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] private bool enableBuzzEffect02 = true;
    [SerializeField] private bool deactivateWhenNotBuzz = true;

    [Header("Timing")]
    [SerializeField] private float startDelaySeconds = 0.5f;
    [SerializeField] private float effectDurationSeconds = 0.7f;
    [SerializeField] private float retriggerIntervalSeconds = 1.0f;

    [Header("Shape")]
    [SerializeField] private float startScale = 1.0f;
    [SerializeField] private float maxScale = 1.75f;
    [SerializeField] private float fadeInRatio = 0.2f;

    [Header("Refs (Optional)")]
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private CanvasGroup targetCanvasGroup;

    private bool isBuzzActive;
    private bool isStopRequested;
    private bool isCyclePlaying;
    private float cycleElapsedSeconds;
    private float delayRemainingSeconds;
    private float intervalRemainingSeconds;

    private void Awake()
    {
        ResolveRefsIfNeeded();
        ResetVisual();
    }

    public void SetBuzzState(bool buzzActive, bool stopRequested)
    {
        ResolveRefsIfNeeded();
        bool wasActive = isBuzzActive;
        isBuzzActive = buzzActive && enableBuzzEffect02;
        isStopRequested = stopRequested;

        if (!isBuzzActive)
        {
            isCyclePlaying = false;
            cycleElapsedSeconds = 0f;
            delayRemainingSeconds = 0f;
            intervalRemainingSeconds = 0f;
            ResetVisual();
            SetTargetActive(!deactivateWhenNotBuzz);
            return;
        }

        SetTargetActive(true);
        if (!wasActive && isBuzzActive)
        {
            BeginDelay();
        }
    }

    public void ManualTick(float deltaSeconds)
    {
        if (!enableBuzzEffect02)
        {
            ResetVisual();
            SetTargetActive(false);
            return;
        }

        if (!isBuzzActive)
        {
            return;
        }

        float dt = Mathf.Max(0f, deltaSeconds);
        if (dt <= 0f)
        {
            return;
        }

        if (isCyclePlaying)
        {
            UpdatePlayingCycle(dt);
            return;
        }

        if (delayRemainingSeconds > 0f)
        {
            delayRemainingSeconds = Mathf.Max(0f, delayRemainingSeconds - dt);
            if (delayRemainingSeconds <= 0f)
            {
                StartCycle();
            }

            return;
        }

        if (intervalRemainingSeconds > 0f)
        {
            intervalRemainingSeconds = Mathf.Max(0f, intervalRemainingSeconds - dt);
            if (intervalRemainingSeconds <= 0f && !isStopRequested)
            {
                StartCycle();
            }
        }
    }

    public void ResetVisual()
    {
        ResolveRefsIfNeeded();
        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = 0f;
        }

        if (targetRect != null)
        {
            targetRect.localScale = Vector3.one * Mathf.Max(0.01f, startScale);
        }
    }

    private void UpdatePlayingCycle(float dt)
    {
        cycleElapsedSeconds += dt;
        float duration = Mathf.Max(0.01f, effectDurationSeconds);
        float t = Mathf.Clamp01(cycleElapsedSeconds / duration);

        // 立ち上がりはフェードイン、拡大は最初速く終盤で減速（EaseOut）。
        float fadeInT = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeInRatio));
        float easedScale = 1f - Mathf.Pow(1f - t, 3f);

        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = fadeInT;
        }

        if (targetRect != null)
        {
            float s = Mathf.Lerp(
                Mathf.Max(0.01f, startScale),
                Mathf.Max(startScale, maxScale),
                easedScale);
            targetRect.localScale = Vector3.one * s;
        }

        if (cycleElapsedSeconds < duration)
        {
            return;
        }

        isCyclePlaying = false;
        cycleElapsedSeconds = 0f;
        ResetVisual();

        if (isStopRequested)
        {
            isBuzzActive = false;
            SetTargetActive(!deactivateWhenNotBuzz);
            return;
        }

        intervalRemainingSeconds = Mathf.Max(0f, retriggerIntervalSeconds);
    }

    private void BeginDelay()
    {
        isCyclePlaying = false;
        cycleElapsedSeconds = 0f;
        intervalRemainingSeconds = 0f;
        delayRemainingSeconds = Mathf.Max(0f, startDelaySeconds);
        ResetVisual();
        if (delayRemainingSeconds <= 0f)
        {
            StartCycle();
        }
    }

    private void StartCycle()
    {
        if (!isBuzzActive)
        {
            return;
        }

        delayRemainingSeconds = 0f;
        intervalRemainingSeconds = 0f;
        cycleElapsedSeconds = 0f;
        isCyclePlaying = true;
    }

    private void ResolveRefsIfNeeded()
    {
        if (targetRoot == null)
        {
            targetRoot = gameObject;
        }

        if (targetRect == null)
        {
            targetRect = targetRoot.GetComponent<RectTransform>();
        }

        if (targetCanvasGroup == null)
        {
            targetCanvasGroup = targetRoot.GetComponent<CanvasGroup>();
            if (targetCanvasGroup == null)
            {
                targetCanvasGroup = targetRoot.AddComponent<CanvasGroup>();
            }
        }
    }

    private void SetTargetActive(bool active)
    {
        if (targetRoot == null)
        {
            return;
        }

        if (targetRoot.activeSelf != active)
        {
            targetRoot.SetActive(active);
        }
    }
}
