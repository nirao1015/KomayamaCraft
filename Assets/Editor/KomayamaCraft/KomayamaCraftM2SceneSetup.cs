using KomayamaCraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class KomayamaCraftM2SceneSetup
{
    private const string StoragePrefabPath =
        "Assets/Prefabs/KomayamaCraft/PrototypeStorage.prefab";
    private const string WorkbenchDefinitionPath =
        "Assets/GameData/KomayamaCraft/Facilities/ScaleRollingWorkbench.asset";
    private const string StorageDefinitionPath =
        "Assets/GameData/KomayamaCraft/Facilities/PrototypeStorage.asset";

    [MenuItem("KomayamaCraft/M2をシーンへ接続", false, 40)]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/komayama_craft_scene.unity")
        {
            throw new System.InvalidOperationException(
                "Open komayama_craft_scene before setting up M2.");
        }

        EnsureStoragePrefab();
        Transform world = FindNamed(scene, "World")?.transform;
        KomayamaDropArea dropArea = Object.FindFirstObjectByType<KomayamaDropArea>();
        KomayamaCraftHud hud = Object.FindFirstObjectByType<KomayamaCraftHud>();
        KomayamaCraftSeManager se = Object.FindFirstObjectByType<KomayamaCraftSeManager>();
        KomayamaHandInventory hand = Object.FindFirstObjectByType<KomayamaHandInventory>();
        KomayamaCraftInputController input =
            Object.FindFirstObjectByType<KomayamaCraftInputController>();
        Camera camera = Object.FindFirstObjectByType<Camera>();

        Collider2D surface = EnsurePlacementSurface(scene, world);
        Tilemap noBuild = EnsureNoBuildPaint(scene, world);
        SpriteRenderer preview = EnsurePreview(scene, world);
        Transform facilityRoot = EnsureFacilityLayer(scene, world);
        EnsureStorageInstance(scene, facilityRoot, hud, se);
        HideLayerFromCameras(scene, "PlacementBlocker");

        GameObject systems = FindNamed(scene, "KomayamaCraftSystems");
        if (systems == null)
        {
            throw new System.InvalidOperationException("KomayamaCraftSystems is missing.");
        }

        KomayamaBuildController build = systems.GetComponent<KomayamaBuildController>();
        if (build == null)
        {
            build = systems.AddComponent<KomayamaBuildController>();
        }

        KomayamaSaveService save = systems.GetComponent<KomayamaSaveService>();
        if (save == null)
        {
            save = systems.AddComponent<KomayamaSaveService>();
        }

        FacilityDefinition workbench =
            AssetDatabase.LoadAssetAtPath<FacilityDefinition>(WorkbenchDefinitionPath);
        FacilityDefinition storage =
            AssetDatabase.LoadAssetAtPath<FacilityDefinition>(StorageDefinitionPath);

        SetObject(build, "hand", hand);
        SetObject(build, "dropArea", dropArea);
        SetObject(build, "hud", hud);
        SetObject(build, "seManager", se);
        SetObject(build, "placementSurface", surface);
        SetObject(build, "noBuildPaint", noBuild);
        SetObject(build, "previewRenderer", preview);
        SetObject(build, "facilityRoot", facilityRoot);
        SetLayerMask(
            build,
            "blockedLayers",
            LayerMask.GetMask(
                "WorldStatic",
                "Facility",
                "ResourceNode",
                "NativeLife",
                "PlacementBlocker"));
        SerializedObject buildSerialized = new(build);
        SerializedProperty facilities = buildSerialized.FindProperty("buildableFacilities");
        facilities.arraySize = 2;
        facilities.GetArrayElementAtIndex(0).objectReferenceValue = workbench;
        facilities.GetArrayElementAtIndex(1).objectReferenceValue = storage;
        buildSerialized.ApplyModifiedPropertiesWithoutUndo();

        SetObject(save, "hand", hand);
        SetObject(save, "dropArea", dropArea);
        SetObject(save, "buildController", build);
        SetObject(save, "hud", hud);
        SetObject(save, "droppedItemRoot", dropArea != null ? dropArea.transform : world);

        SetObject(input, "buildController", build);
        SetObject(hud, "buildController", build);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[KomayamaCraftM2] Scene wiring is ready.");
    }

    [MenuItem("KomayamaCraft/セーブ", false, 50)]
    public static void SaveFromMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[KomayamaCraft] Enter Play Mode before saving.");
            return;
        }

        Object.FindFirstObjectByType<KomayamaSaveService>()?.TrySave();
    }

    [MenuItem("KomayamaCraft/ロード", false, 51)]
    public static void LoadFromMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[KomayamaCraft] Enter Play Mode before loading.");
            return;
        }

        Object.FindFirstObjectByType<KomayamaSaveService>()?.TryLoad();
    }

    private static void EnsureStoragePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoragePrefabPath);
        if (prefab == null)
        {
            throw new System.InvalidOperationException("PrototypeStorage prefab is missing.");
        }

        string tempPath = "Assets/Prefabs/KomayamaCraft/_StorageEdit.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(StoragePrefabPath);
        contents.layer = LayerMask.NameToLayer("Facility");
        if (!contents.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer = contents.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/GameData/KomayamaCraft/NoDropPaintCell.png") ??
            renderer.sprite;
        if (renderer.sprite == null)
        {
            GameObject workbench = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/KomayamaCraft/ScaleRollingWorkbench.prefab");
            if (workbench != null && workbench.TryGetComponent(out SpriteRenderer source))
            {
                renderer.sprite = source.sprite;
            }
        }

        renderer.color = new Color(0.45f, 0.72f, 1f, 1f);
        renderer.sortingOrder = 20;
        contents.transform.localScale = Vector3.one * 1.2f;
        if (!contents.TryGetComponent(out BoxCollider2D collider))
        {
            collider = contents.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        collider.size = new Vector2(1.6f, 1.6f);
        if (!contents.TryGetComponent(out KomayamaStorageFacility storage))
        {
            storage = contents.AddComponent<KomayamaStorageFacility>();
        }

        SetObject(
            storage,
            "definition",
            AssetDatabase.LoadAssetAtPath<FacilityDefinition>(StorageDefinitionPath));
        PrefabUtility.SaveAsPrefabAsset(contents, StoragePrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        _ = tempPath;
    }

    private static Collider2D EnsurePlacementSurface(Scene scene, Transform world)
    {
        GameObject existing = FindNamed(scene, "FieldPlacementSurface");
        if (existing == null)
        {
            existing = new GameObject("FieldPlacementSurface");
            SceneManager.MoveGameObjectToScene(existing, scene);
            if (world != null)
            {
                existing.transform.SetParent(world, false);
            }
        }

        existing.layer = LayerMask.NameToLayer("PlacementSurface");
        existing.SetActive(true);
        BoxCollider2D box = existing.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = existing.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = true;
        // FieldDropArea と同程度の建設可能範囲（未設定時のフォールバックは小さすぎる）
        GameObject dropArea = FindNamed(scene, "FieldDropArea");
        BoxCollider2D dropBox = dropArea != null
            ? dropArea.GetComponent<BoxCollider2D>()
            : null;
        if (dropBox != null)
        {
            box.size = dropBox.size;
            box.offset = dropBox.offset;
            existing.transform.position = dropArea.transform.position;
        }
        else if (box.size.x < 1f || box.size.y < 1f)
        {
            box.size = new Vector2(115.2f, 86.4f);
        }

        return box;
    }

    private static Tilemap EnsureNoBuildPaint(Scene scene, Transform world)
    {
        GameObject gridObject = FindNamed(scene, "BuildRestrictionGrid");
        if (gridObject == null)
        {
            gridObject = new GameObject("BuildRestrictionGrid");
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            if (world != null)
            {
                gridObject.transform.SetParent(world, false);
            }

            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(0.5f, 0.5f, 1f);
        }
        else
        {
            Grid existingGrid = gridObject.GetComponent<Grid>();
            if (existingGrid != null)
            {
                existingGrid.cellSize = new Vector3(0.5f, 0.5f, 1f);
            }
        }

        Transform paintTransform = gridObject.transform.Find("NoBuildPaint");
        GameObject paintObject = paintTransform != null
            ? paintTransform.gameObject
            : new GameObject("NoBuildPaint");
        if (paintTransform == null)
        {
            paintObject.transform.SetParent(gridObject.transform, false);
        }

        int layer = LayerMask.NameToLayer("PlacementBlocker");
        paintObject.layer = layer;
        Tilemap tilemap = paintObject.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            tilemap = paintObject.AddComponent<Tilemap>();
        }

        // 建設不可は緑。ドロップ禁止（赤）と区別する。
        tilemap.color = new Color(0.2f, 0.85f, 0.35f, 0.4f);
        if (paintObject.GetComponent<TilemapRenderer>() == null)
        {
            TilemapRenderer renderer = paintObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 99;
        }

        return tilemap;
    }

    private static SpriteRenderer EnsurePreview(Scene scene, Transform world)
    {
        GameObject existing = FindNamed(scene, "BuildPreview");
        if (existing == null)
        {
            existing = new GameObject("BuildPreview");
            SceneManager.MoveGameObjectToScene(existing, scene);
            if (world != null)
            {
                existing.transform.SetParent(world, false);
            }
        }

        SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = existing.AddComponent<SpriteRenderer>();
        }
        renderer.sortingOrder = 80;
        renderer.enabled = false;
        return renderer;
    }

    /// <summary>
    /// WorldLayers の次・Layer_Mouse の前に建設物ルートを確保する。
    /// </summary>
    private static Transform EnsureFacilityLayer(Scene scene, Transform world)
    {
        GameObject existing = FindNamed(scene, "Layer_Facilities");
        if (existing == null)
        {
            existing = new GameObject("Layer_Facilities");
            SceneManager.MoveGameObjectToScene(existing, scene);
            if (world != null)
            {
                existing.transform.SetParent(world, false);
            }
        }
        else if (world != null && existing.transform.parent != world)
        {
            existing.transform.SetParent(world, true);
        }

        if (world != null)
        {
            int worldLayersIndex = -1;
            int mouseIndex = -1;
            for (int i = 0; i < world.childCount; i++)
            {
                string childName = world.GetChild(i).name;
                if (childName == "WorldLayers")
                {
                    worldLayersIndex = i;
                }

                if (childName == "Layer_Mouse")
                {
                    mouseIndex = i;
                }
            }

            int targetIndex = worldLayersIndex >= 0 ? worldLayersIndex + 1 : 0;
            if (mouseIndex >= 0 && targetIndex > mouseIndex)
            {
                targetIndex = mouseIndex;
            }

            existing.transform.SetSiblingIndex(targetIndex);
        }

        return existing.transform;
    }

    private static void EnsureStorageInstance(
        Scene scene,
        Transform facilityRoot,
        KomayamaCraftHud hud,
        KomayamaCraftSeManager se)
    {
        if (Object.FindFirstObjectByType<KomayamaStorageFacility>() != null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoragePrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "PrototypeStorage";
        instance.transform.position = new Vector3(0f, 2.2f, 0f);
        if (facilityRoot != null)
        {
            instance.transform.SetParent(facilityRoot, true);
        }

        if (instance.TryGetComponent(out KomayamaStorageFacility storage))
        {
            storage.BindRuntime(hud, se);
        }
    }

    private static void HideLayerFromCameras(Scene scene, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].gameObject.scene == scene)
            {
                cameras[i].cullingMask &= ~(1 << layer);
            }
        }
    }

    private static GameObject FindNamed(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChild(roots[i].transform, name);
            if (found != null)
            {
                return found.gameObject;
            }

            if (roots[i].name == name)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChild(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetObject(
        Object target,
        string propertyName,
        Object value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
