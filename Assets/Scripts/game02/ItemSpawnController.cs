using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ItemSpawnController : MonoBehaviour
{
    private const int MaxItemStreamCount = 8;
    private const int MaxItemMovieCount = 5;

    [Header("Refs")]
    [SerializeField] private RectTransform itemCanvas;
    [SerializeField] private ItemPlacementZone itemPlacementZone;
    [SerializeField] private GameObject itemMailCharaPrefab;
    [SerializeField] private GameObject itemEditor01Prefab;
    [SerializeField] private GameObject itemEditor02Prefab;
    [SerializeField] private GameObject itemEditor03Prefab;
    [SerializeField] private GameObject itemStreamPrefab;
    [SerializeField] private GameObject itemMoviePrefab;
    [SerializeField] private ItemStreamGenreCatalog itemStreamGenreCatalog;

    [Header("Fallback Prefab Path")]
    [SerializeField] private string fallbackPrefabFolder = "Assets/Prefabs/game02";
    [SerializeField] private string fallbackItemMailCharaPrefabName = "ItemMailChara";
    [SerializeField] private string fallbackItemEditor01PrefabName = "ItemEditor01";
    [SerializeField] private string fallbackItemEditor02PrefabName = "ItemEditor02";
    [SerializeField] private string fallbackItemEditor03PrefabName = "ItemEditor03";
    [SerializeField] private string fallbackItemStreamPrefabName = "ItemStream_";
    [SerializeField] private string fallbackItemMoviePrefabName = "ItemMovie_";

    [Header("Spawn Placement")]
    [SerializeField] private float spawnNearRadius = 120f;

    private bool hasExecutedInitialCheck;

    public RectTransform ItemCanvas => itemCanvas;
    public ItemPlacementZone PlacementZone => itemPlacementZone;

    /// <summary>ItemCanvas 上に存在する有効な ItemStream の個数（アイテム欄の配信素材数）。</summary>
    public int GetActiveItemStreamCountOnCanvas() => CountActiveItemsByType(ItemType.ItemStream);

    public void ExecuteInitialMainCharacterSpawnCheckOnce()
    {
        if (hasExecutedInitialCheck)
        {
            return;
        }

        hasExecutedInitialCheck = true;
        EnsureItemExists(ItemType.ItemMailChara);
    }

    /// <summary>
    /// 新規プレイ・開始演出終了直前用。ItemCanvas 上に ItemMailChara が無ければワールド座標に対応する位置へ生成する。
    /// </summary>
    public bool TrySpawnInitialMailCharaAtWorldPositionIfAbsent(Vector3 worldPosition)
    {
        if (itemCanvas == null)
        {
            itemCanvas = GetComponentInParent<RectTransform>();
        }

        if (itemCanvas == null)
        {
            Debug.LogWarning("[ItemSpawnController] ItemCanvas 参照が無いため ItemMailChara を生成できません。");
            return false;
        }

        DraggableItemController existing = FindExistingMailCharaOnCanvas();
        if (existing != null)
        {
            hasExecutedInitialCheck = true;
            return true;
        }

        if (hasExecutedInitialCheck)
        {
            return false;
        }

        hasExecutedInitialCheck = true;

        GameObject prefab = ResolvePrefab(ItemType.ItemMailChara);
        if (prefab == null)
        {
            Debug.LogWarning("[ItemSpawnController] ItemMailChara prefab を解決できません。");
            return false;
        }

        GameObject instance = Instantiate(prefab, itemCanvas, false);
        instance.name = ItemType.ItemMailChara.ToString();

        DraggableItemController controller = instance.GetComponent<DraggableItemController>();
        if (controller == null)
        {
            Debug.LogWarning("[ItemSpawnController] ItemMailChara prefab に DraggableItemController がありません。");
            Destroy(instance);
            return false;
        }

        controller.SetItemType(ItemType.ItemMailChara);

        RectTransform itemRect = instance.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            if (!TryWorldPointToCanvasAnchored(worldPosition, out Vector2 anchored))
            {
                TryGetPlacementPointNear(Vector2.zero, out anchored);
            }

            itemRect.anchoredPosition = anchored;
        }

        // StageStartOverlay 再生中はここで Reflow しない。ReflowMailChara が必ずゾーン左端へ寄せるため、
        // 演出が終わる前に ItemCanvas 側が「左に一瞬出た」ように見えてしまう。
        // レイアウト適用は GameManager.KickGameplayStart でまとめて行う。
        return true;
    }

    private DraggableItemController FindExistingMailCharaOnCanvas()
    {
        if (itemCanvas == null)
        {
            return null;
        }

        DraggableItemController[] existingItems = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
        for (int i = 0; i < existingItems.Length; i++)
        {
            DraggableItemController item = existingItems[i];
            if (item != null && item.ItemType == ItemType.ItemMailChara)
            {
                return item;
            }
        }

        return null;
    }

    private bool TryWorldPointToCanvasAnchored(Vector3 worldPosition, out Vector2 anchored)
    {
        anchored = Vector2.zero;
        if (itemCanvas == null)
        {
            return false;
        }

        Canvas canvas = itemCanvas.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam != null ? cam : Camera.main, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(itemCanvas, screenPoint, cam, out anchored);
    }

    public bool TryGetPlacementPointNear(Vector2 preferredPoint, out Vector2 point)
    {
        return TryGetPlacementPointNear(preferredPoint, null, out point);
    }

    public bool TryGetPlacementPointNear(Vector2 preferredPoint, DraggableItemController targetItem, out Vector2 point)
    {
        point = preferredPoint;
        if (itemPlacementZone == null || itemCanvas == null)
        {
            return false;
        }

        if (targetItem != null &&
            itemPlacementZone.TryGetPlacementPointForItem(itemCanvas, targetItem, preferredPoint, out point))
        {
            return true;
        }

        return itemPlacementZone.TryGetRandomPointNear(
            itemCanvas,
            preferredPoint,
            Mathf.Max(0f, spawnNearRadius),
            out point);
    }

    public bool TrySpawnItem(ItemType itemType, Vector2 anchoredPosition, bool triggerBounceToZone, out DraggableItemController spawnedItem)
    {
        return TrySpawnItem(itemType, anchoredPosition, triggerBounceToZone, out spawnedItem, null, false);
    }

    public bool TrySpawnItem(
        ItemType itemType,
        Vector2 anchoredPosition,
        bool triggerBounceToZone,
        out DraggableItemController spawnedItem,
        long? itemStreamSpawnPopularity)
    {
        return TrySpawnItem(itemType, anchoredPosition, triggerBounceToZone, out spawnedItem, itemStreamSpawnPopularity, false);
    }

    public bool TrySpawnItem(
        ItemType itemType,
        Vector2 anchoredPosition,
        bool triggerBounceToZone,
        out DraggableItemController spawnedItem,
        long? itemStreamSpawnPopularity,
        bool skipInitialPlacementReflow)
    {
        spawnedItem = null;
        if (IsSpawnBlockedByFatalError())
        {
            return false;
        }

        if (itemCanvas == null)
        {
            itemCanvas = GetComponentInParent<RectTransform>();
        }

        if (itemCanvas == null)
        {
            Debug.LogWarning("[ItemSpawnController] ItemCanvas 参照が無いため生成できません。");
            return false;
        }

        GameObject prefab = ResolvePrefab(itemType);
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemSpawnController] {itemType} prefab を解決できません。");
            return false;
        }

        GameObject instance = Instantiate(prefab, itemCanvas, false);
        instance.name = itemType.ToString();
        TryAssignManagedName(instance, itemType);

        RectTransform itemRect = instance.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.anchoredPosition = anchoredPosition;
        }

        spawnedItem = instance.GetComponent<DraggableItemController>();
        if (spawnedItem == null)
        {
            Debug.LogWarning($"[ItemSpawnController] Spawned prefab for {itemType} has no DraggableItemController. Destroying instance.");
            Destroy(instance);
            return false;
        }

        spawnedItem.SetItemType(itemType);
        TryApplyItemSpawnExtensions(instance, itemType, itemStreamSpawnPopularity);
        if (triggerBounceToZone)
        {
            spawnedItem.MarkSuppressPlacementReflowForSpawnBounce();
            StartCoroutine(CoDeferredSpawnBounce(spawnedItem));
        }
        else if (!skipInitialPlacementReflow)
        {
            NotifyPlacementLayoutNeedsRefresh();
        }

        TryAutoDiscardIfOverLimit(itemType, spawnedItem);

        return true;
    }

    private void TryAutoDiscardIfOverLimit(ItemType itemType, DraggableItemController spawnedItem)
    {
        if (spawnedItem == null || itemCanvas == null)
        {
            return;
        }

        int maxCount = ResolveMaxCount(itemType);
        if (maxCount <= 0)
        {
            return;
        }

        int current = CountActiveItemsByType(itemType);
        if (current <= maxCount)
        {
            return;
        }

        TrashDropTarget trash = TrashDropTarget.FindActiveTarget();
        if (trash == null)
        {
            // Trash が無い場合でも上限逸脱を放置しない。
            Destroy(spawnedItem.gameObject);
            return;
        }

        trash.TryAutoDiscard(spawnedItem);
    }

    private int ResolveMaxCount(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.ItemStream:
                return MaxItemStreamCount;
            case ItemType.ItemMovie:
                return MaxItemMovieCount;
            default:
                return -1;
        }
    }

    private int CountActiveItemsByType(ItemType itemType)
    {
        if (itemCanvas == null)
        {
            return 0;
        }

        int count = 0;
        DraggableItemController[] all = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
        for (int i = 0; i < all.Length; i++)
        {
            DraggableItemController item = all[i];
            if (item == null || !item.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (item.ItemType == itemType)
            {
                count += 1;
            }
        }

        return count;
    }

    private IEnumerator CoDeferredSpawnBounce(DraggableItemController item)
    {
        yield return null;
        if (item != null)
        {
            item.TriggerBounceBackFromCurrentPosition();
        }
    }

    private static bool IsSpawnBlockedByFatalError()
    {
        return Game02.GameManager.Instance != null && Game02.GameManager.Instance.HasFatalError;
    }

    private void TryApplyItemSpawnExtensions(GameObject instance, ItemType itemType, long? itemStreamSpawnPopularity)
    {
        if (instance == null)
        {
            return;
        }

        if (itemType == ItemType.ItemStream)
        {
            ItemStreamSpawnPopularity streamCarrier = instance.GetComponent<ItemStreamSpawnPopularity>();
            if (streamCarrier != null)
            {
                long value = itemStreamSpawnPopularity ??
                    (Game02.GameManager.Instance != null ? Game02.GameManager.Instance.CurrentPopularity : 0L);
                streamCarrier.ApplySpawnPopularity(value);
                streamCarrier.ApplyStreamGenre(ResolveItemStreamGenre());
            }

            return;
        }

        if (itemType != ItemType.ItemMovie)
        {
            return;
        }

        ItemMoviePower moviePower = instance.GetComponent<ItemMoviePower>();
        if (moviePower == null)
        {
            // ItemMovie_ 側に未設定でも、スポーン時ランダム画像だけは機能するよう実行時補完する。
            moviePower = instance.AddComponent<ItemMoviePower>();
        }

        moviePower.ApplyRandomDisplaySpriteIfConfigured();
    }

    private string ResolveItemStreamGenre()
    {
        if (itemStreamGenreCatalog == null)
        {
            itemStreamGenreCatalog = FindObjectOfType<ItemStreamGenreCatalog>(true);
        }

        if (itemStreamGenreCatalog == null)
        {
            return ItemStreamGenreCatalog.DefaultGenre;
        }

        return itemStreamGenreCatalog.PickRandomGenre();
    }

    public void NotifyItemDragBegan(DraggableItemController draggingItem)
    {
        if (draggingItem == null)
        {
            return;
        }

        NotifyPlacementLayoutNeedsRefresh();
    }

    public void NotifyPlacementLayoutNeedsRefresh()
    {
        if (itemPlacementZone == null || itemCanvas == null)
        {
            return;
        }

        itemPlacementZone.ReflowPlacedItems(itemCanvas);
    }

    public bool CanPlayPlacementZoneHover(DraggableItemController item)
    {
        if (item == null || itemPlacementZone == null || itemCanvas == null)
        {
            return false;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            return false;
        }

        if (item.IsDragging || item.IsBounceReturnActive || item.SuppressPlacementReflowForSpawnBounce)
        {
            return false;
        }

        return itemPlacementZone.ContainsPoint(itemCanvas, itemRect.anchoredPosition);
    }

    private void EnsureItemExists(ItemType itemType)
    {
        if (itemCanvas == null)
        {
            itemCanvas = GetComponentInParent<RectTransform>();
        }

        if (itemCanvas == null)
        {
            Debug.LogWarning("[ItemSpawnController] ItemCanvas 参照が無いため生成できません。");
            return;
        }

        DraggableItemController[] existingItems = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
        for (int i = 0; i < existingItems.Length; i++)
        {
            if (existingItems[i] != null && existingItems[i].ItemType == itemType)
            {
                return;
            }
        }

        GameObject prefab = ResolvePrefab(itemType);
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemSpawnController] {itemType} prefab を解決できません。");
            return;
        }

        string instanceName = itemType.ToString();
        GameObject instance = Instantiate(prefab, itemCanvas, false);
        instance.name = instanceName;

        DraggableItemController controller = instance.GetComponent<DraggableItemController>();
        if (controller != null)
        {
            controller.SetItemType(itemType);
        }

        RectTransform itemRect = instance.GetComponent<RectTransform>();
        if (itemRect != null && TryGetPlacementPointNear(Vector2.zero, out Vector2 spawnPoint))
        {
            itemRect.anchoredPosition = spawnPoint;
        }

        NotifyPlacementLayoutNeedsRefresh();
    }

    private GameObject ResolvePrefab(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.ItemMailChara:
                return ResolvePrefabByName(ref itemMailCharaPrefab, fallbackItemMailCharaPrefabName);
            case ItemType.ItemEditor01:
                return ResolvePrefabByName(ref itemEditor01Prefab, fallbackItemEditor01PrefabName);
            case ItemType.ItemEditor02:
                return ResolvePrefabByName(ref itemEditor02Prefab, fallbackItemEditor02PrefabName);
            case ItemType.ItemEditor03:
                return ResolvePrefabByName(ref itemEditor03Prefab, fallbackItemEditor03PrefabName);
            case ItemType.ItemStream:
                return ResolvePrefabByName(ref itemStreamPrefab, fallbackItemStreamPrefabName);
            case ItemType.ItemMovie:
                return ResolvePrefabByName(ref itemMoviePrefab, fallbackItemMoviePrefabName);
            default:
                return null;
        }
    }

    private void TryAssignManagedName(GameObject instance, ItemType itemType)
    {
        if (instance == null)
        {
            return;
        }

        if (Game02.GameManager.Instance == null)
        {
            return;
        }

        if (Game02.GameManager.Instance.TryIssueNextItemDisplayName(itemType, out string generatedName))
        {
            instance.name = generatedName;
        }
    }

    private GameObject ResolvePrefabByName(ref GameObject prefabField, string fallbackName)
    {
        if (prefabField != null)
        {
            return prefabField;
        }

#if UNITY_EDITOR
        string safeFolder = string.IsNullOrEmpty(fallbackPrefabFolder)
            ? "Assets/Prefabs/game02"
            : fallbackPrefabFolder.TrimEnd('/', '\\');
        string safeName = string.IsNullOrEmpty(fallbackName)
            ? string.Empty
            : fallbackName;
        if (string.IsNullOrEmpty(safeName))
        {
            return null;
        }

        string path = $"{safeFolder}/{safeName}.prefab";
        prefabField = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefabField;
#else
        return null;
#endif
    }
}
