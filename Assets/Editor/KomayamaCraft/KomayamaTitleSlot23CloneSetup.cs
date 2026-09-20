using KomayamaCraft;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slot1Oj1/2/3 を複製して Slot2/3 を作る。X は列間隔の概算のみ。微調整はシーンで行う。
/// Slot1 の Rect / 色はいじらない。
/// </summary>
public static class KomayamaTitleSlot23CloneSetup
{
    private const string MenuPath = "KomayamaCraft/Clone Title Slot2 and Slot3 From Slot1";

    // Slot1 中心 X ≈ -586.6。3列等間隔なら列ピッチ ≈ 586.6
    private const float ColumnPitchX = 586.6f;

    [MenuItem(MenuPath, false, 62)]
    public static void Setup()
    {
        Transform slotPanel = FindSlotPanelRoot();
        if (slotPanel == null)
        {
            Debug.LogError("[Slot23Clone] SlotPanel not found");
            return;
        }

        RectTransform s1o1 = slotPanel.Find("Slot1Oj1") as RectTransform;
        RectTransform s1o2 = slotPanel.Find("Slot1Oj2") as RectTransform;
        RectTransform s1o3 = slotPanel.Find("Slot1Oj3") as RectTransform;
        if (s1o1 == null || s1o2 == null || s1o3 == null)
        {
            Debug.LogError("[Slot23Clone] Slot1Oj1/2/3 missing");
            return;
        }

        float baseX = s1o1.anchoredPosition.x;

        KomayamaTitleSlotCard[] cards = new KomayamaTitleSlotCard[3];
        cards[0] = s1o2.GetComponent<KomayamaTitleSlotCard>();

        for (int slot = 2; slot <= 3; slot++)
        {
            float x = baseX + ColumnPitchX * (slot - 1);
            RectTransform o1 = CloneOj(s1o1, slotPanel, $"Slot{slot}Oj1", x);
            RectTransform o2 = CloneOj(s1o2, slotPanel, $"Slot{slot}Oj2", x);
            RectTransform o3 = CloneOj(s1o3, slotPanel, $"Slot{slot}Oj3", x);

            RenameSlotButton(o2, slot);
            RenameDeleteButton(o3, slot);
            cards[slot - 1] = WireCard(o1, o2, o3, slot);
        }

        // 描画順: Panel1 → Oj1群 → Panel2 → Oj2群 → Panel3 → Oj3群
        Transform p1 = slotPanel.Find("SlotPanel1");
        Transform p2 = slotPanel.Find("SlotPanel2");
        Transform p3 = slotPanel.Find("SlotPanel3");
        int i = 0;
        if (p1 != null) p1.SetSiblingIndex(i++);
        SetSibling(slotPanel, "Slot1Oj1", i++);
        SetSibling(slotPanel, "Slot2Oj1", i++);
        SetSibling(slotPanel, "Slot3Oj1", i++);
        if (p2 != null) p2.SetSiblingIndex(i++);
        SetSibling(slotPanel, "Slot1Oj2", i++);
        SetSibling(slotPanel, "Slot2Oj2", i++);
        SetSibling(slotPanel, "Slot3Oj2", i++);
        if (p3 != null) p3.SetSiblingIndex(i++);
        SetSibling(slotPanel, "Slot1Oj3", i++);
        SetSibling(slotPanel, "Slot2Oj3", i++);
        SetSibling(slotPanel, "Slot3Oj3", i++);

        KomayamaTitleEntry entry = Object.FindFirstObjectByType<KomayamaTitleEntry>(FindObjectsInactive.Include);
        if (entry != null)
        {
            SerializedObject eso = new SerializedObject(entry);
            SerializedProperty cardsProp = eso.FindProperty("slotCards");
            cardsProp.arraySize = 3;
            for (int c = 0; c < 3; c++)
            {
                cardsProp.GetArrayElementAtIndex(c).objectReferenceValue = cards[c];
            }

            eso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
        }

        EditorSceneManager.MarkSceneDirty(slotPanel.gameObject.scene);
        EditorSceneManager.SaveScene(slotPanel.gameObject.scene);
        Debug.Log($"[Slot23Clone] Created Slot2/3 at X={baseX + ColumnPitchX:F1}, {baseX + ColumnPitchX * 2f:F1} (pitch {ColumnPitchX}). Fine-tune X in scene.");
    }

    private static void SetSibling(Transform parent, string name, int index)
    {
        Transform t = parent.Find(name);
        if (t != null)
        {
            t.SetSiblingIndex(index);
        }
    }

    private static RectTransform CloneOj(RectTransform source, Transform parent, string newName, float anchoredX)
    {
        Transform existing = parent.Find(newName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject clone = Object.Instantiate(source.gameObject, parent);
        clone.name = newName;
        RectTransform rt = clone.GetComponent<RectTransform>();
        Vector2 anc = source.anchoredPosition;
        anc.x = anchoredX;
        rt.anchoredPosition = anc;
        // size / anchors / children local layout は Slot1 のままコピー
        return rt;
    }

    private static void RenameSlotButton(RectTransform oj2, int slot)
    {
        Transform btn = oj2.Find("Slot1Button");
        if (btn != null)
        {
            btn.name = $"Slot{slot}Button";
        }
    }

    private static void RenameDeleteButton(RectTransform oj3, int slot)
    {
        Transform del = oj3.Find("SlotDeleteButton");
        if (del != null)
        {
            // 共用名のままでも動くが、区別しやすいようリネーム
            del.name = $"Slot{slot}DeleteButton";
        }
    }

    private static KomayamaTitleSlotCard WireCard(
        RectTransform oj1,
        RectTransform oj2,
        RectTransform oj3,
        int slot)
    {
        // 複製元のカードが付いていたら一旦外して付け直す
        KomayamaTitleSlotCard[] oldCards = oj2.GetComponents<KomayamaTitleSlotCard>();
        for (int i = 0; i < oldCards.Length; i++)
        {
            Object.DestroyImmediate(oldCards[i]);
        }

        KomayamaTitleSlotCard card = oj2.gameObject.AddComponent<KomayamaTitleSlotCard>();
        Button selectButton = oj2.GetComponentInChildren<Button>(true);
        // select = SlotNButton（Delete ではない）
        Transform selectT = oj2.Find($"Slot{slot}Button");
        if (selectT != null)
        {
            selectButton = selectT.GetComponent<Button>();
        }

        Image selectImage = selectButton != null ? selectButton.GetComponent<Image>() : null;
        Image screenshot = oj1.Find("ScreenshotImage")?.GetComponent<Image>();
        TMP_Text nameText = oj3.Find("NameText")?.GetComponent<TMP_Text>();
        TMP_Text saveTime = oj3.Find("SaveTimeText")?.GetComponent<TMP_Text>();
        TMP_Text playTime = oj3.Find("PlayTimeText")?.GetComponent<TMP_Text>();
        Button deleteButton = oj3.Find($"Slot{slot}DeleteButton")?.GetComponent<Button>()
            ?? oj3.Find("SlotDeleteButton")?.GetComponent<Button>();

        // Slot1 カードのホバー色を引き継ぐ（あれば）
        Color hover = new Color(1f, 0.323f, 0.565f, 0.341f);
        Transform slot1Oj2 = oj2.parent.Find("Slot1Oj2");
        if (slot1Oj2 != null)
        {
            KomayamaTitleSlotCard src = slot1Oj2.GetComponent<KomayamaTitleSlotCard>();
            if (src != null)
            {
                SerializedObject srcSo = new SerializedObject(src);
                hover = srcSo.FindProperty("selectHoverColor").colorValue;
            }
        }

        if (selectImage != null && selectImage.color.a > 0.001f)
        {
            hover = selectImage.color;
        }

        SerializedObject so = new SerializedObject(card);
        so.FindProperty("slotNumber").intValue = slot;
        so.FindProperty("selectButton").objectReferenceValue = selectButton;
        so.FindProperty("selectHighlightImage").objectReferenceValue = selectImage;
        so.FindProperty("selectHoverColor").colorValue = hover;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("screenshotImage").objectReferenceValue = screenshot;
        so.FindProperty("saveTimeText").objectReferenceValue = saveTime;
        so.FindProperty("playTimeText").objectReferenceValue = playTime;
        so.FindProperty("deleteButton").objectReferenceValue = deleteButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        // HoverOverlay が Delete に無ければ（Slot1 からコピー済みならある）
        EnsureHoverOverlayChild(deleteButton);

        TitleEffectManager tem = Object.FindFirstObjectByType<TitleEffectManager>(FindObjectsInactive.Include);
        if (tem != null && deleteButton != null)
        {
            AppendHoverBinding(tem, deleteButton);
        }

        EditorUtility.SetDirty(card);
        return card;
    }

    private static void EnsureHoverOverlayChild(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (button.transform.Find("HoverOverlay ") != null || button.transform.Find("HoverOverlay") != null)
        {
            return;
        }

        Image sourceImage = button.GetComponent<Image>();
        if (sourceImage == null || sourceImage.sprite == null)
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

    private static void AppendHoverBinding(TitleEffectManager tem, Button button)
    {
        Transform hoverT = button.transform.Find("HoverOverlay ");
        if (hoverT == null)
        {
            hoverT = button.transform.Find("HoverOverlay");
        }

        Image hoverImage = hoverT != null ? hoverT.GetComponent<Image>() : null;
        if (hoverImage == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(tem);
        SerializedProperty bindings = so.FindProperty("hoverOverlayBindings");
        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty elem = bindings.GetArrayElementAtIndex(i);
            if (elem.FindPropertyRelative("hoverSource").objectReferenceValue == button)
            {
                return;
            }
        }

        int index = bindings.arraySize;
        bindings.arraySize = index + 1;
        SerializedProperty neo = bindings.GetArrayElementAtIndex(index);
        neo.FindPropertyRelative("hoverSource").objectReferenceValue = button;
        neo.FindPropertyRelative("targetOverlayGraphic").objectReferenceValue = hoverImage;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tem);
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
