using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// game_stage_scene の Canvas 開閉とシーン遷移専用。
/// 実装してよい: パネル開閉、遷移ボタン挙動、フェード付きシーン遷移。
/// 実装してはいけない: ボリューム調整や音量計算などの音響設定ロジック。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game01TransitionManager : MonoBehaviour
{
    [Header("開閉対象: Canvas")]
    [SerializeField, Tooltip("ゲームオーバーパネルのルート。未設定時は開閉しません。")]
    private GameObject gameOverPanelRoot;
    [SerializeField, Tooltip("ゲームクリアパネルのルート。未設定時は開閉しません。")]
    private GameObject gameClearedPanelRoot;
    [SerializeField, Tooltip("ステージ開始演出のルート。未設定時は開閉しません。")]
    private GameObject stageStartOverlayRoot;

    [Header("遷移先: シーン名")]
    [SerializeField, Tooltip("リトライ時の遷移先シーン名。")]
    private string retrySceneName = "game_stage_scene";
    [SerializeField, Tooltip("メニュー戻り時の遷移先シーン名。")]
    private string menuSceneName = "menu_scene";

    [Header("遷移演出: Retry")]
    [SerializeField, Tooltip("リトライ時フェードアウト秒数。")]
    private float retryFadeOutSeconds = 1f;
    [SerializeField, Tooltip("リトライ時フェードイン秒数。")]
    private float retryFadeInSeconds = 0.2f;

    [Header("遷移演出: Menu")]
    [SerializeField, Tooltip("メニュー戻り時フェードアウト秒数。")]
    private float menuFadeOutSeconds = 1f;
    [SerializeField, Tooltip("メニュー戻り時フェードイン秒数。")]
    private float menuFadeInSeconds = 0.2f;

    [Header("参照: SE管理")]
    [SerializeField, Tooltip("GameOver ボタンSEを再生する SE 管理。未設定時は無音。")]
    private Game01SeManager game01SeManager;

    [Header("ボタン遷移遅延: GameOver")]
    [SerializeField, Tooltip("Retry クリック後、遷移を開始するまでの待機秒。SE の頭出し用。")]
    private float retryTransitionDelaySeconds = 0.5f;
    [SerializeField, Tooltip("GiveUp クリック後、遷移を開始するまでの待機秒。SE の頭出し用。")]
    private float giveUpTransitionDelaySeconds = 0.05f;

    private bool isTransitioning;

    public void ShowGameOverPanel()
    {
        SetCanvasActive(gameOverPanelRoot, true);
    }

    public void ShowGameClearedPanel()
    {
        SetCanvasActive(gameClearedPanelRoot, true);
        SoundSettingsManager.Instance?.MarkGame01Cleared();
        SteamAchievementController.TryUnlock(SteamAchievementIds.Game01_02);
    }

    public void ShowStageStartOverlay()
    {
        SetCanvasActive(stageStartOverlayRoot, true);
    }

    public void HideStageStartOverlay()
    {
        SetCanvasActive(stageStartOverlayRoot, false);
    }

    public void TransitionToRetryScene()
    {
        LoadSceneWithFade(retrySceneName, retryFadeOutSeconds, retryFadeInSeconds);
    }

    public void TransitionToMenuScene()
    {
        LoadSceneWithFade(menuSceneName, menuFadeOutSeconds, menuFadeInSeconds);
    }

    // GameOverPanel/RetryButton の OnClick から呼ぶ
    public void OnClickGameOverRetryButton()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(TransitionAfterButtonSe(
            true,
            retryTransitionDelaySeconds,
            retrySceneName,
            retryFadeOutSeconds,
            retryFadeInSeconds));
    }

    // GameOverPanel/GiveUpButton の OnClick から呼ぶ
    public void OnClickGameOverGiveUpButton()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(TransitionAfterButtonSe(
            false,
            giveUpTransitionDelaySeconds,
            menuSceneName,
            menuFadeOutSeconds,
            menuFadeInSeconds));
    }

    private static void SetCanvasActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private IEnumerator TransitionAfterButtonSe(
        bool isRetryButton,
        float delaySeconds,
        string destinationScene,
        float fadeOutSeconds,
        float fadeInSeconds)
    {
        isTransitioning = true;
        PlayGameOverButtonSe(isRetryButton);
        float wait = Mathf.Max(0f, delaySeconds);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        LoadSceneWithFade(destinationScene, fadeOutSeconds, fadeInSeconds);
    }

    private void PlayGameOverButtonSe(bool isRetryButton)
    {
        if (game01SeManager == null)
        {
            return;
        }

        if (isRetryButton)
        {
            game01SeManager.PlayGameOverRetryButtonSe();
        }
        else
        {
            game01SeManager.PlayGameOverGiveUpButtonSe();
        }
    }

    private static void LoadSceneWithFade(string sceneName, float fadeOutSeconds, float fadeInSeconds)
    {
        string trimmedSceneName = string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
        if (string.IsNullOrEmpty(trimmedSceneName))
        {
            return;
        }

        FadeManager fadeManager = EnsureFadeManager();
        if (fadeManager != null)
        {
            fadeManager.LoadScene(trimmedSceneName, Mathf.Max(0f, fadeOutSeconds), Mathf.Max(0f, fadeInSeconds));
            return;
        }

        SceneManager.LoadScene(trimmedSceneName);
    }

    private static FadeManager EnsureFadeManager()
    {
        FadeManager fadeManager = FindAnyObjectByType<FadeManager>();
        if (fadeManager == null)
        {
            GameObject fadeManagerObject = new GameObject("FadeManager");
            fadeManager = fadeManagerObject.AddComponent<FadeManager>();
        }

        fadeManager.DebugMode = false;
        return fadeManager;
    }

}
