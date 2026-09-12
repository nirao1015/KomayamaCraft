/// <summary>
/// 隣接する保存version間のJSON変換境界。
/// 実装はファイルI/Oを行わず、入力JSONを次versionのJSONへ変換する。
/// </summary>
public interface IKomayamaCraftSaveMigration
{
    int SourceVersion { get; }
    int TargetVersion { get; }

    /// <summary>
    /// sourceJsonをTargetVersionのJSONへ変換する。
    /// 成功時も元文字列や元ファイルの破棄・上書きは呼び出し側で明示的に判断する。
    /// </summary>
    bool TryMigrate(
        string sourceJson,
        out string migratedJson,
        out string errorMessage);
}
