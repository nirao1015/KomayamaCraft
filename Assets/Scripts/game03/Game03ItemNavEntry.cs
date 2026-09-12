using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ItemNavi 配下の NaviBase 複製に付け、ユニットとフィールドアイテムの論理位置関係から縁寄せと矢印向きを更新する。
/// 対象アイテム（ワールドアンカー）がゲームカメラで画面内に見えている間は CanvasGroup で非表示にする。
/// NaviBase の RectTransform の Anchored Position Y は Initialize 時に読み取り、「ユニットに追従するベース Y」への加算オフセットとして保持する。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class Game03ItemNavEntry : MonoBehaviour
{
    private static readonly List<Game03ItemNavEntry> SameSideStackScratch = new List<Game03ItemNavEntry>(8);

    [Header("Anchored X in ItemNavi parent local space")]
    [Tooltip("アイテムがユニットよりループ最短で右側にあるときの navRoot.anchoredPosition.x")]
    [SerializeField] private float anchoredPositionXWhenRight = 520f;
    [Tooltip("アイテムがユニットよりループ最短で左側にあるときの navRoot.anchoredPosition.x")]
    [SerializeField] private float anchoredPositionXWhenLeft = -520f;

    [Header("Same-side stacking (visible navs only)")]
    [Tooltip("同じ左右側に複数ナビがいるとき、兄弟順で N 番目の Y を N × この値だけ減らす（例: 20 で 2 枚目は元より 20 下）")]
    [SerializeField, Min(0f)] private float duplicateStackVerticalStepPixels = 20f;

    [SerializeField, Range(0f, 0.2f)] private float itemViewportVisibilityMargin = 0.02f;

    [Header("NaviFlame (left layout)")]
    [Tooltip("左表示時の NaviFlame.anchoredPosition.x（NaviItem あり: 相対 Y は維持、X はこの固定値）。NaviItem なし: 左時の X にこの値を使う。")]
    [SerializeField] private float navFlameAnchoredXWhenLeft = -8f;

    private Transform itemWorldAnchor;
    private RectTransform navRoot;
    private RectTransform itemNaviParent;
    private RectTransform navFlame;
    private RectTransform navItem;
    private Vector3 flameInitialScale = Vector3.one;
    /// <summary>NaviItem 基準の NaviFlame 相対オフセット（複製直後に算出）。左は X を navFlameAnchoredXWhenLeft にしつつ Y の相対を維持する。</summary>
    private Vector2 flameOffsetFromNavItem;
    /// <summary>NaviItem が無いとき用の NaviFlame.anchoredPosition スナップショット。</summary>
    private Vector2 flameTemplateAnchoredPosition;
    private Game03UnitManager unitManager;
    private Game03Manager game03Manager;
    private Camera gameplayCamera;
    private Camera canvasEventCamera;
    private CanvasGroup navCanvasGroup;
    private bool useHorizontalLoopNearest;

    /// <summary>複製直後の navRoot.anchoredPosition.y（テンプレで設定した縦オフセット）。</summary>
    private float followAnchoredYOffset;

    /// <summary>複製直後の navRoot.anchoredPosition.x（テンプレの横微調整。右寄せ時の縁寄せ X に加算）。</summary>
    private float followAnchoredXOffset;

    /// <param name="trackingItemIcon">
    /// フィールドアイテム側の見た目。指定時は NaviBase／NaviItem の Image に適用（いずれか無ければスキップ）。
    /// </param>
    public void Initialize(
        Transform worldItemAnchor,
        RectTransform navParentRect,
        Game03UnitManager units,
        Game03Manager manager,
        Camera orthographicGameplayCamera,
        Sprite trackingItemIcon = null,
        bool useHorizontalLoopNearestForNav = false)
    {
        useHorizontalLoopNearest = useHorizontalLoopNearestForNav;
        itemWorldAnchor = worldItemAnchor;
        navRoot = transform as RectTransform;
        itemNaviParent = navParentRect;
        unitManager = units;
        game03Manager = manager;
        gameplayCamera = orthographicGameplayCamera;

        navFlame = transform.Find("NaviFlame") as RectTransform;
        navItem = navRoot != null ? navRoot.Find("NaviItem") as RectTransform : null;
        if (navFlame != null)
        {
            flameInitialScale = navFlame.localScale;
            flameTemplateAnchoredPosition = navFlame.anchoredPosition;
            flameOffsetFromNavItem = navItem != null
                ? (Vector2)navFlame.anchoredPosition - (Vector2)navItem.anchoredPosition
                : Vector2.zero;
        }
        else
        {
            flameTemplateAnchoredPosition = Vector2.zero;
            flameOffsetFromNavItem = Vector2.zero;
        }

        Canvas canvas = navParentRect != null ? navParentRect.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasEventCamera = canvas.worldCamera;
        }

        navCanvasGroup = navRoot.GetComponent<CanvasGroup>();
        if (navCanvasGroup == null)
        {
            navCanvasGroup = navRoot.gameObject.AddComponent<CanvasGroup>();
        }

        ApplyTrackingItemIcon(trackingItemIcon);

        followAnchoredYOffset = navRoot != null ? navRoot.anchoredPosition.y : 0f;
        followAnchoredXOffset = navRoot != null ? navRoot.anchoredPosition.x : 0f;

        UpdatePose(forceFlameLayout: true);
    }

    private void ApplyTrackingItemIcon(Sprite icon)
    {
        if (icon == null)
        {
            return;
        }

        Image navBaseImage = navRoot != null ? navRoot.GetComponent<Image>() : null;
        if (navBaseImage != null)
        {
            navBaseImage.sprite = icon;
        }

        Transform navItemTransform = navRoot != null ? navRoot.Find("NaviItem") : null;
        Image navItemImage = navItemTransform != null ? navItemTransform.GetComponent<Image>() : null;
        if (navItemImage != null)
        {
            navItemImage.sprite = icon;
        }
    }

    private void LateUpdate()
    {
        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        UpdatePose(forceFlameLayout: false);
    }

    private void UpdatePose(bool forceFlameLayout)
    {
        if (itemWorldAnchor == null || navRoot == null || itemNaviParent == null || unitManager == null || unitManager.MainUnitRect == null)
        {
            return;
        }

        Camera visCam = gameplayCamera != null ? gameplayCamera : Camera.main;
        Vector3 anchorWorld = itemWorldAnchor.position;
        if (useHorizontalLoopNearest && unitManager != null && unitManager.MainUnitRect != null)
        {
            anchorWorld = Game03HorizontalLoopUtility.GetNearestLoopWorldPositionToPlayer(
                unitManager,
                anchorWorld,
                unitManager.HorizontalLoopWorldPeriod);
        }

        bool itemVisibleOnScreen = IsWorldPositionRoughlyOnGameplayViewport(visCam, anchorWorld, itemViewportVisibilityMargin);
        if (navCanvasGroup != null)
        {
            navCanvasGroup.alpha = itemVisibleOnScreen ? 0f : 1f;
            navCanvasGroup.blocksRaycasts = !itemVisibleOnScreen;
            navCanvasGroup.interactable = !itemVisibleOnScreen;
        }

        float playerWx = unitManager.MainUnitRect.position.x;
        float itemWx = itemWorldAnchor.position.x;
        float loopPeriod = unitManager.HorizontalLoopWorldPeriod;
        float shortest = SignedShortestLoopDeltaX(playerWx, itemWx, loopPeriod);

        bool placeOnRightEdge = shortest >= 0f;

        Vector3 flameScale = navFlame != null ? navFlame.localScale : Vector3.one;
        float flameMagX = Mathf.Abs(flameInitialScale.x > 0.001f ? flameInitialScale.x : 1f);
        float flameY = Mathf.Approximately(flameInitialScale.y, 0f) ? 1f : flameInitialScale.y;
        // 画面内の「垂直軸まわり」の反転（左右ミラー）＝ localScale.x の符号。scale.y だと上下反転になりしっぽの向きは変わらない。
        float flameX = placeOnRightEdge ? flameMagX : -flameMagX;
        bool flameLayoutMatches =
            Mathf.Approximately(flameScale.x, flameX)
            && Mathf.Approximately(flameScale.y, flameY)
            && Mathf.Approximately(flameScale.z, flameInitialScale.z);
        if (navFlame != null && (forceFlameLayout || !flameLayoutMatches))
        {
            navFlame.localScale = new Vector3(flameX, flameY, flameInitialScale.z);
        }

        if (navFlame != null)
        {
            if (navItem != null)
            {
                Vector2 itemAnchored = navItem.anchoredPosition;
                if (placeOnRightEdge)
                {
                    navFlame.anchoredPosition = itemAnchored + flameOffsetFromNavItem;
                }
                else
                {
                    navFlame.anchoredPosition = new Vector2(navFlameAnchoredXWhenLeft, itemAnchored.y + flameOffsetFromNavItem.y);
                }
            }
            else
            {
                float flameAnchoredX = placeOnRightEdge ? flameTemplateAnchoredPosition.x : navFlameAnchoredXWhenLeft;
                navFlame.anchoredPosition = new Vector2(flameAnchoredX, flameTemplateAnchoredPosition.y);
            }
        }

        Camera uiCam = gameplayCamera != null ? gameplayCamera : Camera.main;
        Vector2 screenUnit = RectTransformUtility.WorldToScreenPoint(uiCam, unitManager.MainUnitRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemNaviParent, screenUnit, canvasEventCamera, out Vector2 localUnit);

        float targetX = placeOnRightEdge
            ? followAnchoredXOffset + anchoredPositionXWhenRight
            : anchoredPositionXWhenLeft;

        float targetY = localUnit.y + followAnchoredYOffset;
        ApplyVerticalStackOffset(ref targetY, placeOnRightEdge);

        navRoot.anchoredPosition = new Vector2(targetX, targetY);
    }

    /// <summary>
    /// CanvasGroup で表示中（アルファあり）のナビだけを数え、同側では sibling 順で Y をずらす。
    /// </summary>
    private void ApplyVerticalStackOffset(ref float baseY, bool placeOnRightEdge)
    {
        float step = duplicateStackVerticalStepPixels;
        if (step <= 0f || itemNaviParent == null)
        {
            return;
        }

        SameSideStackScratch.Clear();
        Game03ItemNavEntry[] entries = itemNaviParent.GetComponentsInChildren<Game03ItemNavEntry>(false);
        for (int i = 0; i < entries.Length; i++)
        {
            Game03ItemNavEntry e = entries[i];
            if (e == null || e.navRoot == null || e.itemWorldAnchor == null || e.unitManager == null || e.unitManager.MainUnitRect == null)
            {
                continue;
            }

            if (!e.IsStackLayoutConsideredVisible())
            {
                continue;
            }

            if (e.ComputePlaceOnRightEdge() != placeOnRightEdge)
            {
                continue;
            }

            SameSideStackScratch.Add(e);
        }

        SameSideStackScratch.Sort(CompareSiblingOrder);

        int index = SameSideStackScratch.IndexOf(this);
        if (index < 0)
        {
            index = 0;
        }

        baseY -= index * step;
    }

    private bool IsStackLayoutConsideredVisible()
    {
        return navCanvasGroup == null || navCanvasGroup.alpha > 0.01f;
    }

    private bool ComputePlaceOnRightEdge()
    {
        if (itemWorldAnchor == null || unitManager == null || unitManager.MainUnitRect == null)
        {
            return true;
        }

        float playerWx = unitManager.MainUnitRect.position.x;
        float itemWx = itemWorldAnchor.position.x;
        float loopPeriod = unitManager.HorizontalLoopWorldPeriod;
        float shortest = SignedShortestLoopDeltaX(playerWx, itemWx, loopPeriod);
        return shortest >= 0f;
    }

    private static int CompareSiblingOrder(Game03ItemNavEntry a, Game03ItemNavEntry b)
    {
        int ai = a != null ? a.transform.GetSiblingIndex() : int.MaxValue;
        int bi = b != null ? b.transform.GetSiblingIndex() : int.MaxValue;
        return ai.CompareTo(bi);
    }

    /// <summary>
    /// アイテム位置とユニット位置の最短ループ符号距離（右方向プラス）。
    /// </summary>
    private static float SignedShortestLoopDeltaX(float playerWorldX, float itemWorldX, float loopPeriod)
    {
        if (loopPeriod <= 0.01f)
        {
            return itemWorldX - playerWorldX;
        }

        float dx = itemWorldX - playerWorldX;
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

    private static bool IsWorldPositionRoughlyOnGameplayViewport(Camera cam, Vector3 worldPosition, float marginViewport)
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

        float m = marginViewport;
        return vp.x >= m && vp.x <= 1f - m && vp.y >= m && vp.y <= 1f - m;
    }
}
