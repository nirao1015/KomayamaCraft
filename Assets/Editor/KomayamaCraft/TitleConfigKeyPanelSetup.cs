#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PanelKey の表示専用キー行とタブ配線を一回セットアップする。
/// </summary>
public static class TitleConfigKeyPanelSetup
{
    [MenuItem("KomayamaCraft/Title/Setup Config Key Panel")]
    public static void Setup()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Transform configCanvas = null;
        Transform panelKey = null;
        Transform panelGeneral = null;
        Transform panelVolume = null;
        Transform tabGeneral = null;
        Transform tabVolume = null;
        Transform tabKey = null;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t.name == "ConfigCanvas") configCanvas = t;
            else if (t.name == "PanelKey") panelKey = t;
            else if (t.name == "PanelGeneral") panelGeneral = t;
            else if (t.name == "PanelVolume") panelVolume = t;
            else if (t.name == "ConfigFieldTabGeneral") tabGeneral = t;
            else if (t.name == "ConfigFieldTabVolume") tabVolume = t;
            else if (t.name == "ConfigFieldTabKey") tabKey = t;
        }

        if (panelKey == null || configCanvas == null || tabKey == null)
        {
            Debug.LogError("[TitleConfigKeyPanelSetup] ConfigCanvas / PanelKey / ConfigFieldTabKey が見つかりません。");
            return;
        }

        for (int i = panelKey.childCount - 1; i >= 0; i--)
        {
            Transform ch = panelKey.GetChild(i);
            if (ch.name == "ConfigTitleTextMy")
            {
                continue;
            }

            Undo.DestroyObjectImmediate(ch.gameObject);
        }

        Transform titleTf = panelKey.Find("ConfigTitleTextMy");
        if (titleTf == null)
        {
            GameObject titleGo = new GameObject("ConfigTitleTextMy", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(titleGo, "Key title");
            titleTf = titleGo.transform;
            titleTf.SetParent(panelKey, false);
            RectTransform trt = titleTf as RectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(-557f, 408f);
            trt.sizeDelta = new Vector2(380f, 80f);
        }

        TextMeshProUGUI titleTmp = titleTf.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 48;
        titleTmp.color = Color.white;
        titleTmp.text = "KEY";
        titleTmp.raycastTarget = false;

        TextMeshProUGUI buildVal = CreateKeyRow(panelKey, font, "KeyBuildRow", "建築", "R", 140f);
        TextMeshProUGUI editVal = CreateKeyRow(panelKey, font, "KeyEditRow", "編集", "T", 70f);
        TextMeshProUGUI skillVal = CreateKeyRow(panelKey, font, "KeySkillRow", "スキル", "F", 0f);
        TextMeshProUGUI dropVal = CreateKeyRow(panelKey, font, "KeyDropRow", "ドロップ", "Space", -70f);
        TextMeshProUGUI itemVal = CreateKeyRow(panelKey, font, "KeyItemSwitchRow", "アイテム切り替え", "E", -140f);

        Button resetBtn = CreateResetButton(panelKey, font);

        Button keyTabBtn = tabKey.GetComponent<Button>();
        if (keyTabBtn == null)
        {
            keyTabBtn = Undo.AddComponent<Button>(tabKey.gameObject);
        }

        Image keyTabImg = tabKey.GetComponent<Image>();
        keyTabBtn.targetGraphic = keyTabImg;
        keyTabBtn.transition = Selectable.Transition.None;

        TitleConfigPanelController controller = configCanvas.GetComponent<TitleConfigPanelController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<TitleConfigPanelController>(configCanvas.gameObject);
        }

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("generalTabButton").objectReferenceValue = tabGeneral != null ? tabGeneral.GetComponent<Button>() : null;
        so.FindProperty("volumeTabButton").objectReferenceValue = tabVolume != null ? tabVolume.GetComponent<Button>() : null;
        so.FindProperty("keyTabButton").objectReferenceValue = keyTabBtn;
        so.FindProperty("panelGeneral").objectReferenceValue = panelGeneral != null ? panelGeneral.gameObject : null;
        so.FindProperty("panelVolume").objectReferenceValue = panelVolume != null ? panelVolume.gameObject : null;
        so.FindProperty("panelKey").objectReferenceValue = panelKey.gameObject;
        so.FindProperty("keyBuildValueText").objectReferenceValue = buildVal;
        so.FindProperty("keyEditValueText").objectReferenceValue = editVal;
        so.FindProperty("keySkillValueText").objectReferenceValue = skillVal;
        so.FindProperty("keyDropValueText").objectReferenceValue = dropVal;
        so.FindProperty("keyItemSwitchValueText").objectReferenceValue = itemVal;
        so.FindProperty("keyResetToDefaultButton").objectReferenceValue = resetBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        panelKey.gameObject.SetActive(false);
        EditorUtility.SetDirty(configCanvas.gameObject);
        EditorSceneManager.MarkSceneDirty(configCanvas.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TitleConfigKeyPanelSetup] PanelKey 表示専用キー UI とタブ接続を完了しました。");
    }

    private static TextMeshProUGUI CreateKeyRow(
        Transform panelKey,
        TMP_FontAsset font,
        string rowName,
        string label,
        string value,
        float y)
    {
        GameObject row = new GameObject(rowName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(row, rowName);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.SetParent(panelKey, false);
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0f, y);
        rowRt.sizeDelta = new Vector2(640f, 64f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(labelGo, rowName + "Label");
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.SetParent(rowRt, false);
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(-120f, 0f);
        lrt.sizeDelta = new Vector2(320f, 48f);
        TextMeshProUGUI lab = labelGo.GetComponent<TextMeshProUGUI>();
        lab.font = font;
        lab.fontSize = 28;
        lab.alignment = TextAlignmentOptions.MidlineRight;
        lab.color = Color.white;
        lab.text = label;
        lab.raycastTarget = false;

        GameObject valueGo = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(valueGo, rowName + "Value");
        RectTransform vrt = valueGo.GetComponent<RectTransform>();
        vrt.SetParent(rowRt, false);
        vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.5f);
        vrt.anchoredPosition = new Vector2(160f, 0f);
        vrt.sizeDelta = new Vector2(200f, 48f);
        TextMeshProUGUI val = valueGo.GetComponent<TextMeshProUGUI>();
        val.font = font;
        val.fontSize = 28;
        val.alignment = TextAlignmentOptions.MidlineLeft;
        val.color = Color.white;
        val.text = value;
        val.raycastTarget = false;
        return val;
    }

    private static Button CreateResetButton(Transform panelKey, TMP_FontAsset font)
    {
        GameObject resetGo = new GameObject(
            "KeyResetToDefaultButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Undo.RegisterCreatedObjectUndo(resetGo, "KeyReset");
        RectTransform resetRt = resetGo.GetComponent<RectTransform>();
        resetRt.SetParent(panelKey, false);
        resetRt.anchorMin = resetRt.anchorMax = new Vector2(0.5f, 0.5f);
        resetRt.anchoredPosition = new Vector2(0f, -230f);
        resetRt.sizeDelta = new Vector2(280f, 56f);
        Image resetImg = resetGo.GetComponent<Image>();
        resetImg.color = new Color(0.25f, 0.28f, 0.35f, 0.95f);
        Button resetBtn = resetGo.GetComponent<Button>();
        resetBtn.targetGraphic = resetImg;

        GameObject resetLabelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(resetLabelGo, "KeyResetText");
        RectTransform rlt = resetLabelGo.GetComponent<RectTransform>();
        rlt.SetParent(resetRt, false);
        rlt.anchorMin = Vector2.zero;
        rlt.anchorMax = Vector2.one;
        rlt.offsetMin = Vector2.zero;
        rlt.offsetMax = Vector2.zero;
        TextMeshProUGUI resetTmp = resetLabelGo.GetComponent<TextMeshProUGUI>();
        resetTmp.font = font;
        resetTmp.fontSize = 26;
        resetTmp.alignment = TextAlignmentOptions.Center;
        resetTmp.color = Color.white;
        resetTmp.text = "デフォルトに戻す";
        resetTmp.raycastTarget = false;
        return resetBtn;
    }
}
#endif
