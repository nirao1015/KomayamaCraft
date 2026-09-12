using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全シーンで固定アスペクト設定を自動適用するランタイムブートストラップ。
/// - MainCamera に FixedAspectCameraFitter を付与
/// - ルート Canvas に FixedAspectCanvasFitter を付与
/// </summary>
public class AspectSettingsBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject go = new GameObject(nameof(AspectSettingsBootstrap));
        DontDestroyOnLoad(go);
        go.AddComponent<AspectSettingsBootstrap>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToCurrentScene();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToCurrentScene();
    }

    private static void ApplyToCurrentScene()
    {
        ApplyCameraFitter();
        ApplyCanvasFitter();
    }

    private static void ApplyCameraFitter()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera c in cameras)
            {
                if (c != null && c.enabled)
                {
                    cam = c;
                    break;
                }
            }
        }

        if (cam == null)
        {
            return;
        }

        if (cam.GetComponent<FixedAspectCameraFitter>() == null)
        {
            cam.gameObject.AddComponent<FixedAspectCameraFitter>();
        }
    }

    private static void ApplyCanvasFitter()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (canvas.rootCanvas != canvas)
            {
                continue;
            }

            if (canvas.GetComponent<FixedAspectCanvasFitter>() == null)
            {
                canvas.gameObject.AddComponent<FixedAspectCanvasFitter>();
            }
        }
    }
}
