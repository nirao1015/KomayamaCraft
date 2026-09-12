using System.IO;
using UnityEngine;

namespace DialogueScene
{
    /// <summary>
    /// 会話 CSV のキー解決。ランタイムは <see cref="DialogueScriptCatalog"/>（Resources/GameData）を正とする。
    /// </summary>
    public static class DialogueCsvPaths
    {
        private const string EditorDialogueFolder = "GameData/Dialogue";

        /// <summary>
        /// Editor 用: <c>Assets/GameData/Dialogue/{sceneName}-{stageName}.csv</c> の絶対パス。
        /// </summary>
        public static string GetEditorDialogueCsvPath(string sceneName, string stageName)
        {
            string safeScene = SanitizeKey(sceneName, "game_stage_scene");
            string safeStage = SanitizeKey(stageName, "null");
            return Path.Combine(Application.dataPath, EditorDialogueFolder, $"{safeScene}-{safeStage}.csv");
        }

        public static string FormatDialogueFileBaseName(string sceneName, string stageName)
        {
            string safeScene = SanitizeKey(sceneName, "game_stage_scene");
            string safeStage = SanitizeKey(stageName, "null");
            return $"{safeScene}-{safeStage}";
        }

        /// <summary>
        /// Inspector 等の複合キー（例: <c>game03_scene-ed</c>）を CSV 用の scene / stage に分解する。
        /// 最初の <c>-</c> で区切る（<c>game_stage_scene-stage_01</c> に対応）。
        /// </summary>
        public static bool TryParseCompositeSceneStage(string composite, out string sceneName, out string stageName)
        {
            sceneName = null;
            stageName = null;
            if (string.IsNullOrWhiteSpace(composite))
            {
                return false;
            }

            string trimmed = composite.Trim();
            int separator = trimmed.IndexOf('-');
            if (separator <= 0 || separator >= trimmed.Length - 1)
            {
                return false;
            }

            sceneName = trimmed.Substring(0, separator).Trim();
            stageName = trimmed.Substring(separator + 1).Trim();
            return !string.IsNullOrEmpty(sceneName) && !string.IsNullOrEmpty(stageName);
        }

        private static string SanitizeKey(string raw, string fallback)
        {
            string safe = string.IsNullOrWhiteSpace(raw)
                ? fallback
                : Path.GetFileNameWithoutExtension(raw.Trim());
            if (string.IsNullOrEmpty(safe))
            {
                safe = fallback;
            }

            return safe;
        }

        /// <summary>
        /// エディタ・デバッグ用: プロジェクト直下の <c>spec/dialogue_sample.csv</c>（Assets の親フォルダ基準）。
        /// ビルドでは存在しない場合がある。
        /// </summary>
        public static string GetProjectSpecDialogueSamplePath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "spec", "dialogue_sample.csv"));
        }
    }
}
