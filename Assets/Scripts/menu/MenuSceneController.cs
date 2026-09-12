using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSceneController : MonoBehaviour
{
    [Header("参照: 遷移・オーディオ")]
    [SerializeField, Tooltip("Canvas 開閉とシーン遷移。ボタン OnClick はこちらを参照してください。")]
    private MenuTransitionManager menuTransitionManager;
    [SerializeField, Tooltip("メニュー BGM の管理。")]
    private MenuBgmManager menuBgmManager;

    [Header("参照")]
    [SerializeField] private RectTransform illustRect;
    [SerializeField] private SplineContainer splineIllust;
    [SerializeField] private SplineAnimate illustSplineAnimate;
    [SerializeField] private GameObject playManualCanvas;
    [SerializeField] private Button playManualExitButton;
    [SerializeField] private RectTransform playManualImageRoot;
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("PlayManualCanvas 背景の暗さ（アルファ）")]
    private float playManualCanvasBgAlpha = 0.82f;

    private const string StartButtonObjectName = "StartButton";
    private const string TitleReturnButtonObjectName = "TitleReturnButton";
    private const string Game01ButtonObjectName = "Game01Button";
    private const string PlayManualButtonObjectName = "PlayManualButton";
    private const string PlayManualCanvasObjectName = "PlayManualCanvas";
    private const string LegacyExitButtonObjectName = "EXITButton";
    private const string PlayManualExitButtonObjectName = "PlayManualExitButton";
    private const string PlayManualCanvasBgObjectName = "CanvasBG";
    private const string PlayManualImageRootObjectName = "PlayManualImageRoot";
    private const int PlayManualCanvasSortingOrder = 1050;
    private const string IllustObjectName = "Illust";
    private const string SplineIllustObjectName = "SplineIlust";
    private bool warnedMissingIllustSpline;

    public void BindMenuTransitionManager(MenuTransitionManager manager)
    {
        menuTransitionManager = manager;
    }

    private void Awake()
    {
        ResolveIllustSplineReferences();
        EnsurePlayManualReferences();
        EnsurePlayManualUiVisuals();
        ConfigureIllustSpline();
        ConfigureAspectCanvases();
    }

    private void Start()
    {
        ApplyBgmVolume();
    }

    private void Update()
    {
        ApplyBgmVolume();

        if (playManualCanvas != null && playManualCanvas.activeInHierarchy)
        {
            EnsurePlayManualCanvasFrontMost();
        }

    }

    private void ResolveIllustSplineReferences()
    {
        if (illustRect == null)
        {
            GameObject illustObject = FindSceneGameObjectByExactName(IllustObjectName);
            if (illustObject != null)
            {
                illustRect = illustObject.GetComponent<RectTransform>();
            }
        }

        if (splineIllust == null)
        {
            GameObject splineObject = FindSceneGameObjectByExactName(SplineIllustObjectName);
            if (splineObject != null)
            {
                splineIllust = splineObject.GetComponent<SplineContainer>();
            }
        }

        if (illustSplineAnimate == null && illustRect != null)
        {
            illustSplineAnimate = illustRect.GetComponent<SplineAnimate>();
        }
    }

    private void EnsurePlayManualReferences()
    {
        if (playManualCanvas == null)
        {
            GameObject found = FindSceneGameObjectByExactName(PlayManualCanvasObjectName);
            if (found != null)
            {
                playManualCanvas = found;
            }
        }

        if (playManualCanvas != null)
        {
            if (playManualExitButton == null)
            {
                playManualExitButton = FindButtonInHierarchy(playManualCanvas, PlayManualExitButtonObjectName, LegacyExitButtonObjectName);
                if (playManualExitButton != null)
                {
                    playManualExitButton.gameObject.name = PlayManualExitButtonObjectName;
                }
            }

            if (playManualImageRoot == null)
            {
                playManualImageRoot = FindRectTransformInHierarchy(playManualCanvas, PlayManualImageRootObjectName);
            }
        }
    }

    private void EnsurePlayManualUiVisuals()
    {
        if (playManualCanvas == null)
        {
            return;
        }

        EnsurePlayManualCanvasFrontMost();

        Transform bgTransform = playManualCanvas.transform.Find(PlayManualCanvasBgObjectName);
        if (bgTransform != null)
        {
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.sprite = null;
                bgImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(playManualCanvasBgAlpha));
            }
        }

        Transform panelTransform = playManualCanvas.transform.Find("Panel");
        if (panelTransform != null)
        {
            Image panelImage = panelTransform.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelTransform.gameObject.AddComponent<Image>();
            }

            panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        }
    }

    public void ApplyPlayManualCanvasVisibility(bool visible)
    {
        if (playManualCanvas == null)
        {
            EnsurePlayManualReferences();
        }

        if (playManualCanvas == null)
        {
            return;
        }

        EnsurePlayManualCanvasFrontMost();
        playManualCanvas.SetActive(visible);
        bool allowMenu = !visible;
        menuTransitionManager?.SetOverlayMenuRowInteractable(allowMenu);

        if (visible)
        {
            EnsurePlayManualReferences();
            RebindPlayManualExitButtons();
            EnsurePlayManualExitButtonVisible();
            EnsurePlayManualUiVisuals();
        }
    }

    private void EnsurePlayManualCanvasFrontMost()
    {
        if (playManualCanvas == null)
        {
            return;
        }

        FixedAspectCanvasFitter playManualFitter = playManualCanvas.GetComponent<FixedAspectCanvasFitter>();
        if (playManualFitter != null)
        {
            playManualFitter.enabled = false;
        }

        Canvas canvas = playManualCanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = playManualCanvas.AddComponent<Canvas>();
        }

        if (playManualCanvas.GetComponent<CanvasScaler>() == null)
        {
            playManualCanvas.AddComponent<CanvasScaler>();
        }

        if (playManualCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            playManualCanvas.AddComponent<GraphicRaycaster>();
        }

        // Overlay + overrideSorting を強制し、さらに sibling も末尾化して確実に前面化する。
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = PlayManualCanvasSortingOrder;
        playManualCanvas.transform.SetAsLastSibling();

        Canvas[] childCanvases = playManualCanvas.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < childCanvases.Length; i++)
        {
            Canvas child = childCanvases[i];
            if (child == null || child == canvas)
            {
                continue;
            }

            child.overrideSorting = true;
            child.sortingOrder = Mathf.Max(child.sortingOrder, PlayManualCanvasSortingOrder + 1);
        }
    }

    private void RebindPlayManualExitButtons()
    {
        if (playManualCanvas == null)
        {
            return;
        }

        if (playManualExitButton != null)
        {
            return;
        }

        Button[] buttons = playManualCanvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null)
            {
                continue;
            }

            string n = b.gameObject.name;
            if (n != PlayManualExitButtonObjectName && n != LegacyExitButtonObjectName)
            {
                continue;
            }

            playManualExitButton = b;
            return;
        }
    }

    private void EnsurePlayManualExitButtonVisible()
    {
        if (playManualExitButton == null)
        {
            return;
        }

        RectTransform buttonRect = playManualExitButton.transform as RectTransform;
        if (buttonRect == null)
        {
            return;
        }

        // 画像切り替え時の重なりで埋もれないよう、常に手前に出す。
        buttonRect.SetAsLastSibling();
    }

    private static GameObject FindSceneGameObjectByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName))
        {
            return null;
        }

        GameObject active = GameObject.Find(exactName);
        if (active != null)
        {
            return active;
        }

        int sceneCount = SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null)
                {
                    continue;
                }

                if (root.name == exactName)
                {
                    return root;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (t != null && t.name == exactName)
                    {
                        return t.gameObject;
                    }
                }
            }
        }

        return null;
    }

    private static Button FindButtonInHierarchy(GameObject root, params string[] objectNames)
    {
        if (root == null || objectNames == null || objectNames.Length == 0)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }

            string name = buttons[i].gameObject.name;
            for (int n = 0; n < objectNames.Length; n++)
            {
                if (name == objectNames[n])
                {
                    return buttons[i];
                }
            }
        }

        return null;
    }

    private static RectTransform FindRectTransformInHierarchy(GameObject root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] != null && rects[i].gameObject.name == objectName)
            {
                return rects[i];
            }
        }

        return null;
    }

    private void ConfigureIllustSpline()
    {
        EnsureIllustDecorationEffects();

        if (illustRect == null || splineIllust == null)
        {
            if (!warnedMissingIllustSpline)
            {
                warnedMissingIllustSpline = true;
                Debug.LogWarning("[MenuScene] Illust または SplineIlust が見つからないため、Spline移動は無効です。");
            }
            return;
        }

        if (illustSplineAnimate == null)
        {
            illustSplineAnimate = illustRect.gameObject.AddComponent<SplineAnimate>();
        }

        // UI Image はスプライン接線に姿勢合わせすると面が裏向きになりやすいので回転追従は無効化する。
        if (illustRect.GetComponent<Graphic>() != null)
        {
            illustSplineAnimate.Alignment = SplineAnimate.AlignmentMode.None;
        }

        illustSplineAnimate.Container = splineIllust;
        illustSplineAnimate.PlayOnAwake = false;
        illustSplineAnimate.Loop = SplineAnimate.LoopMode.Loop;

        MenuIllustSplineTilt illustTilt = illustRect.GetComponent<MenuIllustSplineTilt>();
        if (illustTilt == null)
        {
            illustTilt = illustRect.gameObject.AddComponent<MenuIllustSplineTilt>();
        }

        if (illustTilt.Container == null)
        {
            illustTilt.Container = splineIllust;
        }

        if (illustTilt.Animate == null)
        {
            illustTilt.Animate = illustSplineAnimate;
        }

        illustTilt.RefreshSplineCache();
    }

    /// <summary>
    /// Spline の有無に依存しない Illust 周辺演出（速度線・前景雲）。ビルドでも Illust さえあれば付与する。
    /// </summary>
    private void EnsureIllustDecorationEffects()
    {
        if (illustRect == null)
        {
            return;
        }

        MenuIllustSpeedLineEffect speedLine = illustRect.GetComponent<MenuIllustSpeedLineEffect>();
        if (speedLine == null)
        {
            speedLine = illustRect.gameObject.AddComponent<MenuIllustSpeedLineEffect>();
        }

        MenuIllustForegroundCloudEffect cloudEffect = illustRect.GetComponent<MenuIllustForegroundCloudEffect>();
        if (cloudEffect == null)
        {
            cloudEffect = illustRect.gameObject.AddComponent<MenuIllustForegroundCloudEffect>();
        }
    }

    private void ApplyBgmVolume()
    {
        menuBgmManager?.ApplyBgmVolumeFromSettings();
    }

    private static void ConfigureAspectCanvases()
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

            // PlayManualCanvas は最前面 Overlay として扱うため対象外。
            if (canvas.gameObject.name == PlayManualCanvasObjectName)
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
