using System;
using UnityEngine;

/// <summary>
/// game03 敵出現フェーズ JSON（<c>Assets/GameData/EnemySpawn03</c>）を TextAsset 参照でビルド同梱する。
/// </summary>
[CreateAssetMenu(fileName = "Game03EnemySpawnPhaseCatalog", menuName = "GameData/Game03 Enemy Spawn Phase Catalog")]
public sealed class Game03EnemySpawnPhaseCatalog : ScriptableObject
{
    [SerializeField] private TextAsset phase01;
    [SerializeField] private TextAsset phase02;
    [SerializeField] private TextAsset phase03;
    [SerializeField] private TextAsset phase04;
    [SerializeField] private TextAsset phase05;
    [SerializeField] private TextAsset phase06;
    [SerializeField] private TextAsset phase07;

    public bool TryLoadPhase(int phase1Based, out Game03EnemySpawnPhase01Json data, out string error)
    {
        data = null;
        error = null;
        TextAsset asset = ResolvePhaseAsset(phase1Based);
        if (asset == null)
        {
            error = $"Game03 enemy spawn phase {phase1Based} TextAsset is not assigned in catalog.";
            return false;
        }

        return Game03EnemyPhaseSpawnJsonLoader.TryParseJsonText(asset.text, out data, out error);
    }

    public bool TryLoadByFileName(string fileName, out Game03EnemySpawnPhase01Json data, out string error)
    {
        data = null;
        error = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "Phase spawn file name is empty.";
            return false;
        }

        string trimmed = fileName.Trim();
        for (int phase = 1; phase <= Game03EnemyPhaseTimeline.MaxEnemySpawnPhase; phase++)
        {
            if (!string.Equals(Game03EnemyPhaseTimeline.FormatPhaseJsonFileName(phase), trimmed, StringComparison.Ordinal))
            {
                continue;
            }

            return TryLoadPhase(phase, out data, out error);
        }

        error = $"Unknown phase spawn file name: {trimmed}";
        return false;
    }

    private TextAsset ResolvePhaseAsset(int phase1Based)
    {
        return Mathf.Clamp(phase1Based, 1, 7) switch
        {
            1 => phase01,
            2 => phase02,
            3 => phase03,
            4 => phase04,
            5 => phase05,
            6 => phase06,
            7 => phase07,
            _ => null
        };
    }

#if UNITY_EDITOR
    public void SetPhasesForEditor(
        TextAsset p01,
        TextAsset p02,
        TextAsset p03,
        TextAsset p04,
        TextAsset p05,
        TextAsset p06,
        TextAsset p07)
    {
        phase01 = p01;
        phase02 = p02;
        phase03 = p03;
        phase04 = p04;
        phase05 = p05;
        phase06 = p06;
        phase07 = p07;
    }
#endif
}
