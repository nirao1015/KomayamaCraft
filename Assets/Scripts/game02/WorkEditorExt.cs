using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;
using UnityEngine.UI;

/// <summary>
/// ItemEditor 系専用の仕事場。受け入れ後は Spline 上を周回移動し、ドラッグ取り出しにも対応する。
/// </summary>
public class WorkEditorExt : MonoBehaviour, IWorkplaceTarget, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly System.Collections.Generic.List<WorkEditorExt> ActiveInstances = new System.Collections.Generic.List<WorkEditorExt>();

    [Header("Visuals")]
    [SerializeField] private Image dropRaycastArea;
    [SerializeField] private Image acceptedItemView;
    [SerializeField] private AcceptedItemViewBounceController acceptedItemViewBounce;

    [Header("Path")]
    [SerializeField] private SplineContainer splineEditorWark;

    [Header("Motion")]
    [SerializeField] private float moveSpeed = 0.08f;
    [SerializeField] private float swingAngle = 5f;
    [SerializeField] private float swingSpeed = 2.8f;
    [SerializeField] private bool enableAcceptedItemBounce = false;

    [Header("WorkEditor Influence")]
    [SerializeField] private float workEditorBaseEditSecondsOffset = -5f;

    private bool hasEditorAssigned;
    private Sprite rememberedEditorSprite;
    private string rememberedEditorSpriteName;
    private string rememberedEditorDisplayName;
    private ItemType rememberedEditorType = ItemType.ItemEditor01;

    private DraggableItemController extractingItem;
    private ItemSpawnController cachedSpawnController;

    private SplinePath<Spline> cachedPath;
    private float cachedPathLength;
    private Spline activeSpline;
    private float pathTNormalized;
    private float acceptedBaseEulerZ;
    private bool acceptedBaseEulerCaptured;

    /// <summary>編集者配置後、実ゲーム時間のワークカウントが始まる最初のティックでメッセージ用。</summary>
    private bool pendingFirstWorkEditorExtWorkStartNotify;

    public static void EnsureSceneWorkEditorExtExists()
    {
        GameObject go = GameObject.Find("UnitCanvas/WorkEditorExt");
        if (go == null)
        {
            return;
        }

        if (go.GetComponent<WorkEditorExt>() == null)
        {
            go.AddComponent<WorkEditorExt>();
        }
    }

    public static float GetActiveWorkEditorBaseEditSecondsOffset()
    {
        float sum = 0f;
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            WorkEditorExt ext = ActiveInstances[i];
            if (ext == null || !ext.IsExternalModifierActive())
            {
                continue;
            }

            sum += ext.workEditorBaseEditSecondsOffset;
        }

        return sum;
    }

    public static long GetActiveWorkEditorPopularityBaseBonus()
    {
        if (!HasAnyActiveModifier())
        {
            return 0L;
        }

        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null)
        {
            return 0L;
        }

        return System.Math.Max(0L, gm.CurrentPopularity / 100L);
    }

    private void Awake()
    {
        EnsureDropRaycastArea();
        ResolveReferencesIfNeeded();
        ApplyAcceptedEditorVisual();
        cachedSpawnController = FindObjectOfType<ItemSpawnController>(true);
        RebuildSplineCache();
    }

    private void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
        {
            ActiveInstances.Add(this);
        }

        Spline.Changed += OnSplineChanged;
        Game02.SidePanelOpenCloseController.SidePanelClosed += OnSidePanelClosed;
        RebuildSplineCache();
        if (hasEditorAssigned)
        {
            RefreshAssignedEditorPresentation();
        }
    }

    private void OnDisable()
    {
        ActiveInstances.Remove(this);
        Spline.Changed -= OnSplineChanged;
        Game02.SidePanelOpenCloseController.SidePanelClosed -= OnSidePanelClosed;
        ResetAcceptedSwingRotation();
    }

    private void OnSidePanelClosed()
    {
        if (!hasEditorAssigned)
        {
            return;
        }

        RefreshAssignedEditorPresentation();
    }

    private void Update()
    {
        if (!hasEditorAssigned || acceptedItemView == null)
        {
            return;
        }

        if (acceptedItemView.sprite == null)
        {
            RefreshAssignedEditorPresentation();
            if (acceptedItemView.sprite == null)
            {
                return;
            }
        }

        if (IsBlockedByPauseOrFatal())
        {
            return;
        }

        if (cachedPath == null || cachedPathLength <= math.EPSILON)
        {
            RebuildSplineCache();
            if (cachedPath == null || cachedPathLength <= math.EPSILON)
            {
                return;
            }
        }

        float delta = ResolveGameplayDelta();
        if (delta <= 0f)
        {
            return;
        }

        if (pendingFirstWorkEditorExtWorkStartNotify)
        {
            pendingFirstWorkEditorExtWorkStartNotify = false;
            Game02.Game02MsgManager.TryGet()?.NotifyFirstWorkEditorExtAssigned(Game02.GameManager.Instance);
        }

        Game02.Game02AlienProgressTracker.EnsureExists()?.AddWorkEditorExtWorkSeconds(delta);

        pathTNormalized = Mathf.Repeat(pathTNormalized + Mathf.Max(0f, moveSpeed) * delta, 1f);
        SyncAcceptedViewToSplinePath();
        ApplySwingRotation();
    }

    public bool CanAcceptItem(DraggableItemController item)
    {
        if (item == null || hasEditorAssigned || IsBlockedByPauseOrFatal())
        {
            return false;
        }

        if (!HasValidSpline())
        {
            return false;
        }

        return IsEditorItem(item);
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return;
        }

        AcceptEditor(item);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanExtractEditorByDrag())
        {
            return;
        }

        if (!TrySpawnRememberedEditorForExtraction(eventData, out DraggableItemController spawned))
        {
            return;
        }

        extractingItem = spawned;
        ClearRememberedEditor();
        extractingItem.BeginDragFromExternal(eventData);
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.WorkEditorExtractGrab);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (extractingItem == null)
        {
            return;
        }

        extractingItem.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (extractingItem == null)
        {
            return;
        }

        DraggableItemController current = extractingItem;
        extractingItem = null;
        WorkplaceDragExtractionUtility.ReparentExtractedItemBackToItemCanvas(current, ref cachedSpawnController);
        if (eventData != null)
        {
            current.TrySnapAnchoredPositionToScreenPoint(eventData.position);
            current.RefreshBaseStateFromCurrentTransform();
        }

        current.OnEndDrag(eventData);
    }

    private void AcceptEditor(DraggableItemController item)
    {
        if (item == null || !item.TryGetDisplaySprite(out Sprite acceptedSprite))
        {
            return;
        }

        rememberedEditorSprite = acceptedSprite;
        rememberedEditorSpriteName = acceptedSprite != null ? acceptedSprite.name : string.Empty;
        rememberedEditorDisplayName = item.name;
        rememberedEditorType = ResolveRememberedEditorType(item);
        hasEditorAssigned = true;
        pendingFirstWorkEditorExtWorkStartNotify = true;
        pathTNormalized = 0f;
        RefreshAssignedEditorPresentation();
    }

    private void ClearRememberedEditor()
    {
        hasEditorAssigned = false;
        pendingFirstWorkEditorExtWorkStartNotify = false;
        rememberedEditorSprite = null;
        rememberedEditorSpriteName = string.Empty;
        rememberedEditorDisplayName = string.Empty;
        rememberedEditorType = ItemType.ItemEditor01;
        pathTNormalized = 0f;
        ApplyAcceptedEditorVisual();
        ResetAcceptedSwingRotation();
    }

    public Game02.WorkEditorExtState CaptureSaveState()
    {
        return new Game02.WorkEditorExtState
        {
            objectName = name,
            hasEditorAssigned = hasEditorAssigned,
            rememberedEditorType = (int)rememberedEditorType,
            rememberedEditorDisplayName = rememberedEditorDisplayName ?? string.Empty,
                rememberedEditorSpriteName = !string.IsNullOrEmpty(rememberedEditorSpriteName)
                    ? rememberedEditorSpriteName
                    : rememberedEditorSprite != null ? rememberedEditorSprite.name : string.Empty
        };
    }

    public void ApplySaveState(Game02.WorkEditorExtState state)
    {
        if (state == null)
        {
            return;
        }

        rememberedEditorType = System.Enum.IsDefined(typeof(ItemType), state.rememberedEditorType)
            ? (ItemType)state.rememberedEditorType
            : ItemType.ItemEditor01;
        rememberedEditorDisplayName = state.rememberedEditorDisplayName ?? string.Empty;
        rememberedEditorSpriteName = state.rememberedEditorSpriteName ?? string.Empty;
        rememberedEditorSprite = Game02.Game02SpriteResolver.ResolveByName(rememberedEditorSpriteName);
        hasEditorAssigned = state.hasEditorAssigned;
        pendingFirstWorkEditorExtWorkStartNotify = false;

        // 仕事再開時はロード前の位置を復元せず、先頭から開始する。
        pathTNormalized = 0f;
        RefreshAssignedEditorPresentation();
        ResetAcceptedSwingRotation();
    }

    private bool TrySpawnRememberedEditorForExtraction(PointerEventData eventData, out DraggableItemController spawned)
    {
        spawned = null;
        cachedSpawnController = cachedSpawnController != null ? cachedSpawnController : FindObjectOfType<ItemSpawnController>(true);
        if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
        {
            return false;
        }

        ItemType spawnType = rememberedEditorType;
        if (!cachedSpawnController.TrySpawnItem(spawnType, Vector2.zero, false, out spawned, null, true))
        {
            return false;
        }

        if (spawned == null)
        {
            return false;
        }

        if (rememberedEditorSprite != null)
        {
            spawned.ApplyDisplaySprite(rememberedEditorSprite);
        }

        spawned.SuppressNextWorkplaceDropFor(this);
        if (!string.IsNullOrEmpty(rememberedEditorDisplayName))
        {
            spawned.name = rememberedEditorDisplayName;
        }

        if (!spawned.TrySnapAnchoredPositionToScreenPoint(eventData.position))
        {
            RectTransform itemCanvas = cachedSpawnController.ItemCanvas;
            RectTransform rt = spawned.GetComponent<RectTransform>();
            if (itemCanvas != null && rt != null && TryGetSpawnPointCenterSelfInItemCanvas(itemCanvas, out Vector2 fallback))
            {
                rt.anchoredPosition = fallback;
                spawned.RefreshBaseStateFromCurrentTransform();
            }
        }

        WorkplaceDragExtractionUtility.ReparentExtractedItemToWorkCanvas(spawned, transform);
        return true;
    }

    private bool TryGetSpawnPointCenterSelfInItemCanvas(RectTransform itemCanvasRect, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        RectTransform selfRect = transform as RectTransform;
        if (selfRect == null)
        {
            return false;
        }

        Vector3 worldCenter = selfRect.TransformPoint(selfRect.rect.center);
        Canvas canvas = itemCanvasRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(itemCanvasRect, screenPoint, cam, out anchoredPosition))
        {
            return true;
        }

        anchoredPosition = itemCanvasRect.InverseTransformPoint(worldCenter);
        return true;
    }

    private static bool IsEditorItem(DraggableItemController item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.ItemType == ItemType.ItemEditor01 || item.ItemType == ItemType.ItemEditor02 || item.ItemType == ItemType.ItemEditor03)
        {
            return true;
        }

        return item.name.StartsWith("ItemEditor", System.StringComparison.Ordinal);
    }

    private static ItemType ResolveRememberedEditorType(DraggableItemController item)
    {
        if (item == null)
        {
            return ItemType.ItemEditor01;
        }

        if (item.ItemType == ItemType.ItemEditor02 || item.ItemType == ItemType.ItemEditor03)
        {
            return item.ItemType;
        }

        string n = item.name ?? string.Empty;
        if (n.Contains("ItemEditor03"))
        {
            return ItemType.ItemEditor03;
        }

        if (n.Contains("ItemEditor02"))
        {
            return ItemType.ItemEditor02;
        }

        return ItemType.ItemEditor01;
    }

    private bool CanExtractEditorByDrag()
    {
        if (!hasEditorAssigned)
        {
            return false;
        }

        return !IsBlockedByPauseOrFatal();
    }

    private bool IsBlockedByPauseOrFatal()
    {
        if (Game02.GameManager.Instance == null)
        {
            return false;
        }

        return Game02.GameManager.Instance.ShouldSuppressPlayerInteractions || Game02.GameManager.Instance.HasFatalError;
    }

    private bool HasValidSpline()
    {
        if (splineEditorWark == null)
        {
            return false;
        }

        if (splineEditorWark.Splines == null || splineEditorWark.Splines.Count == 0)
        {
            return false;
        }

        return true;
    }

    private bool IsExternalModifierActive()
    {
        return hasEditorAssigned && isActiveAndEnabled;
    }

    private static bool HasAnyActiveModifier()
    {
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            WorkEditorExt ext = ActiveInstances[i];
            if (ext != null && ext.IsExternalModifierActive())
            {
                return true;
            }
        }

        return false;
    }

    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
    {
        if (splineEditorWark == null || splineEditorWark.Splines == null)
        {
            return;
        }

        for (int i = 0; i < splineEditorWark.Splines.Count; i++)
        {
            if (splineEditorWark.Splines[i] == spline)
            {
                RebuildSplineCache();
                break;
            }
        }
    }

    private void RebuildSplineCache()
    {
        cachedPath = null;
        cachedPathLength = 0f;
        activeSpline = null;
        if (!HasValidSpline())
        {
            return;
        }

        activeSpline = splineEditorWark.Splines[0];
        cachedPath = new SplinePath<Spline>(splineEditorWark.Splines);
        cachedPathLength = cachedPath.GetLength();
    }

    private void RefreshAssignedEditorPresentation()
    {
        ResolveReferencesIfNeeded();
        ApplyAcceptedEditorVisual();
        if (hasEditorAssigned)
        {
            SyncAcceptedViewToSplinePath();
        }
    }

    private void ApplyAcceptedEditorVisual()
    {
        if (acceptedItemView == null)
        {
            return;
        }

        if (hasEditorAssigned && rememberedEditorSprite == null && !string.IsNullOrEmpty(rememberedEditorSpriteName))
        {
            rememberedEditorSprite = Game02.Game02SpriteResolver.ResolveByName(rememberedEditorSpriteName);
        }

        acceptedItemView.sprite = hasEditorAssigned ? rememberedEditorSprite : null;
        bool visible = hasEditorAssigned && rememberedEditorSprite != null;
        acceptedItemView.enabled = visible;
        if (visible && !acceptedItemView.gameObject.activeSelf)
        {
            acceptedItemView.gameObject.SetActive(true);
        }

        if (acceptedItemViewBounce != null)
        {
            acceptedItemViewBounce.SetCarrierActive(enableAcceptedItemBounce && hasEditorAssigned);
        }
    }

    private void SyncAcceptedViewToSplinePath()
    {
        if (!hasEditorAssigned || acceptedItemView == null || splineEditorWark == null)
        {
            return;
        }

        if (cachedPath == null || cachedPathLength <= math.EPSILON || activeSpline == null)
        {
            RebuildSplineCache();
            if (cachedPath == null || cachedPathLength <= math.EPSILON || activeSpline == null)
            {
                return;
            }
        }

        float3 splineLocal = activeSpline.EvaluatePosition(pathTNormalized);
        Vector3 world = splineEditorWark.transform.TransformPoint(new Vector3(splineLocal.x, splineLocal.y, splineLocal.z));
        ApplyAcceptedViewWorldPosition(world);
    }

    private void ApplyAcceptedViewWorldPosition(Vector3 worldPosition)
    {
        if (acceptedItemView == null)
        {
            return;
        }

        RectTransform rt = acceptedItemView.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;
        if (parentRt == null)
        {
            rt.position = worldPosition;
            return;
        }

        Canvas canvas = acceptedItemView.canvas;
        if (canvas == null)
        {
            canvas = parentRt.GetComponentInParent<Canvas>();
        }

        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPoint, cam, out Vector2 localPoint))
        {
            rt.anchoredPosition = localPoint;
            return;
        }

        Vector3 local = parentRt.InverseTransformPoint(worldPosition);
        rt.anchoredPosition3D = local;
    }

    private void ApplySwingRotation()
    {
        if (acceptedItemView == null)
        {
            return;
        }

        CaptureAcceptedBaseEulerIfNeeded();
        RectTransform rt = acceptedItemView.rectTransform;
        float angle = Mathf.Sin(Time.unscaledTime * Mathf.Max(0f, swingSpeed) * Mathf.PI * 2f) * swingAngle;
        rt.localEulerAngles = new Vector3(0f, 0f, acceptedBaseEulerZ + angle);
    }

    private void ResetAcceptedSwingRotation()
    {
        if (acceptedItemView == null)
        {
            return;
        }

        CaptureAcceptedBaseEulerIfNeeded();
        RectTransform rt = acceptedItemView.rectTransform;
        rt.localEulerAngles = new Vector3(0f, 0f, acceptedBaseEulerZ);
    }

    private void CaptureAcceptedBaseEulerIfNeeded()
    {
        if (acceptedBaseEulerCaptured || acceptedItemView == null)
        {
            return;
        }

        acceptedBaseEulerZ = acceptedItemView.rectTransform.localEulerAngles.z;
        acceptedBaseEulerCaptured = true;
    }

    private float ResolveGameplayDelta()
    {
        // WorkEditorExt の移動は実時間ベースで進める。
        // Pause/Fatal は別途 IsBlockedByPauseOrFatal() で停止済み。
        return Mathf.Max(0f, Time.unscaledDeltaTime);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (acceptedItemView == null)
        {
            acceptedItemView = FindComponentInDescendantsByName<Image>("AcceptedItemView");
        }

        if (acceptedItemViewBounce == null && acceptedItemView != null)
        {
            acceptedItemViewBounce = acceptedItemView.GetComponent<AcceptedItemViewBounceController>();
            if (acceptedItemViewBounce == null)
            {
                acceptedItemViewBounce = acceptedItemView.gameObject.AddComponent<AcceptedItemViewBounceController>();
            }
        }

        if (splineEditorWark == null)
        {
            splineEditorWark = FindComponentInDescendantsByName<SplineContainer>("SplineEditorWark");
        }
    }

    private T FindComponentInDescendantsByName<T>(string childName) where T : Component
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, childName, System.StringComparison.Ordinal))
            {
                continue;
            }

            T component = t.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private void EnsureDropRaycastArea()
    {
        if (dropRaycastArea == null)
        {
            dropRaycastArea = GetComponent<Image>();
        }

        if (dropRaycastArea == null)
        {
            dropRaycastArea = gameObject.AddComponent<Image>();
        }

        if (dropRaycastArea == null)
        {
            return;
        }

        dropRaycastArea.enabled = true;
        dropRaycastArea.raycastTarget = true;
        Color c = dropRaycastArea.color;
        c.a = 0f;
        dropRaycastArea.color = c;
    }
}

