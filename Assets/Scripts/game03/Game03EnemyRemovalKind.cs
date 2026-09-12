/// <summary>
/// 敵がフィールドから外れる経路（倒す／消滅／削除）。
/// </summary>
public enum Game03EnemyRemovalKind
{
    /// <summary>武器等で撃破。撃破SE・経験値・撃破演出。</summary>
    DefeatWeapon = 0,

    /// <summary>消滅。撃破SEなし・経験値なし・消滅演出（寿命切れ・全体消しアイテム等）。</summary>
    Vanish = 1,

    /// <summary>削除。SEなし・経験値なし・演出なし。即時 Destroy。</summary>
    Delete = 2,

    /// <summary>
    /// 撃破扱い（SE・撃破演出・撃破数・経験値の数値加算）だがレベルは上げない。
    /// Game03DebugManager の接触撃破デバッグ用。
    /// </summary>
    DefeatWeaponSuppressLevelProgress = 3
}
