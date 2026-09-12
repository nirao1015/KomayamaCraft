using UnityEngine;

/// <summary>
/// Work 系仕事場から取り出したアイテムの Canvas 付け替えを共通化する。
/// </summary>
public static class WorkplaceDragExtractionUtility
{
    public static void ReparentExtractedItemToWorkCanvas(DraggableItemController item, Transform workplaceTransform)
    {
        if (item == null || workplaceTransform == null)
        {
            return;
        }

        RectTransform workCanvasRoot = workplaceTransform.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        if (workCanvasRoot == null || item.transform.parent == workCanvasRoot)
        {
            return;
        }

        item.transform.SetParent(workCanvasRoot, true);
        item.RefreshCanvasReferencesFromHierarchy();
        item.RefreshBaseStateFromCurrentTransform();
    }

    public static void ReparentExtractedItemBackToItemCanvas(DraggableItemController item, ref ItemSpawnController cachedSpawnController)
    {
        if (item == null)
        {
            return;
        }

        RectTransform itemCanvasRt = ResolveItemCanvasRect(ref cachedSpawnController);
        if (itemCanvasRt == null)
        {
            item.RefreshCanvasReferencesFromHierarchy();
            return;
        }

        if (item.transform.parent == itemCanvasRt)
        {
            item.RefreshCanvasReferencesFromHierarchy();
            return;
        }

        item.transform.SetParent(itemCanvasRt, true);
        item.RefreshCanvasReferencesFromHierarchy();
        item.RefreshBaseStateFromCurrentTransform();
    }

    public static RectTransform ResolveItemCanvasRect(ref ItemSpawnController cachedSpawnController)
    {
        if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
        {
            cachedSpawnController = Object.FindObjectOfType<ItemSpawnController>(true);
        }

        if (cachedSpawnController != null && cachedSpawnController.ItemCanvas != null)
        {
            return cachedSpawnController.ItemCanvas;
        }

        Transform[] all = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == "ItemCanvas")
            {
                return t as RectTransform;
            }
        }

        return null;
    }
}

