#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PanelGeneral / PanelVolume / PanelKey の設定行を左寄せ整列する。
/// 行の anchoredPosition.x を揃えれば左端が一致する（pivot = 左中央）。
/// </summary>
public static class TitleConfigPanelRowsAlignSetup
{
    private const float RowWidth = 640f;
    private const float RowHeight = 72f;
    private const float LabelWidth = 340f;
    private const float ControlStartX = 360f;

    [MenuItem("KomayamaCraft/Title/Align Config Panel Rows Left")]
    public static void Align()
    {
        Transform panelGeneral = FindByName("PanelGeneral");
        Transform panelVolume = FindByName("PanelVolume");
        Transform panelKey = FindByName("PanelKey");
        if (panelGeneral == null || panelVolume == null || panelKey == null)
        {
            Debug.LogError("[TitleConfigPanelRowsAlignSetup] パネルが見つかりません。");
            return;
        }

        AlignGeneral(panelGeneral);
        AlignVolume(panelVolume);
        AlignKey(panelKey);

        // 3パネルとも同じ左端 X（旧中央配置の 640 幅行の左端）
        const float sharedLeftX = -320f;
        UnifyRowLeftX(panelGeneral, sharedLeftX);
        UnifyRowLeftX(panelVolume, sharedLeftX);
        UnifyRowLeftX(panelKey, sharedLeftX);

        EditorSceneManager.MarkSceneDirty(panelGeneral.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TitleConfigPanelRowsAlignSetup] 設定行を左寄せ整列しました。");
    }

    private static void AlignGeneral(Transform panel)
    {
        AlignRowRoot(panel.Find("LanguageRow") as RectTransform, keepY: true);
        AlignLabeledControl(
            panel.Find("LanguageRow"),
            "LanguageLabelText",
            "LanguageDropdown",
            controlWidth: 280f,
            controlHeight: 48f);

        AlignRowRoot(panel.Find("PauseWhenInactiveRow") as RectTransform, keepY: true);
        AlignLabeledControl(
            panel.Find("PauseWhenInactiveRow"),
            "Label",
            "Toggle",
            controlWidth: 48f,
            controlHeight: 48f);

        AlignRowRoot(panel.Find("PlayAudioWhenInactiveRow") as RectTransform, keepY: true);
        AlignLabeledControl(
            panel.Find("PlayAudioWhenInactiveRow"),
            "Label",
            "Toggle",
            controlWidth: 48f,
            controlHeight: 48f);
    }

    private static void AlignVolume(Transform panel)
    {
        AlignVolumeRow(panel.Find("MasterRow"), "MasterLabelText", "MasterMinusButton", "MasterValueText", "MasterPlusButton");
        AlignVolumeRow(panel.Find("BgmRow"), "BgmLabelText", "BgmMinusButton", "BgmValueText", "BgmPlusButton");
        AlignVolumeRow(panel.Find("SeRow"), "SeLabelText", "SeMinusButton", "SeValueText", "SePlusButton");
    }

    private static void AlignVolumeRow(
        Transform row,
        string labelName,
        string minusName,
        string valueName,
        string plusName)
    {
        if (row == null)
        {
            return;
        }

        AlignRowRoot(row as RectTransform, keepY: true);
        PlaceLeftLabel(row.Find(labelName) as RectTransform);

        PlaceLeftChild(row.Find(minusName) as RectTransform, ControlStartX, 60f, 60f);
        PlaceLeftChild(row.Find(valueName) as RectTransform, ControlStartX + 70f, 110f, 56f);
        PlaceLeftChild(row.Find(plusName) as RectTransform, ControlStartX + 190f, 60f, 60f);

        TextMeshProUGUI labelTmp = row.Find(labelName)?.GetComponent<TextMeshProUGUI>();
        if (labelTmp != null)
        {
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        TextMeshProUGUI valueTmp = row.Find(valueName)?.GetComponent<TextMeshProUGUI>();
        if (valueTmp != null)
        {
            valueTmp.alignment = TextAlignmentOptions.Center;
        }
    }

    private static void AlignKey(Transform panel)
    {
        string[] rows =
        {
            "KeyBuildRow",
            "KeyEditRow",
            "KeySkillRow",
            "KeyDropRow",
            "KeyItemSwitchRow"
        };

        for (int i = 0; i < rows.Length; i++)
        {
            Transform row = panel.Find(rows[i]);
            if (row == null)
            {
                continue;
            }

            AlignRowRoot(row as RectTransform, keepY: true);
            AlignLabeledControl(row, "Label", "Value", controlWidth: 200f, controlHeight: 48f);
        }

        RectTransform reset = panel.Find("KeyResetToDefaultButton") as RectTransform;
        if (reset != null)
        {
            Undo.RecordObject(reset, "Align reset button");
            float y = reset.anchoredPosition.y;
            reset.anchorMin = reset.anchorMax = new Vector2(0.5f, 0.5f);
            reset.pivot = new Vector2(0f, 0.5f);
            // 行と同じ左端基準。幅はボタン固有のまま。
            reset.anchoredPosition = new Vector2(0f, y);
            EditorUtility.SetDirty(reset);
        }
    }

    private static void AlignLabeledControl(
        Transform row,
        string labelName,
        string controlName,
        float controlWidth,
        float controlHeight)
    {
        if (row == null)
        {
            return;
        }

        PlaceLeftLabel(row.Find(labelName) as RectTransform);
        PlaceLeftChild(row.Find(controlName) as RectTransform, ControlStartX, controlWidth, controlHeight);

        TextMeshProUGUI labelTmp = row.Find(labelName)?.GetComponent<TextMeshProUGUI>();
        if (labelTmp != null)
        {
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        TextMeshProUGUI valueTmp = row.Find(controlName)?.GetComponent<TextMeshProUGUI>();
        if (valueTmp != null)
        {
            valueTmp.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    private static void AlignRowRoot(RectTransform row, bool keepY)
    {
        if (row == null)
        {
            return;
        }

        Undo.RecordObject(row, "Align config row");
        float y = row.anchoredPosition.y;
        float oldWidth = row.sizeDelta.x > 1f ? row.sizeDelta.x : RowWidth;
        // 旧 pivot（多くは中央）での左端を維持し、左 pivot に変換
        float leftEdge = row.anchoredPosition.x - (oldWidth * row.pivot.x);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0f, 0.5f);
        row.sizeDelta = new Vector2(RowWidth, RowHeight);
        row.anchoredPosition = new Vector2(leftEdge, keepY ? y : row.anchoredPosition.y);
        EditorUtility.SetDirty(row);

        Transform hit = row.Find("HitArea");
        if (hit != null)
        {
            RectTransform hitRt = hit as RectTransform;
            Undo.RecordObject(hitRt, "Align hit area");
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;
            hitRt.anchoredPosition = Vector2.zero;
            hitRt.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(hitRt);
        }
    }

    /// <summary>全設定行の左端 X を揃える（Panel 内で同じ値）。</summary>
    private static void UnifyRowLeftX(Transform panel, float leftX)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child.name == "ConfigTitleTextMy")
            {
                continue;
            }

            RectTransform row = child as RectTransform;
            if (row == null)
            {
                continue;
            }

            Undo.RecordObject(row, "Unify row left X");
            row.anchoredPosition = new Vector2(leftX, row.anchoredPosition.y);
            EditorUtility.SetDirty(row);
        }
    }

    private static void PlaceLeftLabel(RectTransform label)
    {
        PlaceLeftChild(label, 0f, LabelWidth, 56f);
    }

    private static void PlaceLeftChild(RectTransform child, float x, float width, float height)
    {
        if (child == null)
        {
            return;
        }

        Undo.RecordObject(child, "Align config child");
        child.anchorMin = child.anchorMax = new Vector2(0f, 0.5f);
        child.pivot = new Vector2(0f, 0.5f);
        child.anchoredPosition = new Vector2(x, 0f);
        child.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(child);
    }

    private static Transform FindByName(string name)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
            {
                return all[i];
            }
        }

        return null;
    }
}
#endif
