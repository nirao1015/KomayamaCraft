using UnityEngine;

namespace DialogueScene
{
    /// <summary>
    /// stand 行の CSV 列: speaker=アンカー(x,y)、text=位置(x,y)、audio_key=サイズ(width,height)。
    /// 空欄は呼び出し側のスロット既定値を使う。
    /// </summary>
    public static class DialogueStandCsvLayout
    {
        public static bool TryParsePosition(string text, out Vector2 position)
        {
            position = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return TryParsePair(text, out position.x, out position.y);
        }

        public static bool TryParseSize(string audioKey, out Vector2 size)
        {
            size = default;
            if (string.IsNullOrWhiteSpace(audioKey))
            {
                return false;
            }

            return TryParsePair(audioKey, out size.x, out size.y);
        }

        private static bool TryParsePair(string raw, out float first, out float second)
        {
            first = 0f;
            second = 0f;
            string[] parts = raw.Split(',');
            if (parts.Length < 1 || parts.Length > 2)
            {
                return false;
            }

            bool hasFirst = parts.Length >= 1 && TryParseFloat(parts[0], out first);
            bool hasSecond = parts.Length >= 2 && TryParseFloat(parts[1], out second);
            if (parts.Length == 1)
            {
                return hasFirst;
            }

            return hasFirst && hasSecond;
        }

        private static bool TryParseFloat(string token, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return float.TryParse(token.Trim(), out value);
        }
    }
}
