using KomayamaCraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KomayamaCraftM45SceneSetup
{
    [MenuItem("KomayamaCraft/M4とM5をシーンへ接続", false, 42)]
    public static void SetupFromMenu()
    {
        ItemDefinition plate = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/IronScalePlate.asset");
        ItemDefinition hull = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/HullRegenScale.asset");
        ItemDefinition ember = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/StellarFurnaceEmber.asset");
        GameObject storagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/KomayamaCraft/PrototypeStorage.prefab");
        GameObject ghostPrefab = DuplicateGhostPrefab(storagePrefab);
        FacilityDefinition ghost = UpsertGhostFacility(ghostPrefab, plate);
        UnlockDefinition tier2 = UpsertTier2Unlock(hull, ember);

        FacilityDefinitionCatalog facilityCatalog =
            AssetDatabase.LoadAssetAtPath<FacilityDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaFacilityCatalog.asset");
        var facilities = new System.Collections.Generic.List<FacilityDefinition>(
            facilityCatalog.Facilities);
        if (!facilities.Contains(ghost))
        {
            facilities.Add(ghost);
        }

        facilityCatalog.SetFacilitiesForEditor(facilities.ToArray());
        EditorUtility.SetDirty(facilityCatalog);

        UnlockDefinitionCatalog unlockCatalog =
            AssetDatabase.LoadAssetAtPath<UnlockDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaUnlockCatalog.asset");
        UnlockDefinition prototype = AssetDatabase.LoadAssetAtPath<UnlockDefinition>(
            "Assets/GameData/KomayamaCraft/Unlocks/PrototypeUnconditional.asset");
        unlockCatalog.SetUnlocksForEditor(new[] { prototype, tier2 });
        EditorUtility.SetDirty(unlockCatalog);

        Scene scene = SceneManager.GetActiveScene();
        GameObject systems = GameObject.Find("KomayamaCraftSystems");
        Transform world = GameObject.Find("World")?.transform;
        KomayamaCraftHud hud = Object.FindFirstObjectByType<KomayamaCraftHud>();
        KomayamaCraftInputController input =
            Object.FindFirstObjectByType<KomayamaCraftInputController>();
        KomayamaBuildController build =
            Object.FindFirstObjectByType<KomayamaBuildController>();
        KomayamaProgressService progress =
            systems.GetComponent<KomayamaProgressService>() ??
            systems.AddComponent<KomayamaProgressService>();
        SerializedObject progressSerialized = new(progress);
        progressSerialized.FindProperty("hud").objectReferenceValue = hud;
        progressSerialized.FindProperty("input").objectReferenceValue = input;
        progressSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject shipObject = GameObject.Find("CrashedShip");
        if (shipObject == null)
        {
            shipObject = new GameObject("CrashedShip");
            SceneManager.MoveGameObjectToScene(shipObject, scene);
            if (world != null)
            {
                shipObject.transform.SetParent(world, false);
            }

            shipObject.transform.position = new Vector3(-7.5f, 3.2f, 0f);
            shipObject.layer = LayerMask.NameToLayer("Facility");
            SpriteRenderer renderer = shipObject.AddComponent<SpriteRenderer>();
            renderer.color = new Color(0.75f, 0.8f, 0.95f, 1f);
            if (storagePrefab.TryGetComponent(out SpriteRenderer source))
            {
                renderer.sprite = source.sprite;
            }

            renderer.sortingOrder = 25;
            BoxCollider2D box = shipObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.8f, 1.2f);
            shipObject.AddComponent<KomayamaShip>();
        }

        KomayamaShip ship = shipObject.GetComponent<KomayamaShip>();
        SerializedObject shipSerialized = new(ship);
        shipSerialized.FindProperty("hand").objectReferenceValue =
            Object.FindFirstObjectByType<KomayamaHandInventory>();
        shipSerialized.FindProperty("hud").objectReferenceValue = hud;
        shipSerialized.FindProperty("progress").objectReferenceValue = progress;
        shipSerialized.FindProperty("hullSlotItem").objectReferenceValue = hull;
        shipSerialized.FindProperty("emberSlotItem").objectReferenceValue = ember;
        shipSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject regionObject = GameObject.Find("Tier2Region");
        if (regionObject == null)
        {
            regionObject = new GameObject("Tier2Region");
            SceneManager.MoveGameObjectToScene(regionObject, scene);
            if (world != null)
            {
                regionObject.transform.SetParent(world, false);
            }

            regionObject.transform.position = new Vector3(7.2f, 3.2f, 0f);
            SpriteRenderer marker = regionObject.AddComponent<SpriteRenderer>();
            if (storagePrefab.TryGetComponent(out SpriteRenderer source))
            {
                marker.sprite = source.sprite;
            }

            marker.color = new Color(1f, 0.25f, 0.25f, 0.25f);
            marker.sortingOrder = 5;
            BoxCollider2D box = regionObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.4f, 2.4f);
            regionObject.AddComponent<KomayamaRegion>();
        }

        SerializedObject regionSerialized = new(regionObject.GetComponent<KomayamaRegion>());
        regionSerialized.FindProperty("regionId").stringValue = "tier2";
        regionSerialized.FindProperty("progress").objectReferenceValue = progress;
        regionSerialized.FindProperty("marker").objectReferenceValue =
            regionObject.GetComponent<SpriteRenderer>();
        regionSerialized.ApplyModifiedPropertiesWithoutUndo();

        FacilityDefinition workbench = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(
            "Assets/GameData/KomayamaCraft/Facilities/ScaleRollingWorkbench.asset");
        FacilityDefinition storage = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(
            "Assets/GameData/KomayamaCraft/Facilities/PrototypeStorage.asset");
        FacilityDefinition vat = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(
            "Assets/GameData/KomayamaCraft/Facilities/SymbioticAssemblyVat.asset");
        SerializedObject buildSerialized = new(build);
        SerializedProperty buildables = buildSerialized.FindProperty("buildableFacilities");
        buildables.arraySize = 4;
        buildables.GetArrayElementAtIndex(0).objectReferenceValue = workbench;
        buildables.GetArrayElementAtIndex(1).objectReferenceValue = storage;
        buildables.GetArrayElementAtIndex(2).objectReferenceValue = vat;
        buildables.GetArrayElementAtIndex(3).objectReferenceValue = ghost;
        buildSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject hudSerialized = new(hud);
        hudSerialized.FindProperty("progress").objectReferenceValue = progress;
        hudSerialized.FindProperty("ship").objectReferenceValue = ship;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[KomayamaCraftM45] Ghost, ship, region, and progress are wired.");
    }

    private static GameObject DuplicateGhostPrefab(GameObject source)
    {
        const string path = "Assets/Prefabs/KomayamaCraft/CourierGhost.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject contents = existing != null
            ? PrefabUtility.LoadPrefabContents(path)
            : PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        contents.name = "CourierGhost";
        contents.layer = LayerMask.NameToLayer("Facility");
        contents.transform.localScale = Vector3.one * 0.9f;
        if (contents.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer.color = new Color(0.85f, 0.55f, 1f, 1f);
        }

        KomayamaStorageFacility storage = contents.GetComponent<KomayamaStorageFacility>();
        if (storage != null)
        {
            Object.DestroyImmediate(storage, true);
        }

        KomayamaProcessingFacility processing = contents.GetComponent<KomayamaProcessingFacility>();
        if (processing != null)
        {
            Object.DestroyImmediate(processing, true);
        }

        if (contents.GetComponent<KomayamaGhost>() == null)
        {
            contents.AddComponent<KomayamaGhost>();
        }

        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static FacilityDefinition UpsertGhostFacility(GameObject prefab, ItemDefinition plate)
    {
        const string path = "Assets/GameData/KomayamaCraft/Facilities/CourierGhost.asset";
        FacilityDefinition facility = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
        if (facility == null)
        {
            facility = ScriptableObject.CreateInstance<FacilityDefinition>();
            AssetDatabase.CreateAsset(facility, path);
        }

        SerializedObject serialized = new(facility);
        serialized.FindProperty("definitionId").stringValue = "facility.courier_ghost";
        serialized.FindProperty("displayName").stringValue = "Courier Ghost";
        serialized.FindProperty("description").stringValue = "地面回収、指定採集、燃料補給を行う作業者。";
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.FindProperty("capabilities").intValue = (int)FacilityCapability.Automation;
        serialized.FindProperty("supportedRecipes").arraySize = 0;
        SerializedProperty cost = serialized.FindProperty("constructionCost");
        cost.arraySize = 1;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = plate;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 2;
        serialized.FindProperty("inputCapacity").intValue = 0;
        serialized.FindProperty("outputCapacity").intValue = 0;
        serialized.FindProperty("storageCapacity").intValue = 0;
        serialized.FindProperty("usesFuel").boolValue = false;
        serialized.FindProperty("fuelCapacity").intValue = 0;
        serialized.FindProperty("acceptedFuelItems").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(facility);
        return facility;
    }

    private static UnlockDefinition UpsertTier2Unlock(ItemDefinition hull, ItemDefinition ember)
    {
        const string path = "Assets/GameData/KomayamaCraft/Unlocks/Tier2ShipRepair.asset";
        UnlockDefinition unlock = AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);
        if (unlock == null)
        {
            unlock = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(unlock, path);
        }

        SerializedObject serialized = new(unlock);
        serialized.FindProperty("definitionId").stringValue = "unlock.tier2_ship_repair";
        serialized.FindProperty("displayName").stringValue = "応急修理完了";
        serialized.FindProperty("description").stringValue = "船殻と補助動力を直すとTier 2が開く。";
        SerializedProperty groups = serialized.FindProperty("anyOfConditionGroups");
        groups.arraySize = 1;
        SerializedProperty conditions =
            groups.GetArrayElementAtIndex(0).FindPropertyRelative("allConditions");
        conditions.arraySize = 2;
        WriteDelivered(conditions.GetArrayElementAtIndex(0), hull);
        WriteDelivered(conditions.GetArrayElementAtIndex(1), ember);
        SerializedProperty targets = serialized.FindProperty("targets");
        targets.arraySize = 2;
        SerializedProperty tier = targets.GetArrayElementAtIndex(0);
        tier.FindPropertyRelative("targetType").enumValueIndex = (int)UnlockTargetType.Tier;
        tier.FindPropertyRelative("tier").intValue = 2;
        SerializedProperty region = targets.GetArrayElementAtIndex(1);
        region.FindPropertyRelative("targetType").enumValueIndex = (int)UnlockTargetType.Region;
        region.FindPropertyRelative("identifier").stringValue = "tier2";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(unlock);
        return unlock;
    }

    private static void WriteDelivered(SerializedProperty condition, ItemDefinition item)
    {
        condition.FindPropertyRelative("conditionType").enumValueIndex =
            (int)UnlockConditionType.ItemDelivered;
        condition.FindPropertyRelative("deliveredItem").objectReferenceValue = item;
        condition.FindPropertyRelative("deliveredAmount").intValue = 1;
    }
}
