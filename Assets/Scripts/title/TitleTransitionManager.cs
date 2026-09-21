using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class TitleTransitionManager : MonoBehaviour
{
    private static readonly Color LockedHoverOverlayColor = new Color(84f / 255f, 84f / 255f, 84f / 255f, 1f);

    [Header("参照: ボタン")]
    [SerializeField, Tooltip("Game01Button。押下時にゲーム開始遷移を実行します。")]
    private Button game01Button;
    [SerializeField, Tooltip("Game02Button。押下時に menu02_scene へ遷移します。")]
    private Button game02Button;
    [SerializeField, Tooltip("Game03Button。押下時に menu03_scene へ遷移します。")]
    private Button game03Button;
    [SerializeField, Tooltip("ConfigButton。押下時に ConfigCanvas を開きます。")]
    private Button configOpenButton;
    [SerializeField, Tooltip("EXITButton。押下時に ConfigCanvas を閉じます。")]
    private Button configExitButton;
    [SerializeField, Tooltip("CreditsButton。押下時に CreditsCanvas を表示します。")]
    private Button creditsOpenButton;
    [SerializeField, Tooltip("CreditsCanvas 内の EXITButton。押下時に CreditsCanvas を非表示にします。")]
    private Button creditsExitButton;
    [SerializeField, Tooltip("CreditsPanel 内の KomayamaButton。押下時に komayamaUrl をブラウザで開きます。")]
    private Button creditsKomayamaButton;
    [SerializeField, Tooltip("EndButton。押下時にアプリケーションを終了します。")]
    private Button endButton;

    [Header("参照: 管理オブジェクト")]
    [SerializeField, Tooltip("開閉対象の ConfigCanvas。")]
    private GameObject configCanvas;
    [SerializeField, Tooltip("開閉対象の CreditsCanvas。")]
    private GameObject creditsCanvas;
    [SerializeField, Tooltip("SE管理オブジェクト。遷移SE/開閉SEを鳴らします。")]
    private TitleSeManager titleSeManager;

    [Header("シーン遷移設定: Game01Button")]
    [SerializeField, Tooltip("Game01Button 押下時の遷移先シーン名。")]
    private string game01SceneName = "menu_scene";

    [Header("シーン遷移設定: Game02Button")]
    [SerializeField, Tooltip("Game02Button 押下時の遷移先シーン名。")]
    private string game02SceneName = "menu02_scene";

    [Header("シーン遷移設定: Game03Button")]
    [SerializeField, Tooltip("Game03Button 押下時の遷移先シーン名。")]
    private string game03SceneName = "menu03_scene";

    [Header("タイトル解放")]
    [SerializeField, Tooltip("Game02Button 子の HoverOverlay。未解放時 #545454。")]
    private Graphic game02HoverOverlay;
    [SerializeField, Tooltip("Game03Button 子の HoverOverlay。未解放時 #545454。")]
    private Graphic game03HoverOverlay;

    [Header("遷移演出設定")]
    [SerializeField, Tooltip("遷移前の待機秒数。")]
    private float transitionWaitSeconds = 0.2f;
    [SerializeField, Tooltip("フェードアウト演出秒数。")]
    private float fadeOutDurationSeconds = 1.5f;
    [SerializeField, Tooltip("前シーン遷移で残った黒フェードを戻す秒数（0以下で無効）。")]
    private float incomingFadeClearSeconds = 0.25f;
    [SerializeField, Tooltip("遷移時に生成する FadeCanvas プレハブ。")]
    private GameObject fadeCanvasPrefab;
    [SerializeField, Tooltip("フェードアウト時に使うマスク画像。未設定なら標準表示。")]
    private Sprite fadeOutSprite;

    [Header("ConfigCanvas 表示設定")]
    [SerializeField, Tooltip("ConfigCanvas の背景暗転アルファ。")]
    [Range(0f, 1f)]
    private float configCanvasBgAlpha = 0.82f;
    [SerializeField, Tooltip("ConfigCanvas を前面に固定するソート順。")]
    private int configCanvasSortingOrder = 1000;

    [Header("CreditsCanvas")]
    [SerializeField, Tooltip("KomayamaButton 押下時に Application.OpenURL で開く URL。Inspector で設定。")]
    private string komayamaUrl;

    private const int FadeCanvasSortingOrder = 2000;
    private const bool EnableTransitionDebugLog = false;

    private bool isTransitioning;
    private Fade fadeController;
    private FadeImage fadeImage;
    private Color game02HoverOverlayDefaultColor = Color.white;
    private Color game03HoverOverlayDefaultColor = Color.white;
    private bool hoverOverlayDefaultColorsCaptured;

    private void Awake()
    {
        Log("Awake begin");
        RefreshTitleGameButtonsInteractable();
        EnsureConfigCanvasVisuals();
        SetConfigCanvasVisible(false);
        SetCreditsCanvasVisible(false);
        TryClearIncomingFade();
        Log("Awake end");
    }

    public void OnClickGame01Button()
    {
        if (isTransitioning)
        {
            return;
        }

        Log("OnClickGame01Button");
        StartCoroutine(TransitionFromGame01Button());
    }

    public void OnClickGame02Button()
    {
        if (isTransitioning || !CanLaunchGame02())
        {
            return;
        }

        Log("OnClickGame02Button");
        StartCoroutine(TransitionToScene(game02SceneName));
    }

    public void OnClickGame03Button()
    {
        if (isTransitioning || !CanLaunchGame03())
        {
            return;
        }

        Log("OnClickGame03Button");
        StartCoroutine(TransitionToScene(game03SceneName));
    }

    public void OnClickConfigOpenButton()
    {
        if (isTransitioning)
        {
            return;
        }

        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.ConfigToggle);
        SetCreditsCanvasVisible(false);
        SetConfigCanvasVisible(true);
    }

    public void OnClickConfigExitButton()
    {
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.ConfigToggle);
        SetConfigCanvasVisible(false);
    }

    public void OnClickCreditsOpenButton()
    {
        if (isTransitioning)
        {
            return;
        }

        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.ConfigToggle);
        SetConfigCanvasVisible(false);
        SetCreditsCanvasVisible(true);
    }

    public void OnClickCreditsExitButton()
    {
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.ConfigToggle);
        SetCreditsCanvasVisible(false);
    }

    public void OnClickKomayamaButton()
    {
        if (string.IsNullOrWhiteSpace(komayamaUrl))
        {
            Debug.LogWarning("[TitleTransitionManager] komayamaUrl が未設定です。");
            return;
        }

        Application.OpenURL(komayamaUrl.Trim());
    }

    public void OnClickEndButton()
    {
        if (isTransitioning)
        {
            return;
        }

        // タイトルにはスロット／ゲーム進行セーブが無い。SE 再生後に Quit。
        StartCoroutine(CoQuitAfterSe());
    }

    /// <summary>
    /// はじめから／続きからのクラフト遷移。SE＋フェードのあと LoadCanvas 経由でロードする。
    /// </summary>
    public void BeginCraftSceneTransition(string sceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[TitleTransitionManager] craft sceneName is empty");
            return;
        }

        StartCoroutine(TransitionToCraftScene(sceneName.Trim()));
    }

    public bool IsTransitioning => isTransitioning;

    private IEnumerator CoQuitAfterSe()
    {
        isTransitioning = true;
        SetButtonsInteractable(false);

        float seLength;
        if (TitleSeManager.TryPlay(titleSeManager, TitleSeCue.AppQuit, out seLength) && seLength > 0f)
        {
            yield return new WaitForSecondsRealtime(seLength);
        }

        QuitApplication();
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator TransitionFromGame01Button()
    {
        isTransitioning = true;
        SetButtonsInteractable(false);
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.TransitionStart);
        float transitionBegin = Time.realtimeSinceStartup;
        Log($"TransitionFromGame01 begin: target={game01SceneName}");

        float wait = Mathf.Max(0f, transitionWaitSeconds);
        if (wait > 0f)
        {
            Log($"TransitionFromGame01 wait start: {wait:0.###}s");
            yield return new WaitForSeconds(wait);
            Log($"TransitionFromGame01 wait end: elapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        }

        yield return StartCoroutine(CoRunFadeOut());

        string nextScene = string.IsNullOrWhiteSpace(game01SceneName) ? null : game01SceneName.Trim();
        if (string.IsNullOrEmpty(nextScene))
        {
            isTransitioning = false;
            SetButtonsInteractable(true);
            yield break;
        }

        Log($"TransitionFromGame01 LoadScene: {nextScene}, totalElapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator TransitionToScene(string destinationSceneName)
    {
        isTransitioning = true;
        SetButtonsInteractable(false);
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.TransitionStart);
        float transitionBegin = Time.realtimeSinceStartup;
        Log($"TransitionToScene begin: destination={destinationSceneName}");

        float wait = Mathf.Max(0f, transitionWaitSeconds);
        if (wait > 0f)
        {
            Log($"TransitionToScene wait start: {wait:0.###}s");
            yield return new WaitForSeconds(wait);
            Log($"TransitionToScene wait end: elapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        }

        yield return StartCoroutine(CoRunFadeOut());

        SceneTransitionContext.DestinationSceneName = null;

        string nextScene = string.IsNullOrWhiteSpace(destinationSceneName) ? null : destinationSceneName.Trim();
        if (string.IsNullOrEmpty(nextScene))
        {
            isTransitioning = false;
            SetButtonsInteractable(true);
            yield break;
        }

        Log($"TransitionToScene LoadScene: {nextScene}, totalElapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator TransitionToCraftScene(string sceneName)
    {
        isTransitioning = true;
        SetButtonsInteractable(false);
        TitleSeManager.TryPlay(titleSeManager, TitleSeCue.TransitionStart);
        float transitionBegin = Time.realtimeSinceStartup;
        Log($"TransitionToCraft begin: target={sceneName}");

        float wait = Mathf.Max(0f, transitionWaitSeconds);
        if (wait > 0f)
        {
            Log($"TransitionToCraft wait start: {wait:0.###}s");
            yield return new WaitForSeconds(wait);
            Log($"TransitionToCraft wait end: elapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        }

        yield return StartCoroutine(CoRunFadeOut());

        Log($"TransitionToCraft LoadCraftScene: {sceneName}, totalElapsed={Time.realtimeSinceStartup - transitionBegin:0.###}s");
        KomayamaCraftSceneLoader.LoadCraftScene(sceneName);
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
        if (fadeImage != null && fadeOutSprite != null)
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

    private void SetConfigCanvasVisible(bool visible)
    {
        if (configCanvas == null)
        {
            return;
        }

        EnsureConfigCanvasVisuals();
        configCanvas.SetActive(visible);

        if (!isTransitioning)
        {
            RefreshOverlayButtonsInteractable();
        }
    }

    private void SetCreditsCanvasVisible(bool visible)
    {
        if (creditsCanvas == null)
        {
            return;
        }

        creditsCanvas.SetActive(visible);
        if (!isTransitioning)
        {
            RefreshOverlayButtonsInteractable();
        }
    }

    private void RefreshOverlayButtonsInteractable()
    {
        bool overlayOpen = IsConfigCanvasVisible() || IsCreditsCanvasVisible();
        SetButtonsInteractable(!overlayOpen);
    }

    private bool IsConfigCanvasVisible()
    {
        return configCanvas != null && configCanvas.activeSelf;
    }

    private bool IsCreditsCanvasVisible()
    {
        return creditsCanvas != null && creditsCanvas.activeSelf;
    }

    private void EnsureConfigCanvasVisuals()
    {
        if (configCanvas == null)
        {
            return;
        }
        // ConfigCanvas 側の Inspector 設定値はここで上書きしない。
    }

    private void SetButtonsInteractable(bool interactable)
    {
        SetTitleGameButtonsBlocked(!interactable);
        if (configOpenButton != null) configOpenButton.interactable = interactable;
        if (creditsOpenButton != null) creditsOpenButton.interactable = interactable;
        if (endButton != null) endButton.interactable = interactable;
        if (configExitButton != null && !interactable) configExitButton.interactable = true;
        if (creditsExitButton != null && !interactable) creditsExitButton.interactable = true;
        if (creditsKomayamaButton != null && !interactable) creditsKomayamaButton.interactable = true;
    }

    private bool CanLaunchGame02()
    {
        return game02Button != null && game02Button.IsInteractable();
    }

    private bool CanLaunchGame03()
    {
        return game03Button != null && game03Button.IsInteractable();
    }

    private void RefreshTitleGameButtonsInteractable()
    {
        SoundSettingsManager progress = SoundSettingsManager.Instance;

        if (game01Button != null)
        {
            game01Button.interactable = true;
        }

        if (game02Button != null)
        {
            game02Button.interactable = progress != null && progress.IsGame02UnlockedOnTitle();
        }

        if (game03Button != null)
        {
            game03Button.interactable = progress != null && progress.IsGame03UnlockedOnTitle();
        }

        RefreshLockedHoverOverlayColors(progress);
    }

    private void SetTitleGameButtonsBlocked(bool blocked)
    {
        if (blocked)
        {
            if (game01Button != null) game01Button.interactable = false;
            if (game02Button != null) game02Button.interactable = false;
            if (game03Button != null) game03Button.interactable = false;
            return;
        }

        RefreshTitleGameButtonsInteractable();
    }

    private void CaptureHoverOverlayDefaultColorsIfNeeded()
    {
        if (hoverOverlayDefaultColorsCaptured)
        {
            return;
        }

        if (game02HoverOverlay != null)
        {
            game02HoverOverlayDefaultColor = game02HoverOverlay.color;
        }

        if (game03HoverOverlay != null)
        {
            game03HoverOverlayDefaultColor = game03HoverOverlay.color;
        }

        hoverOverlayDefaultColorsCaptured = true;
    }

    private void RefreshLockedHoverOverlayColors(SoundSettingsManager progress)
    {
        CaptureHoverOverlayDefaultColorsIfNeeded();

        bool game02Unlocked = progress != null && progress.IsGame02UnlockedOnTitle();
        bool game03Unlocked = progress != null && progress.IsGame03UnlockedOnTitle();

        ApplyHoverOverlayColor(game02HoverOverlay, game02Unlocked, game02HoverOverlayDefaultColor);
        ApplyHoverOverlayColor(game03HoverOverlay, game03Unlocked, game03HoverOverlayDefaultColor);
    }

    private static void ApplyHoverOverlayColor(Graphic overlay, bool unlocked, Color defaultColor)
    {
        if (overlay == null)
        {
            return;
        }

        if (unlocked)
        {
            overlay.color = defaultColor;
            return;
        }

        Color locked = LockedHoverOverlayColor;
        locked.a = defaultColor.a;
        overlay.color = locked;
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

    private static void Log(string message)
    {
        if (!EnableTransitionDebugLog)
        {
            return;
        }

        Debug.Log($"[TitleTransition] t={Time.realtimeSinceStartup:0.###} {message}");
    }

    private void EnsureFadeController()
    {
        if (fadeController != null)
        {
            return;
        }

        if (fadeCanvasPrefab == null)
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fadeCanvasPrefab == null)
        {
            fadeCanvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fade/FadeCanvas.prefab");
        }

        if (fadeOutSprite == null)
        {
            fadeOutSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/fade/effect01.png");
        }
    }
#endif
}
