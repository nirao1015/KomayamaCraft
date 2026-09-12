/// <summary>
/// 中盤背景演出（Fire / City）中は <see cref="Game01Manager"/> が JSON 敵出現を抑止する。
/// </summary>
public interface IGame01MidgameBackgroundEnemySpawnSuppressor
{
    bool IsSuppressingEnemySpawns(float gameplayElapsedSinceT0Seconds);
}
