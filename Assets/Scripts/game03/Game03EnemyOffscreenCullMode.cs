/// <summary>
/// 画面外に出た敵の扱い（種別03/04/05 は「削除」、種別14 は「消滅」）。
/// </summary>
public enum Game03EnemyOffscreenCullMode
{
    None = 0,
    DeleteBeyondVisibleMargin = 1,
    VanishBeyondVisibleMargin = 2
}
