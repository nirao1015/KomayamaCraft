using UnityEngine;

/// <summary>
/// game03: <c>GameplayElapsedSeconds</c> が閾値に達したらゲームクリア演出を開始し、ゲーム内タイマーとゲームプレイを停止する。
/// 演出シーケンスは <see cref="Game03GameClearCeremonyController"/> に委譲する。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03GameClearPresentation : MonoBehaviour
{
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03MetaRunEndCoordinator runEndCoordinator;
    [SerializeField, Tooltip("未設定なら survivalClearGameplaySeconds のみ。設定時はデバッグ上書きを解決する。")]
    private Game03DebugManager game03DebugManager;
    [SerializeField, Tooltip("レーザー・船演出。未設定のときはパネルのみ即表示＋ポーズ（フォールバック）。")]
    private Game03GameClearCeremonyController gameClearCeremony;

    [SerializeField, Tooltip("PanelCanvas/GameClearedPanel 等のルート。")]
    private GameObject gameClearedPanelRoot;

    [SerializeField, Min(1f), Tooltip("この秒数（GameplayElapsedSeconds）で自動オープン。既定 1200 = 20 分。")]
    private float survivalClearGameplaySeconds = 1200f;

    private bool survivalClearHandled;

    private void Awake()
    {
        ApplyHiddenAtStart();
    }

    private void Update()
    {
        if (survivalClearHandled || game03Manager == null || Game03RunSessionState.IsRunEnded)
        {
            return;
        }

        if (!game03Manager.CanRunGameplay)
        {
            return;
        }

        float thresholdSeconds = ResolveSurvivalClearThresholdSeconds();
        if (game03Manager.GameplayElapsedSeconds < thresholdSeconds)
        {
            return;
        }

        survivalClearHandled = true;
        Game03RunSessionState.MarkCleared();
        runEndCoordinator?.NotifyRunCleared();
        FreezeGameplayForClear();
        if (gameClearCeremony != null)
        {
            gameClearCeremony.BeginCeremony();
        }
        else
        {
            PresentPanelOnlyFallback();
        }
    }

    private void ApplyHiddenAtStart()
    {
        if (gameClearedPanelRoot != null)
        {
            gameClearedPanelRoot.SetActive(false);
        }
    }

    private float ResolveSurvivalClearThresholdSeconds()
    {
        if (game03DebugManager != null)
        {
            return game03DebugManager.GetEffectiveSurvivalClearGameplaySeconds(survivalClearGameplaySeconds);
        }

        return survivalClearGameplaySeconds;
    }

    /// <summary>タイマー外からのクリア演出。メタ付与なし。演出は生存クリアと同一シーケンス。</summary>
    public void OpenPresentation()
    {
        Game03RunSessionState.MarkCleared();
        FreezeGameplayForClear();
        if (gameClearCeremony != null)
        {
            gameClearCeremony.BeginCeremony();
        }
        else
        {
            PresentPanelOnlyFallback();
        }
    }

    private void FreezeGameplayForClear()
    {
        if (game03Manager != null)
        {
            game03Manager.SetPaused(true, suppressGameplayPausePanelWhilePaused: true);
            game03Manager.SetGameplayElapsedAccumulationSuppressed(true);
        }
    }

    private void PresentPanelOnlyFallback()
    {
        if (gameClearedPanelRoot != null)
        {
            gameClearedPanelRoot.SetActive(true);
        }

        SteamAchievementController.TryUnlock(SteamAchievementIds.Game03_02);
        SoundSettingsManager.Instance?.MarkGame03Cleared();
    }
}
