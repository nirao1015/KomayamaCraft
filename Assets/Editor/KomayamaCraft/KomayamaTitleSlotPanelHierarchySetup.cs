using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KomayamaCraft;
using TMPro;

/// <summary>
/// 参照の配線のみ。Slot1Button の Rect / 色 / α、SlotPanel の並びは変更しない。
/// </summary>
public static class KomayamaTitleSlotPanelHierarchySetup
{
    private const string MenuPath = "KomayamaCraft/Wire Title Slot1 Card Refs Only";

    [MenuItem(MenuPath, false, 61)]
    public static void Setup()
    {
        Transform slotPanel = FindSlotPanelRoot();
        if (slotPanel == null)
        {
            Debug.LogError("[SlotPanelSetup] SlotPanel not found");
            return;
        }

        Transform oj1 = slotPanel.Find("Slot1Oj1");
        Transform oj2 = slotPanel.Find("Slot1Oj2");
        Transform oj3 = slotPanel.Find("Slot1Oj3");
        if (oj1 == null || oj2 == null || oj3 == null)
        {
            Debug.LogError("[SlotPanelSetup] Slot1Oj1/2/3 missing");
            return;
        }

        // 並び・Rect・色はいじらない。カード参照だけ繋ぐ。
        KomayamaTitleSlotCard stray = oj1.GetComponent<KomayamaTitleSlotCard>();
        if (stray != null)
        {
            Object.DestroyImmediate(stray);
        }

        Image screenshot = oj1.Find("ScreenshotImage")?.GetComponent<Image>();
        Button selectButton = oj2.Find("Slot1Button")?.GetComponent<Button>();
        Image selectImage = selectButton != null ? selectButton.GetComponent<Image>() : null;
        TMP_Text nameText = oj3.Find("NameText")?.GetComponent<TMP_Text>();
        TMP_Text saveTime = oj3.Find("SaveTimeText")?.GetComponent<TMP_Text>();
        TMP_Text playTime = oj3.Find("PlayTimeText")?.GetComponent<TMP_Text>();
        Button deleteButton = oj3.Find("SlotDeleteButton")?.GetComponent<Button>();

        KomayamaTitleSlotCard card = oj2.GetComponent<KomayamaTitleSlotCard>();
        if (card == null)
        {
            card = oj2.gameObject.AddComponent<KomayamaTitleSlotCard>();
        }

        SerializedObject cso = new SerializedObject(card);
        cso.FindProperty("slotNumber").intValue = 1;
        cso.FindProperty("selectButton").objectReferenceValue = selectButton;
        cso.FindProperty("selectHighlightImage").objectReferenceValue = selectImage;
        // 色は Image に α>0 があるときだけ記憶。α=0 なら既存の selectHoverColor を維持。
        if (selectImage != null && selectImage.color.a > 0.001f)
        {
            cso.FindProperty("selectHoverColor").colorValue = selectImage.color;
        }

        cso.FindProperty("nameText").objectReferenceValue = nameText;
        cso.FindProperty("screenshotImage").objectReferenceValue = screenshot;
        cso.FindProperty("saveTimeText").objectReferenceValue = saveTime;
        cso.FindProperty("playTimeText").objectReferenceValue = playTime;
        cso.FindProperty("deleteButton").objectReferenceValue = deleteButton;
        cso.ApplyModifiedPropertiesWithoutUndo();

        Button backButton = slotPanel.Find("SlotBackButton")?.GetComponent<Button>();
        EnsureHoverOverlayChild(backButton);
        EnsureHoverOverlayChild(deleteButton);

        TitleEffectManager tem = Object.FindFirstObjectByType<TitleEffectManager>(FindObjectsInactive.Include);
        if (tem != null)
        {
            AppendHoverBindings(tem, backButton, deleteButton);
        }

        KomayamaTitleEntry entry = Object.FindFirstObjectByType<KomayamaTitleEntry>(FindObjectsInactive.Include);
        if (entry != null)
        {
            SerializedObject eso = new SerializedObject(entry);
            SerializedProperty cards = eso.FindProperty("slotCards");
            cards.arraySize = 1;
            cards.GetArrayElementAtIndex(0).objectReferenceValue = card;
            Transform slotCanvas = slotPanel.parent;
            if (slotCanvas != null && slotCanvas.name == "SlotCanvas")
            {
                eso.FindProperty("slotCanvas").objectReferenceValue = slotCanvas.gameObject;
            }

            if (backButton != null)
            {
                eso.FindProperty("slotBackButton").objectReferenceValue = backButton;
            }

            Button yes = eso.FindProperty("confirmYesButton").objectReferenceValue as Button;
            if (yes != null)
            {
                eso.FindProperty("confirmYesLabel").objectReferenceValue =
                    yes.GetComponentInChildren<TMP_Text>(true);
            }

            eso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
        }

        EditorUtility.SetDirty(card);
        EditorSceneManager.MarkSceneDirty(slotPanel.gameObject.scene);
        EditorSceneManager.SaveScene(slotPanel.gameObject.scene);
        Debug.Log("[SlotPanelSetup] Wired refs only. Did not change hierarchy order / Slot1Button rect / colors.");
    }

    private static void EnsureHoverOverlayChild(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image sourceImage = button.GetComponent<Image>();
        if (sourceImage == null || sourceImage.sprite == null)
        {
            return;
        }

        Transform existing = button.transform.Find("HoverOverlay ");
        if (existing == null)
        {
            existing = button.transform.Find("HoverOverlay");
        }

        if (existing != null)
        {
            return;
        }

        GameObject hoverGo = new GameObject("HoverOverlay ", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        hoverGo.transform.SetParent(button.transform, false);
        hoverGo.transform.SetAsFirstSibling();

        RectTransform rt = hoverGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image hoverImage = hoverGo.GetComponent<Image>();
        hoverImage.sprite = sourceImage.sprite;
        hoverImage.type = sourceImage.type;
        hoverImage.preserveAspect = sourceImage.preserveAspect;
        Color c = sourceImage.color;
        c.a = Mathf.Clamp01(Mathf.Max(0.64f, c.a * 0.7f));
        hoverImage.color = c;
        hoverImage.raycastTarget = false;
    }

    private static void AppendHoverBindings(TitleEffectManager tem, params Button[] buttons)
    {
        SerializedObject so = new SerializedObject(tem);
        SerializedProperty bindings = so.FindProperty("hoverOverlayBindings");

        for (int b = 0; b < buttons.Length; b++)
        {
            Button button = buttons[b];
            if (button == null)
            {
                continue;
            }

            Transform hoverT = button.transform.Find("HoverOverlay ");
            if (hoverT == null)
            {
                hoverT = button.transform.Find("HoverOverlay");
            }

            Image hoverImage = hoverT != null ? hoverT.GetComponent<Image>() : null;
            if (hoverImage == null || HasBinding(bindings, button, hoverImage))
            {
                continue;
            }

            int index = bindings.arraySize;
            bindings.arraySize = index + 1;
            SerializedProperty elem = bindings.GetArrayElementAtIndex(index);
            elem.FindPropertyRelative("hoverSource").objectReferenceValue = button;
            elem.FindPropertyRelative("targetOverlayGraphic").objectReferenceValue = hoverImage;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tem);
    }

    private static bool HasBinding(SerializedProperty bindings, Button button, Image hoverImage)
    {
        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty elem = bindings.GetArrayElementAtIndex(i);
            Object src = elem.FindPropertyRelative("hoverSource").objectReferenceValue;
            Object tgt = elem.FindPropertyRelative("targetOverlayGraphic").objectReferenceValue;
            if (src == button && tgt == hoverImage)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindSlotPanelRoot()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == "SlotPanel")
            {
                return all[i];
            }
        }

        return null;
    }
}
