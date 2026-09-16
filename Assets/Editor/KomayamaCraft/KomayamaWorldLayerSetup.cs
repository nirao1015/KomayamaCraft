using System.IO;
using KomayamaCraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KomayamaWorldLayerSetup
{
    private const string PlaceholderPath = "Assets/GameData/KomayamaCraft/World/LayerPlaceholder.png";
    private const int ScreenColumns = 6;
    private const int ScreenRows = 8;
    private const int SeaExtraScreens = 1;

    [MenuItem("KomayamaCraft/ワールドレイヤーをシーンへ用意", false, 50)]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/komayama_craft_scene.unity")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/komayama_craft_scene.unity", OpenSceneMode.Single);
        }

        EnsureSortingLayer("WorldSea", 1001);
        EnsureSortingLayer("WorldContinent", 1002);
        EnsureSortingLayer("WorldObject", 1003);
        EnsureSortingLayer("WorldEffect", 1004);
        EnsureSortingLayer("WorldOverlay", 1005);
        EnsureSortingLayer("WorldMouse", 1006);
        EnsureSortingLayer("WorldSystem", 1007);

        Sprite placeholder = EnsurePlaceholderSprite();
        Camera camera = Camera.main;
        float ortho = camera != null ? camera.orthographicSize : 5.4f;
        float aspect = camera != null ? camera.aspect : 16f / 9f;
        float screenHeight = ortho * 2f;
        float screenWidth = screenHeight * aspect;
        Vector2 continentSize = new(screenWidth * ScreenColumns, screenHeight * ScreenRows);
        Vector2 seaSize = new(
            screenWidth * (ScreenColumns + SeaExtraScreens * 2),
            screenHeight * (ScreenRows + SeaExtraScreens * 2));

        KomayamaShip ship = Object.FindFirstObjectByType<KomayamaShip>();
        Vector3 center = ship != null ? ship.transform.position : Vector3.zero;
        center.z = 0f;

        Transform world = GameObject.Find("World") != null ? GameObject.Find("World").transform : null;
        GameObject root = GameObject.Find("WorldLayers");
        if (root == null)
        {
            root = new GameObject("WorldLayers");
            SceneManager.MoveGameObjectToScene(root, scene);
            if (world != null)
            {
                root.transform.SetParent(world, false);
            }
        }

        root.transform.position = center;
        Transform seaRoot = EnsureContainer(root.transform, "Layer_Sea");
        Transform continentRoot = EnsureContainer(root.transform, "Layer_Continent");
        RemoveOwnSprite(seaRoot);
        RemoveOwnSprite(continentRoot);
            int seaMinX = -SeaExtraScreens;
        int seaMaxX = ScreenColumns - 1 + SeaExtraScreens;
        int seaMinY = -SeaExtraScreens;
        int seaMaxY = ScreenRows - 1 + SeaExtraScreens;
        EnsureRegionGrid(
            seaRoot,
            placeholder,
            new Vector2(screenWidth, screenHeight),
            seaMinX,
            seaMaxX,
            seaMinY,
            seaMaxY,
            "WorldSea",
            Color.white,
            Color.white);
        RemoveOutOfRangeRegionCells(seaRoot, seaMinX, seaMaxX, seaMinY, seaMaxY);
        EnsureRegionGrid(
            continentRoot,
            placeholder,
            new Vector2(screenWidth, screenHeight),
            0,
            ScreenColumns - 1,
            0,
            ScreenRows - 1,
            "WorldContinent",
            Color.white,
            Color.white);
        RemoveOutOfRangeRegionCells(
            continentRoot,
            0,
            ScreenColumns - 1,
            0,
            ScreenRows - 1);
        EnsureContainer(root.transform, "Layer_Objects");
        EnsureContainer(root.transform, "Layer_Effects");
        GameObject guide = EnsureLayer(root.transform, "ContinentGuide", placeholder, continentSize, "WorldContinent", 10, new Color(1f, 0.92f, 0.2f, 0.12f));
        guide.SetActive(false);

        Transform crash = root.transform.Find("CrashSiteCenter");
        if (crash == null)
        {
            GameObject marker = new("CrashSiteCenter");
            marker.transform.SetParent(root.transform, false);
            crash = marker.transform;
        }

        crash.localPosition = Vector3.zero;

        ExpandCameraLimits(center, continentSize, new Vector2(screenWidth, screenHeight));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "[KomayamaWorldLayers] 1x screen=" + screenWidth + "x" + screenHeight +
            " continent=" + continentSize.x + "x" + continentSize.y +
            " sea=" + seaSize.x + "x" + seaSize.y +
            " center=" + center);
    }

    private static Material LoadUnlitSpriteMaterial()
    {
        return AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
    }

    private static GameObject EnsureLayer(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 size,
        string sortingLayer,
        int order,
        Color color)
    {
        Transform existing = parent.Find(name);
        GameObject layer = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            layer.transform.SetParent(parent, false);
        }

        layer.transform.localPosition = Vector3.zero;
        layer.transform.localScale = Vector3.one;
        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = layer.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.sharedMaterial = LoadUnlitSpriteMaterial();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        return layer;
    }

    private static Transform EnsureContainer(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            existing.localPosition = Vector3.zero;
            existing.localScale = Vector3.one;
            return existing;
        }

        GameObject container = new(name);
        container.transform.SetParent(parent, false);
        container.transform.localPosition = Vector3.zero;
        return container.transform;
    }

    private static void RemoveOwnSprite(Transform target)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Object.DestroyImmediate(renderer);
        }
    }

    private static void EnsureRegionGrid(
        Transform parent,
        Sprite placeholder,
        Vector2 cellSize,
        int minX,
        int maxX,
        int minY,
        int maxY,
        string sortingLayer,
        Color colorA,
        Color colorB)
    {
        float originX = (ScreenColumns - 1) * 0.5f;
        float originY = (ScreenRows - 1) * 0.5f;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                string cellName = $"Region_X{x:00}_Y{y:00}";
                if (x < 0 || y < 0)
                {
                    cellName = $"Region_X{x}_Y{y}";
                }

                bool checker = ((x + y) & 1) == 0;
                Vector2 local = new((x - originX) * cellSize.x, (y - originY) * cellSize.y);
                EnsureRegionCell(
                    parent,
                    cellName,
                    placeholder,
                    cellSize,
                    local,
                    sortingLayer,
                    checker ? colorA : colorB);
            }
        }
    }

    private static void RemoveOutOfRangeRegionCells(
        Transform parent,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            var match = System.Text.RegularExpressions.Regex.Match(
                child.name,
                @"Region_X(-?\d+)_Y(-?\d+)");
            if (!match.Success)
            {
                continue;
            }

            int x = int.Parse(match.Groups[1].Value);
            int y = int.Parse(match.Groups[2].Value);
            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureRegionCell(
        Transform parent,
        string name,
        Sprite placeholder,
        Vector2 size,
        Vector2 localPosition,
        string sortingLayer,
        Color color)
    {
        Transform existing = parent.Find(name);
        GameObject cell = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            cell.transform.SetParent(parent, false);
        }

        cell.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        cell.transform.localScale = Vector3.one;
        SpriteRenderer renderer = cell.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = cell.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = placeholder;
        }

        renderer.color = Color.white;
        renderer.sharedMaterial = LoadUnlitSpriteMaterial();
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = 0;
        KomayamaCraft.KCWorldRegionCell cellSettings = cell.GetComponent<KomayamaCraft.KCWorldRegionCell>();
        if (cellSettings == null)
        {
            cellSettings = cell.AddComponent<KomayamaCraft.KCWorldRegionCell>();
        }

        cellSettings.SetCellSize(size);
    }

    private static void ExpandCameraLimits(Vector3 center, Vector2 continentSize, Vector2 screenSize)
    {
        KomayamaCraftCameraController camera = Object.FindFirstObjectByType<KomayamaCraftCameraController>();
        if (camera == null)
        {
            return;
        }

        Vector2 travel = new(
            Mathf.Max(0f, continentSize.x * 0.5f - screenSize.x * 0.5f),
            Mathf.Max(0f, continentSize.y * 0.5f - screenSize.y * 0.5f));
        SerializedObject serialized = new(camera);
        serialized.FindProperty("minimumPosition").vector2Value =
            new Vector2(center.x - travel.x, center.y - travel.y);
        serialized.FindProperty("maximumPosition").vector2Value =
            new Vector2(center.x + travel.x, center.y + travel.y);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite EnsurePlaceholderSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
        if (existing != null)
        {
            return existing;
        }

        string folder = Path.GetDirectoryName(PlaceholderPath);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(PlaceholderPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(PlaceholderPath);
        TextureImporter importer = AssetImporter.GetAtPath(PlaceholderPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 8f;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
    }

    private static void EnsureSortingLayer(string layerName, int uniqueId)
    {
        SerializedObject tagManager = new(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName)
            {
                return;
            }
        }

        layers.arraySize++;
        SerializedProperty added = layers.GetArrayElementAtIndex(layers.arraySize - 1);
        added.FindPropertyRelative("name").stringValue = layerName;
        added.FindPropertyRelative("uniqueID").intValue = uniqueId;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
