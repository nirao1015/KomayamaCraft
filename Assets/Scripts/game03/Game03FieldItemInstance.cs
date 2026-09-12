using System;
using UnityEngine;

/// <summary>
/// ItemComDeviceCanvas 上に生成されるフィールドアイテム 1 体分の寿命・ルート・ワールドアンカーを保持する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03FieldItemInstance : MonoBehaviour
{
    private RectTransform rectTransform;
    private float lifetimeUnscaledSeconds;
    private float spawnUnscaledTime;
    private Transform worldAnchorTransform;
    private Transform navTrackingTransform;
    private Vector3 loopFieldLocalAnchor;
    private bool hasLoopFieldLocalAnchor;
    private Game03FieldItemPickupRoute pickupRoute;
    private Action<Game03FieldItemInstance> pickedCallback;
    private Game03FieldItemCoordinator coordinator;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (rectTransform = GetComponent<RectTransform>());
    public Transform WorldAnchorTransform => worldAnchorTransform;
    public Transform NavAnchorTransform => worldAnchorTransform != null ? worldAnchorTransform : navTrackingTransform;
    public Game03FieldItemPickupRoute PickupRoute => pickupRoute;

    public bool UsesHorizontalLoopDisplay => RouteUsesHorizontalLoopDisplay(pickupRoute);

    public static bool RouteUsesHorizontalLoopDisplay(Game03FieldItemPickupRoute route)
    {
        return route == Game03FieldItemPickupRoute.PermanentInitial
            || route == Game03FieldItemPickupRoute.MilestoneConsumable
            || route == Game03FieldItemPickupRoute.EmergencyCargo
            || route == Game03FieldItemPickupRoute.MidBossReward
            || route == Game03FieldItemPickupRoute.BossReward;
    }

    public void BindCoordinator(Game03FieldItemCoordinator coordinatorRef)
    {
        coordinator = coordinatorRef;
    }

    public void Initialize(
        Transform worldAnchor,
        Game03FieldItemPickupRoute route,
        float lifetimeUnscaledSeconds,
        Action<Game03FieldItemInstance> onPicked)
    {
        hasLoopFieldLocalAnchor = false;
        navTrackingTransform = null;
        worldAnchorTransform = worldAnchor;
        pickupRoute = route;
        this.lifetimeUnscaledSeconds = Mathf.Max(0f, lifetimeUnscaledSeconds);
        spawnUnscaledTime = Time.unscaledTime;
        pickedCallback = onPicked;
    }

    /// <summary>
    /// 撃破位置など ObstaclesRoot ローカルで固定したマップ座標（スクロールは fieldRoot 変換で追従）。
    /// </summary>
    public void InitializeWithFieldLocalAnchor(
        Vector3 fieldLocalPosition,
        Transform navAnchor,
        Game03FieldItemPickupRoute route,
        float lifetimeUnscaledSeconds,
        Action<Game03FieldItemInstance> onPicked)
    {
        worldAnchorTransform = null;
        navTrackingTransform = navAnchor;
        hasLoopFieldLocalAnchor = true;
        loopFieldLocalAnchor = fieldLocalPosition;
        pickupRoute = route;
        this.lifetimeUnscaledSeconds = Mathf.Max(0f, lifetimeUnscaledSeconds);
        spawnUnscaledTime = Time.unscaledTime;
        pickedCallback = onPicked;
    }

    public bool TryResolveLoopSourceWorld(Transform fieldRoot, out Vector3 world)
    {
        if (hasLoopFieldLocalAnchor && fieldRoot != null)
        {
            world = fieldRoot.TransformPoint(loopFieldLocalAnchor);
            return true;
        }

        if (worldAnchorTransform != null)
        {
            world = worldAnchorTransform.position;
            return true;
        }

        world = default;
        return false;
    }

    public bool IsLifetimeExpiredUnscaled()
    {
        if (lifetimeUnscaledSeconds <= 0f)
        {
            return false;
        }

        return Time.unscaledTime - spawnUnscaledTime >= lifetimeUnscaledSeconds;
    }

    public void NotifyPicked()
    {
        pickedCallback?.Invoke(this);
        pickedCallback = null;
    }

    private void OnDestroy()
    {
        coordinator?.NotifyFieldItemDestroyed(this);
    }
}
