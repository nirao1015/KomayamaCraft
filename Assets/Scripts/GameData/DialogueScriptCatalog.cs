using System;
using UnityEngine;

/// <summary>
/// 会話台本 CSV（<c>Assets/GameData/Dialogue</c>）を TextAsset 参照でビルド同梱するカタログ。
/// </summary>
[CreateAssetMenu(fileName = "DialogueScriptCatalog", menuName = "GameData/Dialogue Script Catalog")]
public sealed class DialogueScriptCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string sceneName;
        public string stageName;
        public TextAsset csv;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public bool TryGetTextAsset(string sceneName, string stageName, out TextAsset csv)
    {
        csv = null;
        string sceneKey = NormalizeSceneKey(sceneName);
        string stageKey = NormalizeStageKey(stageName);

        for (int i = 0; i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e.csv == null)
            {
                continue;
            }

            if (string.Equals(NormalizeSceneKey(e.sceneName), sceneKey, StringComparison.Ordinal) &&
                string.Equals(NormalizeStageKey(e.stageName), stageKey, StringComparison.Ordinal))
            {
                csv = e.csv;
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeSceneKey(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? "game_stage_scene" : sceneName.Trim();
    }

    internal static string NormalizeStageKey(string stageName)
    {
        return string.IsNullOrWhiteSpace(stageName) ? "null" : stageName.Trim();
    }

#if UNITY_EDITOR
    public void SetEntriesForEditor(Entry[] newEntries)
    {
        entries = newEntries ?? Array.Empty<Entry>();
    }
#endif
}
