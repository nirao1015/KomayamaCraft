#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DialogueScene;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/GameData 配下の CSV/JSON から Resources カタログを再生成する。
/// </summary>
public static class GameDataCatalogSync
{
    private const string MenuPathJp = "ツール/GameData/全カタログを同期";
    private const string MenuPathEn = "Tools/GameData/Sync All Catalogs";

    private const string DialogueSourceFolder = "Assets/GameData/Dialogue";
    private const string EnemySpawn03Folder = "Assets/GameData/EnemySpawn03";
    private const string EnemySpawn01Folder = "Assets/GameData/EnemySpawn";
    private const string ResourcesGameDataFolder = "Assets/Resources/GameData";

    [MenuItem(MenuPathJp, false, 200)]
    public static void SyncAllJp()
    {
        SyncAll();
    }

    [MenuItem(MenuPathEn, false, 200)]
    public static void SyncAllEn()
    {
        SyncAll();
    }

    public static void SyncAll()
    {
        EnsureFolder(ResourcesGameDataFolder);
        SyncDialogueCatalog();
        SyncGame03EnemySpawnCatalog();
        SyncGame01EnemySpawnCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameDataCatalogSync] All catalogs synced under Resources/GameData.");
    }

    private static void SyncDialogueCatalog()
    {
        string assetPath = $"{ResourcesGameDataFolder}/DialogueScriptCatalog.asset";
        DialogueScriptCatalog catalog = LoadOrCreate<DialogueScriptCatalog>(assetPath);
        var list = new List<DialogueScriptCatalog.Entry>();

        foreach (string assetPathCsv in EnumerateAssetsInFolder(DialogueSourceFolder, ".csv"))
        {
            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPathCsv);
            if (csv == null)
            {
                Debug.LogWarning($"[GameDataCatalogSync] Not a TextAsset (reimport as Text Asset?): {assetPathCsv}");
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPathCsv);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            if (!DialogueCsvPaths.TryParseCompositeSceneStage(fileName, out string scene, out string stage))
            {
                Debug.LogWarning($"[GameDataCatalogSync] Skip dialogue file (not scene-stage): {fileName}");
                continue;
            }

            list.Add(new DialogueScriptCatalog.Entry
            {
                sceneName = scene,
                stageName = stage,
                csv = csv
            });
        }

        catalog.SetEntriesForEditor(list.ToArray());
        EditorUtility.SetDirty(catalog);
        Debug.Log($"[GameDataCatalogSync] Dialogue entries: {list.Count}");
    }

    private static void SyncGame03EnemySpawnCatalog()
    {
        string assetPath = $"{ResourcesGameDataFolder}/Game03EnemySpawnPhaseCatalog.asset";
        Game03EnemySpawnPhaseCatalog catalog = LoadOrCreate<Game03EnemySpawnPhaseCatalog>(assetPath);
        catalog.SetPhasesForEditor(
            LoadJson(1),
            LoadJson(2),
            LoadJson(3),
            LoadJson(4),
            LoadJson(5),
            LoadJson(6),
            LoadJson(7));
        EditorUtility.SetDirty(catalog);
        Debug.Log("[GameDataCatalogSync] Game03 enemy spawn phases synced.");

        TextAsset LoadJson(int phase)
        {
            string file = Game03EnemyPhaseTimeline.FormatPhaseJsonFileName(phase);
            return AssetDatabase.LoadAssetAtPath<TextAsset>($"{EnemySpawn03Folder}/{file}");
        }
    }

    private static void SyncGame01EnemySpawnCatalog()
    {
        string assetPath = $"{ResourcesGameDataFolder}/Game01EnemySpawnCatalog.asset";
        Game01EnemySpawnCatalog catalog = LoadOrCreate<Game01EnemySpawnCatalog>(assetPath);
        var list = new List<Game01EnemySpawnCatalog.StageEntry>();

        foreach (string jsonPath in EnumerateAssetsInFolder(EnemySpawn01Folder, ".json"))
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null)
            {
                continue;
            }

            string stageName = Path.GetFileNameWithoutExtension(jsonPath);
            list.Add(new Game01EnemySpawnCatalog.StageEntry
            {
                stageName = stageName,
                json = json
            });
        }

        catalog.SetStagesForEditor(list.ToArray());
        EditorUtility.SetDirty(catalog);
        Debug.Log($"[GameDataCatalogSync] Game01 enemy spawn stages: {list.Count}");
    }

    private static IEnumerable<string> EnumerateAssetsInFolder(string assetsFolder, string extension)
    {
        if (!AssetDatabase.IsValidFolder(assetsFolder))
        {
            yield break;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { assetsFolder });
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) ||
                Directory.Exists(path) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return path;
        }
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, assetPath);
        return created;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/GameData"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "GameData");
        }
    }
}
#endif
