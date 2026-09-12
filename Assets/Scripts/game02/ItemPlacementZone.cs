using UnityEngine;
using System.Collections.Generic;

public class ItemPlacementZone : MonoBehaviour
{
    [SerializeField] private RectTransform zoneRect;
    [Header("Placement Range (from zone left, pixels)")]
    [SerializeField] private float itemEditorStartPixel = 120f;
    [SerializeField] private float itemStreamStartPixel = 420f;
    [SerializeField] private float itemMovieStartPixel = 780f;
    [Header("Common Gap Between Areas (pixels)")]
    [SerializeField] private float areaGapPixels = 24f;

    public RectTransform ZoneRect => zoneRect;

    private void Reset()
    {
        if (zoneRect == null)
        {
            zoneRect = GetComponent<RectTransform>();
        }
    }

    public bool TryGetRandomPoint(RectTransform targetSpace, out Vector2 pointInTargetSpace)
    {
        pointInTargetSpace = Vector2.zero;
        if (!TryGetZoneRectInTargetSpace(targetSpace, out Rect rect))
        {
            return false;
        }

        float x = Random.Range(rect.xMin, rect.xMax);
        float y = Random.Range(rect.yMin, rect.yMax);
        pointInTargetSpace = new Vector2(x, y);
        return true;
    }

    public bool TryGetRandomPointNear(
        RectTransform targetSpace,
        Vector2 preferredPointInTargetSpace,
        float nearRadius,
        out Vector2 pointInTargetSpace)
    {
        pointInTargetSpace = Vector2.zero;
        if (!TryGetZoneRectInTargetSpace(targetSpace, out Rect rect))
        {
            return false;
        }

        Vector2 clampedPreferred = new Vector2(
            Mathf.Clamp(preferredPointInTargetSpace.x, rect.xMin, rect.xMax),
            Mathf.Clamp(preferredPointInTargetSpace.y, rect.yMin, rect.yMax));

        if (nearRadius <= 0f)
        {
            pointInTargetSpace = clampedPreferred;
            return true;
        }

        Vector2 jitter = Random.insideUnitCircle * nearRadius;
        Vector2 candidate = clampedPreferred + jitter;
        pointInTargetSpace = new Vector2(
            Mathf.Clamp(candidate.x, rect.xMin, rect.xMax),
            Mathf.Clamp(candidate.y, rect.yMin, rect.yMax));
        return true;
    }

    public bool TryGetPlacementPointForItem(
        RectTransform targetSpace,
        DraggableItemController targetItem,
        Vector2 preferredPointInTargetSpace,
        out Vector2 pointInTargetSpace)
    {
        pointInTargetSpace = preferredPointInTargetSpace;
        if (targetItem == null || !TryGetZoneRectInTargetSpace(targetSpace, out Rect zoneRectInTargetSpace))
        {
            return false;
        }

        PlacementCategory category = ResolveCategory(targetItem);
        if (category == PlacementCategory.Unknown)
        {
            return false;
        }

        if (category == PlacementCategory.MailChara)
        {
            float clampedY = Mathf.Clamp(preferredPointInTargetSpace.y, zoneRectInTargetSpace.yMin, zoneRectInTargetSpace.yMax);
            pointInTargetSpace = new Vector2(zoneRectInTargetSpace.xMin, clampedY);
            return true;
        }

        if (!TryGetBandRange(category, zoneRectInTargetSpace, out float bandMinX, out float bandMaxX))
        {
            return false;
        }

        var groupedItems = new List<DraggableItemController>();
        CollectItemsInCategory(targetSpace, category, groupedItems);
        int itemCount = groupedItems.Count;
        if (itemCount <= 0)
        {
            return false;
        }

        int index = groupedItems.IndexOf(targetItem);
        if (index < 0)
        {
            groupedItems.Add(targetItem);
            index = groupedItems.Count - 1;
            itemCount = groupedItems.Count;
        }

        float x = ComputeInteriorEvenlySpacedX(index, itemCount, bandMinX, bandMaxX);
        float y = Mathf.Clamp(preferredPointInTargetSpace.y, zoneRectInTargetSpace.yMin, zoneRectInTargetSpace.yMax);
        pointInTargetSpace = new Vector2(x, y);
        return true;
    }

    public bool ContainsPoint(RectTransform targetSpace, Vector2 pointInTargetSpace)
    {
        if (!TryGetZoneRectInTargetSpace(targetSpace, out Rect zoneRectInTargetSpace))
        {
            return false;
        }

        return pointInTargetSpace.x >= zoneRectInTargetSpace.xMin &&
               pointInTargetSpace.x <= zoneRectInTargetSpace.xMax &&
               pointInTargetSpace.y >= zoneRectInTargetSpace.yMin &&
               pointInTargetSpace.y <= zoneRectInTargetSpace.yMax;
    }

    public void ReflowPlacedItems(RectTransform targetSpace)
    {
        if (targetSpace == null || !TryGetZoneRectInTargetSpace(targetSpace, out Rect zoneRectInTargetSpace))
        {
            return;
        }

        var items = targetSpace.GetComponentsInChildren<DraggableItemController>(true);
        ReflowMailChara(items, zoneRectInTargetSpace);
        ReflowCategory(items, PlacementCategory.Editor, zoneRectInTargetSpace);
        ReflowCategory(items, PlacementCategory.Stream, zoneRectInTargetSpace);
        ReflowCategory(items, PlacementCategory.Movie, zoneRectInTargetSpace);
        ApplyVisualOrderLeftFront(items);
    }

    private bool TryGetZoneRectInTargetSpace(RectTransform targetSpace, out Rect rectInTargetSpace)
    {
        rectInTargetSpace = default;
        if (targetSpace == null)
        {
            return false;
        }

        RectTransform source = zoneRect != null ? zoneRect : GetComponent<RectTransform>();
        if (source == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        Vector3 local0 = targetSpace.InverseTransformPoint(corners[0]);
        Vector3 local1 = targetSpace.InverseTransformPoint(corners[1]);
        Vector3 local2 = targetSpace.InverseTransformPoint(corners[2]);
        Vector3 local3 = targetSpace.InverseTransformPoint(corners[3]);

        float minX = Mathf.Min(local0.x, local1.x, local2.x, local3.x);
        float maxX = Mathf.Max(local0.x, local1.x, local2.x, local3.x);
        float minY = Mathf.Min(local0.y, local1.y, local2.y, local3.y);
        float maxY = Mathf.Max(local0.y, local1.y, local2.y, local3.y);

        if (maxX - minX <= 0.01f || maxY - minY <= 0.01f)
        {
            return false;
        }

        rectInTargetSpace = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private void ReflowMailChara(DraggableItemController[] items, Rect zoneRectInTargetSpace)
    {
        for (int i = 0; i < items.Length; i++)
        {
            DraggableItemController item = items[i];
            if (!CanParticipateInLayout(item) || ResolveCategory(item) != PlacementCategory.MailChara)
            {
                continue;
            }

            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect == null)
            {
                continue;
            }

            Vector2 anchored = itemRect.anchoredPosition;
            anchored.x = zoneRectInTargetSpace.xMin;
            anchored.y = Mathf.Clamp(anchored.y, zoneRectInTargetSpace.yMin, zoneRectInTargetSpace.yMax);
            itemRect.anchoredPosition = anchored;
            break;
        }
    }

    private void ReflowCategory(DraggableItemController[] items, PlacementCategory category, Rect zoneRectInTargetSpace)
    {
        if (!TryGetBandRange(category, zoneRectInTargetSpace, out float bandMinX, out float bandMaxX))
        {
            return;
        }

        var groupedItems = new List<DraggableItemController>();
        for (int i = 0; i < items.Length; i++)
        {
            DraggableItemController item = items[i];
            if (!CanParticipateInLayout(item) || ResolveCategory(item) != category)
            {
                continue;
            }

            groupedItems.Add(item);
        }

        int count = groupedItems.Count;
        if (count <= 0)
        {
            return;
        }

        groupedItems.Sort((a, b) =>
        {
            RectTransform aRect = a != null ? a.GetComponent<RectTransform>() : null;
            RectTransform bRect = b != null ? b.GetComponent<RectTransform>() : null;
            if (aRect == null && bRect == null)
            {
                return 0;
            }

            if (aRect == null)
            {
                return 1;
            }

            if (bRect == null)
            {
                return -1;
            }

            float ax = aRect.anchoredPosition.x;
            float bx = bRect.anchoredPosition.x;
            if (Mathf.Approximately(ax, bx))
            {
                return aRect.GetSiblingIndex().CompareTo(bRect.GetSiblingIndex());
            }

            return ax.CompareTo(bx);
        });

        for (int i = 0; i < count; i++)
        {
            RectTransform itemRect = groupedItems[i].GetComponent<RectTransform>();
            if (itemRect == null)
            {
                continue;
            }

            float x = ComputeInteriorEvenlySpacedX(i, count, bandMinX, bandMaxX);
            Vector2 anchored = itemRect.anchoredPosition;
            anchored.x = x;
            anchored.y = Mathf.Clamp(anchored.y, zoneRectInTargetSpace.yMin, zoneRectInTargetSpace.yMax);
            itemRect.anchoredPosition = anchored;
        }
    }

    private void CollectItemsInCategory(
        RectTransform targetSpace,
        PlacementCategory category,
        List<DraggableItemController> outItems)
    {
        outItems.Clear();
        if (targetSpace == null)
        {
            return;
        }

        var items = targetSpace.GetComponentsInChildren<DraggableItemController>(true);
        for (int i = 0; i < items.Length; i++)
        {
            DraggableItemController item = items[i];
            if (!CanParticipateInLayout(item) || ResolveCategory(item) != category)
            {
                continue;
            }

            outItems.Add(item);
        }
    }

    private bool TryGetBandRange(PlacementCategory category, Rect zoneRectInTargetSpace, out float minX, out float maxX)
    {
        float zoneLeft = zoneRectInTargetSpace.xMin;
        float zoneRight = zoneRectInTargetSpace.xMax;
        minX = zoneLeft;
        maxX = zoneRight;

        float safeEditorStart = Mathf.Clamp(zoneLeft + itemEditorStartPixel, zoneLeft, zoneRight);
        float safeStreamStart = Mathf.Clamp(zoneLeft + itemStreamStartPixel, zoneLeft, zoneRight);
        float safeMovieStart = Mathf.Clamp(zoneLeft + itemMovieStartPixel, zoneLeft, zoneRight);
        float gap = Mathf.Max(0f, areaGapPixels);

        switch (category)
        {
            case PlacementCategory.Editor:
                minX = Mathf.Min(safeEditorStart + gap, safeStreamStart - gap);
                maxX = Mathf.Max(safeEditorStart + gap, safeStreamStart - gap);
                return true;
            case PlacementCategory.Stream:
                minX = Mathf.Min(safeStreamStart + gap, safeMovieStart - gap);
                maxX = Mathf.Max(safeStreamStart + gap, safeMovieStart - gap);
                return true;
            case PlacementCategory.Movie:
                minX = Mathf.Min(safeMovieStart + gap, zoneRight);
                maxX = Mathf.Max(safeMovieStart + gap, zoneRight);
                return true;
            default:
                return false;
        }
    }

    private static float ComputeInteriorEvenlySpacedX(int index, int count, float bandMinX, float bandMaxX)
    {
        if (count <= 0)
        {
            return (bandMinX + bandMaxX) * 0.5f;
        }

        int clampedIndex = Mathf.Clamp(index, 0, count - 1);
        float t = (clampedIndex + 1f) / (count + 1f);
        return Mathf.Lerp(bandMinX, bandMaxX, t);
    }

    private PlacementCategory ResolveCategory(DraggableItemController item)
    {
        if (item == null)
        {
            return PlacementCategory.Unknown;
        }

        if (item.ItemType == ItemType.ItemMailChara)
        {
            return PlacementCategory.MailChara;
        }

        if (item.ItemType == ItemType.ItemEditor01 ||
            item.ItemType == ItemType.ItemEditor02 ||
            item.ItemType == ItemType.ItemEditor03)
        {
            return PlacementCategory.Editor;
        }

        if (item.ItemType == ItemType.ItemStream)
        {
            return PlacementCategory.Stream;
        }

        if (item.ItemType == ItemType.ItemMovie)
        {
            return PlacementCategory.Movie;
        }

        string itemName = item.name;
        if (itemName.StartsWith("ItemEditor", System.StringComparison.Ordinal))
        {
            return PlacementCategory.Editor;
        }

        if (itemName.StartsWith("ItemStream_", System.StringComparison.Ordinal) || item.ItemType == ItemType.ItemStream)
        {
            return PlacementCategory.Stream;
        }

        if (itemName.StartsWith("ItemMovie_", System.StringComparison.Ordinal) || item.ItemType == ItemType.ItemMovie)
        {
            return PlacementCategory.Movie;
        }

        return PlacementCategory.Unknown;
    }

    private bool CanParticipateInLayout(DraggableItemController item)
    {
        return item != null &&
            item.gameObject.activeInHierarchy &&
            !item.IsDragging &&
            !item.IsBounceReturnActive &&
            !item.SuppressPlacementReflowForSpawnBounce;
    }

    private void ApplyVisualOrderLeftFront(DraggableItemController[] items)
    {
        var sortable = new List<RectTransform>();
        for (int i = 0; i < items.Length; i++)
        {
            DraggableItemController item = items[i];
            if (!CanParticipateInLayout(item) || ResolveCategory(item) == PlacementCategory.Unknown)
            {
                continue;
            }

            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect == null)
            {
                continue;
            }

            sortable.Add(itemRect);
        }

        sortable.Sort((a, b) =>
        {
            float ax = a.anchoredPosition.x;
            float bx = b.anchoredPosition.x;
            if (Mathf.Approximately(ax, bx))
            {
                return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
            }

            // 右 -> 左 の順で SetAsLastSibling すると左ほど手前になる。
            return bx.CompareTo(ax);
        });

        for (int i = 0; i < sortable.Count; i++)
        {
            sortable[i].SetAsLastSibling();
        }
    }

    private enum PlacementCategory
    {
        Unknown = 0,
        MailChara = 1,
        Editor = 2,
        Stream = 3,
        Movie = 4
    }
}
