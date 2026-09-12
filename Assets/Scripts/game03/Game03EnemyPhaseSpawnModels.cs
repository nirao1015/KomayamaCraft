using System;
using System.IO;
using UnityEngine;

/// <summary>
/// EnemySpawnPhaseNN.json とゲームタイム上のフェーズ境界（仕様）。
/// </summary>
public static class Game03EnemyPhaseTimeline
{
    /// <summary>敵出現フェーズ JSON の最大番号（EnemySpawnPhase07 まで）。</summary>
    public const int MaxEnemySpawnPhase = 7;

    public static string FormatPhaseJsonFileName(int phase1Based)
    {
        int p = Mathf.Clamp(phase1Based, 1, 99);
        return $"EnemySpawnPhase{p:00}.json";
    }

    /// <summary>
    /// 仕様どおりの各フェーズ開始時刻（ゲーム内経過秒）。P1:0〜2分, P2:2〜5分, …
    /// </summary>
    public static float GetGlobalStartSecondsForPhase(int phase1Based)
    {
        switch (Mathf.Clamp(phase1Based, 1, 7))
        {
            case 1:
                return 0f;
            case 2:
                return 120f;
            case 3:
                return 300f;
            case 4:
                return 480f;
            case 5:
                return 660f;
            case 6:
                return 840f;
            case 7:
                return 1080f;
            default:
                return 1080f;
        }
    }

    /// <summary>
    /// ゲーム内経過秒から、現在どの敵出現フェーズにいるか（1〜<see cref="MaxEnemySpawnPhase"/>）。
    /// 境界は <see cref="GetGlobalStartSecondsForPhase"/> に一致（P1:0〜, P2:120〜, … P7:1080〜）。
    /// </summary>
    public static int GetActivePhaseNumberFromGlobalElapsed(float globalElapsedSeconds)
    {
        for (int p = 1; p < MaxEnemySpawnPhase; p++)
        {
            float nextStart = GetGlobalStartSecondsForPhase(p + 1);
            if (globalElapsedSeconds < nextStart)
            {
                return p;
            }
        }

        return MaxEnemySpawnPhase;
    }
}

/// <summary>
/// EnemySpawnPhaseNN.json（JsonUtility）。同一スキーマで Phase01〜共用。
/// </summary>
[Serializable]
public sealed class Game03EnemySpawnPhase01Json
{
    public float phaseDurationSeconds = 120f;
    public float hpFlatBonus;
    public float hpMult = 1f;
    public float expMult = 1f;
    public bool phaseResetFlag;
    public Game03EnemyPhaseSpawnEntryJson[] entries = Array.Empty<Game03EnemyPhaseSpawnEntryJson>();
}

/// <summary>
/// EnemySpawnPhaseNN.json 1 行分 JsonUtility 用。
/// HP／接触ダメージ／コア半径／経験値段は <see cref="Game03EnemyStats"/> プレハブ側。
/// フェーズ全体の係数は <see cref="Game03EnemySpawnPhase01Json"/> の hpMult / hpFlatBonus / expMult。
/// </summary>
[Serializable]
public sealed class Game03EnemyPhaseSpawnEntryJson
{
    public float spawnStartTime;
    public float spawnEndTime;
    public string enemyKind = "Enemy00";
    public int spawnPatternId = 1;
    public float lifetimeSeconds;
    public int initialSpawnCount;
    public int spawnCountPerWave = 1;
    public float spawnIntervalSeconds = 4f;
    /// <summary>種別04/05のみ。0 超なら <see cref="spawnIntervalSeconds"/> は「秒間隔」ではなく距離ルール用（全滅後の再湧きクールダウン秒）に回す。</summary>
    public float spawnSpacingPixels;
    public int maxAlive = 6;
    public float spawnOffsetPx;
    public int steering;
    public bool isBoss;
    public float speedTierMult = 1f;
    public float kSpeed = 1f;
}

public static class Game03EnemyPhaseSpawnJsonLoader
{
    private const string RelativeFolder = "GameData/EnemySpawn03";

    public static bool TryLoadPhase01(out Game03EnemySpawnPhase01Json data, out string error)
    {
        return TryLoad("EnemySpawnPhase01.json", out data, out error);
    }

    public static bool TryLoad(string fileName, out Game03EnemySpawnPhase01Json data, out string error)
    {
        data = null;
        error = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "Phase spawn file name is empty.";
            return false;
        }

        Game03EnemySpawnPhaseCatalog catalog = GameDataCatalogs.Game03EnemySpawn;
        if (catalog != null && catalog.TryLoadByFileName(fileName.Trim(), out data, out error))
        {
            return true;
        }

#if UNITY_EDITOR
        string path = ResolveEditorPhaseSpawnJsonPath(fileName.Trim());
        if (path != null)
        {
            try
            {
                return TryParseJsonText(File.ReadAllText(path), out data, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
#endif

        error =
            $"Phase spawn JSON not found: {fileName.Trim()} " +
            $"(Resources/GameData/Game03EnemySpawnPhaseCatalog or Assets/{RelativeFolder}).";
        return false;
    }

    public static bool TryParseJsonText(string jsonText, out Game03EnemySpawnPhase01Json data, out string error)
    {
        data = null;
        error = null;
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "Phase spawn JSON text is empty.";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<Game03EnemySpawnPhase01Json>(jsonText);
            if (data == null || data.entries == null)
            {
                error = "Phase spawn JSON parse failed.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

#if UNITY_EDITOR
    private static string ResolveEditorPhaseSpawnJsonPath(string fileName)
    {
        string assetsPath = Path.Combine(Application.dataPath, RelativeFolder, fileName);
        return File.Exists(assetsPath) ? assetsPath : null;
    }
#endif
}
