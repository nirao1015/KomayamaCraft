/// <summary>
/// フィールド Device アイテム取得時の Pod／フラグ分岐に使用する。
/// </summary>
public enum Game03FieldItemPickupRoute
{
    PermanentInitial,
    MilestoneConsumable,
    /// <summary>エマージェンシーカーゴ着弾で生成された消費アイテム（■3 効果）。</summary>
    EmergencyCargo,
    /// <summary>中ボス（出現種別51）撃破ドロップ → UnitUG。</summary>
    MidBossReward,
    /// <summary>大ボス（出現種別61）撃破ドロップ → BossUG。</summary>
    BossReward,
}
