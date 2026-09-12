using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// menu_scene の Canvas 開閉とシーン遷移を担当します。
/// 実装してよい例: PlayManual の開閉、遷移ボタンに伴うフェードとロード。
/// 実装してはいけない例: マスター／BGM／SE の数値変更ロジック（MenuSceneController 側に置いてください）。
/// 本クラス以外で Canvas の表示やシーン遷移を追加する場合は、設計と重複しないか先に確認してください。
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuTransitionManager : MonoBehaviour
{
    [Header("参照: シーンコントローラ")]
    [SerializeField, Tooltip("イラストや PlayManual 補助を担当する MenuSceneController。")]
    private MenuSceneController menuSceneController;

    [Header("参照: 管理オブジェクト")]
    [SerializeField, Tooltip("SE 管理。遷移と開閉操作の SE を再生します。")]
    private MenuSeManager menuSeManager;

    [Header("参照: ボタン")]
    [SerializeField, Tooltip("スタートボタン。会話シーン経由のゲーム開始遷移。")]
    private Button startButton;
    [SerializeField, Tooltip("タイトルへ戻るボタン。menu02 の TitleReturn と同じ挙動（専用SE＋コンテキストクリア）。")]
    private Button titleReturnButton;
    [SerializeField, Tooltip("Game02（メニュー02）へ遷移するボタン。未設定なら無視します。")]
    private Button game02Button;
    [SerializeField, Tooltip("遊び方を開くボタン。")]
    private Button playManualOpenButton;
    [SerializeField, Tooltip("遊び方を閉じるボタン。")]
    private Button playManualExitButton;

    [Header("開閉対象: Canvas")]
    [SerializeField, Tooltip("遊び方のルート Canvas。")]
    private GameObject playManualCanvas;

    [Header("シーン遷移設定: スタート")]
    [SerializeField, Tooltip("スタート後に最初に読み込む会話シーン名。")]
    private string dialogueSceneName = "dialogue_scene";
    [SerializeField, Tooltip("会話シーンのあとで遷移する先シーン名（MenuStartButtonTransitionSettings で上書き可）。")]
    private string startDestinationSceneName = "game_stage_scene";
    [SerializeField, Tooltip("SceneTransitionContext に渡す既定のステージ名。")]
    private string stageName = "stage_01";

    [Header("シーン遷移設定: その他")]
    [SerializeField, Tooltip("Game02 ボタン押下時の遷移先シーン名。")]
    private string menu02SceneName = "menu02_scene";
    [SerializeField, Tooltip("タイトル戻りボタン押下時の遷移先シーン名。")]
    private string titleSceneName = "title_scene";

    [Header("遷移演出設定")]
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

    [Header("ボタン押下演出（任意）")]
    [SerializeField, Tooltip("画像ボタンの押下演出を付与するか。")]
    private bool enableImageButtonPressFeedback = true;
    [SerializeField, Tooltip("押下時のスケール倍率。")]
    private float imageButtonPressedScale = 0.94f;
    [SerializeField, Tooltip("押下時の明るさ倍率。")]
    private float imageButtonPressedBrightness = 0.84f;

    private const int FadeCanvasSortingOrder = 2000;
    private const bool EnableTransitionDebugLog = false;

    private bool isTransitioning;
    private Fade fadeController;
    private FadeImage fadeImage;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        Log("Awake begin");
        TryClearIncomingFade();
        menuSceneController?.BindMenuTransitionManager(this);
        SetOverlayCanvasesVisibleOnBoot();
        ConfigureImageButtonPressFeedbackIfNeeded();
        Log("Awake end");
    }

    public void ConfigureImageButtonPressFeedbackIfNeeded()
    {
        if (!enableImageButtonPressFeedback)
        {
            return;
        }

        AttachPressFeedback(startButton);
        AttachPressFeedback(titleReturnButton);
        AttachPressFeedback(game02Button);
        AttachPressFeedback(playManualOpenButton);
    }

    private void AttachPressFeedback(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;

        MenuImageButtonPressFeedback feedback = button.GetComponent<MenuImageButtonPressFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<MenuImageButtonPressFeedback>();
        }

        feedback.Configure(imageButtonPressedScale, imageButtonPressedBrightness);
    }

    private void TryClearIncomingFade()
    {
        Log($"TryClearIncomingFade begin (seconds={incomingFadeClearSeconds:0.###})");
        if (incomingFadeClearSeconds <= 0f)
        {
            Log("TryClearIncomingFade skipped: seconds <= 0");
            return;
        }

        Fade fade = ResolveIncomingFade();
        if (fade == null)
        {
            Log("TryClearIncomingFade skipped: Fade not found");
            return;
        }

        Log($"TryClearIncomingFade execute: target={fade.gameObject.name}, active={fade.gameObject.activeSelf}, enabled={fade.enabled}");
        fade.FadeOut(Mathf.Max(0.01f, incomingFadeClearSeconds));
        Log("TryClearIncomingFade requested FadeOut");
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

    private void SetOverlayCanvasesVisibleOnBoot()
    {
        if (playManualCanvas != null)
        {
            playManualCanvas.SetActive(false);
        }

        SetOverlayMenuRowInteractable(true);
    }

    public void OnClickStartButton()
    {
        if (isTransitioning)
        {
            return;
        }

        Log("OnClickStartButton");
        StartCoroutine(TransitionToStartDestinationCoroutine());
    }

    public void OnClickTitleReturnButton()
    {
        if (isTransitioning)
        {
            return;
        }

        Log("OnClickTitleReturnButton");
        menuSeManager?.PlayByCue(MenuSeCue.TitleReturnTransition);
        SceneTransitionContext.DestinationSceneName = null;
        SceneTransitionContext.StageName = null;
        StartCoroutine(TransitionToSceneCoroutine(titleSceneName, false));
    }

    public void OnClickPlayManualOpenButton()
    {
        if (isTransitioning)
        {
            return;
        }

        ClearLingeringFadeOverlayIfNeeded();

        menuSeManager?.PlayByCue(MenuSeCue.PlayManualToggle);
        menuSceneController?.ApplyPlayManualCanvasVisibility(true);
    }

    public void OnClickPlayManualExitButton()
    {
        menuSeManager?.PlayByCue(MenuSeCue.PlayManualToggle);
        menuSceneController?.ApplyPlayManualCanvasVisibility(false);
    }

    public void ClearLingeringFadeOverlayIfNeeded()
    {
        if (isTransitioning)
        {
            return;
        }

        GameObject fadeCanvasObject = GameObject.Find("FadeCanvas");
        if (fadeCanvasObject != null && fadeCanvasObject.activeInHierarchy)
        {
            fadeCanvasObject.SetActive(false);
        }
    }

    private IEnumerator TransitionToSceneCoroutine(string destinationSceneName, bool writeStageContext)
    {
        isTransitioning = true;
        SetMainNavButtonsInteractable(false);
        float transitionBegin = Time.realtimeSinceStartup;
        Log($"TransitionToScene begin: destination={destinationSceneName}, writeStageContext={writeStageContext}");

        float transitionWait = Mathf.Max(0f, transitionWaitSeconds);
        if (transitionWait > 0f)
        {
            Log($"TransitionToScene wait start: {transitionWait:0.###}s");
            yield return new WaitForSeconds(transitionWait);
            Log($"TransitionToScene wait end: elapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        }

        yield return StartCoroutine(CoRunFadeOut());

        if (writeStageContext)
        {
            SceneTransitionContext.StageName = stageName;
        }
        else
        {
            SceneTransitionContext.DestinationSceneName = null;
        }

        string nextScene = string.IsNullOrEmpty(destinationSceneName) ? dialogueSceneName : destinationSceneName;
        Log($"TransitionToScene LoadScene: {nextScene}, totalElapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator TransitionToStartDestinationCoroutine()
    {
        float transitionBegin = Time.realtimeSinceStartup;
        ResolveStartButtonTransitionContext(out string targetScene, out string stageContextName);
        bool hasRelayTarget = !string.IsNullOrEmpty(targetScene);
        bool hasDialogueScene = !string.IsNullOrWhiteSpace(dialogueSceneName);
        Log($"TransitionToStart begin: targetScene={targetScene}, stage={stageContextName}, hasRelayTarget={hasRelayTarget}, hasDialogueScene={hasDialogueScene}");
        if (!hasRelayTarget || !hasDialogueScene)
        {
            menuSeManager?.PlayByCue(MenuSeCue.TransitionStart);
            yield return StartCoroutine(TransitionToSceneCoroutine(dialogueSceneName, true));
            yield break;
        }

        isTransitioning = true;
        SetMainNavButtonsInteractable(false);
        menuSeManager?.PlayByCue(MenuSeCue.TransitionStart);

        float transitionWait = Mathf.Max(0f, transitionWaitSeconds);
        if (transitionWait > 0f)
        {
            Log($"TransitionToStart wait start: {transitionWait:0.###}s");
            yield return new WaitForSeconds(transitionWait);
            Log($"TransitionToStart wait end: elapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        }

        yield return StartCoroutine(CoRunFadeOut());

        SceneTransitionContext.DestinationSceneName = targetScene;
        SceneTransitionContext.StageName = stageContextName;
        Log($"TransitionToStart LoadScene: {dialogueSceneName}, totalElapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        SceneManager.LoadScene(dialogueSceneName);
    }

    private IEnumerator CoRunFadeOut()
    {
        float fadeBegin = Time.realtimeSinceStartup;
        EnsureFadeController();
        bool fadeCompleted = fadeController == null;
        if (fadeController == null)
        {
            Log("CoRunFadeOut skipped: fadeController is null");
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
        Log($"CoRunFadeOut completed: elapsed={Time.realtimeSinceStartup - fadeBegin:0.###}s");
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

    private static void Log(string message)
    {
        if (!EnableTransitionDebugLog)
        {
            return;
        }

        Debug.Log($"[MenuTransition] t={Time.realtimeSinceStartup:0.###} {message}");
    }

    private void SetMainNavButtonsInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }

        if (titleReturnButton != null)
        {
            titleReturnButton.interactable = interactable;
        }

        if (game02Button != null)
        {
            game02Button.interactable = interactable;
        }

        SetOverlayMenuRowInteractable(interactable);
    }

    public void SetOverlayMenuRowInteractable(bool interactable)
    {
        if (playManualOpenButton != null)
        {
            playManualOpenButton.interactable = interactable;
        }

    }

    private void ResolveStartButtonTransitionContext(out string destinationSceneName, out string stageContextName)
    {
        destinationSceneName = string.IsNullOrWhiteSpace(startDestinationSceneName)
            ? null
            : startDestinationSceneName.Trim();
        stageContextName = string.IsNullOrWhiteSpace(stageName)
            ? null
            : stageName.Trim();

        if (startButton == null)
        {
            return;
        }

        MenuStartButtonTransitionSettings settings = startButton.GetComponent<MenuStartButtonTransitionSettings>();
        if (settings == null || !settings.UseOverrides)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.DestinationSceneName))
        {
            destinationSceneName = settings.DestinationSceneName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(settings.StageName))
        {
            stageContextName = settings.StageName.Trim();
        }
    }
}
