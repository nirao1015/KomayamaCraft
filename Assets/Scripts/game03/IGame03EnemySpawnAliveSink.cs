/// <summary>
/// フェーズ JSON の各エントリが生成した敵の生存数を同期するためのコールバック（統合仕様【13】）。
/// </summary>
public interface IGame03EnemySpawnAliveSink
{
    void OnPhaseSpawnedEnemyRemoved(int entryIndex);
}
