using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game02
{
    public sealed class Game02SaveService
    {
        private const string SaveFolderName = "saveData";
        private const string SaveFileName = "game02Data.json";
        private const string LegacySaveFolderName = "game02";
        private const string LegacySaveFileName = "save.json";

        private string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveFolderName);
        private string SaveFilePath => Path.Combine(SaveDirectoryPath, SaveFileName);
        private string LegacySaveFilePath => Path.Combine(Application.persistentDataPath, LegacySaveFolderName, LegacySaveFileName);

        public bool Exists()
        {
            return File.Exists(SaveFilePath) || File.Exists(LegacySaveFilePath);
        }

        public bool TryLoad(out Game02SaveData data)
        {
            data = null;
            string path = ResolveLoadPath();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                if (!TryReadAndUpgrade(path, out Game02SaveData loaded))
                {
                    return false;
                }

                data = loaded;
                TryMigrateLegacyFileIfNeeded(path, loaded);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Game02SaveService] load failed: {ex.Message}");
                return false;
            }
        }

        public bool TrySave(Game02SaveData data)
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

                data.version = Game02SaveData.CurrentVersion;
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
                    SteamSessionFileLogger.SummarizeGame02Save(data));

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Game02SaveService] save failed: {ex.Message}");
                return false;
            }
        }

        public bool TryDelete()
        {
            try
            {
                string path = SaveFilePath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (File.Exists(LegacySaveFilePath))
                {
                    File.Delete(LegacySaveFilePath);
                }

                string tempPath = path + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Game02SaveService] delete failed: {ex.Message}");
                return false;
            }
        }

        private string ResolveLoadPath()
        {
            if (File.Exists(SaveFilePath))
            {
                return SaveFilePath;
            }

            return File.Exists(LegacySaveFilePath) ? LegacySaveFilePath : string.Empty;
        }

        private static bool TryReadAndUpgrade(string path, out Game02SaveData loaded)
        {
            loaded = null;
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            loaded = JsonUtility.FromJson<Game02SaveData>(json);
            if (loaded == null || loaded.version < 1 || loaded.version > Game02SaveData.CurrentVersion)
            {
                return false;
            }

            if (loaded.version < Game02SaveData.CurrentVersion)
            {
                if (loaded.alienProgress == null)
                {
                    loaded.alienProgress = new AlienProgressState();
                }

                // v1 セーブには異星人進行が無い。初期所持金の再付与で生涯獲得が二重計上されないようフラグだけ立てる。
                if (loaded.version == 1)
                {
                    loaded.alienProgress.moneyInitialGrantRecorded = true;
                }

                if (loaded.version < 3 && loaded.gameManagerState != null)
                {
                    Game02MsgManager.MigrateGameManagerStateFromV2ToV3(loaded.gameManagerState);
                }

                loaded.version = Game02SaveData.CurrentVersion;
            }

            if (loaded.alienProgress == null)
            {
                loaded.alienProgress = new AlienProgressState();
            }

            if (loaded.alienProgress.upgradePurchaseLog == null)
            {
                loaded.alienProgress.upgradePurchaseLog = new List<UpgradePurchaseLogEntry>();
            }

            return true;
        }

        private void TryMigrateLegacyFileIfNeeded(string loadedPath, Game02SaveData data)
        {
            if (data == null)
            {
                return;
            }

            if (!string.Equals(loadedPath, LegacySaveFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TrySave(data);
        }
    }
}
