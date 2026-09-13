using System.Collections.Generic;
using System.IO;
using KomayamaCraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class KomayamaNoDropPaintUtility
{
    public const string PaintObjectName = "NoDropPaint";
    public const string GridObjectName = "DropRestrictionGrid";
    public const string LayerName = "DropBlocker";
    public const string OverlaySortingLayerName = "WorldOverlay";
    public const float CellSize = 0.5f;

    private const string TilePath =
        "Assets/GameData/KomayamaCraft/NoDropPaintTile.asset";
    private const string SpritePath =
        "Assets/GameData/KomayamaCraft/NoDropPaintCell.png";
    private const string PalettePath =
        "Assets/TilePalettes/NoDropPaint.prefab";

    [MenuItem("KomayamaCraft/ドロップ禁止を塗る準備", false, 10)]
    [MenuItem("Tools/KomayamaCraft/M1/Ensure No-Drop Paint Grid", false, 10)]
    public static void EnsurePaintGridFromMenu()
    {
        EnsurePaintAssets();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new System.InvalidOperationException(
                "Open komayama_craft_scene before creating the paint grid.");
        }

        EnsurePaintGrid(scene, null);
        WireDropArea(scene);
        HidePaintFromGameplayCameras(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            "[KomayamaCraftM1] No-drop paint grid is ready. " +
            "KomayamaCraft > ドロップ禁止を塗る を実行し、" +
            "Scene View の地面をクリックして塗ってください。");
    }

    public static Tile EnsurePaintAssets()
    {
        EnsureFolder("Assets/GameData/KomayamaCraft");
        EnsureFolder("Assets/TilePalettes");
        Sprite sprite = GetOrCreatePaintSprite();
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }

        tile.name = "NoDropPaintTile";
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        EnsurePalette(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }

    public static NoDropPaintCapture CapturePaintFromLoadedScenes()
    {
        var positions = new List<Vector3Int>();
        var tiles = new List<TileBase>();
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null || maps[i].gameObject.name != PaintObjectName)
            {
                continue;
            }

            foreach (Vector3Int cell in maps[i].cellBounds.allPositionsWithin)
            {
                TileBase painted = maps[i].GetTile(cell);
                if (painted == null)
                {
                    continue;
                }

                positions.Add(cell);
                tiles.Add(painted);
            }
        }

        return new NoDropPaintCapture(positions.ToArray(), tiles.ToArray());
    }

    public static Tilemap EnsurePaintGrid(Scene scene, Transform parent)
    {
        _ = parent;
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
        {
            throw new System.InvalidOperationException(
                $"Layer '{LayerName}' is missing.");
        }

        Transform world = FindWorldRoot(scene);
        if (world == null)
        {
            throw new System.InvalidOperationException(
                "World is missing. Open komayama_craft_scene first.");
        }

        world.name = "World";
        Transform layers = FindWorldLayers(world);
        if (layers != null)
        {
            layers.name = "WorldLayers";
        }

        Transform grid = FindCanonicalGrid(world);
        if (grid == null)
        {
            GameObject gridObject = new(GridObjectName);
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            gridObject.transform.SetParent(world, false);
            gridObject.transform.position = Vector3.zero;
            gridObject.AddComponent<Grid>();
            grid = gridObject.transform;
        }
        else if (grid.parent != world)
        {
            grid.SetParent(world, true);
        }

        grid.gameObject.name = GridObjectName;
        grid.position = Vector3.zero;
        if (grid.TryGetComponent(out Grid gridComponent))
        {
            gridComponent.cellSize = new Vector3(CellSize, CellSize, 1f);
        }

        Tilemap paint = FindPaintTilemap(scene);
        GameObject paintObject;
        if (paint != null)
        {
            paintObject = paint.gameObject;
            if (paintObject.transform.parent != grid)
            {
                paintObject.transform.SetParent(grid, true);
            }
        }
        else
        {
            paintObject = new GameObject(PaintObjectName);
            paintObject.transform.SetParent(grid, false);
            paintObject.AddComponent<Tilemap>();
            paintObject.AddComponent<TilemapRenderer>();
            paintObject.AddComponent<Rigidbody2D>();
            paintObject.AddComponent<CompositeCollider2D>();
            paintObject.AddComponent<TilemapCollider2D>();
        }

        paintObject.transform.localPosition = Vector3.zero;
        paintObject.transform.localRotation = Quaternion.identity;
        paintObject.transform.localScale = Vector3.one;
        ConfigurePaintObject(paintObject, layer);
        return paintObject.GetComponent<Tilemap>();
    }

    public static void RestorePaint(Tilemap tilemap, NoDropPaintCapture capture)
    {
        if (tilemap == null ||
            capture.positions == null ||
            capture.positions.Length == 0)
        {
            return;
        }

        tilemap.SetTiles(capture.positions, capture.tiles);
        tilemap.CompressBounds();
    }

    public static void WireDropArea(Scene scene)
    {
        Tilemap paint = FindPaintTilemap(scene);
        KomayamaDropArea[] areas = Object.FindObjectsByType<KomayamaDropArea>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i] == null)
            {
                continue;
            }

            SerializedObject serialized = new(areas[i]);
            SerializedProperty noDrop = serialized.FindProperty("noDropPaint");
            if (noDrop != null)
            {
                noDrop.objectReferenceValue = paint;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static void HidePaintFromGameplayCameras(Scene scene)
    {
        int layer = LayerMask.NameToLayer(LayerName);
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

    private static void ConfigurePaintObject(GameObject paintObject, int layer)
    {
        paintObject.layer = layer;
        paintObject.name = PaintObjectName;
        Transform gridTransform = paintObject.transform.parent;
        if (gridTransform != null &&
            gridTransform.TryGetComponent(out Grid grid) &&
            gridTransform.GetComponent<KomayamaCraft.KomayamaResourceNode>() == null &&
            gridTransform.Find("Layer_Sea") == null &&
            gridTransform.Find("CrashedShip") == null &&
            gridTransform.Find("FieldDropArea") == null)
        {
            gridTransform.gameObject.name = GridObjectName;
            grid.cellSize = new Vector3(CellSize, CellSize, 1f);
        }

        if (paintObject.TryGetComponent(out Tilemap tilemap))
        {
            tilemap.color = new Color(1f, 0.12f, 0.12f, 0.45f);
        }

        if (paintObject.TryGetComponent(out TilemapRenderer renderer))
        {
            EnsureOverlaySortingLayer();
            renderer.sortingLayerName = OverlaySortingLayerName;
            renderer.sortingOrder = 100;
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (unlit != null)
            {
                renderer.sharedMaterial = unlit;
            }
        }

        if (paintObject.TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
        }

        if (paintObject.TryGetComponent(out CompositeCollider2D composite))
        {
            composite.isTrigger = true;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        if (paintObject.TryGetComponent(out TilemapCollider2D tilemapCollider))
        {
            tilemapCollider.compositeOperation =
                Collider2D.CompositeOperation.Merge;
        }
    }

    private static Tilemap FindPaintTilemap(Scene scene)
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null &&
                maps[i].gameObject.scene == scene &&
                maps[i].gameObject.name == PaintObjectName)
            {
                return maps[i];
            }
        }

        return null;
    }

    private static Transform FindWorldRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == "World")
            {
                return roots[i].transform;
            }
        }

        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                all[i].gameObject.scene == scene &&
                (all[i].name == "CrashedShip" || all[i].name == "FieldDropArea") &&
                all[i].parent != null)
            {
                return all[i].parent;
            }
        }

        return null;
    }

    private static Transform FindWorldLayers(Transform world)
    {
        Transform named = world.Find("WorldLayers");
        if (named != null)
        {
            return named;
        }

        for (int i = 0; i < world.childCount; i++)
        {
            Transform child = world.GetChild(i);
            if (child.Find("Layer_Sea") != null || child.Find("Layer_Continent") != null)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindCanonicalGrid(Transform world)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Transform fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null ||
                candidate.gameObject.scene != world.gameObject.scene ||
                !candidate.TryGetComponent(out Grid _) ||
                candidate.Find("Layer_Sea") != null ||
                candidate.Find("CrashedShip") != null ||
                candidate.Find("FieldDropArea") != null)
            {
                continue;
            }

            if (candidate.Find(PaintObjectName) != null)
            {
                return candidate;
            }

            if (candidate.name == GridObjectName && fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private static void EnsureOverlaySortingLayer()
    {
        SerializedObject tagManager = new(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue ==
                OverlaySortingLayerName)
            {
                return;
            }
        }

        layers.arraySize++;
        SerializedProperty added = layers.GetArrayElementAtIndex(layers.arraySize - 1);
        added.FindPropertyRelative("name").stringValue = OverlaySortingLayerName;
        added.FindPropertyRelative("uniqueID").intValue = 1005;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite GetOrCreatePaintSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null)
        {
            return existing;
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 0.18f, 0.18f, 1f);
        }

        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SpritePath);

        TextureImporter importer =
            AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void EnsurePalette(Tile tile)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
        if (existing != null)
        {
            return;
        }

        GameObject paletteRoot = new("NoDropPaint");
        Grid grid = paletteRoot.AddComponent<Grid>();
        grid.cellSize = new Vector3(CellSize, CellSize, 1f);
        GameObject layer = new("Layer1");
        layer.transform.SetParent(paletteRoot.transform, false);
        Tilemap tilemap = layer.AddComponent<Tilemap>();
        layer.AddComponent<TilemapRenderer>();
        tilemap.SetTile(Vector3Int.zero, tile);
        PrefabUtility.SaveAsPrefabAsset(paletteRoot, PalettePath);
        Object.DestroyImmediate(paletteRoot);
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

    public readonly struct NoDropPaintCapture
    {
        public NoDropPaintCapture(Vector3Int[] positions, TileBase[] tiles)
        {
            this.positions = positions;
            this.tiles = tiles;
        }

        public readonly Vector3Int[] positions;
        public readonly TileBase[] tiles;
    }
}
