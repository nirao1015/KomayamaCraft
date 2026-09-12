using UnityEngine;

/// <summary>
/// game_stage_scene 用デバッグ設定。
/// isProductionMode=true のときは既存 Inspector 値をそのまま利用する。
/// </summary>
public sealed class Game01DebugManager : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool isProductionMode = true;

    [Header("Targets")]
    [SerializeField] private Game01Manager game01Manager;
    [SerializeField] private Game01PlayerLife game01PlayerLife;

    [Header("Debug Overrides (isProductionMode=false のとき使用)")]
    [SerializeField] private int debugMaxLives = 3;
    [SerializeField] private float debugClearTimeSeconds = 120f;
    [SerializeField] private float debugStartTimeSeconds = 0f;
    [SerializeField] private bool debugSkipStageIntro = false;

    private void Reset()
    {
        if (game01Manager == null)
        {
            game01Manager = FindObjectOfType<Game01Manager>(true);
        }

        if (game01PlayerLife == null)
        {
            game01PlayerLife = FindObjectOfType<Game01PlayerLife>(true);
        }
    }

    private void Awake()
    {
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool effectiveProduction = TitleDebugManager.ResolveProductionMode(isProductionMode);

        if (game01Manager != null)
        {
            // 本番ON時は必ず 0 秒開始。OFF時はデバッグ開始時間でシーク開始する。
            game01Manager.ApplyDebugEnemySpawnTimelineStartSeconds(effectiveProduction ? 0f : debugStartTimeSeconds);
            // 本番ON時は必ず開始演出を飛ばさない。
            game01Manager.ApplyDebugSkipStageIntro(!effectiveProduction && debugSkipStageIntro);
        }

        if (effectiveProduction)
        {
            return;
        }

        if (game01Manager != null)
        {
            game01Manager.ApplyDebugClearTimeSeconds(debugClearTimeSeconds);
        }

        if (game01PlayerLife != null)
        {
            game01PlayerLife.ApplyDebugMaxLives(debugMaxLives);
        }
    }
}
