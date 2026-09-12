using TMPro;
using UnityEngine;

/// <summary>
/// game03: ラン終了リザルト（GameOverPanel → ResultPanel、またはクリア演出後の ResultPanel）。
/// 仕様: <c>spec/game03/game03_game_over_spec.md</c> / <c>spec/game03/ゲームクリア演出.txt</c>
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03GameOverPresentation : MonoBehaviour
{
    public enum ResultPanelReturnDestination
    {
        Menu03,
        DialogueAfterGame03Clear
    }

    private enum PresentationPhase
    {
        None,
        GameOverPanel,
        ResultPanel
    }

    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03MetaRunEndCoordinator metaRunEndCoordinator;
    [SerializeField] private Game03BgmManager bgmManager;
    [SerializeField] private Game03SeManager seManager;
    [SerializeField] private Game03TransitionManager transitionManager;
    [SerializeField] private Game03ResultPanelPopulator resultPanelPopulator;

    [SerializeField] private GameObject gameOverPanelRoot;
    [SerializeField] private GameObject resultPanelRoot;
    [SerializeField] private TextMeshProUGUI gameOverReturnText;
    [SerializeField] private TextMeshProUGUI resultReturnText;

    [SerializeField, Min(0.01f)] private float returnTextBlinkPeriodSeconds = 3f;
    [SerializeField, Min(0.05f)] private float confirmDoubleTapIntervalSeconds = 0.28f;

    private readonly Game03ReturnTextPrompt gameOverReturnPrompt = new Game03ReturnTextPrompt();
    private readonly Game03ReturnTextPrompt resultReturnPrompt = new Game03ReturnTextPrompt();

    private PresentationPhase phase;
    private bool gameOverHandled;
    private ResultPanelReturnDestination resultReturnDestination = ResultPanelReturnDestination.Menu03;

    private void Awake()
    {
        ApplyHiddenAtStart();
    }

    private void Update()
    {
        switch (phase)
        {
            case PresentationPhase.GameOverPanel:
                TickGameOverPanel();
                break;
            case PresentationPhase.ResultPanel:
                TickResultPanel();
                break;
        }
    }

    /// <summary>プレイヤー HP が 0 になったとき <see cref="Game03UnitManager"/> から 1 回だけ呼ぶ。</summary>
    public void NotifyPlayerDefeated()
    {
        if (gameOverHandled)
        {
            return;
        }

        if (Game03RunSessionState.EndedByClear)
        {
            return;
        }

        gameOverHandled = true;
        if (!Game03RunSessionState.EndedByGameOver)
        {
            Game03RunSessionState.MarkGameOver();
        }

        DismissBlockingPanels();

        if (game03Manager != null)
        {
            game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
            game03Manager.SetGameplayElapsedAccumulationSuppressed(true);
        }

        metaRunEndCoordinator?.NotifyGameOver();

        bgmManager?.StopBgm();
        seManager?.PlayPresentationCue(Game03SeCue.GameOver);
        bgmManager?.PlayGameOverPresentationBgm();

        if (gameOverPanelRoot != null)
        {
            gameOverPanelRoot.SetActive(true);
            BringPanelToFront(gameOverPanelRoot.transform);
        }
        else
        {
            Debug.LogWarning("[Game03GameOverPresentation] gameOverPanelRoot が未設定のため GameOverPanel を表示できません。", this);
        }

        gameOverReturnPrompt.Begin(
            gameOverReturnText,
            returnTextBlinkPeriodSeconds,
            confirmDoubleTapIntervalSeconds,
            Game03ReturnTextPrompt.ConfirmInputMode.SinglePress);
        phase = PresentationPhase.GameOverPanel;
    }

    private static void DismissBlockingPanels()
    {
        Game03LevelUpManager levelUp = FindAnyObjectByType<Game03LevelUpManager>(FindObjectsInactive.Include);
        levelUp?.DismissForGameOver();

        Game03UnitUgPresentationController unitUg =
            FindAnyObjectByType<Game03UnitUgPresentationController>(FindObjectsInactive.Include);
        unitUg?.ForceCloseImmediate();

        Game03BossUgPresentationController bossUg =
            FindAnyObjectByType<Game03BossUgPresentationController>(FindObjectsInactive.Include);
        bossUg?.ForceCloseImmediate();
    }

    private static void BringPanelToFront(Transform panelRoot)
    {
        if (panelRoot == null || panelRoot.parent == null)
        {
            return;
        }

        panelRoot.SetAsLastSibling();
    }

    /// <summary>タイムクリア演出後。GameClearedPanel を閉じたあと ResultPanel を開く（戻り先は dialogue_scene 経由）。</summary>
    public void OpenResultPanelAfterGameClear()
    {
        OpenResultPanel(ResultPanelReturnDestination.DialogueAfterGame03Clear);
    }

    /// <summary>リザルト表示のみ更新（ReturnText 待ちは呼び出し側）。</summary>
    public void PopulateResultPanelForRunEnd()
    {
        resultPanelPopulator?.PopulateFromCurrentRun();
    }

    private void TickGameOverPanel()
    {
        gameOverReturnPrompt.TickBlink();
        if (!gameOverReturnPrompt.TryConsumeConfirm())
        {
            return;
        }

        seManager?.PlayPresentationCue(Game03SeCue.GameOverPanelAdvance);
        gameOverReturnPrompt.End();

        if (gameOverPanelRoot != null)
        {
            gameOverPanelRoot.SetActive(false);
        }

        OpenResultPanel(ResultPanelReturnDestination.Menu03);
    }

    private void OpenResultPanel(ResultPanelReturnDestination destination)
    {
        resultReturnDestination = destination;
        resultPanelPopulator?.PopulateFromCurrentRun();
        if (resultPanelRoot != null)
        {
            resultPanelRoot.SetActive(true);
            BringPanelToFront(resultPanelRoot.transform);
        }

        resultReturnPrompt.Begin(
            resultReturnText,
            returnTextBlinkPeriodSeconds,
            confirmDoubleTapIntervalSeconds,
            Game03ReturnTextPrompt.ConfirmInputMode.SinglePress);
        phase = PresentationPhase.ResultPanel;
    }

    private void TickResultPanel()
    {
        resultReturnPrompt.TickBlink();
        if (!resultReturnPrompt.TryConsumeConfirm())
        {
            return;
        }

        resultReturnPrompt.End();
        phase = PresentationPhase.None;

        seManager?.PlayPresentationCue(Game03SeCue.TransitionStart);
        if (transitionManager == null)
        {
            return;
        }

        if (resultReturnDestination == ResultPanelReturnDestination.DialogueAfterGame03Clear)
        {
            transitionManager.TransitionToDialogueForGame03ClearEnd();
        }
        else
        {
            transitionManager.TransitionToScene("menu03_scene");
        }
    }

    private void ApplyHiddenAtStart()
    {
        phase = PresentationPhase.None;
        if (gameOverPanelRoot != null)
        {
            gameOverPanelRoot.SetActive(false);
        }

        if (resultPanelRoot != null)
        {
            resultPanelRoot.SetActive(false);
        }
    }
}

/// <summary><see cref="Game03ElapsedTimeUiController"/> と同一の経過時間表示。</summary>
public static class Game03ElapsedTimeUiFormatter
{
    private const int MaxDisplaySeconds = (999 * 3600) + (59 * 60) + 59;

    public static string FormatElapsed(int seconds)
    {
        int clamped = Mathf.Clamp(seconds, 0, MaxDisplaySeconds);
        int hours = clamped / 3600;
        int minutes = (clamped % 3600) / 60;
        int secs = clamped % 60;

        if (hours > 0)
        {
            return $"{hours}h:{minutes:00}m:{secs:00}s";
        }

        return $"{minutes:00}m:{secs:00}s";
    }
}
