#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KomayamaCraft;

/// <summary>
/// クラフトシーンにメインメニュー UI・設定パネル複製・SE／参照配線を一回セットアップする。
/// </summary>
public static class CraftMainMenuSceneSetup
{
    private const string CraftScenePath = "Assets/Scenes/komayama_craft_scene.unity";
    private const string ConfigToggleClipGuid = "7cdb73725efa2a2478be6f162dd7b73b";
    private const string TransitionStartClipGuid = "c586590fe6f21b14ca82b841e934d18a";
    private const string FadePrefabPath = "Assets/Imports/Fade/FadeCanvas.prefab";
    private const string ConfigPrefabPath = "Assets/Prefabs/KomayamaCraft/ConfigCanvas.prefab";

    [MenuItem("KomayamaCraft/Craft/Setup Main Menu Scene")]
    public static void Setup()
    {
        Scene craftScene = EnsureCraftSceneOpen();
        if (!craftScene.IsValid())
        {
            Debug.LogError("[CraftMainMenuSceneSetup] craft scene を開けません。");
            return;
        }

        Transform systemCanvas = FindNamed(craftScene.GetRootGameObjects(), "SystemCanvas");
        if (systemCanvas == null)
        {
            Debug.LogError("[CraftMainMenuSceneSetup] SystemCanvas が見つかりません。");
            return;
        }

        Transform menuObject = FindDeep(systemCanvas, "MenuObject");
        Transform menuSettings = FindDeep(systemCanvas, "menu設定");
        if (menuObject == null || menuSettings == null)
        {
            Debug.LogError("[CraftMainMenuSceneSetup] MenuObject / menu設定 が見つかりません。");
            return;
        }

        Button openButton = EnsureMenuSettingsButton(menuSettings);
        CanvasGroup menuCanvasGroup = EnsureCanvasGroup(menuObject.gameObject);

        GameObject mainMenuRoot = EnsureMainMenuUi(systemCanvas);
        GameObject craftConfigRoot = EnsureCraftConfigFromPrefab(systemCanvas);
        craftConfigRoot.SetActive(false);
        mainMenuRoot.SetActive(false);

        GameObject host = EnsureControllerHost(systemCanvas);
        KomayamaCraftMainMenuController controller =
            host.GetComponent<KomayamaCraftMainMenuController>()
            ?? Undo.AddComponent<KomayamaCraftMainMenuController>(host);

        WireMainMenuController(
            controller,
            openButton,
            mainMenuRoot,
            menuCanvasGroup,
            craftConfigRoot);

        EnsureSeEntries();

        EditorSceneManager.MarkSceneDirty(craftScene);
        EditorSceneManager.SaveScene(craftScene);
        Debug.Log("[CraftMainMenuSceneSetup] メインメニュー／設定パネルのセットアップ完了。");
    }

    private static Scene EnsureCraftSceneOpen()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == CraftScenePath)
        {
            return active;
        }

        return EditorSceneManager.OpenScene(CraftScenePath, OpenSceneMode.Single);
    }

    private static Button EnsureMenuSettingsButton(Transform menuSettings)
    {
        Button button = menuSettings.GetComponent<Button>();
        if (button == null)
        {
            button = Undo.AddComponent<Button>(menuSettings.gameObject);
        }

        Transform menuImage = menuSettings.Find("menu");
        Image target = menuImage != null ? menuImage.GetComponent<Image>() : null;
        if (target != null)
        {
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
        }

        return button;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = Undo.AddComponent<CanvasGroup>(go);
        }

        return group;
    }

    private static GameObject EnsureControllerHost(Transform systemCanvas)
    {
        Transform existing = systemCanvas.Find("MainMenuController");
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject host = new GameObject("MainMenuController", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(host, "MainMenuController");
        host.transform.SetParent(systemCanvas, false);
        RectTransform rt = host.GetComponent<RectTransform>();
        StretchFull(rt);
        return host;
    }

    private static GameObject EnsureMainMenuUi(Transform systemCanvas)
    {
        Transform existing = systemCanvas.Find("MainMenuRoot");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        TMP_FontAsset font = ResolveFont();
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/menu/menu_bk_設定.png");
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/menu/menu_sq.png");

        GameObject root = new GameObject("MainMenuRoot", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "MainMenuRoot");
        root.transform.SetParent(systemCanvas, false);
        root.transform.SetAsLastSibling();
        StretchFull(root.GetComponent<RectTransform>());

        GameObject dim = CreateUiObject("MainMenuDim", root.transform);
        StretchFull(dim.GetComponent<RectTransform>());
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = true;

        GameObject panel = CreateUiObject("MainMenuPanel", root.transform);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720f, 520f);
        panelRt.anchoredPosition = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        CreateTmp("MainMenuTitle", panel.transform, font, "メインメニュー", 42f, new Vector2(0f, 190f), new Vector2(560f, 70f));

        CreateMenuButton(
            "MainMenuCloseButton",
            panel.transform,
            font,
            "×",
            36f,
            new Vector2(310f, 210f),
            new Vector2(64f, 64f),
            buttonSprite);

        CreateMenuButton(
            "MainMenuSettingsButton",
            panel.transform,
            font,
            "設定",
            34f,
            new Vector2(0f, 70f),
            new Vector2(480f, 72f),
            buttonSprite);

        CreateMenuButton(
            "MainMenuSaveTitleButton",
            panel.transform,
            font,
            "セーブしてタイトル",
            30f,
            new Vector2(0f, -20f),
            new Vector2(480f, 72f),
            buttonSprite);

        CreateMenuButton(
            "MainMenuSaveQuitButton",
            panel.transform,
            font,
            "セーブしてデスクトップ",
            28f,
            new Vector2(0f, -110f),
            new Vector2(480f, 72f),
            buttonSprite);

        TextMeshProUGUI status = CreateTmp(
            "MainMenuStatusText",
            panel.transform,
            font,
            string.Empty,
            24f,
            new Vector2(0f, -190f),
            new Vector2(560f, 48f));
        status.color = new Color(1f, 0.85f, 0.55f, 1f);

        return root;
    }

    private static GameObject EnsureCraftConfigFromPrefab(Transform systemCanvas)
    {
        Transform existing = systemCanvas.Find("CraftConfigCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfigPrefabPath);
        if (prefab == null)
        {
            throw new System.InvalidOperationException(
                "ConfigCanvas プレハブがありません。先に KomayamaCraft/Shared/Setup ConfigCanvas Prefab を実行してください: "
                + ConfigPrefabPath);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, systemCanvas.gameObject.scene);
        instance.name = "CraftConfigCanvas";
        Undo.RegisterCreatedObjectUndo(instance, "CraftConfigCanvas");
        // worldPositionStays=false: 親 SystemCanvas(scale 0.01) の影響を local に焼き込まない
        instance.transform.SetParent(systemCanvas, false);
        instance.transform.SetAsLastSibling();

        // MenuRoot 等と同じくネスト Canvas は持たない
        StripNestedCanvasComponents(instance);
        StretchFull(instance.GetComponent<RectTransform>());

        TitleConfigPanelController panel = instance.GetComponent<TitleConfigPanelController>();
        if (panel != null)
        {
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty seProp = so.FindProperty("titleSeManager");
            if (seProp != null)
            {
                seProp.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        ConfigVolumeUi volume = instance.GetComponent<ConfigVolumeUi>();
        if (volume != null)
        {
            SerializedObject so = new SerializedObject(volume);
            so.FindProperty("titleSeManager").objectReferenceValue = null;
            so.FindProperty("titleBgmManager").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        instance.SetActive(false);
        return instance;
    }

    private static void StripNestedCanvasComponents(GameObject root)
    {
        FixedAspectCanvasFitter fitter = root.GetComponent<FixedAspectCanvasFitter>();
        if (fitter != null)
        {
            Undo.DestroyObjectImmediate(fitter);
        }

        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Undo.DestroyObjectImmediate(raycaster);
        }

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.DestroyObjectImmediate(scaler);
        }

        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas != null)
        {
            Undo.DestroyObjectImmediate(canvas);
        }
    }

    private static void WireMainMenuController(
        KomayamaCraftMainMenuController controller,
        Button openButton,
        GameObject mainMenuRoot,
        CanvasGroup menuCanvasGroup,
        GameObject craftConfigRoot)
    {
        Transform panel = mainMenuRoot.transform.Find("MainMenuPanel");
        Button closeButton = panel.Find("MainMenuCloseButton").GetComponent<Button>();
        Button settingsButton = panel.Find("MainMenuSettingsButton").GetComponent<Button>();
        Button saveTitleButton = panel.Find("MainMenuSaveTitleButton").GetComponent<Button>();
        Button saveQuitButton = panel.Find("MainMenuSaveQuitButton").GetComponent<Button>();
        TMP_Text statusText = panel.Find("MainMenuStatusText").GetComponent<TMP_Text>();

        Transform exitTf = FindDeep(craftConfigRoot.transform, "EXITButton");
        Button exitButton = exitTf != null ? exitTf.GetComponent<Button>() : null;

        KomayamaGameClock clock = Object.FindFirstObjectByType<KomayamaGameClock>(FindObjectsInactive.Include);
        KomayamaSaveService save = Object.FindFirstObjectByType<KomayamaSaveService>(FindObjectsInactive.Include);
        KomayamaCraftSeManager se = Object.FindFirstObjectByType<KomayamaCraftSeManager>(FindObjectsInactive.Include);
        KomayamaBuildMenuSlide slide = Object.FindFirstObjectByType<KomayamaBuildMenuSlide>(FindObjectsInactive.Include);
        GameObject fadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FadePrefabPath);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("openButton").objectReferenceValue = openButton;
        so.FindProperty("mainMenuRoot").objectReferenceValue = mainMenuRoot;
        so.FindProperty("mainMenuPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        so.FindProperty("saveTitleButton").objectReferenceValue = saveTitleButton;
        so.FindProperty("saveQuitButton").objectReferenceValue = saveQuitButton;
        so.FindProperty("statusMessageText").objectReferenceValue = statusText;
        so.FindProperty("craftConfigRoot").objectReferenceValue = craftConfigRoot;
        so.FindProperty("craftConfigExitButton").objectReferenceValue = exitButton;
        so.FindProperty("menuObjectCanvasGroup").objectReferenceValue = menuCanvasGroup;
        so.FindProperty("gameClock").objectReferenceValue = clock;
        so.FindProperty("saveService").objectReferenceValue = save;
        so.FindProperty("seManager").objectReferenceValue = se;
        so.FindProperty("buildMenuSlide").objectReferenceValue = slide;
        so.FindProperty("titleSceneName").stringValue = "title_scene";
        so.FindProperty("fadeCanvasPrefab").objectReferenceValue = fadePrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureSeEntries()
    {
        KomayamaCraftSeManager se = Object.FindFirstObjectByType<KomayamaCraftSeManager>(FindObjectsInactive.Include);
        if (se == null)
        {
            Debug.LogWarning("[CraftMainMenuSceneSetup] KomayamaCraftSeManager がありません。");
            return;
        }

        AudioClip configToggle = LoadClip(ConfigToggleClipGuid);
        AudioClip transitionStart = LoadClip(TransitionStartClipGuid);

        SerializedObject so = new SerializedObject(se);
        SerializedProperty entries = so.FindProperty("entries");

        EnsureCue(entries, (int)KomayamaCraftSeCue.ShipRepairCompleteJoy, null, "宇宙船修理完了の喜びモーション開始時");
        EnsureCue(entries, (int)KomayamaCraftSeCue.MainMenuToggle, configToggle, "メインメニュー開閉（menu設定／バツ／Esc）");
        EnsureCue(entries, (int)KomayamaCraftSeCue.MainMenuSettings, configToggle, "メインメニュー「設定」押下");
        EnsureCue(entries, (int)KomayamaCraftSeCue.MainMenuSaveTitle, configToggle, "メインメニュー「セーブしてタイトル」");
        EnsureCue(entries, (int)KomayamaCraftSeCue.MainMenuSaveQuit, configToggle, "メインメニュー「セーブしてデスクトップ」〜終了待ち");
        EnsureCue(entries, (int)KomayamaCraftSeCue.TransitionToTitle, transitionStart, "タイトルへ戻る遷移開始（フェード前）");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(se);
    }

    private static void EnsureCue(SerializedProperty entries, int cueValue, AudioClip clip, string description)
    {
        int found = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("cue").enumValueIndex == cueValue)
            {
                found = i;
                break;
            }
        }

        if (found < 0)
        {
            found = entries.arraySize;
            entries.arraySize++;
        }

        SerializedProperty entry = entries.GetArrayElementAtIndex(found);
        entry.FindPropertyRelative("cue").enumValueIndex = cueValue;
        // arraySize 増分は末尾複製のため、clip は毎回明示設定する
        entry.FindPropertyRelative("clip").objectReferenceValue = clip;

        SerializedProperty vol = entry.FindPropertyRelative("volume");
        if (vol.floatValue <= 0f)
        {
            vol.floatValue = 1f;
        }

        SerializedProperty desc = entry.FindPropertyRelative("description");
        if (string.IsNullOrWhiteSpace(desc.stringValue))
        {
            desc.stringValue = description;
        }
    }

    private static AudioClip LoadClip(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static Button CreateMenuButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        float fontSize,
        Vector2 anchoredPos,
        Vector2 size,
        Sprite sprite)
    {
        GameObject go = CreateUiObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        CreateTmp("Label", go.transform, font, label, fontSize, Vector2.zero, size);
        return button;
    }

    private static TextMeshProUGUI CreateTmp(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string text,
        float fontSize,
        Vector2 anchoredPos,
        Vector2 size)
    {
        GameObject go = CreateUiObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            return font;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Basic/NotoSansJP-Regular SDF.asset");
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static Button FindButton(GameObject root, string exactName)
    {
        Transform t = FindDeep(root.transform, exactName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static TMP_Text FindTmp(GameObject root, string exactName)
    {
        Transform t = FindDeep(root.transform, exactName);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindNamed(IEnumerable<GameObject> roots, string exactName)
    {
        foreach (GameObject root in roots)
        {
            if (root != null && root.name == exactName)
            {
                return root.transform;
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string exactName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == exactName)
        {
            return root;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
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
