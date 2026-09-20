using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトルの新規／続きボタンを <c>Canvas</c> 直下（Game01Button と同階層）へ置き、
/// 別 Canvas の実行時風オーバーレイ <c>KomayamaCraftTitleEntry</c> を廃止する。
/// </summary>
public static class KomayamaCraftM9TitleSetup
{
    private const string TitleScenePath = "Assets/Scenes/title_scene.unity";

    [MenuItem("KomayamaCraft/M9をタイトルへ接続", false, 44)]
    public static void SetupFromMenu()
    {
        Scene title = SceneManager.GetActiveScene();
        if (title.path != TitleScenePath)
        {
            title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        GameObject creditsGo = GameObject.Find("CreditsButton");
        if (canvasGo == null || creditsGo == null)
        {
            Debug.LogError("[KomayamaCraftM9] Canvas または CreditsButton が見つかりません。");
            return;
        }

        DestroyIfExists(canvasGo.transform, "NewGameButton");
        DestroyIfExists(canvasGo.transform, "ContinueButton");
        DestroyIfExists(canvasGo.transform, "SlotPanel");
        DestroyIfExists(canvasGo.transform, "ConfirmPanel");

        Button newGameButton = DuplicateCreditsButton(
            creditsGo,
            canvasGo.transform,
            "NewGameButton",
            "新規開始",
            new Vector2(-160f, -460f));
        Button continueButton = DuplicateCreditsButton(
            creditsGo,
            canvasGo.transform,
            "ContinueButton",
            "続きから",
            new Vector2(160f, -460f));

        GameObject oldRoot = GameObject.Find("KomayamaCraftTitleEntry");
        GameObject slotPanel;
        GameObject confirmPanel;
        if (oldRoot != null)
        {
            slotPanel = TakeOrCreatePanel(oldRoot.transform, canvasGo.transform, "SlotPanel");
            confirmPanel = TakeOrCreatePanel(oldRoot.transform, canvasGo.transform, "ConfirmPanel");
        }
        else
        {
            slotPanel = EnsurePanelUnderCanvas(canvasGo.transform, "SlotPanel", new Color(0.04f, 0.06f, 0.1f, 0.72f));
            confirmPanel = EnsurePanelUnderCanvas(canvasGo.transform, "ConfirmPanel", new Color(0.02f, 0.03f, 0.06f, 0.82f));
            EnsureSlotContents(slotPanel.transform);
            EnsureConfirmContents(confirmPanel.transform);
        }

        slotPanel.SetActive(false);
        confirmPanel.SetActive(false);

        Transform config = canvasGo.transform.Find("ConfigButton");
        if (config != null)
        {
            newGameButton.transform.SetSiblingIndex(config.GetSiblingIndex());
            continueButton.transform.SetSiblingIndex(config.GetSiblingIndex());
        }

        slotPanel.transform.SetAsLastSibling();
        confirmPanel.transform.SetAsLastSibling();

        GameObject host = GameObject.Find("TitleSceneController");
        if (host == null)
        {
            host = canvasGo;
        }

        KomayamaTitleEntry entry = host.GetComponent<KomayamaTitleEntry>();
        if (entry == null)
        {
            entry = host.AddComponent<KomayamaTitleEntry>();
        }

        WireEntry(entry, newGameButton, continueButton, slotPanel, confirmPanel);

        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        // 旧オーバーレイ上に残った Entry を Canvas 以外から除去
        KomayamaTitleEntry[] entries = Object.FindObjectsByType<KomayamaTitleEntry>(FindObjectsSortMode.None);
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i] != entry)
            {
                Object.DestroyImmediate(entries[i]);
            }
        }

        EditorSceneManager.MarkSceneDirty(title);
        EditorSceneManager.SaveScene(title);
        Debug.Log("[KomayamaCraftM9] NewGame/Continue を Canvas 直下へ配置し、KomayamaCraftTitleEntry オーバーレイを除去しました。");
    }

    private static void WireEntry(
        KomayamaTitleEntry entry,
        Button newGameButton,
        Button continueButton,
        GameObject slotPanel,
        GameObject confirmPanel)
    {
        TMP_Text slotTitle = FindTmp(slotPanel.transform, "SlotPanelTitle");
        Button slot1 = FindButton(slotPanel.transform, "Slot1Button");
        Button slot2 = FindButton(slotPanel.transform, "Slot2Button");
        Button slot3 = FindButton(slotPanel.transform, "Slot3Button");
        Button slotBack = FindButton(slotPanel.transform, "SlotBackButton");
        TMP_Text confirmText = FindTmp(confirmPanel.transform, "ConfirmText");
        Button confirmYes = FindButton(confirmPanel.transform, "ConfirmYesButton");
        Button confirmNo = FindButton(confirmPanel.transform, "ConfirmNoButton");

        SerializedObject serialized = new SerializedObject(entry);
        serialized.FindProperty("newGameButton").objectReferenceValue = newGameButton;
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("slotPanel").objectReferenceValue = slotPanel;
        serialized.FindProperty("slotPanelTitle").objectReferenceValue = slotTitle;
        SerializedProperty slotButtonProp = serialized.FindProperty("slotButtons");
        slotButtonProp.arraySize = 3;
        slotButtonProp.GetArrayElementAtIndex(0).objectReferenceValue = slot1;
        slotButtonProp.GetArrayElementAtIndex(1).objectReferenceValue = slot2;
        slotButtonProp.GetArrayElementAtIndex(2).objectReferenceValue = slot3;
        SerializedProperty slotLabelProp = serialized.FindProperty("slotLabels");
        slotLabelProp.arraySize = 3;
        slotLabelProp.GetArrayElementAtIndex(0).objectReferenceValue =
            slot1 != null ? slot1.GetComponentInChildren<TMP_Text>(true) : null;
        slotLabelProp.GetArrayElementAtIndex(1).objectReferenceValue =
            slot2 != null ? slot2.GetComponentInChildren<TMP_Text>(true) : null;
        slotLabelProp.GetArrayElementAtIndex(2).objectReferenceValue =
            slot3 != null ? slot3.GetComponentInChildren<TMP_Text>(true) : null;
        serialized.FindProperty("slotBackButton").objectReferenceValue = slotBack;
        serialized.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
        serialized.FindProperty("confirmText").objectReferenceValue = confirmText;
        serialized.FindProperty("confirmYesButton").objectReferenceValue = confirmYes;
        serialized.FindProperty("confirmNoButton").objectReferenceValue = confirmNo;
        serialized.FindProperty("craftSceneName").stringValue = "komayama_craft_scene";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button DuplicateCreditsButton(
        GameObject creditsTemplate,
        Transform canvas,
        string name,
        string label,
        Vector2 anchored)
    {
        GameObject go = Object.Instantiate(creditsTemplate, canvas, false);
        go.name = name;
        go.layer = canvas.gameObject.layer;
        SetLayerRecursive(go.transform, canvas.gameObject.layer);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 110f);
        rect.anchoredPosition = anchored;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Button button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
        }

        TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
            tmp.raycastTarget = false;
        }

        return button;
    }

    private static GameObject TakeOrCreatePanel(Transform oldRoot, Transform canvas, string name)
    {
        Transform existing = oldRoot.Find(name);
        if (existing != null)
        {
            existing.SetParent(canvas, false);
            SetLayerRecursive(existing, canvas.gameObject.layer);
            StretchFull(existing.GetComponent<RectTransform>());
            return existing.gameObject;
        }

        Color color = name == "ConfirmPanel"
            ? new Color(0.02f, 0.03f, 0.06f, 0.82f)
            : new Color(0.04f, 0.06f, 0.1f, 0.72f);
        GameObject panel = EnsurePanelUnderCanvas(canvas, name, color);
        if (name == "SlotPanel")
        {
            EnsureSlotContents(panel.transform);
        }
        else
        {
            EnsureConfirmContents(panel.transform);
        }

        return panel;
    }

    private static GameObject EnsurePanelUnderCanvas(Transform canvas, string name, Color color)
    {
        DestroyIfExists(canvas, name);
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = canvas.gameObject.layer;
        panel.transform.SetParent(canvas, false);
        StretchFull(panel.GetComponent<RectTransform>());
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static void EnsureSlotContents(Transform slotPanel)
    {
        EnsureText(slotPanel, "SlotPanelTitle", "スロットを選ぶ", 36f, new Vector2(0f, 220f), new Vector2(720f, 64f));
        EnsurePlainButton(slotPanel, "Slot1Button", "スロット1", new Vector2(0f, 80f), new Vector2(420f, 100f));
        EnsurePlainButton(slotPanel, "Slot2Button", "スロット2", new Vector2(0f, -40f), new Vector2(420f, 100f));
        EnsurePlainButton(slotPanel, "Slot3Button", "スロット3", new Vector2(0f, -160f), new Vector2(420f, 100f));
        EnsurePlainButton(slotPanel, "SlotBackButton", "戻る", new Vector2(0f, -280f), new Vector2(220f, 64f));
    }

    private static void EnsureConfirmContents(Transform confirmPanel)
    {
        EnsureText(
            confirmPanel,
            "ConfirmText",
            "スロットのデータを上書きしますか？",
            32f,
            new Vector2(0f, 40f),
            new Vector2(820f, 80f));
        EnsurePlainButton(confirmPanel, "ConfirmYesButton", "上書きする", new Vector2(-160f, -80f), new Vector2(240f, 64f));
        EnsurePlainButton(confirmPanel, "ConfirmNoButton", "キャンセル", new Vector2(160f, -80f), new Vector2(240f, 64f));
    }

    private static void EnsurePlainButton(Transform parent, string name, string label, Vector2 anchored, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        if (existing == null)
        {
            buttonObject.transform.SetParent(parent, false);
        }

        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchored;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.97f, 0.93f, 0.82f, 0.96f);
        if (buttonObject.GetComponent<Button>() == null)
        {
            buttonObject.AddComponent<Button>();
        }

        Transform labelTransform = buttonObject.transform.Find("Label");
        GameObject labelObject = labelTransform != null
            ? labelTransform.gameObject
            : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        if (labelTransform == null)
        {
            labelObject.transform.SetParent(buttonObject.transform, false);
        }

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        text.raycastTarget = false;
        RectTransform textRect = labelObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        string label,
        float fontSize,
        Vector2 anchored,
        Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        if (existing == null)
        {
            textObject.transform.SetParent(parent, false);
        }

        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchored;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void DestroyIfExists(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].gameObject.layer = layer;
        }
    }

    private static Button FindButton(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static TMP_Text FindTmp(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }
}
