#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KomayamaCraft;

/// <summary>
/// メインメニュー各ボタンに HoverOverlay を付け、ホバー発光を配線する。
/// </summary>
public static class CraftMainMenuHoverOverlaySetup
{
    private const string CraftScenePath = "Assets/Scenes/komayama_craft_scene.unity";

    private static readonly string[] ButtonNames =
    {
        "MainMenuCloseButton",
        "MainMenuSettingsButton",
        "MainMenuSaveTitleButton",
        "MainMenuSaveQuitButton"
    };

    [MenuItem("KomayamaCraft/Craft/Setup Main Menu HoverOverlay")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != CraftScenePath)
        {
            scene = EditorSceneManager.OpenScene(CraftScenePath, OpenSceneMode.Single);
        }

        Transform panel = FindDeepNamed(scene, "MainMenuPanel");
        if (panel == null)
        {
            Debug.LogError("[CraftMainMenuHoverOverlaySetup] MainMenuPanel が見つかりません。");
            return;
        }

        // MainMenuController は常時 active（MainMenuRoot は閉じていると非表示）
        Transform controllerTf = FindDeepNamed(scene, "MainMenuController");
        GameObject hostGo = controllerTf != null
            ? controllerTf.gameObject
            : (panel.parent != null ? panel.parent.gameObject : panel.gameObject);

        KomayamaCraftHoverOverlayEffectManager manager =
            hostGo.GetComponent<KomayamaCraftHoverOverlayEffectManager>()
            ?? Undo.AddComponent<KomayamaCraftHoverOverlayEffectManager>(hostGo);

        Button[] buttons = new Button[ButtonNames.Length];
        for (int i = 0; i < ButtonNames.Length; i++)
        {
            Transform t = FindChildNamed(panel, ButtonNames[i]);
            if (t == null)
            {
                Debug.LogWarning("[CraftMainMenuHoverOverlaySetup] ボタン無し: " + ButtonNames[i]);
                continue;
            }

            Button button = t.GetComponent<Button>();
            if (button == null)
            {
                continue;
            }

            EnsureHoverOverlayChild(button);
            buttons[i] = button;
        }

        WireBindings(manager, buttons);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CraftMainMenuHoverOverlaySetup] MainMenu ボタンに HoverOverlay を配線しました。");
    }

    private static void EnsureHoverOverlayChild(Button button)
    {
        Image sourceImage = button.targetGraphic as Image;
        if (sourceImage == null)
        {
            sourceImage = button.GetComponent<Image>();
        }

        if (sourceImage == null || sourceImage.sprite == null)
        {
            return;
        }

        Transform existing = button.transform.Find("HoverOverlay ");
        if (existing == null)
        {
            existing = button.transform.Find("HoverOverlay");
        }

        Image hoverImage;
        if (existing != null)
        {
            hoverImage = existing.GetComponent<Image>();
        }
        else
        {
            // タイトル慣習に合わせ末尾スペース付き名
            GameObject hoverGo = new GameObject(
                "HoverOverlay ",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Undo.RegisterCreatedObjectUndo(hoverGo, "HoverOverlay");
            hoverGo.transform.SetParent(button.transform, false);
            hoverGo.transform.SetAsFirstSibling();
            hoverImage = hoverGo.GetComponent<Image>();

            RectTransform rt = hoverGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        if (hoverImage == null)
        {
            return;
        }

        hoverImage.sprite = sourceImage.sprite;
        hoverImage.type = sourceImage.type;
        hoverImage.preserveAspect = sourceImage.preserveAspect;
        Color c = sourceImage.color;
        c.a = Mathf.Clamp01(Mathf.Max(0.64f, c.a * 0.7f));
        hoverImage.color = c;
        hoverImage.raycastTarget = false;
        EditorUtility.SetDirty(hoverImage);
    }

    private static void WireBindings(KomayamaCraftHoverOverlayEffectManager manager, Button[] buttons)
    {
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty bindings = so.FindProperty("hoverOverlayBindings");
        bindings.ClearArray();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
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
            if (hoverImage == null)
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
    }

    private static Transform FindDeepNamed(Scene scene, string exactName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == exactName)
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    private static Transform FindChildNamed(Transform parent, string exactName)
    {
        Transform direct = parent.Find(exactName);
        if (direct != null)
        {
            return direct;
        }

        Transform[] all = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == exactName)
            {
                return all[i];
            }
        }

        return null;
    }
}
#endif
