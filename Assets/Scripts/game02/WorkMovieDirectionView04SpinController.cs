using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class WorkMovieDirectionView04SpinController : MonoBehaviour, IWorkStreamingDirectionView
{
    [Header("Stage Spin")]
    [SerializeField] private bool enableStageSpin = true;
    [SerializeField] private float initialRotationSpeedDegPerSec = 180f;
    [SerializeField] private float finalRotationSpeedDegPerSec = 45f;
    [SerializeField] private float stage1EndSeconds = 100f;
    [SerializeField] private float stage2EndSeconds = 180f;

    private Image image;
    private RectTransform rectTransform;
    private float baseZAngle;
    private float accumulatedRotation;
    private float currentElapsedSeconds;
    private bool isCarrierActive;

    public void Configure(
        bool enabled,
        float initialSpeedDegPerSec,
        float finalSpeedDegPerSec,
        float stage1EndSec,
        float stage2EndSec)
    {
        enableStageSpin = enabled;
        initialRotationSpeedDegPerSec = Mathf.Max(0f, initialSpeedDegPerSec);
        finalRotationSpeedDegPerSec = Mathf.Max(0f, finalSpeedDegPerSec);
        stage1EndSeconds = Mathf.Max(0.01f, stage1EndSec);
        stage2EndSeconds = Mathf.Max(stage1EndSeconds, stage2EndSec);
    }

    public void SetStageElapsedSeconds(float elapsedSeconds)
    {
        currentElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
    }

    public void SetCarrierActive(bool active)
    {
        CacheRefsIfNeeded();
        if (isCarrierActive == active)
        {
            return;
        }

        isCarrierActive = active;
        if (!active)
        {
            currentElapsedSeconds = 0f;
            accumulatedRotation = 0f;
            ApplyHiddenVisual();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyVisibleVisual();
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

        CacheRefsIfNeeded();
        ApplyVisibleVisual();

        if (!enableStageSpin || rectTransform == null)
        {
            return;
        }

        float dt = Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        if (dt <= 0f)
        {
            return;
        }

        float speed = ResolveRotationSpeedByElapsed(currentElapsedSeconds);
        accumulatedRotation += speed * dt;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, baseZAngle - accumulatedRotation);
    }

    private float ResolveRotationSpeedByElapsed(float elapsedSeconds)
    {
        float initial = Mathf.Max(0f, initialRotationSpeedDegPerSec);
        float final = Mathf.Max(0f, finalRotationSpeedDegPerSec);
        float s1 = Mathf.Max(0.01f, stage1EndSeconds);
        float s2 = Mathf.Max(s1, stage2EndSeconds);
        if (elapsedSeconds <= s1)
        {
            return initial;
        }

        if (elapsedSeconds >= s2)
        {
            return final;
        }

        float t = Mathf.InverseLerp(s1, s2, elapsedSeconds);
        return Mathf.Lerp(initial, final, t);
    }

    private static bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
    }

    private void CacheRefsIfNeeded()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (rectTransform == null)
        {
            rectTransform = image != null ? image.rectTransform : GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseZAngle = rectTransform.localEulerAngles.z;
            }
        }
    }

    private void ApplyHiddenVisual()
    {
        if (image != null)
        {
            Color c = image.color;
            c.a = 0f;
            image.color = c;
        }

        if (rectTransform != null)
        {
            rectTransform.localEulerAngles = new Vector3(0f, 0f, baseZAngle);
        }
    }

    private void ApplyVisibleVisual()
    {
        if (image != null)
        {
            Color c = image.color;
            c.a = 1f;
            image.color = c;
        }
    }
}
