using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Illust の手前に cloud01/cloud02 を通過させる落下演出。
/// 開始位置と倍率はシーン上の cloud オブジェクト配置値をそのまま利用する。
/// </summary>
[DefaultExecutionOrder(70)]
public sealed class MenuIllustForegroundCloudEffect : MonoBehaviour
{
    [Serializable]
    private sealed class CloudPassConfig
    {
        public string sourceObjectName = "cloud01";
        public float intervalSeconds = 8f;
        public float moveDurationSeconds = 0.95f;
    }

    private sealed class CloudRuntime
    {
        public CloudPassConfig config;
        /// <summary>ワールド／Canvas 外のスプライト用（従来）。</summary>
        public SpriteRenderer worldSpriteSource;
        /// <summary>Canvas 上の Image テンプレ用。設定時はカメラ非依存で parentRect 座標に写す。</summary>
        public RectTransform canvasSourceRect;
        public Image canvasSourceImage;
        public bool canvasSourceWasGraphicEnabled;
        public RectTransform overlayRect;
        public Image overlayImage;
        public Coroutine loopCoroutine;

        public bool IsCanvasSource => canvasSourceRect != null && canvasSourceImage != null;
    }

    [Header("参照")]
    [SerializeField] private RectTransform illustRect;

    [Header("有効化")]
    [SerializeField] private bool enableEffect = true;

    [Header("見た目")]
    [SerializeField] private float alpha = 0.7f;
    [SerializeField] private float endMarginY = 240f;
    [Tooltip("cloud を Canvas 上の Image にした場合、テンプレを隠してオーバーレイのみ表示する。")]
    [SerializeField] private bool hideCanvasCloudSourceTemplate = true;

    [Header("通過設定")]
    [SerializeField] private CloudPassConfig cloud01 = new CloudPassConfig
    {
        sourceObjectName = "cloud01",
        intervalSeconds = 8f,
        moveDurationSeconds = 0.95f
    };

    [SerializeField] private CloudPassConfig cloud02 = new CloudPassConfig
    {
        sourceObjectName = "cloud02",
        intervalSeconds = 13f,
        moveDurationSeconds = 1.1f
    };

    private readonly List<CloudRuntime> _runtimes = new List<CloudRuntime>();
    private readonly Vector3[] _worldCornersScratch = new Vector3[4];
    private readonly Vector3[] _boundsWorldCornersScratch = new Vector3[8];

    private void Awake()
    {
        if (illustRect == null)
        {
            illustRect = transform as RectTransform;
        }

        BuildRuntimes();
    }

    private void OnEnable()
    {
        StopAllRuntimeCoroutines();
        SetAllOverlayActive(false);

        if (!enableEffect)
        {
            return;
        }

        for (int i = 0; i < _runtimes.Count; i++)
        {
            CloudRuntime rt = _runtimes[i];
            if (rt.overlayImage == null || rt.overlayRect == null)
            {
                continue;
            }

            if (rt.IsCanvasSource)
            {
                if (rt.canvasSourceImage.sprite == null)
                {
                    continue;
                }

                if (hideCanvasCloudSourceTemplate)
                {
                    rt.canvasSourceWasGraphicEnabled = rt.canvasSourceImage.enabled;
                    rt.canvasSourceImage.enabled = false;
                }
            }
            else if (rt.worldSpriteSource == null || rt.worldSpriteSource.sprite == null)
            {
                continue;
            }

            rt.loopCoroutine = StartCoroutine(RunCloudLoop(rt));
        }
    }

    private void OnDisable()
    {
        StopAllRuntimeCoroutines();
        SetAllOverlayActive(false);

        for (int i = 0; i < _runtimes.Count; i++)
        {
            CloudRuntime rt = _runtimes[i];
            if (hideCanvasCloudSourceTemplate && rt.IsCanvasSource && rt.canvasSourceImage != null)
            {
                rt.canvasSourceImage.enabled = rt.canvasSourceWasGraphicEnabled;
            }
        }
    }

    private void BuildRuntimes()
    {
        _runtimes.Clear();

        AddRuntime(cloud01);
        AddRuntime(cloud02);
    }

    private void AddRuntime(CloudPassConfig config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.sourceObjectName))
        {
            return;
        }

        RectTransform parentRect = illustRect != null ? illustRect.parent as RectTransform : null;
        if (parentRect == null)
        {
            return;
        }

        string overlayName = "IllustFrontCloud_" + config.sourceObjectName;
        GameObject overlayObject;
        Transform existing = parentRect.Find(overlayName);
        if (existing != null)
        {
            overlayObject = existing.gameObject;
        }
        else
        {
            overlayObject = new GameObject(overlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(parentRect, false);
        }

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        Image overlayImage = overlayObject.GetComponent<Image>();
        if (overlayImage == null)
        {
            overlayImage = overlayObject.AddComponent<Image>();
        }

        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);

        overlayImage.raycastTarget = false;
        overlayImage.preserveAspect = true;
        overlayRect.gameObject.SetActive(false);

        CloudRuntime runtime = new CloudRuntime
        {
            config = config,
            overlayRect = overlayRect,
            overlayImage = overlayImage
        };

        if (TryFindCanvasCloudImageSource(config.sourceObjectName, out RectTransform uiRect, out Image uiImage))
        {
            runtime.canvasSourceRect = uiRect;
            runtime.canvasSourceImage = uiImage;
            overlayImage.sprite = uiImage.sprite;
        }
        else
        {
            SpriteRenderer source = FindSpriteRendererByName(config.sourceObjectName);
            if (source == null)
            {
                return;
            }

            runtime.worldSpriteSource = source;
            overlayImage.sprite = source.sprite;
        }

        overlayImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        _runtimes.Add(runtime);
    }

    private IEnumerator RunCloudLoop(CloudRuntime rt)
    {
        while (true)
        {
            yield return RunSinglePass(rt);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, rt.config.intervalSeconds));
        }
    }

    private IEnumerator RunSinglePass(CloudRuntime rt)
    {
        if (rt.overlayRect == null || rt.overlayImage == null || illustRect == null)
        {
            yield break;
        }

        RectTransform parentRect = illustRect.parent as RectTransform;
        if (parentRect == null)
        {
            yield break;
        }

        Vector2 startLocal;
        Vector2 cloudSize;

        if (rt.IsCanvasSource)
        {
            if (rt.canvasSourceRect == null || rt.canvasSourceImage == null || rt.canvasSourceImage.sprite == null)
            {
                yield break;
            }

            GetRectBoundsInParentLocal(parentRect, rt.canvasSourceRect, out startLocal, out cloudSize);
            if (cloudSize.x <= 0.01f || cloudSize.y <= 0.01f)
            {
                cloudSize = new Vector2(
                    Mathf.Abs(rt.canvasSourceRect.rect.width),
                    Mathf.Abs(rt.canvasSourceRect.rect.height));
            }

            if (cloudSize.x <= 0.01f || cloudSize.y <= 0.01f)
            {
                yield break;
            }
        }
        else
        {
            if (rt.worldSpriteSource == null || rt.worldSpriteSource.sprite == null)
            {
                yield break;
            }

            // Canvas 直下の SpriteRenderer 等: カメラに依存せず親 Rect のローカルへ写す（ビルドで Screen 変換が失敗しやすい）。
            bool spriteUnderIllustParent =
                parentRect != null && rt.worldSpriteSource.transform.IsChildOf(parentRect);

            if (spriteUnderIllustParent)
            {
                startLocal = ParentRectLocalFromWorldPoint(parentRect, rt.worldSpriteSource.transform.position);
                GetWorldBoundsSizeInParentLocal(parentRect, rt.worldSpriteSource.bounds, out cloudSize);
                cloudSize.x = Mathf.Max(cloudSize.x, 2f);
                cloudSize.y = Mathf.Max(cloudSize.y, 2f);
            }
            else
            {
                Camera cam = ResolveMenuWorldCamera(illustRect);
                int waitFrames = 0;
                const int maxCameraWaitFrames = 200;
                while (cam == null && waitFrames < maxCameraWaitFrames)
                {
                    waitFrames++;
                    yield return null;
                    cam = ResolveMenuWorldCamera(illustRect);
                }

                if (cam == null)
                {
                    yield break;
                }

                if (!TryWorldToParentLocal(parentRect, rt.worldSpriteSource.transform.position, cam, out startLocal))
                {
                    yield break;
                }

                cloudSize = GetSourceUiSize(parentRect, rt.worldSpriteSource, cam);
                if (cloudSize.x <= 0.01f || cloudSize.y <= 0.01f)
                {
                    yield break;
                }
            }
        }

        rt.overlayRect.sizeDelta = cloudSize;
        rt.overlayRect.anchoredPosition = startLocal;
        if (rt.IsCanvasSource)
        {
            rt.overlayRect.localRotation =
                Quaternion.Inverse(parentRect.rotation) * rt.canvasSourceRect.rotation;
        }
        else if (rt.worldSpriteSource != null && parentRect != null &&
                 rt.worldSpriteSource.transform.IsChildOf(parentRect))
        {
            rt.overlayRect.localRotation =
                Quaternion.Inverse(parentRect.rotation) * rt.worldSpriteSource.transform.rotation;
        }
        else if (rt.worldSpriteSource != null)
        {
            rt.overlayRect.localEulerAngles = rt.worldSpriteSource.transform.localEulerAngles;
        }

        rt.overlayRect.localScale = Vector3.one;
        rt.overlayImage.sprite = rt.IsCanvasSource ? rt.canvasSourceImage.sprite : rt.worldSpriteSource.sprite;
        rt.overlayImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        // Illust の手前レイヤーへ固定。
        int targetSibling = Mathf.Min(parentRect.childCount - 1, illustRect.GetSiblingIndex() + 1);
        rt.overlayRect.SetSiblingIndex(targetSibling);
        rt.overlayRect.gameObject.SetActive(true);

        float endY = parentRect.rect.yMax + cloudSize.y * 0.5f + Mathf.Max(0f, endMarginY);
        float startY = startLocal.y;
        float x = startLocal.x;

        float duration = Mathf.Max(0.05f, rt.config.moveDurationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rt.overlayRect.anchoredPosition = new Vector2(x, Mathf.Lerp(startY, endY, t));
            yield return null;
        }

        rt.overlayRect.gameObject.SetActive(false);
    }

    private static SpriteRenderer FindSpriteRendererByName(string name)
    {
        SpriteRenderer[] spriteRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr != null && sr.gameObject.name == name)
            {
                return sr;
            }
        }

        return null;
    }

    private bool TryFindCanvasCloudImageSource(string objectName, out RectTransform rect, out Image image)
    {
        rect = null;
        image = null;

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        Canvas illustCanvas = illustRect != null ? illustRect.GetComponentInParent<Canvas>() : null;
        if (illustCanvas != null)
        {
            RectTransform[] rects = illustCanvas.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] == null || rects[i].gameObject.name != objectName)
                {
                    continue;
                }

                if (rects[i].name.StartsWith("IllustFrontCloud_", StringComparison.Ordinal))
                {
                    continue;
                }

                Image img = rects[i].GetComponent<Image>();
                if (img != null && img.sprite != null)
                {
                    rect = rects[i];
                    image = img;
                    return true;
                }
            }
        }

        RectTransform[] allRects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allRects.Length; i++)
        {
            if (allRects[i] == null || allRects[i].gameObject.name != objectName)
            {
                continue;
            }

            if (allRects[i].name.StartsWith("IllustFrontCloud_", StringComparison.Ordinal))
            {
                continue;
            }

            Image img = allRects[i].GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                rect = allRects[i];
                image = img;
                return true;
            }
        }

        return false;
    }

    private static Vector2 ParentRectLocalFromWorldPoint(RectTransform parentRect, Vector3 worldPoint)
    {
        Vector3 local = parentRect.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

    /// <summary>ワールド AABB の 8 頂点を親の XY 平面に射影し、親ローカルでの外接矩形サイズを求める。</summary>
    private void GetWorldBoundsSizeInParentLocal(RectTransform parentRect, Bounds worldBounds, out Vector2 sizeLocal)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        _boundsWorldCornersScratch[0] = new Vector3(min.x, min.y, min.z);
        _boundsWorldCornersScratch[1] = new Vector3(min.x, max.y, min.z);
        _boundsWorldCornersScratch[2] = new Vector3(max.x, min.y, min.z);
        _boundsWorldCornersScratch[3] = new Vector3(max.x, max.y, min.z);
        _boundsWorldCornersScratch[4] = new Vector3(min.x, min.y, max.z);
        _boundsWorldCornersScratch[5] = new Vector3(min.x, max.y, max.z);
        _boundsWorldCornersScratch[6] = new Vector3(max.x, min.y, max.z);
        _boundsWorldCornersScratch[7] = new Vector3(max.x, max.y, max.z);

        Vector2 p0 = ParentRectLocalFromWorldPoint(parentRect, _boundsWorldCornersScratch[0]);
        Vector2 vmin = p0;
        Vector2 vmax = p0;
        for (int i = 1; i < 8; i++)
        {
            Vector2 p = ParentRectLocalFromWorldPoint(parentRect, _boundsWorldCornersScratch[i]);
            vmin = Vector2.Min(vmin, p);
            vmax = Vector2.Max(vmax, p);
        }

        sizeLocal = vmax - vmin;
    }

    /// <summary>親 Rect のローカルで、子の軸平行バウンドの中心とサイズ（Canvas スケール込み）。</summary>
    private void GetRectBoundsInParentLocal(RectTransform parentRect, RectTransform childRect, out Vector2 centerLocal, out Vector2 sizeLocal)
    {
        childRect.GetWorldCorners(_worldCornersScratch);
        Vector2 min = ParentRectLocalFromWorldPoint(parentRect, _worldCornersScratch[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = ParentRectLocalFromWorldPoint(parentRect, _worldCornersScratch[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        sizeLocal = max - min;
        centerLocal = (min + max) * 0.5f;
    }

    /// <summary>
    /// ワールド座標のスプライトを UI に載せ替えるためのカメラ。ビルド初フレームで Camera.main が未確定なことがある。
    /// </summary>
    private static Camera ResolveMenuWorldCamera(RectTransform illustOrUiRoot)
    {
        if (illustOrUiRoot != null)
        {
            Canvas canvas = illustOrUiRoot.GetComponentInParent<Canvas>();
            if (canvas != null &&
                canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
            {
                return cameras[i];
            }
        }

        return null;
    }

    private static bool TryWorldToParentLocal(RectTransform parentRect, Vector3 worldPos, Camera cam, out Vector2 localPoint)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out localPoint);
    }

    private static Vector2 GetSourceUiSize(RectTransform parentRect, SpriteRenderer sr, Camera cam)
    {
        Bounds b = sr.bounds;
        Vector3 min = new Vector3(b.min.x, b.min.y, sr.transform.position.z);
        Vector3 max = new Vector3(b.max.x, b.max.y, sr.transform.position.z);

        if (!TryWorldToParentLocal(parentRect, min, cam, out Vector2 localMin))
        {
            return Vector2.zero;
        }

        if (!TryWorldToParentLocal(parentRect, max, cam, out Vector2 localMax))
        {
            return Vector2.zero;
        }

        Vector2 size = localMax - localMin;
        return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
    }

    private void StopAllRuntimeCoroutines()
    {
        for (int i = 0; i < _runtimes.Count; i++)
        {
            if (_runtimes[i].loopCoroutine != null)
            {
                StopCoroutine(_runtimes[i].loopCoroutine);
                _runtimes[i].loopCoroutine = null;
            }
        }
    }

    private void SetAllOverlayActive(bool active)
    {
        for (int i = 0; i < _runtimes.Count; i++)
        {
            if (_runtimes[i].overlayRect != null)
            {
                _runtimes[i].overlayRect.gameObject.SetActive(active);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        alpha = Mathf.Clamp01(alpha);
        endMarginY = Mathf.Max(0f, endMarginY);

        if (cloud01 != null)
        {
            cloud01.intervalSeconds = Mathf.Max(0.2f, cloud01.intervalSeconds);
            cloud01.moveDurationSeconds = Mathf.Max(0.05f, cloud01.moveDurationSeconds);
            if (string.IsNullOrWhiteSpace(cloud01.sourceObjectName))
            {
                cloud01.sourceObjectName = "cloud01";
            }
        }

        if (cloud02 != null)
        {
            cloud02.intervalSeconds = Mathf.Max(0.2f, cloud02.intervalSeconds);
            cloud02.moveDurationSeconds = Mathf.Max(0.05f, cloud02.moveDurationSeconds);
            if (string.IsNullOrWhiteSpace(cloud02.sourceObjectName))
            {
                cloud02.sourceObjectName = "cloud02";
            }
        }
    }
#endif
}

