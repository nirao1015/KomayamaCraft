using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class KomayamaNoDropPaintSceneTool
{
    private const string MenuPath = "KomayamaCraft/ドロップ禁止を塗る";
    private const string TilePath =
        "Assets/GameData/KomayamaCraft/NoDropPaintTile.asset";
    private const int MinBrushSize = 1;
    private const int MaxBrushSize = 11;

    private static bool enabled;
    private static int brushSize = 7;
    private static bool rectangleDrag;
    private static bool rectangleErase;
    private static Vector3Int rectangleStart;
    private static Tool previousTool;
    private static bool previousToolsHidden;

    [MenuItem(MenuPath, false, 11)]
    public static void TogglePaintMode()
    {
        if (enabled)
        {
            SetEnabled(false);
            return;
        }

        KomayamaNoDropPaintUtility.EnsurePaintAssets();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new System.InvalidOperationException(
                "Open komayama_craft_scene before painting no-drop cells.");
        }

        KomayamaNoDropPaintUtility.EnsurePaintGrid(scene, null);
        KomayamaNoDropPaintUtility.WireDropArea(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = null;
        brushSize = 7;
        SetEnabled(true);
        Debug.Log(
            "[KomayamaCraftM1] ドロップ禁止塗りを開始しました。" +
            "先に Shift+ドラッグで広範囲を塗り、あとから 1 で1マス調整してください。");
    }

    [MenuItem(MenuPath, true)]
    public static bool TogglePaintModeValidate()
    {
        Menu.SetChecked(MenuPath, enabled);
        return true;
    }

    private static void SetEnabled(bool value)
    {
        if (enabled == value)
        {
            return;
        }

        enabled = value;
        rectangleDrag = false;
        if (enabled)
        {
            previousTool = Tools.current;
            previousToolsHidden = Tools.hidden;
            Tools.current = Tool.View;
            Tools.hidden = true;
            SceneView.duringSceneGui += OnSceneGui;
        }
        else
        {
            SceneView.duringSceneGui -= OnSceneGui;
            Tools.hidden = previousToolsHidden;
            Tools.current = previousTool;
            Debug.Log("[KomayamaCraftM1] ドロップ禁止塗りを終了しました。");
        }

        SceneView.RepaintAll();
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (!enabled)
        {
            return;
        }

        Tilemap tilemap = FindPaintTilemap();
        Event current = Event.current;
        HandleShortcuts(current);
        if (!enabled)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        Tools.current = Tool.View;
        Tools.hidden = true;
        DrawHud();
        if (tilemap == null)
        {
            return;
        }

        if (!TryGetCell(tilemap, current.mousePosition, out Vector3Int cell))
        {
            return;
        }

        if (rectangleDrag)
        {
            DrawRectPreview(tilemap, rectangleStart, cell);
        }
        else
        {
            DrawBrushPreview(tilemap, cell);
        }

        HandlePaintInput(tilemap, cell, current);
    }

    private static void HandleShortcuts(Event current)
    {
        if (current.type != EventType.KeyDown)
        {
            return;
        }

        if (current.keyCode == KeyCode.Escape)
        {
            SetEnabled(false);
            current.Use();
            return;
        }

        if (current.keyCode == KeyCode.Alpha1 || current.keyCode == KeyCode.Keypad1)
        {
            brushSize = 1;
            current.Use();
        }
        else if (current.keyCode == KeyCode.Alpha2 || current.keyCode == KeyCode.Keypad2)
        {
            brushSize = 5;
            current.Use();
        }
        else if (current.keyCode == KeyCode.Alpha3 || current.keyCode == KeyCode.Keypad3)
        {
            brushSize = 9;
            current.Use();
        }
        else if (current.keyCode == KeyCode.LeftBracket)
        {
            brushSize = Mathf.Max(MinBrushSize, brushSize - 2);
            current.Use();
        }
        else if (current.keyCode == KeyCode.RightBracket)
        {
            brushSize = Mathf.Min(MaxBrushSize, brushSize + 2);
            current.Use();
        }
    }

    private static void HandlePaintInput(
        Tilemap tilemap,
        Vector3Int cell,
        Event current)
    {
        bool paint = current.button == 0;
        bool erase = current.button == 1;
        if (!paint && !erase && current.type != EventType.MouseUp)
        {
            return;
        }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (current.type == EventType.MouseDown && current.shift && (paint || erase))
        {
            rectangleDrag = true;
            rectangleErase = erase;
            rectangleStart = cell;
            current.Use();
            return;
        }

        if (rectangleDrag)
        {
            if (current.type == EventType.MouseUp)
            {
                Undo.RecordObject(tilemap, rectangleErase
                    ? "Erase no-drop rectangle"
                    : "Paint no-drop rectangle");
                PaintRect(tilemap, rectangleStart, cell, tile, !rectangleErase);
                FinishStroke(tilemap);
                rectangleDrag = false;
                current.Use();
            }

            return;
        }

        bool pressed = current.type == EventType.MouseDown ||
            current.type == EventType.MouseDrag;
        if (!pressed || (!paint && !erase))
        {
            return;
        }

        if (current.type == EventType.MouseDown)
        {
            Undo.IncrementCurrentGroup();
        }

        Undo.RecordObject(tilemap, paint ? "Paint no-drop cells" : "Erase no-drop cells");
        PaintBrush(tilemap, cell, tile, paint);
        FinishStroke(tilemap);
        current.Use();
    }

    private static void PaintBrush(
        Tilemap tilemap,
        Vector3Int center,
        Tile tile,
        bool paint)
    {
        int extent = brushSize / 2;
        for (int y = -extent; y <= extent; y++)
        {
            for (int x = -extent; x <= extent; x++)
            {
                Vector3Int target = center + new Vector3Int(x, y, 0);
                tilemap.SetTile(target, paint ? tile : null);
            }
        }
    }

    private static void PaintRect(
        Tilemap tilemap,
        Vector3Int start,
        Vector3Int end,
        Tile tile,
        bool paint)
    {
        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), paint ? tile : null);
            }
        }
    }

    private static void FinishStroke(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
        EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
    }

    private static void DrawHud()
    {
        Handles.BeginGUI();
        GUI.Box(new Rect(12f, 12f, 520f, 72f), GUIContent.none);
        GUI.Label(
            new Rect(20f, 16f, 504f, 60f),
            "ドロップ禁止塗り  先に広範囲 → あとから1マスで調整\n" +
            "Shift+ドラッグ=四角  左=塗る  右=消す  1=精密  2=中  3=広範囲  Esc=終了\n" +
            $"今のブラシ: {brushSize}マス");
        Handles.EndGUI();
    }

    private static void DrawBrushPreview(Tilemap tilemap, Vector3Int center)
    {
        int extent = brushSize / 2;
        DrawRectPreview(
            tilemap,
            center + new Vector3Int(-extent, -extent, 0),
            center + new Vector3Int(extent, extent, 0));
    }

    private static void DrawRectPreview(
        Tilemap tilemap,
        Vector3Int start,
        Vector3Int end)
    {
        Vector3 minCenter = tilemap.GetCellCenterWorld(
            new Vector3Int(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), 0));
        Vector3 maxCenter = tilemap.GetCellCenterWorld(
            new Vector3Int(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y), 0));
        Vector3 half = tilemap.cellSize * 0.5f;
        Vector3[] corners =
        {
            new(minCenter.x - half.x, minCenter.y - half.y, 0f),
            new(minCenter.x - half.x, maxCenter.y + half.y, 0f),
            new(maxCenter.x + half.x, maxCenter.y + half.y, 0f),
            new(maxCenter.x + half.x, minCenter.y - half.y, 0f)
        };
        Handles.DrawSolidRectangleWithOutline(
            corners,
            new Color(1f, 0.15f, 0.15f, 0.18f),
            new Color(1f, 0.15f, 0.15f, 0.95f));
    }

    private static bool TryGetCell(
        Tilemap tilemap,
        Vector2 guiPoint,
        out Vector3Int cell)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float enter))
        {
            cell = default;
            return false;
        }

        Vector3 world = ray.GetPoint(enter);
        cell = tilemap.WorldToCell(world);
        return true;
    }

    private static Tilemap FindPaintTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null &&
                maps[i].gameObject.name == KomayamaNoDropPaintUtility.PaintObjectName)
            {
                return maps[i];
            }
        }

        return null;
    }

}
