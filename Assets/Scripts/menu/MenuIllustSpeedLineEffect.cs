using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メニューの Illust 直下から速度線を発生させる軽量エフェクト。
/// 発生パターンは事前生成してループ再生し、ラインはプール再利用する。
/// </summary>
[DefaultExecutionOrder(65)]
public sealed class MenuIllustSpeedLineEffect : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private RectTransform illustRect;
    [SerializeField] private RectTransform effectRoot;

    [Header("生成パターン（ループ）")]
    [SerializeField] private int randomSeed = 4271;
    [SerializeField] private float patternLoopSeconds = 2.4f;
    [SerializeField] private int patternEventCount = 22;
    [SerializeField] private bool emissionByIllustSpeed = true;
    [SerializeField] private float maxEmissionIllustSpeed = 900f;

    [Header("見た目")]
    [SerializeField] private int poolSize = 28;
    [SerializeField] private float startYOffset = -140f;
    [SerializeField] private float spawnXRange = 220f;
    [SerializeField] private float minSpeed = 820f;
    [SerializeField] private float maxSpeed = 1500f;
    [SerializeField] private float minLife = 0.32f;
    [SerializeField] private float maxLife = 0.62f;
    [SerializeField] private float startWidth = 5.8f;
    [SerializeField] private float endWidth = 2.1f;
    [SerializeField] private float startLength = 72f;
    [SerializeField] private float endLength = 260f;
    [SerializeField] private float xDriftRange = 110f;
    [SerializeField] private Color lineColorStart = new Color(0.68f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color lineColorEnd = new Color(0.62f, 0.66f, 1f, 0.45f);
    [SerializeField] private AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1f),
        new Keyframe(1f, 0f));
    [SerializeField] private bool useContrastOutline = true;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Vector2 outlineDistance = new Vector2(1.8f, 1.8f);

    private struct PatternEvent
    {
        public float time;
        public float x;
        public float speed;
        public float life;
        public float driftX;
        public float lengthBias;
        public float widthBias;
        public float alphaBias;
    }

    private sealed class LineState
    {
        public RectTransform rect;
        public Image image;
        public bool active;
        public float age;
        public float life;
        public float speed;
        public float driftX;
        public float widthMul;
        public float lengthMul;
        public float alphaMul;
        public Vector2 startPos;
    }

    private static Sprite s_whiteSprite;
    private readonly List<PatternEvent> _events = new List<PatternEvent>();
    private readonly List<LineState> _lines = new List<LineState>();
    private float _patternTime;
    private int _nextEventIndex;
    private Vector2 _lastIllustPos;
    private bool _hasLastIllustPos;

    private void Awake()
    {
        EnsureReferences();
        EnsurePool();
        RebuildPattern();
    }

    private void OnEnable()
    {
        _patternTime = 0f;
        _nextEventIndex = 0;
        _hasLastIllustPos = false;
        DeactivateAllLines();
    }

    private void LateUpdate()
    {
        if (illustRect == null || effectRoot == null || _events.Count == 0 || _lines.Count == 0)
        {
            return;
        }

        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        float emissionScale = ComputeEmissionScale(dt);

        _patternTime += dt;
        while (_patternTime >= patternLoopSeconds)
        {
            _patternTime -= patternLoopSeconds;
            _nextEventIndex = 0;
        }

        while (_nextEventIndex < _events.Count && _events[_nextEventIndex].time <= _patternTime)
        {
            if (Random.value <= emissionScale)
            {
                Spawn(_events[_nextEventIndex], emissionScale);
            }

            _nextEventIndex++;
        }

        UpdateLines(dt);
    }

    private void EnsureReferences()
    {
        if (illustRect == null)
        {
            illustRect = transform as RectTransform;
        }

        if (illustRect == null)
        {
            return;
        }

        if (effectRoot == null)
        {
            Transform parent = illustRect.parent;
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("IllustSpeedLineRoot");
            if (existing != null)
            {
                effectRoot = existing as RectTransform;
            }
            else
            {
                GameObject rootObject = new GameObject("IllustSpeedLineRoot", typeof(RectTransform));
                effectRoot = rootObject.GetComponent<RectTransform>();
                effectRoot.SetParent(parent, false);
                effectRoot.anchorMin = Vector2.zero;
                effectRoot.anchorMax = Vector2.one;
                effectRoot.offsetMin = Vector2.zero;
                effectRoot.offsetMax = Vector2.zero;
            }
        }

        // イラストより背面に置く（演出の主役はイラスト本体）。
        if (effectRoot != null)
        {
            int illustIndex = illustRect.GetSiblingIndex();
            int desired = Mathf.Max(0, illustIndex - 1);
            effectRoot.SetSiblingIndex(desired);
        }
    }

    private void EnsurePool()
    {
        if (effectRoot == null)
        {
            return;
        }

        poolSize = Mathf.Max(4, poolSize);
        while (_lines.Count < poolSize)
        {
            LineState line = CreateLine(_lines.Count);
            _lines.Add(line);
        }
    }

    private LineState CreateLine(int index)
    {
        GameObject lineObject = new GameObject("SpeedLine_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.SetParent(effectRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(startWidth, startLength);

        Image image = lineObject.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(lineColorStart.r, lineColorStart.g, lineColorStart.b, 0f);
        image.raycastTarget = false;

        if (useContrastOutline)
        {
            Outline outline = lineObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = lineObject.AddComponent<Outline>();
            }

            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = true;
        }

        lineObject.SetActive(false);
        return new LineState
        {
            rect = rect,
            image = image,
            active = false
        };
    }

    private void RebuildPattern()
    {
        _events.Clear();
        patternLoopSeconds = Mathf.Max(0.2f, patternLoopSeconds);
        patternEventCount = Mathf.Max(4, patternEventCount);

        Random.State prev = Random.state;
        Random.InitState(randomSeed);

        for (int i = 0; i < patternEventCount; i++)
        {
            PatternEvent e = new PatternEvent
            {
                time = Random.Range(0f, patternLoopSeconds),
                x = Random.Range(-spawnXRange, spawnXRange),
                speed = Random.Range(minSpeed, maxSpeed),
                life = Random.Range(minLife, maxLife),
                driftX = Random.Range(-xDriftRange, xDriftRange),
                lengthBias = Random.Range(0.8f, 1.25f),
                widthBias = Random.Range(0.75f, 1.2f),
                alphaBias = Random.Range(0.7f, 1.15f)
            };
            _events.Add(e);
        }

        _events.Sort((a, b) => a.time.CompareTo(b.time));
        Random.state = prev;
    }

    private float ComputeEmissionScale(float dt)
    {
        if (!emissionByIllustSpeed || illustRect == null)
        {
            return 1f;
        }

        Vector2 now = illustRect.anchoredPosition;
        if (!_hasLastIllustPos)
        {
            _lastIllustPos = now;
            _hasLastIllustPos = true;
            return 0.6f;
        }

        float speed = Vector2.Distance(now, _lastIllustPos) / Mathf.Max(0.0001f, dt);
        _lastIllustPos = now;
        float t = Mathf.Clamp01(speed / Mathf.Max(10f, maxEmissionIllustSpeed));
        return Mathf.Lerp(0.35f, 1f, t);
    }

    private void Spawn(PatternEvent e, float emissionScale)
    {
        LineState line = GetInactiveLine();
        if (line == null || line.rect == null || line.image == null)
        {
            return;
        }

        line.active = true;
        line.age = 0f;
        line.life = Mathf.Max(0.08f, e.life);
        line.speed = e.speed;
        line.driftX = e.driftX;
        line.widthMul = e.widthBias;
        line.lengthMul = e.lengthBias;
        line.alphaMul = e.alphaBias * emissionScale;

        Vector2 illustPos = illustRect.anchoredPosition;
        Vector2 start = new Vector2(illustPos.x + e.x, illustPos.y + startYOffset);
        line.startPos = start;
        line.rect.anchoredPosition = start;
        line.rect.localEulerAngles = Vector3.zero;
        line.rect.sizeDelta = new Vector2(startWidth * line.widthMul, startLength * line.lengthMul);
        line.image.color = new Color(lineColorStart.r, lineColorStart.g, lineColorStart.b, 0f);
        line.rect.gameObject.SetActive(true);
    }

    private void UpdateLines(float dt)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            LineState line = _lines[i];
            if (line == null || !line.active || line.rect == null || line.image == null)
            {
                continue;
            }

            line.age += dt;
            float t = Mathf.Clamp01(line.age / Mathf.Max(0.01f, line.life));
            if (t >= 1f)
            {
                line.active = false;
                line.rect.gameObject.SetActive(false);
                continue;
            }

            float y = line.startPos.y + line.speed * line.age;
            float x = line.startPos.x + line.driftX * t;
            line.rect.anchoredPosition = new Vector2(x, y);

            float width = Mathf.Lerp(startWidth, endWidth, t) * line.widthMul;
            float length = Mathf.Lerp(startLength, endLength, t) * line.lengthMul;
            line.rect.sizeDelta = new Vector2(width, length);

            Color c = Color.Lerp(lineColorStart, lineColorEnd, t);
            float a = Mathf.Clamp01(alphaCurve.Evaluate(t)) * c.a * line.alphaMul;
            line.image.color = new Color(c.r, c.g, c.b, a);
        }
    }

    private LineState GetInactiveLine()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            if (!_lines[i].active)
            {
                return _lines[i];
            }
        }

        return _lines[0];
    }

    private void DeactivateAllLines()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            LineState line = _lines[i];
            if (line == null || line.rect == null)
            {
                continue;
            }

            line.active = false;
            line.rect.gameObject.SetActive(false);
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
        {
            return s_whiteSprite;
        }

        Texture2D tex = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        patternLoopSeconds = Mathf.Max(0.2f, patternLoopSeconds);
        patternEventCount = Mathf.Max(4, patternEventCount);
        poolSize = Mathf.Max(4, poolSize);
        minSpeed = Mathf.Max(1f, minSpeed);
        maxSpeed = Mathf.Max(minSpeed, maxSpeed);
        minLife = Mathf.Max(0.05f, minLife);
        maxLife = Mathf.Max(minLife, maxLife);
        startWidth = Mathf.Max(0.2f, startWidth);
        endWidth = Mathf.Max(0.1f, endWidth);
        startLength = Mathf.Max(2f, startLength);
        endLength = Mathf.Max(2f, endLength);
        spawnXRange = Mathf.Max(0f, spawnXRange);
        xDriftRange = Mathf.Max(0f, xDriftRange);
        maxEmissionIllustSpeed = Mathf.Max(1f, maxEmissionIllustSpeed);
        outlineDistance.x = Mathf.Max(0f, outlineDistance.x);
        outlineDistance.y = Mathf.Max(0f, outlineDistance.y);
    }
#endif
}
