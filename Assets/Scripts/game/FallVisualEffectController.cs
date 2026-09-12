using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 落下感の演出: 画面端の速度線（画像不要）と、中央から放射する塵パーティクル（当たりなし）。
/// </summary>
[DisallowMultipleComponent]
public class FallVisualEffectController : MonoBehaviour
{
    [Header("開始条件")]
    [SerializeField] private bool playOnlyAfterControlStart = true;
    [SerializeField] private Game01Manager gameManager;

    [Header("参照（未設定時は自動）")]
    [SerializeField] private Canvas targetRootCanvas;

    [Header("速度線（UI・画像不要）")]
    [SerializeField] private bool enableSpeedLines = true;
    [SerializeField] private int linesPerSide = 10;
    [SerializeField] private Vector2 lineWidthPixels = new Vector2(2f, 5f);
    [SerializeField] private Vector2 lineLengthPixels = new Vector2(40f, 140f);
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField] private Vector2 lineSpeedPixelsPerSec = new Vector2(220f, 520f);
    [SerializeField, Range(0.5f, 0.95f)] private float lineSpawnRadiusRatio = 0.8f;
    [SerializeField] private Vector2 lineLifetimeSeconds = new Vector2(0.35f, 0.8f);
    [SerializeField, Range(0.05f, 0.8f)] private float lineFadeInPortion = 0.3f;
    [SerializeField] private float pulseSpeed = 2.6f;
    [SerializeField] private float pulseAlphaAmplitude = 0.12f;

    [Header("塵パーティクル（当たりなし・ワールド）")]
    [SerializeField] private bool enableDustParticles = true;
    [SerializeField] private Transform dustSpawnOverride;
    [SerializeField] private bool useNoEntryZoneRing = true;
    [SerializeField] private CircleCollider2D noEntryZoneCollider;
    [SerializeField] private string noEntryZoneObjectName = "no_entryzoone";
    [SerializeField] private Vector2 dustRingOffsetRange = new Vector2(-0.08f, 0.12f);
    [SerializeField] private int maxParticles = 180;
    [SerializeField] private float dustEmissionRate = 30f;
    [SerializeField] private float dustSpawnRadius = 0.35f;
    [SerializeField] private float dustSpeedMin = 0.8f;
    [SerializeField] private float dustSpeedMax = 3.2f;
    [SerializeField] private float dustLifetimeMin = 0.6f;
    [SerializeField] private float dustLifetimeMax = 1.8f;
    [SerializeField] private float dustSizeMin = 0.03f;
    [SerializeField] private float dustSizeMax = 0.09f;
    [SerializeField] private Color dustTint = new Color(1f, 0.97f, 0.88f, 0.45f);
    [Tooltip("ビルドで Shader.Find が失敗する場合に設定。未設定ならシェーダー名を順に試す。")]
    [SerializeField] private Material dustParticleTemplateMaterial;
    [SerializeField] private int particleSortingOrder = -45;

    private static readonly string[] s_DustParticleShaderCandidates =
    {
        "Universal Render Pipeline/Particles/Unlit",
        "Universal Render Pipeline/Particles/Simple Unlit",
        "Particles/Standard Unlit",
        "Particles/Alpha Blended",
        "Legacy Shaders/Particles/Alpha Blended",
        "Sprites/Default",
    };

    private struct SpeedLine
    {
        public RectTransform rect;
        public Image image;
        public float speed;
        public Vector2 direction;
        public float age;
        public float lifetime;
        public float phase;
    }

    private static Sprite s_whiteSprite;
    private RectTransform speedLinesRoot;
    private readonly List<SpeedLine> speedLines = new List<SpeedLine>();
    private ParticleSystem dustSystem;
    private float dustEmitAccumulator;
    private bool visualsRunning;
    private bool stageCleared;

    public void HandleStageClear()
    {
        stageCleared = true;
        visualsRunning = false;

        if (speedLinesRoot != null)
        {
            speedLinesRoot.gameObject.SetActive(false);
        }

        if (dustSystem != null)
        {
            dustSystem.Pause(true);
        }
    }

    private void Awake()
    {
        ResolveReferences();

        if (enableSpeedLines && targetRootCanvas != null)
        {
            BuildSpeedLines();
        }

        if (enableDustParticles)
        {
            BuildDustSystem();
        }

        ApplyVisualState(false);
    }

    private void Update()
    {
        bool shouldRun = ShouldRunEffects();
        if (shouldRun != visualsRunning)
        {
            ApplyVisualState(shouldRun);
        }

        if (!visualsRunning)
        {
            return;
        }

        UpdateSpeedLines(Time.unscaledDeltaTime);
        UpdateDustEmission(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (dustSystem != null)
        {
            Destroy(dustSystem.gameObject);
        }

        if (speedLinesRoot != null)
        {
            Destroy(speedLinesRoot.gameObject);
        }
    }

    private void ResolveReferences()
    {
        // Inspector 設定前提。自動探索は行わない。
    }

    private void ResolveNoEntryZoneReference()
    {
        // Inspector 設定前提。自動探索は行わない。
    }

    private bool ShouldRunEffects()
    {
        if (stageCleared)
        {
            return false;
        }

        if (!playOnlyAfterControlStart)
        {
            return true;
        }

        if (gameManager == null)
        {
            return true;
        }

        if (!gameManager.WaitForStageIntro)
        {
            return true;
        }

        return gameManager.IsStageIntroComplete;
    }

    private void ApplyVisualState(bool run)
    {
        if (stageCleared)
        {
            return;
        }

        visualsRunning = run;

        if (speedLinesRoot != null)
        {
            speedLinesRoot.gameObject.SetActive(run && enableSpeedLines);
        }

        if (dustSystem != null)
        {
            if (run && enableDustParticles)
            {
                dustSystem.Play();
                ResetSpeedLines();
            }
            else
            {
                dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                dustEmitAccumulator = 0f;
            }
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
        {
            return s_whiteSprite;
        }

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }

    private void BuildSpeedLines()
    {
        GameObject rootGo = new GameObject("SpeedLinesOverlay");
        RectTransform root = rootGo.AddComponent<RectTransform>();
        root.SetParent(targetRootCanvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.SetAsFirstSibling();
        speedLinesRoot = root;

        int lineCount = Mathf.Max(2, linesPerSide * 2);
        for (int i = 0; i < lineCount; i++)
        {
            GameObject lineGo = new GameObject($"SpeedLine_{i}");
            RectTransform rt = lineGo.AddComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;

            Image img = lineGo.AddComponent<Image>();
            img.sprite = GetWhiteSprite();
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            SpeedLine line = new SpeedLine
            {
                rect = rt,
                image = img,
                phase = Random.Range(0f, Mathf.PI * 2f)
            };
            RespawnLine(ref line, true);
            speedLines.Add(line);
        }
    }

    private void UpdateSpeedLines(float dt)
    {
        if (speedLines.Count == 0 || speedLinesRoot == null)
        {
            return;
        }

        float halfWidth = Mathf.Max(1f, speedLinesRoot.rect.width * 0.5f);
        float halfHeight = Mathf.Max(1f, speedLinesRoot.rect.height * 0.5f);
        float t = Time.unscaledTime * pulseSpeed;

        for (int i = 0; i < speedLines.Count; i++)
        {
            SpeedLine line = speedLines[i];
            line.age += dt;
            Vector2 pos = line.rect.anchoredPosition + (line.direction * line.speed * dt);
            line.rect.anchoredPosition = pos;

            bool outOfBounds = Mathf.Abs(pos.x) > (halfWidth + 50f) || Mathf.Abs(pos.y) > (halfHeight + 50f);
            if (line.age >= line.lifetime || outOfBounds)
            {
                RespawnLine(ref line, false);
            }

            float progress = Mathf.Clamp01(line.age / Mathf.Max(0.01f, line.lifetime));
            float fadeInPortion = Mathf.Clamp(lineFadeInPortion, 0.05f, 0.95f);
            float alphaProgress = progress < fadeInPortion
                ? (progress / fadeInPortion)
                : (1f - ((progress - fadeInPortion) / (1f - fadeInPortion)));
            float pulse = 1f + (Mathf.Sin(t + line.phase) * pulseAlphaAmplitude);
            float a = Mathf.Clamp01(lineColor.a * alphaProgress * pulse);
            Color c = lineColor;
            c.a = a;
            line.image.color = c;
            speedLines[i] = line;
        }
    }

    private void BuildDustSystem()
    {
        GameObject go = new GameObject("RadialDustParticles");
        go.transform.SetParent(transform, false);
        go.transform.position = GetDustOrigin();

        dustSystem = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dustSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.startSpeed = 0f;
        main.startSize = 0.05f;
        main.startLifetime = 1f;
        main.startColor = dustTint;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = dustSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = dustSystem.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule col = dustSystem.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.98f, 0.9f), 0f),
                new GradientColorKey(new Color(1f, 0.98f, 0.9f), 1f)
            },
            new GradientAlphaKey[] { new GradientAlphaKey(dustTint.a, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer renderer = dustSystem.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = particleSortingOrder;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ApplyDustMaterial(renderer);
    }

    private void UpdateDustEmission(float dt)
    {
        if (dustSystem == null || !enableDustParticles)
        {
            return;
        }

        dustEmitAccumulator += Mathf.Max(0f, dustEmissionRate) * dt;
        int emitCount = Mathf.FloorToInt(dustEmitAccumulator);
        if (emitCount <= 0)
        {
            return;
        }

        dustEmitAccumulator -= emitCount;
        for (int i = 0; i < emitCount; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }
            else
            {
                dir.Normalize();
            }

            float speed = Random.Range(dustSpeedMin, dustSpeedMax);
            Vector3 spawnPos = GetDustSpawnPosition(dir);

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = spawnPos,
                velocity = new Vector3(dir.x, dir.y, 0f) * speed,
                startLifetime = Random.Range(dustLifetimeMin, dustLifetimeMax),
                startSize = Random.Range(dustSizeMin, dustSizeMax),
                startColor = new Color(1f, 0.98f, 0.9f, Random.Range(0.25f, 0.55f))
            };
            dustSystem.Emit(p, 1);
        }
    }

    private Vector3 GetDustSpawnPosition(Vector2 radialDirection)
    {
        if (dustSpawnOverride != null)
        {
            Vector2 randomOffset = Random.insideUnitCircle * dustSpawnRadius;
            Vector3 p = dustSpawnOverride.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
            p.z = 0f;
            return p;
        }

        if (useNoEntryZoneRing && noEntryZoneCollider != null)
        {
            Vector2 center = noEntryZoneCollider.bounds.center;
            float ringRadius = noEntryZoneCollider.radius * Mathf.Max(
                Mathf.Abs(noEntryZoneCollider.transform.lossyScale.x),
                Mathf.Abs(noEntryZoneCollider.transform.lossyScale.y));
            float ringOffset = Random.Range(dustRingOffsetRange.x, dustRingOffsetRange.y);
            Vector2 ringPoint = center + (radialDirection * (ringRadius + ringOffset));
            Vector2 randomOffset = Random.insideUnitCircle * (dustSpawnRadius * 0.5f);
            Vector3 p = new Vector3(ringPoint.x + randomOffset.x, ringPoint.y + randomOffset.y, 0f);
            return p;
        }

        Vector3 origin = GetDustOrigin();
        Vector2 fallbackOffset = Random.insideUnitCircle * dustSpawnRadius;
        return origin + new Vector3(fallbackOffset.x, fallbackOffset.y, 0f);
    }

    private void ResetSpeedLines()
    {
        for (int i = 0; i < speedLines.Count; i++)
        {
            SpeedLine line = speedLines[i];
            RespawnLine(ref line, true);
            speedLines[i] = line;
        }
    }

    private void RespawnLine(ref SpeedLine line, bool instantVisible)
    {
        if (speedLinesRoot == null)
        {
            return;
        }

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.right;
        }
        dir.Normalize();

        float halfW = Mathf.Max(1f, speedLinesRoot.rect.width * 0.5f);
        float halfH = Mathf.Max(1f, speedLinesRoot.rect.height * 0.5f);
        float tx = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        float toEdge = Mathf.Min(tx, ty);
        float startDistance = toEdge * Mathf.Clamp(lineSpawnRadiusRatio, 0.5f, 0.95f);
        Vector2 startPos = dir * startDistance;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        line.rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        line.rect.sizeDelta = new Vector2(
            Random.Range(lineWidthPixels.x, lineWidthPixels.y),
            Random.Range(lineLengthPixels.x, lineLengthPixels.y));
        line.rect.anchoredPosition = startPos;

        line.direction = dir;
        line.speed = Random.Range(lineSpeedPixelsPerSec.x, lineSpeedPixelsPerSec.y);
        line.lifetime = Random.Range(lineLifetimeSeconds.x, lineLifetimeSeconds.y);
        line.age = instantVisible ? Random.Range(0f, line.lifetime * 0.4f) : 0f;
        line.phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private Vector3 GetDustOrigin()
    {
        if (dustSpawnOverride != null)
        {
            return dustSpawnOverride.position;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return transform.position;
        }

        float zDist = Mathf.Abs(cam.transform.position.z);
        Vector3 w = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, zDist));
        w.z = 0f;
        return w;
    }

    private void ApplyDustMaterial(ParticleSystemRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        if (dustParticleTemplateMaterial != null)
        {
            renderer.material = new Material(dustParticleTemplateMaterial);
            return;
        }

        Shader shader = null;
        for (int i = 0; i < s_DustParticleShaderCandidates.Length; i++)
        {
            shader = Shader.Find(s_DustParticleShaderCandidates[i]);
            if (shader != null)
            {
                break;
            }
        }

        if (shader == null)
        {
            Debug.LogWarning(
                "[FallVisualEffectController] Dust particle shader not found. " +
                "Assign Dust Particle Template Material in the Inspector, or add the URP particle shader to Always Included Shaders.");
            return;
        }

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(1f, 0.98f, 0.9f, 1f));
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", new Color(1f, 0.98f, 0.9f, 1f));
        }

        renderer.material = mat;
    }
}
