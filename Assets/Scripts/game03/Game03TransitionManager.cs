using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// game03_scene の Canvas 開閉と画面遷移専用マネージャ。
/// それ以外の機能（例: 音量調整ロジック）は本クラスに実装しないでください。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03TransitionManager : MonoBehaviour
{
    private const int FadeCanvasSortingOrder = 2000;

    [Header("開閉機能: ConfigCanvas")]
    [SerializeField, Tooltip("開閉対象の ConfigCanvas ルート。未設定時は開閉しません。")]
    private GameObject configCanvasRoot;

    [Header("遷移ボタン: StartButton")]
    [SerializeField, Tooltip("開始遷移に使うボタン。未設定なら自動遷移は行いません。")]
    private Button startButton;
    [SerializeField, Tooltip("StartButton 押下時の遷移先シーン名。")]
    private string startDestinationSceneName = "dialogue_scene";

    [Header("遷移ボタン: TitleReturnButton")]
    [SerializeField, Tooltip("タイトル戻り遷移に使うボタン。未設定なら自動遷移は行いません。")]
    private Button titleReturnButton;
    [SerializeField, Tooltip("TitleReturnButton 押下時の遷移先シーン名。")]
    private string titleReturnDestinationSceneName = "title_scene";

    [Header("遷移ボタン: PausePanel.ButtonMenu")]
    [SerializeField, Tooltip("PausePanel 内のメニューへ戻るボタン（game02 の Game02MenuReturnButton と同型）。")]
    private Button leaveToMenu03FromPausePanelButton;
    [SerializeField, Tooltip("ButtonMenu 押下時の遷移先シーン名。")]
    private string menu03SceneName = "menu03_scene";

    [Header("遷移演出設定")]
    [SerializeField, Tooltip("遷移 SE 再生後、フェード開始まで待機する秒数。")]
    private float transitionWaitSeconds = 0.5f;
    [SerializeField, Tooltip("フェードアウト秒数。")]
    private float fadeOutSeconds = 1f;
    [SerializeField, Tooltip("FadeCanvas プレハブ。未設定なら FadeManager にフォールバック。")]
    private GameObject fadeCanvasPrefab;
    [SerializeField, Tooltip("フェードアウト時に使うマスク画像。未設定ならプレハブ既定。")]
    private Sprite fadeOutSprite;

    [Header("ボタン押下演出")]
    [SerializeField, Tooltip("有効時、遷移ボタンに MenuImageButtonPressFeedback を同期します（game02 と同様）。")]
    private bool enableButtonPressFeedback = true;
    [SerializeField, Tooltip("押下時のスケール倍率。")]
    private float pressedScale = 0.94f;
    [SerializeField, Tooltip("押下時の明るさ倍率。")]
    private float pressedBrightness = 0.84f;

    private Fade fadeController;
    private FadeImage fadeImage;
    private bool isTransitioning;

    public bool IsSceneTransitionInProgress => isTransitioning;

    private void Awake()
    {
        ConfigureButtonFeedback();
    }

    private void OnEnable()
    {
        ConfigureButtonFeedback();
    }

    public void SetConfigCanvasActive(bool active)
    {
        if (configCanvasRoot == null)
        {
            return;
        }

        configCanvasRoot.SetActive(active);
    }

    public void OpenConfigCanvas()
    {
        SetConfigCanvasActive(true);
    }

    public void CloseConfigCanvas()
    {
        SetConfigCanvasActive(false);
    }

    public void OnClickStartButton()
    {
        TransitionToScene(startDestinationSceneName);
    }

    public void OnClickTitleReturnButton()
    {
        Game03SeManager.TryGet()?.PlayByCue(Game03SeCue.TitleReturnButtonClick);
        TransitionToScene(titleReturnDestinationSceneName);
    }

    public void TransitionToScene(string destinationSceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        string trimmed = string.IsNullOrWhiteSpace(destinationSceneName) ? null : destinationSceneName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        StartCoroutine(CoTransition(trimmed));
    }

    public void TransitionToDialogueForGame03ClearEnd()
    {
        SceneTransitionContext.DialogueStreamingSceneNameOverride = "game03_scene";
        SceneTransitionContext.StageName = "ed";
        SceneTransitionContext.DestinationSceneName = "menu03_scene";
        TransitionToScene("dialogue_scene");
    }

    /// <summary>
    /// PausePanel.ButtonMenu の OnClick から呼ぶ（game02 の RequestLeaveToMenuFromPausePanel 相当）。
    /// </summary>
    public void TransitionToMenu03FromPausePanel()
    {
        string destination = string.IsNullOrWhiteSpace(menu03SceneName) ? "menu03_scene" : menu03SceneName.Trim();
        TransitionToScene(destination);
    }

    private IEnumerator CoTransition(string sceneName)
    {
        isTransitioning = true;
        SetRouteButtonsInteractable(false);
        Game03SeManager.TryGet()?.PlayPresentationCue(Game03SeCue.TransitionStart);

        float wait = Mathf.Max(0f, transitionWaitSeconds);
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        if (fadeCanvasPrefab != null)
        {
            yield return StartCoroutine(CoRunFadeOut(Mathf.Max(0.01f, fadeOutSeconds)));
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        FadeManager fadeManager = EnsureFadeManager();
        if (fadeManager != null)
        {
            fadeManager.LoadScene(sceneName, Mathf.Max(0f, fadeOutSeconds), 0.2f);
            yield break;
        }

        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator CoRunFadeOut(float durationSeconds)
    {
        EnsureFadeController();
        bool completed = fadeController == null;
        if (fadeController == null)
        {
            yield break;
        }

        yield return null;
        if (fadeImage != null && fadeOutSprite != null && fadeOutSprite.texture != null)
        {
            fadeImage.UpdateMaskTexture(fadeOutSprite.texture);
        }

        fadeController.FadeIn(durationSeconds, () => completed = true);
        while (!completed)
        {
            yield return null;
        }
    }

    private void EnsureFadeController()
    {
        if (fadeCanvasPrefab == null)
        {
            return;
        }

        if (fadeController != null)
        {
            return;
        }

        Fade existing = ResolveExistingFadeInstance();
        if (existing != null)
        {
            fadeController = existing;
            fadeImage = existing.GetComponent<FadeImage>();
            EnsureFadeCanvasReady(existing.gameObject);
            return;
        }

        GameObject fadeObject = Instantiate(fadeCanvasPrefab);
        fadeObject.name = "FadeCanvas";
        DontDestroyOnLoad(fadeObject);
        EnsureFadeCanvasReady(fadeObject);

        fadeController = fadeObject.GetComponent<Fade>();
        fadeImage = fadeObject.GetComponent<FadeImage>();
    }

    private static Fade ResolveExistingFadeInstance()
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

    private static void EnsureFadeCanvasReady(GameObject fadeObject)
    {
        if (fadeObject == null)
        {
            return;
        }

        EnsureFadeCanvasFrontMost(fadeObject);

        if (fadeObject.transform is RectTransform rectTransform)
        {
            rectTransform.localScale = Vector3.one;
        }

        FadeImage fadeImageComponent = fadeObject.GetComponent<FadeImage>();
        if (fadeImageComponent != null && !fadeImageComponent.enabled)
        {
            fadeImageComponent.enabled = true;
        }
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

    private static FadeManager EnsureFadeManager()
    {
        FadeManager manager = FindAnyObjectByType<FadeManager>();
        if (manager == null)
        {
            GameObject go = new GameObject("FadeManager");
            manager = go.AddComponent<FadeManager>();
        }

        manager.DebugMode = false;
        return manager;
    }

    public void ConfigureLeaveButtonPressFeedback(Button button)
    {
        if (!enableButtonPressFeedback)
        {
            return;
        }

        MenuImageButtonPressFeedback.AttachTo(button, pressedScale, pressedBrightness);
    }

    private void ConfigureButtonFeedback()
    {
        if (!enableButtonPressFeedback)
        {
            return;
        }

        MenuImageButtonPressFeedback.AttachTo(startButton, pressedScale, pressedBrightness);
        MenuImageButtonPressFeedback.AttachTo(titleReturnButton, pressedScale, pressedBrightness);
        MenuImageButtonPressFeedback.AttachTo(leaveToMenu03FromPausePanelButton, pressedScale, pressedBrightness);
    }

    private void SetRouteButtonsInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }

        if (titleReturnButton != null)
        {
            titleReturnButton.interactable = interactable;
        }

        if (leaveToMenu03FromPausePanelButton != null)
        {
            leaveToMenu03FromPausePanelButton.interactable = interactable;
        }
    }
}
