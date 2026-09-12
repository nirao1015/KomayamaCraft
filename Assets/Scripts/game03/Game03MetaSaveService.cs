using System;
using System.IO;
using UnityEngine;

/// <summary>
/// game03 メタ進行セーブの読み書き（persistentDataPath/saveData/game03Data.json）。
/// </summary>
public sealed class Game03MetaSaveService
{
    private const string SaveFolderName = "saveData";
    private const string SaveFileName = "game03Data.json";
    private const string LegacySaveFolderName = "game03";
    private const string LegacySaveFileName = "meta_save.json";

    private static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveFolderName);
    private static string SaveFilePath => Path.Combine(SaveDirectoryPath, SaveFileName);
    private static string LegacySaveFilePath => Path.Combine(Application.persistentDataPath, LegacySaveFolderName, LegacySaveFileName);

    public bool Exists()
    {
        return File.Exists(SaveFilePath) || File.Exists(LegacySaveFilePath);
    }

    public bool TryLoad(out Game03MetaSaveData data)
    {
        data = null;
        string path = ResolveLoadPath();
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            Game03MetaSaveData loaded = JsonUtility.FromJson<Game03MetaSaveData>(json);
            if (loaded == null || loaded.version != Game03MetaSaveData.CurrentVersion)
            {
                TryBackupUnsupportedSave(path);
                return false;
            }

            Game03MetaProgressController.ClampSaveFields(loaded);
            data = loaded;
            if (string.Equals(path, LegacySaveFilePath, StringComparison.OrdinalIgnoreCase))
            {
                TrySave(loaded);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03MetaSaveService] load failed: {ex.Message}");
            return false;
        }
    }

    public bool TrySave(Game03MetaSaveData data)
    {
        if (data == null)
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(SaveDirectoryPath))
            {
                Directory.CreateDirectory(SaveDirectoryPath);
            }

            Game03MetaProgressController.ClampSaveFields(data);
            data.version = Game03MetaSaveData.CurrentVersion;
            data.updatedAtUtc = DateTime.UtcNow.ToString("o");
            data.buildVersion = GameBuildVersion.GetCurrentApplicationVersion();
            string json = JsonUtility.ToJson(data, true);
            string path = SaveFilePath;
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
            if (File.Exists(LegacySaveFilePath))
            {
                File.Delete(LegacySaveFilePath);
            }

            SteamSessionFileLogger.LogSaveWrite(
                SaveFileName,
                path,
                SteamSessionFileLogger.SummarizeGame03MetaSave(data));

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03MetaSaveService] save failed: {ex.Message}");
            return false;
        }
    }

    private static string ResolveLoadPath()
    {
        if (File.Exists(SaveFilePath))
        {
            return SaveFilePath;
        }

        return File.Exists(LegacySaveFilePath) ? LegacySaveFilePath : string.Empty;
    }

    private static void TryBackupUnsupportedSave(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            string backup = path + ".unsupported.bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(path, backup);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Game03MetaSaveService] backup unsupported save failed: {ex.Message}");
        }
    }
}
