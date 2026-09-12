using System.Collections.Generic;
using KomayamaCraft;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KomayamaCraftM678ContentSetup
{
    private const string ItemsFolder = "Assets/GameData/KomayamaCraft/Items";
    private const string RecipesFolder = "Assets/GameData/KomayamaCraft/Recipes";
    private const string FacilitiesFolder = "Assets/GameData/KomayamaCraft/Facilities";
    private const string NodesFolder = "Assets/GameData/KomayamaCraft/ResourceNodes";
    private const string PrefabsFolder = "Assets/Prefabs/KomayamaCraft";

    [MenuItem("KomayamaCraft/M6からM8をシーンへ接続", false, 43)]
    public static void SetupFromMenu()
    {
        ItemDefinition ironScale = Load<ItemDefinition>($"{ItemsFolder}/IronScale.asset");
        ItemDefinition plate = Load<ItemDefinition>($"{ItemsFolder}/IronScalePlate.asset");
        ItemDefinition heatSac = Load<ItemDefinition>($"{ItemsFolder}/HeatSacHerb.asset");
        ItemDefinition jelly = Load<ItemDefinition>($"{ItemsFolder}/ChargeJelly.asset");
        ItemDefinition rivet = Load<ItemDefinition>($"{ItemsFolder}/ScaleRivet.asset");
        ItemDefinition radiator = Load<ItemDefinition>($"{ItemsFolder}/RadiatorScalePlate.asset");
        ItemDefinition frame = Load<ItemDefinition>($"{ItemsFolder}/ReinforceFrame.asset");
        ItemDefinition packing = Load<ItemDefinition>($"{ItemsFolder}/BioPacking.asset");
        ItemDefinition heatCell = Load<ItemDefinition>($"{ItemsFolder}/HeatSacCell.asset");
        ItemDefinition chargeCell = Load<ItemDefinition>($"{ItemsFolder}/ChargeCell.asset");
        ItemDefinition membrane = Load<ItemDefinition>($"{ItemsFolder}/RegenMembrane.asset");
        ItemDefinition hull = Load<ItemDefinition>($"{ItemsFolder}/HullRegenScale.asset");
        ItemDefinition ember = Load<ItemDefinition>($"{ItemsFolder}/StellarFurnaceEmber.asset");

        ItemDefinition horn = Item("MagneticHorn", "item.magnetic_horn", "磁角", ItemCategory.RawMaterial);
        ItemDefinition bundle = Item("ConductiveBundle", "item.conductive_bundle", "導電束", ItemCategory.IntermediateMaterial);
        ItemDefinition coil = Item("ResonanceCoil", "item.resonance_coil", "共鳴コイル", ItemCategory.IntermediateMaterial);
        ItemDefinition circuit = Item("StarCrestCircuit", "item.star_crest_circuit", "星紋回路", ItemCategory.IntermediateMaterial);
        ItemDefinition attitude = Item("AttitudeController", "item.attitude_controller", "姿勢制御子", ItemCategory.Component);
        ItemDefinition memory = Item("MemoryShard", "item.memory_shard", "記憶晶片", ItemCategory.Component);
        ItemDefinition starChart = Item("StarChartCore", "item.star_chart_core", "星図復元核", ItemCategory.RepairPart);
        ItemDefinition crystal = Item("StarCrestCrystal", "item.star_crest_crystal", "星紋晶", ItemCategory.RawMaterial);
        ItemDefinition stabilizer = Item("PhaseStabilizer", "item.phase_stabilizer", "位相安定芯", ItemCategory.Component);
        ItemDefinition shard = Item("PhaseShard", "item.phase_shard", "位相片", ItemCategory.RawMaterial);
        ItemDefinition lattice = Item("PhaseLattice", "item.phase_lattice", "位相格子", ItemCategory.IntermediateMaterial);
        ItemDefinition joint = Item("PhaseJoint", "item.phase_joint", "位相継手", ItemCategory.IntermediateMaterial);
        ItemDefinition ring = Item("InertiaRing", "item.inertia_ring", "慣性相殺環", ItemCategory.RepairPart);
        ItemDefinition silk = Item("OrbitSilk", "item.orbit_silk", "軌道絹", ItemCategory.RawMaterial);
        ItemDefinition compass = Item("InterstellarCompass", "item.interstellar_compass", "恒星間羅針", ItemCategory.RepairPart);

        GameObject beast = Load<GameObject>($"{PrefabsFolder}/IronScaleBeast.prefab");
        GameObject workbenchPrefab = Load<GameObject>($"{PrefabsFolder}/ScaleRollingWorkbench.prefab");
        GameObject hornPrefab = Tint(beast, $"{PrefabsFolder}/MagneticHornBeast.prefab", "MagneticHornBeast", new Color(0.55f, 0.4f, 1f), 1.35f);
        GameObject crystalPrefab = Tint(beast, $"{PrefabsFolder}/StarCrestCrystal.prefab", "StarCrestCrystal", new Color(0.95f, 0.85f, 0.35f), 1.1f);
        GameObject shardPrefab = Tint(beast, $"{PrefabsFolder}/PhaseShardNode.prefab", "PhaseShardNode", new Color(0.8f, 0.2f, 0.95f), 1.05f);
        GameObject cocoonPrefab = Tint(beast, $"{PrefabsFolder}/OrbitCocoon.prefab", "OrbitCocoon", new Color(0.65f, 0.95f, 0.9f), 1.2f);
        GameObject furnacePrefab = Tint(workbenchPrefab, $"{PrefabsFolder}/CrystalCultureFurnace.prefab", "CrystalCultureFurnace", new Color(0.45f, 0.75f, 1f), 1.3f);
        GameObject joinerPrefab = Tint(workbenchPrefab, $"{PrefabsFolder}/PhaseJoiner.prefab", "PhaseJoiner", new Color(0.75f, 0.35f, 0.95f), 1.3f);
        GameObject spinnerPrefab = Tint(workbenchPrefab, $"{PrefabsFolder}/VacuumSpinner.prefab", "VacuumSpinner", new Color(0.85f, 0.9f, 1f), 1.3f);

        RecipeDefinition bundleRecipe = Recipe("ConductiveBundle", "recipe.conductive_bundle", "導電束培養", "facility.crystal_culture_furnace", false, 1.5f, bundle, horn, 1);
        RecipeDefinition coilRecipe = Recipe("ResonanceCoil", "recipe.resonance_coil", "共鳴コイル培養", "facility.crystal_culture_furnace", true, 2f, coil, bundle, 1, chargeCell, 1);
        RecipeDefinition circuitRecipe = Recipe("StarCrestCircuit", "recipe.star_crest_circuit", "星紋回路培養", "facility.crystal_culture_furnace", true, 2f, circuit, bundle, 1, heatCell, 1);
        RecipeDefinition attitudeRecipe = Recipe("AttitudeController", "recipe.attitude_controller", "姿勢制御子組立", "facility.crystal_culture_furnace", true, 2f, attitude, coil, 1, rivet, 1);
        RecipeDefinition memoryRecipe = Recipe("MemoryShard", "recipe.memory_shard", "記憶晶片培養", "facility.crystal_culture_furnace", true, 2f, memory, circuit, 1, packing, 1);
        RecipeDefinition starChartRecipe = Recipe("StarChartCore", "recipe.star_chart_core", "星図復元核組立", "facility.crystal_culture_furnace", true, 2.5f, starChart, attitude, 1, memory, 1, frame, 1);
        RecipeDefinition stabilizerRecipe = Recipe("PhaseStabilizer", "recipe.phase_stabilizer", "位相安定芯", "facility.phase_joiner", false, 1.5f, stabilizer, crystal, 1, memory, 1);
        RecipeDefinition latticeRecipe = Recipe("PhaseLattice", "recipe.phase_lattice", "位相格子接合", "facility.phase_joiner", false, 1.5f, lattice, crystal, 1);
        RecipeDefinition jointRecipe = Recipe("PhaseJoint", "recipe.phase_joint", "位相継手接合", "facility.phase_joiner", true, 2f, joint, shard, 1, bundle, 1);
        RecipeDefinition ringRecipe = Recipe("InertiaRing", "recipe.inertia_ring", "慣性相殺環接合", "facility.phase_joiner", true, 2.5f, ring, lattice, 1, joint, 1);
        RecipeDefinition compassRecipe = Recipe("InterstellarCompass", "recipe.interstellar_compass", "恒星間羅針紡績", "facility.vacuum_spinner", true, 2.5f, compass, silk, 1, frame, 1);

        ResourceNodeDefinition hornNode = Node("MagneticHornBeast", "resource_node.magnetic_horn_beast", "磁角獣", ResourceNodeKind.Biological, horn, hornPrefab);
        ResourceNodeDefinition crystalNode = Node("StarCrestCrystal", "resource_node.star_crest_crystal", "星紋晶", ResourceNodeKind.NonBiological, crystal, crystalPrefab);
        ResourceNodeDefinition shardNode = Node("PhaseShard", "resource_node.phase_shard", "位相片", ResourceNodeKind.NonBiological, shard, shardPrefab);
        ResourceNodeDefinition cocoonNode = Node("OrbitCocoon", "resource_node.orbit_cocoon", "軌道繭", ResourceNodeKind.Biological, silk, cocoonPrefab);

        UnlockDefinition t2 = Load<UnlockDefinition>("Assets/GameData/KomayamaCraft/Unlocks/Tier2ShipRepair.asset");
        UnlockDefinition t3 = DeliverUnlock("Tier3StarChart", "unlock.tier3_star_chart", "星図復元完了", starChart, 3, "tier3");
        UnlockDefinition t4 = DeliverUnlock("Tier4InertiaRing", "unlock.tier4_inertia_ring", "離陸準備完了", ring, 4, "tier4");

        FacilityDefinition workbench = Load<FacilityDefinition>($"{FacilitiesFolder}/ScaleRollingWorkbench.asset");
        FacilityDefinition storage = Load<FacilityDefinition>($"{FacilitiesFolder}/PrototypeStorage.asset");
        FacilityDefinition vat = Load<FacilityDefinition>($"{FacilitiesFolder}/SymbioticAssemblyVat.asset");
        FacilityDefinition ghost = Load<FacilityDefinition>($"{FacilitiesFolder}/CourierGhost.asset");
        FacilityDefinition furnace = UpsertFacility("CrystalCultureFurnace");
        ConfigureFacility(furnace, "facility.crystal_culture_furnace", "結晶培養炉", furnacePrefab,
            new[] { bundleRecipe, coilRecipe, circuitRecipe, attitudeRecipe, memoryRecipe, starChartRecipe },
            plate, 3, heatCell, t2.DefinitionId);
        FacilityDefinition joiner = UpsertFacility("PhaseJoiner");
        ConfigureFacility(joiner, "facility.phase_joiner", "位相接合機", joinerPrefab,
            new[] { stabilizerRecipe, latticeRecipe, jointRecipe, ringRecipe },
            circuit, 2, heatCell, t3.DefinitionId);
        FacilityDefinition spinner = UpsertFacility("VacuumSpinner");
        ConfigureFacility(spinner, "facility.vacuum_spinner", "真空紡績機", spinnerPrefab,
            new[] { compassRecipe }, joint, 2, heatCell, t4.DefinitionId);

        WireNode(hornPrefab, hornNode);
        WireNode(crystalPrefab, crystalNode);
        WireNode(shardPrefab, shardNode);
        WireNode(cocoonPrefab, cocoonNode);
        WireFacility(furnacePrefab, furnace, bundleRecipe);
        WireFacility(joinerPrefab, joiner, stabilizerRecipe);
        WireFacility(spinnerPrefab, spinner, compassRecipe);

        MergeCatalogs(
            new[]
            {
                ironScale, plate, heatSac, jelly, rivet, radiator, frame, packing, heatCell, chargeCell,
                membrane, hull, ember, horn, bundle, coil, circuit, attitude, memory, starChart,
                crystal, stabilizer, shard, lattice, joint, ring, silk, compass
            },
            CollectRecipes(bundleRecipe, coilRecipe, circuitRecipe, attitudeRecipe, memoryRecipe, starChartRecipe,
                stabilizerRecipe, latticeRecipe, jointRecipe, ringRecipe, compassRecipe),
            new[] { workbench, storage, vat, ghost, furnace, joiner, spinner },
            new[]
            {
                Load<ResourceNodeDefinition>($"{NodesFolder}/IronScaleBeast.asset"),
                Load<ResourceNodeDefinition>($"{NodesFolder}/HeatSacHerb.asset"),
                Load<ResourceNodeDefinition>($"{NodesFolder}/ChargeJellyfish.asset"),
                hornNode, crystalNode, shardNode, cocoonNode
            },
            new[]
            {
                Load<UnlockDefinition>("Assets/GameData/KomayamaCraft/Unlocks/PrototypeUnconditional.asset"),
                t2, t3, t4
            });

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == "Assets/Scenes/komayama_craft_scene.unity")
        {
            WireScene(scene, hornPrefab, crystalPrefab, shardPrefab, cocoonPrefab,
                hornNode, crystalNode, shardNode, cocoonNode,
                workbench, storage, vat, ghost, furnace, joiner, spinner,
                starChart, ring, compass, stabilizer);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[KomayamaCraftM678] Tier 2-4 content is ready.");
    }

    private static void WireScene(
        Scene scene,
        GameObject hornPrefab,
        GameObject crystalPrefab,
        GameObject shardPrefab,
        GameObject cocoonPrefab,
        ResourceNodeDefinition hornNode,
        ResourceNodeDefinition crystalNode,
        ResourceNodeDefinition shardNode,
        ResourceNodeDefinition cocoonNode,
        FacilityDefinition workbench,
        FacilityDefinition storage,
        FacilityDefinition vat,
        FacilityDefinition ghost,
        FacilityDefinition furnace,
        FacilityDefinition joiner,
        FacilityDefinition spinner,
        ItemDefinition starChart,
        ItemDefinition ring,
        ItemDefinition compass,
        ItemDefinition stabilizer)
    {
        Transform world = GameObject.Find("World")?.transform;
        KomayamaDropArea drop = Object.FindFirstObjectByType<KomayamaDropArea>();
        KomayamaCraftHud hud = Object.FindFirstObjectByType<KomayamaCraftHud>();
        KomayamaCraftSeManager se = Object.FindFirstObjectByType<KomayamaCraftSeManager>();
        KomayamaProgressService progress = Object.FindFirstObjectByType<KomayamaProgressService>();
        PlaceNode(scene, world, hornPrefab, hornNode, new Vector3(7.2f, 3.2f), drop, hud, se);
        PlaceNode(scene, world, crystalPrefab, crystalNode, new Vector3(0f, -3.2f), drop, hud, se);
        PlaceNode(scene, world, shardPrefab, shardNode, new Vector3(9.2f, -4.4f), drop, hud, se);
        PlaceNode(scene, world, cocoonPrefab, cocoonNode, new Vector3(-7f, 0.8f), drop, hud, se);
        EnsureRegion(scene, world, progress, "Tier3Region", "tier3", new Vector3(0f, -3.2f), 2.4f, null);
        EnsureRegion(scene, world, progress, "PhaseHazard", string.Empty, new Vector3(9.2f, -4.4f), 2.2f, stabilizer);
        EnsureRegion(scene, world, progress, "Tier4Region", "tier4", new Vector3(-7f, 0.8f), 2.2f, null);

        KomayamaShip ship = Object.FindFirstObjectByType<KomayamaShip>();
        if (ship != null)
        {
            SerializedObject shipSerialized = new(ship);
            shipSerialized.FindProperty("starChartSlotItem").objectReferenceValue = starChart;
            shipSerialized.FindProperty("inertiaRingSlotItem").objectReferenceValue = ring;
            shipSerialized.FindProperty("compassSlotItem").objectReferenceValue = compass;
            shipSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        KomayamaBuildController build = Object.FindFirstObjectByType<KomayamaBuildController>();
        SerializedObject buildSerialized = new(build);
        SerializedProperty list = buildSerialized.FindProperty("buildableFacilities");
        list.arraySize = 7;
        FacilityDefinition[] buildables = { workbench, storage, vat, ghost, furnace, joiner, spinner };
        for (int i = 0; i < buildables.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = buildables[i];
        }

        buildSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject systems = GameObject.Find("KomayamaCraftSystems");
        KomayamaEscapeController escape = systems.GetComponent<KomayamaEscapeController>() ??
            systems.AddComponent<KomayamaEscapeController>();
        KomayamaTutorialController tutorial = systems.GetComponent<KomayamaTutorialController>() ??
            systems.AddComponent<KomayamaTutorialController>();
        GameObject ending = EnsureEndingOverlay(scene);

        SerializedObject escapeSerialized = new(escape);
        escapeSerialized.FindProperty("ship").objectReferenceValue = ship;
        escapeSerialized.FindProperty("progress").objectReferenceValue = progress;
        escapeSerialized.FindProperty("hud").objectReferenceValue = hud;
        escapeSerialized.FindProperty("endingRoot").objectReferenceValue = ending;
        escapeSerialized.FindProperty("endingText").objectReferenceValue =
            ending.GetComponentInChildren<TMP_Text>(true);
        escapeSerialized.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject tutorialSerialized = new(tutorial);
        tutorialSerialized.FindProperty("hud").objectReferenceValue = hud;
        tutorialSerialized.FindProperty("hand").objectReferenceValue =
            Object.FindFirstObjectByType<KomayamaHandInventory>();
        tutorialSerialized.ApplyModifiedPropertiesWithoutUndo();
        KomayamaSaveService save = Object.FindFirstObjectByType<KomayamaSaveService>();
        if (save != null)
        {
            SerializedObject saveSerialized = new(save);
            saveSerialized.FindProperty("loadOnStart").boolValue = true;
            saveSerialized.FindProperty("autosaveOnQuit").boolValue = true;
            saveSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static GameObject EnsureEndingOverlay(Scene scene)
    {
        GameObject ending = FindNamed(scene, "EndingOverlay");
        if (ending == null)
        {
            ending = new GameObject("EndingOverlay");
            SceneManager.MoveGameObjectToScene(ending, scene);
        }

        Canvas canvas = ending.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = ending.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        if (ending.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = ending.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (ending.GetComponent<GraphicRaycaster>() == null)
        {
            ending.AddComponent<GraphicRaycaster>();
        }

        Transform brokenPanel = ending.transform.Find("Panel");
        if (brokenPanel != null && brokenPanel.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(brokenPanel.gameObject);
        }

        Transform panelTransform = ending.transform.Find("Panel");
        GameObject panel = panelTransform != null ? panelTransform.gameObject : new GameObject("Panel", typeof(RectTransform));
        if (panelTransform == null)
        {
            panel.transform.SetParent(ending.transform, false);
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = panel.AddComponent<Image>();
        }

        panelImage.color = new Color(0.02f, 0.04f, 0.08f, 0.82f);

        Transform labelTransform = panel.transform.Find("EndingText");
        GameObject label = labelTransform != null ? labelTransform.gameObject : new GameObject("EndingText", typeof(RectTransform));
        if (labelTransform == null)
        {
            label.transform.SetParent(panel.transform, false);
        }

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.1f, 0.35f);
        labelRect.anchorMax = new Vector2(0.9f, 0.65f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = label.AddComponent<TextMeshProUGUI>();
        }
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 42f;
        text.text = "惑星圏を離脱した。Enterでフィールドへ戻る。";
        ending.SetActive(false);
        return ending;
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null &&
                transforms[i].gameObject.scene == scene &&
                transforms[i].gameObject.name == objectName)
            {
                return transforms[i].gameObject;
            }
        }

        return null;
    }

    private static void PlaceNode(
        Scene scene,
        Transform world,
        GameObject prefab,
        ResourceNodeDefinition definition,
        Vector3 position,
        KomayamaDropArea drop,
        KomayamaCraftHud hud,
        KomayamaCraftSeManager se)
    {
        KomayamaResourceNode[] existing = Object.FindObjectsByType<KomayamaResourceNode>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].Definition == definition)
            {
                existing[i].transform.position = position;
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

        SerializedObject serialized = new(instance.GetComponent<KomayamaResourceNode>());
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("dropArea").objectReferenceValue = drop;
        serialized.FindProperty("hud").objectReferenceValue = hud;
        serialized.FindProperty("seManager").objectReferenceValue = se;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureRegion(
        Scene scene,
        Transform world,
        KomayamaProgressService progress,
        string objectName,
        string regionId,
        Vector3 position,
        float size,
        ItemDefinition requiredCrafted)
    {
        GameObject regionObject = GameObject.Find(objectName);
        if (regionObject == null)
        {
            regionObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(regionObject, scene);
            if (world != null)
            {
                regionObject.transform.SetParent(world, false);
            }

            regionObject.transform.position = position;
            SpriteRenderer marker = regionObject.AddComponent<SpriteRenderer>();
            marker.color = new Color(1f, 0.25f, 0.25f, 0.22f);
            marker.sortingOrder = 4;
            BoxCollider2D box = regionObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(size, size);
            regionObject.AddComponent<KomayamaRegion>();
        }

        regionObject.transform.position = position;
        if (regionObject.TryGetComponent(out BoxCollider2D existingBox))
        {
            existingBox.size = new Vector2(size, size);
        }

        SerializedObject serialized = new(regionObject.GetComponent<KomayamaRegion>());
        serialized.FindProperty("regionId").stringValue = regionId;
        serialized.FindProperty("progress").objectReferenceValue = progress;
        serialized.FindProperty("marker").objectReferenceValue = regionObject.GetComponent<SpriteRenderer>();
        serialized.FindProperty("requiredCraftedItem").objectReferenceValue = requiredCrafted;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemDefinition Item(string file, string id, string name, ItemCategory category)
    {
        string path = $"{ItemsFolder}/{file}.asset";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("maxStack").intValue = 8;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static RecipeDefinition Recipe(
        string file,
        string id,
        string name,
        string facilityId,
        bool usesFuel,
        float seconds,
        ItemDefinition output,
        params object[] inputs)
    {
        string path = $"{RecipesFolder}/{file}.asset";
        RecipeDefinition recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        SerializedObject serialized = new(recipe);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("requiredFacilityId").stringValue = facilityId;
        serialized.FindProperty("usesFuel").boolValue = usesFuel;
        serialized.FindProperty("processingSeconds").floatValue = seconds;
        serialized.FindProperty("outputMode").enumValueIndex = 0;
        SerializedProperty inputProp = serialized.FindProperty("inputs");
        inputProp.arraySize = inputs.Length / 2;
        for (int i = 0; i + 1 < inputs.Length; i += 2)
        {
            SerializedProperty element = inputProp.GetArrayElementAtIndex(i / 2);
            element.FindPropertyRelative("item").objectReferenceValue = (ItemDefinition)inputs[i];
            element.FindPropertyRelative("amount").intValue = (int)inputs[i + 1];
        }

        SerializedProperty outputs = serialized.FindProperty("outputs");
        outputs.arraySize = 1;
        outputs.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = output;
        outputs.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
        outputs.GetArrayElementAtIndex(0).FindPropertyRelative("weight").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static ResourceNodeDefinition Node(
        string file,
        string id,
        string name,
        ResourceNodeKind kind,
        ItemDefinition yield,
        GameObject prefab)
    {
        string path = $"{NodesFolder}/{file}.asset";
        ResourceNodeDefinition node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            AssetDatabase.CreateAsset(node, path);
        }

        SerializedObject serialized = new(node);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.FindProperty("gatheringSeconds").floatValue = 0f;
        serialized.FindProperty("reharvestMode").enumValueIndex = (int)ResourceNodeReharvestMode.Immediate;
        serialized.FindProperty("reharvestSeconds").floatValue = 0f;
        SerializedProperty yields = serialized.FindProperty("yields");
        yields.arraySize = 1;
        yields.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = yield;
        yields.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
        return node;
    }

    private static FacilityDefinition UpsertFacility(string file)
    {
        string path = $"{FacilitiesFolder}/{file}.asset";
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
        string name,
        GameObject prefab,
        RecipeDefinition[] recipes,
        ItemDefinition costItem,
        int costAmount,
        ItemDefinition fuel,
        string unlockId)
    {
        SerializedObject serialized = new(facility);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.FindProperty("capabilities").intValue = (int)FacilityCapability.Processing;
        SerializedProperty recipeProp = serialized.FindProperty("supportedRecipes");
        recipeProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            recipeProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }

        SerializedProperty cost = serialized.FindProperty("constructionCost");
        cost.arraySize = 1;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = costItem;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = costAmount;
        serialized.FindProperty("inputCapacity").intValue = 8;
        serialized.FindProperty("outputCapacity").intValue = 8;
        serialized.FindProperty("usesFuel").boolValue = true;
        serialized.FindProperty("fuelCapacity").intValue = 8;
        SerializedProperty fuels = serialized.FindProperty("acceptedFuelItems");
        fuels.arraySize = 1;
        fuels.GetArrayElementAtIndex(0).objectReferenceValue = fuel;
        serialized.FindProperty("requiredUnlockId").stringValue = unlockId ?? string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(facility);
    }

    private static UnlockDefinition DeliverUnlock(
        string file,
        string id,
        string name,
        ItemDefinition item,
        int tier,
        string regionId)
    {
        string path = $"Assets/GameData/KomayamaCraft/Unlocks/{file}.asset";
        UnlockDefinition unlock = AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);
        if (unlock == null)
        {
            unlock = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(unlock, path);
        }

        SerializedObject serialized = new(unlock);
        serialized.FindProperty("definitionId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = name;
        SerializedProperty groups = serialized.FindProperty("anyOfConditionGroups");
        groups.arraySize = 1;
        SerializedProperty conditions = groups.GetArrayElementAtIndex(0).FindPropertyRelative("allConditions");
        conditions.arraySize = 1;
        conditions.GetArrayElementAtIndex(0).FindPropertyRelative("conditionType").enumValueIndex =
            (int)UnlockConditionType.ItemDelivered;
        conditions.GetArrayElementAtIndex(0).FindPropertyRelative("deliveredItem").objectReferenceValue = item;
        conditions.GetArrayElementAtIndex(0).FindPropertyRelative("deliveredAmount").intValue = 1;
        SerializedProperty targets = serialized.FindProperty("targets");
        targets.arraySize = 2;
        targets.GetArrayElementAtIndex(0).FindPropertyRelative("targetType").enumValueIndex = (int)UnlockTargetType.Tier;
        targets.GetArrayElementAtIndex(0).FindPropertyRelative("tier").intValue = tier;
        targets.GetArrayElementAtIndex(1).FindPropertyRelative("targetType").enumValueIndex = (int)UnlockTargetType.Region;
        targets.GetArrayElementAtIndex(1).FindPropertyRelative("identifier").stringValue = regionId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(unlock);
        return unlock;
    }

    private static GameObject Tint(GameObject source, string path, string name, Color color, float scale)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject contents = PrefabUtility.LoadPrefabContents(
            existing != null ? path : AssetDatabase.GetAssetPath(source));
        contents.name = name;
        contents.transform.localScale = Vector3.one * scale;
        if (contents.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer.color = color;
        }

        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void WireNode(GameObject prefab, ResourceNodeDefinition definition)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        KomayamaResourceNode node = contents.GetComponent<KomayamaResourceNode>() ??
            contents.AddComponent<KomayamaResourceNode>();
        SerializedObject serialized = new(node);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static void WireFacility(GameObject prefab, FacilityDefinition definition, RecipeDefinition recipe)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        contents.layer = LayerMask.NameToLayer("Facility");
        KomayamaStorageFacility storage = contents.GetComponent<KomayamaStorageFacility>();
        if (storage != null)
        {
            Object.DestroyImmediate(storage, true);
        }

        KomayamaProcessingFacility processing = contents.GetComponent<KomayamaProcessingFacility>() ??
            contents.AddComponent<KomayamaProcessingFacility>();
        SerializedObject serialized = new(processing);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("recipe").objectReferenceValue = recipe;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static RecipeDefinition[] CollectRecipes(params RecipeDefinition[] extra)
    {
        RecipeDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<RecipeDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaRecipeCatalog.asset");
        var list = new List<RecipeDefinition>();
        for (int i = 0; i < catalog.Recipes.Count; i++)
        {
            if (catalog.Recipes[i] != null && !list.Contains(catalog.Recipes[i]))
            {
                list.Add(catalog.Recipes[i]);
            }
        }

        for (int i = 0; i < extra.Length; i++)
        {
            if (!list.Contains(extra[i]))
            {
                list.Add(extra[i]);
            }
        }

        return list.ToArray();
    }

    private static void MergeCatalogs(
        ItemDefinition[] items,
        RecipeDefinition[] recipes,
        FacilityDefinition[] facilities,
        ResourceNodeDefinition[] nodes,
        UnlockDefinition[] unlocks)
    {
        AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaItemCatalog.asset").SetItemsForEditor(items);
        AssetDatabase.LoadAssetAtPath<RecipeDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaRecipeCatalog.asset").SetRecipesForEditor(recipes);
        AssetDatabase.LoadAssetAtPath<FacilityDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaFacilityCatalog.asset").SetFacilitiesForEditor(facilities);
        AssetDatabase.LoadAssetAtPath<ResourceNodeDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaResourceNodeCatalog.asset").SetResourceNodesForEditor(nodes);
        AssetDatabase.LoadAssetAtPath<UnlockDefinitionCatalog>(
            "Assets/Resources/GameData/KomayamaUnlockCatalog.asset").SetUnlocksForEditor(unlocks);
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new System.InvalidOperationException("Missing " + path);
        }

        return asset;
    }
}
