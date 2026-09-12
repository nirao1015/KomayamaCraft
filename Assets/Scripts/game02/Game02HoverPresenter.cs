using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ホバー表示の見た目制御雛形。
/// 遅延表示や表示先パネルへの配置を後続実装で行う。
/// </summary>
public class Game02HoverPresenter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform panelCanvasRoot;
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Timing")]
    [SerializeField] private float showDelaySeconds = 0.25f;

    [Header("Layout")]
    [SerializeField] private float horizontalPadding = 20f;
    [SerializeField] private float verticalPadding = 16f;
    [SerializeField] private float lineSpacing = 8f;
    [SerializeField] private float minWidth = 160f;
    [SerializeField] private float maxWidth = 520f;
    [SerializeField] private float minHeight = 80f;
    [SerializeField] private Vector2 tooltipScreenOffset = new Vector2(16f, -16f);

    [Header("Hover Cursor")]
    [SerializeField] private bool enableHoverCursorSwap;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 hoverCursorHotspot = Vector2.zero;
    [SerializeField] private CursorMode hoverCursorMode = CursorMode.Auto;
    [SerializeField] private bool useOverlayCursorFallback = true;
    [Header("Hover2HitArea Override")]
    [SerializeField] private string hover2HitAreaName = "Hover2HitArea";
    [SerializeField] private string[] hover2CanvasRootNames = { "UnitCanvas", "ItemCanvas", "PanelCanvas", "FieldCanvas" };
    [SerializeField] private Texture2D hover2CursorTexture;
    [SerializeField] private Vector2 hover2CursorHotspotOffset = Vector2.zero;
    [SerializeField] private Vector2 hover2TooltipOffsetDelta = Vector2.zero;

    private Coroutine showRoutine;
    private Game02HoverContentProvider pendingContentProvider;
    private Image hoverCursorOverlayImage;
    private Sprite hoverCursorOverlaySprite;
    private bool isHoverCursorActive;
    private Texture2D transparentCursorTexture;
    private bool isHover2CursorMode;

    private void Awake()
    {
        SetVisible(false);
    }

    private void OnDisable()
    {
        ResetHoverCursor();
    }

    public void Configure(
        RectTransform panelRoot,
        RectTransform tooltipRect,
        TMP_Text title,
        TMP_Text description)
    {
        panelCanvasRoot = panelRoot;
        tooltipRoot = tooltipRect;
        titleText = title;
        descriptionText = description;
    }

    public void ConfigureTiming(float tooltipShowDelaySeconds)
    {
        showDelaySeconds = Mathf.Max(0f, tooltipShowDelaySeconds);
    }

    public void ConfigureHoverCursor(
        bool enabled,
        Texture2D texture,
        Vector2 hotspot,
        CursorMode mode,
        bool useOverlayFallback)
    {
        enableHoverCursorSwap = enabled;
        hoverCursorTexture = texture;
        hoverCursorHotspot = hotspot;
        hoverCursorMode = mode;
        useOverlayCursorFallback = useOverlayFallback;
        RebuildOverlayCursorSprite();
    }

    public void RequestShow(Game02HoverContentProvider contentProvider, Vector2 pointerScreenPosition, Transform hoverSource = null)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        isHover2CursorMode = IsHover2Target(hoverSource);
        pendingContentProvider = contentProvider;
        SetTooltipPosition(pointerScreenPosition);
        ApplyHoverCursor(pointerScreenPosition);

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowWithDelay());
    }

    public void RequestShow(Game02HoverContentProvider contentProvider)
    {
        RequestShow(contentProvider, GetCurrentPointerScreenPosition(), null);
    }

    public void UpdateHoverPosition(Vector2 pointerScreenPosition)
    {
        UpdateHoverPosition(pointerScreenPosition, null);
    }

    public void UpdateHoverPosition(Vector2 pointerScreenPosition, Transform hoverSource)
    {
        if (hoverSource != null)
        {
            isHover2CursorMode = IsHover2Target(hoverSource);
        }

        SetTooltipPosition(pointerScreenPosition);
        UpdateOverlayCursorPosition(pointerScreenPosition);
    }

    public void RequestHide()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        pendingContentProvider = null;
        SetVisible(false);
        ResetHoverCursor();
        isHover2CursorMode = false;
    }

    private IEnumerator ShowWithDelay()
    {
        float delay = Mathf.Max(0f, showDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        ApplyContent(pendingContentProvider);
        SetVisible(true);
        showRoutine = null;
    }

    private void ApplyContent(Game02HoverContentProvider contentProvider)
    {
        string title = contentProvider != null ? contentProvider.GetTitle() : string.Empty;
        string description = contentProvider != null ? contentProvider.GetDescription() : string.Empty;

        if (titleText != null)
        {
            titleText.text = title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }

        UpdateTooltipSize(title, description);
    }

    private void SetTooltipPosition(Vector2 pointerScreenPosition)
    {
        if (tooltipRoot == null || panelCanvasRoot == null)
        {
            return;
        }

        Canvas canvas = panelCanvasRoot.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelCanvasRoot,
            pointerScreenPosition,
            eventCamera,
            out Vector2 pointerLocalPoint);

        Vector3 localPosition = BuildTooltipLocalPosition(pointerLocalPoint);
        localPosition = ClampLocalPositionInPanel(localPosition);
        tooltipRoot.localPosition = localPosition;
    }

    private Vector3 BuildTooltipLocalPosition(Vector2 pointerLocalPoint)
    {
        Rect panelRect = panelCanvasRoot.rect;
        Rect tooltipRect = tooltipRoot.rect;
        Vector2 pivot = tooltipRoot.pivot;

        float panelLeft = -panelRect.width * panelCanvasRoot.pivot.x;
        float panelRight = panelRect.width * (1f - panelCanvasRoot.pivot.x);
        float panelTop = panelRect.height * (1f - panelCanvasRoot.pivot.y);
        float panelBottom = -panelRect.height * panelCanvasRoot.pivot.y;

        float leftExtent = tooltipRect.width * pivot.x;
        float rightExtent = tooltipRect.width * (1f - pivot.x);
        float topExtent = tooltipRect.height * (1f - pivot.y);
        float bottomExtent = tooltipRect.height * pivot.y;

        Vector2 effectiveOffset = GetEffectiveTooltipOffset();
        float gapX = Mathf.Abs(effectiveOffset.x);
        float gapY = Mathf.Abs(effectiveOffset.y);

        float requiredRightSpace = gapX + leftExtent + rightExtent;
        float requiredLeftSpace = gapX + leftExtent + rightExtent;
        float requiredDownSpace = gapY + topExtent + bottomExtent;
        float requiredUpSpace = gapY + topExtent + bottomExtent;

        float availableRightSpace = panelRight - pointerLocalPoint.x;
        float availableLeftSpace = pointerLocalPoint.x - panelLeft;
        float availableDownSpace = pointerLocalPoint.y - panelBottom;
        float availableUpSpace = panelTop - pointerLocalPoint.y;

        // 基本は右下。必要スペースを満たせないときのみ左/上へ切り替える。
        bool placeRight = availableRightSpace >= requiredRightSpace || availableRightSpace >= availableLeftSpace;
        bool placeDown = availableDownSpace >= requiredDownSpace || availableDownSpace >= availableUpSpace;

        float finalX = placeRight
            ? pointerLocalPoint.x + gapX + leftExtent
            : pointerLocalPoint.x - gapX - rightExtent;

        float finalY = placeDown
            ? pointerLocalPoint.y - gapY - topExtent
            : pointerLocalPoint.y + gapY + bottomExtent;

        return new Vector3(finalX, finalY, tooltipRoot.localPosition.z);
    }

    private Vector3 ClampLocalPositionInPanel(Vector3 desiredLocalPosition)
    {
        if (panelCanvasRoot == null || tooltipRoot == null)
        {
            return desiredLocalPosition;
        }

        Rect panelRect = panelCanvasRoot.rect;
        Rect tooltipRect = tooltipRoot.rect;

        float panelLeft = -panelRect.width * panelCanvasRoot.pivot.x;
        float panelRight = panelRect.width * (1f - panelCanvasRoot.pivot.x);
        float panelBottom = -panelRect.height * panelCanvasRoot.pivot.y;
        float panelTop = panelRect.height * (1f - panelCanvasRoot.pivot.y);

        float leftExtent = Mathf.Max(1f, tooltipRect.width * tooltipRoot.pivot.x);
        float rightExtent = Mathf.Max(1f, tooltipRect.width * (1f - tooltipRoot.pivot.x));
        float bottomExtent = Mathf.Max(1f, tooltipRect.height * tooltipRoot.pivot.y);
        float topExtent = Mathf.Max(1f, tooltipRect.height * (1f - tooltipRoot.pivot.y));

        float clampedX = Mathf.Clamp(desiredLocalPosition.x, panelLeft + leftExtent, panelRight - rightExtent);
        float clampedY = Mathf.Clamp(desiredLocalPosition.y, panelBottom + bottomExtent, panelTop - topExtent);

        return new Vector3(clampedX, clampedY, desiredLocalPosition.z);
    }

    private void UpdateTooltipSize(string title, string description)
    {
        if (tooltipRoot == null)
        {
            return;
        }

        float contentWidthForMeasure = Mathf.Max(1f, maxWidth - horizontalPadding * 2f);
        float titleWidth = GetPreferredWidth(titleText, title, contentWidthForMeasure);
        float descriptionWidth = GetPreferredWidth(descriptionText, description, contentWidthForMeasure);

        float targetWidth = Mathf.Max(titleWidth, descriptionWidth) + horizontalPadding * 2f;
        targetWidth = Mathf.Clamp(targetWidth, minWidth, maxWidth);
        float contentWidth = Mathf.Max(1f, targetWidth - horizontalPadding * 2f);

        float titleHeight = GetPreferredHeight(titleText, title, contentWidth);
        float descriptionHeight = GetPreferredHeight(descriptionText, description, contentWidth);
        bool hasBothLines = titleHeight > 0f && descriptionHeight > 0f;

        float targetHeight = verticalPadding * 2f + titleHeight + descriptionHeight + (hasBothLines ? lineSpacing : 0f);
        targetHeight = Mathf.Max(minHeight, targetHeight);

        tooltipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        tooltipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private static float GetPreferredWidth(TMP_Text textComponent, string value, float maxContentWidth)
    {
        if (textComponent == null || string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        Vector2 preferred = textComponent.GetPreferredValues(value, maxContentWidth, Mathf.Infinity);
        return preferred.x;
    }

    private static float GetPreferredHeight(TMP_Text textComponent, string value, float targetContentWidth)
    {
        if (textComponent == null || string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        Vector2 preferred = textComponent.GetPreferredValues(value, targetContentWidth, Mathf.Infinity);
        return preferred.y;
    }

    private void SetVisible(bool visible)
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(visible);
        }
    }

    private void ApplyHoverCursor(Vector2 pointerScreenPosition)
    {
        Texture2D effectiveCursorTexture = GetEffectiveCursorTexture();
        if (!enableHoverCursorSwap || effectiveCursorTexture == null)
        {
            return;
        }

        Vector2 effectiveHotspot = GetEffectiveCursorHotspot();
        float maxX = Mathf.Max(0f, effectiveCursorTexture.width - 1);
        float maxY = Mathf.Max(0f, effectiveCursorTexture.height - 1);
        Vector2 safeHotspot = new Vector2(
            Mathf.Clamp(effectiveHotspot.x, 0f, maxX),
            Mathf.Clamp(effectiveHotspot.y, 0f, maxY));

        CursorMode effectiveMode = hoverCursorMode;
        if (effectiveMode == CursorMode.ForceSoftware && !effectiveCursorTexture.isReadable)
        {
            effectiveMode = CursorMode.Auto;
        }

        if (useOverlayCursorFallback)
        {
            EnsureTransparentCursorTexture();
            if (transparentCursorTexture != null)
            {
                Cursor.SetCursor(transparentCursorTexture, Vector2.zero, CursorMode.ForceSoftware);
            }
            else
            {
                Cursor.SetCursor(effectiveCursorTexture, safeHotspot, effectiveMode);
            }
        }
        else
        {
            Cursor.SetCursor(effectiveCursorTexture, safeHotspot, effectiveMode);
        }

        isHoverCursorActive = true;

        if (useOverlayCursorFallback)
        {
            EnsureOverlayCursorImage();
            if (hoverCursorOverlayImage != null)
            {
                hoverCursorOverlayImage.gameObject.SetActive(true);
                UpdateOverlayCursorPosition(pointerScreenPosition);
            }

            Cursor.visible = true;
        }
    }

    private void ResetHoverCursor()
    {
        if (!isHoverCursorActive)
        {
            return;
        }

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (hoverCursorOverlayImage != null)
        {
            hoverCursorOverlayImage.gameObject.SetActive(false);
        }

        Cursor.visible = true;
        isHoverCursorActive = false;
    }

    private void EnsureTransparentCursorTexture()
    {
        if (transparentCursorTexture != null)
        {
            return;
        }

        transparentCursorTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 0);
        }

        transparentCursorTexture.SetPixels32(pixels);
        transparentCursorTexture.Apply(false, false);
    }

    private void EnsureOverlayCursorImage()
    {
        if (panelCanvasRoot == null)
        {
            return;
        }

        if (hoverCursorOverlayImage == null)
        {
            Transform existing = panelCanvasRoot.Find("HoverCursorOverlay");
            RectTransform overlayRect;
            if (existing == null)
            {
                GameObject go = new GameObject("HoverCursorOverlay", typeof(RectTransform), typeof(Image));
                overlayRect = go.GetComponent<RectTransform>();
                overlayRect.SetParent(panelCanvasRoot, false);
            }
            else
            {
                overlayRect = existing.GetComponent<RectTransform>();
            }

            hoverCursorOverlayImage = overlayRect.GetComponent<Image>();
            hoverCursorOverlayImage.raycastTarget = false;
            hoverCursorOverlayImage.color = Color.white;
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(0f, 0f);
            overlayRect.pivot = new Vector2(0f, 1f);
            hoverCursorOverlayImage.gameObject.SetActive(false);
            overlayRect.SetAsLastSibling();
        }

        RebuildOverlayCursorSprite();
        hoverCursorOverlayImage.sprite = hoverCursorOverlaySprite;
        if (hoverCursorOverlaySprite != null)
        {
            hoverCursorOverlayImage.SetNativeSize();
        }
    }

    private void RebuildOverlayCursorSprite()
    {
        Texture2D effectiveCursorTexture = GetEffectiveCursorTexture();
        if (effectiveCursorTexture == null)
        {
            hoverCursorOverlaySprite = null;
            return;
        }

        hoverCursorOverlaySprite = Sprite.Create(
            effectiveCursorTexture,
            new Rect(0f, 0f, effectiveCursorTexture.width, effectiveCursorTexture.height),
            new Vector2(0f, 1f),
            100f);
    }

    private void UpdateOverlayCursorPosition(Vector2 pointerScreenPosition)
    {
        if (!useOverlayCursorFallback || hoverCursorOverlayImage == null || panelCanvasRoot == null)
        {
            return;
        }

        Canvas canvas = panelCanvasRoot.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        RectTransform overlayRect = hoverCursorOverlayImage.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelCanvasRoot,
            pointerScreenPosition,
            eventCamera,
            out Vector2 localPoint);

        overlayRect.localPosition = new Vector3(
            localPoint.x - GetEffectiveCursorHotspot().x,
            localPoint.y + GetEffectiveCursorHotspot().y,
            overlayRect.localPosition.z);
    }

    private bool IsHover2Target(Transform hoverSource)
    {
        if (hoverSource == null || string.IsNullOrEmpty(hover2HitAreaName))
        {
            return false;
        }

        if (!string.Equals(hoverSource.name, hover2HitAreaName, System.StringComparison.Ordinal))
        {
            return false;
        }

        Transform current = hoverSource;
        while (current != null)
        {
            if (IsHover2CanvasRootName(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsHover2CanvasRootName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName) || hover2CanvasRootNames == null)
        {
            return false;
        }

        for (int i = 0; i < hover2CanvasRootNames.Length; i++)
        {
            string rootName = hover2CanvasRootNames[i];
            if (string.IsNullOrEmpty(rootName))
            {
                continue;
            }

            if (string.Equals(targetName, rootName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private Texture2D GetEffectiveCursorTexture()
    {
        if (isHover2CursorMode && hover2CursorTexture != null)
        {
            return hover2CursorTexture;
        }

        return hoverCursorTexture;
    }

    private Vector2 GetEffectiveCursorHotspot()
    {
        if (isHover2CursorMode)
        {
            return hoverCursorHotspot + hover2CursorHotspotOffset;
        }

        return hoverCursorHotspot;
    }

    private Vector2 GetEffectiveTooltipOffset()
    {
        if (isHover2CursorMode)
        {
            return tooltipScreenOffset + hover2TooltipOffsetDelta;
        }

        return tooltipScreenOffset;
    }

    private static Vector2 GetCurrentPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }

        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif
        return Vector2.zero;
    }
}
