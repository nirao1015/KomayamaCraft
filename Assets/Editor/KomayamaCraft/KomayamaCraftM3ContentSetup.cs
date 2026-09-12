using System.Collections.Generic;
using KomayamaCraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KomayamaCraftM3ContentSetup
{
    private const string ItemsFolder = "Assets/GameData/KomayamaCraft/Items";
    private const string RecipesFolder = "Assets/GameData/KomayamaCraft/Recipes";
    private const string FacilitiesFolder = "Assets/GameData/KomayamaCraft/Facilities";
    private const string NodesFolder = "Assets/GameData/KomayamaCraft/ResourceNodes";
    private const string PrefabsFolder = "Assets/Prefabs/KomayamaCraft";

    [MenuItem("KomayamaCraft/M3をシーンへ接続", false, 41)]
    public static void SetupFromMenu()
    {
        EnsureFolders();

        ItemDefinition ironScale = Load<ItemDefinition>($"{ItemsFolder}/IronScale.asset");
        ItemDefinition ironScalePlate = Load<ItemDefinition>($"{ItemsFolder}/IronScalePlate.asset");
        ItemDefinition heatSacHerb = UpsertItem(
            "HeatSacHerb",
            "item.heat_sac_herb",
            "熱嚢草の嚢",
            "熱を蓄えた草から採取できる燃料素材。",
            ItemCategory.Fuel);
        ItemDefinition chargeJelly = UpsertItem(
            "ChargeJelly",
            "item.charge_jellyfish",
            "蓄電クラゲ片",
            "触れると痺れるクラゲの断片。",
            ItemCategory.RawMaterial);
        ItemDefinition scaleRivet = UpsertItem(
            "ScaleRivet",
            "item.scale_rivet",
            "鱗リベット",
            "鉄鱗を打ち出した接合部品。",
            ItemCategory.IntermediateMaterial);
        ItemDefinition radiatorPlate = UpsertItem(
            "RadiatorScalePlate",
            "item.radiator_scale_plate",
            "放熱鱗板",
            "熱を逃すために圧延した鱗板。",
            ItemCategory.IntermediateMaterial);
        ItemDefinition reinforceFrame = UpsertItem(
            "ReinforceFrame",
            "item.reinforce_frame",
            "補強フレーム",
            "鱗板とリベットで組んだ骨組。",
            ItemCategory.IntermediateMaterial);
        ItemDefinition bioPacking = UpsertItem(
            "BioPacking",
            "item.bio_packing",
            "生体パッキン",
            "熱嚢素材から培養した密封部品。",
            ItemCategory.Component);
        ItemDefinition heatSacCell = UpsertItem(
            "HeatSacCell",
            "item.heat_sac_cell",
            "熱嚢セル",
            "安定化した熱源。設備燃料にもなる。",
            ItemCategory.Fuel);
        ItemDefinition chargeCell = UpsertItem(
            "ChargeCell",
            "item.charge_cell",
            "蓄電セル",
            "クラゲ片を固めた補助電源。",
            ItemCategory.Component);
        ItemDefinition regenMembrane = UpsertItem(
            "RegenMembrane",
            "item.regen_membrane",
            "再生膜",
            "船体の傷を埋める生体膜。",
            ItemCategory.Component);
        ItemDefinition hullRegenScale = UpsertItem(
            "HullRegenScale",
            "item.hull_regen_scale",
            "船殻再生鱗",
            "船体応急修理用の完成部品。",
            ItemCategory.RepairPart);
        ItemDefinition stellarEmber = UpsertItem(
            "StellarFurnaceEmber",
            "item.stellar_furnace_ember",
            "恒星炉の種火",
            "補助動力復旧用の完成部品。",
            ItemCategory.RepairPart);

        GameObject beastPrefab = Load<GameObject>($"{PrefabsFolder}/IronScaleBeast.prefab");
        GameObject workbenchPrefab = Load<GameObject>($"{PrefabsFolder}/ScaleRollingWorkbench.prefab");
        GameObject heatPrefab = DuplicateTintedPrefab(
            beastPrefab,
            $"{PrefabsFolder}/HeatSacHerb.prefab",
            "HeatSacHerb",
            new Color(0.95f, 0.45f, 0.2f, 1f),
            1.15f);
        GameObject jellyPrefab = DuplicateTintedPrefab(
            beastPrefab,
            $"{PrefabsFolder}/ChargeJellyfish.prefab",
            "ChargeJellyfish",
            new Color(0.35f, 0.85f, 1f, 1f),
            1.1f);
        GameObject vatPrefab = DuplicateTintedPrefab(
            workbenchPrefab,
            $"{PrefabsFolder}/SymbioticAssemblyVat.prefab",
            "SymbioticAssemblyVat",
            new Color(0.55f, 0.95f, 0.45f, 1f),
            1.35f);

        RecipeDefinition rolling = Load<RecipeDefinition>($"{RecipesFolder}/IronScaleRolling.asset");
        RecipeDefinition rivet = UpsertRecipe(
            "ScaleRivet",
            "recipe.scale_rivet",
            "鱗リベット打ち",
            "facility.scale_rolling_workbench",
            false,
            1.2f,
            RecipeOutputMode.Fixed,
            Inputs(ironScale, 1),
            Outputs(scaleRivet, 1, 1));
        RecipeDefinition radiator = UpsertRecipe(
            "RadiatorScalePlate",
            "recipe.radiator_scale_plate",
            "放熱鱗板圧延",
            "facility.scale_rolling_workbench",
            true,
            2f,
            RecipeOutputMode.Fixed,
            Inputs(ironScalePlate, 1),
            Outputs(radiatorPlate, 1, 1));
        RecipeDefinition frame = UpsertRecipe(
            "ReinforceFrame",
            "recipe.reinforce_frame",
            "補強フレーム組立",
            "facility.scale_rolling_workbench",
            true,
            2f,
            RecipeOutputMode.Fixed,
            Inputs(ironScalePlate, 1, scaleRivet, 1),
            Outputs(reinforceFrame, 1, 1));
        RecipeDefinition packing = UpsertRecipe(
            "BioPacking",
            "recipe.bio_packing",
            "生体パッキン培養",
            "facility.symbiotic_assembly_vat",
            false,
            1.5f,
            RecipeOutputMode.Fixed,
            Inputs(heatSacHerb, 1),
            Outputs(bioPacking, 1, 1));
        RecipeDefinition heatCell = UpsertRecipe(
            "HeatSacCell",
            "recipe.heat_sac_cell",
            "熱嚢セル安定化",
            "facility.symbiotic_assembly_vat",
            false,
            1.5f,
            RecipeOutputMode.Fixed,
            Inputs(heatSacHerb, 1),
            Outputs(heatSacCell, 1, 1));
        RecipeDefinition chargeCellRecipe = UpsertRecipe(
            "ChargeCell",
            "recipe.charge_cell",
            "蓄電セル固化",
            "facility.symbiotic_assembly_vat",
            false,
            1.5f,
            RecipeOutputMode.Fixed,
            Inputs(chargeJelly, 1),
            Outputs(chargeCell, 1, 1));
        RecipeDefinition membrane = UpsertRecipe(
            "RegenMembrane",
            "recipe.regen_membrane",
            "再生膜培養",
            "facility.symbiotic_assembly_vat",
            true,
            2f,
            RecipeOutputMode.Fixed,
            Inputs(bioPacking, 1, heatSacHerb, 1),
            Outputs(regenMembrane, 1, 1));
        RecipeDefinition unstable = UpsertRecipe(
            "UnstableCulture",
            "recipe.unstable_culture",
            "不安定共生培養",
            "facility.symbiotic_assembly_vat",
            true,
            2f,
            RecipeOutputMode.WeightedSingle,
            Inputs(heatSacHerb, 1, chargeJelly, 1),
            Outputs(regenMembrane, 1, 50, bioPacking, 1, 30, heatSacCell, 1, 20));
        RecipeDefinition hull = UpsertRecipe(
            "HullRegenScale",
            "recipe.hull_regen_scale",
            "船殻再生鱗組立",
            "facility.symbiotic_assembly_vat",
            true,
            2.5f,
            RecipeOutputMode.Fixed,
            Inputs(radiatorPlate, 1, regenMembrane, 1, reinforceFrame, 1),
            Outputs(hullRegenScale, 1, 1));
        RecipeDefinition ember = UpsertRecipe(
            "StellarFurnaceEmber",
            "recipe.stellar_furnace_ember",
            "恒星炉の種火組立",
            "facility.symbiotic_assembly_vat",
            true,
            2.5f,
            RecipeOutputMode.Fixed,
            Inputs(heatSacCell, 1, chargeCell, 1, reinforceFrame, 1),
            Outputs(stellarEmber, 1, 1));

        ResourceNodeDefinition heatNode = UpsertNode(
            "HeatSacHerb",
            "resource_node.heat_sac_herb",
            "熱嚢草",
            "熱を孕んだ草。刈り取ってもすぐ再生する。",
            ResourceNodeKind.Plant,
            heatSacHerb,
            heatPrefab);
        ResourceNodeDefinition jellyNode = UpsertNode(
            "ChargeJellyfish",
            "resource_node.charge_jellyfish",
            "蓄電クラゲ",
            "浅瀬で脈打つクラゲ。触片はすぐ生える。",
            ResourceNodeKind.Biological,
            chargeJelly,
            jellyPrefab);

        FacilityDefinition workbench = Load<FacilityDefinition>(
            $"{FacilitiesFolder}/ScaleRollingWorkbench.asset");
        FacilityDefinition storage = Load<FacilityDefinition>(
            $"{FacilitiesFolder}/PrototypeStorage.asset");
        ConfigureFacility(
            workbench,
            "facility.scale_rolling_workbench",
            "鱗圧延作業台",
            workbenchPrefab,
            FacilityCapability.Processing,
            new[] { rolling, rivet, radiator, frame },
            Inputs(ironScale, 5),
            8,
            8,
            0,
            true,
            8,
            new[] { heatSacHerb, heatSacCell });
        FacilityDefinition vat = UpsertFacilityAsset("SymbioticAssemblyVat");
        ConfigureFacility(
            vat,
            "facility.symbiotic_assembly_vat",
            "共生組立槽",
            vatPrefab,
            FacilityCapability.Processing,
            new[] { packing, heatCell, chargeCellRecipe, membrane, unstable, hull, ember },
            Inputs(ironScalePlate, 3),
            8,
            8,
            0,
            true,
            8,
            new[] { heatSacCell });

        WirePrefabNode(heatPrefab, heatNode);
        WirePrefabNode(jellyPrefab, jellyNode);
        WirePrefabFacility(vatPrefab, vat, packing);

        WriteCatalogs(
            new[]
            {
                ironScale, ironScalePlate, heatSacHerb, chargeJelly, scaleRivet,
                radiatorPlate, reinforceFrame, bioPacking, heatSacCell, chargeCell,
                regenMembrane, hullRegenScale, stellarEmber
            },
            new[]
            {
                rolling, rivet, radiator, frame, packing, heatCell, chargeCellRecipe,
                membrane, unstable, hull, ember
            },
            new[] { workbench, storage, vat },
            new[]
            {
                Load<ResourceNodeDefinition>($"{NodesFolder}/IronScaleBeast.asset"),
                heatNode,
                jellyNode
            });

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == "Assets/Scenes/komayama_craft_scene.unity")
        {
            PlaceNodes(scene, heatPrefab, jellyPrefab, heatNode, jellyNode);
            WireBuildables(scene, workbench, storage, vat);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[KomayamaCraftM3] Tier 1 content and scene wiring are ready.");
    }

    private static void PlaceNodes(
        Scene scene,
        GameObject heatPrefab,
        GameObject jellyPrefab,
        ResourceNodeDefinition heatNode,
        ResourceNodeDefinition jellyNode)
    {
        Transform world = GameObject.Find("World")?.transform;
        KomayamaDropArea dropArea = Object.FindFirstObjectByType<KomayamaDropArea>();
        KomayamaCraftHud hud = Object.FindFirstObjectByType<KomayamaCraftHud>();
        KomayamaCraftSeManager se = Object.FindFirstObjectByType<KomayamaCraftSeManager>();
        EnsureNodeInstance(scene, world, heatPrefab, heatNode, new Vector3(-6.5f, -2.5f, 0f), dropArea, hud, se);
        EnsureNodeInstance(scene, world, jellyPrefab, jellyNode, new Vector3(6.5f, -2.5f, 0f), dropArea, hud, se);
    }

    private static void EnsureNodeInstance(
        Scene scene,
        Transform world,
        GameObject prefab,
        ResourceNodeDefinition definition,
        Vector3 position,
        KomayamaDropArea dropArea,
        KomayamaCraftHud hud,
        KomayamaCraftSeManager se)
    {
        KomayamaResourceNode[] existing = Object.FindObjectsByType<KomayamaResourceNode>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].Definition == definition)
            {
                return;
            }
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = definition.DisplayName;
        instance.transform.position = position;
        if (world != null)
        {
            instance.transform.SetParent(world, true);
        }

        if (instance.TryGetComponent(out KomayamaResourceNode node))
        {
            SerializedObject serialized = new(node);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("dropArea").objectReferenceValue = dropArea;
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.FindProperty("seManager").objectReferenceValue = se;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void WireBuildables(
        Scene scene,
        FacilityDefinition workbench,
        FacilityDefinition storage,
        FacilityDefinition vat)
    {
        KomayamaBuildController build = Object.FindFirstObjectByType<KomayamaBuildController>();
        if (build == null)
        {
            return;
        }

        SerializedObject serialized = new(build);
        SerializedProperty facilities = serialized.FindProperty("buildableFacilities");
        facilities.arraySize = 3;
        facilities.GetArrayElementAtIndex(0).objectReferenceValue = workbench;
        facilities.GetArrayElementAtIndex(1).objectReferenceValue = storage;
        facilities.GetArrayElementAtIndex(2).objectReferenceValue = vat;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _ = scene;
    }

    private static ItemDefinition UpsertItem(
        string fileName,
        string id,
        string displayName,
        string description,
        ItemCategory category)
    {
        string path = $"{ItemsFolder}/{fileName}.asset";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("maxStack").intValue = 8;
        SerializedProperty traits = serialized.FindProperty("traitIds");
        traits.arraySize = 1;
        traits.GetArrayElementAtIndex(0).stringValue = "prototype";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static RecipeDefinition UpsertRecipe(
        string fileName,
        string id,
        string displayName,
        string facilityId,
        bool usesFuel,
        float seconds,
        RecipeOutputMode mode,
        ItemAmount[] inputs,
        RecipeOutput[] outputs)
    {
        string path = $"{RecipesFolder}/{fileName}.asset";
        RecipeDefinition recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        SerializedObject serialized = new(recipe);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("requiredFacilityId").stringValue = facilityId;
        serialized.FindProperty("processingSeconds").floatValue = seconds;
        serialized.FindProperty("usesFuel").boolValue = usesFuel;
        serialized.FindProperty("outputMode").enumValueIndex = (int)mode;
        WriteItemAmounts(serialized.FindProperty("inputs"), inputs);
        WriteOutputs(serialized.FindProperty("outputs"), outputs);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static ResourceNodeDefinition UpsertNode(
        string fileName,
        string id,
        string displayName,
        string description,
        ResourceNodeKind kind,
        ItemDefinition yield,
        GameObject prefab)
    {
        string path = $"{NodesFolder}/{fileName}.asset";
        ResourceNodeDefinition node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            AssetDatabase.CreateAsset(node, path);
        }

        SerializedObject serialized = new(node);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.FindProperty("gatheringSeconds").floatValue = 0f;
        serialized.FindProperty("reharvestMode").enumValueIndex = (int)ResourceNodeReharvestMode.Immediate;
        serialized.FindProperty("reharvestSeconds").floatValue = 0f;
        WriteItemAmounts(serialized.FindProperty("yields"), Inputs(yield, 1));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
        return node;
    }

    private static FacilityDefinition UpsertFacilityAsset(string fileName)
    {
        string path = $"{FacilitiesFolder}/{fileName}.asset";
        FacilityDefinition facility = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
        if (facility == null)
        {
            facility = ScriptableObject.CreateInstance<FacilityDefinition>();
            AssetDatabase.CreateAsset(facility, path);
        }

        return facility;
    }

    private static void ConfigureFacility(
        FacilityDefinition facility,
        string id,
        string displayName,
        GameObject prefab,
        FacilityCapability capabilities,
        RecipeDefinition[] recipes,
        ItemAmount[] cost,
        int inputCapacity,
        int outputCapacity,
        int storageCapacity,
        bool usesFuel,
        int fuelCapacity,
        ItemDefinition[] fuels)
    {
        SerializedObject serialized = new(facility);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.FindProperty("capabilities").intValue = (int)capabilities;
        SerializedProperty recipeProp = serialized.FindProperty("supportedRecipes");
        recipeProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            recipeProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }

        WriteItemAmounts(serialized.FindProperty("constructionCost"), cost);
        serialized.FindProperty("inputCapacity").intValue = inputCapacity;
        serialized.FindProperty("outputCapacity").intValue = outputCapacity;
        serialized.FindProperty("storageCapacity").intValue = storageCapacity;
        serialized.FindProperty("usesFuel").boolValue = usesFuel;
        serialized.FindProperty("fuelCapacity").intValue = fuelCapacity;
        SerializedProperty fuelProp = serialized.FindProperty("acceptedFuelItems");
        fuelProp.arraySize = fuels.Length;
        for (int i = 0; i < fuels.Length; i++)
        {
            fuelProp.GetArrayElementAtIndex(i).objectReferenceValue = fuels[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(facility);
    }

    private static GameObject DuplicateTintedPrefab(
        GameObject source,
        string path,
        string objectName,
        Color color,
        float scale)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject contents = existing != null
            ? PrefabUtility.LoadPrefabContents(path)
            : PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        contents.name = objectName;
        contents.transform.localScale = Vector3.one * scale;
        if (contents.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer.color = color;
        }

        if (existing == null)
        {
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }

        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void WirePrefabNode(GameObject prefab, ResourceNodeDefinition definition)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        KomayamaResourceNode node = contents.GetComponent<KomayamaResourceNode>();
        if (node == null)
        {
            node = contents.AddComponent<KomayamaResourceNode>();
        }

        SerializedObject serialized = new(node);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static void WirePrefabFacility(
        GameObject prefab,
        FacilityDefinition definition,
        RecipeDefinition recipe)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        contents.layer = LayerMask.NameToLayer("Facility");
        KomayamaStorageFacility storage = contents.GetComponent<KomayamaStorageFacility>();
        if (storage != null)
        {
            Object.DestroyImmediate(storage, true);
        }

        KomayamaProcessingFacility processing = contents.GetComponent<KomayamaProcessingFacility>();
        if (processing == null)
        {
            processing = contents.AddComponent<KomayamaProcessingFacility>();
        }

        SerializedObject serialized = new(processing);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("recipe").objectReferenceValue = recipe;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static void WriteCatalogs(
        ItemDefinition[] items,
        RecipeDefinition[] recipes,
        FacilityDefinition[] facilities,
        ResourceNodeDefinition[] nodes)
    {
        ItemDefinitionCatalog itemCatalog =
            AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaItemCatalog.asset");
        RecipeDefinitionCatalog recipeCatalog =
            AssetDatabase.LoadAssetAtPath<RecipeDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaRecipeCatalog.asset");
        FacilityDefinitionCatalog facilityCatalog =
            AssetDatabase.LoadAssetAtPath<FacilityDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaFacilityCatalog.asset");
        ResourceNodeDefinitionCatalog nodeCatalog =
            AssetDatabase.LoadAssetAtPath<ResourceNodeDefinitionCatalog>(
                "Assets/Resources/GameData/KomayamaResourceNodeCatalog.asset");
        itemCatalog.SetItemsForEditor(items);
        recipeCatalog.SetRecipesForEditor(recipes);
        facilityCatalog.SetFacilitiesForEditor(facilities);
        nodeCatalog.SetResourceNodesForEditor(nodes);
        EditorUtility.SetDirty(itemCatalog);
        EditorUtility.SetDirty(recipeCatalog);
        EditorUtility.SetDirty(facilityCatalog);
        EditorUtility.SetDirty(nodeCatalog);
    }

    private static void WriteItemAmounts(SerializedProperty property, ItemAmount[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("item").objectReferenceValue = values[i].Item;
            element.FindPropertyRelative("amount").intValue = values[i].Amount;
        }
    }

    private static void WriteOutputs(SerializedProperty property, RecipeOutput[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("item").objectReferenceValue = values[i].Item;
            element.FindPropertyRelative("amount").intValue = values[i].Amount;
            element.FindPropertyRelative("weight").intValue = values[i].Weight;
        }
    }

    private static ItemAmount[] Inputs(params object[] pairs)
    {
        var list = new List<ItemAmount>();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            list.Add(new ItemAmount((ItemDefinition)pairs[i], (int)pairs[i + 1]));
        }

        return list.ToArray();
    }

    private static RecipeOutput[] Outputs(params object[] values)
    {
        var list = new List<RecipeOutput>();
        for (int i = 0; i + 2 < values.Length; i += 3)
        {
            list.Add(new RecipeOutput(
                (ItemDefinition)values[i],
                (int)values[i + 1],
                (int)values[i + 2]));
        }

        return list.ToArray();
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new System.InvalidOperationException($"Missing asset: {path}");
        }

        return asset;
    }

    private static void EnsureFolders()
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(RecipesFolder);
        EnsureFolder(FacilitiesFolder);
        EnsureFolder(NodesFolder);
        EnsureFolder(PrefabsFolder);
    }

    private static void EnsureFolder(string path)
    {
        string current = string.Empty;
        string[] parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            string next = string.IsNullOrEmpty(current) ? parts[i] : $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next) && i > 0)
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
