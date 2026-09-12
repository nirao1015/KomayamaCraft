using UnityEngine;

/// <summary>
/// カメラの Viewport Rect を固定アスペクトに合わせる。
/// 横長は左右余白（ピラーボックス）、縦寄りは上下余白（レターボックス）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FixedAspectCameraFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private int referenceWidth = 1920;
    [SerializeField] private int referenceHeight = 1080;
    [SerializeField] private bool enableFixedAspect = true;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Awake()
    {
        ResolveCamera();
        Apply();
    }

    private void OnEnable()
    {
        ResolveCamera();
        Apply();
    }

    private void LateUpdate()
    {
        ResolveCamera();
        if (targetCamera == null)
        {
            return;
        }

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            Apply();
        }
    }

    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }

    private void Apply()
    {
        if (targetCamera == null)
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (!enableFixedAspect || referenceHeight <= 0)
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        if (Screen.width <= 0 || Screen.height <= 0)
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        float targetAspect = (float)referenceWidth / referenceHeight;
        float windowAspect = (float)Screen.width / Screen.height;

        if (windowAspect > targetAspect)
        {
            float scale = targetAspect / windowAspect;
            float x = (1f - scale) * 0.5f;
            targetCamera.rect = new Rect(x, 0f, scale, 1f);
        }
        else
        {
            float scale = windowAspect / targetAspect;
            float y = (1f - scale) * 0.5f;
            targetCamera.rect = new Rect(0f, y, 1f, scale);
        }
    }
}
