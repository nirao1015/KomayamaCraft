using UnityEngine;

/// <summary>
/// ラン終了時にメタ通貨を加算して保存する（クリア／ゲームオーバーどちらか一方のみ）。
/// 付与: 固定ベース + 経験値実績換算（時間・フェーズ倍率、スマホ除外）+ 中ボス／Boss 獲得ポイント。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03MetaRunEndCoordinator : MonoBehaviour
{
    [SerializeField, Tooltip("クリア／ゲームオーバー共通の固定ベース。")]
    private long metaCurrencyRewardOnClear = 800;

    [SerializeField, Tooltip("人気→メタ換算用。未設定時はシーン内を探索。")]
    private Game03StatusManager statusManager;

    [SerializeField, Tooltip("経過時間・フェーズ取得用。未設定時はシーン内を探索。")]
    private Game03Manager game03Manager;

    private bool outcomeConsumed;

    public bool OutcomeConsumed => outcomeConsumed;

    /// <summary>ラン終了メタ付与の固定ベース（バランスログ等の projected 計算用）。</summary>
    public long MetaCurrencyBaseRewardOnRunEnd => metaCurrencyRewardOnClear;

    private void Awake()
    {
        if (statusManager == null)
        {
            statusManager = FindAnyObjectByType<Game03StatusManager>(FindObjectsInactive.Include);
        }

        if (game03Manager == null)
        {
            game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
        }
    }

    public void NotifyRunCleared()
    {
        GrantRunEndReward();
    }

    public void NotifyGameOver()
    {
        GrantRunEndReward();
    }

    private void GrantRunEndReward()
    {
        if (outcomeConsumed)
        {
            return;
        }

        outcomeConsumed = true;
        long total = metaCurrencyRewardOnClear + GetPopularityGrant() + GetBossBonusPoints();
        ApplyMetaCurrencyGrant(total);
    }

    private long GetPopularityGrant()
    {
        int lifetimeExperience = statusManager != null ? statusManager.LifetimeTotalExperienceEarned : 0;
        float elapsedSeconds = game03Manager != null ? game03Manager.GameplayElapsedSeconds : 0f;
        int phase = Game03EnemyPhaseTimeline.GetActivePhaseNumberFromGlobalElapsed(elapsedSeconds);
        return Game03MetaEconomyRules.ComputeMetaCurrencyFromLifetimeExperience(
            lifetimeExperience,
            elapsedSeconds,
            phase);
    }

    private static long GetBossBonusPoints()
    {
        return Game03RunSessionState.MidBossAcquisitionPointsTotal
            + Game03RunSessionState.BossAcquisitionPointsTotal;
    }

    private void ApplyMetaCurrencyGrant(long total)
    {
        if (total <= 0)
        {
            return;
        }

        Game03MetaProgressController controller = Game03MetaProgressController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[Game03MetaRunEndCoordinator] Game03MetaProgressController が見つかりません。", this);
            return;
        }

        Game03RunSessionState.RecordMetaCurrencyGranted(total);
        controller.AddMetaCurrencyAndPersist(total);
    }
}
