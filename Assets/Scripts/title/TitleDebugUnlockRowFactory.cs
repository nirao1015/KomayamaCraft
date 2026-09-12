using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ConfigPanel 配下にデバッグ用「全ゲーム解放」行を実行時生成する（手編集シーン YAML を使わない）。
/// </summary>
public static class TitleDebugUnlockRowFactory
{
    private const string RowName = "DebugUnlockRow";
    private const string HitAreaName = "DebugUnlockHitArea";
    private const string ToggleName = "UnlockAllGamesToggle";
    private const string CheckmarkName = "Checkmark";
    private const string ImageCheckmarkName = "ImageCheckmark";
    private const string LabelName = "DebugUnlockLabel";

    private static Sprite cachedUiWhiteSprite;

    public static GameObject ResolveCheckmarkFromRow(Transform row)
    {
        if (row == null)
        {
            return null;
        }

        Transform check = row.Find($"{ToggleName}/{ImageCheckmarkName}");
        if (check == null)
        {
            check = row.Find($"{ToggleName}/{CheckmarkName}");
        }

        return check != null ? check.gameObject : null;
    }

    public static void DestroyRowIfAny(Transform configPanel)
    {
        if (configPanel == null)
        {
            return;
        }

        Transform existing = configPanel.Find(RowName);
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing.gameObject);
        }
    }

    public static void EnsureRow(Transform configPanel, TitleTransitionManager transitionManager)
    {
        if (configPanel == null || transitionManager == null)
        {
            return;
        }

        Transform existing = configPanel.Find(RowName);
        if (existing != null)
        {
            BindExistingRow(existing, transitionManager);
            return;
        }

        BuildRow(configPanel, transitionManager);
    }

    private static void BindExistingRow(Transform row, TitleTransitionManager transitionManager)
    {
        if (row is RectTransform rowRect && rowRect.localScale == Vector3.zero)
        {
            rowRect.localScale = Vector3.one;
        }

        Transform hit = row.Find(HitAreaName);
        Button button = hit != null ? hit.GetComponent<Button>() : row.GetComponentInChildren<Button>(true);
        // チェック表示は Inspector 指定を優先（ImageCheckmark 等を実行時に差し替えない）
        transitionManager.RegisterDebugUnlockControls(button, null);
    }

    private static void BuildRow(Transform configPanel, TitleTransitionManager transitionManager)
    {
        FontStyles labelStyle = FontStyles.Normal;
        Color labelColor = Color.white;
        TMP_FontAsset labelFont = null;
        TextMeshProUGUI fontSource = configPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        if (fontSource != null)
        {
            labelFont = fontSource.font;
            labelStyle = fontSource.fontStyle;
            labelColor = fontSource.color;
        }

        GameObject rowGo = new GameObject(RowName, typeof(RectTransform));
        rowGo.layer = configPanel.gameObject.layer;
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.SetParent(configPanel, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(640f, 56f);
        rowRect.anchoredPosition = new Vector2(0f, -152f);

        GameObject labelGo = CreateTmpLabel(rowRect, labelFont, labelStyle, labelColor);
        GameObject toggleGo = CreateToggleVisual(rowRect, configPanel, labelFont, labelColor);
        Button hitButton = CreateHitArea(rowRect, transitionManager);

        transitionManager.RegisterDebugUnlockControls(
            hitButton,
            toggleGo.transform.Find(CheckmarkName)?.gameObject);

        labelGo.transform.SetAsFirstSibling();
        toggleGo.transform.SetSiblingIndex(1);
        hitButton.transform.SetAsLastSibling();
    }

    private static Sprite ResolveFrameSprite(Transform configPanel)
    {
        Image[] images = configPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.sprite != null)
            {
                return image.sprite;
            }
        }

        return GetOrCreateUiWhiteSprite();
    }

    private static Sprite GetOrCreateUiWhiteSprite()
    {
        if (cachedUiWhiteSprite != null)
        {
            return cachedUiWhiteSprite;
        }

        Texture2D tex = Texture2D.whiteTexture;
        cachedUiWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return cachedUiWhiteSprite;
    }

    private static GameObject CreateTmpLabel(RectTransform parent, TMP_FontAsset font, FontStyles fontStyle, Color color)
    {
        GameObject labelGo = new GameObject(LabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.layer = parent.gameObject.layer;
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(80f, 0f);
        labelRect.sizeDelta = new Vector2(420f, 48f);

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.text = "全ゲーム解放";
        tmp.fontSize = 28f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null)
        {
            tmp.font = font;
            tmp.fontStyle = fontStyle;
        }

        return labelGo;
    }

    private static GameObject CreateToggleVisual(
        RectTransform parent,
        Transform configPanel,
        TMP_FontAsset font,
        Color labelColor)
    {
        Sprite frameSprite = ResolveFrameSprite(configPanel);
        Color frameColor = Color.white;
        Image[] images = configPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].sprite != null)
            {
                frameColor = images[i].color;
                break;
            }
        }

        GameObject toggleGo = new GameObject(ToggleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        toggleGo.layer = parent.gameObject.layer;
        RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.SetParent(parent, false);
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-40f, 0f);
        toggleRect.sizeDelta = new Vector2(40f, 40f);

        Image frameImage = toggleGo.GetComponent<Image>();
        frameImage.raycastTarget = false;
        frameImage.sprite = frameSprite;
        frameImage.color = frameColor;
        frameImage.type = Image.Type.Simple;

        GameObject checkGo = CreateCheckmarkChild(toggleRect, font, labelColor);
        checkGo.SetActive(TitleTransitionManager.IsDebugUnlockAllEnabled());

        return toggleGo;
    }

    private static GameObject CreateCheckmarkChild(RectTransform parent, TMP_FontAsset font, Color color)
    {
        GameObject checkGo = new GameObject(CheckmarkName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        checkGo.layer = parent.gameObject.layer;
        RectTransform checkRect = checkGo.GetComponent<RectTransform>();
        checkRect.SetParent(parent, false);
        checkRect.anchorMin = checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(36f, 36f);
        checkRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = checkGo.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.text = "\u2713";
        tmp.fontSize = 32f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null)
        {
            tmp.font = font;
        }

        return checkGo;
    }

    private static Button CreateHitArea(RectTransform parent, TitleTransitionManager transitionManager)
    {
        Sprite hitSprite = GetOrCreateUiWhiteSprite();

        GameObject hitGo = new GameObject(HitAreaName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hitGo.layer = parent.gameObject.layer;
        RectTransform hitRect = hitGo.GetComponent<RectTransform>();
        hitRect.SetParent(parent, false);
        hitRect.anchorMin = Vector2.zero;
        hitRect.anchorMax = Vector2.one;
        hitRect.offsetMin = Vector2.zero;
        hitRect.offsetMax = Vector2.zero;

        Image hitImage = hitGo.GetComponent<Image>();
        hitImage.raycastTarget = true;
        hitImage.sprite = hitSprite;
        hitImage.color = new Color(1f, 1f, 1f, 0f);

        Button button = hitGo.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitImage;
        return button;
    }
}
