using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class WorkMovieDirectionView01RiseArrowController : MonoBehaviour, IWorkStreamingDirectionView
{
    [Header("Rise Arrow")]
    [SerializeField] private bool enableRiseArrow = true;
    [SerializeField] private Vector2 moveDistance = new Vector2(70f, 50f);
    [SerializeField] private float moveDurationSeconds = 1.2f;
    [SerializeField] private float fadeInSeconds = 0.2f;
    [SerializeField] private float fadeOutSeconds = 0.4f;

    [Header("Spawn Interval By Stage")]
    [SerializeField] private float stage1SpawnIntervalSeconds = 5f;
    [SerializeField] private float stage2SpawnIntervalSeconds = 12.5f;
    [SerializeField] private float stage3SpawnIntervalSeconds = 20f;
    [SerializeField] private float stage1EndSeconds = 100f;
    [SerializeField] private float stage2EndSeconds = 180f;

    [Header("Color By Stage")]
    [SerializeField] private Color stage1Color = new Color(0f, 0.898f, 1f, 1f);   // #00E5FF
    [SerializeField] private Color stage2Color = new Color(1f, 0.761f, 0.278f, 1f); // #FFC247
    [SerializeField] private Color stage3Color = new Color(0.435f, 0.525f, 0.659f, 1f); // #6F86A8

    private sealed class ActiveArrow
    {
        public GameObject GameObject;
        public RectTransform RectTransform;
        public Image Image;
        public Vector2 StartAnchoredPosition;
        public Vector2 EndAnchoredPosition;
        public Color BaseColor;
        public float ElapsedSeconds;
    }

    private readonly List<ActiveArrow> activeArrows = new List<ActiveArrow>(16);
    private Image sourceImage;
    private RectTransform sourceRectTransform;
    private float spawnAccumulator;
    private float currentElapsedSeconds;
    private bool isCarrierActive;

    public void Configure(
        bool enabled,
        Vector2 configuredMoveDistance,
        float configuredMoveDurationSeconds,
        float configuredFadeInSeconds,
        float configuredFadeOutSeconds,
        float configuredStage1SpawnIntervalSeconds,
        float configuredStage2SpawnIntervalSeconds,
        float configuredStage3SpawnIntervalSeconds,
        float configuredStage1EndSeconds,
        float configuredStage2EndSeconds,
        Color configuredStage1Color,
        Color configuredStage2Color,
        Color configuredStage3Color)
    {
        enableRiseArrow = enabled;
        moveDistance = configuredMoveDistance;
        moveDurationSeconds = Mathf.Max(0.01f, configuredMoveDurationSeconds);
        fadeInSeconds = Mathf.Max(0f, configuredFadeInSeconds);
        fadeOutSeconds = Mathf.Max(0f, configuredFadeOutSeconds);
        stage1SpawnIntervalSeconds = Mathf.Max(0.01f, configuredStage1SpawnIntervalSeconds);
        stage2SpawnIntervalSeconds = Mathf.Max(0.01f, configuredStage2SpawnIntervalSeconds);
        stage3SpawnIntervalSeconds = Mathf.Max(0.01f, configuredStage3SpawnIntervalSeconds);
        stage1EndSeconds = Mathf.Max(0.01f, configuredStage1EndSeconds);
        stage2EndSeconds = Mathf.Max(stage1EndSeconds, configuredStage2EndSeconds);
        stage1Color = configuredStage1Color;
        stage2Color = configuredStage2Color;
        stage3Color = configuredStage3Color;
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
            spawnAccumulator = 0f;
            DestroyAllActiveArrows();
            ApplySourceHidden();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplySourceHidden();
        spawnAccumulator = ResolveSpawnIntervalByElapsed(currentElapsedSeconds);
    }

    private void Awake()
    {
        CacheRefsIfNeeded();
        ApplySourceHidden();
    }

    private void Update()
    {
        if (!isCarrierActive || IsPaused())
        {
            return;
        }

        float dt = Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        if (dt <= 0f)
        {
            return;
        }

        if (enableRiseArrow)
        {
            spawnAccumulator += dt;
            float interval = ResolveSpawnIntervalByElapsed(currentElapsedSeconds);
            int guard = 0;
            while (spawnAccumulator >= interval && guard < 10)
            {
                spawnAccumulator -= interval;
                SpawnArrowInstance();
                interval = ResolveSpawnIntervalByElapsed(currentElapsedSeconds);
                guard++;
            }
        }

        for (int i = activeArrows.Count - 1; i >= 0; i--)
        {
            ActiveArrow arrow = activeArrows[i];
            if (arrow == null || arrow.GameObject == null || arrow.Image == null || arrow.RectTransform == null)
            {
                activeArrows.RemoveAt(i);
                continue;
            }

            arrow.ElapsedSeconds += dt;
            float duration = Mathf.Max(0.01f, moveDurationSeconds);
            float t = Mathf.Clamp01(arrow.ElapsedSeconds / duration);
            arrow.RectTransform.anchoredPosition = Vector2.LerpUnclamped(arrow.StartAnchoredPosition, arrow.EndAnchoredPosition, t);

            float alpha = EvaluateAlpha(arrow.ElapsedSeconds, duration);
            Color c = arrow.BaseColor;
            c.a *= alpha;
            arrow.Image.color = c;

            if (arrow.ElapsedSeconds >= duration)
            {
                Destroy(arrow.GameObject);
                activeArrows.RemoveAt(i);
            }
        }
    }

    private void SpawnArrowInstance()
    {
        if (sourceImage == null || sourceRectTransform == null || sourceRectTransform.parent == null)
        {
            return;
        }

        GameObject go = new GameObject("DirectionView01_RiseArrowFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(sourceRectTransform.parent, false);
        rect.localScale = sourceRectTransform.localScale;
        rect.localRotation = sourceRectTransform.localRotation;
        rect.anchorMin = sourceRectTransform.anchorMin;
        rect.anchorMax = sourceRectTransform.anchorMax;
        rect.pivot = sourceRectTransform.pivot;
        rect.sizeDelta = sourceRectTransform.sizeDelta;
        rect.anchoredPosition = sourceRectTransform.anchoredPosition;
        rect.SetSiblingIndex(sourceRectTransform.GetSiblingIndex());

        Image image = go.GetComponent<Image>();
        image.sprite = sourceImage.sprite;
        image.material = sourceImage.material;
        image.type = sourceImage.type;
        image.preserveAspect = sourceImage.preserveAspect;
        image.maskable = sourceImage.maskable;
        image.raycastTarget = false;

        ActiveArrow arrow = new ActiveArrow
        {
            GameObject = go,
            RectTransform = rect,
            Image = image,
            StartAnchoredPosition = sourceRectTransform.anchoredPosition,
            EndAnchoredPosition = sourceRectTransform.anchoredPosition + moveDistance,
            BaseColor = ResolveStageColorByElapsed(currentElapsedSeconds),
            ElapsedSeconds = 0f
        };
        activeArrows.Add(arrow);
    }

    private float EvaluateAlpha(float elapsedSeconds, float durationSeconds)
    {
        float fadeIn = Mathf.Max(0f, fadeInSeconds);
        float fadeOut = Mathf.Max(0f, fadeOutSeconds);
        float alpha = 1f;
        if (fadeIn > 0f && elapsedSeconds < fadeIn)
        {
            alpha = Mathf.Clamp01(elapsedSeconds / fadeIn);
        }

        float fadeOutStart = Mathf.Max(0f, durationSeconds - fadeOut);
        if (fadeOut > 0f && elapsedSeconds > fadeOutStart)
        {
            float t = Mathf.Clamp01((durationSeconds - elapsedSeconds) / fadeOut);
            alpha = Mathf.Min(alpha, t);
        }

        return Mathf.Clamp01(alpha);
    }

    private float ResolveSpawnIntervalByElapsed(float elapsedSeconds)
    {
        if (elapsedSeconds <= stage1EndSeconds)
        {
            return Mathf.Max(0.01f, stage1SpawnIntervalSeconds);
        }

        if (elapsedSeconds <= stage2EndSeconds)
        {
            return Mathf.Max(0.01f, stage2SpawnIntervalSeconds);
        }

        return Mathf.Max(0.01f, stage3SpawnIntervalSeconds);
    }

    private Color ResolveStageColorByElapsed(float elapsedSeconds)
    {
        if (elapsedSeconds <= stage1EndSeconds)
        {
            return stage1Color;
        }

        if (elapsedSeconds <= stage2EndSeconds)
        {
            return stage2Color;
        }

        return stage3Color;
    }

    private static bool IsPaused()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.IsPaused;
    }

    private void CacheRefsIfNeeded()
    {
        if (sourceImage == null)
        {
            sourceImage = GetComponent<Image>();
        }

        if (sourceRectTransform == null)
        {
            sourceRectTransform = sourceImage != null ? sourceImage.rectTransform : GetComponent<RectTransform>();
        }
    }

    private void ApplySourceHidden()
    {
        if (sourceImage == null)
        {
            return;
        }

        Color c = sourceImage.color;
        c.a = 0f;
        sourceImage.color = c;
    }

    private void DestroyAllActiveArrows()
    {
        for (int i = 0; i < activeArrows.Count; i++)
        {
            ActiveArrow arrow = activeArrows[i];
            if (arrow != null && arrow.GameObject != null)
            {
                Destroy(arrow.GameObject);
            }
        }

        activeArrows.Clear();
    }
}
