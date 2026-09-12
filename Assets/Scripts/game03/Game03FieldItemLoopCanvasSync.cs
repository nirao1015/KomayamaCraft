using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 水平ループ最近傍ワールド座標からフィールドアイテム Canvas 上の anchoredPosition を同期する。
/// </summary>
public static class Game03FieldItemLoopCanvasSync
{
    public static void SyncInstancesOnCanvas(
        IReadOnlyList<Game03FieldItemInstance> instances,
        Game03UnitManager unitManager,
        Camera worldCamera,
        RectTransform targetCanvas)
    {
        if (instances == null || instances.Count == 0 || unitManager == null || targetCanvas == null)
        {
            return;
        }

        float loopPeriod = unitManager.HorizontalLoopWorldPeriod;
        Transform fieldRoot = unitManager.FieldRoot;

        for (int i = 0; i < instances.Count; i++)
        {
            Game03FieldItemInstance instance = instances[i];
            if (instance == null || !instance.UsesHorizontalLoopDisplay)
            {
                continue;
            }

            RectTransform rt = instance.RectTransform;
            if (rt == null || !instance.TryResolveLoopSourceWorld(fieldRoot, out Vector3 sourceWorld))
            {
                continue;
            }

            Vector3 worldForUi = Game03HorizontalLoopUtility.GetNearestLoopWorldPositionToPlayer(
                unitManager,
                sourceWorld,
                loopPeriod);

            if (TryWorldPointToCanvasLocal(worldCamera, targetCanvas, worldForUi, out Vector2 anchored))
            {
                rt.anchoredPosition = anchored;
            }
        }
    }

    public static bool TryWorldPointToCanvasLocal(
        Camera worldCamera,
        RectTransform targetCanvas,
        Vector3 worldPosition,
        out Vector2 localAnchored)
    {
        localAnchored = Vector2.zero;
        Camera wc = worldCamera != null ? worldCamera : Camera.main;
        if (wc == null || targetCanvas == null)
        {
            return false;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(wc, worldPosition);
        Camera eventCamera = GetUiEventCamera(targetCanvas);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetCanvas, screenPoint, eventCamera, out localAnchored);
    }

    private static Camera GetUiEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }
}
