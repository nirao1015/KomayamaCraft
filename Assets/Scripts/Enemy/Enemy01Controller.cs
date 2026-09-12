using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Enemy01Controller : MonoBehaviour
{
    [Header("Task-006 Inspector")]
    [SerializeField] private float initialExpandSeconds = 1f;
    [SerializeField] private float initialStopSeconds = 0.5f;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField, Range(0f, 360f)] private float objectDirection = 0f;
    [SerializeField, Range(30f, 360f)] private float objectAngle = 50f;
    [SerializeField] private float objectThickness = 0.35f;
    [SerializeField] private float scaleGrowthRate = 0.15f;
    [SerializeField] private int collisionDamage = 1;

    [Header("Visual Sprite (Optional)")]
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Color obstacleSpriteColor = Color.white;
    [SerializeField] private int obstacleSpriteSortingOrder = 131;
    [Header("Visual Sprite Lists By Gameplay Time")]
    [SerializeField] private float phaseTime1Seconds = 30f;
    [SerializeField] private float phaseTime2Seconds = 90f;
    [SerializeField] private List<Sprite> obstacleSpriteList1 = new List<Sprite>();
    [SerializeField] private List<Sprite> obstacleSpriteList2 = new List<Sprite>();
    [SerializeField] private List<Sprite> obstacleSpriteList3 = new List<Sprite>();

    private const float InitialScale = 0f;
    private const float BaseRadius = 1.2f;
    private const int ArcSegments = 18;
    private const float OffscreenMargin = 0.15f;
    private static readonly Color ObstacleColor = new Color(1f, 0.5f, 0.2f, 0.85f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private PolygonCollider2D polygonCollider;
    private SpriteRenderer obstacleSpriteRenderer;
    private Camera mainCamera;
    private Vector3 moveDirection;
    private bool isMoving;
    private int maxSimultaneousCount = 5;
    private static int activeCount;
    private bool registeredInSimultaneousCap;
    private static float s_lastSimultaneousCapLogUnscaledTime;
    private static int s_simultaneousCapDeniedSinceLastLog;
    private const string ObstacleSpriteChildName = "ObstacleSprite";
    private Sprite runtimeSelectedObstacleSprite;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        polygonCollider = GetComponent<PolygonCollider2D>();
        mainCamera = Camera.main;
        runtimeSelectedObstacleSprite = ResolveObstacleSpriteForCurrentTimeline();

        BuildArcMeshAndCollider();
        EnsureDefaultMaterial();
        EnsureObstacleSpriteRenderer();
        RefreshObstacleSpriteVisual();
    }

    private void Reset()
    {
        objectDirection = Random.Range(0f, 360f);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return;
            }
#endif
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
            polygonCollider = polygonCollider != null ? polygonCollider : GetComponent<PolygonCollider2D>();
            runtimeSelectedObstacleSprite = null;
            EnsureObstacleSpriteRenderer();
            RefreshObstacleSpriteVisual();
        }
    }

    private void OnDisable()
    {
        if (!registeredInSimultaneousCap)
        {
            return;
        }

        registeredInSimultaneousCap = false;
        activeCount = Mathf.Max(0, activeCount - 1);
    }

    /// <summary>
    /// 静的カウンタとシーン上の生存数を揃える（同一セッションでドメイン再読込なしの再プレイや、OnEnable 内 Destroy の取りこぼし対策）。
    /// </summary>
    public static void ResyncSimultaneousSpawnCountFromScene()
    {
        Enemy01Controller[] alive = FindObjectsByType<Enemy01Controller>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        activeCount = alive.Length;
        for (int i = 0; i < alive.Length; i++)
        {
            alive[i].registeredInSimultaneousCap = true;
        }
    }

    private static void LogSimultaneousSpawnCapDenied(string label, int maxAllowed, int countedActive)
    {
        s_simultaneousCapDeniedSinceLastLog++;
        float now = Time.unscaledTime;
        if (now - s_lastSimultaneousCapLogUnscaledTime < 0.45f)
        {
            return;
        }

        s_lastSimultaneousCapLogUnscaledTime = now;
        int batch = s_simultaneousCapDeniedSinceLastLog;
        s_simultaneousCapDeniedSinceLastLog = 0;
        float elapsed = -1f;
        if (GameManager.Instance != null)
        {
            elapsed = GameManager.Instance.GameplayElapsedSinceControlSeconds;
        }

        string elapsedText = elapsed >= 0f ? $"{elapsed:F2}" : "n/a";
        Debug.LogWarning(
            $"[{label}] 同時出現上限のため破棄: max={maxAllowed}, countedActive={countedActive}, gameplayElapsed={elapsedText}s, 直近ログ間隔での拒否件数={batch}");
    }

    public void SetMaxSimultaneousCount(int count)
    {
        maxSimultaneousCount = Mathf.Max(1, count);
    }

    public void ApplySpawnSettings(
        float initialExpandTime,
        float initialStopTime,
        float moveSpeedValue,
        float directionDegrees,
        float angleDegrees,
        float thicknessValue,
        float scaleRate)
    {
        initialExpandSeconds = Mathf.Max(0f, initialExpandTime);
        initialStopSeconds = Mathf.Max(0f, initialStopTime);
        moveSpeed = Mathf.Max(0f, moveSpeedValue);
        objectDirection = Mathf.Repeat(directionDegrees, 360f);
        objectAngle = Mathf.Clamp(angleDegrees, 30f, 360f);
        objectThickness = Mathf.Max(0.01f, thicknessValue);
        scaleGrowthRate = Mathf.Max(0f, scaleRate);

        BuildArcMeshAndCollider();
    }

    private void Start()
    {
        // GameManager の Instantiate 直後は OnEnable が先に走るため、同時上限はここで判定する
        // （SetMaxSimultaneousCount / ApplySpawnSettings 適用後の max を使う）。
        TryRegisterSimultaneousSpawnOrDestroy();
        if (!registeredInSimultaneousCap)
        {
            return;
        }

        StartCoroutine(RunSequence());
    }

    private void TryRegisterSimultaneousSpawnOrDestroy()
    {
        if (registeredInSimultaneousCap)
        {
            return;
        }

        int maxAllowed = Mathf.Max(1, maxSimultaneousCount);
        if (activeCount >= maxAllowed)
        {
            LogSimultaneousSpawnCapDenied("Enemy01", maxAllowed, activeCount);
            Destroy(gameObject);
            return;
        }

        activeCount++;
        registeredInSimultaneousCap = true;
    }

    private System.Collections.IEnumerator RunSequence()
    {
        float noEntryRadius = ResolveNoEntryRadius();
        transform.rotation = Quaternion.Euler(0f, 0f, -objectDirection);
        transform.localScale = Vector3.one * InitialScale;

        float targetScale = Mathf.Max(0.05f, noEntryRadius / BaseRadius);
        float timer = 0f;
        float expandDuration = Mathf.Max(0f, initialExpandSeconds);
        while (timer < expandDuration)
        {
            if (IsStageClearFrozen())
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Enemy01] RunSequence expand aborted (stage cleared): scale={transform.localScale.x:F3}, expandProgress={timer:F2}/{expandDuration:F2}");
#endif
                yield break;
            }

            while (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
            {
                yield return null;
            }

            timer += Time.deltaTime;
            float t = expandDuration <= 0f ? 1f : Mathf.Clamp01(timer / expandDuration);
            float scale = Mathf.Lerp(InitialScale, targetScale, t);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        transform.localScale = Vector3.one * targetScale;
        float stopWait = Mathf.Max(0f, initialStopSeconds);
        float stopElapsed = 0f;
        while (stopElapsed < stopWait)
        {
            if (IsStageClearFrozen())
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Enemy01] RunSequence stop-wait aborted (stage cleared): stopElapsed={stopElapsed:F2}/{stopWait:F2}");
#endif
                yield break;
            }

            while (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
            {
                yield return null;
            }

            stopElapsed += Time.deltaTime;
            yield return null;
        }

        moveDirection = ResolveMoveDirection();
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving)
        {
            return;
        }

        if (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
        {
            return;
        }

        if (IsStageClearFrozen())
        {
            return;
        }

        transform.position += moveDirection * (moveSpeed * Time.deltaTime);
        transform.localScale *= 1f + (Mathf.Max(0f, scaleGrowthRate) * Time.deltaTime);

        if (IsFullyOffscreen())
        {
            Destroy(gameObject);
        }
    }

    private void BuildArcMeshAndCollider()
    {
        int segments = Mathf.Clamp(ArcSegments, 6, 64);
        float arc = Mathf.Clamp(objectAngle, 30f, 360f);
        float outer = BaseRadius;
        float inner = Mathf.Max(0.01f, outer - Mathf.Max(0.01f, objectThickness));

        Vector2[] outline = new Vector2[(segments + 1) * 2];
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        float startAngle = -arc * 0.5f;
        float delta = arc / segments;

        for (int i = 0; i <= segments; i++)
        {
            float a = (90f + startAngle + (delta * i)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 outerPoint = dir * outer;
            Vector2 innerPoint = dir * inner;

            int outerIndex = i;
            int innerIndex = i + segments + 1;
            outline[outerIndex] = outerPoint;
            outline[(outline.Length - 1) - i] = innerPoint;
            vertices[outerIndex] = outerPoint;
            vertices[innerIndex] = innerPoint;
        }

        for (int i = 0; i < segments; i++)
        {
            int o0 = i;
            int o1 = i + 1;
            int in0 = i + segments + 1;
            int in1 = i + segments + 2;
            int t = i * 6;

            triangles[t + 0] = o0;
            triangles[t + 1] = o1;
            triangles[t + 2] = in1;
            triangles[t + 3] = o0;
            triangles[t + 4] = in1;
            triangles[t + 5] = in0;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Enemy01ArcMesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;

        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, outline);
        polygonCollider.isTrigger = true;

        RefreshObstacleSpriteTransform(mesh.bounds);
        RefreshObstacleSpriteVisual();
    }

    private void EnsureDefaultMaterial()
    {
        if (meshRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                meshRenderer.sharedMaterial = new Material(shader);
            }
        }

        if (meshRenderer.sharedMaterial != null)
        {
            meshRenderer.sharedMaterial.color = ObstacleColor;
        }

        meshRenderer.sortingOrder = 130;
    }

    private void EnsureObstacleSpriteRenderer()
    {
        if (obstacleSpriteRenderer != null)
        {
            return;
        }

        Transform child = transform.Find(ObstacleSpriteChildName);
        if (child == null)
        {
            GameObject childGo = new GameObject(ObstacleSpriteChildName);
            childGo.transform.SetParent(transform, false);
            child = childGo.transform;
        }

        obstacleSpriteRenderer = child.GetComponent<SpriteRenderer>();
        if (obstacleSpriteRenderer == null)
        {
            obstacleSpriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void RefreshObstacleSpriteVisual()
    {
        if (obstacleSpriteRenderer == null)
        {
            return;
        }

        Sprite displaySprite = runtimeSelectedObstacleSprite != null ? runtimeSelectedObstacleSprite : obstacleSprite;
        obstacleSpriteRenderer.sprite = displaySprite;
        obstacleSpriteRenderer.color = obstacleSpriteColor;
        obstacleSpriteRenderer.sortingOrder = obstacleSpriteSortingOrder;
        obstacleSpriteRenderer.enabled = displaySprite != null;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = displaySprite == null;
        }
    }

    private void RefreshObstacleSpriteTransform(Bounds localMeshBounds)
    {
        if (obstacleSpriteRenderer == null || obstacleSpriteRenderer.sprite == null)
        {
            return;
        }

        Transform spriteTransform = obstacleSpriteRenderer.transform;
        spriteTransform.localPosition = localMeshBounds.center;
        spriteTransform.localRotation = Quaternion.identity;

        Vector3 spriteSize = obstacleSpriteRenderer.sprite.bounds.size;
        float sx = spriteSize.x > 0.0001f ? localMeshBounds.size.x / spriteSize.x : 1f;
        float sy = spriteSize.y > 0.0001f ? localMeshBounds.size.y / spriteSize.y : 1f;
        spriteTransform.localScale = new Vector3(sx, sy, 1f);
    }

    private float ResolveNoEntryRadius()
    {
        GameObject zone = GameObject.Find("no_entryzoone");
        if (zone == null)
        {
            transform.position = Vector3.zero;
            return 2f;
        }

        CircleCollider2D circle = zone.GetComponent<CircleCollider2D>();
        transform.position = zone.transform.position;
        if (circle == null)
        {
            return 2f;
        }

        transform.position = circle.bounds.center;
        float scaledRadius = circle.radius * Mathf.Abs(zone.transform.lossyScale.x);
        return Mathf.Max(0.1f, scaledRadius);
    }

    private Vector3 ResolveMoveDirection()
    {
        return Quaternion.Euler(0f, 0f, -objectDirection) * Vector3.up;
    }

    private bool IsFullyOffscreen()
    {
        if (meshRenderer == null)
        {
            return false;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return false;
        }

        Bounds bounds = meshRenderer.bounds;
        Vector3 minViewport = mainCamera.WorldToViewportPoint(bounds.min);
        Vector3 maxViewport = mainCamera.WorldToViewportPoint(bounds.max);

        bool outLeft = maxViewport.x < -OffscreenMargin;
        bool outRight = minViewport.x > 1f + OffscreenMargin;
        bool outBottom = maxViewport.y < -OffscreenMargin;
        bool outTop = minViewport.y > 1f + OffscreenMargin;
        if (outLeft || outRight || outBottom || outTop)
        {
            return true;
        }

        // スケール成長で AABB が画面より大きくなり、四辺の「完全に片側」条件を満たせない場合がある。
        // そのときはバウンド中心がビューポート外へ十分出たら退場とみなす。
        if (!mainCamera.orthographic)
        {
            return false;
        }

        float halfH = mainCamera.orthographicSize;
        float halfW = halfH * mainCamera.aspect;
        float screenR = Mathf.Max(halfW, halfH);
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
        if (maxExtent > screenR * 5f)
        {
            return true;
        }

        bool oversizedX = bounds.size.x >= 2f * halfW * 0.92f;
        bool oversizedY = bounds.size.y >= 2f * halfH * 0.92f;
        if (!oversizedX && !oversizedY)
        {
            return false;
        }

        Vector3 centerVp = mainCamera.WorldToViewportPoint(bounds.center);
        if (centerVp.z <= 0f)
        {
            return true;
        }

        const float looseMargin = 0.22f;
        return centerVp.x < -looseMargin ||
            centerVp.x > 1f + looseMargin ||
            centerVp.y < -looseMargin ||
            centerVp.y > 1f + looseMargin;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerLife playerLife = other.GetComponent<PlayerLife>();
        if (playerLife == null)
        {
            playerLife = other.GetComponentInParent<PlayerLife>();
        }

        if (playerLife == null)
        {
            return;
        }

        playerLife.TakeDamage(Mathf.Max(1, collisionDamage));
    }

    private static bool IsStageClearFrozen()
    {
        return GameManager.Instance != null && GameManager.Instance.IsStageCleared;
    }

    private Sprite ResolveObstacleSpriteForCurrentTimeline()
    {
        float t1 = Mathf.Max(0f, phaseTime1Seconds);
        float t2 = Mathf.Max(t1, phaseTime2Seconds);
        float gameplaySeconds = GameManager.Instance != null
            ? Mathf.Max(0f, GameManager.Instance.GameplayElapsedSinceControlSeconds)
            : 0f;

        List<Sprite> sourceList;
        if (gameplaySeconds < t1)
        {
            sourceList = obstacleSpriteList1;
        }
        else if (gameplaySeconds < t2)
        {
            sourceList = obstacleSpriteList2;
        }
        else
        {
            sourceList = obstacleSpriteList3;
        }

        Sprite fromList = PickRandomSpriteFromList(sourceList);
        return fromList != null ? fromList : obstacleSprite;
    }

    private static Sprite PickRandomSpriteFromList(List<Sprite> sprites)
    {
        if (sprites == null || sprites.Count == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < sprites.Count; i++)
        {
            Sprite s = sprites[i];
            if (s == null)
            {
                continue;
            }

            if (pick == 0)
            {
                return s;
            }

            pick--;
        }

        return null;
    }
}
