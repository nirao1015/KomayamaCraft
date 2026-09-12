using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace KomayamaCraft
{
    public readonly struct KomayamaSaveSlotInfo
    {
        public KomayamaSaveSlotInfo(int slot, bool hasData, DateTime? lastSavedAtLocal)
        {
            Slot = slot;
            HasData = hasData;
            LastSavedAtLocal = lastSavedAtLocal;
        }

        public int Slot { get; }
        public bool HasData { get; }
        public DateTime? LastSavedAtLocal { get; }

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
            string path = File.Exists(SaveFilePath(slot))
                ? SaveFilePath(slot)
                : File.Exists(BackupFilePath(slot))
                    ? BackupFilePath(slot)
                    : null;
            if (path == null)
            {
                return null;
            }

            return File.GetLastWriteTime(path);
        }

        public static KomayamaSaveSlotInfo GetInfo(int slot)
        {
            slot = ClampSlot(slot);
            return new KomayamaSaveSlotInfo(slot, HasData(slot), GetLastSavedAtLocal(slot));
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

        public static bool TryConsumeBootRequest(out bool startNew, out int slot)
        {
            startNew = false;
            slot = LastPlayedSlot;
            if (!File.Exists(BootFilePath))
            {
                return false;
            }

            string text = File.ReadAllText(BootFilePath).Trim();
            File.Delete(BootFilePath);
            string[] parts = text.Split(',');
            if (parts.Length < 2 || !int.TryParse(parts[1], out slot))
            {
                return false;
            }

            startNew = parts[0] == "new";
            slot = ClampSlot(slot);
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
            string savePath = SaveFilePath(slot);
            string backupPath = BackupFilePath(slot);
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }
}
