using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// BossUG パネル演出（Buzz 抽選 → 当選表示 → 別衣装反映 → UgTarrget 確定 UI）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03BossUgPresentationController : MonoBehaviour
{
    public struct BossUgPresentationPayload
    {
        public int weaponNumber;
        public RectTransform equippedUnitRect;
        public Sprite weaponBodySprite;
        public Sprite outfitSprite;
        public string displayName;
        public string description;
    }

    private const string NameTextChildName = "NameText (TMP)";
    private const string DescTextChildName = "DescText (TMP)";
    private const string LegacyTextChildName = "Text (TMP)";

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03BgmManager bgmManager;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private RectTransform bossUgPanelRoot;
    [SerializeField] private RectTransform effectOjRoot;
    [SerializeField] private Game03UnitUgBuzzEffectController buzzEffectController;
    [SerializeField] private Game03UnitUgCommentTickerController commentTicker;
    [SerializeField] private Game03UnitUgMonyCountUpController monyCountUp;
    [SerializeField] private RectTransform ugTargetRoot;
    [SerializeField] private Image ugTargetAcceptedItemView;
    [SerializeField] private TMP_Text ugTargetNameText;
    [SerializeField] private TMP_Text ugTargetDescText;
    [SerializeField] private Canvas rootCanvas;

    [Header("Boss points")]
    [SerializeField, Tooltip("BossUG 1 回あたりの獲得ポイント表示・ラン終了時メタ通貨加算（係数なし）。")]
    private long bossAcquisitionPointsPerEvent = 200;

    [Header("Timing")]
    [SerializeField] private float lotteryDurationSeconds = 6f;
    [SerializeField] private float revealDurationSeconds = 1.5f;
    [SerializeField] private float revealHoldBeforeFallSeconds = 0.12f;
    [SerializeField] private float revealFallSeconds = 1f;

    [Header("Reveal Motion")]
    [SerializeField] private RectTransform revealIconParent;
    [SerializeField] private float revealScreenFillScale = 2.8f;
    [SerializeField] private float revealStartScale = 0.35f;
    [SerializeField] private float revealApproachSpinDegrees = 720f;
    [SerializeField] private float revealImpactTiltDegrees = 18f;
    [SerializeField] private float revealFallSpinDegrees = 180f;
    [SerializeField] private float revealFallBottomPadding = 120f;
    [SerializeField] private float revealFallDistanceFallback = 1400f;

    private Image revealIconImage;
    private Coroutine presentationRoutine;
    private MonoBehaviour coroutineHost;
    private Action onFinished;
    private bool panelOpen;
    private bool skipPresentationRequested;
    private bool confirmCloseRequested;
    private bool isAwaitingConfirmClose;

    public bool IsPanelOpen => panelOpen;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        ApplyInitialVisibility();
        ResolveOptionalTexts();
        EnsureRevealIcon();
    }

    public void BeginPresentation(
        BossUgPresentationPayload payload,
        IReadOnlyList<Sprite> loserSprites,
        Action finished,
        MonoBehaviour host)
    {
        ResolveReferencesIfNeeded();
        ResolveOptionalTexts();
        EnsureRevealIcon();

        coroutineHost = host != null && host.gameObject.activeInHierarchy ? host : this;
        if (presentationRoutine != null && coroutineHost != null)
        {
            coroutineHost.StopCoroutine(presentationRoutine);
        }

        onFinished = finished;
        skipPresentationRequested = false;
        confirmCloseRequested = false;
        isAwaitingConfirmClose = false;
        if (coroutineHost == null)
        {
            Debug.LogError("[Game03BossUg] Coroutine host is missing. Presentation aborted.", this);
            finished?.Invoke();
            return;
        }

        presentationRoutine = coroutineHost.StartCoroutine(RunPresentation(payload, loserSprites));
    }

    public void ForceCloseImmediate()
    {
        if (presentationRoutine != null && coroutineHost != null)
        {
            coroutineHost.StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        buzzEffectController?.StopImmediate();
        commentTicker?.StopTicker();
        monyCountUp?.Cancel();
        HideRevealIcon();
        SetPanelOpen(false);
        game03Manager?.SetPaused(false);
        onFinished = null;
        panelOpen = false;
        isAwaitingConfirmClose = false;
        confirmCloseRequested = false;
        skipPresentationRequested = false;
    }

    private void Update()
    {
        if (!panelOpen || presentationRoutine == null)
        {
            return;
        }

        if (buzzEffectController != null && buzzEffectController.IsRunning)
        {
            buzzEffectController.ManualTick(Time.unscaledDeltaTime);
        }

        if (skipPresentationRequested && !isAwaitingConfirmClose)
        {
            monyCountUp?.SnapToFinal();
        }

        if (!IsSkipPressed())
        {
            return;
        }

        if (isAwaitingConfirmClose)
        {
            confirmCloseRequested = true;
        }
        else
        {
            skipPresentationRequested = true;
        }
    }

    private IEnumerator RunPresentation(BossUgPresentationPayload payload, IReadOnlyList<Sprite> loserSprites)
    {
        panelOpen = true;
        skipPresentationRequested = false;
        confirmCloseRequested = false;
        isAwaitingConfirmClose = false;

        game03Manager?.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
        bgmManager?.StopBgm();
        bgmManager?.PlayBossUgPresentationBgm();

        SetPanelOpen(true);
        commentTicker?.BeginTicker();
        long bossPoints = Math.Max(0L, bossAcquisitionPointsPerEvent);
        Game03RunSessionState.AddBossAcquisitionPoints(bossPoints);
        monyCountUp?.BeginCountUp(bossPoints);

        if (effectOjRoot != null)
        {
            effectOjRoot.gameObject.SetActive(true);
        }

        if (ugTargetRoot != null)
        {
            ugTargetRoot.gameObject.SetActive(false);
        }

        ConfigureBuzzEffectForSpawn();
        if (buzzEffectController != null)
        {
            buzzEffectController.BeginLottery(loserSprites, lotteryDurationSeconds);
        }

        float lotteryElapsed = 0f;
        while (lotteryElapsed < lotteryDurationSeconds)
        {
            if (skipPresentationRequested)
            {
                break;
            }

            if (buzzEffectController != null)
            {
                buzzEffectController.ManualTick(Time.unscaledDeltaTime);
            }

            lotteryElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        buzzEffectController?.RequestStopSpawning();
        if (!skipPresentationRequested)
        {
            while (buzzEffectController != null && buzzEffectController.IsRunning)
            {
                if (skipPresentationRequested)
                {
                    break;
                }

                yield return null;
            }
        }

        if (skipPresentationRequested)
        {
            buzzEffectController?.StopImmediate();
            monyCountUp?.SnapToFinal();
        }

        if (effectOjRoot != null)
        {
            effectOjRoot.gameObject.SetActive(true);
        }

        Sprite revealSprite = payload.weaponBodySprite;
        if (!skipPresentationRequested)
        {
            yield return RunRevealMotion(revealSprite);
        }
        else
        {
            HideRevealIcon();
        }

        if (payload.equippedUnitRect != null)
        {
            weaponManager?.ApplyOutfitToUnitRect(payload.equippedUnitRect, payload.outfitSprite);
        }
        else
        {
            weaponManager?.ApplyOutfitToEquippedUnit(payload.weaponNumber, payload.outfitSprite);
        }

        weaponManager?.TryApplyBossWeaponEnhancement(payload.weaponNumber);

        if (effectOjRoot != null)
        {
            effectOjRoot.gameObject.SetActive(false);
        }

        buzzEffectController?.StopImmediate();
        ShowUgTarget(payload);

        isAwaitingConfirmClose = true;
        skipPresentationRequested = false;
        confirmCloseRequested = false;
        yield return null;

        while (panelOpen)
        {
            if (confirmCloseRequested || IsConfirmPressed())
            {
                break;
            }

            yield return null;
        }

        isAwaitingConfirmClose = false;
        confirmCloseRequested = false;

        CloseAndFinish();
        presentationRoutine = null;
    }

    private void ShowUgTarget(BossUgPresentationPayload payload)
    {
        if (ugTargetRoot != null)
        {
            ugTargetRoot.gameObject.SetActive(true);
        }

        ResolveAcceptedItemViewIfNeeded();
        if (ugTargetAcceptedItemView != null)
        {
            ugTargetAcceptedItemView.sprite = payload.outfitSprite;
            ugTargetAcceptedItemView.enabled = payload.outfitSprite != null;
            ugTargetAcceptedItemView.preserveAspect = true;
        }

        if (ugTargetNameText != null)
        {
            ugTargetNameText.text = payload.displayName ?? string.Empty;
        }

        if (ugTargetDescText != null)
        {
            ugTargetDescText.text = payload.description ?? string.Empty;
        }
    }

    private void CloseAndFinish()
    {
        HideRevealIcon();
        if (ugTargetRoot != null)
        {
            ugTargetRoot.gameObject.SetActive(false);
        }

        SetPanelOpen(false);
        game03Manager?.SetPaused(false);
        bgmManager?.StopBgm();
        bgmManager?.PlayMainBgm();

        panelOpen = false;
        isAwaitingConfirmClose = false;
        confirmCloseRequested = false;
        skipPresentationRequested = false;
        commentTicker?.StopTicker();
        Action finished = onFinished;
        onFinished = null;
        finished?.Invoke();
    }

    private void SetPanelOpen(bool open)
    {
        panelOpen = open;
        if (bossUgPanelRoot != null)
        {
            bossUgPanelRoot.gameObject.SetActive(open);
            if (open)
            {
                bossUgPanelRoot.SetAsLastSibling();
            }
        }
    }

    private void ApplyInitialVisibility()
    {
        if (bossUgPanelRoot != null)
        {
            bossUgPanelRoot.gameObject.SetActive(false);
        }

        if (effectOjRoot != null)
        {
            effectOjRoot.gameObject.SetActive(false);
        }

        if (ugTargetRoot != null)
        {
            ugTargetRoot.gameObject.SetActive(false);
        }
    }

    private void ResolveReferencesIfNeeded()
    {
        if (bossUgPanelRoot == null)
        {
            GameObject panel = GameObject.Find("BossUGPanel");
            if (panel != null)
            {
                bossUgPanelRoot = panel.GetComponent<RectTransform>();
            }
        }

        if (effectOjRoot == null && bossUgPanelRoot != null)
        {
            Transform effect = bossUgPanelRoot.Find("EffectOj");
            if (effect != null)
            {
                effectOjRoot = effect as RectTransform;
            }
        }

        if (ugTargetRoot == null && bossUgPanelRoot != null)
        {
            Transform target = bossUgPanelRoot.Find("UgTarrget");
            if (target != null)
            {
                ugTargetRoot = target as RectTransform;
            }
        }

        if (buzzEffectController == null && effectOjRoot != null)
        {
            buzzEffectController = effectOjRoot.GetComponentInChildren<Game03UnitUgBuzzEffectController>(true);
        }

        if (commentTicker == null && bossUgPanelRoot != null)
        {
            commentTicker = bossUgPanelRoot.GetComponentInChildren<Game03UnitUgCommentTickerController>(true);
        }

        if (monyCountUp == null && bossUgPanelRoot != null)
        {
            Transform ojMony = bossUgPanelRoot.Find("OjMony");
            if (ojMony != null)
            {
                monyCountUp = ojMony.GetComponent<Game03UnitUgMonyCountUpController>();
            }
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (game03Manager == null)
        {
            game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
        }

        if (bgmManager == null)
        {
            bgmManager = Game03BgmManager.TryGet();
        }

        if (weaponManager == null)
        {
            weaponManager = FindAnyObjectByType<Game03WeaponManager>(FindObjectsInactive.Include);
        }
    }

    private void ResolveOptionalTexts()
    {
        if (ugTargetRoot == null)
        {
            return;
        }

        if (ugTargetNameText == null)
        {
            ugTargetNameText = FindTmp(ugTargetRoot, NameTextChildName)
                ?? FindTmp(ugTargetRoot, "NameText");
        }

        if (ugTargetDescText == null)
        {
            ugTargetDescText = FindTmp(ugTargetRoot, DescTextChildName)
                ?? FindTmp(ugTargetRoot, LegacyTextChildName)
                ?? FindTmp(ugTargetRoot, "DescText");
        }

        ResolveAcceptedItemViewIfNeeded();
    }

    private void ResolveAcceptedItemViewIfNeeded()
    {
        if (ugTargetAcceptedItemView != null || ugTargetRoot == null)
        {
            return;
        }

        Transform accepted = ugTargetRoot.Find("AcceptedItemView");
        if (accepted != null)
        {
            ugTargetAcceptedItemView = accepted.GetComponent<Image>();
            return;
        }

        Image[] images = ugTargetRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.name == "AcceptedItemView")
            {
                ugTargetAcceptedItemView = image;
                return;
            }
        }
    }

    private static TMP_Text FindTmp(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void EnsureRevealIcon()
    {
        if (revealIconImage != null)
        {
            return;
        }

        RectTransform parent = revealIconParent != null ? revealIconParent : effectOjRoot;
        if (parent == null)
        {
            return;
        }

        Transform existing = parent.Find("RevealIcon");
        if (existing != null)
        {
            revealIconImage = existing.GetComponent<Image>();
            return;
        }

        GameObject go = new GameObject("RevealIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 220f);
        rt.anchoredPosition = Vector2.zero;
        revealIconImage = go.GetComponent<Image>();
        revealIconImage.raycastTarget = false;
        revealIconImage.preserveAspect = true;
        go.SetActive(false);
    }

    private void HideRevealIcon()
    {
        if (revealIconImage != null)
        {
            revealIconImage.gameObject.SetActive(false);
        }
    }

    private void ConfigureBuzzEffectForSpawn()
    {
        if (buzzEffectController == null)
        {
            return;
        }

        RectTransform spawnParent = effectOjRoot != null ? effectOjRoot : bossUgPanelRoot;
        if (spawnParent == null)
        {
            return;
        }

        buzzEffectController.ConfigureSpawnRects(null, spawnParent);
    }

    private IEnumerator RunRevealMotion(Sprite icon)
    {
        EnsureRevealIcon();
        if (revealIconImage == null)
        {
            yield break;
        }

        revealIconImage.gameObject.SetActive(true);
        revealIconImage.sprite = icon;
        revealIconImage.enabled = icon != null;
        Color revealColor = Color.white;
        revealColor.a = 1f;
        revealIconImage.color = revealColor;
        revealIconImage.preserveAspect = true;

        RectTransform rt = revealIconImage.rectTransform;
        rt.SetAsLastSibling();
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * revealStartScale;

        float spinSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float startAngle = UnityEngine.Random.Range(-14f, 14f);
        float approachSpin = revealApproachSpinDegrees * spinSign;
        float landingAngle = startAngle + revealImpactTiltDegrees * spinSign;
        float fallSpin = revealFallSpinDegrees * spinSign;
        const float approachSettleStartT = 0.82f;

        float moveDuration = Mathf.Max(0.05f, revealDurationSeconds * 0.55f);
        float holdDuration = Mathf.Max(0f, revealHoldBeforeFallSeconds);
        float fallDuration = Mathf.Max(0.05f, revealFallSeconds);
        float targetScale = revealScreenFillScale;

        Vector2 startPos = rt.anchoredPosition;
        Vector2 approachPos = new Vector2(0f, 40f);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            if (skipPresentationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rt.anchoredPosition = Vector2.Lerp(startPos, approachPos, eased);
            float scale = Mathf.Lerp(revealStartScale, targetScale, eased);
            rt.localScale = Vector3.one * scale;

            float spinAngle = startAngle + approachSpin * eased;
            if (t >= approachSettleStartT)
            {
                float settleT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(approachSettleStartT, 1f, t));
                spinAngle += (landingAngle - spinAngle) * settleT;
            }

            rt.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
            yield return null;
        }

        rt.anchoredPosition = approachPos;
        rt.localScale = Vector3.one * targetScale;
        rt.localRotation = Quaternion.Euler(0f, 0f, landingAngle);

        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            if (skipPresentationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            rt.localRotation = Quaternion.Euler(0f, 0f, landingAngle);
            yield return null;
        }

        Vector2 fallStart = rt.anchoredPosition;
        Vector2 fallEnd = ComputeRevealFallEndPosition(rt, fallStart);
        float fallStartAngle = landingAngle;
        elapsed = 0f;
        while (elapsed < fallDuration)
        {
            if (skipPresentationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float eased = t * t;
            rt.anchoredPosition = Vector2.Lerp(fallStart, fallEnd, eased);
            rt.localRotation = Quaternion.Euler(0f, 0f, fallStartAngle + fallSpin * eased);
            yield return null;
        }

        rt.anchoredPosition = fallEnd;
        rt.localRotation = Quaternion.Euler(0f, 0f, fallStartAngle + fallSpin);
        yield return null;

        HideRevealIcon();
    }

    private Vector2 ComputeRevealFallEndPosition(RectTransform iconRect, Vector2 fallStart)
    {
        float halfHeight = iconRect.rect.height * Mathf.Abs(iconRect.localScale.y) * 0.5f;
        float padding = Mathf.Max(0f, revealFallBottomPadding);

        float referenceHeight = 1080f;
        RectTransform canvasRect = ResolveCanvasRect();
        if (canvasRect != null && canvasRect.rect.height > 1f)
        {
            referenceHeight = canvasRect.rect.height;
        }

        float dropDistance = referenceHeight * 0.65f + halfHeight + padding;
        dropDistance = Mathf.Max(dropDistance, revealFallDistanceFallback * 0.5f);
        return new Vector2(fallStart.x, fallStart.y - dropDistance);
    }

    private RectTransform ResolveCanvasRect()
    {
        if (rootCanvas != null)
        {
            return rootCanvas.transform as RectTransform;
        }

        if (bossUgPanelRoot != null)
        {
            Canvas c = bossUgPanelRoot.GetComponentInParent<Canvas>();
            if (c != null)
            {
                rootCanvas = c;
                return c.transform as RectTransform;
            }
        }

        return null;
    }

    private static bool IsSkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                return true;
            }
        }
#endif
        return false;
    }

    private static bool IsConfirmPressed() => IsSkipPressed();
}
