using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MenuImageButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float pressedBrightness = 0.84f;

    private RectTransform rectTransform;
    private Graphic graphic;
    private Vector3 initialScale = Vector3.one;
    private Color initialColor = Color.white;
    private bool initialized;

    /// <summary>
    /// menu03 の <see cref="Menu03TransitionManager"/> と同様、Button に押下縮小を付与する。
    /// </summary>
    public static void AttachTo(Button button, float scale, float brightness)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;
        MenuImageButtonPressFeedback feedback = button.GetComponent<MenuImageButtonPressFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<MenuImageButtonPressFeedback>();
        }

        feedback.Configure(scale, brightness);
    }

    public void Configure(float scale, float brightness)
    {
        pressedScale = Mathf.Clamp(scale, 0.75f, 1f);
        pressedBrightness = Mathf.Clamp(brightness, 0.5f, 1f);
        initialized = false;
        EnsureCached();
        ApplyReleasedVisual();
    }

    private void Awake()
    {
        EnsureCached();
    }

    private void OnEnable()
    {
        initialized = false;
        EnsureCached();
        ApplyReleasedVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        EnsureCached();
        if (rectTransform != null)
        {
            rectTransform.localScale = initialScale * pressedScale;
        }

        if (graphic != null)
        {
            Color dimmed = initialColor * pressedBrightness;
            graphic.color = new Color(dimmed.r, dimmed.g, dimmed.b, initialColor.a);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ApplyReleasedVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyReleasedVisual();
    }

    private void EnsureCached()
    {
        if (initialized)
        {
            return;
        }

        rectTransform = transform as RectTransform;
        Button button = GetComponent<Button>();
        graphic = button != null ? button.targetGraphic : GetComponent<Graphic>();
        if (rectTransform != null)
        {
            initialScale = rectTransform.localScale;
        }

        if (graphic != null)
        {
            initialColor = graphic.color;
        }

        initialized = true;
    }

    private void ApplyReleasedVisual()
    {
        EnsureCached();
        if (rectTransform != null)
        {
            rectTransform.localScale = initialScale;
        }

        if (graphic != null)
        {
            graphic.color = initialColor;
        }
    }
}
