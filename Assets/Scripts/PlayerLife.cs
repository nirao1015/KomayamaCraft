using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// Handles player life, invincibility, and game-over state.
/// Attach this to the Player object.
/// </summary>
public class Game01PlayerLife : MonoBehaviour
{
    [Header("Life")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float invincibilityDuration = 1f;

    [Header("Damage Feedback")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite damagedSprite;
    [SerializeField] private Game01SeManager game01SeManager;
    [SerializeField] private float blinkIntervalSeconds = 0.08f;
    [SerializeField] private float redFlashSeconds = 0.1f;
    [SerializeField] private bool useScreenRedFlash;
    [SerializeField] private GameObject redFlashObject;

    public int CurrentLives { get; private set; }
    public bool IsInvincible => invincibleTimer > 0f;
    public bool IsGameOver { get; private set; }

    /// <summary>ダメージ演出コルーチン稼働中（進入禁止リバウンドのスプライト差し替えより表示を優先）。</summary>
    public bool IsDamageFeedbackActive => damageFeedbackCoroutine != null;

    /// <summary>シングルプレイヤー想定の参照用（存在しなければ null）。</summary>
    public static Game01PlayerLife Instance { get; private set; }

    public event Action OnGameOver;

    private float invincibleTimer;
    private Coroutine damageFeedbackCoroutine;
    private SpriteRenderer redFlashRenderer;

    private void Awake()
    {
        Instance = this;
        CurrentLives = Mathf.Max(1, maxLives);
        invincibleTimer = 0f;
        IsGameOver = false;

        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (playerSpriteRenderer != null && normalSprite == null)
        {
            normalSprite = playerSpriteRenderer.sprite;
        }

        if (redFlashObject == null)
        {
            var found = GameObject.Find("RedFlash");
            if (found != null)
            {
                redFlashObject = found;
            }
        }

        if (redFlashObject != null)
        {
            redFlashRenderer = redFlashObject.GetComponent<SpriteRenderer>();
        }

        if (useScreenRedFlash)
        {
            DisableScreenRedFlash();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Call this when the player should take damage (e.g., on obstacle collision).
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsGameOver || IsInvincible || amount <= 0)
        {
            return;
        }

        CurrentLives -= amount;
        CurrentLives = Mathf.Max(CurrentLives, 0);
        invincibleTimer = invincibilityDuration;
        PlayDamageFeedback();

        if (CurrentLives <= 0)
        {
            HandleGameOver();
        }
    }

    public void SetDamageInvincibilityDuration(float duration)
    {
        invincibilityDuration = Mathf.Max(0f, duration);
    }

    public void GrantTemporaryInvincibility(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, Mathf.Max(0f, duration));
    }

    public void ApplyDebugMaxLives(int debugMaxLives)
    {
        maxLives = Mathf.Max(1, debugMaxLives);
        CurrentLives = Mathf.Max(1, maxLives);
        IsGameOver = false;
        invincibleTimer = 0f;
    }

    /// <summary>
    /// FireScreen 等がプレイヤー本体スプライトを上書きした演出の終了時に呼ぶ。
    /// 被弾中にキャッシュされた差し替えへ戻さず、必ず <see cref="normalSprite"/> と白表示に戻す（無敵中の点滅は継続してよい）。
    /// </summary>
    public void RestoreNormalSpriteAfterFireScreenEffect()
    {
        if (playerSpriteRenderer == null)
        {
            return;
        }

        if (normalSprite != null)
        {
            playerSpriteRenderer.sprite = normalSprite;
        }

        playerSpriteRenderer.color = Color.white;
    }

    private void PlayDamageFeedback()
    {
        game01SeManager?.PlayPlayerDamageSe();

        bool needsPlayerVisual = playerSpriteRenderer != null;
        bool needsScreenRedFlash = useScreenRedFlash && redFlashObject != null;
        if (!needsPlayerVisual && !needsScreenRedFlash)
        {
            return;
        }

        if (damageFeedbackCoroutine != null)
        {
            StopCoroutine(damageFeedbackCoroutine);
        }

        damageFeedbackCoroutine = StartCoroutine(DamageFeedbackCoroutine());
    }

    private IEnumerator DamageFeedbackCoroutine()
    {
        bool hasPlayerRenderer = playerSpriteRenderer != null;
        bool doScreenRedFlash = useScreenRedFlash && redFlashObject != null;

        if (hasPlayerRenderer && damagedSprite != null)
        {
            playerSpriteRenderer.sprite = damagedSprite;
        }

        float redFlashToggleAccum = 0f;
        bool redFlashVisible = true;
        if (doScreenRedFlash)
        {
            redFlashObject.SetActive(true);
            if (redFlashRenderer != null)
            {
                redFlashRenderer.enabled = true;
            }

            redFlashVisible = true;
            redFlashToggleAccum = 0f;
        }

        float elapsed = 0f;
        float blinkTimer = 0f;
        bool highAlpha = true;
        float blinkInterval = Mathf.Max(0.01f, blinkIntervalSeconds);
        float redFlashInterval = Mathf.Max(0.01f, redFlashSeconds);

        while (invincibleTimer > 0f && !IsGameOver)
        {
            if (doScreenRedFlash)
            {
                redFlashToggleAccum += Time.deltaTime;
                if (redFlashToggleAccum >= redFlashInterval)
                {
                    redFlashToggleAccum = 0f;
                    redFlashVisible = !redFlashVisible;
                    if (redFlashRenderer != null)
                    {
                        redFlashRenderer.enabled = redFlashVisible;
                    }
                    else
                    {
                        redFlashObject.SetActive(redFlashVisible);
                    }
                }
            }

            if (hasPlayerRenderer)
            {
                elapsed += Time.deltaTime;
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkInterval)
                {
                    highAlpha = !highAlpha;
                    blinkTimer = 0f;
                }

                float alpha = highAlpha ? 1f : 0.2f;
                float redBlend = redFlashSeconds > 0f ? Mathf.Clamp01(1f - (elapsed / redFlashSeconds)) : 0f;
                float gAndB = 1f - (redBlend * 0.6f);
                playerSpriteRenderer.color = new Color(1f, gAndB, gAndB, alpha);
            }

            yield return null;
        }

        DisableScreenRedFlash();

        if (IsGameOver)
        {
            if (hasPlayerRenderer && damagedSprite != null)
            {
                playerSpriteRenderer.sprite = damagedSprite;
            }

            if (hasPlayerRenderer)
            {
                playerSpriteRenderer.color = Color.white;
            }

            damageFeedbackCoroutine = null;
            yield break;
        }

        if (hasPlayerRenderer && normalSprite != null)
        {
            playerSpriteRenderer.sprite = normalSprite;
        }

        if (hasPlayerRenderer)
        {
            playerSpriteRenderer.color = Color.white;
        }

        damageFeedbackCoroutine = null;
    }

    private void DisableScreenRedFlash()
    {
        if (!useScreenRedFlash || redFlashObject == null)
        {
            return;
        }

        if (redFlashRenderer != null)
        {
            redFlashRenderer.enabled = true;
        }

        redFlashObject.SetActive(false);
    }

    private void HandleGameOver()
    {
        if (damageFeedbackCoroutine != null)
        {
            StopCoroutine(damageFeedbackCoroutine);
            damageFeedbackCoroutine = null;
        }

        DisableScreenRedFlash();

        IsGameOver = true;

        if (playerSpriteRenderer != null && damagedSprite != null)
        {
            playerSpriteRenderer.sprite = damagedSprite;
            playerSpriteRenderer.color = Color.white;
        }

        Debug.Log("Game Over");
        OnGameOver?.Invoke();
    }
}

// Backward-compatible alias for existing script asset GUID.
public class PlayerLife : Game01PlayerLife
{
}

