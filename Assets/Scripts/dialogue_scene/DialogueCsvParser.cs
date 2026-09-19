using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DialogueScene
{
    /// <summary>
    /// 会話台本 CSV（<c>spec/dialogue_scene.md</c> §8）のパーサ。
    /// </summary>
    public static class DialogueCsvParser
    {
        private const int ColumnCount = 7;
        private static readonly string[] ExpectedHeader =
        {
            "type", "speaker", "text", "slot", "sprite_key", "mode", "audio_key"
        };

        /// <summary>
        /// Editor 用: <see cref="DialogueCsvPaths.GetEditorDialogueCsvPath"/> のパスを読む。
        /// </summary>
        public static IReadOnlyList<DialogueRow> ParseFromEditorGameDataStage(string sceneName, string stageName)
        {
            return ParseFromFilePath(DialogueCsvPaths.GetEditorDialogueCsvPath(sceneName, stageName));
        }

        /// <summary>
        /// ファイルを UTF-8 で読み、パースする。
        /// </summary>
        public static IReadOnlyList<DialogueRow> ParseFromFilePath(string absoluteOrProjectRelativePath)
        {
            if (string.IsNullOrEmpty(absoluteOrProjectRelativePath))
            {
                throw new ArgumentException("Path is null or empty.", nameof(absoluteOrProjectRelativePath));
            }

            string path = Path.IsPathRooted(absoluteOrProjectRelativePath)
                ? absoluteOrProjectRelativePath
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", absoluteOrProjectRelativePath));

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Dialogue CSV not found.", path);
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            return ParseFromText(text);
        }

        /// <summary>
        /// CSV 文字列全体をパースする（BOM 付き UTF-8 も可）。
        /// </summary>
        public static IReadOnlyList<DialogueRow> ParseFromText(string csvText)
        {
            if (csvText == null)
            {
                throw new ArgumentNullException(nameof(csvText));
            }

            if (csvText.Length > 0 && csvText[0] == '\ufeff')
            {
                csvText = csvText.Substring(1);
            }

            List<string[]> rawRows = SplitCsvRows(csvText);
            if (rawRows.Count == 0)
            {
                return Array.Empty<DialogueRow>();
            }

            ValidateHeader(rawRows[0]);

            var result = new List<DialogueRow>(rawRows.Count - 1);
            for (int i = 1; i < rawRows.Count; i++)
            {
                string[] cells = NormalizeRow(rawRows[i]);
                if (IsEmptyRow(cells))
                {
                    continue;
                }

                string typeToken = cells[0].Trim();
                if (string.IsNullOrEmpty(typeToken))
                {
                    Debug.LogWarning($"[DialogueCsvParser] Row {i + 1}: empty type, skipped.");
                    continue;
                }

                DialogueRowType type = ParseRowType(typeToken);
                if (type == DialogueRowType.Unknown)
                {
                    Debug.LogWarning($"[DialogueCsvParser] Row {i + 1}: unknown type '{typeToken}', skipped.");
                    continue;
                }

                result.Add(new DialogueRow(
                    type,
                    cells[1],
                    cells[2],
                    cells[3],
                    cells[4],
                    cells[5],
                    cells[6]));
            }

            return result;
        }

        private static void ValidateHeader(string[] header)
        {
            if (header.Length < ColumnCount)
            {
                Debug.LogWarning("[DialogueCsvParser] Header has fewer columns than expected.");
            }

            for (int i = 0; i < Mathf.Min(ColumnCount, header.Length); i++)
            {
                string expected = ExpectedHeader[i];
                string actual = header[i].Trim();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[DialogueCsvParser] Header column {i}: expected '{expected}', got '{actual}'. Parsing continues by column index.");
                }
            }
        }

        private static string[] NormalizeRow(string[] row)
        {
            var cells = new string[ColumnCount];
            if (row == null || row.Length == 0)
            {
                return cells;
            }

            // 末尾の空セルは Excel 等の余分なカンマでよく付くため落とす（7 列に収める）
            int effectiveLen = row.Length;
            while (effectiveLen > ColumnCount && string.IsNullOrEmpty(row[effectiveLen - 1]?.Trim()))
            {
                effectiveLen--;
            }

            int n = Mathf.Min(ColumnCount, effectiveLen);
            for (int i = 0; i < n; i++)
            {
                cells[i] = row[i]?.Trim() ?? string.Empty;
            }

            if (effectiveLen > ColumnCount)
            {
                Debug.LogWarning($"[DialogueCsvParser] Row has {effectiveLen} columns; extras are ignored.");
            }

            return cells;
        }

        private static bool IsEmptyRow(string[] cells)
        {
            foreach (string c in cells)
            {
                if (!string.IsNullOrEmpty(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static DialogueRowType ParseRowType(string token)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "line":
                    return DialogueRowType.Line;
                case "bg":
                    return DialogueRowType.Bg;
                case "stand":
                    return DialogueRowType.Stand;
                case "se":
                    return DialogueRowType.Se;
                case "bgm":
                    return DialogueRowType.Bgm;
                case "bgm_stop":
                    return DialogueRowType.BgmStop;
                case "fx_shake":
                    return DialogueRowType.FxShake;
                case "fx_shake_start":
                    return DialogueRowType.FxShakeStart;
                case "fx_shake_stop":
                    return DialogueRowType.FxShakeStop;
                case "fx_game03_ed_finale":
                    return DialogueRowType.FxGame03EdFinale;
                case "dim":
                    return DialogueRowType.Dim;
                case "camera":
                    return DialogueRowType.Camera;
                case "end":
                    return DialogueRowType.End;
                default:
                    return DialogueRowType.Unknown;
            }
        }

        /// <summary>
        /// RFC 4180 風: カンマ区切り、ダブルクォートで囲み、<c>""</c> でクォートエスケープ。
        /// </summary>
        internal static List<string[]> SplitCsvRows(string text)
        {
            var allRows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                    }
                    else if (c == '\n')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        allRows.Add(row);
                        row = new List<string>();
                    }
                    else if (c != '\r')
                    {
                        field.Append(c);
                    }
                }
            }

            row.Add(field.ToString());
            if (RowHasMeaningfulContent(row))
            {
                allRows.Add(row);
            }

            var arrays = new List<string[]>(allRows.Count);
            foreach (List<string> r in allRows)
            {
                arrays.Add(r.ToArray());
            }

            return arrays;
        }

        private static bool RowHasMeaningfulContent(List<string> row)
        {
            if (row == null || row.Count == 0)
            {
                return false;
            }

            if (row.Count > 1)
            {
                return true;
            }

            return !string.IsNullOrEmpty(row[0]);
        }
    }
}
