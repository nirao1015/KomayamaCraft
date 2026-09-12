using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class KomayamaNoBuildPaintSceneTool
{
    private const string MenuPath = "KomayamaCraft/建設不可を塗る";
    private const string TilePath =
        "Assets/GameData/KomayamaCraft/NoDropPaintTile.asset";
    private const string PaintObjectName = "NoBuildPaint";
    private const int MinBrushSize = 1;
    private const int MaxBrushSize = 11;

    private static bool enabled;
    private static int brushSize = 7;
    private static bool rectangleDrag;
    private static bool rectangleErase;
    private static Vector3Int rectangleStart;

    [MenuItem(MenuPath, false, 21)]
    public static void TogglePaintMode()
    {
        if (enabled)
        {
            SetEnabled(false);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/komayama_craft_scene.unity")
        {
            throw new System.InvalidOperationException(
                "Open komayama_craft_scene before painting no-build cells.");
        }

        Tilemap tilemap = FindPaintTilemap();
        if (tilemap == null)
        {
            throw new System.InvalidOperationException(
                "NoBuildPaint is missing. Run KomayamaCraft > M2をシーンへ接続 first.");
        }

        Selection.activeGameObject = tilemap.gameObject;
        brushSize = 7;
        SetEnabled(true);
        Debug.Log(
            "[KomayamaCraftM2] 建設不可塗りを開始しました。" +
            "Shift+ドラッグで広範囲、1 で1マス調整、Escで終了。");
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
            SceneView.duringSceneGui += OnSceneGui;
        }
        else
        {
            SceneView.duringSceneGui -= OnSceneGui;
            Debug.Log("[KomayamaCraftM2] 建設不可塗りを終了しました。");
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
        HandleUtility.AddDefaultControl(controlId);
        DrawHud();
        if (tilemap == null || !TryGetCell(tilemap, current.mousePosition, out Vector3Int cell))
        {
            return;
        }

        if (rectangleDrag)
        {
            DrawRectPreview(tilemap, rectangleStart, cell);
        }
        else
        {
            int extent = brushSize / 2;
            DrawRectPreview(
                tilemap,
                cell + new Vector3Int(-extent, -extent, 0),
                cell + new Vector3Int(extent, extent, 0));
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

    private static void HandlePaintInput(Tilemap tilemap, Vector3Int cell, Event current)
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
                    ? "Erase no-build rectangle"
                    : "Paint no-build rectangle");
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

        Undo.RecordObject(tilemap, paint ? "Paint no-build cells" : "Erase no-build cells");
        int extent = brushSize / 2;
        for (int y = -extent; y <= extent; y++)
        {
            for (int x = -extent; x <= extent; x++)
            {
                tilemap.SetTile(cell + new Vector3Int(x, y, 0), paint ? tile : null);
            }
        }

        FinishStroke(tilemap);
        current.Use();
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
            "建設不可塗り  ドロップ禁止とは別レイヤーです\n" +
            "Shift+ドラッグ=四角  左=塗る  右=消す  1=精密  2=中  3=広範囲  Esc=終了\n" +
            $"今のブラシ: {brushSize}マス");
        Handles.EndGUI();
    }

    private static void DrawRectPreview(Tilemap tilemap, Vector3Int start, Vector3Int end)
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
            new Color(0.15f, 0.45f, 1f, 0.18f),
            new Color(0.2f, 0.65f, 1f, 0.95f));
    }

    private static bool TryGetCell(Tilemap tilemap, Vector2 guiPoint, out Vector3Int cell)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float enter))
        {
            cell = default;
            return false;
        }

        cell = tilemap.WorldToCell(ray.GetPoint(enter));
        return true;
    }

    private static Tilemap FindPaintTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].gameObject.name == PaintObjectName)
            {
                return maps[i];
            }
        }

        return null;
    }
}
