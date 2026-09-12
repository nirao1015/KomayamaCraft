using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// logo_scene 制御。複数 Canvas の扱いは menu03（CanvasBG + 前面 Canvas の切替）に合わせる。
/// </summary>
public class LogoSceneController : MonoBehaviour
{
    private const string TitleSceneName = "title_scene";
    private const string CanvasBgObjectName = "CanvasBG";
    private const string LegacyBackgroundObjectName = "BackGround";
    private const string LogoChildName = "Logo";
    private const string FictionTextChildName = "Text (TMP)";

    private const int BgCanvasSortingOrder = 0;
    private const int LogoCanvasSortingOrder = 200;
    private const int FictionCanvasSortingOrder = 400;

    [Header("参照（menu03 と同様に Canvas を割り当て）")]
    [SerializeField]
    [Tooltip("背景専用 Canvas。子に CanvasBG のみ置き、フェードしない")]
    private Canvas bgCanvas;

    [SerializeField]
    [Tooltip("企業ロゴ用 Canvas（常時表示・Logo のみフェード）")]
    private Canvas logoCanvas;

    [SerializeField]
    [Tooltip("フィクション用 Canvas（menu03 の PlayManualCanvas 相当・表示時に最前面へ）")]
    private Canvas fictionCanvas;

    [SerializeField]
    [Tooltip("フェード対象（未設定なら Logo を検索）")]
    private CanvasGroup logoFadeGroup;

    [SerializeField]
    [Tooltip("フェード対象（未設定なら Text (TMP) を検索）")]
    private CanvasGroup fictionFadeGroup;

    [Header("企業ロゴ (Canvas)")]
    [SerializeField]
    private float canvasFadeInDurationSeconds = 0.5f;

    [SerializeField]
    private float canvasDisplaySeconds = 3f;

    [SerializeField]
    private float canvasFadeOutDurationSeconds = 1f;

    [Header("フィクション (FictionCanvas)")]
    [SerializeField]
    private float fictionFadeInDurationSeconds = 0.5f;

    [SerializeField]
    private float fictionDisplaySeconds = 3f;

    [SerializeField]
    private float fictionFadeOutDurationSeconds = 1f;

    [Header("シーン遷移")]
    [SerializeField]
    private float sceneTransitionFadeOutSeconds = 1.5f;

    private void Start()
    {
        EnsureLogoSceneAspectSettings();
        SetupCanvasLayers();
        StartCoroutine(RunLogoSequenceAfterCanvasReady());
    }

    private IEnumerator RunLogoSequenceAfterCanvasReady()
    {
        yield return null;
        ApplyFixedAspectCanvasStack();
        yield return RunLogoSequence();
    }

    private void SetupCanvasLayers()
    {
        ConfigureBackgroundCanvas(bgCanvas);
        ConfigureContentCanvas(logoCanvas);
        ConfigureContentCanvas(fictionCanvas);

        HideDuplicateCanvasBg(logoCanvas);
        HideDuplicateCanvasBg(fictionCanvas);

        logoFadeGroup = ResolveFadeGroup(logoFadeGroup, logoCanvas, LogoChildName);
        fictionFadeGroup = ResolveFadeGroup(fictionFadeGroup, fictionCanvas, FictionTextChildName);

        PrepareFadeTargetActiveHidden(logoFadeGroup, logoCanvas, LogoChildName);
        ApplyInitialCanvasVisibility();
        ApplyFixedAspectCanvasStack();
    }

    /// <summary>menu03 の CanvasBG と同様、背景 Canvas はルートをフェードさせない。</summary>
    private static void ConfigureBackgroundCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        EnsureCanvasScaler(canvas);
        StripRootCanvasGroup(canvas);

        Transform canvasBg = canvas.transform.Find(CanvasBgObjectName);
        if (canvasBg == null)
        {
            canvasBg = canvas.transform.Find(LegacyBackgroundObjectName);
        }

        if (canvasBg != null && canvasBg.name != CanvasBgObjectName)
        {
            canvasBg.name = CanvasBgObjectName;
        }
    }

    /// <summary>ロゴ／フィクション用 Canvas（フェードは子の CanvasGroup のみ）。</summary>
    private static void ConfigureContentCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        EnsureCanvasScaler(canvas);
        SetCanvasRootOpaque(canvas);
    }

    private static void EnsureCanvasScaler(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>
    /// MainCamera の <see cref="FixedAspectCameraFitter"/> + 各 Canvas の
    /// <see cref="FixedAspectCanvasFitter"/>（Screen Space Camera・16:9 追従）。
    /// </summary>
    private void ApplyFixedAspectCanvasStack()
    {
        EnsureLogoSceneAspectSettings();
        ConfigureStackCanvas(bgCanvas, BgCanvasSortingOrder);
        ConfigureStackCanvas(logoCanvas, LogoCanvasSortingOrder);
        ConfigureStackCanvas(fictionCanvas, FictionCanvasSortingOrder);
        EnsureCanvasStackOrder();
    }

    private static void ConfigureStackCanvas(Canvas canvas, int sortingOrder)
    {
        if (canvas == null)
        {
            return;
        }

        EnsureCanvasScaler(canvas);

        FixedAspectCanvasFitter fitter = canvas.GetComponent<FixedAspectCanvasFitter>();
        if (fitter == null)
        {
            fitter = canvas.gameObject.AddComponent<FixedAspectCanvasFitter>();
        }

        fitter.LockSortingOrder(sortingOrder);
        fitter.Refresh();
    }

    private static Camera ResolveMainCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera c in cameras)
        {
            if (c != null && c.enabled)
            {
                return c;
            }
        }

        return null;
    }

    private void ApplyInitialCanvasVisibility()
    {
        if (bgCanvas != null)
        {
            bgCanvas.gameObject.SetActive(true);
        }

        if (logoCanvas != null)
        {
            logoCanvas.gameObject.SetActive(true);
            PrepareFadeTargetActiveHidden(logoFadeGroup, logoCanvas, LogoChildName);
        }

        if (fictionCanvas != null)
        {
            fictionCanvas.gameObject.SetActive(false);
        }
    }

    /// <summary>BGCanvas → Canvas → FictionCanvas（奥→手前）。</summary>
    private void EnsureCanvasStackOrder()
    {
        if (bgCanvas == null)
        {
            return;
        }

        int baseIndex = bgCanvas.transform.GetSiblingIndex();
        bgCanvas.sortingOrder = BgCanvasSortingOrder;

        if (logoCanvas != null)
        {
            logoCanvas.sortingOrder = LogoCanvasSortingOrder;
            logoCanvas.transform.SetSiblingIndex(baseIndex + 1);
        }

        if (fictionCanvas != null)
        {
            fictionCanvas.sortingOrder = FictionCanvasSortingOrder;
            int fictionSibling = logoCanvas != null ? logoCanvas.transform.GetSiblingIndex() + 1 : baseIndex + 1;
            fictionCanvas.transform.SetSiblingIndex(fictionSibling);
        }
    }

    private void ShowFictionCanvasFront()
    {
        if (fictionCanvas == null)
        {
            return;
        }

        fictionCanvas.gameObject.SetActive(true);
        ApplyFixedAspectCanvasStack();
        fictionCanvas.transform.SetAsLastSibling();
    }

    private static void EnsureLogoSceneAspectSettings()
    {
        Camera cam = ResolveMainCamera();
        if (cam != null && cam.GetComponent<FixedAspectCameraFitter>() == null)
        {
            cam.gameObject.AddComponent<FixedAspectCameraFitter>();
        }
    }

    private IEnumerator RunLogoSequence()
    {
        if (logoCanvas == null || logoFadeGroup == null)
        {
            Debug.LogError("[LogoScene] logoCanvas or logoFadeGroup is not set.");
            yield break;
        }

        if (fictionCanvas == null || fictionFadeGroup == null)
        {
            Debug.LogError("[LogoScene] fictionCanvas or fictionFadeGroup is not set.");
            yield break;
        }

        logoFadeGroup.alpha = 0f;

        yield return FadeCanvasGroup(logoFadeGroup, 0f, 1f, canvasFadeInDurationSeconds);
        yield return WaitDisplaySecondsSkippable(canvasDisplaySeconds);
        yield return FadeCanvasGroup(logoFadeGroup, 1f, 0f, canvasFadeOutDurationSeconds);

        ShowFictionCanvasFront();
        fictionFadeGroup.alpha = 0f;
        yield return FadeCanvasGroup(fictionFadeGroup, 0f, 1f, fictionFadeInDurationSeconds);
        yield return WaitDisplaySecondsSkippable(fictionDisplaySeconds);
        yield return FadeCanvasGroup(fictionFadeGroup, 1f, 0f, fictionFadeOutDurationSeconds);
        fictionCanvas.gameObject.SetActive(false);

        FadeManager fadeManager = FindAnyObjectByType<FadeManager>();
        if (fadeManager == null)
        {
            GameObject fadeManagerObject = new GameObject("FadeManager");
            fadeManager = fadeManagerObject.AddComponent<FadeManager>();
        }

        fadeManager.DebugMode = false;
        fadeManager.LoadScene(TitleSceneName, Mathf.Max(0f, sceneTransitionFadeOutSeconds));
    }

    private static void HideDuplicateCanvasBg(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        Transform legacy = canvas.transform.Find(LegacyBackgroundObjectName);
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
        }

        Transform duplicateBg = canvas.transform.Find(CanvasBgObjectName);
        if (duplicateBg != null)
        {
            duplicateBg.gameObject.SetActive(false);
        }
    }

    private static void StripRootCanvasGroup(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        CanvasGroup rootGroup = canvas.GetComponent<CanvasGroup>();
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
        }
    }

    private static void SetCanvasRootOpaque(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        CanvasGroup rootGroup = canvas.GetComponent<CanvasGroup>();
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
        }
    }

    /// <summary>非表示は SetActive ではなく alpha 0。子は常時アクティブのままフェードする。</summary>
    private static void PrepareFadeTargetActiveHidden(CanvasGroup fadeGroup, Canvas canvas, string childName)
    {
        Transform child = FindDirectChild(canvas != null ? canvas.transform : null, childName);
        if (child != null && !child.gameObject.activeSelf)
        {
            child.gameObject.SetActive(true);
        }

        if (fadeGroup == null && child != null)
        {
            fadeGroup = child.GetComponent<CanvasGroup>();
            if (fadeGroup == null)
            {
                fadeGroup = child.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
        }
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static CanvasGroup ResolveFadeGroup(CanvasGroup assigned, Canvas canvas, string childName)
    {
        if (assigned != null)
        {
            return assigned;
        }

        if (canvas == null)
        {
            return null;
        }

        Transform child = FindDirectChild(canvas.transform, childName);
        if (child == null)
        {
            Debug.LogWarning($"[LogoScene] Child '{childName}' not found under {canvas.name}.");
            return null;
        }

        CanvasGroup group = child.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = child.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    /// <summary>表示ホールド中。クリック／スペース／Esc で即フェードアウトへ進める。</summary>
    private static IEnumerator WaitDisplaySecondsSkippable(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (IsSkipToFadeOutPressed())
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static bool IsSkipToFadeOutPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                return true;
            }
        }
#endif
        return false;
    }
}
