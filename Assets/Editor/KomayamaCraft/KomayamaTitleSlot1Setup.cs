using KomayamaCraft;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class KomayamaTitleSlot1Setup
{
    [MenuItem("KomayamaCraft/Setup Title Slot1Oj Card", false, 60)]
    public static void Setup()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("[Slot1Setup] Canvas not found");
            return;
        }

        Transform slotPanel = canvas.transform.Find("SlotPanel");
        if (slotPanel == null)
        {
            Debug.LogError("[Slot1Setup] SlotPanel not found");
            return;
        }

        RectTransform slot1 = slotPanel.Find("Slot1Oj") as RectTransform;
        if (slot1 == null)
        {
            Debug.LogError("[Slot1Setup] Slot1Oj not found");
            return;
        }

        TMP_FontAsset font = null;
        Transform oldLabel = slot1.Find("Label");
        if (oldLabel != null)
        {
            TMP_Text oldTmp = oldLabel.GetComponent<TMP_Text>();
            if (oldTmp != null)
            {
                font = oldTmp.font;
            }
        }

        if (font == null)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LightNovelPOPv2 SDF.asset");
        }

        slot1.anchorMin = slot1.anchorMax = new Vector2(0.5f, 0.5f);
        slot1.pivot = new Vector2(0.5f, 0.5f);
        slot1.anchoredPosition = new Vector2(-586.6f, -76.8f);
        slot1.sizeDelta = new Vector2(552f, 730.6f);

        RectTransform nameRt = EnsureChild(slot1, "NameText", new Vector2(0f, 318f), new Vector2(480f, 52f));
        TMP_Text nameTmp = EnsureTmp(nameRt, font, "スロット1", 28f, TextAlignmentOptions.Center, Color.white);

        RectTransform shotRt = EnsureChild(slot1, "ScreenshotImage", new Vector2(0f, 40f), new Vector2(470f, 420f));
        Image shotImg = shotRt.GetComponent<Image>();
        if (shotImg == null)
        {
            shotImg = shotRt.gameObject.AddComponent<Image>();
        }

        shotImg.color = new Color(0.08f, 0.12f, 0.22f, 0.9f);
        shotImg.raycastTarget = false;
        shotImg.preserveAspect = true;

        RectTransform saveRt = EnsureChild(slot1, "SaveTimeText", new Vector2(0f, -255f), new Vector2(480f, 36f));
        TMP_Text saveTmp = EnsureTmp(
            saveRt, font, "セーブ時刻 —", 20f, TextAlignmentOptions.Center, Color.white);

        RectTransform playRt = EnsureChild(slot1, "PlayTimeText", new Vector2(0f, -292f), new Vector2(480f, 32f));
        TMP_Text playTmp = EnsureTmp(
            playRt,
            font,
            "プレイ --:--",
            18f,
            TextAlignmentOptions.Center,
            new Color(0.85f, 0.92f, 1f, 1f));

        Transform slot2 = slotPanel.Find("Slot2Button");
        Transform slot3 = slotPanel.Find("Slot3Button");
        if (slot2 != null)
        {
            slot2.gameObject.SetActive(false);
        }

        if (slot3 != null)
        {
            slot3.gameObject.SetActive(false);
        }

        RectTransform back = slotPanel.Find("SlotBackButton") as RectTransform;
        if (back != null)
        {
            back.anchoredPosition = new Vector2(0f, -470f);
        }

        RectTransform panelTitle = slotPanel.Find("SlotPanelTitle") as RectTransform;
        if (panelTitle != null)
        {
            panelTitle.anchoredPosition = new Vector2(-760f, 430f);
            panelTitle.sizeDelta = new Vector2(420f, 48f);
        }

        RectTransform btnT = slot1.Find("Slot1Button") as RectTransform;
        if (btnT == null)
        {
            GameObject btnGo = new GameObject("Slot1Button", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(slot1, false);
            btnT = btnGo.GetComponent<RectTransform>();
        }

        Image btnImg = btnT.GetComponent<Image>();
        if (btnImg == null)
        {
            btnImg = btnT.gameObject.AddComponent<Image>();
        }

        btnImg.color = new Color(1f, 1f, 1f, 0.01f);
        btnImg.raycastTarget = true;
        Button btn = btnT.GetComponent<Button>();
        if (btn == null)
        {
            btn = btnT.gameObject.AddComponent<Button>();
        }

        btn.targetGraphic = btnImg;
        btnT.anchorMin = Vector2.zero;
        btnT.anchorMax = Vector2.one;
        btnT.offsetMin = Vector2.zero;
        btnT.offsetMax = Vector2.zero;
        btnT.SetAsLastSibling();

        if (oldLabel != null)
        {
            Object.DestroyImmediate(oldLabel.gameObject);
        }

        KomayamaTitleSlotCard card = slot1.GetComponent<KomayamaTitleSlotCard>();
        if (card == null)
        {
            card = slot1.gameObject.AddComponent<KomayamaTitleSlotCard>();
        }

        SerializedObject so = new SerializedObject(card);
        so.FindProperty("slotNumber").intValue = 1;
        so.FindProperty("selectButton").objectReferenceValue = btn;
        so.FindProperty("nameText").objectReferenceValue = nameTmp;
        so.FindProperty("screenshotImage").objectReferenceValue = shotImg;
        so.FindProperty("saveTimeText").objectReferenceValue = saveTmp;
        so.FindProperty("playTimeText").objectReferenceValue = playTmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        KomayamaTitleEntry entry = Object.FindFirstObjectByType<KomayamaTitleEntry>(FindObjectsInactive.Include);
        if (entry != null)
        {
            SerializedObject eso = new SerializedObject(entry);
            SerializedProperty cardsProp = eso.FindProperty("slotCards");
            cardsProp.arraySize = 1;
            cardsProp.GetArrayElementAtIndex(0).objectReferenceValue = card;
            eso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
        }

        EditorUtility.SetDirty(slot1.gameObject);
        EditorSceneManager.MarkSceneDirty(slot1.gameObject.scene);
        EditorSceneManager.SaveScene(slot1.gameObject.scene);
        Debug.Log("[Slot1Setup] Slot1Oj card wired.");
    }

    private static RectTransform EnsureChild(RectTransform parent, string name, Vector2 anc, Vector2 size)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            t = go.transform;
        }

        RectTransform rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anc;
        rt.sizeDelta = size;
        return rt;
    }

    private static TMP_Text EnsureTmp(
        RectTransform rt,
        TMP_FontAsset font,
        string text,
        float fontSize,
        TextAlignmentOptions align,
        Color color)
    {
        TMP_Text tmp = rt.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        }

        if (font != null)
        {
            tmp.font = font;
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }
}
