using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Menu03TransitionManager : MonoBehaviour
{
    [Header("参照: ボタン")]
    [SerializeField, Tooltip("TitleReturnButton。押下時に title_scene へ遷移します。")]
    private Button titleReturnButton;
    [SerializeField, Tooltip("StartButton。押下時に dialogue_scene 経由で game03_scene へ遷移します。")]
    private Button startButton;
    [SerializeField, Tooltip("PlayManualButton。押下時に PlayManualCanvas を開きます。")]
    private Button playManualOpenButton;
    [SerializeField, Tooltip("PlayManual の EXITButton。押下時に PlayManualCanvas を閉じます。")]
    private Button playManualExitButton;

    [Header("参照: 管理オブジェクト")]
    [SerializeField, Tooltip("SE管理オブジェクト。遷移開始時SEを再生します。")]
    private Menu03SeManager menu03SeManager;
    [SerializeField, Tooltip("開発時のみ。遷移フェード前にメタ上書きを再適用する（本番フラグ ON なら無視）。未設定ならスキップ。")]
    private Menu03DebugManager menu03DebugManager;
    [SerializeField, Tooltip("開閉対象の PlayManualCanvas。")]
    private GameObject playManualCanvas;
    [SerializeField, Tooltip("開閉対象の UGCanvas（UpgradeButton と EXIT で制御）。")]
    private GameObject ugCanvas;
    [SerializeField, Tooltip("UGCanvas の EXITButton。")]
    private Button ugExitButton;

    [Header("シーン遷移設定")]
    [SerializeField, Tooltip("TitleReturnButton 押下時の遷移先シーン名。")]
    private string titleSceneName = "title_scene";
    [SerializeField, Tooltip("StartButton 押下時に最初に遷移するシーン名。")]
    private string dialogueSceneName = "dialogue_scene";
    [SerializeField, Tooltip("StartButton の到達先シーン名。")]
    private string game03SceneName = "game03_scene";
    [SerializeField, Tooltip("シーン遷移時に SceneTransitionContext に渡すステージ名。")]
    private string stageName = "stage_01";

    [Header("TitleReturnButton 押下演出")]
    [SerializeField, Tooltip("有効時、TitleReturnButton 押下時に暗く・縮小する演出を付与します。")]
    private bool enableTitleReturnPressFeedback = true;
    [SerializeField, Tooltip("押下時のスケール倍率。")]
    private float titleReturnPressedScale = 0.94f;
    [SerializeField, Tooltip("押下時の明るさ倍率。")]
    private float titleReturnPressedBrightness = 0.84f;

    [Header("遷移演出設定")]
    [SerializeField, Tooltip("押下後に遷移処理を開始するまでの待機秒数。")]
    private float clickDelaySeconds = 0f;
    [SerializeField, Tooltip("遷移SE再生後、フェード開始までの待機秒数。")]
    private float transitionWaitSeconds = 0.5f;
    [SerializeField, Tooltip("フェードアウト演出秒数。")]
    private float fadeOutDurationSeconds = 1f;
    [SerializeField, Tooltip("前シーン遷移で残った黒フェードを戻す秒数（0以下で無効）。")]
    private float incomingFadeClearSeconds = 0.25f;
    [SerializeField, Tooltip("遷移時に生成する FadeCanvas プレハブ。")]
    private GameObject fadeCanvasPrefab;
    [SerializeField, Tooltip("フェードアウト時に使うマスク画像。未設定なら標準表示。")]
    private Sprite fadeOutSprite;

    private const int FadeCanvasSortingOrder = 2000;

    private bool isTransitioning;
    private Fade fadeController;
    private FadeImage fadeImage;

    private void Awake()
    {
        ApplyInitialOverlayVisibility();
        ConfigureMainButtonPressFeedbackIfNeeded();
        TryClearIncomingFade();
    }

    public void OnClickTitleReturnButton()
    {
        if (isTransitioning)
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.TitleReturnTransition);
        SceneTransitionContext.DestinationSceneName = null;
        SceneTransitionContext.StageName = null;
        SceneTransitionContext.DialogueStreamingSceneNameOverride = null;
        StartCoroutine(TransitionToSceneCoroutine(titleSceneName));
    }

    public void OnClickStartButton()
    {
        if (isTransitioning)
        {
            return;
        }

        SceneTransitionContext.DestinationSceneName = game03SceneName;
        SceneTransitionContext.StageName = ResolveStageName();
        menu03SeManager?.PlayByCue(Menu03SeCue.TransitionStart);
        StartCoroutine(TransitionToSceneCoroutine(dialogueSceneName));
    }

    public void OnClickPlayManualOpenButton()
    {
        if (isTransitioning)
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.PlayManualToggle);
        SetPlayManualVisible(true);
    }

    public void OnClickPlayManualExitButton()
    {
        if (isTransitioning)
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.PlayManualToggle);
        SetPlayManualVisible(false);
    }

    /// <summary>
    /// UpgradeButton などから呼ぶ。PlayManualCanvas と同じ SE・メインボタン無効化。
    /// </summary>
    public void OnClickUgOpenButton()
    {
        if (isTransitioning)
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.PlayManualToggle);
        SetUgCanvasVisible(true);
    }

    /// <summary>
    /// UGCanvas.EXITButton から呼ぶ。PlayManual の終了と同じ SE・挙動。
    /// </summary>
    public void OnClickUgExitButton()
    {
        if (isTransitioning)
        {
            return;
        }

        menu03SeManager?.PlayByCue(Menu03SeCue.PlayManualToggle);
        SetUgCanvasVisible(false);
    }

    private IEnumerator TransitionToSceneCoroutine(string destinationSceneName)
    {
        string nextScene = string.IsNullOrWhiteSpace(destinationSceneName) ? null : destinationSceneName.Trim();
        if (string.IsNullOrEmpty(nextScene))
        {
            yield break;
        }

        isTransitioning = true;
        SetMainButtonsInteractable(false);

        menu03DebugManager?.ApplyDevelopmentMetaOverridesFromInspector();

        float clickDelay = Mathf.Max(0f, clickDelaySeconds);
        if (clickDelay > 0f)
        {
            yield return new WaitForSeconds(clickDelay);
        }

        float transitionWait = Mathf.Max(0f, transitionWaitSeconds);
        if (transitionWait > 0f)
        {
            yield return new WaitForSeconds(transitionWait);
        }

        yield return StartCoroutine(CoRunFadeOut());
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator CoRunFadeOut()
    {
        EnsureFadeController();
        bool fadeCompleted = fadeController == null;
        if (fadeController == null)
        {
            yield break;
        }

        yield return null;
        if (fadeImage != null && fadeOutSprite != null && fadeOutSprite.texture != null)
        {
            fadeImage.UpdateMaskTexture(fadeOutSprite.texture);
        }

        fadeController.FadeIn(Mathf.Max(0.01f, fadeOutDurationSeconds), () => fadeCompleted = true);
        while (!fadeCompleted)
        {
            yield return null;
        }
    }

    private void ApplyInitialOverlayVisibility()
    {
        if (playManualCanvas != null)
        {
            playManualCanvas.SetActive(false);
        }

        if (ugCanvas != null)
        {
            ugCanvas.SetActive(false);
        }

        SetMainButtonsInteractable(true);
        EnsureOverlayExitButtonsInteractable();
    }

    private bool AnyOverlayVisible()
    {
        bool manualOpen = playManualCanvas != null && playManualCanvas.activeSelf;
        bool ugOpen = ugCanvas != null && ugCanvas.activeSelf;
        return manualOpen || ugOpen;
    }

    private void RefreshMainButtonsForOverlayState()
    {
        if (!isTransitioning)
        {
            SetMainButtonsInteractable(!AnyOverlayVisible());
        }

        EnsureOverlayExitButtonsInteractable();
    }

    private void EnsureOverlayExitButtonsInteractable()
    {
        if (playManualExitButton != null)
        {
            playManualExitButton.interactable = true;
        }

        if (ugExitButton != null)
        {
            ugExitButton.interactable = true;
        }
    }

    private void SetPlayManualVisible(bool visible)
    {
        if (visible && ugCanvas != null && ugCanvas.activeSelf)
        {
            ugCanvas.SetActive(false);
        }

        if (playManualCanvas != null)
        {
            playManualCanvas.SetActive(visible);
        }

        RefreshMainButtonsForOverlayState();
    }

    private void SetUgCanvasVisible(bool visible)
    {
        if (visible && playManualCanvas != null && playManualCanvas.activeSelf)
        {
            playManualCanvas.SetActive(false);
        }

        if (ugCanvas != null)
        {
            ugCanvas.SetActive(visible);
        }

        RefreshMainButtonsForOverlayState();
    }

    private void SetMainButtonsInteractable(bool interactable)
    {
        if (titleReturnButton != null) titleReturnButton.interactable = interactable;
        if (startButton != null) startButton.interactable = interactable;
        if (playManualOpenButton != null) playManualOpenButton.interactable = interactable;
    }

    private void ConfigureMainButtonPressFeedbackIfNeeded()
    {
        if (!enableTitleReturnPressFeedback)
        {
            return;
        }

        AttachPressFeedback(titleReturnButton);
        AttachPressFeedback(startButton);
    }

    private void AttachPressFeedback(Button button)
    {
        MenuImageButtonPressFeedback.AttachTo(button, titleReturnPressedScale, titleReturnPressedBrightness);
    }

    private void TryClearIncomingFade()
    {
        if (incomingFadeClearSeconds <= 0f)
        {
            return;
        }

        Fade fade = ResolveIncomingFade();
        if (fade == null)
        {
            return;
        }

        fade.FadeOut(Mathf.Max(0.01f, incomingFadeClearSeconds));
    }

    private static Fade ResolveIncomingFade()
    {
        Fade[] fades = FindObjectsByType<Fade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fades.Length; i++)
        {
            Fade candidate = fades[i];
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.gameObject.activeSelf)
            {
                candidate.gameObject.SetActive(true);
            }

            if (!candidate.enabled)
            {
                candidate.enabled = true;
            }

            return candidate;
        }

        return null;
    }

    private void EnsureFadeController()
    {
        if (fadeController != null || fadeCanvasPrefab == null)
        {
            return;
        }

        GameObject fadeObject = Instantiate(fadeCanvasPrefab);
        fadeObject.name = "FadeCanvas";
        DontDestroyOnLoad(fadeObject);
        EnsureFadeCanvasFrontMost(fadeObject);

        fadeController = fadeObject.GetComponent<Fade>();
        fadeImage = fadeObject.GetComponent<FadeImage>();
    }

    private static void EnsureFadeCanvasFrontMost(GameObject fadeObject)
    {
        if (fadeObject == null)
        {
            return;
        }

        Canvas[] canvases = fadeObject.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = FadeCanvasSortingOrder;
        }
    }

    private string ResolveStageName()
    {
        string resolved = string.IsNullOrWhiteSpace(stageName) ? "stage_01" : stageName.Trim();
        if (startButton == null)
        {
            return resolved;
        }

        MenuStartButtonTransitionSettings settings = startButton.GetComponent<MenuStartButtonTransitionSettings>();
        if (settings == null || !settings.UseOverrides)
        {
            return resolved;
        }

        if (!string.IsNullOrWhiteSpace(settings.StageName))
        {
            resolved = settings.StageName.Trim();
        }

        return resolved;
    }
}
