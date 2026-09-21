using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace KomayamaCraft
{
    public readonly struct KomayamaSaveSlotInfo
    {
        public KomayamaSaveSlotInfo(
            int slot,
            bool hasData,
            DateTime? lastSavedAtLocal,
            float? playSeconds,
            string thumbnailPath)
        {
            Slot = slot;
            HasData = hasData;
            LastSavedAtLocal = lastSavedAtLocal;
            PlaySeconds = playSeconds;
            ThumbnailPath = thumbnailPath ?? string.Empty;
        }

        public int Slot { get; }
        public bool HasData { get; }
        public DateTime? LastSavedAtLocal { get; }
        public float? PlaySeconds { get; }
        public string ThumbnailPath { get; }

        public string FormatLabel(bool isLastPlayed)
        {
            string lastMark = isLastPlayed && HasData ? " 前回" : string.Empty;
            if (!HasData)
            {
                return $"スロット{Slot}{lastMark}\n空き";
            }

            string time = LastSavedAtLocal.HasValue
                ? LastSavedAtLocal.Value.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
                : "日時不明";
            return $"スロット{Slot}{lastMark}\n使用中\n{time}";
        }
    }

    public static class KomayamaSaveSlots
    {
        public const int SlotCount = 3;

        private static int activeSlot;

        public static int ActiveSlot =>
            activeSlot > 0 ? activeSlot : LastPlayedSlot;

        public static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, "saveData");

        public static string LastSlotFilePath =>
            Path.Combine(SaveDirectory, "lastSlot.txt");

        public static string BootFilePath =>
            Path.Combine(SaveDirectory, "bootRequest.txt");

        public static int ClampSlot(int slot)
        {
            return Mathf.Clamp(slot, 1, SlotCount);
        }

        public static void MigrateLegacyIfNeeded()
        {
            Directory.CreateDirectory(SaveDirectory);
            string legacy = Path.Combine(SaveDirectory, "komayamaCraftData.json");
            string legacyBackup = Path.Combine(SaveDirectory, "komayamaCraftData.bak.json");
            string slot1 = SaveFilePath(1);
            if (File.Exists(legacy) && !File.Exists(slot1))
            {
                File.Move(legacy, slot1);
            }

            if (File.Exists(legacyBackup) && !File.Exists(BackupFilePath(1)))
            {
                File.Move(legacyBackup, BackupFilePath(1));
            }
        }

        public static string SaveFilePath(int slot)
        {
            return Path.Combine(SaveDirectory, $"komayamaCraftData_{ClampSlot(slot)}.json");
        }

        public static string BackupFilePath(int slot)
        {
            return Path.Combine(SaveDirectory, $"komayamaCraftData_{ClampSlot(slot)}.bak.json");
        }

        public static string TempFilePath(int slot)
        {
            return Path.Combine(SaveDirectory, $"komayamaCraftData_{ClampSlot(slot)}.tmp.json");
        }

        public static string ThumbnailFilePath(int slot)
        {
            return Path.Combine(SaveDirectory, $"komayamaCraftThumb_{ClampSlot(slot)}.png");
        }

        public static bool HasData(int slot)
        {
            MigrateLegacyIfNeeded();
            return File.Exists(SaveFilePath(slot)) || File.Exists(BackupFilePath(slot));
        }

        public static bool HasAnySave()
        {
            for (int i = 1; i <= SlotCount; i++)
            {
                if (HasData(i))
                {
                    return true;
                }
            }

            return false;
        }

        public static DateTime? GetLastSavedAtLocal(int slot)
        {
            MigrateLegacyIfNeeded();
            if (TryReadSaveSummary(slot, out DateTime? savedAt, out _))
            {
                if (savedAt.HasValue)
                {
                    return savedAt;
                }
            }

            string path = ResolveExistingSavePath(slot);
            if (path == null)
            {
                return null;
            }

            return File.GetLastWriteTime(path);
        }

        public static KomayamaSaveSlotInfo GetInfo(int slot)
        {
            slot = ClampSlot(slot);
            bool has = HasData(slot);
            DateTime? savedAt = null;
            float? playSeconds = null;
            if (has)
            {
                TryReadSaveSummary(slot, out savedAt, out playSeconds);
                if (!savedAt.HasValue)
                {
                    string path = ResolveExistingSavePath(slot);
                    if (path != null)
                    {
                        savedAt = File.GetLastWriteTime(path);
                    }
                }
            }

            return new KomayamaSaveSlotInfo(
                slot,
                has,
                savedAt,
                playSeconds,
                ThumbnailFilePath(slot));
        }

        private static string ResolveExistingSavePath(int slot)
        {
            MigrateLegacyIfNeeded();
            if (File.Exists(SaveFilePath(slot)))
            {
                return SaveFilePath(slot);
            }

            if (File.Exists(BackupFilePath(slot)))
            {
                return BackupFilePath(slot);
            }

            return null;
        }

        private static bool TryReadSaveSummary(int slot, out DateTime? savedAtLocal, out float? playSeconds)
        {
            savedAtLocal = null;
            playSeconds = null;
            string path = ResolveExistingSavePath(slot);
            if (path == null)
            {
                return false;
            }

            try
            {
                KomayamaCraftSaveData data =
                    JsonUtility.FromJson<KomayamaCraftSaveData>(File.ReadAllText(path));
                if (data == null)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(data.updatedAtUtc) &&
                    DateTime.TryParse(
                        data.updatedAtUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTime utc))
                {
                    savedAtLocal = utc.ToLocalTime();
                }

                if (data.gameplayElapsedSeconds > 0f)
                {
                    playSeconds = Mathf.Min(
                        data.gameplayElapsedSeconds,
                        KomayamaSaveService.MaxGameplayElapsedSeconds);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int LastPlayedSlot
        {
            get
            {
                MigrateLegacyIfNeeded();
                if (File.Exists(LastSlotFilePath) &&
                    int.TryParse(File.ReadAllText(LastSlotFilePath).Trim(), out int slot))
                {
                    return ClampSlot(slot);
                }

                return 1;
            }
        }

        public static void RememberLastPlayedSlot(int slot)
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(LastSlotFilePath, ClampSlot(slot).ToString());
        }

        public static void WriteBootRequest(bool startNew, int slot)
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(BootFilePath, (startNew ? "new," : "continue,") + ClampSlot(slot));
            RememberLastPlayedSlot(slot);
        }

        public static bool TryPeekBootRequest(out bool startNew, out int slot)
        {
            startNew = false;
            slot = LastPlayedSlot;
            if (!File.Exists(BootFilePath))
            {
                return false;
            }

            string text = File.ReadAllText(BootFilePath).Trim();
            string[] parts = text.Split(',');
            if (parts.Length < 2 || !int.TryParse(parts[1], out slot))
            {
                return false;
            }

            startNew = parts[0] == "new";
            slot = ClampSlot(slot);
            return true;
        }

        public static bool TryConsumeBootRequest(out bool startNew, out int slot)
        {
            if (!TryPeekBootRequest(out startNew, out slot))
            {
                return false;
            }

            if (File.Exists(BootFilePath))
            {
                File.Delete(BootFilePath);
            }

            return true;
        }

        public static void SetActiveSlot(int slot)
        {
            activeSlot = ClampSlot(slot);
            RememberLastPlayedSlot(activeSlot);
        }

        public static void PrepareNewGame(int slot)
        {
            SetActiveSlot(slot);
            DeleteSlotData(slot);
        }

        /// <summary>スロットのセーブ／バックアップ／サムネを削除する。ActiveSlot は変えない。</summary>
        public static void DeleteSlotData(int slot)
        {
            slot = ClampSlot(slot);
            Directory.CreateDirectory(SaveDirectory);
            TryDeleteFile(SaveFilePath(slot));
            TryDeleteFile(BackupFilePath(slot));
            TryDeleteFile(TempFilePath(slot));
            TryDeleteFile(ThumbnailFilePath(slot));
        }

        private static void TryDeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
