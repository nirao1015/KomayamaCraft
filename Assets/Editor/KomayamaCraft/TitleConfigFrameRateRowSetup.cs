#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PanelGeneral に FPS 三択（LanguageRow 複製）を追加し、TitleConfigPanelController へ配線する。
/// </summary>
public static class TitleConfigFrameRateRowSetup
{
    private const string PrefabPath = "Assets/Prefabs/KomayamaCraft/ConfigCanvas.prefab";
    private const float RowStepY = 100f;

    [MenuItem("KomayamaCraft/Title/Setup Config Frame Rate Row")]
    public static void Setup()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform panelGeneral = FindDeep(contents.transform, "PanelGeneral");
            if (panelGeneral == null)
            {
                Debug.LogError("[TitleConfigFrameRateRowSetup] PanelGeneral が見つかりません。");
                return;
            }

            Transform languageRow = panelGeneral.Find("LanguageRow");
            if (languageRow == null)
            {
                Debug.LogError("[TitleConfigFrameRateRowSetup] LanguageRow が見つかりません。");
                return;
            }

            Transform existing = panelGeneral.Find("FrameRateRow");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject frameRateRowGo = Object.Instantiate(languageRow.gameObject, panelGeneral);
            frameRateRowGo.name = "FrameRateRow";
            frameRateRowGo.transform.SetSiblingIndex(languageRow.GetSiblingIndex() + 1);

            RectTransform frameRateRow = frameRateRowGo.GetComponent<RectTransform>();
            RectTransform languageRt = languageRow as RectTransform;
            float leftX = languageRt != null ? languageRt.anchoredPosition.x : -320f;
            float languageY = languageRt != null ? languageRt.anchoredPosition.y : 120f;
            // 言語の下へ 100px 刻みで並べる（再実行しても絶対位置なのでずれない）
            SetRowY(frameRateRow, leftX, languageY - RowStepY);
            SetRowY(panelGeneral.Find("PauseWhenInactiveRow") as RectTransform, leftX, languageY - (RowStepY * 2f));
            SetRowY(panelGeneral.Find("PlayAudioWhenInactiveRow") as RectTransform, leftX, languageY - (RowStepY * 3f));

            RenameChild(frameRateRow, "LanguageLabelText", "FrameRateLabelText");
            RenameChild(frameRateRow, "LanguageDropdown", "FrameRateDropdown");

            TextMeshProUGUI label = frameRateRow.Find("FrameRateLabelText")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = "フレームレート";
            }

            TMP_Dropdown dropdown = frameRateRow.Find("FrameRateDropdown")?.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                Debug.LogError("[TitleConfigFrameRateRowSetup] FrameRateDropdown が見つかりません。");
                return;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "FPS無制限",
                "60FPS",
                "30FPS"
            });
            dropdown.value = (int)GameFrameRate.DefaultMode;
            dropdown.RefreshShownValue();

            TitleConfigPanelController controller = contents.GetComponent<TitleConfigPanelController>();
            if (controller == null)
            {
                Debug.LogError("[TitleConfigFrameRateRowSetup] TitleConfigPanelController が見つかりません。");
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("frameRateDropdown").objectReferenceValue = dropdown;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log("[TitleConfigFrameRateRowSetup] FrameRateRow を追加し、配線しました: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void SetRowY(RectTransform row, float x, float y)
    {
        if (row == null)
        {
            return;
        }

        row.anchoredPosition = new Vector2(x, y);
    }

    private static void RenameChild(Transform parent, string from, string to)
    {
        Transform child = parent.Find(from);
        if (child != null)
        {
            child.name = to;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
