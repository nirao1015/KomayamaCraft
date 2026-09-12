using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class Game01PlayerController : MonoBehaviour
{
    [Header("Move Speed")]
    [SerializeField] private float minimumSpeed = 1f;
    [SerializeField] private float minimumSpeedDistance = 0.5f;
    [SerializeField] private float maximumSpeed = 6f;
    [SerializeField] private float maximumSpeedDistance = 8f;
    [SerializeField] private float acceleration = 8f;

    [Header("Rebound")]
    [SerializeField] private float reboundReactionDistance = 0.8f;
    [SerializeField] private float reboundInvincibleTime = 0.25f;
    [SerializeField] private float reboundSpeed = 8f;
    [SerializeField] private float reboundDistance = 1.2f;
    [SerializeField] private float reboundWaitTime = 0.3f;
    [SerializeField] private Ease reboundMoveEase = Ease.OutSine;
    [SerializeField] private float reboundDurationScale = 1.35f;
    [SerializeField] private Sprite reboundPlayerSprite;
    [SerializeField] private float damageInvincibleTime = 1f;

    [Header("Destination")]
    [SerializeField] private Transform destination;
    [SerializeField] private SpriteRenderer destinationSpriteRenderer;
    [SerializeField] private float destinationSpriteSwapSeconds = 2f;
    [SerializeField] private Sprite[] destinationSprites = new Sprite[1];
    [SerializeField] private float destinationSpriteSize = 0.6f;
    [SerializeField] private Image reboundDelayTime;

    [Header("Audio")]
    [SerializeField] private Game01SeManager game01SeManager;

    private Rigidbody2D rb;
    private Camera mainCamera;
    private Game01PlayerLife playerLife;
    private SpriteRenderer playerBodySpriteRenderer;
    private bool reboundSpriteSwapActive;
    private Sprite reboundSpriteRestore;
    private Vector2 cursorWorldPosition;
    private Vector2 lastValidCursorWorldPosition;
    private float reboundInvincibleTimer;
    private float reboundCooldownTimer;
    private bool isRebounding;
    private Tween reboundTween;
    private bool hasCursorEnteredScreen;
    private readonly List<Sprite> validDestinationSprites = new List<Sprite>(10);
    private int destinationSpriteIndex;
    private float destinationSpriteSwapTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        playerLife = GetComponent<Game01PlayerLife>();
        playerBodySpriteRenderer = GetComponent<SpriteRenderer>();

        if (destination == null)
        {
            GameObject destinationObject = GameObject.Find("Destination");
            if (destinationObject != null)
            {
                destination = destinationObject.transform;
            }
        }

        if (destinationSpriteRenderer == null && destination != null)
        {
            destinationSpriteRenderer = destination.GetComponent<SpriteRenderer>();
        }

        if (reboundDelayTime == null)
        {
            GameObject reboundDelayObject = GameObject.Find("ReboundDelayTime");
            if (reboundDelayObject != null)
            {
                reboundDelayTime = reboundDelayObject.GetComponent<Image>();
            }
        }

        if (playerLife != null)
        {
            playerLife.SetDamageInvincibilityDuration(damageInvincibleTime);
        }

        lastValidCursorWorldPosition = rb.position;
        cursorWorldPosition = lastValidCursorWorldPosition;

        if (destination != null)
        {
            destination.position = new Vector3(9999f, 9999f, 0f);
            float destinationSize = Mathf.Max(0.01f, destinationSpriteSize);
            destination.localScale = new Vector3(destinationSize, destinationSize, 1f);
            destination.gameObject.SetActive(false);
        }

        RebuildDestinationSpriteList();
        ApplyCurrentDestinationSprite();

        if (reboundDelayTime != null)
        {
            reboundDelayTime.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideReboundDelayTimeWhileControllerDisabled();
    }

    private void OnEnable()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        TryRefreshCursorFromMouse();
        UpdateReboundDelayTimeUI();
    }

    private void HideReboundDelayTimeWhileControllerDisabled()
    {
        if (reboundDelayTime == null)
        {
            return;
        }

        if (reboundDelayTime.gameObject.activeSelf)
        {
            reboundDelayTime.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerLife != null && playerLife.IsGameOver)
        {
            return;
        }

        if (mainCamera == null)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 viewport = mainCamera.ScreenToViewportPoint(mouseScreenPosition);
        bool isInsideScreen = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        if (isInsideScreen)
        {
            lastValidCursorWorldPosition = ScreenToWorldOnGameplayPlane(mouseScreenPosition);
            hasCursorEnteredScreen = true;
        }

        cursorWorldPosition = lastValidCursorWorldPosition;

        if (ShouldRotateTowardsCursor())
        {
            RotateTowardsCursor();
        }

        if (destination != null)
        {
            if (hasCursorEnteredScreen)
            {
                if (!destination.gameObject.activeSelf)
                {
                    destination.gameObject.SetActive(true);
                }

                destination.position = cursorWorldPosition;
                float destinationSize = Mathf.Max(0.01f, destinationSpriteSize);
                destination.localScale = new Vector3(destinationSize, destinationSize, 1f);
            }
        }

        UpdateReboundDelayTimeUI();
        UpdateDestinationSpriteCycle();

        if (reboundInvincibleTimer > 0f)
        {
            reboundInvincibleTimer -= Time.deltaTime;
        }

        if (reboundCooldownTimer > 0f)
        {
            reboundCooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateDestinationSpriteCycle()
    {
        if (destinationSpriteRenderer == null)
        {
            return;
        }

        if (validDestinationSprites.Count == 0 && HasAnyDestinationSpriteConfigured())
        {
            RebuildDestinationSpriteList();
            ApplyCurrentDestinationSprite();
            return;
        }

        if (destinationSpriteRenderer.sprite == null && validDestinationSprites.Count > 0)
        {
            ApplyCurrentDestinationSprite();
        }

        if (validDestinationSprites.Count <= 1)
        {
            return;
        }

        float interval = Mathf.Max(0.01f, destinationSpriteSwapSeconds);
        destinationSpriteSwapTimer += Time.deltaTime;
        if (destinationSpriteSwapTimer < interval)
        {
            return;
        }

        while (destinationSpriteSwapTimer >= interval)
        {
            destinationSpriteSwapTimer -= interval;
            destinationSpriteIndex = (destinationSpriteIndex + 1) % validDestinationSprites.Count;
        }

        ApplyCurrentDestinationSprite();
    }

    private void RebuildDestinationSpriteList()
    {
        validDestinationSprites.Clear();

        if (destinationSprites == null || destinationSprites.Length == 0)
        {
            destinationSpriteIndex = 0;
            destinationSpriteSwapTimer = 0f;
            return;
        }

        int maxCount = Mathf.Min(10, destinationSprites.Length);
        for (int i = 0; i < maxCount; i++)
        {
            Sprite sprite = destinationSprites[i];
            if (sprite != null)
            {
                validDestinationSprites.Add(sprite);
            }
        }

        destinationSpriteIndex = 0;
        destinationSpriteSwapTimer = 0f;
    }

    private void ApplyCurrentDestinationSprite()
    {
        if (destinationSpriteRenderer == null || validDestinationSprites.Count == 0)
        {
            return;
        }

        destinationSpriteRenderer.enabled = true;
        destinationSpriteRenderer.sprite = validDestinationSprites[destinationSpriteIndex];
    }

    private bool HasAnyDestinationSpriteConfigured()
    {
        if (destinationSprites == null || destinationSprites.Length == 0)
        {
            return false;
        }

        int maxCount = Mathf.Min(10, destinationSprites.Length);
        for (int i = 0; i < maxCount; i++)
        {
            if (destinationSprites[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        if (destinationSprites != null && destinationSprites.Length > 10)
        {
            System.Array.Resize(ref destinationSprites, 10);
        }

        destinationSpriteSwapSeconds = Mathf.Max(0.01f, destinationSpriteSwapSeconds);
        destinationSpriteSize = Mathf.Max(0.01f, destinationSpriteSize);
    }

    private bool IsCursorControlActive()
    {
        if (!hasCursorEnteredScreen)
        {
            return false;
        }

        Game01Manager game01Manager = Game01Manager.Instance;
        if (game01Manager == null)
        {
            return true;
        }

        if (game01Manager.WaitForStageIntro && !game01Manager.IsStageIntroComplete)
        {
            return false;
        }

        return !game01Manager.IsStageCleared;
    }

    private bool ShouldRotateTowardsCursor()
    {
        return IsCursorControlActive();
    }

    private void TryRefreshCursorFromMouse()
    {
        if (mainCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 viewport = mainCamera.ScreenToViewportPoint(mouseScreenPosition);
        bool isInsideScreen = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        if (!isInsideScreen)
        {
            return;
        }

        lastValidCursorWorldPosition = ScreenToWorldOnGameplayPlane(mouseScreenPosition);
        hasCursorEnteredScreen = true;
        cursorWorldPosition = lastValidCursorWorldPosition;
    }

    private Vector2 ScreenToWorldOnGameplayPlane(Vector2 screenPosition)
    {
        float depth = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        return new Vector2(world.x, world.y);
    }

    private void RotateTowardsCursor()
    {
        Vector2 toCursor = cursorWorldPosition - rb.position;
        if (toCursor.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        // 右向きを 0 度基準に、マウス方向へ回転。
        float angleDeg = Mathf.Atan2(toCursor.y, toCursor.x) * Mathf.Rad2Deg;
        rb.MoveRotation(angleDeg);
    }

    private void FixedUpdate()
    {
        if (playerLife != null && playerLife.IsGameOver)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isRebounding)
        {
            return;
        }

        if (reboundInvincibleTimer > 0f)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
            return;
        }

        if (!IsCursorControlActive())
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector2 toMouse = cursorWorldPosition - currentPosition;
        float distance = toMouse.magnitude;

        if (distance <= 0.0001f)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
            return;
        }

        if (reboundCooldownTimer <= 0f && distance <= reboundReactionDistance)
        {
            TriggerRebound(currentPosition);
            return;
        }

        Vector2 direction = toMouse / distance;
        float speedRatio = Mathf.InverseLerp(minimumSpeedDistance, maximumSpeedDistance, distance);
        float currentSpeed = Mathf.Lerp(minimumSpeed, maximumSpeed, speedRatio);
        Vector2 targetVelocity = direction * currentSpeed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
    }

    private void TriggerRebound(Vector2 currentPosition)
    {
        reboundTween?.Kill();

        isRebounding = true;
        rb.linearVelocity = Vector2.zero;
        PlayReboundSe();
        reboundInvincibleTimer = Mathf.Max(0f, reboundInvincibleTime);
        reboundCooldownTimer = Mathf.Max(0f, reboundInvincibleTime + reboundWaitTime);
        if (playerLife != null && reboundInvincibleTimer > 0f)
        {
            playerLife.GrantTemporaryInvincibility(reboundInvincibleTimer);
        }

        Vector2 away = currentPosition - cursorWorldPosition;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.up;
        }

        Vector2 direction = away.normalized;
        float distance = Mathf.Max(0.1f, reboundDistance);
        float speed = Mathf.Max(0.1f, reboundSpeed);
        float duration = (distance / speed) * Mathf.Max(0.2f, reboundDurationScale);
        Vector2 target = currentPosition + direction * distance;

        TryApplyReboundPlayerSprite();

        reboundTween = rb
            .DOMove(target, duration)
            .SetEase(reboundMoveEase)
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(OnReboundCompleted)
            .OnKill(OnReboundCompleted);
    }

    private void PlayReboundSe()
    {
        game01SeManager?.PlayPlayerReboundSe();
    }

    private void OnReboundCompleted()
    {
        TryRestoreReboundPlayerSprite();

        if (!isRebounding)
        {
            return;
        }

        isRebounding = false;
    }

    private void TryApplyReboundPlayerSprite()
    {
        if (reboundPlayerSprite == null || playerBodySpriteRenderer == null)
        {
            return;
        }

        if (playerLife != null && playerLife.IsDamageFeedbackActive)
        {
            return;
        }

        reboundSpriteRestore = playerBodySpriteRenderer.sprite;
        playerBodySpriteRenderer.sprite = reboundPlayerSprite;
        reboundSpriteSwapActive = true;
    }

    private void TryRestoreReboundPlayerSprite()
    {
        if (!reboundSpriteSwapActive || playerBodySpriteRenderer == null || reboundPlayerSprite == null)
        {
            reboundSpriteSwapActive = false;
            return;
        }

        if (playerBodySpriteRenderer.sprite != reboundPlayerSprite)
        {
            reboundSpriteSwapActive = false;
            return;
        }

        playerBodySpriteRenderer.sprite = reboundSpriteRestore;
        reboundSpriteSwapActive = false;
    }

    private void UpdateReboundDelayTimeUI()
    {
        if (reboundDelayTime == null)
        {
            return;
        }

        float delayRemaining = Mathf.Max(0f, reboundCooldownTimer - reboundInvincibleTimer);
        bool shouldShow = !isRebounding && delayRemaining > 0f && reboundWaitTime > 0f;
        if (!shouldShow)
        {
            if (reboundDelayTime.gameObject.activeSelf)
            {
                reboundDelayTime.gameObject.SetActive(false);
            }

            return;
        }

        if (!reboundDelayTime.gameObject.activeSelf)
        {
            reboundDelayTime.gameObject.SetActive(true);
        }

        float normalized = Mathf.Clamp01(delayRemaining / reboundWaitTime);
        reboundDelayTime.fillAmount = normalized;

        if (mainCamera == null)
        {
            return;
        }

        RectTransform delayRect = reboundDelayTime.rectTransform;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(transform.position);
        RectTransform parentRect = delayRect.parent as RectTransform;
        if (parentRect != null)
        {
            Canvas canvas = delayRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
            }

            Vector2 anchored;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out anchored);
            delayRect.anchoredPosition = anchored;
        }

        float worldRadius = Mathf.Max(0.1f, reboundReactionDistance);
        Vector3 edgeScreen = mainCamera.WorldToScreenPoint(transform.position + Vector3.right * worldRadius);
        float radiusPixels = Vector2.Distance(screenPoint, edgeScreen);
        float diameterPixels = Mathf.Max(16f, radiusPixels * 2f);
        delayRect.sizeDelta = new Vector2(diameterPixels, diameterPixels);
    }
}

// Backward-compatible alias for existing script asset GUID.
public class PlayerController : Game01PlayerController
{
}
