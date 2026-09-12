using System;
using UnityEngine;

/// <summary>
/// game01 ステージ敵出現 JSON（<c>Assets/GameData/EnemySpawn</c>）を TextAsset 参照でビルド同梱する。
/// </summary>
[CreateAssetMenu(fileName = "Game01EnemySpawnCatalog", menuName = "GameData/Game01 Enemy Spawn Catalog")]
public sealed class Game01EnemySpawnCatalog : ScriptableObject
{
    [Serializable]
    public struct StageEntry
    {
        public string stageName;
        public TextAsset json;
    }

    [SerializeField] private StageEntry[] stages = Array.Empty<StageEntry>();

    public bool TryGetStageJsonText(string stageBaseName, out string jsonText)
    {
        jsonText = null;
        if (string.IsNullOrWhiteSpace(stageBaseName))
        {
            return false;
        }

        string key = stageBaseName.Trim();
        for (int i = 0; i < stages.Length; i++)
        {
            StageEntry e = stages[i];
            if (e.json == null || string.IsNullOrWhiteSpace(e.stageName))
            {
                continue;
            }

            if (string.Equals(e.stageName.Trim(), key, StringComparison.Ordinal))
            {
                jsonText = e.json.text;
                return !string.IsNullOrWhiteSpace(jsonText);
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public void SetStagesForEditor(StageEntry[] newStages)
    {
        stages = newStages ?? Array.Empty<StageEntry>();
    }
#endif
}
