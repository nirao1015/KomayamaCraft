using UnityEngine;

/// <summary>
/// タイトルで設定する本番モードのマスター。
/// ON のとき全シーンの本番リリース扱いを強制し、OFF のときは各シーンの DebugManager 設定に従う。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class TitleDebugManager : MonoBehaviour
{
    public static TitleDebugManager Instance { get; private set; }

    [Header("本番リリース用（マスター）")]
    [SerializeField, Tooltip(
        "ON のとき game01 / game02 / game03 / menu03 の本番モードをすべて ON として扱う。" +
        " OFF のときは各シーンの本番チェックを個別に参照する。")]
    private bool masterProductionReleaseBuild;

    [Header("Steam 診断ログ")]
    [SerializeField, Tooltip(
        "ON のとき persistentDataPath/SteamDebugLogs/ に新規ログファイルを作成し、" +
        "セーブ書き込み・Steam 実績判定を追記する（Steam Auto-Cloud 対象外）。")]
    private bool steamLogFileMode;

    public bool MasterProductionReleaseBuild => masterProductionReleaseBuild;

    public bool SteamLogFileMode => steamLogFileMode;

    public static bool IsSteamLogFileModeActive =>
        Instance != null && Instance.steamLogFileMode;

    public static bool IsMasterProductionReleaseActive =>
        Instance != null && Instance.masterProductionReleaseBuild;

    /// <summary>
    /// シーン個別の本番フラグとマスターを合成した実効値。
    /// </summary>
    public static bool ResolveProductionReleaseBuild(bool sceneProductionReleaseBuild) =>
        IsMasterProductionReleaseActive || sceneProductionReleaseBuild;

    /// <summary>
    /// <see cref="Game01DebugManager"/> の isProductionMode 用（true = 本番）。
    /// </summary>
    public static bool ResolveProductionMode(bool sceneIsProductionMode) =>
        ResolveProductionReleaseBuild(sceneIsProductionMode);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SteamSessionFileLogger.BeginSessionIfEnabled(steamLogFileMode);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
