using UnityEngine;

/// <summary>
/// 水平タイルループ上の最短距離・プレイヤーに最も近いワールド座標（ItemComDevice 表示／ナビ用）。
/// </summary>
public static class Game03HorizontalLoopUtility
{
    public static float SignedShortestLoopDeltaX(float referenceWorldX, float targetWorldX, float loopPeriod)
    {
        if (loopPeriod <= 0.01f)
        {
            return targetWorldX - referenceWorldX;
        }

        float dx = targetWorldX - referenceWorldX;
        float half = loopPeriod * 0.5f;
        dx -= Mathf.Round(dx / loopPeriod) * loopPeriod;
        if (dx > half)
        {
            dx -= loopPeriod;
        }
        else if (dx < -half)
        {
            dx += loopPeriod;
        }

        return dx;
    }

    public static Vector3 GetNearestLoopWorldPosition(Vector3 referenceWorld, Vector3 targetWorld, float loopPeriod)
    {
        float nearestX = referenceWorld.x + SignedShortestLoopDeltaX(referenceWorld.x, targetWorld.x, loopPeriod);
        return new Vector3(nearestX, targetWorld.y, targetWorld.z);
    }

    public static Vector3 GetNearestLoopWorldPositionToPlayer(Game03UnitManager unitManager, Vector3 targetWorld, float loopPeriod)
    {
        float referenceX = 0f;
        if (unitManager != null && unitManager.MainUnitRect != null)
        {
            referenceX = unitManager.MainUnitRect.position.x;
        }

        return GetNearestLoopWorldPosition(new Vector3(referenceX, targetWorld.y, targetWorld.z), targetWorld, loopPeriod);
    }
}
