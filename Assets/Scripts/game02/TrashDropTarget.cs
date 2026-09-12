using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TrashDropTarget : MonoBehaviour, IWorkplaceTarget
{
    [Header("View")]
    [SerializeField] private Image imageClose;
    [SerializeField] private Image imageOpen;

    [Header("Shake Animation")]
    [SerializeField] private float jumpUpPixels = 16f;
    [SerializeField] private float shakeDurationSeconds = 0.22f;
    [SerializeField] private float shakeAngleDegrees = 10f;
    [SerializeField] private int shakeCount = 3;

    [Header("Trash MoneyAddUI")]
    [SerializeField] private TMP_Text moneyAddUiText;
    [SerializeField] private float moneyAddFadeSeconds = 0.75f;
    [SerializeField] private float moneyAddRisePixels = 24f;
    [SerializeField] private float refundBase = 100f;
    [SerializeField] private float refundScale = 700f;
    [SerializeField] private float refundPopularityDecay = 80000f;

    private RectTransform imageOpenRect;
    private Vector2 imageOpenBaseAnchoredPosition;
    private bool hasImageOpenBasePos;
    private Coroutine shakeRoutine;
    private bool isPointerHoverByDrag;
    private RectTransform moneyAddUiRect;
    private Vector2 moneyAddUiBaseAnchoredPosition;
    private bool hasMoneyAddUiBasePos;
    private bool isMoneyAddFading;
    private float moneyAddFadeElapsedSeconds;
    private long pendingMoneyAddDisplayDelta;

    private static readonly HashSet<TrashDropTarget> RegisteredTargets = new HashSet<TrashDropTarget>();

    private void Awake()
    {
        ResolveRefsIfNeeded();
        SetOpenState(false);
    }

    private void OnEnable()
    {
        RegisteredTargets.Add(this);
        ResolveRefsIfNeeded();
        SetOpenState(isPointerHoverByDrag);
        InitializeMoneyAddUi();
    }

    private void OnDisable()
    {
        RegisteredTargets.Remove(this);
        isPointerHoverByDrag = false;
        SetOpenState(false);
        InitializeMoneyAddUi();
    }

    private void Update()
    {
        UpdateMoneyAddFade();
    }

    public bool CanAcceptItem(DraggableItemController item)
    {
        if (!isActiveAndEnabled || item == null)
        {
            return false;
        }

        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm != null && (gm.ShouldSuppressPlayerInteractions || gm.HasFatalError))
        {
            return false;
        }

        if (item.ItemType == ItemType.ItemStream ||
            item.ItemType == ItemType.ItemMovie)
        {
            return true;
        }

        // 既存シーン上で ItemType 未設定でも、命名規約で受け入れ可能にする。
        string n = item.name;
        if (string.IsNullOrEmpty(n))
        {
            return false;
        }

        return n.StartsWith("ItemStream_", System.StringComparison.Ordinal) ||
               n.StartsWith("ItemMovie_", System.StringComparison.Ordinal);
    }

    public void OnItemDropped(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return;
        }

        ApplyRefundForManualDiscard(item);
        PlayTrashFx();
        Game02.Game02MsgManager.TryGet()?.NotifyFirstTrashDrop(Game02.GameManager.Instance);
    }

    public bool TryAutoDiscard(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return false;
        }

        PlayTrashFx();
        Game02.Game02MsgManager.TryGet()?.NotifyFirstTrashDrop(Game02.GameManager.Instance);
        if (item != null)
        {
            Destroy(item.gameObject);
        }

        return true;
    }

    public bool TrySimulateManualDiscard(DraggableItemController item)
    {
        if (!CanAcceptItem(item))
        {
            return false;
        }

        OnItemDropped(item);
        if (item != null)
        {
            Destroy(item.gameObject);
        }

        return true;
    }

    public static void UpdateDragHover(PointerEventData eventData, DraggableItemController draggingItem)
    {
        if (eventData == null || EventSystem.current == null)
        {
            ClearDragHover();
            return;
        }

        TrashDropTarget hovered = null;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (go == null)
            {
                continue;
            }

            TrashDropTarget target = go.GetComponentInParent<TrashDropTarget>();
            if (target == null)
            {
                continue;
            }

            if (!target.CanAcceptItem(draggingItem))
            {
                continue;
            }

            hovered = target;
            break;
        }

        foreach (TrashDropTarget target in RegisteredTargets)
        {
            if (target == null)
            {
                continue;
            }

            target.SetDragHoverState(target == hovered);
        }
    }

    public static void ClearDragHover()
    {
        foreach (TrashDropTarget target in RegisteredTargets)
        {
            if (target == null)
            {
                continue;
            }

            target.SetDragHoverState(false);
        }
    }

    public static bool TryDropFromPointer(PointerEventData eventData, DraggableItemController item)
    {
        if (eventData == null || item == null)
        {
            return false;
        }

        foreach (TrashDropTarget target in RegisteredTargets)
        {
            if (target == null || !target.isActiveAndEnabled || !target.CanAcceptItem(item))
            {
                continue;
            }

            RectTransform rect = target.transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            Camera cam = eventData.pressEventCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, cam))
            {
                target.OnItemDropped(item);
                return true;
            }
        }

        // 保険: 登録セットが空のケースでは Find して1回だけ判定する。
        TrashDropTarget fallback = FindActiveTarget();
        if (fallback == null || !fallback.CanAcceptItem(item))
        {
            return false;
        }

        RectTransform fallbackRect = fallback.transform as RectTransform;
        if (fallbackRect == null)
        {
            return false;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(fallbackRect, eventData.position, eventData.pressEventCamera))
        {
            return false;
        }

        fallback.OnItemDropped(item);
        return true;
    }

    public static TrashDropTarget FindActiveTarget()
    {
        foreach (TrashDropTarget target in RegisteredTargets)
        {
            if (target != null && target.isActiveAndEnabled)
            {
                return target;
            }
        }

        TrashDropTarget found = FindObjectOfType<TrashDropTarget>(true);
        return found != null && found.isActiveAndEnabled ? found : null;
    }

    public static void EnsureSceneTrashDropTargetExists()
    {
        GameObject trash = GameObject.Find("PanelCanvas/Trash");
        if (trash == null)
        {
            trash = FindInactiveByName("Trash");
        }

        if (trash == null)
        {
            return;
        }

        if (trash.GetComponent<TrashDropTarget>() == null)
        {
            trash.AddComponent<TrashDropTarget>();
        }
    }

    private static GameObject FindInactiveByName(string objectName)
    {
        Transform[] all = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == objectName)
            {
                return t.gameObject;
            }
        }

        return null;
    }

    private void ResolveRefsIfNeeded()
    {
        if (imageClose == null)
        {
            imageClose = FindImageByName("ImageClose");
        }

        if (imageOpen == null)
        {
            imageOpen = FindImageByName("ImageOpen");
        }

        if (imageOpen != null && imageOpenRect == null)
        {
            imageOpenRect = imageOpen.rectTransform;
            imageOpenBaseAnchoredPosition = imageOpenRect.anchoredPosition;
            hasImageOpenBasePos = true;
        }

        if (moneyAddUiText == null)
        {
            Transform byPath = transform.Find("MoneyAddUI");
            if (byPath != null)
            {
                moneyAddUiText = byPath.GetComponent<TMP_Text>();
            }

            if (moneyAddUiText == null)
            {
                TMP_Text[] all = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    TMP_Text candidate = all[i];
                    if (candidate != null && candidate.name == "MoneyAddUI")
                    {
                        moneyAddUiText = candidate;
                        break;
                    }
                }
            }
        }

        if (moneyAddUiText != null && moneyAddUiRect == null)
        {
            moneyAddUiRect = moneyAddUiText.rectTransform;
            moneyAddUiBaseAnchoredPosition = moneyAddUiRect.anchoredPosition;
            hasMoneyAddUiBasePos = true;
        }
    }

    private Image FindImageByName(string name)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (!string.Equals(t.name, name, System.StringComparison.Ordinal))
            {
                continue;
            }

            return t.GetComponent<Image>();
        }

        return null;
    }

    private void SetDragHoverState(bool hover)
    {
        isPointerHoverByDrag = hover;
        if (shakeRoutine != null)
        {
            return;
        }

        SetOpenState(hover);
    }

    private void SetOpenState(bool open)
    {
        if (imageOpen != null)
        {
            imageOpen.gameObject.SetActive(open);
        }

        if (imageClose != null)
        {
            imageClose.gameObject.SetActive(!open);
        }
    }

    private void PlayTrashFx()
    {
        ResolveRefsIfNeeded();
        PlayTrashSe();
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(CoPlayShake());
    }

    private void PlayTrashSe()
    {
        Game02.Game02SeManager.TryGet()?.PlayByCue(Game02.Game02SeCue.TrashDrop);
    }

    private void ApplyRefundForManualDiscard(DraggableItemController item)
    {
        Game02.GameManager gm = Game02.GameManager.Instance;
        if (gm == null || item == null)
        {
            return;
        }

        long refund = ComputeRefundByPopularity(gm.CurrentPopularity);
        if (refund <= 0L)
        {
            return;
        }

        string source = string.IsNullOrEmpty(item.name) ? "TrashDropTarget" : $"TrashDropTarget.{item.name}";
        if (!gm.RequestMoneyDelta(refund, "Trash manual refund", source, -1))
        {
            return;
        }

        ShowMoneyAdd(refund);
    }

    private long ComputeRefundByPopularity(long popularity)
    {
        double pop = System.Math.Max(0d, popularity);
        double decay = System.Math.Max(1d, refundPopularityDecay);
        double raw = System.Math.Max(0d, refundBase) +
                     (System.Math.Max(0d, refundScale) * (1d - System.Math.Exp(-pop / decay)));
        return (long)System.Math.Floor(System.Math.Max(0d, raw));
    }

    private void InitializeMoneyAddUi()
    {
        ResolveRefsIfNeeded();
        if (moneyAddUiText == null)
        {
            return;
        }

        pendingMoneyAddDisplayDelta = 0L;
        if (moneyAddUiText.gameObject.activeSelf)
        {
            moneyAddUiText.gameObject.SetActive(false);
        }
        moneyAddUiText.text = string.Empty;
        Color c = moneyAddUiText.color;
        c.a = 0f;
        moneyAddUiText.color = c;
        ResetMoneyAddUiPosition();
        isMoneyAddFading = false;
        moneyAddFadeElapsedSeconds = 0f;
    }

    private void ShowMoneyAdd(long delta)
    {
        ResolveRefsIfNeeded();
        if (moneyAddUiText == null || delta <= 0L)
        {
            return;
        }

        if (!moneyAddUiText.gameObject.activeSelf)
        {
            moneyAddUiText.gameObject.SetActive(true);
        }

        pendingMoneyAddDisplayDelta += delta;
        moneyAddUiText.text = $"+{pendingMoneyAddDisplayDelta:N0}";
        Color c = moneyAddUiText.color;
        c.a = 1f;
        moneyAddUiText.color = c;
        ResetMoneyAddUiPosition();
        isMoneyAddFading = true;
        moneyAddFadeElapsedSeconds = 0f;
    }

    private void UpdateMoneyAddFade()
    {
        if (!isMoneyAddFading || moneyAddUiText == null)
        {
            return;
        }

        float fade = Mathf.Max(0.01f, moneyAddFadeSeconds);
        moneyAddFadeElapsedSeconds += Mathf.Max(0f, Game02.GameManager.GameplayDelta);
        float t = Mathf.Clamp01(moneyAddFadeElapsedSeconds / fade);
        Color c = moneyAddUiText.color;
        c.a = 1f - t;
        moneyAddUiText.color = c;
        ApplyMoneyAddUiRise(Mathf.Max(0f, moneyAddRisePixels) * t);
        if (t >= 1f)
        {
            pendingMoneyAddDisplayDelta = 0L;
            moneyAddUiText.text = string.Empty;
            moneyAddUiText.gameObject.SetActive(false);
            ResetMoneyAddUiPosition();
            isMoneyAddFading = false;
        }
    }

    private void ApplyMoneyAddUiRise(float riseOffset)
    {
        if (moneyAddUiRect == null || !hasMoneyAddUiBasePos)
        {
            return;
        }

        moneyAddUiRect.anchoredPosition = moneyAddUiBaseAnchoredPosition + new Vector2(0f, riseOffset);
    }

    private void ResetMoneyAddUiPosition()
    {
        if (moneyAddUiRect == null || !hasMoneyAddUiBasePos)
        {
            return;
        }

        moneyAddUiRect.anchoredPosition = moneyAddUiBaseAnchoredPosition;
    }

    private IEnumerator CoPlayShake()
    {
        SetOpenState(true);
        if (!hasImageOpenBasePos || imageOpenRect == null)
        {
            yield return null;
            SetOpenState(isPointerHoverByDrag);
            shakeRoutine = null;
            yield break;
        }

        imageOpenRect.anchoredPosition = imageOpenBaseAnchoredPosition + Vector2.up * Mathf.Max(0f, jumpUpPixels);
        float duration = Mathf.Max(0.01f, shakeDurationSeconds);
        int cycles = Mathf.Max(1, shakeCount);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Game02.GameManager.Instance != null && Game02.GameManager.Instance.ShouldSuppressPlayerInteractions)
            {
                yield return null;
                continue;
            }

            elapsed += Game02.GameManager.GameplayDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            float wave = Mathf.Sin(t * cycles * Mathf.PI * 2f);
            imageOpenRect.localRotation = Quaternion.Euler(0f, 0f, wave * Mathf.Max(0f, shakeAngleDegrees));
            yield return null;
        }

        imageOpenRect.localRotation = Quaternion.identity;
        imageOpenRect.anchoredPosition = imageOpenBaseAnchoredPosition;
        SetOpenState(isPointerHoverByDrag);
        shakeRoutine = null;
    }
}
