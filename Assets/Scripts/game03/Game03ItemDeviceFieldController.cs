using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ObstaclesRoot 上のワールド配置マーカーを ItemCanvas.ItemComDeviceCanvas に複製配置する。
/// </summary>
public sealed class Game03ItemDeviceFieldController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField, Tooltip("フィールドアイテムを配置する Canvas（ItemComDeviceCanvas 等）。")]
    private RectTransform itemDeviceCanvas;
    [SerializeField, Tooltip("複製元テンプレート。実行時は非表示にし、スポーン時に複製する。")]
    private RectTransform itemDeviceTemplate;
    [SerializeField, Tooltip("ユニット中心・フィールドスクロール同期に使用。")]
    private Game03UnitManager unitManager;
    [SerializeField, Tooltip("ポーズ中は取得判定を止める。未設定時は常に判定。")]
    private Game03Manager game03Manager;
    [SerializeField, Tooltip("ObstaclesRoot マーカーのワールド座標を Canvas ローカルへ変換するカメラ。未設定時は Camera.main。")]
    private Camera worldCamera;

    [Header("取得")]
    [SerializeField, Min(0.5f), Tooltip("ユニット中心からこの距離（Canvas ローカル px）以内でタッチ取得。")]
    private float pickupRadiusPixels = 96f;

    private readonly List<Game03FieldItemInstance> trackedInstances = new List<Game03FieldItemInstance>(32);

    private void Awake()
    {
        if (itemDeviceCanvas == null)
        {
            itemDeviceCanvas = transform as RectTransform;
        }

        if (itemDeviceTemplate != null)
        {
            itemDeviceTemplate.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (itemDeviceCanvas == null || unitManager == null)
        {
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        ApplyScrollToPickupsForNonLoopItems();
        Game03FieldItemLoopCanvasSync.SyncInstancesOnCanvas(trackedInstances, unitManager, worldCamera, itemDeviceCanvas);

        if (!unitManager.TryGetUnitCenterOnRect(itemDeviceCanvas, out Vector2 playerLocal))
        {
            return;
        }

        for (int i = trackedInstances.Count - 1; i >= 0; i--)
        {
            Game03FieldItemInstance instance = trackedInstances[i];
            if (instance == null || instance.RectTransform == null)
            {
                trackedInstances.RemoveAt(i);
                continue;
            }

            if (instance.IsLifetimeExpiredUnscaled())
            {
                trackedInstances.RemoveAt(i);
                Destroy(instance.gameObject);
                continue;
            }

            Vector2 delta = playerLocal - instance.RectTransform.anchoredPosition;
            if (delta.sqrMagnitude <= pickupRadiusPixels * pickupRadiusPixels)
            {
                trackedInstances.RemoveAt(i);
                instance.NotifyPicked();
                Destroy(instance.gameObject);
            }
        }
    }

    /// <summary>
    /// デバッグ互換: 寿命無期限・取得時コールバックなし。
    /// </summary>
    public bool TrySpawnAtObstaclePlace(Transform obstaclePlaceMarker, out RectTransform spawnedRoot)
    {
        spawnedRoot = null;
        FieldItemSpawnConfig cfg = FieldItemSpawnConfig.Make(
            0f,
            Game03FieldItemPickupRoute.PermanentInitial,
            null);

        if (!TrySpawnFieldItem(obstaclePlaceMarker, cfg, out Game03FieldItemInstance instance))
        {
            return false;
        }

        spawnedRoot = instance.RectTransform;
        return true;
    }

    /// <summary>
    /// ワールド配置マーカーに同期したフィールドアイテムを生成する（既定テンプレート）。
    /// </summary>
    public bool TrySpawnFieldItem(Transform obstaclePlaceMarker, FieldItemSpawnConfig config, out Game03FieldItemInstance instance)
    {
        return TrySpawnFieldItem(obstaclePlaceMarker, config, itemDeviceTemplate, out instance);
    }

    /// <summary>
    /// 複製元 Rect を指定してワールド配置マーカーに同期したフィールドアイテムを生成する。
    /// </summary>
    public bool TrySpawnFieldItem(
        Transform obstaclePlaceMarker,
        FieldItemSpawnConfig config,
        RectTransform templateOverride,
        out Game03FieldItemInstance instance)
    {
        instance = null;
        RectTransform template = templateOverride != null ? templateOverride : itemDeviceTemplate;
        if (itemDeviceCanvas == null || template == null || obstaclePlaceMarker == null)
        {
            Debug.LogWarning("[Game03ItemDeviceFieldController] Missing canvas, template, or place marker.");
            return false;
        }

        GameObject instanceObject = Instantiate(template.gameObject, itemDeviceCanvas);
        instanceObject.SetActive(true);
        RectTransform instanceRect = instanceObject.GetComponent<RectTransform>();
        if (instanceRect == null)
        {
            Destroy(instanceObject);
            Debug.LogWarning("[Game03ItemDeviceFieldController] Template has no RectTransform.");
            return false;
        }

        Vector3 spawnWorld = obstaclePlaceMarker.position;
        if (Game03FieldItemInstance.RouteUsesHorizontalLoopDisplay(config.Route))
        {
            spawnWorld = Game03HorizontalLoopUtility.GetNearestLoopWorldPositionToPlayer(
                unitManager,
                obstaclePlaceMarker.position,
                unitManager != null ? unitManager.HorizontalLoopWorldPeriod : 0f);
        }

        if (!Game03FieldItemLoopCanvasSync.TryWorldPointToCanvasLocal(worldCamera, itemDeviceCanvas, spawnWorld, out Vector2 anchored))
        {
            Destroy(instanceObject);
            Debug.LogWarning("[Game03ItemDeviceFieldController] Failed to convert world position to canvas local.");
            return false;
        }

        instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
        instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
        instanceRect.pivot = new Vector2(0.5f, 0.5f);
        instanceRect.anchoredPosition = anchored;
        instanceRect.localRotation = Quaternion.identity;
        instanceRect.localScale = Vector3.one;

        Game03FieldItemInstance itemInstance = instanceObject.GetComponent<Game03FieldItemInstance>();
        if (itemInstance == null)
        {
            itemInstance = instanceObject.AddComponent<Game03FieldItemInstance>();
        }

        itemInstance.Initialize(obstaclePlaceMarker, config.Route, config.LifetimeUnscaledSeconds, config.OnPicked);
        trackedInstances.Add(itemInstance);
        instance = itemInstance;
        return true;
    }

    /// <summary>水平ループ表示対象以外は、従来どおりスクロール差分を anchoredPosition に加算。</summary>
    private void ApplyScrollToPickupsForNonLoopItems()
    {
        if (unitManager == null)
        {
            return;
        }

        Vector2 worldDelta = unitManager.LastAppliedFieldDeltaWorld;
        if (worldDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (!TryConvertWorldDeltaToLocalWithFallback(worldDelta, out Vector2 localDelta))
        {
            return;
        }

        for (int i = 0; i < trackedInstances.Count; i++)
        {
            Game03FieldItemInstance instance = trackedInstances[i];
            if (instance != null && instance.UsesHorizontalLoopDisplay)
            {
                continue;
            }

            RectTransform rt = instance != null ? instance.RectTransform : null;
            if (rt != null)
            {
                rt.anchoredPosition += localDelta;
            }
        }
    }

    private bool TryConvertWorldDeltaToLocalWithFallback(Vector2 worldDelta, out Vector2 localDelta)
    {
        localDelta = Vector2.zero;
        if (itemDeviceCanvas == null || unitManager == null)
        {
            return false;
        }

        if (unitManager.TryConvertWorldDeltaToLocalOnRect(itemDeviceCanvas, worldDelta, out localDelta))
        {
            return true;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return false;
        }

        Camera eventCamera = GetUiEventCamera(itemDeviceCanvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemDeviceCanvas, Vector2.zero, eventCamera, out Vector2 p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemDeviceCanvas, new Vector2(Screen.width, Screen.height), eventCamera, out Vector2 p1);
        float localHeight = Mathf.Abs(p1.y - p0.y);
        float worldHeight = cam.orthographicSize * 2f;
        if (localHeight <= 0.0001f || worldHeight <= 0.0001f)
        {
            return false;
        }

        float localUnitsPerWorld = localHeight / worldHeight;
        localDelta = worldDelta * localUnitsPerWorld;
        return true;
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

/// <summary>
/// <see cref="Game03ItemDeviceFieldController.TrySpawnFieldItem"/> への引数。
/// </summary>
public readonly struct FieldItemSpawnConfig
{
    public readonly float LifetimeUnscaledSeconds;
    public readonly Game03FieldItemPickupRoute Route;
    public readonly Action<Game03FieldItemInstance> OnPicked;

    private FieldItemSpawnConfig(float lifetimeUnscaledSeconds, Game03FieldItemPickupRoute route, Action<Game03FieldItemInstance> onPicked)
    {
        LifetimeUnscaledSeconds = lifetimeUnscaledSeconds;
        Route = route;
        OnPicked = onPicked;
    }

    public static FieldItemSpawnConfig Make(
        float lifetimeUnscaledSeconds,
        Game03FieldItemPickupRoute route,
        Action<Game03FieldItemInstance> onPicked)
    {
        return new FieldItemSpawnConfig(lifetimeUnscaledSeconds, route, onPicked);
    }
}
