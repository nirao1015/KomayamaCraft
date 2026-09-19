using System;
using System.Collections.Generic;
using System.IO;
using KomayamaCraft;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class KomayamaCraftM1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/komayama_craft_scene.unity";
    private const string ScriptRoot = "Assets/Scripts/komayama_craft_scene";
    private const string PrefabRoot = "Assets/Prefabs/KomayamaCraft";

    [MenuItem("KomayamaCraft/ゲームプレイシーンを再生成", false, 20)]
    [MenuItem("Tools/KomayamaCraft/M1/Rebuild Gameplay Scene", false, 20)]
    public static void RebuildGameplayScene()
    {
        EnsureFolder(ScriptRoot);
        MoveM1Assets();

        ItemDefinition ironScale = Load<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/IronScale.asset");
        ItemDefinition ironScalePlate = Load<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/IronScalePlate.asset");
        ResourceNodeDefinition ironScaleBeastDefinition =
            Load<ResourceNodeDefinition>(
                "Assets/GameData/KomayamaCraft/ResourceNodes/IronScaleBeast.asset");
        FacilityDefinition workbenchDefinition = Load<FacilityDefinition>(
            "Assets/GameData/KomayamaCraft/Facilities/ScaleRollingWorkbench.asset");
        RecipeDefinition rollingRecipe = Load<RecipeDefinition>(
            "Assets/GameData/KomayamaCraft/Recipes/IronScaleRolling.asset");

        Sprite groundSprite = Load<Sprite>(
            "Assets/Sprites/_Images/tile_grass.png");
        Sprite resourceSprite = Load<Sprite>(
            "Assets/Sprites/_Clean Vector Icons/T_24_diamond_.png");
        Sprite droppedSprite = Load<Sprite>(
            "Assets/Sprites/_Images/tile_gem.png");
        Sprite facilitySprite = Load<Sprite>(
            "Assets/Sprites/_Clean Vector Icons/T_1_anvil_.png");
        Sprite outputSprite = Load<Sprite>(
            "Assets/Sprites/_Images/tile_cog.png");

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isDirty)
        {
            throw new InvalidOperationException(
                "Save or discard the active scene changes before rebuilding M1.");
        }

        KomayamaNoDropPaintUtility.NoDropPaintCapture noDropPaint =
            KomayamaNoDropPaintUtility.CapturePaintFromLoadedScenes();
        KomayamaNoDropPaintUtility.EnsurePaintAssets();

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        KomayamaDroppedItem droppedPrefab =
            BuildDroppedItemPrefab(droppedSprite);
        KomayamaResourceNode resourcePrefab =
            BuildResourceNodePrefab(ironScaleBeastDefinition, resourceSprite);
        KomayamaProcessingFacility facilityPrefab =
            BuildFacilityPrefab(workbenchDefinition, rollingRecipe, facilitySprite);

        // Saving over prefab assets can invalidate cached Unity object handles.
        ironScale = Load<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/IronScale.asset");
        ironScalePlate = Load<ItemDefinition>(
            "Assets/GameData/KomayamaCraft/Items/IronScalePlate.asset");
        ironScaleBeastDefinition = Load<ResourceNodeDefinition>(
            "Assets/GameData/KomayamaCraft/ResourceNodes/IronScaleBeast.asset");
        workbenchDefinition = Load<FacilityDefinition>(
            "Assets/GameData/KomayamaCraft/Facilities/ScaleRollingWorkbench.asset");
        rollingRecipe = Load<RecipeDefinition>(
            "Assets/GameData/KomayamaCraft/Recipes/IronScaleRolling.asset");
        groundSprite = Load<Sprite>("Assets/Sprites/_Images/tile_grass.png");
        resourceSprite = Load<Sprite>(
            "Assets/Sprites/_Clean Vector Icons/T_24_diamond_.png");
        droppedSprite = Load<Sprite>("Assets/Sprites/_Images/tile_gem.png");
        facilitySprite = Load<Sprite>(
            "Assets/Sprites/_Clean Vector Icons/T_1_anvil_.png");
        outputSprite = Load<Sprite>("Assets/Sprites/_Images/tile_cog.png");
        droppedPrefab = Load<GameObject>(
            $"{PrefabRoot}/DroppedIronScale.prefab")
            .GetComponent<KomayamaDroppedItem>();
        resourcePrefab = Load<GameObject>(
            $"{PrefabRoot}/IronScaleBeast.prefab")
            .GetComponent<KomayamaResourceNode>();
        facilityPrefab = Load<GameObject>(
            $"{PrefabRoot}/ScaleRollingWorkbench.prefab")
            .GetComponent<KomayamaProcessingFacility>();

        SetObject(ironScale, "icon", droppedSprite);
        SetObject(ironScalePlate, "icon", outputSprite);
        SetObject(ironScaleBeastDefinition, "icon", resourceSprite);
        SetObject(ironScaleBeastDefinition, "prefab", resourcePrefab.gameObject);
        SetObject(workbenchDefinition, "icon", facilitySprite);
        SetObject(workbenchDefinition, "prefab", facilityPrefab.gameObject);

        BuildScene(
            scene,
            groundSprite,
            noDropPaint,
            droppedPrefab,
            resourcePrefab,
            facilityPrefab,
            ironScaleBeastDefinition,
            workbenchDefinition,
            rollingRecipe);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new InvalidOperationException($"Could not save {ScenePath}.");
        }

        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"[KomayamaCraftM1] Rebuilt {ScenePath}");
    }

    private static void BuildScene(
        Scene scene,
        Sprite groundSprite,
        KomayamaNoDropPaintUtility.NoDropPaintCapture noDropPaint,
        KomayamaDroppedItem droppedPrefab,
        KomayamaResourceNode resourcePrefab,
        KomayamaProcessingFacility facilityPrefab,
        ResourceNodeDefinition resourceDefinition,
        FacilityDefinition facilityDefinition,
        RecipeDefinition recipe)
    {
        GameObject world = new("World");
        SceneManager.MoveGameObjectToScene(world, scene);

        GameObject lightObject = new("Global Light 2D");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        Light2D globalLight = lightObject.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 0.8f;

        GameObject ground = new("FieldBackground");
        ground.layer = LayerMask.NameToLayer("WorldStatic");
        ground.transform.SetParent(world.transform);
        SpriteRenderer groundRenderer = ground.AddComponent<SpriteRenderer>();
        groundRenderer.sprite = groundSprite;
        groundRenderer.color = new Color(0.38f, 0.58f, 0.5f, 1f);
        groundRenderer.sortingOrder = -100;
        groundRenderer.drawMode = SpriteDrawMode.Sliced;
        groundRenderer.size = new Vector2(16f, 8f);

        GameObject dropAreaObject = new("FieldDropArea");
        dropAreaObject.layer = LayerMask.NameToLayer("DropArea");
        dropAreaObject.transform.SetParent(world.transform);
        BoxCollider2D dropCollider = dropAreaObject.AddComponent<BoxCollider2D>();
        dropCollider.isTrigger = true;
        dropCollider.size = new Vector2(16f, 8f);
        KomayamaDropArea dropArea = dropAreaObject.AddComponent<KomayamaDropArea>();
        SetObject(dropArea, "dropAreaCollider", dropCollider);
        SetObject(dropArea, "droppedItemPrefab", droppedPrefab);
        SetObject(dropArea, "droppedItemRoot", dropAreaObject.transform);
        SetLayerMask(
            dropArea,
            "blockingLayers",
            LayerMask.GetMask(
                KomayamaNoDropPaintUtility.LayerName,
                "Facility",
                "ResourceNode",
                "NativeLife"));
        SetFloat(dropArea, "sourceRandomMinRadius", 1.5f);
        SetFloat(dropArea, "sourceRandomMaxRadius", 2.4f);
        SetInt(dropArea, "outerCandidateAttempts", 512);
        SetFloat(dropArea, "dropAnimationSeconds", 0.35f);
        SetFloat(dropArea, "dropAnimationArcHeight", 0.9f);
        SetFloat(dropArea, "pushAnimationSeconds", 0.15f);

        Tilemap paintMap = KomayamaNoDropPaintUtility.EnsurePaintGrid(
            scene,
            world.transform);
        KomayamaNoDropPaintUtility.RestorePaint(paintMap, noDropPaint);
        SetObject(dropArea, "noDropPaint", paintMap);

        Camera camera = BuildCamera(scene);
        EventSystem eventSystem = BuildEventSystem(scene);
        AudioServices audio = BuildAudioServices(scene);

        GameObject resourceObject =
            (GameObject)PrefabUtility.InstantiatePrefab(
                resourcePrefab.gameObject,
                scene);
        KomayamaResourceNode resourceNode =
            resourceObject.GetComponent<KomayamaResourceNode>();
        resourceObject.name = "IronScaleBeast";
        resourceObject.transform.position = new Vector3(-3.2f, 0f, 0f);

        GameObject facilityObject =
            (GameObject)PrefabUtility.InstantiatePrefab(
                facilityPrefab.gameObject,
                scene);
        KomayamaProcessingFacility facility =
            facilityObject.GetComponent<KomayamaProcessingFacility>();
        facilityObject.name = "ScaleRollingWorkbench";
        facilityObject.transform.position = new Vector3(3.2f, 0f, 0f);

        GameObject systems = new("KomayamaCraftSystems");
        SceneManager.MoveGameObjectToScene(systems, scene);
        KomayamaHandInventory hand = systems.AddComponent<KomayamaHandInventory>();
        KomayamaCraftInputController input =
            systems.AddComponent<KomayamaCraftInputController>();

        HudObjects hudObjects = BuildHud(scene, camera);
        KomayamaCraftHud hud = hudObjects.hud;

        SetObject(resourceNode, "definition", resourceDefinition);
        SetObject(resourceNode, "dropArea", dropArea);
        SetObject(resourceNode, "hud", hud);
        SetObject(resourceNode, "seManager", audio.seManager);

        SetObject(facility, "definition", facilityDefinition);
        SetObject(facility, "recipe", recipe);
        SetObject(facility, "hud", hud);
        SetObject(facility, "seManager", audio.seManager);

        SetObject(input, "targetCamera", camera);
        SetObject(input, "eventSystem", eventSystem);
        SetObject(input, "dropArea", dropArea);
        SetObject(input, "hand", hand);
        SetObject(input, "hud", hud);
        SetObject(input, "seManager", audio.seManager);
        SetLayerMask(
            input,
            "interactableLayers",
            LayerMask.GetMask("ResourceNode", "NativeLife", "DroppedItem", "Facility"));
        SetFloat(input, "gatherHoldIntervalSeconds", 0.5f);

        SetObject(hud, "hand", hand);
        SetObject(hud, "resourceNode", resourceNode);
        SetObject(hud, "facility", facility);

        GameObject debugManagerObject = new("KomayamaCraftDebugManager");
        SceneManager.MoveGameObjectToScene(debugManagerObject, scene);
        KomayamaCraftDebugManager debugManager =
            debugManagerObject.AddComponent<KomayamaCraftDebugManager>();
        SetObject(debugManager, "debugOverlay", hudObjects.debugOverlay);
        SetObject(debugManager, "gameplayCamera", camera);
    }

    private static Camera BuildCamera(Scene scene)
    {
        GameObject cameraObject = new("Main Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.12f, 1f);
        int dropBlockerLayer = LayerMask.NameToLayer(
            KomayamaNoDropPaintUtility.LayerName);
        if (dropBlockerLayer >= 0)
        {
            camera.cullingMask &= ~(1 << dropBlockerLayer);
        }
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        FixedAspectCameraFitter fitter =
            cameraObject.AddComponent<FixedAspectCameraFitter>();
        SetObject(fitter, "targetCamera", camera);
        KomayamaCraftCameraController cameraController =
            cameraObject.AddComponent<KomayamaCraftCameraController>();
        SetObject(cameraController, "targetCamera", camera);
        return camera;
    }

    private static EventSystem BuildEventSystem(Scene scene)
    {
        GameObject eventSystemObject = new("EventSystem");
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
        return eventSystem;
    }

    private static AudioServices BuildAudioServices(Scene scene)
    {
        GameObject settingsObject = new("SoundSettingsManager");
        SceneManager.MoveGameObjectToScene(settingsObject, scene);
        settingsObject.AddComponent<SoundSettingsManager>();

        GameObject bgmObject = new("KomayamaCraftBgmManager");
        SceneManager.MoveGameObjectToScene(bgmObject, scene);
        AudioSource bgmSource = bgmObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        KomayamaCraftBgmManager bgm =
            bgmObject.AddComponent<KomayamaCraftBgmManager>();
        SetObject(bgm, "playbackSource", bgmSource);
        SetObject(
            bgm,
            "gameplayClip",
            Load<AudioClip>("Assets/BGM/BGM_-_102_-_Universe.mp3"));

        GameObject seObject = new("KomayamaCraftSeManager");
        SceneManager.MoveGameObjectToScene(seObject, scene);
        AudioSource seSource = seObject.AddComponent<AudioSource>();
        seSource.playOnAwake = false;
        KomayamaCraftSeManager se =
            seObject.AddComponent<KomayamaCraftSeManager>();
        SetObject(se, "playbackSource", seSource);

        AudioClip positive = Load<AudioClip>(
            "Assets/SE/game02/決定ボタンを押す51.mp3");
        AudioClip complete = Load<AudioClip>(
            "Assets/SE/game02/シャキーン2.mp3");
        AudioClip invalid = Load<AudioClip>(
            "Assets/SE/game02/maou_se_system18.mp3");
        SetSeEntries(se, positive, complete, invalid);

        return new AudioServices(bgm, se);
    }

    private static HudObjects BuildHud(Scene scene, Camera camera)
    {
        GameObject canvasObject = new(
            "HudCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        FixedAspectCanvasFitter fitter =
            canvasObject.AddComponent<FixedAspectCanvasFitter>();
        SetObject(fitter, "targetCanvas", canvas);
        SetObject(fitter, "targetCamera", camera);

        KomayamaCraftHud hud = canvasObject.AddComponent<KomayamaCraftHud>();
        TextMeshProUGUI guide = CreateText(
            canvasObject.transform,
            "GuideText",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 34f),
            new Vector2(1200f, 52f),
            26f,
            TextAlignmentOptions.Center);
        TextMeshProUGUI hand = CreateText(
            canvasObject.transform,
            "HandText",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(28f, -28f),
            new Vector2(520f, 64f),
            32f,
            TextAlignmentOptions.TopLeft);
        TextMeshProUGUI state = CreateText(
            canvasObject.transform,
            "StateText",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-28f, -28f),
            new Vector2(560f, 110f),
            28f,
            TextAlignmentOptions.TopRight);
        TextMeshProUGUI message = CreateText(
            canvasObject.transform,
            "MessageText",
            new Vector2(0.5f, 0.72f),
            new Vector2(0.5f, 0.72f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(960f, 80f),
            34f,
            TextAlignmentOptions.Center);
        message.gameObject.SetActive(false);

        TextMeshProUGUI cursorHand = CreateText(
            canvasObject.transform,
            "CursorHand",
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(22f, -22f),
            new Vector2(360f, 50f),
            24f,
            TextAlignmentOptions.TopLeft);
        cursorHand.text = "手持ち";
        cursorHand.gameObject.SetActive(false);

        GameObject debugOverlay = new(
            "DebugOverlay",
            typeof(RectTransform));
        debugOverlay.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI debugText = debugOverlay.AddComponent<TextMeshProUGUI>();
        debugText.text = "M1 DEBUG\nDropArea: 16 × 8\n重なり半径: 0.28";
        debugText.fontSize = 20f;
        debugText.color = new Color(1f, 1f, 1f, 0.65f);
        debugText.alignment = TextAlignmentOptions.BottomRight;
        debugText.raycastTarget = false;
        RectTransform debugRect = debugOverlay.GetComponent<RectTransform>();
        debugRect.anchorMin = Vector2.one;
        debugRect.anchorMax = Vector2.one;
        debugRect.pivot = Vector2.one;
        debugRect.anchoredPosition = new Vector2(-24f, -20f);
        debugRect.sizeDelta = new Vector2(400f, 100f);

        SetObject(hud, "handText", hand);
        SetObject(hud, "guideText", guide);
        SetObject(hud, "stateText", state);
        SetObject(hud, "messageText", message);
        SetObject(hud, "cursorHandRoot", cursorHand.rectTransform);
        return new HudObjects(hud, debugOverlay);
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static KomayamaDroppedItem BuildDroppedItemPrefab(Sprite sprite)
    {
        string path = $"{PrefabRoot}/DroppedIronScale.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null &&
            existing.TryGetComponent(out KomayamaDroppedItem existingItem))
        {
            return existingItem;
        }

        GameObject temporary = new("DroppedIronScale");
        temporary.layer = LayerMask.NameToLayer("DroppedItem");
        SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 30;
        temporary.transform.localScale = Vector3.one * 0.75f;
        CircleCollider2D collider = temporary.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        temporary.AddComponent<KomayamaDroppedItem>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        UnityEngine.Object.DestroyImmediate(temporary);
        return prefab.GetComponent<KomayamaDroppedItem>();
    }

    private static KomayamaResourceNode BuildResourceNodePrefab(
        ResourceNodeDefinition definition,
        Sprite sprite)
    {
        string path = $"{PrefabRoot}/IronScaleBeast.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null &&
            existing.TryGetComponent(out KomayamaResourceNode existingNode))
        {
            SetObject(existingNode, "definition", definition);
            return existingNode;
        }

        GameObject temporary = new("IronScaleBeast");
        temporary.layer = LayerMask.NameToLayer("NativeLife");
        SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.78f, 0.9f, 1f, 1f);
        renderer.sortingOrder = 20;
        temporary.transform.localScale = Vector3.one * 1.5f;
        CircleCollider2D collider = temporary.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        KomayamaResourceNode node = temporary.AddComponent<KomayamaResourceNode>();
        SetObject(node, "definition", definition);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        UnityEngine.Object.DestroyImmediate(temporary);
        return prefab.GetComponent<KomayamaResourceNode>();
    }

    private static KomayamaProcessingFacility BuildFacilityPrefab(
        FacilityDefinition definition,
        RecipeDefinition recipe,
        Sprite sprite)
    {
        string path = $"{PrefabRoot}/ScaleRollingWorkbench.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null &&
            existing.TryGetComponent(
                out KomayamaProcessingFacility existingFacility))
        {
            SetObject(existingFacility, "definition", definition);
            SetObject(existingFacility, "recipe", recipe);
            return existingFacility;
        }

        GameObject temporary = new("ScaleRollingWorkbench");
        temporary.layer = LayerMask.NameToLayer("Facility");
        SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 0.78f, 0.42f, 1f);
        renderer.sortingOrder = 20;
        temporary.transform.localScale = Vector3.one * 1.45f;
        BoxCollider2D collider = temporary.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        KomayamaProcessingFacility facility =
            temporary.AddComponent<KomayamaProcessingFacility>();
        SetObject(facility, "definition", definition);
        SetObject(facility, "recipe", recipe);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        UnityEngine.Object.DestroyImmediate(temporary);
        return prefab.GetComponent<KomayamaProcessingFacility>();
    }

    private static void SetSeEntries(
        KomayamaCraftSeManager manager,
        AudioClip positive,
        AudioClip complete,
        AudioClip invalid)
    {
        SerializedObject serialized = new(manager);
        SerializedProperty entries = serialized.FindProperty("entries");
        entries.arraySize = Enum.GetValues(typeof(KomayamaCraftSeCue)).Length;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("cue").enumValueIndex = i;
            entry.FindPropertyRelative("clip").objectReferenceValue =
                i == (int)KomayamaCraftSeCue.Invalid
                    ? invalid
                    : i == (int)KomayamaCraftSeCue.ProcessingComplete
                        ? complete
                        : positive;
            entry.FindPropertyRelative("volume").floatValue = 0.8f;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MoveM1Assets()
    {
        MoveIfNeeded(
            "Assets/GameData/KomayamaCraft/Items/PrototypeRawMaterial.asset",
            "Assets/GameData/KomayamaCraft/Items/IronScale.asset");
        MoveIfNeeded(
            "Assets/GameData/KomayamaCraft/Items/PrototypeProcessedPart.asset",
            "Assets/GameData/KomayamaCraft/Items/IronScalePlate.asset");
        MoveIfNeeded(
            "Assets/GameData/KomayamaCraft/Recipes/PrototypeProcessing.asset",
            "Assets/GameData/KomayamaCraft/Recipes/IronScaleRolling.asset");
        MoveIfNeeded(
            "Assets/GameData/KomayamaCraft/Facilities/PrototypeProcessor.asset",
            "Assets/GameData/KomayamaCraft/Facilities/ScaleRollingWorkbench.asset");
        MoveIfNeeded(
            "Assets/GameData/KomayamaCraft/ResourceNodes/PrototypeResourceNode.asset",
            "Assets/GameData/KomayamaCraft/ResourceNodes/IronScaleBeast.asset");
        MoveIfNeeded(
            $"{PrefabRoot}/PrototypeResourceNode.prefab",
            $"{PrefabRoot}/IronScaleBeast.prefab");
        MoveIfNeeded(
            $"{PrefabRoot}/PrototypeProcessor.prefab",
            $"{PrefabRoot}/ScaleRollingWorkbench.prefab");
    }

    private static void MoveIfNeeded(string source, string destination)
    {
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null ||
            AssetDatabase.LoadMainAssetAtPath(source) == null)
        {
            return;
        }

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void AddSceneToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        if (scenes.Exists(scene => scene.path == ScenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        string current = string.Empty;
        string[] parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            string next = string.IsNullOrEmpty(current)
                ? parts[i]
                : $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next) && i > 0)
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException($"Required asset not found: {path}");
        }
        return asset;
    }

    private static void SetObject(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        UnityEngine.Object target,
        string propertyName,
        float value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(
        UnityEngine.Object target,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(
        UnityEngine.Object target,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private readonly struct AudioServices
    {
        public AudioServices(
            KomayamaCraftBgmManager bgmManager,
            KomayamaCraftSeManager seManager)
        {
            this.bgmManager = bgmManager;
            this.seManager = seManager;
        }

        public readonly KomayamaCraftBgmManager bgmManager;
        public readonly KomayamaCraftSeManager seManager;
    }

    private readonly struct HudObjects
    {
        public HudObjects(KomayamaCraftHud hud, GameObject debugOverlay)
        {
            this.hud = hud;
            this.debugOverlay = debugOverlay;
        }

        public readonly KomayamaCraftHud hud;
        public readonly GameObject debugOverlay;
    }
}
