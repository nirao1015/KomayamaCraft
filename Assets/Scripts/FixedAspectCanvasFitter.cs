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
    [SerializeField] private bool forceFrontSorting = true;
    [SerializeField] private int minimumSortingOrder = 200;

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
        float safePlaneDistance = Mathf.Clamp(
            targetCanvas.planeDistance,
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
