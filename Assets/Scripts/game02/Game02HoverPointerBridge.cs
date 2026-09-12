using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 親UIがポインターを先に受け取る場合に、ホバー当たり（例: Hover2HitArea）の判定を中継する。
/// </summary>
public sealed class Game02HoverPointerBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [System.Serializable]
    private sealed class BridgeTarget
    {
        public RectTransform Rect;
        public Game02HoverTrigger Trigger;
    }

    [SerializeField] private List<BridgeTarget> targets = new List<BridgeTarget>();
    private Game02HoverTrigger activeTrigger;

    public void RegisterTarget(Game02HoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        RectTransform rect = trigger.GetComponent<RectTransform>();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && targets[i].Trigger == trigger)
            {
                targets[i].Rect = rect;
                return;
            }
        }

        targets.Add(new BridgeTarget
        {
            Rect = rect,
            Trigger = trigger
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Vector2 screen = eventData != null ? eventData.position : Vector2.zero;
        Camera cam = eventData != null ? eventData.enterEventCamera : null;
        UpdateActiveTarget(screen, cam, true);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        Vector2 screen = eventData != null ? eventData.position : Vector2.zero;
        Camera cam = eventData != null ? eventData.enterEventCamera : null;
        UpdateActiveTarget(screen, cam, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activeTrigger != null)
        {
            activeTrigger.ForwardPointerExit();
            activeTrigger = null;
        }
    }

    private void OnDisable()
    {
        if (activeTrigger != null)
        {
            activeTrigger.ForwardPointerExit();
            activeTrigger = null;
        }
    }

    private void UpdateActiveTarget(Vector2 screenPosition, Camera eventCamera, bool isEnter)
    {
        Game02HoverTrigger next = ResolveTargetAt(screenPosition, eventCamera);
        if (ReferenceEquals(next, activeTrigger))
        {
            if (activeTrigger != null)
            {
                activeTrigger.ForwardPointerMove(screenPosition);
            }

            return;
        }

        if (activeTrigger != null)
        {
            activeTrigger.ForwardPointerExit();
        }

        activeTrigger = next;
        if (activeTrigger == null)
        {
            return;
        }

        if (isEnter)
        {
            activeTrigger.ForwardPointerEnter(screenPosition);
            return;
        }

        activeTrigger.ForwardPointerEnter(screenPosition);
        activeTrigger.ForwardPointerMove(screenPosition);
    }

    private Game02HoverTrigger ResolveTargetAt(Vector2 screenPosition, Camera eventCamera)
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            BridgeTarget target = targets[i];
            if (target == null || target.Trigger == null || target.Rect == null || !target.Trigger.isActiveAndEnabled)
            {
                continue;
            }

            if (!target.Rect.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(target.Rect, screenPosition, eventCamera))
            {
                return target.Trigger;
            }
        }

        return null;
    }
}
