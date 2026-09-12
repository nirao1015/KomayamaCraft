using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KomayamaCraftM9TitleSetup
{
    [MenuItem("KomayamaCraft/M9をタイトルへ接続", false, 44)]
    public static void SetupFromMenu()
    {
        Scene title = SceneManager.GetActiveScene();
        if (title.path != "Assets/Scenes/title_scene.unity")
        {
            title = EditorSceneManager.OpenScene("Assets/Scenes/title_scene.unity", OpenSceneMode.Single);
        }

        GameObject root = GameObject.Find("KomayamaCraftTitleEntry");
        if (root == null)
        {
            root = new GameObject("KomayamaCraftTitleEntry");
            SceneManager.MoveGameObjectToScene(root, title);
        }

        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = root.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        if (root.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (root.GetComponent<GraphicRaycaster>() == null)
        {
            root.AddComponent<GraphicRaycaster>();
        }

        Button newButton = EnsureButton(root.transform, "NewGameButton", "新規開始", new Vector2(-520f, -460f), new Vector2(280f, 72f));
        Button continueButton = EnsureButton(root.transform, "ContinueButton", "続きから", new Vector2(-220f, -460f), new Vector2(280f, 72f));
        GameObject slotPanel = EnsurePanel(root.transform, "SlotPanel", new Color(0.04f, 0.06f, 0.1f, 0.72f));
        TMP_Text slotTitle = EnsureText(slotPanel.transform, "SlotPanelTitle", "スロットを選ぶ", 36f, new Vector2(0f, 220f), new Vector2(720f, 64f));
        Button slot1 = EnsureButton(slotPanel.transform, "Slot1Button", "スロット1", new Vector2(0f, 80f), new Vector2(420f, 100f));
        Button slot2 = EnsureButton(slotPanel.transform, "Slot2Button", "スロット2", new Vector2(0f, -40f), new Vector2(420f, 100f));
        Button slot3 = EnsureButton(slotPanel.transform, "Slot3Button", "スロット3", new Vector2(0f, -160f), new Vector2(420f, 100f));
        Button slotBack = EnsureButton(slotPanel.transform, "SlotBackButton", "戻る", new Vector2(0f, -280f), new Vector2(220f, 64f));
        GameObject confirmPanel = EnsurePanel(root.transform, "ConfirmPanel", new Color(0.02f, 0.03f, 0.06f, 0.82f));
        TMP_Text confirmText = EnsureText(
            confirmPanel.transform,
            "ConfirmText",
            "スロットのデータを上書きしますか？",
            32f,
            new Vector2(0f, 40f),
            new Vector2(820f, 80f));
        Button confirmYes = EnsureButton(confirmPanel.transform, "ConfirmYesButton", "上書きする", new Vector2(-160f, -80f), new Vector2(240f, 64f));
        Button confirmNo = EnsureButton(confirmPanel.transform, "ConfirmNoButton", "キャンセル", new Vector2(160f, -80f), new Vector2(240f, 64f));
        slotPanel.SetActive(false);
        confirmPanel.SetActive(false);

        KomayamaTitleEntry entry = root.GetComponent<KomayamaTitleEntry>();
        if (entry == null)
        {
            entry = root.AddComponent<KomayamaTitleEntry>();
        }

        SerializedObject serialized = new(entry);
        serialized.FindProperty("newGameButton").objectReferenceValue = newButton;
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
        slotLabelProp.GetArrayElementAtIndex(0).objectReferenceValue = slot1.GetComponentInChildren<TMP_Text>(true);
        slotLabelProp.GetArrayElementAtIndex(1).objectReferenceValue = slot2.GetComponentInChildren<TMP_Text>(true);
        slotLabelProp.GetArrayElementAtIndex(2).objectReferenceValue = slot3.GetComponentInChildren<TMP_Text>(true);
        serialized.FindProperty("slotBackButton").objectReferenceValue = slotBack;
        serialized.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
        serialized.FindProperty("confirmText").objectReferenceValue = confirmText;
        serialized.FindProperty("confirmYesButton").objectReferenceValue = confirmYes;
        serialized.FindProperty("confirmNoButton").objectReferenceValue = confirmNo;
        serialized.FindProperty("craftSceneName").stringValue = "komayama_craft_scene";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystem, title);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(title);
        EditorSceneManager.SaveScene(title);
        Debug.Log("[KomayamaCraftM9] Title new/continue slot select is ready.");
    }

    private static GameObject EnsurePanel(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject panel = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            panel.transform.SetParent(parent, false);
        }

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.AddComponent<Image>();
        }

        image.color = color;
        return panel;
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
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            textObject.transform.SetParent(parent, false);
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchored;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        text.text = label;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        return text;
    }

    private static Button EnsureButton(Transform parent, string name, string label, Vector2 anchored, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            buttonObject.transform.SetParent(parent, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchored;
        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
        {
            image = buttonObject.AddComponent<Image>();
        }

        image.color = new Color(0.97f, 0.93f, 0.82f, 0.96f);
        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        Transform labelTransform = buttonObject.transform.Find("Label");
        GameObject labelObject = labelTransform != null
            ? labelTransform.gameObject
            : new GameObject("Label", typeof(RectTransform));
        if (labelTransform == null)
        {
            labelObject.transform.SetParent(buttonObject.transform, false);
        }

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = labelObject.AddComponent<TextMeshProUGUI>();
        }

        text.text = label;
        text.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        RectTransform textRect = labelObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }
}
