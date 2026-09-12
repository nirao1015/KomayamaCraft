using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ホバー侵入/離脱の検知専用トリガー雛形。
/// 実ロジックは別コンポーネントへ委譲する前提。
/// </summary>
public class Game02HoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Binding")]
    [SerializeField] private Game02HoverPresenter hoverPresenter;
    [SerializeField] private Game02HoverContentProvider contentProvider;

    [Header("Callbacks")]
    [SerializeField] private UnityEvent onPointerEnter;
    [SerializeField] private UnityEvent onPointerExit;
    private bool isHovering;

    public void ConfigureBinding(Game02HoverPresenter presenter, Game02HoverContentProvider provider)
    {
        hoverPresenter = presenter;
        contentProvider = provider;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HandlePointerEnter(eventData != null ? eventData.position : GetCurrentPointerScreenPosition());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HandlePointerExit();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        HandlePointerMove(eventData != null ? eventData.position : GetCurrentPointerScreenPosition());
    }

    public void ForwardPointerEnter(Vector2 pointerScreenPosition)
    {
        HandlePointerEnter(pointerScreenPosition);
    }

    public void ForwardPointerMove(Vector2 pointerScreenPosition)
    {
        HandlePointerMove(pointerScreenPosition);
    }

    public void ForwardPointerExit()
    {
        HandlePointerExit();
    }

    private void OnMouseEnter()
    {
        HandlePointerEnter(GetCurrentPointerScreenPosition());
    }

    private void OnMouseExit()
    {
        HandlePointerExit();
    }

    private void OnMouseOver()
    {
        HandlePointerMove(GetCurrentPointerScreenPosition());
    }

    private static Vector2 GetCurrentPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }

        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif
        return Vector2.zero;
    }

    private void HandlePointerEnter(Vector2 pointerScreenPosition)
    {
        if (isHovering)
        {
            return;
        }

        isHovering = true;

        if (hoverPresenter != null)
        {
            hoverPresenter.RequestShow(contentProvider, pointerScreenPosition, transform);
        }

        onPointerEnter?.Invoke();
    }

    private void HandlePointerExit()
    {
        if (!isHovering)
        {
            return;
        }

        isHovering = false;

        if (hoverPresenter != null)
        {
            hoverPresenter.RequestHide();
        }

        onPointerExit?.Invoke();
    }

    private void HandlePointerMove(Vector2 pointerScreenPosition)
    {
        if (hoverPresenter != null)
        {
            hoverPresenter.UpdateHoverPosition(pointerScreenPosition, transform);
        }
    }

    private void OnDisable()
    {
        if (!isHovering)
        {
            return;
        }

        isHovering = false;
        if (hoverPresenter != null)
        {
            hoverPresenter.RequestHide();
        }
    }
}
