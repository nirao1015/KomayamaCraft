#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KomayamaCraft;

/// <summary>
/// タイトル ConfigCanvas を共通プレハブ化し、title / craft 両シーンへ差し替える。
/// </summary>
public static class ConfigCanvasPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/KomayamaCraft/ConfigCanvas.prefab";
    private const string TitleScenePath = "Assets/Scenes/title_scene.unity";
    private const string CraftScenePath = "Assets/Scenes/komayama_craft_scene.unity";

    [MenuItem("KomayamaCraft/Shared/Setup ConfigCanvas Prefab (Title+Craft)")]
    public static void Setup()
    {
        string folder = Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        GameObject prefabRoot = CreateOrRepairPrefab();
        if (prefabRoot == null)
        {
            Debug.LogError("[ConfigCanvasPrefabSetup] プレハブ作成に失敗しました。");
            return;
        }

        PlaceTitleInstance(prefabRoot);
        PlaceCraftInstance(prefabRoot);

        AssetDatabase.SaveAssets();
        Debug.Log("[ConfigCanvasPrefabSetup] ConfigCanvas プレハブ化と両シーン差し替え完了: " + PrefabPath);
    }

    /// <summary>
    /// 既存プレハブのルート scale/Rect を直し、title/craft へ再配置する。
    /// </summary>
    [MenuItem("KomayamaCraft/Shared/Repair ConfigCanvas Prefab Scale")]
    public static void RepairScale()
    {
        Setup();
    }

    private static GameObject CreateOrRepairPrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RemoveMissingScriptsDeep(contents);
                NormalizeTitleRootRect(contents);
                PrepareSharedComponents(contents, clearTitleSideRefs: true);
                ClearPersistentButtonListeners(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        Scene titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        GameObject source = FindRootOrDeep(titleScene, "ConfigCanvas");
        if (source == null)
        {
            Debug.LogError("[ConfigCanvasPrefabSetup] title_scene に ConfigCanvas がありません。");
            return null;
        }

        GameObject work = Object.Instantiate(source);
        work.name = "ConfigCanvas";
        work.SetActive(true);
        // Instantiate 直後に親 scale の影響で localScale が崩れることがあるので必ず正規化
        work.transform.SetParent(null, false);
        RemoveMissingScriptsDeep(work);
        NormalizeTitleRootRect(work);
        PrepareSharedComponents(work, clearTitleSideRefs: true);
        ClearPersistentButtonListeners(work);

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(work, PrefabPath);
        Object.DestroyImmediate(work);
        return prefabAsset;
    }

    private static void NormalizeTitleRootRect(GameObject root)
    {
        RectTransform rt = root.GetComponent<RectTransform>();
        if (rt == null)
        {
            return;
        }

        // タイトル用 Overlay ルート: 画面全体・scale 1 を強制（0 や親由来の崩れを防ぐ）
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = false;
            if (canvas.sortingOrder < 300)
            {
                canvas.sortingOrder = 300;
            }
        }
    }

    private static void NormalizePrefabRootRectAsset()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoveMissingScriptsDeep(contents);
            NormalizeTitleRootRect(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void PlaceTitleInstance(GameObject prefabAsset)
    {
        Scene titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

        GameObject old = FindRootOrDeep(titleScene, "ConfigCanvas");
        int sibling = -1;
        if (old != null)
        {
            sibling = old.transform.GetSiblingIndex();
            Undo.DestroyObjectImmediate(old);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, titleScene);
        instance.name = "ConfigCanvas";
        if (sibling >= 0)
        {
            instance.transform.SetSiblingIndex(sibling);
        }

        NormalizeTitleRootRect(instance);
        instance.SetActive(false);
        PrepareSharedComponents(instance, clearTitleSideRefs: false);
        ClearPersistentButtonListeners(instance);

        TitleSeManager se = Object.FindFirstObjectByType<TitleSeManager>(FindObjectsInactive.Include);
        TitleBgmManager bgm = Object.FindFirstObjectByType<TitleBgmManager>(FindObjectsInactive.Include);

        // 設定 SE はプレハブ ConfigSePlayer（タイトル TitleSeManager から同期済み）。BGM 反映だけタイトル固有。
        ConfigVolumeUi volume = instance.GetComponent<ConfigVolumeUi>();
        if (volume != null)
        {
            SerializedObject so = new SerializedObject(volume);
            so.FindProperty("titleBgmManager").objectReferenceValue = bgm;
            so.FindProperty("titleSeManager").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        TitleConfigPanelController panel = instance.GetComponent<TitleConfigPanelController>();
        if (panel != null)
        {
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("titleSeManager").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 設定 SE クリップだけプレハブへ反映（Transform 全体 Apply は scale 0 を焼き込むことがある）
        ConfigSePlayer sePlayer = instance.GetComponent<ConfigSePlayer>();
        if (sePlayer != null && se != null)
        {
            sePlayer.EditorApplyFromTitleSeManager(se);
            PrefabUtility.ApplyObjectOverride(sePlayer, PrefabPath, InteractionMode.AutomatedAction);
        }

        // ルート scale/Rect をプレハブにも強制正規化
        NormalizePrefabRootRectAsset();

        TitleTransitionManager transition =
            Object.FindFirstObjectByType<TitleTransitionManager>(FindObjectsInactive.Include);
        if (transition != null)
        {
            Transform exitTf = FindDeep(instance.transform, "EXITButton");
            Button exitButton = exitTf != null ? exitTf.GetComponent<Button>() : null;
            SerializedObject so = new SerializedObject(transition);
            so.FindProperty("configCanvas").objectReferenceValue = instance;
            so.FindProperty("configExitButton").objectReferenceValue = exitButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(transition);
        }

        EditorSceneManager.MarkSceneDirty(titleScene);
        EditorSceneManager.SaveScene(titleScene);
    }

    private static void PlaceCraftInstance(GameObject prefabAsset)
    {
        Scene craftScene = EditorSceneManager.OpenScene(CraftScenePath, OpenSceneMode.Single);
        Transform systemCanvas = FindRootOrDeep(craftScene, "SystemCanvas")?.transform;
        if (systemCanvas == null)
        {
            Debug.LogError("[ConfigCanvasPrefabSetup] SystemCanvas がありません。");
            return;
        }

        GameObject old = null;
        Transform oldCraft = systemCanvas.Find("CraftConfigCanvas");
        if (oldCraft != null)
        {
            old = oldCraft.gameObject;
        }
        else
        {
            old = FindRootOrDeep(craftScene, "CraftConfigCanvas");
        }

        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, craftScene);
        instance.name = "CraftConfigCanvas";
        // worldPositionStays=false で親 scale(0.01) の影響を localScale に焼き込まない
        instance.transform.SetParent(systemCanvas, false);
        instance.transform.SetAsLastSibling();

        // SystemCanvas 配下は MenuRoot 等と同じく「ネスト Canvas なし」で描画する
        StripNestedCanvasComponents(instance);
        StretchUnderSystemCanvas(instance);

        PrepareSharedComponents(instance, clearTitleSideRefs: true);
        ClearPersistentButtonListeners(instance);
        instance.SetActive(false);

        Transform exitTf = FindDeep(instance.transform, "EXITButton");
        Button exitButton = exitTf != null ? exitTf.GetComponent<Button>() : null;

        KomayamaCraftMainMenuController controller =
            Object.FindFirstObjectByType<KomayamaCraftMainMenuController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("craftConfigRoot").objectReferenceValue = instance;
            so.FindProperty("craftConfigExitButton").objectReferenceValue = exitButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(craftScene);
        EditorSceneManager.SaveScene(craftScene);
    }

    private static void StretchUnderSystemCanvas(GameObject root)
    {
        RectTransform rt = root.GetComponent<RectTransform>();
        if (rt == null)
        {
            return;
        }

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static void StripNestedCanvasComponents(GameObject root)
    {
        // 親 SystemCanvas が Canvas/Scaler/Raycaster を持つ。二重 Canvas は scale/描画で事故りやすい。
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

    private static void RemoveMissingScriptsDeep(GameObject root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(all[i].gameObject);
        }
    }

    private static void PrepareSharedComponents(GameObject root, bool clearTitleSideRefs)
    {
        TitleSeManager titleSeForClips =
            Object.FindFirstObjectByType<TitleSeManager>(FindObjectsInactive.Include);

        ConfigSePlayer sePlayer = EnsureConfigSePlayer(root, titleSeForClips);

        TitleConfigPanelController panel =
            root.GetComponent<TitleConfigPanelController>()
            ?? Undo.AddComponent<TitleConfigPanelController>(root);

        SerializedObject panelSo = new SerializedObject(panel);
        panelSo.FindProperty("configSePlayer").objectReferenceValue = sePlayer;
        // 設定 SE はプレハブの ConfigSePlayer（タイトル設定のコピー）を使う。シーン固有 TitleSeManager は不要。
        panelSo.FindProperty("titleSeManager").objectReferenceValue = null;
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        // 旧クラフト専用コンポーネントが残っていれば除去
        Component obsolete = root.GetComponent("KomayamaCraftConfigVolumeUi");
        if (obsolete != null)
        {
            Undo.DestroyObjectImmediate(obsolete);
        }

        ConfigVolumeUi volume =
            root.GetComponent<ConfigVolumeUi>()
            ?? Undo.AddComponent<ConfigVolumeUi>(root);

        SerializedObject volSo = new SerializedObject(volume);
        volSo.FindProperty("masterMinusButton").objectReferenceValue = FindButton(root, "MasterMinusButton");
        volSo.FindProperty("masterPlusButton").objectReferenceValue = FindButton(root, "MasterPlusButton");
        volSo.FindProperty("bgmMinusButton").objectReferenceValue = FindButton(root, "BgmMinusButton");
        volSo.FindProperty("bgmPlusButton").objectReferenceValue = FindButton(root, "BgmPlusButton");
        volSo.FindProperty("seMinusButton").objectReferenceValue = FindButton(root, "SeMinusButton");
        volSo.FindProperty("sePlusButton").objectReferenceValue = FindButton(root, "SePlusButton");
        volSo.FindProperty("masterValueText").objectReferenceValue = FindTmp(root, "MasterValueText");
        volSo.FindProperty("bgmValueText").objectReferenceValue = FindTmp(root, "BgmValueText");
        volSo.FindProperty("seValueText").objectReferenceValue = FindTmp(root, "SeValueText");
        volSo.FindProperty("configSePlayer").objectReferenceValue = sePlayer;
        volSo.FindProperty("titleSeManager").objectReferenceValue = null;

        if (clearTitleSideRefs)
        {
            volSo.FindProperty("titleBgmManager").objectReferenceValue = null;
        }

        volSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(volume);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(sePlayer);
    }

    private static ConfigSePlayer EnsureConfigSePlayer(GameObject root, TitleSeManager titleSe)
    {
        AudioSource source = root.GetComponent<AudioSource>();
        if (source == null)
        {
            source = Undo.AddComponent<AudioSource>(root);
        }

        source.playOnAwake = false;

        ConfigSePlayer player =
            root.GetComponent<ConfigSePlayer>()
            ?? Undo.AddComponent<ConfigSePlayer>(root);

        SerializedObject so = new SerializedObject(player);
        so.FindProperty("playbackSource").objectReferenceValue = source;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (titleSe != null)
        {
            player.EditorApplyFromTitleSeManager(titleSe);
        }

        return player;
    }

    private static void ClearPersistentButtonListeners(GameObject root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            // EXIT などシーン固有の永続参照を落とす（コード側で再配線）
            if (button.name == "EXITButton" ||
                button.name.Contains("Minus") ||
                button.name.Contains("Plus"))
            {
                SerializedObject so = new SerializedObject(button);
                SerializedProperty onClick = so.FindProperty("m_OnClick");
                if (onClick != null)
                {
                    SerializedProperty calls = onClick.FindPropertyRelative("m_PersistentCalls.m_Calls");
                    if (calls != null)
                    {
                        calls.ClearArray();
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }
    }

    private static GameObject FindRootOrDeep(Scene scene, string exactName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == exactName)
            {
                return roots[i];
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeep(roots[i].transform, exactName);
            if (found != null)
            {
                return found.gameObject;
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
}
#endif
