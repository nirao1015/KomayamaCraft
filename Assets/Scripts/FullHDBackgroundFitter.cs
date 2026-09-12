using UnityEngine;

/// <summary>
/// FullHD(1920x1080)を基準に、16:9固定のViewport Rectを設定しつつ、
/// Orthographicカメラと背景(SpriteRenderer)の見える範囲を揃えます。
/// 使い方:
/// - BackGlound(背景)に SpriteRenderer を付ける
/// - このスクリプトをカメラ側に付ける
/// - Inspectorで targetBackground に BackGlound の SpriteRenderer を指定する
/// </summary>
[ExecuteAlways]
public class FullHDBackgroundFitter : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Background")]
    [SerializeField] private SpriteRenderer targetBackground;

    [Header("FullHD Settings")]
    [SerializeField] private int targetWidth = 1920;
    [SerializeField] private int targetHeight = 1080;

    [Header("Aspect")]
    [SerializeField]
    [Tooltip("有効時、Camera.rect を使って16:9を維持し余白を表示する")]
    private bool enforceFixedAspect = true;

    [Tooltip("背景/プレイヤー等のスプライトが使っている pixelsPerUnit。背景spriteの設定がこれとズレるとサイズが合いません。")]
    [SerializeField] private float pixelsPerUnit = 100f;

    [Header("Draw Order")]
    [SerializeField] private bool forceBackgroundBehind = true;
    [SerializeField] private int backgroundSortingOrder = -100;

    [Header("スプライト差し替え後のフィット")]
    [SerializeField]
    [Tooltip("背景スプライト変更後に一度この値を基準にし、その上で画面を覆う倍率を計算する（通常の Fit では従来どおり intrinsic のみ）")]
    private Vector3 initialBackgroundScaleBeforeCover = new Vector3(1f, 1f, 1f);

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void OnValidate()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null || targetBackground == null)
        {
            return;
        }

        Fit(false);
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        Fit(false);
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            Fit(false);
        }
    }

    /// <summary>解像度変更と同様の通常フィット（従来どおり intrinsic のみでスケール決定）。</summary>
    public void RefreshBackgroundFit()
    {
        Fit(false);
    }

    /// <summary>背景スプライト差し替え直後用。Inspector の基準スケールを踏まえてから画面サイズに合わせて拡大する。</summary>
    public void RefreshBackgroundFitAfterSpriteChange()
    {
        Fit(true);
    }

    private void Fit(bool applyInspectorInitialScaleBeforeCover)
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (!targetCamera.orthographic)
        {
            Debug.LogWarning("FullHDBackgroundFitter: Orthographicカメラが必要です。Camera.orthographicをONにしてください。自動でONにします。");
            targetCamera.orthographic = true;
        }

        float targetAspect = (float)targetWidth / targetHeight;
        if (enforceFixedAspect)
        {
            ApplyFixedAspectViewport(targetAspect);
        }
        else
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        // OrthographicCameraは高さ(orthographicSize)が「半分の世界サイズ」。
        // よって、targetHeight(px) -> world units に換算して orthographicSize を決める。
        targetCamera.orthographicSize = (targetHeight * 0.5f) / pixelsPerUnit;

        // 画面に表示されるワールドサイズ
        float heightWorld = targetCamera.orthographicSize * 2f;
        float widthWorld = heightWorld * targetAspect;

        if (targetBackground.sprite == null)
        {
            return;
        }

        if (forceBackgroundBehind)
        {
            targetBackground.sortingOrder = backgroundSortingOrder;
        }

        // spriteの「素の」サイズ(スケール1のときのワールドサイズ)
        float spriteWidth = targetBackground.sprite.rect.width / targetBackground.sprite.pixelsPerUnit;
        float spriteHeight = targetBackground.sprite.rect.height / targetBackground.sprite.pixelsPerUnit;

        if (spriteWidth <= 0f || spriteHeight <= 0f)
        {
            return;
        }

        Vector3 bgScale;
        if (applyInspectorInitialScaleBeforeCover)
        {
            Vector3 initial = initialBackgroundScaleBeforeCover;
            float ix = Mathf.Abs(initial.x);
            float iy = Mathf.Abs(initial.y);
            if (ix < 1e-5f)
            {
                ix = 1f;
            }

            if (iy < 1e-5f)
            {
                iy = 1f;
            }

            float w0 = spriteWidth * ix;
            float h0 = spriteHeight * iy;
            float k = Mathf.Max(widthWorld / Mathf.Max(w0, 1e-6f), heightWorld / Mathf.Max(h0, 1e-6f));
            bgScale.x = initial.x * k;
            bgScale.y = initial.y * k;
            bgScale.z = Mathf.Abs(initial.z) < 1e-5f ? 1f : initial.z;
        }
        else
        {
            // 元画像のアスペクト比を維持したまま、画面を覆う最小の等倍係数を使う。
            float scaleToCoverWidth = widthWorld / spriteWidth;
            float scaleToCoverHeight = heightWorld / spriteHeight;
            float uniformScale = Mathf.Max(scaleToCoverWidth, scaleToCoverHeight);
            bgScale.x = uniformScale;
            bgScale.y = uniformScale;
            bgScale.z = 1f;
        }

        targetBackground.transform.localScale = bgScale;

        // 背景をカメラ中心へ
        Vector3 camPos = targetCamera.transform.position;
        targetBackground.transform.position = new Vector3(camPos.x, camPos.y, targetBackground.transform.position.z);
    }

    private void ApplyFixedAspectViewport(float targetAspect)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        float windowAspect = (float)Screen.width / Screen.height;
        if (windowAspect > targetAspect)
        {
            // 横長端末: 左右に余白（ピラーボックス）
            float scale = targetAspect / windowAspect;
            float x = (1f - scale) * 0.5f;
            targetCamera.rect = new Rect(x, 0f, scale, 1f);
        }
        else
        {
            // 縦寄り端末: 上下に余白（レターボックス）
            float scale = windowAspect / targetAspect;
            float y = (1f - scale) * 0.5f;
            targetCamera.rect = new Rect(0f, y, 1f, scale);
        }
    }
}

