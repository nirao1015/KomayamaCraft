using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Enemy12Controller : MonoBehaviour
{
    [SerializeField] private float baseMoveSpeed = 4f;
    [SerializeField, Range(0f, 360f)] private float moveDirectionDegrees = 0f;
    [SerializeField] private float playerHomingStrength = 0.8f;
    [SerializeField] private float maxTurnRateDegPerSec = 120f;
    [SerializeField] private float homingStartDelayAfterEnterSeconds = 0.2f;
    [SerializeField, Range(0f, 1f)] private float minForwardSpeedRatio = 0.35f;
    [SerializeField] private int collisionDamage = 1;
    [SerializeField] private float baseSizePixels = 128f;
    [SerializeField] private float sizeMultiplier = 1f;
    [Header("Visual Sprite Lists By Gameplay Time")]
    [SerializeField] private float phaseTime1Seconds = 30f;
    [SerializeField] private float phaseTime2Seconds = 90f;
    [SerializeField] private List<Sprite> spriteList1 = new List<Sprite>();
    [SerializeField] private List<Sprite> spriteList2 = new List<Sprite>();
    [SerializeField] private List<Sprite> spriteList3 = new List<Sprite>();

    private const float OffscreenMargin = 0.15f;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;
    private Vector2 baseDirection;
    private Vector2 currentHomingDirection;
    private bool hasEnteredViewport;
    private float enteredViewportTimer;
    private Sprite defaultAssignedSprite;

    private int maxSimultaneousCount = 8;
    private static int activeCount;
    private static float s_lastSimultaneousCapLogUnscaledTime;
    private static int s_simultaneousCapDeniedSinceLastLog;

    private void Awake()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        defaultAssignedSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        baseDirection = ResolveBaseDirection();
        currentHomingDirection = Vector2.zero;
        ApplyTimelineRandomSprite();

        circleCollider.isTrigger = true;
        ApplySizeMultiplier(sizeMultiplier);
        EnemyPlayerPositionSampler.EnsureExists();
    }

    private void OnEnable()
    {
        activeCount++;
        int maxAllowed = Mathf.Max(1, maxSimultaneousCount);
        if (activeCount > maxAllowed)
        {
            LogSimultaneousSpawnCapDenied("Enemy12", maxAllowed, activeCount);
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
    }

    private static void LogSimultaneousSpawnCapDenied(string label, int maxAllowed, int countedActiveAfterRegisterAttempt)
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
            $"[{label}] 同時出現上限のため破棄: max={maxAllowed}, countedActive={countedActiveAfterRegisterAttempt}, gameplayElapsed={elapsedText}s, 直近ログ間隔での拒否件数={batch}");
    }

    public void SetMaxSimultaneousCount(int count)
    {
        maxSimultaneousCount = Mathf.Max(1, count);
    }

    public void SetupMovement(float speed, float directionDegrees, float homingStrength, float turnRateDegPerSec)
    {
        baseMoveSpeed = Mathf.Max(0f, speed);
        moveDirectionDegrees = Mathf.Repeat(directionDegrees, 360f);
        playerHomingStrength = Mathf.Max(0f, homingStrength);
        maxTurnRateDegPerSec = Mathf.Max(0f, turnRateDegPerSec);
        baseDirection = ResolveBaseDirection();
        currentHomingDirection = Vector2.zero;
        hasEnteredViewport = false;
        enteredViewportTimer = 0f;
    }

    public void ApplySizeMultiplier(float size)
    {
        sizeMultiplier = Mathf.Max(0.01f, size);
        if (spriteRenderer == null || circleCollider == null || spriteRenderer.sprite == null)
        {
            return;
        }

        float targetWorldSize = Mathf.Max(1f, baseSizePixels) / Mathf.Max(1f, spriteRenderer.sprite.pixelsPerUnit);
        float spriteWorldWidth = spriteRenderer.sprite.rect.width / Mathf.Max(1f, spriteRenderer.sprite.pixelsPerUnit);
        float spriteWorldHeight = spriteRenderer.sprite.rect.height / Mathf.Max(1f, spriteRenderer.sprite.pixelsPerUnit);
        float spriteMaxWorld = Mathf.Max(0.0001f, Mathf.Max(spriteWorldWidth, spriteWorldHeight));

        float normalizedScale = targetWorldSize / spriteMaxWorld;
        float totalScale = normalizedScale * sizeMultiplier;
        transform.localScale = Vector3.one * totalScale;
        circleCollider.radius = (targetWorldSize * 0.5f) / Mathf.Max(0.0001f, normalizedScale);
    }

    public void ApplySprite(Sprite sprite)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        // 時間帯リストに有効な画像がある場合はそちらを優先する。
        Sprite selectedByTimeline = ResolveSpriteForCurrentTimeline(fallbackToDefault: false);
        if (selectedByTimeline != null)
        {
            spriteRenderer.sprite = selectedByTimeline;
        }
        else if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
        }

        ApplySizeMultiplier(sizeMultiplier);
    }

    private void Update()
    {
        if (PlayerLife.Instance != null && PlayerLife.Instance.IsGameOver)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsStageCleared)
        {
            return;
        }

        bool isInsideViewport = IsInsideViewport();
        if (isInsideViewport && !hasEnteredViewport)
        {
            hasEnteredViewport = true;
            enteredViewportTimer = 0f;
        }

        if (hasEnteredViewport && isInsideViewport)
        {
            enteredViewportTimer += Time.deltaTime;
        }

        Vector2 sampledPlayerPosition = EnemyPlayerPositionSampler.GetLatestPositionOr(transform.position);
        Vector2 toPlayer = sampledPlayerPosition - (Vector2)transform.position;
        Vector2 playerDirection = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.zero;

        bool shouldApplyHoming =
            hasEnteredViewport &&
            isInsideViewport &&
            enteredViewportTimer >= Mathf.Max(0f, homingStartDelayAfterEnterSeconds);

        if (shouldApplyHoming && playerDirection != Vector2.zero && playerHomingStrength > 0f)
        {
            if (currentHomingDirection.sqrMagnitude <= 0.000001f)
            {
                currentHomingDirection = playerDirection;
            }
            else
            {
                float maxTurnRadians = Mathf.Deg2Rad * Mathf.Max(0f, maxTurnRateDegPerSec) * Time.deltaTime;
                Vector3 rotatedHoming = Vector3.RotateTowards(
                    new Vector3(currentHomingDirection.x, currentHomingDirection.y, 0f),
                    new Vector3(playerDirection.x, playerDirection.y, 0f),
                    maxTurnRadians,
                    float.PositiveInfinity);
                currentHomingDirection = new Vector2(rotatedHoming.x, rotatedHoming.y);
                if (currentHomingDirection.sqrMagnitude > 0.000001f)
                {
                    currentHomingDirection.Normalize();
                }
            }
        }
        else
        {
            currentHomingDirection = Vector2.zero;
        }

        Vector2 baseVelocity = baseDirection * baseMoveSpeed;
        Vector2 homingVelocity = currentHomingDirection * (baseMoveSpeed * playerHomingStrength);
        Vector2 finalVelocity = baseVelocity + homingVelocity;

        float minForwardSpeed = Mathf.Max(0f, baseMoveSpeed * minForwardSpeedRatio);
        float forwardComponent = Vector2.Dot(finalVelocity, baseDirection);
        if (forwardComponent < minForwardSpeed)
        {
            float needed = minForwardSpeed - forwardComponent;
            finalVelocity += baseDirection * needed;
        }

        transform.position += (Vector3)(finalVelocity * Time.deltaTime);
        if (IsFullyOffscreen())
        {
            Destroy(gameObject);
        }
    }

    private Vector2 ResolveBaseDirection()
    {
        // Enemy11 と同じく、0度=右を基準に時計回り方向へ進む解釈に合わせる。
        return (Quaternion.Euler(0f, 0f, -moveDirectionDegrees) * Vector3.right).normalized;
    }

    private bool IsInsideViewport()
    {
        if (mainCamera == null || spriteRenderer == null)
        {
            return false;
        }

        Bounds bounds = spriteRenderer.bounds;
        Vector3 minViewport = mainCamera.WorldToViewportPoint(bounds.min);
        Vector3 maxViewport = mainCamera.WorldToViewportPoint(bounds.max);

        bool intersectsX = maxViewport.x >= 0f && minViewport.x <= 1f;
        bool intersectsY = maxViewport.y >= 0f && minViewport.y <= 1f;
        return intersectsX && intersectsY;
    }

    private bool IsFullyOffscreen()
    {
        if (mainCamera == null || spriteRenderer == null)
        {
            return false;
        }

        Bounds bounds = spriteRenderer.bounds;
        Vector3 minViewport = mainCamera.WorldToViewportPoint(bounds.min);
        Vector3 maxViewport = mainCamera.WorldToViewportPoint(bounds.max);

        bool outLeft = maxViewport.x < -OffscreenMargin;
        bool outRight = minViewport.x > 1f + OffscreenMargin;
        bool outBottom = maxViewport.y < -OffscreenMargin;
        bool outTop = minViewport.y > 1f + OffscreenMargin;
        return outLeft || outRight || outBottom || outTop;
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

    private void ApplyTimelineRandomSprite()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite selected = ResolveSpriteForCurrentTimeline(fallbackToDefault: true);
        if (selected != null)
        {
            spriteRenderer.sprite = selected;
        }
    }

    private Sprite ResolveSpriteForCurrentTimeline(bool fallbackToDefault)
    {
        float t1 = Mathf.Max(0f, phaseTime1Seconds);
        float t2 = Mathf.Max(t1, phaseTime2Seconds);
        float gameplaySeconds = GameManager.Instance != null
            ? Mathf.Max(0f, GameManager.Instance.GameplayElapsedSinceControlSeconds)
            : 0f;

        List<Sprite> sourceList;
        if (gameplaySeconds < t1)
        {
            sourceList = spriteList1;
        }
        else if (gameplaySeconds < t2)
        {
            sourceList = spriteList2;
        }
        else
        {
            sourceList = spriteList3;
        }

        Sprite fromList = PickRandomSpriteFromList(sourceList);
        if (fromList != null)
        {
            return fromList;
        }

        return fallbackToDefault ? defaultAssignedSprite : null;
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
