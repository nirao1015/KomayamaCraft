using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft.Editor
{
    /// <summary>
    /// MenuHoverGuideHit プレハブの作成と、各 menu* への横展開。
    /// </summary>
    public static class CraftMenuHoverGuideSetup
    {
        private const string PrefabPath = "Assets/Prefabs/KomayamaCraft/MenuHoverGuideHit.prefab";
        private const string FontPath = "Assets/Fonts/UI/LightNovelPOPv2 SDF.asset";

        private static readonly (string menuName, string title, string guide)[] Defaults =
        {
            ("menu建設", "建設", "施設の建設・配置を行います。"),
            ("menuスキル", "スキル", "スキルの確認・振り分けを行います。（仮）"),
            ("menu図鑑", "図鑑", "図鑑を開いて収集状況を確認します。（仮）"),
            ("menuステータス", "ステータス", "機体のステータスを確認します。（仮）"),
            ("menu設定", "設定", "ゲーム設定やセーブ操作を行います。"),
        };

        [MenuItem("KomayamaCraft/Craft/Setup Menu Hover Guides")]
        public static void SetupAll()
        {
            EnsurePrefab();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (prefab == null)
            {
                Debug.LogError("[CraftMenuHoverGuideSetup] Prefab missing: " + PrefabPath);
                return;
            }

            int count = 0;
            for (int i = 0; i < Defaults.Length; i++)
            {
                Transform menu = FindInOpenScenes(Defaults[i].menuName);
                if (menu == null)
                {
                    Debug.LogWarning("[CraftMenuHoverGuideSetup] Not found: " + Defaults[i].menuName);
                    continue;
                }

                EnsureHitInstance(menu, prefab, font, Defaults[i].title, Defaults[i].guide);
                count++;
                EditorUtility.SetDirty(menu.gameObject);
                EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[CraftMenuHoverGuideSetup] Applied HoverGuideHit to {count} menus. Prefab={PrefabPath}");
        }

        [MenuItem("KomayamaCraft/Craft/Create Menu Hover Guide Hit Prefab")]
        public static void EnsurePrefabMenu()
        {
            string path = EnsurePrefab();
            Debug.Log("[CraftMenuHoverGuideSetup] Prefab ready: " + path);
        }

        private static string EnsurePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return PrefabPath;
            }

            string dir = "Assets/Prefabs/KomayamaCraft";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "KomayamaCraft");
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            GameObject root = new GameObject("MenuHoverGuideHit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image image = root.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            KomayamaMenuHoverGuideHit hit = root.AddComponent<KomayamaMenuHoverGuideHit>();
            SerializedObject so = new SerializedObject(hit);
            so.FindProperty("title").stringValue = "タイトル";
            so.FindProperty("guide").stringValue = "ガイド文を入力";
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("titleFontSize").floatValue = 22f;
            so.FindProperty("guideFontSize").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return PrefabPath;
        }

        private static void EnsureHitInstance(
            Transform menu,
            GameObject prefab,
            TMP_FontAsset font,
            string title,
            string guide)
        {
            Transform old = menu.Find("HoverGuideHit");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // Also clear prefab instance named MenuHoverGuideHit
            Transform old2 = menu.Find("MenuHoverGuideHit");
            if (old2 != null)
            {
                Object.DestroyImmediate(old2.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu);
            instance.name = "HoverGuideHit";
            instance.transform.SetAsLastSibling();

            RectTransform rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            KomayamaMenuHoverGuideHit hit = instance.GetComponent<KomayamaMenuHoverGuideHit>();
            SerializedObject so = new SerializedObject(hit);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("guide").stringValue = guide;
            if (font != null)
            {
                so.FindProperty("font").objectReferenceValue = font;
            }

            Button button = menu.GetComponent<Button>();
            so.FindProperty("forwardClickTo").objectReferenceValue = button;
            so.FindProperty("presenter").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindInOpenScenes(string name)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.scene.IsValid() && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
