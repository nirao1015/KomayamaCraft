using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public sealed class WorkMovieDirectionView02CommentBalloonController : MonoBehaviour, IWorkStreamingDirectionView
{
    private const int MaxActiveEffects = 5;

    [Header("Spawn Position")]
    [SerializeField] private float spawnOffsetMaxX = 20f;

    [Header("Movement")]
    [SerializeField] private float maxRiseHeight = 120f;
    [SerializeField] private float effectDurationSeconds = 3f;
    [SerializeField] private float maxSwayWidth = 12f;
    [SerializeField] private float swayStrength = 3.5f;
    [SerializeField] private float swayNoiseScale = 1.2f;
    [SerializeField] private float swayNoiseSpeedMin = 0.7f;
    [SerializeField] private float swayNoiseSpeedMax = 1.5f;

    [Header("Fade")]
    [SerializeField] private float fadeInSeconds = 0.4f;
    [SerializeField] private float fadeOutSeconds = 0.4f;

    [Header("Spawn Interval By Stage (random 0..interval)")]
    [SerializeField] private float stage1SpawnIntervalSeconds = 5f;
    [SerializeField] private float stage2SpawnIntervalSeconds = 12.5f;
    [SerializeField] private float stage3SpawnIntervalSeconds = 20f;
    [SerializeField] private float stage1EndSeconds = 100f;
    [SerializeField] private float stage2EndSeconds = 180f;

    [Header("Bright 12-Color Palette (includes white)")]
    [SerializeField] private Color[] colorPalette = new Color[]
    {
        new Color(1f, 1f, 1f, 1f),           // #FFFFFF
        new Color(0.918f, 0.969f, 1f, 1f),   // #EAF7FF
        new Color(0.851f, 0.953f, 1f, 1f),   // #D9F3FF
        new Color(0.78f, 0.933f, 1f, 1f),    // #C7EEFF
        new Color(0.71f, 0.91f, 1f, 1f),     // #B5E8FF
        new Color(0.639f, 0.886f, 1f, 1f),   // #A3E2FF
        new Color(0.839f, 1f, 0.918f, 1f),   // #D6FFEA
        new Color(1f, 0.969f, 0.8f, 1f),     // #FFF7CC
        new Color(1f, 0.902f, 0.722f, 1f),   // #FFE6B8
        new Color(1f, 0.827f, 0.761f, 1f),   // #FFD3C2
        new Color(0.965f, 0.847f, 1f, 1f),   // #F6D8FF
        new Color(0.886f, 0.863f, 1f, 1f),   // #E2DCFF
    };

    private sealed class ActiveBalloon
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image image;
        public float elapsedSeconds;
        public float startX;
        public float startY;
        public float noiseSeed;
        public float noiseSpeed;
        public Color baseColor;
    }

    private readonly List<ActiveBalloon> active = new List<ActiveBalloon>(MaxActiveEffects);
    private Image sourceImage;
    private RectTransform sourceRectTransform;
    private float elapsedStageSeconds;
    private float spawnDelaySeconds;
    private float spawnAccumulator;
    private bool isCarrierActive;

    public void SetStageElapsedSeconds(float elapsedSeconds)
    {
        elapsedStageSeconds = Mathf.Max(0f, elapsedSeconds);
    }

    public void SetCarrierActive(bool activeState)
    {
        CacheRefsIfNeeded();
        if (isCarrierActive == activeState)
        {
            return;
        }

        isCarrierActive = activeState;
        if (!activeState)
        {
            spawnAccumulator = 0f;
            spawnDelaySeconds = 0f;
            DestroyAllEffects();
            ApplySourceHidden();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplySourceHidden();
        spawnAccumulator = 0f;
        spawnDelaySeconds = ResolveRandomSpawnDelayByStage(elapsedStageSeconds);
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

        spawnAccumulator += dt;
        if (spawnAccumulator >= Mathf.Max(0.01f, spawnDelaySeconds))
        {
            spawnAccumulator = 0f;
            spawnDelaySeconds = ResolveRandomSpawnDelayByStage(elapsedStageSeconds);
            TrySpawnOne();
        }

        float duration = Mathf.Max(0.01f, effectDurationSeconds);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ActiveBalloon b = active[i];
            if (b == null || b.gameObject == null || b.image == null || b.rectTransform == null)
            {
                active.RemoveAt(i);
                continue;
            }

            b.elapsedSeconds += dt;
            float t = Mathf.Clamp01(b.elapsedSeconds / duration);

            float y = b.startY + Mathf.Lerp(0f, Mathf.Max(0f, maxRiseHeight), t);
            float phase = b.elapsedSeconds * Mathf.Max(0.01f, b.noiseSpeed);
            float noise = Mathf.PerlinNoise(b.noiseSeed, phase * Mathf.Max(0.01f, swayNoiseScale)) * 2f - 1f;
            float sway = noise * Mathf.Max(0f, swayStrength);
            float x = Mathf.Clamp(b.startX + sway, b.startX - Mathf.Max(0f, maxSwayWidth), b.startX + Mathf.Max(0f, maxSwayWidth));
            b.rectTransform.anchoredPosition = new Vector2(x, y);

            float alpha = EvaluateAlpha(b.elapsedSeconds, duration);
            Color c = b.baseColor;
            c.a *= alpha;
            b.image.color = c;

            if (b.elapsedSeconds >= duration)
            {
                Destroy(b.gameObject);
                active.RemoveAt(i);
            }
        }
    }

    private void TrySpawnOne()
    {
        if (active.Count >= MaxActiveEffects)
        {
            return;
        }

        if (sourceImage == null || sourceRectTransform == null || sourceRectTransform.parent == null)
        {
            return;
        }

        GameObject go = new GameObject("DirectionView02_CommentFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(sourceRectTransform.parent, false);
        rect.localScale = sourceRectTransform.localScale;
        rect.localRotation = sourceRectTransform.localRotation;
        rect.anchorMin = sourceRectTransform.anchorMin;
        rect.anchorMax = sourceRectTransform.anchorMax;
        rect.pivot = sourceRectTransform.pivot;
        rect.sizeDelta = sourceRectTransform.sizeDelta;
        rect.SetSiblingIndex(sourceRectTransform.GetSiblingIndex());

        float startOffsetX = Random.Range(0f, Mathf.Max(0f, spawnOffsetMaxX));
        float startX = sourceRectTransform.anchoredPosition.x + startOffsetX;
        float startY = sourceRectTransform.anchoredPosition.y;
        rect.anchoredPosition = new Vector2(startX, startY);

        Image img = go.GetComponent<Image>();
        img.sprite = sourceImage.sprite;
        img.material = sourceImage.material;
        img.type = sourceImage.type;
        img.preserveAspect = sourceImage.preserveAspect;
        img.maskable = sourceImage.maskable;
        img.raycastTarget = false;

        Color selected = ResolveRandomPaletteColor();
        selected.a = 0f;
        img.color = selected;

        ActiveBalloon b = new ActiveBalloon
        {
            gameObject = go,
            rectTransform = rect,
            image = img,
            elapsedSeconds = 0f,
            startX = startX,
            startY = startY,
            noiseSeed = Random.Range(0f, 1000f),
            noiseSpeed = Random.Range(Mathf.Min(swayNoiseSpeedMin, swayNoiseSpeedMax), Mathf.Max(swayNoiseSpeedMin, swayNoiseSpeedMax)),
            baseColor = new Color(selected.r, selected.g, selected.b, 1f)
        };
        active.Add(b);
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

    private float ResolveRandomSpawnDelayByStage(float elapsedSeconds)
    {
        float interval = ResolveBaseSpawnIntervalByStage(elapsedSeconds);
        float r = Random.Range(0f, Mathf.Max(0f, interval));
        return Mathf.Max(0.01f, r);
    }

    private float ResolveBaseSpawnIntervalByStage(float elapsedSeconds)
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

    private Color ResolveRandomPaletteColor()
    {
        if (colorPalette == null || colorPalette.Length == 0)
        {
            return Color.white;
        }

        int idx = Random.Range(0, colorPalette.Length);
        return colorPalette[idx];
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

    private void DestroyAllEffects()
    {
        for (int i = 0; i < active.Count; i++)
        {
            ActiveBalloon b = active[i];
            if (b != null && b.gameObject != null)
            {
                Destroy(b.gameObject);
            }
        }

        active.Clear();
    }
}
