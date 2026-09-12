using UnityEngine;

/// <summary>
/// 敵の移動速度をプレイヤー速度に対する比で段階指定する（プレハブの Game03EnemyStats）。
/// 比の値: 停止0.01 / 最小0.2 / 小0.35 / 中0.5 / 大0.75 / 特大0.9
/// </summary>
public enum Game03EnemyMoveSpeedTier
{
    [InspectorName("停止 (比 0.01)")]
    Stopped = 0,

    [InspectorName("最小 (比 0.2)")]
    Minimum = 1,

    [InspectorName("小 (比 0.35)")]
    Small = 2,

    [InspectorName("中 (比 0.5)")]
    Medium = 3,

    [InspectorName("大 (比 0.75)")]
    Large = 4,

    [InspectorName("特大 (比 0.9)")]
    ExtraLarge = 5
}

public static class Game03EnemyMoveSpeedTierUtil
{
    public static float ToPlayerSpeedRatio(Game03EnemyMoveSpeedTier tier)
    {
        return tier switch
        {
            Game03EnemyMoveSpeedTier.Stopped => 0.01f,
            Game03EnemyMoveSpeedTier.Minimum => 0.2f,
            Game03EnemyMoveSpeedTier.Small => 0.35f,
            Game03EnemyMoveSpeedTier.Medium => 0.5f,
            Game03EnemyMoveSpeedTier.Large => 0.75f,
            Game03EnemyMoveSpeedTier.ExtraLarge => 0.9f,
            _ => 0.5f
        };
    }
}
