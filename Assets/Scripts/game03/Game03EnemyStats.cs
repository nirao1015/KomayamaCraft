using UnityEngine;

public class Game03EnemyStats : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float coreRadius = 30f;
    [SerializeField, Tooltip("ワールド移動速度は Game03UnitManager のプレイヤー速度 × この段階比 × JSON の speedTierMult × kSpeed で決まる。")]
    private Game03EnemyMoveSpeedTier moveSpeedTier = Game03EnemyMoveSpeedTier.Medium;
    [SerializeField, Min(1)] private int touchDamage = 5;
    [SerializeField, Min(1)] private int maxLife = 1;
    [SerializeField] private Game03ExpTier experienceTier = Game03ExpTier.Red;
    [SerializeField, Min(0), Tooltip("撃破ドロップの基準経験値。0 のとき experienceTier の基準値を使う。")]
    private int experienceValue;
    [SerializeField] private bool isBossEnemy;
    [SerializeField, Range(0f, 100f), Tooltip("武器ノックバック距離をこの割合だけ減衰。大きいほど吹き飛びにくい（100 で実質ほぼ無効）。")]
    private float knockbackResistancePercent = 0f;

    /// <summary>スポーン処理で書き込まれる実効ワールド速度（表示・デバッグ用）。</summary>
    private float runtimeMoveSpeedWorld;

    public float CoreRadius => coreRadius;

    /// <summary>スポーン後の実効ワールド移動速度（秒あたり）。未スポーン時は 0。</summary>
    public float MoveSpeed => runtimeMoveSpeedWorld;

    /// <summary>プレイヤー速度に対する比（段階）。</summary>
    public float PlayerSpeedRatio => Game03EnemyMoveSpeedTierUtil.ToPlayerSpeedRatio(moveSpeedTier);
    public int TouchDamage => touchDamage;
    public int MaxLife => maxLife;
    public Game03ExpTier ExperienceTier => experienceTier;
    /// <summary>0 のときは experienceTier から基準値を解決する。</summary>
    public int ExperienceValue => experienceValue;
    public bool IsBossEnemy => isBossEnemy;

    /// <summary>0〜100。武器ノックバックの距離に対するパーセント減衰。</summary>
    public float KnockbackResistancePercent => knockbackResistancePercent;

    /// <summary>
    /// Instantiate 直後にのみ呼ぶ。Inspector で調整したプレハブ原本には使わないこと。
    /// </summary>
    public void ApplyRuntimeSpawnConfig(float moveSpeedWorldUnitsPerSecond, int runtimeMaxLifeValue)
    {
        runtimeMoveSpeedWorld = Mathf.Max(0f, moveSpeedWorldUnitsPerSecond);
        maxLife = Mathf.Max(1, runtimeMaxLifeValue);
    }
}
