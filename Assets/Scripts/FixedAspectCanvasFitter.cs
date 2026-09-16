using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas を 16:9 固定カメラの Viewport（Camera.rect）へ追従させる。
/// Screen Space Overlay のままだと余白にも UI が広がるため、
/// Screen Space Camera に揃えて worldCamera を設定する。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FixedAspectCanvasFitter : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera targetCamera;

    [Header("CanvasScaler")]
    [SerializeField] private bool forceCanvasScalerSettings = true;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float matchWidthOrHeight = 0.5f;

    [Header("Draw Order")]
    [SerializeField, Tooltip("ON のとき sortingOrder を下限以上にする。Sorting Layer は触らない（KomayamaScreenCanvasBands が帯を担当）。")]
    private bool forceFrontSorting = false;
    [SerializeField] private int minimumSortingOrder = 0;

    [Header("World Depth (2D)")]
    [Tooltip("ON にすると planeDistance をワールド（典型 z=0）より手前に固定する。Screen Space Camera で地形の後ろに潜るのを防ぐ。")]
    [SerializeField] private bool forceFrontPlaneDistance = false;
    [SerializeField] private float frontPlaneDistance = 1f;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastCameraRect = new Rect(-1f, -1f, -1f, -1f);
    private int lockedSortingOrder = -1;

    private void Awake()
    {
        ResolveReferences();
        Apply();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Apply();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        if (targetCanvas == null || targetCamera == null)
        {
            return;
        }

        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            targetCamera.rect != lastCameraRect)
        {
            Apply();
        }
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    /// <summary>
    /// 複数 Canvas を重ねるシーン用。指定時は <see cref="minimumSortingOrder"/> による繰り上げを行わない。
    /// </summary>
    public void LockSortingOrder(int sortingOrder)
    {
        lockedSortingOrder = sortingOrder;
    }

    public void Refresh()
    {
        ResolveReferences();
        Apply();
    }

    private void Apply()
    {
        if (targetCanvas == null || targetCamera == null)
        {
            return;
        }

        // UI を Camera.rect の描画領域に追従させる
        if (targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        if (targetCanvas.worldCamera != targetCamera)
        {
            targetCanvas.worldCamera = targetCamera;
        }

        // カメラ前方の確実に見える位置へ置く（far clip も超えない）
        // 2D ワールド（z≈0）より手前に置く場合は frontPlaneDistance を使う
        float desiredPlane = forceFrontPlaneDistance ? frontPlaneDistance : targetCanvas.planeDistance;
        float safePlaneDistance = Mathf.Clamp(
            desiredPlane,
            targetCamera.nearClipPlane + 0.1f,
            targetCamera.farClipPlane - 0.1f);
        targetCanvas.planeDistance = safePlaneDistance;

        if (forceFrontSorting)
        {
            targetCanvas.overrideSorting = true;
            if (lockedSortingOrder >= 0)
            {
                targetCanvas.sortingOrder = lockedSortingOrder;
            }
            else if (targetCanvas.sortingOrder < minimumSortingOrder)
            {
                targetCanvas.sortingOrder = minimumSortingOrder;
            }
        }

        if (forceCanvasScalerSettings)
        {
            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraRect = targetCamera.rect;
    }
}
