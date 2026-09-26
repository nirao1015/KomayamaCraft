using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// タイトル→クラフト入場前の初期化完了フラグ集約。
    /// </summary>
    public static class KomayamaCraftBootReady
    {
        public static bool SaveReady { get; private set; }
        public static bool QuestLogReady { get; private set; }
        public static bool AmbienceReady { get; private set; }

        public static bool IsAllReady => SaveReady && QuestLogReady && AmbienceReady;

        public static void ResetForLoad()
        {
            SaveReady = false;
            QuestLogReady = false;
            AmbienceReady = false;
        }

        public static void NotifySaveReady()
        {
            SaveReady = true;
        }

        public static void NotifyQuestLogReady()
        {
            QuestLogReady = true;
        }

        public static void NotifyAmbienceReady()
        {
            AmbienceReady = true;
        }

        public static string DescribePending()
        {
            return
                $"save={SaveReady} questLog={QuestLogReady} ambience={AmbienceReady}";
        }
    }
}
