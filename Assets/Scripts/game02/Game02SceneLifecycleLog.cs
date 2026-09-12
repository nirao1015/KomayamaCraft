using System;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// game02 シーンの起動〜ゲーム開始までのライフサイクル用コンソールログ（常時出力）。
    /// </summary>
    public static class Game02SceneLifecycleLog
    {
        public const string Tag = "[game02/lifecycle]";

        public static void SceneBootSaveResolution(bool didLoadSave, string detail)
        {
            Log("シーン開始直後（セーブ読込可否確定）", $"didLoadSave={didLoadSave} {detail}");
        }

        public static void InitComplete(string detail)
        {
            Log("イニシャライズ処理終わり", detail);
        }

        public static void StageStartSequenceBegin(string detail)
        {
            Log("ゲーム開始演出開始", detail);
        }

        public static void StageStartSequenceEnd(string detail)
        {
            Log("ゲーム開始演出終了", detail);
        }

        public static void ActualGameplayStart(string detail)
        {
            Log("実ゲーム開始", detail);
        }

        private static void Log(string phase, string detail)
        {
            float u = Time.unscaledTime;
            float r = Time.realtimeSinceStartup;
            int f = Time.frameCount;
            string utc = DateTime.UtcNow.ToString("o");
            Debug.Log($"{Tag} phase={phase} utc={utc} unscaledTime={u:F3}s realtimeSinceStartup={r:F3}s frame={f} {detail}");
        }
    }
}
