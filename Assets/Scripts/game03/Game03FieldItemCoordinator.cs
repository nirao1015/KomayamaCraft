using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 恒久／経験値 milestone のフィールドアイテム、ナビ、Pod 開始を束ねる。
/// </summary>
[DefaultExecutionOrder(-120)]
[DisallowMultipleComponent]
public sealed class Game03FieldItemCoordinator : MonoBehaviour
{
    private readonly Dictionary<Game03FieldItemInstance, RectTransform> navigationByItemInstance = new Dictionary<Game03FieldItemInstance, RectTransform>(16);
    private readonly HashSet<int> deployedScratch = new HashSet<int>();
    private readonly List<Transform> eligiblePlacesScratch = new List<Transform>(8);
    private readonly Queue<byte> pendingMilestoneSpawnSignals = new Queue<byte>(8);
    private bool firstScheduledPodItemSpawned;
    private bool secondScheduledPodItemSpawned;
    private bool thirdScheduledPodItemSpawned;

    [Header("参照")]
    [SerializeField, Tooltip("恒久アイテム（ItemComDevice）のスポーン・取得。")]
    private Game03ItemDeviceFieldController fieldItemDeviceController;
    [SerializeField, Tooltip("消費アイテム（救急箱・爆弾・電話等）のスポーン・取得。")]
    private Game03ItemDeviceFieldController consumableItemFieldController;
    [SerializeField, Tooltip("中ボス／大ボス報酬（ItemBossCanvas）。")]
    private Game03BossDropItemFieldController bossDropItemFieldController;
    [SerializeField, Tooltip("アイテム取得後の Pod 演出開始。")]
    private Game03PodManager podManager;
    [SerializeField, Tooltip("サブユニット装備状況（Pod 種別抽選）。")]
    private Game03WeaponManager weaponManager;
    [SerializeField, Tooltip("経験値 milestone・電話報酬など。")]
    private Game03StatusManager statusManager;
    [SerializeField, Tooltip("ユニット位置・向き。")]
    private Game03UnitManager unitManager;
    [SerializeField, Tooltip("ゲームプレイ可否。")]
    private Game03Manager game03Manager;
    [SerializeField, Tooltip("爆弾の画面内敵判定に使用。未設定時は Camera.main。")]
    private Camera gameplayCamera;
    [SerializeField, Tooltip("爆弾による通常敵一掃。")]
    private Game03EnemyManager enemyManager;
    [SerializeField, Tooltip("経験値ドロップの生成・爆弾時の全吸い込み開始。")]
    private Game03ExperienceFieldController experienceFieldController;
    [SerializeField, Tooltip("消費アイテム取得 SE。")]
    private Game03SeManager game03SeManager;
    [SerializeField, Tooltip("爆弾取得時の BombEffectImage 演出。")]
    private Game03BombItemEffectPlayer bombItemEffectPlayer;
    [SerializeField, Tooltip("電話アイテム取得時の視聴者数 UI 加算演出。")]
    private Game03PopularAddUiPlayer popularAddUiPlayer;

    [Header("配置マーカー")]
    [SerializeField, Tooltip("ObstaclesRoot 上の恒久アイテム配置点（ItemComDevicePlace01 等）。")]
    private Transform permanentPlaceMarker;
    [SerializeField, Tooltip("経験値 milestone 用の消費アイテム配置点（最大3）。")]
    private Transform[] milestonePlaceMarkers = new Transform[3];

    [Header("サブユニット")]
    [SerializeField, Tooltip("Unit01～04 の RectTransform。装備済み Pod 種別の抽選に使用。")]
    private RectTransform[] subUnitSlots = new RectTransform[4];

    [Header("ナビ")]
    [SerializeField, Tooltip("複製元 NaviBase。Game03ItemNavEntry をエディタで付与。位置は Inspector で設定。")]
    private RectTransform navBaseTemplate;
    [SerializeField, Tooltip("ナビ複製物の親。")]
    private RectTransform itemNaviParent;

    [Header("Pod 加入スケジュール（ゲーム内経過秒）")]
    [SerializeField, Min(0f), Tooltip("2 回目の Pod 用 ItemComDevice（1 回目は開始直後の恒久スポーン）。")]
    private float firstPodJoinGameplaySeconds = 60f;
    [SerializeField, Min(0f), Tooltip("3 回目の Pod 用アイテム出現。")]
    private float secondPodJoinGameplaySeconds = 180f;
    [SerializeField, Min(0f), Tooltip("4 回目の Pod 用アイテム出現。")]
    private float thirdPodJoinGameplaySeconds = 300f;

    [Header("Milestone 消費アイテム")]
    [SerializeField, Min(1f), Tooltip("milestone 用消費アイテムの寿命（秒・unscaled）。")]
    private float milestoneItemLifetimeUnscaledSeconds = 45f;

    [Header("緊急カーゴ用テンプレート")]
    [SerializeField, Tooltip("救急箱の ItemNormalCanvas テンプレート RectTransform。")]
    private RectTransform cargoTemplateMedkit;
    [SerializeField, Tooltip("爆弾の ItemNormalCanvas テンプレート RectTransform。")]
    private RectTransform cargoTemplateBomb;
    [SerializeField, Tooltip("電話の ItemNormalCanvas テンプレート RectTransform。")]
    private RectTransform cargoTemplatePhone;

    [Header("爆弾・可視判定")]
    [SerializeField, Range(0f, 0.2f), Tooltip("爆弾が撃破する敵のビューポート判定マージン（0～0.2）。")]
    private float viewportMargin = 0.02f;

    private void Awake()
    {
        if (navBaseTemplate != null)
        {
            navBaseTemplate.gameObject.SetActive(false);
        }

        TrySpawnPermanentInitialItemWithNavigation();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (navBaseTemplate != null && navBaseTemplate.GetComponent<Game03ItemNavEntry>() == null)
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] navBaseTemplate（NaviBase）に Game03ItemNavEntry をアタッチしてください。", this);
        }
    }
#endif

    private void OnEnable()
    {
        if (statusManager != null)
        {
            statusManager.ExperienceMilestoneReached += OnExperienceMilestoneReached;
        }
    }

    private void OnDisable()
    {
        if (statusManager != null)
        {
            statusManager.ExperienceMilestoneReached -= OnExperienceMilestoneReached;
        }
    }

    private void Update()
    {
        TrySpawnScheduledPodJoinItems();

        while (pendingMilestoneSpawnSignals.Count > 0)
        {
            if (!TrySpawnSingleMilestoneConsumable())
            {
                break;
            }

            pendingMilestoneSpawnSignals.Dequeue();
        }
    }

    private void TrySpawnScheduledPodJoinItems()
    {
        if (game03Manager == null || fieldItemDeviceController == null)
        {
            return;
        }

        float elapsed = game03Manager.GameplayElapsedSeconds;
        if (!firstScheduledPodItemSpawned && elapsed >= firstPodJoinGameplaySeconds)
        {
            firstScheduledPodItemSpawned = TrySpawnPodJoinFieldItem();
        }

        if (!secondScheduledPodItemSpawned && elapsed >= secondPodJoinGameplaySeconds)
        {
            secondScheduledPodItemSpawned = TrySpawnPodJoinFieldItem();
        }

        if (!thirdScheduledPodItemSpawned && elapsed >= thirdPodJoinGameplaySeconds)
        {
            thirdScheduledPodItemSpawned = TrySpawnPodJoinFieldItem();
        }
    }

    private void OnExperienceMilestoneReached(Game03ExperienceMilestoneReachedArgs args)
    {
        int count = Mathf.Max(1, args.SpawnCount);
        for (int i = 0; i < count; i++)
        {
            pendingMilestoneSpawnSignals.Enqueue(1);
        }
    }

    private void TrySpawnPermanentInitialItemWithNavigation()
    {
        if (fieldItemDeviceController == null || permanentPlaceMarker == null)
        {
            return;
        }

        FieldItemSpawnConfig cfg = FieldItemSpawnConfig.Make(
            lifetimeUnscaledSeconds: 0f,
            route: Game03FieldItemPickupRoute.PermanentInitial,
            onPicked: HandleInstancePicked);

        if (!fieldItemDeviceController.TrySpawnFieldItem(permanentPlaceMarker, cfg, out Game03FieldItemInstance instance))
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] Permanent field item spawn failed.");
            return;
        }

        FinalizeSpawn(instance);
    }

    private bool TrySpawnPodJoinFieldItem()
    {
        if (fieldItemDeviceController == null)
        {
            return false;
        }

        Transform marker = PickPodJoinPlaceMarker();
        if (marker == null)
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] Pod join field item: no place marker.");
            return false;
        }

        FieldItemSpawnConfig cfg = FieldItemSpawnConfig.Make(
            lifetimeUnscaledSeconds: 0f,
            route: Game03FieldItemPickupRoute.PermanentInitial,
            onPicked: HandleInstancePicked);

        if (!fieldItemDeviceController.TrySpawnFieldItem(marker, cfg, out Game03FieldItemInstance instance))
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] Pod join field item spawn failed.");
            return false;
        }

        FinalizeSpawn(instance);
        return true;
    }

    private Transform PickPodJoinPlaceMarker()
    {
        CollectEligibleMilestonePlaces(eligiblePlacesScratch);
        if (eligiblePlacesScratch.Count > 0)
        {
            return eligiblePlacesScratch[UnityEngine.Random.Range(0, eligiblePlacesScratch.Count)];
        }

        return permanentPlaceMarker;
    }

    private bool TrySpawnSingleMilestoneConsumable()
    {
        CollectEligibleMilestonePlaces(eligiblePlacesScratch);
        if (eligiblePlacesScratch.Count == 0)
        {
            return false;
        }

        Transform marker = eligiblePlacesScratch[UnityEngine.Random.Range(0, eligiblePlacesScratch.Count)];
        FieldItemSpawnConfig cfg = FieldItemSpawnConfig.Make(
            milestoneItemLifetimeUnscaledSeconds,
            Game03FieldItemPickupRoute.MilestoneConsumable,
            HandleInstancePicked);

        if (!fieldItemDeviceController.TrySpawnFieldItem(marker, cfg, out Game03FieldItemInstance instance))
        {
            return false;
        }

        FinalizeSpawn(instance);
        return true;
    }

    private void FinalizeSpawn(Game03FieldItemInstance instance)
    {
        instance.BindCoordinator(this);
        TrySpawnNavigationFor(instance);
    }

    /// <summary>ItemBossCanvas 上のボス報酬アイテム用ナビを付与する。</summary>
    public void FinalizeBossDropSpawn(Game03FieldItemInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.BindCoordinator(this);
        TrySpawnNavigationFor(instance);
    }

    /// <summary>
    /// エマージェンシーカーゴ着弾: 抽選済み種別の消費アイテムをワールド座標に生成する。
    /// </summary>
    public bool TrySpawnCargoFieldItem(Transform worldAnchor, Game03CargoItemKind kind)
    {
        if (consumableItemFieldController == null || worldAnchor == null)
        {
            return false;
        }

        RectTransform template = ResolveCargoTemplate(kind);
        if (template == null)
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] Cargo template missing for " + kind);
            return false;
        }

        FieldItemSpawnConfig cfg = FieldItemSpawnConfig.Make(
            milestoneItemLifetimeUnscaledSeconds,
            Game03FieldItemPickupRoute.EmergencyCargo,
            HandleInstancePicked);

        if (!consumableItemFieldController.TrySpawnFieldItem(worldAnchor, cfg, template, out Game03FieldItemInstance instance))
        {
            return false;
        }

        Game03FieldConsumableKindBinding binding = instance.GetComponent<Game03FieldConsumableKindBinding>();
        if (binding == null)
        {
            binding = instance.gameObject.AddComponent<Game03FieldConsumableKindBinding>();
        }

        binding.Configure(kind);
        FinalizeSpawn(instance);
        return true;
    }

    private RectTransform ResolveCargoTemplate(Game03CargoItemKind kind)
    {
        return kind switch
        {
            Game03CargoItemKind.Medkit => cargoTemplateMedkit,
            Game03CargoItemKind.Bomb => cargoTemplateBomb,
            Game03CargoItemKind.Phone => cargoTemplatePhone,
            _ => null
        };
    }

    private void TrySpawnNavigationFor(Game03FieldItemInstance instance)
    {
        if (navBaseTemplate == null || itemNaviParent == null || instance == null || instance.NavAnchorTransform == null)
        {
            return;
        }

        RectTransform clone = Instantiate(navBaseTemplate, itemNaviParent);
        clone.gameObject.SetActive(true);

        Game03ItemNavEntry entry = clone.GetComponent<Game03ItemNavEntry>();
        if (entry == null)
        {
            Debug.LogError("[Game03FieldItemCoordinator] navBaseTemplate に Game03ItemNavEntry が無いためナビを複製できません。NaviBase にコンポーネントを付けてください。", this);
            Destroy(clone.gameObject);
            return;
        }

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        TryResolveTrackingItemIcon(instance.RectTransform, out Sprite iconSprite);
        entry.Initialize(
            instance.NavAnchorTransform,
            itemNaviParent,
            unitManager,
            game03Manager,
            cam,
            iconSprite,
            instance.UsesHorizontalLoopDisplay);
        navigationByItemInstance[instance] = clone;
    }

    /// <summary>
    /// ItemComDevice 複製ルート配下の Image から追跡アイコンを取得する（名前に Itemimage を含むもの優先）。
    /// </summary>
    private static bool TryResolveTrackingItemIcon(RectTransform fieldItemRoot, out Sprite sprite)
    {
        sprite = null;
        if (fieldItemRoot == null)
        {
            return false;
        }

        Image[] images = fieldItemRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img.sprite == null)
            {
                continue;
            }

            if (img.gameObject.name.IndexOf("itemimage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sprite = img.sprite;
                return true;
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img != null && img.sprite != null)
            {
                sprite = img.sprite;
                return true;
            }
        }

        return false;
    }

    private void CollectEligibleMilestonePlaces(List<Transform> buffer)
    {
        buffer.Clear();
        if (milestonePlaceMarkers == null)
        {
            return;
        }

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        for (int i = 0; i < milestonePlaceMarkers.Length; i++)
        {
            Transform t = milestonePlaceMarkers[i];
            if (t == null)
            {
                continue;
            }

            if (!IsTransformRoughlyVisibleOnScreen(cam, t.position))
            {
                buffer.Add(t);
            }
        }
    }

    private bool IsTransformRoughlyVisibleOnScreen(Camera cam, Vector3 worldPosition)
    {
        if (cam == null)
        {
            return false;
        }

        Vector3 vp = cam.WorldToViewportPoint(worldPosition);
        if (vp.z <= 0f)
        {
            return false;
        }

        float m = viewportMargin;
        return vp.x >= m && vp.x <= 1f - m && vp.y >= m && vp.y <= 1f - m;
    }

    public void NotifyFieldItemDestroyed(Game03FieldItemInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        RemoveNavigationFor(instance);
    }

    private void RemoveNavigationFor(Game03FieldItemInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        if (!navigationByItemInstance.TryGetValue(instance, out RectTransform nav))
        {
            return;
        }

        navigationByItemInstance.Remove(instance);
        if (nav != null)
        {
            Destroy(nav.gameObject);
        }
    }

    private void HandleInstancePicked(Game03FieldItemInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        RemoveNavigationFor(instance);

        if (instance.PickupRoute == Game03FieldItemPickupRoute.PermanentInitial)
        {
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_06);
        }

        if (instance.TryGetComponent(out Game03FieldConsumableKindBinding binding))
        {
            Game03CombatBalanceLogger.TryGet()?.RecordItemPickup(binding.Kind);
            ApplyConsumablePickupEffect(binding.Kind);
            return;
        }

        int podType = ResolvePodTypeForPickup(instance.PickupRoute);
        if (podType < 2 || podType > 7)
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] No eligible pod type for pickup.");
            return;
        }

        if (podManager == null)
        {
            Debug.LogWarning("[Game03FieldItemCoordinator] PodManager missing.");
            return;
        }

        bool ok = podManager.StartPodSequence(podType);
        if (ok)
        {
            Game03CombatBalanceLogger.TryGet()?.RecordPodPickup(podType);
        }

        if (ok && instance.PickupRoute == Game03FieldItemPickupRoute.MilestoneConsumable && statusManager != null)
        {
            statusManager.NotifyExperienceMilestonePodJoinCompleted();
        }
    }

    private void ApplyConsumablePickupEffect(Game03CargoItemKind kind)
    {
        game03SeManager?.TryPlayConsumableItemPickupSe(kind);

        switch (kind)
        {
            case Game03CargoItemKind.Medkit:
                if (unitManager != null)
                {
                    unitManager.ApplyMedkitHealFromCurrentHpFraction(0.30f);
                }

                break;
            case Game03CargoItemKind.Bomb:
                bombItemEffectPlayer?.PlayIfConfigured();

                Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
                if (enemyManager != null)
                {
                    enemyManager.InstantKillVisibleNormalEnemiesForBombItem(cam, viewportMargin);
                }

                if (experienceFieldController != null)
                {
                    experienceFieldController.BeginAbsorbSequenceForAllPickupsFromBombItem();
                }

                break;
            case Game03CargoItemKind.Phone:
                if (statusManager != null
                    && statusManager.TryAddPhoneItemViewerReward(out int added)
                    && added > 0)
                {
                    popularAddUiPlayer?.PlayAdd((long)added * Game03MetaEconomyRules.PopularDisplayMultiplier);
                }

                break;
        }
    }

    private int ResolvePodTypeForPickup(Game03FieldItemPickupRoute route)
    {
        CollectDeployedSubPodTypes(deployedScratch);

        if (route == Game03FieldItemPickupRoute.PermanentInitial)
        {
            if (statusManager != null
                && !statusManager.HasExperienceMilestonePodJoinCompleted
                && !deployedScratch.Contains(2))
            {
                return 2;
            }

            return TryDrawEligiblePodType(deployedScratch, out int pod) ? pod : -1;
        }

        return TryDrawEligiblePodType(deployedScratch, out int drawn) ? drawn : -1;
    }

    private void CollectDeployedSubPodTypes(HashSet<int> output)
    {
        Game03PodTypeSelection.CollectDeployedSubPodTypes(weaponManager, subUnitSlots, output);
    }

    private static bool TryDrawEligiblePodType(HashSet<int> deployed, out int podType) =>
        Game03PodTypeSelection.TryDrawEligiblePodType(deployed, out podType);
}
