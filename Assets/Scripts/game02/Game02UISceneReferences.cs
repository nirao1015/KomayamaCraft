using UnityEngine;

/// <summary>
/// game02 UIシーン参照の雛形。
/// FieldCanvas/HoverHit/PanelCanvas をまとめて保持する。
/// </summary>
public class Game02UISceneReferences : MonoBehaviour
{
    [Header("Field")]
    [SerializeField] private RectTransform fieldCanvas;
    [SerializeField] private Transform hoverHitTarget;

    [Header("Panel")]
    [SerializeField] private RectTransform panelCanvas;

    public RectTransform FieldCanvas => fieldCanvas;
    public Transform HoverHitTarget => hoverHitTarget;
    public RectTransform PanelCanvas => panelCanvas;

    public void Configure(RectTransform fieldRoot, Transform hoverHit, RectTransform panelRoot)
    {
        fieldCanvas = fieldRoot;
        hoverHitTarget = hoverHit;
        panelCanvas = panelRoot;
    }
}
