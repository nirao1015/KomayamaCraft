#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft.Editor
{
    /// <summary>
    /// スキル／図鑑／ステータスのひな形（背景＋戻る）を SystemCanvas に用意する。
    /// </summary>
    public static class CraftPlaceholderMenusSetup
    {
        private const string MenuPath = "KomayamaCraft/Craft/Setup Placeholder Menus (Skill/Atlas/Status)";

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            GameObject canvas = GameObject.Find("SystemCanvas");
            if (canvas == null)
            {
                Debug.LogError("[CraftPlaceholderMenusSetup] SystemCanvas が見つかりません。");
                return;
            }

            TMP_FontAsset font = FindFont();
            Color dimColor = new Color(0f, 0f, 0f, 0.55f);

            GameObject skillRoot = EnsureRoot(canvas.transform, "SkillMenuRoot", "SkillRoot");
            GameObject atlasRoot = EnsureRoot(canvas.transform, "AtlasMenuRoot", "SettingsRoot");
            GameObject statusRoot = EnsureRoot(canvas.transform, "StatusMenuRoot", null);
            BuildShell(skillRoot, font, dimColor);
            BuildShell(atlasRoot, font, dimColor);
            BuildShell(statusRoot, font, dimColor);

            Button btnSkill = EnsureMenuButton("menuスキル");
            Button btnAtlas = EnsureMenuButton("menu図鑑");
            Button btnStatus = EnsureMenuButton("menuステータス");

            Transform hostT = FindNamed("PlaceholderMenusController");
            GameObject hostGo;
            if (hostT == null)
            {
                hostGo = new GameObject("PlaceholderMenusController", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                RectTransform hrt = hostGo.GetComponent<RectTransform>();
                hrt.anchorMin = Vector2.zero;
                hrt.anchorMax = Vector2.zero;
                hrt.sizeDelta = Vector2.zero;
            }
            else
            {
                hostGo = hostT.gameObject;
            }

            KomayamaCraftPlaceholderMenuController ctrl =
                hostGo.GetComponent<KomayamaCraftPlaceholderMenuController>();
            if (ctrl == null)
            {
                ctrl = hostGo.AddComponent<KomayamaCraftPlaceholderMenuController>();
            }

            KomayamaBuildMenuSlide slide =
                Object.FindFirstObjectByType<KomayamaBuildMenuSlide>(FindObjectsInactive.Include);
            KomayamaGameClock clock =
                Object.FindFirstObjectByType<KomayamaGameClock>(FindObjectsInactive.Include);

            SerializedObject so = new SerializedObject(ctrl);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = 3;
            SetEntry(entries, 0, "skill", btnSkill, skillRoot);
            SetEntry(entries, 1, "atlas", btnAtlas, atlasRoot);
            SetEntry(entries, 2, "status", btnStatus, statusRoot);
            so.FindProperty("buildMenuSlide").objectReferenceValue = slide;
            so.FindProperty("gameClock").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(hostGo);
            EditorUtility.SetDirty(skillRoot);
            EditorUtility.SetDirty(atlasRoot);
            EditorUtility.SetDirty(statusRoot);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            Debug.Log("[CraftPlaceholderMenusSetup] Skill/Atlas/Status ひな形をセットアップしました。");
        }

        private static void SetEntry(
            SerializedProperty entries,
            int index,
            string id,
            Button open,
            GameObject root)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("id").stringValue = id;
            e.FindPropertyRelative("openButton").objectReferenceValue = open;
            e.FindPropertyRelative("root").objectReferenceValue = root;
            Transform back = root.transform.Find("BackButton");
            e.FindPropertyRelative("backButton").objectReferenceValue =
                back != null ? back.GetComponent<Button>() : null;
        }

        private static GameObject EnsureRoot(Transform canvas, string preferred, string legacy)
        {
            Transform t = FindNamed(preferred);
            if (t == null && !string.IsNullOrEmpty(legacy))
            {
                t = FindNamed(legacy);
                if (t != null)
                {
                    t.name = preferred;
                }
            }

            if (t == null)
            {
                GameObject go = new GameObject(preferred, typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                t = go.transform;
            }

            RectTransform rt = t as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform c = t.GetChild(i);
                if (c.name == "Background" || c.name == "BackButton")
                {
                    continue;
                }

                Object.DestroyImmediate(c.gameObject);
            }

            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        private static void BuildShell(GameObject root, TMP_FontAsset font, Color dimColor)
        {
            Transform bgT = root.transform.Find("Background");
            if (bgT == null)
            {
                GameObject bgGo = new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                bgGo.transform.SetParent(root.transform, false);
                bgT = bgGo.transform;
            }

            RectTransform bgRt = bgT as RectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgT.GetComponent<Image>();
            bgImg.color = dimColor;
            bgImg.raycastTarget = true;

            Transform backT = root.transform.Find("BackButton");
            if (backT == null)
            {
                GameObject backGo = new GameObject(
                    "BackButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                backGo.transform.SetParent(root.transform, false);
                backT = backGo.transform;
            }

            RectTransform backRt = backT as RectTransform;
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(40f, -40f);
            backRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 160f);
            backRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 56f);
            Image backImg = backT.GetComponent<Image>();
            backImg.color = new Color(0.15f, 0.25f, 0.4f, 0.95f);
            backImg.raycastTarget = true;
            Button backBtn = backT.GetComponent<Button>();
            backBtn.targetGraphic = backImg;

            Transform labelT = backT.Find("Label");
            if (labelT == null)
            {
                GameObject labelGo = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(backT, false);
                labelT = labelGo.transform;
            }

            RectTransform labelRt = labelT as RectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = labelT.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.text = "戻る";
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        private static Button EnsureMenuButton(string menuName)
        {
            Transform t = FindNamed(menuName);
            if (t == null)
            {
                Debug.LogError("[CraftPlaceholderMenusSetup] " + menuName + " が見つかりません。");
                return null;
            }

            Button btn = t.GetComponent<Button>();
            if (btn == null)
            {
                btn = t.gameObject.AddComponent<Button>();
            }

            Transform menuChild = t.Find("menu");
            Image menuImg = menuChild != null ? menuChild.GetComponent<Image>() : null;
            if (menuImg != null)
            {
                btn.targetGraphic = menuImg;
                menuImg.raycastTarget = true;
            }

            return btn;
        }

        private static Transform FindNamed(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == name && t.gameObject.scene.IsValid())
                {
                    return t;
                }
            }

            return null;
        }

        private static TMP_FontAsset FindFont()
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                if (fonts[i] != null && fonts[i].name.Contains("Noto"))
                {
                    return fonts[i];
                }
            }

            for (int i = 0; i < fonts.Length; i++)
            {
                if (fonts[i] != null && fonts[i].name.Contains("JP"))
                {
                    return fonts[i];
                }
            }

            return fonts.Length > 0 ? fonts[0] : null;
        }
    }
}
#endif
