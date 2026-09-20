using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TitleEffectManager : HoverOverlayEffectManagerBase
{
    private enum UnitBigKEffectMode
    {
        None,
        High,
        Mid,
        ClickReact
    }

    [Header("Global Spotlight Overlay")]
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.62f;

    [Header("Game01Button Hover Spotlight")]
    [SerializeField] private bool enableGame01HoverSpotlight = true;
    [SerializeField] private Button game01Button;
    [SerializeField] private float game01SpotlightPadding = 26f;
    [SerializeField] private Color game01SpotlightColor = new Color(1f, 0.98f, 0.85f, 0.22f);
    [SerializeField] private Color game01SpotlightRayColor = new Color(1f, 0.94f, 0.75f, 0.28f);
    [SerializeField] private float game01SpotlightRotationZ = -28f;
    [SerializeField] private Vector2 game01SpotlightOffset = new Vector2(56f, 44f);
    [SerializeField] private float game01SpotlightCoreScale = 1.08f;
    [SerializeField] private float game01SpotlightRayLengthScale = 3.15f;
    [SerializeField] private float game01SpotlightRayWidthScale = 1.28f;
    [SerializeField] private float game01SpotlightRayRotationZ = 38f;
    [SerializeField] private Vector2 game01SpotlightRayOffset = new Vector2(132f, 104f);

    [Header("Game02Button Hover Spotlight")]
    [SerializeField] private bool enableGame02HoverSpotlight = true;
    [SerializeField] private Button game02Button;
    [SerializeField] private float game02SpotlightPadding = 26f;
    [SerializeField] private Color game02SpotlightColor = new Color(1f, 0.98f, 0.85f, 0.22f);
    [SerializeField] private Color game02SpotlightRayColor = new Color(1f, 0.94f, 0.75f, 0.28f);
    [SerializeField] private float game02SpotlightRotationZ = -28f;
    [SerializeField] private Vector2 game02SpotlightOffset = new Vector2(56f, 44f);
    [SerializeField] private float game02SpotlightCoreScale = 1.08f;
    [SerializeField] private float game02SpotlightRayLengthScale = 3.15f;
    [SerializeField] private float game02SpotlightRayWidthScale = 1.28f;
    [SerializeField] private float game02SpotlightRayRotationZ = 38f;
    [SerializeField] private Vector2 game02SpotlightRayOffset = new Vector2(132f, 104f);

    [Header("Game03Button Hover Spotlight")]
    [SerializeField] private bool enableGame03HoverSpotlight = true;
    [SerializeField] private Button game03Button;
    [SerializeField] private float game03SpotlightPadding = 26f;
    [SerializeField] private Color game03SpotlightColor = new Color(1f, 0.98f, 0.85f, 0.22f);
    [SerializeField] private Color game03SpotlightRayColor = new Color(1f, 0.94f, 0.75f, 0.28f);
    [SerializeField] private float game03SpotlightRotationZ = -28f;
    [SerializeField] private Vector2 game03SpotlightOffset = new Vector2(56f, 44f);
    [SerializeField] private float game03SpotlightCoreScale = 1.08f;
    [SerializeField] private float game03SpotlightRayLengthScale = 3.15f;
    [SerializeField] private float game03SpotlightRayWidthScale = 1.28f;
    [SerializeField] private float game03SpotlightRayRotationZ = 38f;
    [SerializeField] private Vector2 game03SpotlightRayOffset = new Vector2(132f, 104f);

    [Header("Unit Big K References")]
    [SerializeField] private RectTransform unitBigKObject;
    [SerializeField] private RectTransform unitBigKImage;
    [SerializeField] private Image unitBigKFaceImage;
    [SerializeField] private Image unitBigKShadowImage;

    [Header("Unit Big K Ground Shadow")]
    [SerializeField] private bool enableUnitBigKShadow = true;
    [SerializeField] private Vector2 unitBigKShadowOffset = new Vector2(0f, -86f);
    [SerializeField] private Vector2 unitBigKShadowSize = new Vector2(188f, 52f);
    [SerializeField] private Color unitBigKShadowColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("Unit Big K Swim (Lissajous)")]
    [SerializeField] private bool enableUnitBigKSwim = true;
    [SerializeField, Min(0f)] private float swimAmplitudeX = 48f;
    [SerializeField, Min(0f)] private float swimAmplitudeY = 32f;
    [SerializeField, Min(0.1f)] private float swimPeriodXSeconds = 17f;
    [SerializeField, Min(0.1f)] private float swimPeriodYSeconds = 11f;
    [SerializeField] private float swimPhaseXDegrees = 0f;
    [SerializeField] private float swimPhaseYDegrees = 35f;
    [SerializeField, Tooltip("時計回りを正。画像（unit-big-k-Image）のローカルZ回転。")]
    private float swimSpinDegreesPerSecond = 6f;

    [Header("Unit Big K Blink")]
    [SerializeField] private bool enableUnitBigKBlink = true;
    [SerializeField] private Sprite unitBigKEyesOpenSprite;
    [SerializeField] private Sprite unitBigKEyesClosedSprite;
    [SerializeField, Min(0.5f)] private float blinkIntervalMinSeconds = 3.2f;
    [SerializeField, Min(0.5f)] private float blinkIntervalMaxSeconds = 7.5f;
    [SerializeField, Min(0.02f)] private float blinkClosedSeconds = 0.1f;
    [SerializeField, Range(0f, 1f)] private float blinkDoubleChance = 0.22f;
    [SerializeField, Min(0.02f)] private float blinkDoubleGapSeconds = 0.08f;

    [Header("Unit Big K Click React")]
    [SerializeField] private bool enableUnitBigKClickReact = true;
    [SerializeField, Range(0f, 1f), Tooltip("クリック時に回転演出を選ぶ確率。残りは耳尻尾ピコピコ。")]
    private float clickReactSpinChance = 0.3f;
    [SerializeField, Min(0f)] private float clickReactShakePixels = 20f;
    [SerializeField, Min(0.05f)] private float clickReactShakeSeconds = 0.22f;
    [SerializeField, Min(0.05f)] private float clickReactSpinSeconds = 1.2f;
    [SerializeField, Min(0f)] private float clickReactCooldownSeconds = 3f;
    [SerializeField] private Sprite unitBigKTailDownEarsTightSprite;
    [SerializeField] private Sprite unitBigKTailUpEarsOpenSprite;
    [SerializeField, Min(1)] private int clickReactEarRepeatCount = 3;
    [SerializeField, Min(0.02f)] private float clickReactEarFrameSeconds = 0.12f;

    [Header("Unit Big K High Priority (OnClick Jump)")]
    [SerializeField] private float highJumpHeight = 30f;
    [SerializeField] private float highJumpDurationSeconds = 0.28f;

    [Header("Unit Big K Mid Priority (Hover Look)")]
    [SerializeField] private float midTurnAngle = 10f;
    [SerializeField] private float midBaseRotationY = 180f;
    [SerializeField] private float midBaseRotationZ = 0f;
    [SerializeField] private float midLookForwardAngleOffsetZ = 0f;
    [SerializeField] private bool midInvertLookWhenFlippedY = true;
    [SerializeField] private float midTurnDurationSeconds = 0.2f;

    private RectTransform canvasRect;
    private Image dimTop;
    private Image dimBottom;
    private Image dimLeft;
    private Image dimRight;
    private Image spotlightCoreImage;
    private Image spotlightRayImage;
    private bool spotlightVisible;
    private static Sprite spotlightCoreSprite;
    private static Sprite spotlightRaySprite;
    private static Sprite unitBigKShadowSprite;
    private UnitBigKEffectMode activeUnitBigKEffectMode;
    private Coroutine unitBigKEffectCoroutine;
    private Coroutine unitBigKBlinkCoroutine;
    private bool clickListenersRegistered;
    private Button hoveredGameButton;
    private Vector2 unitBigKObjectBaseAnchoredPosition;
    private Vector2 unitBigKImageBaseAnchoredPosition;
    private Vector3 unitBigKImageBaseLocalEuler;
    private Quaternion unitBigKObjectBaseRotation;
    private Vector3 unitBigKObjectBaseEuler;
    private float unitBigKSwimElapsed;
    private float unitBigKSpinDegrees;
    private bool unitBigKSwimPaused;
    private float unitBigKClickNextAllowedUnscaledTime;
    private Button unitBigKClickButton;
    private bool unitBigKClickListenerRegistered;

    protected override void Awake()
    {
        base.Awake();
        EnsureSpotlightRuntime();
        EnsureUnitBigKRuntime();
        CacheUnitBigKBasePose();
        RegisterButtonClickListeners();
        RegisterUnitBigKClickListener();
        RestartUnitBigKBlinkLoop();
        SetSpotlightVisible(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureSpotlightRuntime();
        EnsureUnitBigKRuntime();
        CacheUnitBigKBasePose();
        RegisterButtonClickListeners();
        RegisterUnitBigKClickListener();
        RestartUnitBigKBlinkLoop();
        SetSpotlightVisible(false);
    }

    private void OnDisable()
    {
        UnregisterButtonClickListeners();
        UnregisterUnitBigKClickListener();
        StopUnitBigKBlinkLoop();
        StopUnitBigKEffect(true);
        unitBigKSwimPaused = false;
        hoveredGameButton = null;
    }

    protected override void Update()
    {
        base.Update();
        UpdateButtonSpotlight();
        UpdateUnitBigKSwim();
    }

    private void UpdateButtonSpotlight()
    {
        if (canvasRect == null)
        {
            SetSpotlightVisible(false);
            return;
        }

        if (TryUpdateSpotlightForButton(
            enableGame01HoverSpotlight,
            game01Button,
            game01SpotlightPadding,
            game01SpotlightColor,
            game01SpotlightRayColor,
            game01SpotlightRotationZ,
            game01SpotlightOffset,
            game01SpotlightCoreScale,
            game01SpotlightRayLengthScale,
            game01SpotlightRayWidthScale,
            game01SpotlightRayRotationZ,
            game01SpotlightRayOffset))
        {
            HandleHoveredButtonChanged(game01Button);
            return;
        }

        if (TryUpdateSpotlightForButton(
            enableGame02HoverSpotlight,
            game02Button,
            game02SpotlightPadding,
            game02SpotlightColor,
            game02SpotlightRayColor,
            game02SpotlightRotationZ,
            game02SpotlightOffset,
            game02SpotlightCoreScale,
            game02SpotlightRayLengthScale,
            game02SpotlightRayWidthScale,
            game02SpotlightRayRotationZ,
            game02SpotlightRayOffset))
        {
            HandleHoveredButtonChanged(game02Button);
            return;
        }

        if (TryUpdateSpotlightForButton(
            enableGame03HoverSpotlight,
            game03Button,
            game03SpotlightPadding,
            game03SpotlightColor,
            game03SpotlightRayColor,
            game03SpotlightRotationZ,
            game03SpotlightOffset,
            game03SpotlightCoreScale,
            game03SpotlightRayLengthScale,
            game03SpotlightRayWidthScale,
            game03SpotlightRayRotationZ,
            game03SpotlightRayOffset))
        {
            HandleHoveredButtonChanged(game03Button);
            return;
        }

        HandleHoveredButtonChanged(null);
        SetSpotlightVisible(false);
    }

    private bool TryUpdateSpotlightForButton(
        bool enabled,
        Button button,
        float padding,
        Color coreColor,
        Color rayColor,
        float coreRotationZ,
        Vector2 coreOffset,
        float coreScale,
        float rayLengthScale,
        float rayWidthScale,
        float rayRotationZ,
        Vector2 rayOffset)
    {
        if (!enabled || button == null || !button.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null || !TryGetButtonBoundsInCanvas(buttonRect, padding, out Vector2 min, out Vector2 max))
        {
            return false;
        }

        if (!IsPointerInsideButton(buttonRect))
        {
            return false;
        }

        SetSpotlightVisible(true);
        ApplyDimPanels(min, max);
        ApplySpotlightVisual(min, max, coreColor, rayColor, coreRotationZ, coreOffset, coreScale, rayLengthScale, rayWidthScale, rayRotationZ, rayOffset);
        return true;
    }

    private bool IsPointerInsideButton(RectTransform buttonRect)
    {
        if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Canvas c = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>() : null;
        Camera cam = null;
        if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = c.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, pointerScreenPosition, cam);
    }

    private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        return true;
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private bool TryGetButtonBoundsInCanvas(RectTransform buttonRect, float padding, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (canvasRect == null || buttonRect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector2 localMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 localMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 localPoint = canvasRect.InverseTransformPoint(corners[i]);
            localMin = Vector2.Min(localMin, localPoint);
            localMax = Vector2.Max(localMax, localPoint);
        }

        float pad = Mathf.Max(0f, padding);
        localMin -= Vector2.one * pad;
        localMax += Vector2.one * pad;
        min = localMin;
        max = localMax;
        return true;
    }

    private void ApplyDimPanels(Vector2 holeMin, Vector2 holeMax)
    {
        Rect r = canvasRect.rect;
        SetRect(dimTop.rectTransform, r.xMin, r.xMax, holeMax.y, r.yMax);
        SetRect(dimBottom.rectTransform, r.xMin, r.xMax, r.yMin, holeMin.y);
        SetRect(dimLeft.rectTransform, r.xMin, holeMin.x, holeMin.y, holeMax.y);
        SetRect(dimRight.rectTransform, holeMax.x, r.xMax, holeMin.y, holeMax.y);
    }

    private void ApplySpotlightVisual(
        Vector2 holeMin,
        Vector2 holeMax,
        Color coreColor,
        Color rayColor,
        float coreRotationZ,
        Vector2 coreOffset,
        float coreScale,
        float rayLengthScale,
        float rayWidthScale,
        float rayRotationZ,
        Vector2 rayOffset)
    {
        if (spotlightCoreImage == null || spotlightRayImage == null)
        {
            return;
        }

        Vector2 baseSize = holeMax - holeMin;
        Vector2 coreCenter = (holeMin + holeMax) * 0.5f + coreOffset;
        Vector2 coreSize = baseSize * coreScale;
        RectTransform coreRt = spotlightCoreImage.rectTransform;
        coreRt.localRotation = Quaternion.Euler(0f, 0f, coreRotationZ);
        SetRect(coreRt, coreCenter.x - coreSize.x * 0.5f, coreCenter.x + coreSize.x * 0.5f, coreCenter.y - coreSize.y * 0.5f, coreCenter.y + coreSize.y * 0.5f);
        spotlightCoreImage.color = coreColor;

        Vector2 rayCenter = coreCenter + rayOffset;
        Vector2 raySize = new Vector2(baseSize.x * rayLengthScale, baseSize.y * rayWidthScale);
        RectTransform rayRt = spotlightRayImage.rectTransform;
        rayRt.localRotation = Quaternion.Euler(0f, 0f, rayRotationZ);
        SetRect(rayRt, rayCenter.x - raySize.x * 0.5f, rayCenter.x + raySize.x * 0.5f, rayCenter.y - raySize.y * 0.5f, rayCenter.y + raySize.y * 0.5f);
        spotlightRayImage.color = rayColor;
    }

    private static void SetRect(RectTransform rt, float xMin, float xMax, float yMin, float yMax)
    {
        if (rt == null)
        {
            return;
        }

        float width = Mathf.Max(0f, xMax - xMin);
        float height = Mathf.Max(0f, yMax - yMin);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        rt.sizeDelta = new Vector2(width, height);
    }

    private void EnsureSpotlightRuntime()
    {
        if (game01Button == null)
        {
            Transform found = transform.root != null ? transform.root.Find("TitleCanvas/Game01Button") : null;
            if (found != null)
            {
                game01Button = found.GetComponent<Button>();
            }
        }

        if (game02Button == null)
        {
            Transform found = transform.root != null ? transform.root.Find("TitleCanvas/Game02Button") : null;
            if (found != null)
            {
                game02Button = found.GetComponent<Button>();
            }
        }

        if (game03Button == null)
        {
            Transform found = transform.root != null ? transform.root.Find("TitleCanvas/Game03Button") : null;
            if (found != null)
            {
                game03Button = found.GetComponent<Button>();
            }
        }

        if (canvasRect == null)
        {
            Canvas canvas = null;
            if (game01Button != null)
            {
                canvas = game01Button.GetComponentInParent<Canvas>();
            }

            if (canvas == null && game02Button != null)
            {
                canvas = game02Button.GetComponentInParent<Canvas>();
            }

            if (canvas == null && game03Button != null)
            {
                canvas = game03Button.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas c = canvases[i];
                    if (c != null && c.gameObject.name == "TitleCanvas")
                    {
                        canvas = c;
                        break;
                    }
                }

                if (canvas == null && canvases.Length > 0)
                {
                    canvas = canvases[0];
                }
            }

            if (canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
            }
        }

        if (canvasRect == null)
        {
            return;
        }

        dimTop = EnsureOverlayImage("Game03SpotlightDimTop", new Color(0f, 0f, 0f, dimAlpha));
        dimBottom = EnsureOverlayImage("Game03SpotlightDimBottom", new Color(0f, 0f, 0f, dimAlpha));
        dimLeft = EnsureOverlayImage("Game03SpotlightDimLeft", new Color(0f, 0f, 0f, dimAlpha));
        dimRight = EnsureOverlayImage("Game03SpotlightDimRight", new Color(0f, 0f, 0f, dimAlpha));
        spotlightCoreImage = EnsureOverlayImage("GameButtonSpotlightCore", game03SpotlightColor, EnsureSpotlightCoreSprite());
        spotlightRayImage = EnsureOverlayImage("GameButtonSpotlightRay", game03SpotlightRayColor, EnsureSpotlightRaySprite());

        if (spotlightCoreImage != null)
        {
            spotlightCoreImage.rectTransform.SetAsLastSibling();
        }

        if (spotlightRayImage != null)
        {
            spotlightRayImage.rectTransform.SetAsLastSibling();
        }
    }

    private void EnsureUnitBigKRuntime()
    {
        if (unitBigKObject == null)
        {
            Transform found = transform.root != null ? transform.root.Find("Canvas/unit-big-k-Object") : null;
            if (found == null && transform.root != null)
            {
                found = transform.root.Find("TitleCanvas/unit-big-k-Object");
            }

            if (found != null)
            {
                unitBigKObject = found as RectTransform;
            }
        }

        if (unitBigKImage == null)
        {
            Transform found = null;
            if (unitBigKObject != null)
            {
                found = unitBigKObject.Find("unit-big-k-Image");
            }

            if (found == null && transform.root != null)
            {
                found = transform.root.Find("Canvas/unit-big-k-Object/unit-big-k-Image");
            }

            if (found != null)
            {
                unitBigKImage = found as RectTransform;
            }
        }

        if (unitBigKFaceImage == null && unitBigKImage != null)
        {
            unitBigKFaceImage = unitBigKImage.GetComponent<Image>();
        }

        if (unitBigKFaceImage != null)
        {
            if (unitBigKEyesOpenSprite == null && unitBigKFaceImage.sprite != null)
            {
                unitBigKEyesOpenSprite = unitBigKFaceImage.sprite;
            }

            if (unitBigKEyesOpenSprite != null &&
                unitBigKFaceImage.sprite != unitBigKEyesClosedSprite)
            {
                unitBigKFaceImage.sprite = unitBigKEyesOpenSprite;
            }

            if (enableUnitBigKClickReact)
            {
                unitBigKFaceImage.raycastTarget = true;
                unitBigKClickButton = unitBigKFaceImage.GetComponent<Button>();
                if (unitBigKClickButton == null)
                {
                    unitBigKClickButton = unitBigKFaceImage.gameObject.AddComponent<Button>();
                }

                unitBigKClickButton.transition = Selectable.Transition.None;
                unitBigKClickButton.targetGraphic = unitBigKFaceImage;
            }
        }

        EnsureUnitBigKShadow();
    }

    private void EnsureUnitBigKShadow()
    {
        if (unitBigKObject == null)
        {
            return;
        }

        Transform shadowTransform = unitBigKObject.Find("unit-big-k-Shadow");
        if (shadowTransform == null)
        {
            GameObject shadowObject = new GameObject("unit-big-k-Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowObject.transform.SetParent(unitBigKObject, false);
            shadowTransform = shadowObject.transform;
        }

        if (unitBigKShadowImage == null)
        {
            unitBigKShadowImage = shadowTransform.GetComponent<Image>();
        }

        if (unitBigKShadowImage == null)
        {
            unitBigKShadowImage = shadowTransform.gameObject.AddComponent<Image>();
        }

        RefreshUnitBigKShadowLayout();

        unitBigKShadowImage.sprite = EnsureUnitBigKShadowSprite();
        unitBigKShadowImage.type = Image.Type.Simple;
        unitBigKShadowImage.color = unitBigKShadowColor;
        unitBigKShadowImage.raycastTarget = false;
        unitBigKShadowImage.enabled = enableUnitBigKShadow;

        if (unitBigKImage != null && unitBigKShadowImage.transform.GetSiblingIndex() > unitBigKImage.GetSiblingIndex())
        {
            unitBigKShadowImage.transform.SetSiblingIndex(unitBigKImage.GetSiblingIndex());
        }
    }

    private void CacheUnitBigKBasePose()
    {
        if (unitBigKObject != null)
        {
            unitBigKObjectBaseAnchoredPosition = unitBigKObject.anchoredPosition;
            unitBigKObjectBaseRotation = unitBigKObject.localRotation;
            unitBigKObjectBaseEuler = unitBigKObject.localEulerAngles;
        }

        if (unitBigKImage != null)
        {
            unitBigKImageBaseAnchoredPosition = unitBigKImage.anchoredPosition;
            unitBigKImageBaseLocalEuler = unitBigKImage.localEulerAngles;
        }

        unitBigKSwimElapsed = 0f;
        unitBigKSpinDegrees = 0f;
        RefreshUnitBigKShadowLayout();
    }

    private void RefreshUnitBigKShadowLayout()
    {
        if (unitBigKShadowImage == null)
        {
            return;
        }

        RectTransform shadowRect = unitBigKShadowImage.rectTransform;
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        Vector2 baseAnchor = unitBigKImage != null ? unitBigKImageBaseAnchoredPosition : Vector2.zero;
        shadowRect.anchoredPosition = baseAnchor + unitBigKShadowOffset;
        shadowRect.sizeDelta = unitBigKShadowSize;
        shadowRect.localRotation = Quaternion.identity;
        shadowRect.localScale = Vector3.one;
    }

    private void RegisterButtonClickListeners()
    {
        if (clickListenersRegistered)
        {
            return;
        }

        if (game01Button != null) game01Button.onClick.AddListener(OnClickGameButton);
        if (game02Button != null) game02Button.onClick.AddListener(OnClickGameButton);
        if (game03Button != null) game03Button.onClick.AddListener(OnClickGameButton);
        clickListenersRegistered = true;
    }

    private void UnregisterButtonClickListeners()
    {
        if (!clickListenersRegistered)
        {
            return;
        }

        if (game01Button != null) game01Button.onClick.RemoveListener(OnClickGameButton);
        if (game02Button != null) game02Button.onClick.RemoveListener(OnClickGameButton);
        if (game03Button != null) game03Button.onClick.RemoveListener(OnClickGameButton);
        clickListenersRegistered = false;
    }

    private void OnClickGameButton()
    {
        TriggerHighJumpEffect();
    }

    private void HandleHoveredButtonChanged(Button newHoveredButton)
    {
        if (hoveredGameButton == newHoveredButton)
        {
            return;
        }

        hoveredGameButton = newHoveredButton;
        if (hoveredGameButton == null)
        {
            if (unitBigKObject != null)
            {
                unitBigKObject.localRotation = Quaternion.Euler(unitBigKObjectBaseEuler.x, midBaseRotationY, midBaseRotationZ);
            }
            return;
        }

        TriggerMidLookEffect(hoveredGameButton);
    }

    private void UpdateUnitBigKSwim()
    {
        if (!enableUnitBigKSwim || unitBigKSwimPaused)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        unitBigKSwimElapsed += dt;

        if (unitBigKObject != null)
        {
            float periodX = Mathf.Max(0.1f, swimPeriodXSeconds);
            float periodY = Mathf.Max(0.1f, swimPeriodYSeconds);
            float phaseX = swimPhaseXDegrees * Mathf.Deg2Rad;
            float phaseY = swimPhaseYDegrees * Mathf.Deg2Rad;
            float x = Mathf.Sin((Mathf.PI * 2f * unitBigKSwimElapsed / periodX) + phaseX) * swimAmplitudeX;
            float y = Mathf.Sin((Mathf.PI * 2f * unitBigKSwimElapsed / periodY) + phaseY) * swimAmplitudeY;
            unitBigKObject.anchoredPosition = unitBigKObjectBaseAnchoredPosition + new Vector2(x, y);
        }

        if (unitBigKImage != null)
        {
            // Unity の +Z は反時計回りなので、時計回りはマイナス。
            unitBigKSpinDegrees -= swimSpinDegreesPerSecond * dt;
            ApplyUnitBigKImageSpin();
        }
    }

    private void ApplyUnitBigKImageSpin()
    {
        if (unitBigKImage == null)
        {
            return;
        }

        Vector3 euler = unitBigKImageBaseLocalEuler;
        euler.z += unitBigKSpinDegrees;
        unitBigKImage.localEulerAngles = euler;
    }

    private void RegisterUnitBigKClickListener()
    {
        if (unitBigKClickListenerRegistered || !enableUnitBigKClickReact)
        {
            return;
        }

        EnsureUnitBigKRuntime();
        if (unitBigKClickButton == null)
        {
            return;
        }

        unitBigKClickButton.onClick.AddListener(OnClickUnitBigKImage);
        unitBigKClickListenerRegistered = true;
    }

    private void UnregisterUnitBigKClickListener()
    {
        if (!unitBigKClickListenerRegistered)
        {
            return;
        }

        if (unitBigKClickButton != null)
        {
            unitBigKClickButton.onClick.RemoveListener(OnClickUnitBigKImage);
        }

        unitBigKClickListenerRegistered = false;
    }

    private void OnClickUnitBigKImage()
    {
        if (!enableUnitBigKClickReact)
        {
            return;
        }

        if (activeUnitBigKEffectMode == UnitBigKEffectMode.ClickReact)
        {
            return;
        }

        if (Time.unscaledTime < unitBigKClickNextAllowedUnscaledTime)
        {
            return;
        }

        bool playSpin = Random.value < Mathf.Clamp01(clickReactSpinChance);
        if (playSpin)
        {
            StartUnitBigKEffect(UnitBigKEffectMode.ClickReact, CoPlayUnitBigKClickSpinEffect());
        }
        else
        {
            StartUnitBigKEffect(UnitBigKEffectMode.ClickReact, CoPlayUnitBigKClickEarTailEffect());
        }
    }

    private System.Collections.IEnumerator CoPlayUnitBigKClickSpinEffect()
    {
        if (unitBigKImage == null)
        {
            FinishUnitBigKClickReact();
            yield break;
        }

        unitBigKSwimPaused = true;

        Vector2 imageBase = unitBigKImageBaseAnchoredPosition;
        float shakeAmp = Mathf.Abs(clickReactShakePixels);
        float shakeDur = Mathf.Max(0.05f, clickReactShakeSeconds);
        float shakeElapsed = 0f;
        while (shakeElapsed < shakeDur)
        {
            shakeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shakeElapsed / shakeDur);
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            unitBigKImage.anchoredPosition = imageBase + new Vector2(0f, wave * shakeAmp);
            yield return null;
        }

        unitBigKImage.anchoredPosition = imageBase;

        float spinSign = swimSpinDegreesPerSecond >= 0f ? 1f : -1f;
        float reverseDelta = 360f * spinSign;
        float spinStart = unitBigKSpinDegrees;
        float spinDur = Mathf.Max(0.05f, clickReactSpinSeconds);
        float spinElapsed = 0f;
        while (spinElapsed < spinDur)
        {
            spinElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(spinElapsed / spinDur);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            unitBigKSpinDegrees = spinStart + reverseDelta * eased;
            ApplyUnitBigKImageSpin();
            yield return null;
        }

        unitBigKSpinDegrees = spinStart + reverseDelta;
        ApplyUnitBigKImageSpin();

        unitBigKSwimPaused = false;
        FinishUnitBigKClickReact();
    }

    private System.Collections.IEnumerator CoPlayUnitBigKClickEarTailEffect()
    {
        if (unitBigKFaceImage == null ||
            unitBigKTailDownEarsTightSprite == null ||
            unitBigKTailUpEarsOpenSprite == null)
        {
            // 素材未設定時は回転側へフォールバック
            yield return CoPlayUnitBigKClickSpinEffect();
            yield break;
        }

        // 遊泳は継続。瞬きとスプライトが競合しないよう一時停止。
        StopUnitBigKBlinkLoop();

        int repeats = Mathf.Max(1, clickReactEarRepeatCount);
        float frameDur = Mathf.Max(0.02f, clickReactEarFrameSeconds);
        for (int i = 0; i < repeats; i++)
        {
            unitBigKFaceImage.sprite = unitBigKTailDownEarsTightSprite;
            float elapsed = 0f;
            while (elapsed < frameDur)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            unitBigKFaceImage.sprite = unitBigKTailUpEarsOpenSprite;
            elapsed = 0f;
            while (elapsed < frameDur)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ApplyUnitBigKEyesOpen();
        RestartUnitBigKBlinkLoop();
        FinishUnitBigKClickReact();
    }

    private void FinishUnitBigKClickReact()
    {
        unitBigKSwimPaused = false;
        unitBigKClickNextAllowedUnscaledTime =
            Time.unscaledTime + Mathf.Max(0f, clickReactCooldownSeconds);
        activeUnitBigKEffectMode = UnitBigKEffectMode.None;
        unitBigKEffectCoroutine = null;
    }

    private void RestartUnitBigKBlinkLoop()
    {
        StopUnitBigKBlinkLoop();
        if (!enableUnitBigKBlink || !isActiveAndEnabled)
        {
            return;
        }

        unitBigKBlinkCoroutine = StartCoroutine(CoUnitBigKBlinkLoop());
    }

    private void StopUnitBigKBlinkLoop()
    {
        if (unitBigKBlinkCoroutine != null)
        {
            StopCoroutine(unitBigKBlinkCoroutine);
            unitBigKBlinkCoroutine = null;
        }

        ApplyUnitBigKEyesOpen();
    }

    private System.Collections.IEnumerator CoUnitBigKBlinkLoop()
    {
        while (enableUnitBigKBlink)
        {
            float min = Mathf.Max(0.5f, blinkIntervalMinSeconds);
            float max = Mathf.Max(min, blinkIntervalMaxSeconds);
            float wait = Random.Range(min, max);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return CoPlayUnitBigKBlinkOnce();
            if (Random.value < blinkDoubleChance)
            {
                float gap = Mathf.Max(0.02f, blinkDoubleGapSeconds);
                float gapElapsed = 0f;
                while (gapElapsed < gap)
                {
                    gapElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return CoPlayUnitBigKBlinkOnce();
            }
        }

        unitBigKBlinkCoroutine = null;
    }

    private System.Collections.IEnumerator CoPlayUnitBigKBlinkOnce()
    {
        if (unitBigKFaceImage == null ||
            unitBigKEyesClosedSprite == null ||
            unitBigKEyesOpenSprite == null)
        {
            yield break;
        }

        unitBigKFaceImage.sprite = unitBigKEyesClosedSprite;
        float closed = Mathf.Max(0.02f, blinkClosedSeconds);
        float elapsed = 0f;
        while (elapsed < closed)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyUnitBigKEyesOpen();
    }

    private void ApplyUnitBigKEyesOpen()
    {
        if (unitBigKFaceImage != null && unitBigKEyesOpenSprite != null)
        {
            unitBigKFaceImage.sprite = unitBigKEyesOpenSprite;
        }
    }

    private void TriggerHighJumpEffect()
    {
        if (activeUnitBigKEffectMode == UnitBigKEffectMode.ClickReact || unitBigKSwimPaused)
        {
            return;
        }

        StartUnitBigKEffect(UnitBigKEffectMode.High, CoPlayHighJumpEffect());
    }

    private void TriggerMidLookEffect(Button targetButton)
    {
        if (targetButton == null ||
            activeUnitBigKEffectMode == UnitBigKEffectMode.High ||
            activeUnitBigKEffectMode == UnitBigKEffectMode.ClickReact ||
            unitBigKSwimPaused)
        {
            return;
        }

        StartUnitBigKEffect(UnitBigKEffectMode.Mid, CoPlayMidLookEffect(targetButton));
    }

    private void StartUnitBigKEffect(UnitBigKEffectMode mode, System.Collections.IEnumerator routine)
    {
        StopUnitBigKEffect(false);
        activeUnitBigKEffectMode = mode;
        unitBigKEffectCoroutine = StartCoroutine(routine);
    }

    private void StopUnitBigKEffect(bool resetPose)
    {
        if (unitBigKEffectCoroutine != null)
        {
            StopCoroutine(unitBigKEffectCoroutine);
            unitBigKEffectCoroutine = null;
        }

        bool wasClickReact = activeUnitBigKEffectMode == UnitBigKEffectMode.ClickReact;
        activeUnitBigKEffectMode = UnitBigKEffectMode.None;
        if (wasClickReact || resetPose)
        {
            unitBigKSwimPaused = false;
            if (wasClickReact)
            {
                ApplyUnitBigKEyesOpen();
                if (isActiveAndEnabled)
                {
                    RestartUnitBigKBlinkLoop();
                }
            }

            if (unitBigKImage != null && wasClickReact && !resetPose)
            {
                unitBigKImage.anchoredPosition = unitBigKImageBaseAnchoredPosition;
                ApplyUnitBigKImageSpin();
            }
        }

        if (resetPose)
        {
            ApplyUnitBigKBasePose();
        }
    }

    private void ApplyUnitBigKBasePose()
    {
        if (unitBigKObject != null)
        {
            unitBigKObject.anchoredPosition = unitBigKObjectBaseAnchoredPosition;
            unitBigKObject.localRotation = Quaternion.Euler(unitBigKObjectBaseEuler.x, midBaseRotationY, midBaseRotationZ);
        }

        if (unitBigKImage != null)
        {
            unitBigKImage.anchoredPosition = unitBigKImageBaseAnchoredPosition;
            unitBigKImage.localEulerAngles = unitBigKImageBaseLocalEuler;
        }

        unitBigKSpinDegrees = 0f;
        ApplyUnitBigKEyesOpen();
    }

    private System.Collections.IEnumerator CoPlayHighJumpEffect()
    {
        if (unitBigKImage == null)
        {
            activeUnitBigKEffectMode = UnitBigKEffectMode.None;
            yield break;
        }

        Vector2 basePos = unitBigKImageBaseAnchoredPosition;
        float duration = Mathf.Max(0.05f, highJumpDurationSeconds);
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float y;
            if (t < 0.5f)
            {
                float upT = half <= 0f ? 1f : Mathf.Clamp01(elapsed / half);
                y = Mathf.Lerp(0f, highJumpHeight, EaseOutCubic(upT));
            }
            else
            {
                float downT = half <= 0f ? 1f : Mathf.Clamp01((elapsed - half) / half);
                y = Mathf.Lerp(highJumpHeight, 0f, EaseInCubic(downT));
            }

            unitBigKImage.anchoredPosition = basePos + new Vector2(0f, y);
            yield return null;
        }

        unitBigKImage.anchoredPosition = basePos;
        activeUnitBigKEffectMode = UnitBigKEffectMode.None;
        unitBigKEffectCoroutine = null;
    }

    private float CalculateLookRotationZ(RectTransform targetRect)
    {
        if (canvasRect == null || unitBigKObject == null || targetRect == null)
        {
            return midBaseRotationZ;
        }

        Vector2 unitLocal = canvasRect.InverseTransformPoint(unitBigKObject.position);
        Vector2 targetLocal = canvasRect.InverseTransformPoint(targetRect.position);
        Vector2 direction = targetLocal - unitLocal;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return midBaseRotationZ;
        }

        float desiredZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + midLookForwardAngleOffsetZ;
        float limitedDelta = Mathf.Clamp(Mathf.DeltaAngle(midBaseRotationZ, desiredZ), -Mathf.Abs(midTurnAngle), Mathf.Abs(midTurnAngle));
        if (midInvertLookWhenFlippedY)
        {
            limitedDelta = -limitedDelta;
        }

        return midBaseRotationZ + limitedDelta;
    }

    private System.Collections.IEnumerator CoPlayMidLookEffect(Button targetButton)
    {
        if (unitBigKObject == null || targetButton == null)
        {
            activeUnitBigKEffectMode = UnitBigKEffectMode.None;
            yield break;
        }

        RectTransform targetRect = targetButton.transform as RectTransform;
        if (targetRect == null)
        {
            activeUnitBigKEffectMode = UnitBigKEffectMode.None;
            yield break;
        }

        float targetZ = CalculateLookRotationZ(targetRect);
        Quaternion startRot = unitBigKObject.localRotation;
        Quaternion endRot = Quaternion.Euler(unitBigKObjectBaseEuler.x, midBaseRotationY, targetZ);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, midTurnDurationSeconds);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            unitBigKObject.localRotation = Quaternion.Slerp(startRot, endRot, EaseOutCubic(t));
            yield return null;
        }

        unitBigKObject.localRotation = endRot;
        activeUnitBigKEffectMode = UnitBigKEffectMode.None;
        unitBigKEffectCoroutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        float p = 1f - Mathf.Clamp01(t);
        return 1f - p * p * p;
    }

    private static float EaseInCubic(float t)
    {
        float p = Mathf.Clamp01(t);
        return p * p * p;
    }

    private Image EnsureOverlayImage(string objectName, Color color, Sprite sprite = null)
    {
        Transform t = canvasRect.Find(objectName);
        GameObject go = t != null ? t.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (go.transform.parent != canvasRect)
        {
            go.transform.SetParent(canvasRect, false);
        }

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = false;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        return image;
    }

    private void SetSpotlightVisible(bool visible)
    {
        spotlightVisible = visible;
        SetImageActive(dimTop, visible);
        SetImageActive(dimBottom, visible);
        SetImageActive(dimLeft, visible);
        SetImageActive(dimRight, visible);
        SetImageActive(spotlightRayImage, visible);
        SetImageActive(spotlightCoreImage, visible);
    }

    private static void SetImageActive(Image image, bool active)
    {
        if (image != null && image.gameObject.activeSelf != active)
        {
            image.gameObject.SetActive(active);
        }
    }

    private static Sprite EnsureSpotlightCoreSprite()
    {
        if (spotlightCoreSprite == null)
        {
            spotlightCoreSprite = CreateRadialSprite(
                192,
                0.5f,
                0.5f,
                innerAlpha: 1f,
                outerAlpha: 0f,
                exponent: 1.85f);
        }

        return spotlightCoreSprite;
    }

    private static Sprite EnsureSpotlightRaySprite()
    {
        if (spotlightRaySprite == null)
        {
            spotlightRaySprite = CreateRaySprite(320, 192);
        }

        return spotlightRaySprite;
    }

    private static Sprite EnsureUnitBigKShadowSprite()
    {
        if (unitBigKShadowSprite == null)
        {
            unitBigKShadowSprite = CreateEllipticShadowSprite(192, 64, 2.4f);
        }

        return unitBigKShadowSprite;
    }

    private static Sprite CreateRadialSprite(int size, float centerX, float centerY, float innerAlpha, float outerAlpha, float exponent)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half - (centerX - 0.5f);
                float ny = (y - half) / half - (centerY - 0.5f);
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float t = Mathf.Clamp01(1f - r);
                float a = Mathf.Lerp(outerAlpha, innerAlpha, Mathf.Pow(t, exponent));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRaySprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float halfH = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float ny = Mathf.Abs((y - halfH) / halfH);
            float sideFade = Mathf.Pow(1f - Mathf.Clamp01(ny), 1.7f);
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)(width - 1);
                float tipFade = Mathf.Pow(1f - nx, 2.1f);
                float tailFade = Mathf.Pow(nx, 0.7f);
                float alpha = sideFade * tipFade * tailFade;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0f, 0.5f), 100f);
    }

    private static Sprite CreateEllipticShadowSprite(int width, int height, float falloff)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float halfW = (width - 1) * 0.5f;
        float halfH = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - halfW) / halfW;
                float ny = (y - halfH) / halfH;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), Mathf.Max(0.1f, falloff));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
