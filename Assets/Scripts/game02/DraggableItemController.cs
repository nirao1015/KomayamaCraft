using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableItemController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Identity")]
    [SerializeField] private ItemType itemType = ItemType.Unknown;
    [SerializeField] private float editorEditSpeed = 1f;
    [SerializeField] private float editorPopularityMultiplier = 1f;
    [SerializeField] private long editorBuzzBaseValue = 2L;

    [Header("View")]
    [SerializeField] private Image itemImage;
    [SerializeField] private Sprite displaySprite;

    [Header("Bounce")]
    [SerializeField] private float bounceMoveSeconds = 0.35f;
    [SerializeField] private float bounceReturnSeconds = 0.12f;
    [SerializeField] private float bounceOvershootDistance = 28f;

    [Header("Hover Presentation")]
    [SerializeField] private bool enableHoverPresentation = true;
    [SerializeField] private float hoverScaleMultiplier = 1.08f;
    [SerializeField] private float hoverLiftPixels = 20f;
    [SerializeField] private float hoverTransitionSeconds = 0.12f;
    [SerializeField] private float hoverOrbitRadiusPixels = 8f;
    [SerializeField] private float hoverOrbitSpeedDegPerSec = 48f;
    [SerializeField] private bool hoverOrbitClockwise = true;

    private RectTransform rectTransform;
    private RectTransform itemCanvasRect;
    private Canvas rootCanvas;
    private ItemSpawnController itemSpawnController;
    private Canvas mailDragOverlayCanvas;
    private bool mailDragCanvasAddedAtRuntime;
    private bool isMailDragOverlayActive;
    private int mailDragOriginalSortingOrder;
    private bool mailDragOriginalOverrideSorting;
    private bool isDragging;
    private bool isReturning;
    private bool isInputLocked;
    private bool suppressPlacementReflowForSpawnBounce;
    private bool suppressNextWorkplaceDrop;
    private IWorkplaceTarget suppressNextWorkplaceDropOnlyFor;
    private Vector2 dragOffset;
    private Vector2 baseAnchoredPosition;
    private Vector3 baseLocalScale;
    private Vector2 hoverCenter;
    private float hoverOrbitAngleDeg;
    private float hoverTransitionElapsed;
    private Vector2 hoverTransitionFromPosition;
    private Vector2 hoverTransitionToPosition;
    private Vector3 hoverTransitionFromScale;
    private Vector3 hoverTransitionToScale;
    private HoverVisualState hoverVisualState;

    private static DraggableItemController activeHoveredItem;

    public ItemType ItemType => itemType;
    public float EditorEditSpeed => editorEditSpeed;
    public float EditorPopularityMultiplier => editorPopularityMultiplier;
    public long EditorBuzzBaseValue => editorBuzzBaseValue;
    public bool IsDragging => isDragging;
    public bool IsInputLocked => isInputLocked;

    /// <summary>
    /// 非受け入れドロップ後の跳ね戻り移動中。自動レイアウト（Reflow）はこの間スキップする。
    /// </summary>
    public bool IsBounceReturnActive => isReturning;

    /// <summary>
    /// スポーン直後に跳ね戻りを1フレーム遅延するとき、それまで Reflow 対象にしない。
    /// </summary>
    public bool SuppressPlacementReflowForSpawnBounce => suppressPlacementReflowForSpawnBounce;

    public void MarkSuppressPlacementReflowForSpawnBounce()
    {
        suppressPlacementReflowForSpawnBounce = true;
    }

    public void SetItemType(ItemType type)
    {
        itemType = type;
    }

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (itemImage == null)
        {
            itemImage = GetComponent<Image>();
        }

        if (itemImage != null && displaySprite != null)
        {
            itemImage.sprite = displaySprite;
        }

        itemCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
        if (rectTransform != null)
        {
            baseAnchoredPosition = rectTransform.anchoredPosition;
            baseLocalScale = rectTransform.localScale;
        }
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (IsPaused())
        {
            ForceStopHoverPresentation();
            return;
        }

        if (!isDragging && !isReturning && !suppressPlacementReflowForSpawnBounce &&
            hoverVisualState == HoverVisualState.None)
        {
            baseAnchoredPosition = rectTransform.anchoredPosition;
            baseLocalScale = rectTransform.localScale;
        }

        UpdateHoverPresentation(Game02.GameManager.GameplayDelta);
    }

    private void OnDisable()
    {
        suppressPlacementReflowForSpawnBounce = false;
        TrashDropTarget.ClearDragHover();
        RestoreMailCharaParentAfterDrag();
        if (activeHoveredItem == this)
        {
            activeHoveredItem = null;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanHandleInput() || eventData == null || rectTransform == null || itemCanvasRect == null)
        {
            return;
        }

        if (!IsTopMostDraggableAtPointer(eventData))
        {
            return;
        }

        EndHoverPresentationImmediate();
        isDragging = true;
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.ItemGrab);
        itemSpawnController?.NotifyItemDragBegan(this);
        TryLiftMailCharaToSidePanelForeground();
        rectTransform.SetAsLastSibling();
        if (TryScreenToCanvasPoint(eventData.position, out Vector2 canvasPoint))
        {
            dragOffset = rectTransform.anchoredPosition - canvasPoint;
        }
        else
        {
            dragOffset = Vector2.zero;
        }

        TrashDropTarget.UpdateDragHover(eventData, this);
    }

    public void BeginDragFromExternal(PointerEventData eventData)
    {
        if (!CanHandleInput() || eventData == null || rectTransform == null || itemCanvasRect == null)
        {
            return;
        }

        EndHoverPresentationImmediate();
        isDragging = true;
        itemSpawnController?.NotifyItemDragBegan(this);
        TryLiftMailCharaToSidePanelForeground();
        rectTransform.SetAsLastSibling();
        if (TryScreenToCanvasPoint(eventData.position, out Vector2 canvasPoint))
        {
            dragOffset = rectTransform.anchoredPosition - canvasPoint;
        }
        else
        {
            dragOffset = Vector2.zero;
        }

        TrashDropTarget.UpdateDragHover(eventData, this);
    }

    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;
        if (locked)
        {
            EndHoverPresentationImmediate();
        }
    }

    public void SuppressNextWorkplaceDrop()
    {
        suppressNextWorkplaceDrop = true;
        suppressNextWorkplaceDropOnlyFor = null;
    }

    public void SuppressNextWorkplaceDropFor(IWorkplaceTarget workplace)
    {
        if (workplace == null)
        {
            SuppressNextWorkplaceDrop();
            return;
        }

        suppressNextWorkplaceDrop = true;
        suppressNextWorkplaceDropOnlyFor = workplace;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData == null || rectTransform == null)
        {
            return;
        }

        if (!TryScreenToCanvasPoint(eventData.position, out Vector2 canvasPoint))
        {
            return;
        }

        rectTransform.anchoredPosition = canvasPoint + dragOffset;
        TrashDropTarget.UpdateDragHover(eventData, this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || rectTransform == null)
        {
            return;
        }

        RefreshCanvasReferencesFromHierarchy();
        isDragging = false;
        TrashDropTarget.ClearDragHover();
        RestoreMailCharaParentAfterDrag();
        if (TrashDropTarget.TryDropFromPointer(eventData, this))
        {
            Destroy(gameObject);
            return;
        }

        if (TryDropOnWorkplace(eventData))
        {
            Destroy(gameObject);
            return;
        }

        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.ItemInvalidDropRelease);
        StartCoroutine(BounceBackToPlacement(rectTransform.anchoredPosition));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableHoverPresentation || eventData == null)
        {
            return;
        }

        if (!CanStartHoverPresentation(eventData))
        {
            return;
        }

        if (activeHoveredItem != null && activeHoveredItem != this)
        {
            activeHoveredItem.EndHoverPresentationImmediate();
        }

        activeHoveredItem = this;
        BeginHoverPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activeHoveredItem == this)
        {
            activeHoveredItem = null;
        }

        EndHoverPresentationWithTransition();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!enableHoverPresentation || eventData == null)
        {
            return;
        }

        // OnPointerEnter が取りこぼされたケースの保険として、
        // Move 発火時にもホバー開始判定を行う。
        if (activeHoveredItem == this || hoverVisualState != HoverVisualState.None)
        {
            return;
        }

        if (!CanStartHoverPresentation(eventData))
        {
            return;
        }

        if (activeHoveredItem != null && activeHoveredItem != this)
        {
            activeHoveredItem.EndHoverPresentationImmediate();
        }

        activeHoveredItem = this;
        BeginHoverPresentation();
    }

    public bool TryGetDisplaySprite(out Sprite sprite)
    {
        sprite = null;
        if (itemImage == null)
        {
            itemImage = GetComponent<Image>();
        }

        if (itemImage != null && itemImage.sprite != null)
        {
            sprite = itemImage.sprite;
            return true;
        }

        if (displaySprite != null)
        {
            sprite = displaySprite;
            return true;
        }

        return false;
    }

    public void ApplyDisplaySprite(Sprite sprite)
    {
        if (itemImage == null)
        {
            itemImage = GetComponent<Image>();
        }

        if (itemImage != null)
        {
            itemImage.sprite = sprite;
        }

        displaySprite = sprite;
    }

    /// <summary>
    /// スクリーン座標を <see cref="OnBeginDrag"/> / <see cref="OnDrag"/> と同じ方法で親キャンバスへ変換し、
    /// <see cref="RectTransform.anchoredPosition"/> をその点に合わせる（取り出しスポーン直後の位置合わせ用）。
    /// </summary>
    public bool TrySnapAnchoredPositionToScreenPoint(Vector2 screenPoint)
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (itemCanvasRect == null || rootCanvas == null)
        {
            itemCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rectTransform == null || itemCanvasRect == null)
        {
            return false;
        }

        if (!TryScreenToCanvasPoint(screenPoint, out Vector2 canvasPoint))
        {
            return false;
        }

        rectTransform.anchoredPosition = canvasPoint;
        baseAnchoredPosition = canvasPoint;
        baseLocalScale = rectTransform.localScale;
        return true;
    }

    public void RefreshBaseStateFromCurrentTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (rectTransform == null)
        {
            return;
        }

        baseAnchoredPosition = rectTransform.anchoredPosition;
        baseLocalScale = rectTransform.localScale;
    }

    /// <summary>
    /// 親キャンバスを付け替えた直後に、<see cref="OnDrag"/> 用の Canvas / Rect 参照を再解決する。
    /// </summary>
    public void RefreshCanvasReferencesFromHierarchy()
    {
        itemCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void TriggerBounceBackFromCurrentPosition()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (rectTransform == null || isReturning)
        {
            return;
        }

        EndHoverPresentationImmediate();
        isDragging = false;
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.ItemInvalidDropRelease);
        StartCoroutine(BounceBackToPlacement(rectTransform.anchoredPosition));
    }

    private bool TryDropOnWorkplace(PointerEventData eventData)
    {
        if (IsPaused() || eventData == null || EventSystem.current == null)
        {
            return false;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (go.GetComponentInParent<DraggableItemController>() == this)
            {
                continue;
            }

            IWorkplaceTarget workplace = go.GetComponentInParent<IWorkplaceTarget>();
            if (workplace != null)
            {
                if (suppressNextWorkplaceDrop)
                {
                    if (suppressNextWorkplaceDropOnlyFor == null)
                    {
                        suppressNextWorkplaceDrop = false;
                        return false;
                    }

                    if (ReferenceEquals(workplace, suppressNextWorkplaceDropOnlyFor))
                    {
                        suppressNextWorkplaceDrop = false;
                        suppressNextWorkplaceDropOnlyFor = null;
                        continue;
                    }
                }

                if (workplace.CanAcceptItem(this))
                {
                    suppressNextWorkplaceDrop = false;
                    suppressNextWorkplaceDropOnlyFor = null;
                    workplace.OnItemDropped(this);
                    return true;
                }

                continue;
            }

            // ブロッカーは「そのレイヤで手前」と判定された 1 件だけで打ち切らない。
            // 背面に仕事場（IWorkplaceTarget）がある場合は続きの Raycast 結果を見る。
            if (Game02.SidePanelPointerInputBlocker.IsBlockingRaycastHit(go))
            {
                continue;
            }
        }

        suppressNextWorkplaceDrop = false;
        suppressNextWorkplaceDropOnlyFor = null;
        return false;
    }

    private IEnumerator BounceBackToPlacement(Vector2 releasePoint)
    {
        if (rectTransform == null || itemCanvasRect == null)
        {
            suppressPlacementReflowForSpawnBounce = false;
            yield break;
        }

        itemSpawnController = itemSpawnController != null
            ? itemSpawnController
            : FindObjectOfType<ItemSpawnController>(true);

        if (itemSpawnController == null ||
            !itemSpawnController.TryGetPlacementPointNear(releasePoint, this, out Vector2 targetPoint))
        {
            isReturning = false;
            suppressPlacementReflowForSpawnBounce = false;
            itemSpawnController?.NotifyPlacementLayoutNeedsRefresh();
            yield break;
        }

        // 取り出し直後などで座標系差により「開始点と同一点」が返る場合、ゾーン内の代替点へ補正する。
        if ((targetPoint - releasePoint).sqrMagnitude <= 1f)
        {
            if (TryGetFallbackPlacementPoint(releasePoint, out Vector2 fallbackPoint))
            {
                targetPoint = fallbackPoint;
            }
        }

        // 離した座標から跳ね戻る。EndHover が baseAnchored（ドラッグ開始時）へ戻すと距離ゼロになり一瞬に見えるため、必ず release を起点にする。
        Vector2 startPoint = releasePoint;
        isReturning = true;
        suppressPlacementReflowForSpawnBounce = false;
        EndHoverPresentationImmediate();
        rectTransform.anchoredPosition = startPoint;
        PlayBounceSe();
        Vector2 direction = (targetPoint - startPoint).sqrMagnitude > 0.0001f
            ? (targetPoint - startPoint).normalized
            : Vector2.up;
        Vector2 overshootPoint = targetPoint + direction * Mathf.Max(0f, bounceOvershootDistance);

        yield return MoveAnchoredLinear(startPoint, overshootPoint, Mathf.Max(0.01f, bounceMoveSeconds));
        yield return MoveAnchoredLinear(overshootPoint, targetPoint, Mathf.Max(0.01f, bounceReturnSeconds));

        isReturning = false;
        itemSpawnController?.NotifyPlacementLayoutNeedsRefresh();
    }

    private bool TryGetFallbackPlacementPoint(Vector2 releasePoint, out Vector2 fallbackPoint)
    {
        fallbackPoint = releasePoint;
        if (itemSpawnController == null || itemCanvasRect == null)
        {
            return false;
        }

        ItemPlacementZone zone = itemSpawnController.PlacementZone;
        if (zone == null)
        {
            return false;
        }

        if (zone.TryGetRandomPointNear(itemCanvasRect, releasePoint, 240f, out Vector2 nearPoint))
        {
            fallbackPoint = nearPoint;
            return true;
        }

        return zone.TryGetRandomPoint(itemCanvasRect, out fallbackPoint);
    }

    private IEnumerator MoveAnchoredLinear(Vector2 from, Vector2 to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (IsPaused())
            {
                yield return null;
                continue;
            }

            // 反発移動は UI 補間なので、ゲーム進行タイマー未開始時でも確実に完了させる。
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
    }

    private bool TryScreenToCanvasPoint(Vector2 screenPoint, out Vector2 canvasPoint)
    {
        Camera eventCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            itemCanvasRect,
            screenPoint,
            eventCamera,
            out canvasPoint);
    }

    private bool CanHandleInput()
    {
        return !isInputLocked && !isReturning && !suppressPlacementReflowForSpawnBounce && !IsPaused();
    }

    private void TryLiftMailCharaToSidePanelForeground()
    {
        if (itemType != ItemType.ItemMailChara)
        {
            return;
        }

        Game02.SidePanelOpenCloseController sidePanel = FindObjectOfType<Game02.SidePanelOpenCloseController>(true);
        if (sidePanel == null || !sidePanel.IsOpen)
        {
            return;
        }

        mailDragOverlayCanvas = GetComponent<Canvas>();
        if (mailDragOverlayCanvas == null)
        {
            mailDragOverlayCanvas = gameObject.AddComponent<Canvas>();
            mailDragCanvasAddedAtRuntime = true;
        }
        else
        {
            mailDragCanvasAddedAtRuntime = false;
        }

        mailDragOriginalSortingOrder = mailDragOverlayCanvas.sortingOrder;
        mailDragOriginalOverrideSorting = mailDragOverlayCanvas.overrideSorting;
        mailDragOverlayCanvas.overrideSorting = true;
        mailDragOverlayCanvas.sortingOrder = sidePanel.DragForegroundSortingOrder;
        isMailDragOverlayActive = true;
    }

    private void RestoreMailCharaParentAfterDrag()
    {
        if (!isMailDragOverlayActive)
        {
            return;
        }

        if (mailDragOverlayCanvas != null)
        {
            mailDragOverlayCanvas.overrideSorting = mailDragOriginalOverrideSorting;
            mailDragOverlayCanvas.sortingOrder = mailDragOriginalSortingOrder;
        }

        if (mailDragCanvasAddedAtRuntime && mailDragOverlayCanvas != null)
        {
            Destroy(mailDragOverlayCanvas);
        }

        mailDragOverlayCanvas = null;
        mailDragCanvasAddedAtRuntime = false;
        isMailDragOverlayActive = false;
    }

    private bool CanStartHoverPresentation(PointerEventData eventData)
    {
        if (isInputLocked || isDragging || isReturning || suppressPlacementReflowForSpawnBounce || IsPaused())
        {
            return false;
        }

        itemSpawnController = itemSpawnController != null
            ? itemSpawnController
            : FindObjectOfType<ItemSpawnController>(true);
        if (itemSpawnController == null || !itemSpawnController.CanPlayPlacementZoneHover(this))
        {
            return false;
        }

        return IsTopMostDraggableAtPointer(eventData);
    }

    private bool IsTopMostDraggableAtPointer(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null)
        {
            return false;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (Game02.SidePanelPointerInputBlocker.IsBlockingRaycastHit(go))
            {
                return false;
            }

            DraggableItemController topItem = go.GetComponentInParent<DraggableItemController>();
            if (topItem == null)
            {
                continue;
            }

            return topItem == this;
        }

        return false;
    }

    private void BeginHoverPresentation()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.SetAsLastSibling();
        baseAnchoredPosition = rectTransform.anchoredPosition;
        baseLocalScale = rectTransform.localScale;
        hoverCenter = baseAnchoredPosition + new Vector2(0f, hoverLiftPixels);
        hoverOrbitAngleDeg = 0f;

        hoverTransitionFromPosition = rectTransform.anchoredPosition;
        hoverTransitionToPosition = hoverCenter;
        hoverTransitionFromScale = rectTransform.localScale;
        hoverTransitionToScale = baseLocalScale * Mathf.Max(0.01f, hoverScaleMultiplier);
        hoverTransitionElapsed = 0f;
        hoverVisualState = HoverVisualState.Entering;
    }

    private void EndHoverPresentationWithTransition()
    {
        if (hoverVisualState == HoverVisualState.None || rectTransform == null)
        {
            return;
        }

        hoverTransitionFromPosition = rectTransform.anchoredPosition;
        hoverTransitionToPosition = baseAnchoredPosition;
        hoverTransitionFromScale = rectTransform.localScale;
        hoverTransitionToScale = baseLocalScale;
        hoverTransitionElapsed = 0f;
        hoverVisualState = HoverVisualState.Exiting;
    }

    private void EndHoverPresentationImmediate()
    {
        if (rectTransform == null)
        {
            hoverVisualState = HoverVisualState.None;
            return;
        }

        // ホバー中のみ座標を戻す。ドラッグ中は None のままなので、跳ね戻り開始時に誤ってスロット座標へ飛ばさない。
        if (hoverVisualState != HoverVisualState.None)
        {
            rectTransform.anchoredPosition = baseAnchoredPosition;
            rectTransform.localScale = baseLocalScale;
        }

        hoverVisualState = HoverVisualState.None;
        hoverTransitionElapsed = 0f;
        TryRestorePlacementOrderIfNoActiveHover();
    }

    private void ForceStopHoverPresentation()
    {
        if (activeHoveredItem == this)
        {
            activeHoveredItem = null;
        }

        EndHoverPresentationImmediate();
    }

    private void UpdateHoverPresentation(float deltaTime)
    {
        if (hoverVisualState == HoverVisualState.None || rectTransform == null)
        {
            return;
        }

        float safeSeconds = Mathf.Max(0.01f, hoverTransitionSeconds);
        switch (hoverVisualState)
        {
            case HoverVisualState.Entering:
                hoverTransitionElapsed += Mathf.Max(0f, deltaTime);
                ApplyTransition(hoverTransitionFromPosition, hoverTransitionToPosition, hoverTransitionFromScale, hoverTransitionToScale, hoverTransitionElapsed / safeSeconds);
                if (hoverTransitionElapsed >= safeSeconds)
                {
                    hoverVisualState = HoverVisualState.Hovering;
                }
                break;
            case HoverVisualState.Hovering:
                float signedSpeed = Mathf.Max(0f, hoverOrbitSpeedDegPerSec) * (hoverOrbitClockwise ? -1f : 1f);
                hoverOrbitAngleDeg += signedSpeed * Mathf.Max(0f, deltaTime);
                float rad = hoverOrbitAngleDeg * Mathf.Deg2Rad;
                Vector2 orbitOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Max(0f, hoverOrbitRadiusPixels);
                rectTransform.anchoredPosition = hoverCenter + orbitOffset;
                rectTransform.localScale = hoverTransitionToScale;
                break;
            case HoverVisualState.Exiting:
                hoverTransitionElapsed += Mathf.Max(0f, deltaTime);
                ApplyTransition(hoverTransitionFromPosition, hoverTransitionToPosition, hoverTransitionFromScale, hoverTransitionToScale, hoverTransitionElapsed / safeSeconds);
                if (hoverTransitionElapsed >= safeSeconds)
                {
                    rectTransform.anchoredPosition = baseAnchoredPosition;
                    rectTransform.localScale = baseLocalScale;
                    hoverVisualState = HoverVisualState.None;
                    TryRestorePlacementOrderIfNoActiveHover();
                }
                break;
        }
    }

    private void ApplyTransition(
        Vector2 fromPosition,
        Vector2 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
        rectTransform.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
    }

    private void TryRestorePlacementOrderIfNoActiveHover()
    {
        if (activeHoveredItem != null)
        {
            return;
        }

        itemSpawnController?.NotifyPlacementLayoutNeedsRefresh();
    }

    private bool IsPaused()
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        return gm != null && gm.ShouldSuppressPlayerInteractions;
    }

    private void PlayBounceSe()
    {
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.ItemBounce);
    }

    private enum HoverVisualState
    {
        None = 0,
        Entering = 1,
        Hovering = 2,
        Exiting = 3
    }
}
