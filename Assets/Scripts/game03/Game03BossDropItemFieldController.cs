using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// ItemBossCanvas 上の中ボス／大ボス報酬アイテム（出現種別51・61 撃破ドロップ）の生成・取得。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03BossDropItemFieldController : MonoBehaviour
{
    private const int MidBossSpawnPatternId = 51;
    private const int FinalBossSpawnPatternId = 61;

    private readonly List<Game03FieldItemInstance> trackedInstances = new List<Game03FieldItemInstance>(4);

    [Header("参照")]
    [SerializeField, Tooltip("ItemBossCanvas の RectTransform。")]
    private RectTransform itemBossCanvas;
    [SerializeField, Tooltip("ItemBossCanvas.MidBoss テンプレート。")]
    private RectTransform midBossItemTemplate;
    [SerializeField, Tooltip("ItemBossCanvas.Boss テンプレート。")]
    private RectTransform bossItemTemplate;
    [SerializeField] private Game03UnitManager unitManager;
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Camera worldCamera;
    [SerializeField, Tooltip("縦方向の到達可能帯クランプ（経験値と同系）。")]
    private Game03ExperienceFieldController experienceFieldController;
    [SerializeField] private Game03UnitUgManager unitUgManager;
    [SerializeField] private Game03BossUgManager bossUgManager;
    [SerializeField, Tooltip("ナビ複製用。未設定時は Find。")]
    private Game03FieldItemCoordinator fieldItemCoordinator;

    [Header("取得")]
    [SerializeField, Min(0.5f), Tooltip("ユニット中心からこの距離（Canvas ローカル px）以内でタッチ取得。")]
    private float pickupRadiusPixels = 96f;

    private void Awake()
    {
        if (itemBossCanvas == null)
        {
            itemBossCanvas = transform as RectTransform;
        }

        if (midBossItemTemplate != null)
        {
            midBossItemTemplate.gameObject.SetActive(false);
        }

        if (bossItemTemplate != null)
        {
            bossItemTemplate.gameObject.SetActive(false);
        }

        if (fieldItemCoordinator == null)
        {
            fieldItemCoordinator = FindAnyObjectByType<Game03FieldItemCoordinator>(FindObjectsInactive.Include);
        }
    }

    private void Update()
    {
        if (itemBossCanvas == null || unitManager == null)
        {
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        Game03FieldItemLoopCanvasSync.SyncInstancesOnCanvas(trackedInstances, unitManager, worldCamera, itemBossCanvas);
        ApplyScrollToPickupsForNonLoopItems();

        if (!unitManager.TryGetUnitCenterOnRect(itemBossCanvas, out Vector2 playerLocal))
        {
            return;
        }

        float pickupRadiusSq = pickupRadiusPixels * pickupRadiusPixels;
        for (int i = trackedInstances.Count - 1; i >= 0; i--)
        {
            Game03FieldItemInstance instance = trackedInstances[i];
            if (instance == null || instance.RectTransform == null)
            {
                trackedInstances.RemoveAt(i);
                continue;
            }

            Vector2 delta = playerLocal - instance.RectTransform.anchoredPosition;
            if (delta.sqrMagnitude > pickupRadiusSq)
            {
                continue;
            }

            trackedInstances.RemoveAt(i);
            instance.NotifyPicked();
            Destroy(instance.gameObject);
        }
    }

    public void TrySpawnDropForDefeatedEnemy(RectTransform enemyUiRect, int spawnPatternId)
    {
        if (enemyUiRect == null)
        {
            return;
        }

        if (spawnPatternId == MidBossSpawnPatternId)
        {
            TrySpawnDropAtEnemyCenter(enemyUiRect, midBossItemTemplate, Game03FieldItemPickupRoute.MidBossReward, OnMidBossItemPicked);
            return;
        }

        if (spawnPatternId == FinalBossSpawnPatternId)
        {
            TrySpawnDropAtEnemyCenter(enemyUiRect, bossItemTemplate, Game03FieldItemPickupRoute.BossReward, OnBossItemPicked);
        }
    }

    private void TrySpawnDropAtEnemyCenter(
        RectTransform enemyUiRect,
        RectTransform template,
        Game03FieldItemPickupRoute route,
        Action<Game03FieldItemInstance> onPicked)
    {
        if (itemBossCanvas == null || template == null || enemyUiRect == null)
        {
            Debug.LogWarning("[Game03BossDropItem] Canvas またはテンプレートが未設定のためドロップできません。", this);
            return;
        }

        Transform fieldRoot = unitManager != null ? unitManager.FieldRoot : null;
        if (fieldRoot == null)
        {
            Debug.LogWarning("[Game03BossDropItem] FieldRoot が未設定のためドロップできません。", this);
            return;
        }

        Vector3 fieldLocal = fieldRoot.InverseTransformPoint(enemyUiRect.position);
        Vector3 spawnWorld = Game03HorizontalLoopUtility.GetNearestLoopWorldPositionToPlayer(
            unitManager,
            fieldRoot.TransformPoint(fieldLocal),
            unitManager.HorizontalLoopWorldPeriod);

        if (!Game03FieldItemLoopCanvasSync.TryWorldPointToCanvasLocal(worldCamera, itemBossCanvas, spawnWorld, out Vector2 localPos))
        {
            Debug.LogWarning("[Game03BossDropItem] 敵中心の Canvas 変換に失敗しました。", this);
            return;
        }

        if (experienceFieldController != null)
        {
            localPos = experienceFieldController.ClampReachableVerticalOnCanvas(itemBossCanvas, localPos);
        }

        FieldItemSpawnConfig config = FieldItemSpawnConfig.Make(0f, route, onPicked);
        if (!TrySpawnAtCanvasLocal(localPos, template, config, out Game03FieldItemInstance instance))
        {
            return;
        }

        instance.InitializeWithFieldLocalAnchor(fieldLocal, instance.RectTransform, config.Route, config.LifetimeUnscaledSeconds, config.OnPicked);
        fieldItemCoordinator?.FinalizeBossDropSpawn(instance);
        trackedInstances.Add(instance);
    }

    private bool TrySpawnAtCanvasLocal(
        Vector2 anchoredPosition,
        RectTransform template,
        FieldItemSpawnConfig config,
        out Game03FieldItemInstance instance)
    {
        instance = null;
        if (itemBossCanvas == null || template == null)
        {
            return false;
        }

        GameObject instanceObject = Instantiate(template.gameObject, itemBossCanvas);
        instanceObject.SetActive(true);
        RectTransform instanceRect = instanceObject.GetComponent<RectTransform>();
        if (instanceRect == null)
        {
            Destroy(instanceObject);
            return false;
        }

        instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
        instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
        instanceRect.pivot = new Vector2(0.5f, 0.5f);
        instanceRect.anchoredPosition = anchoredPosition;
        instanceRect.localRotation = Quaternion.identity;
        instanceRect.localScale = Vector3.one;

        Game03FieldItemInstance itemInstance = instanceObject.GetComponent<Game03FieldItemInstance>();
        if (itemInstance == null)
        {
            itemInstance = instanceObject.AddComponent<Game03FieldItemInstance>();
        }

        instance = itemInstance;
        return true;
    }

    private bool TryEnemyCenterToCanvasLocal(RectTransform enemyUiRect, out Vector2 localAnchored)
    {
        localAnchored = Vector2.zero;
        if (itemBossCanvas == null || enemyUiRect == null)
        {
            return false;
        }

        Camera cam = GetUiEventCamera(itemBossCanvas);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, enemyUiRect.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemBossCanvas, screen, cam, out localAnchored))
        {
            localAnchored = itemBossCanvas.InverseTransformPoint(enemyUiRect.position);
        }

        return true;
    }

    private void OnMidBossItemPicked(Game03FieldItemInstance instance)
    {
        if (unitUgManager == null)
        {
            Debug.LogWarning("[Game03BossDropItem] Game03UnitUgManager が未設定のため UnitUG を開始できません。", this);
            return;
        }

        unitUgManager.TryRequestPresentation(Game03UnitUgRequestSource.MidBossDrop);
    }

    private void OnBossItemPicked(Game03FieldItemInstance instance)
    {
        if (bossUgManager == null)
        {
            Debug.LogWarning("[Game03BossDropItem] Game03BossUgManager が未設定のため BossUG を開始できません。", this);
            return;
        }

        bossUgManager.TryRequestPresentation(Game03BossUgRequestSource.BossItemDrop);
    }

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

        if (!TryConvertWorldDeltaToLocal(worldDelta, out Vector2 localDelta))
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

    private bool TryConvertWorldDeltaToLocal(Vector2 worldDelta, out Vector2 localDelta)
    {
        localDelta = Vector2.zero;
        if (itemBossCanvas == null || unitManager == null)
        {
            return false;
        }

        if (unitManager.TryConvertWorldDeltaToLocalOnRect(itemBossCanvas, worldDelta, out localDelta))
        {
            return true;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return false;
        }

        Camera eventCamera = GetUiEventCamera(itemBossCanvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemBossCanvas, Vector2.zero, eventCamera, out Vector2 p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemBossCanvas, new Vector2(Screen.width, Screen.height), eventCamera, out Vector2 p1);
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
