using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ビルドプロファイルの Version（実行時は <see cref="Application.version"/>）の取得と、
/// プレフィックス無視の数値セグメント比較。
/// </summary>
public static class GameBuildVersion
{
    /// <summary>アクティブな Build Profile の Player Settings 上書きを含む現在の Version 文字列。</summary>
    public static string GetCurrentApplicationVersion()
    {
        return Application.version ?? string.Empty;
    }

    /// <summary>
    /// <paramref name="a"/> が <paramref name="b"/> より新しい（大きい）とき正、等しいとき 0、古いとき負。
    /// </summary>
    public static int Compare(string a, string b)
    {
        if (!TryParseNumericComponents(a, out int[] componentsA))
        {
            componentsA = Array.Empty<int>();
        }

        if (!TryParseNumericComponents(b, out int[] componentsB))
        {
            componentsB = Array.Empty<int>();
        }

        int maxLength = Mathf.Max(componentsA.Length, componentsB.Length);
        for (int i = 0; i < maxLength; i++)
        {
            int valueA = i < componentsA.Length ? componentsA[i] : 0;
            int valueB = i < componentsB.Length ? componentsB[i] : 0;
            if (valueA != valueB)
            {
                return valueA.CompareTo(valueB);
            }
        }

        return 0;
    }

    public static bool IsNewerThan(string a, string b)
    {
        return Compare(a, b) > 0;
    }

    /// <summary>
    /// 先頭から最初の数字が現れる位置以降を対象に、<c>.</c> 区切りの整数列を抽出する。
    /// 例: <c>α-4.0.1</c> → 4,0,1 / <c>β2.3.1</c> → 2,3,1
    /// </summary>
    public static bool TryParseNumericComponents(string version, out int[] components)
    {
        components = Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        int firstDigitIndex = -1;
        for (int i = 0; i < version.Length; i++)
        {
            if (char.IsDigit(version[i]))
            {
                firstDigitIndex = i;
                break;
            }
        }

        if (firstDigitIndex < 0)
        {
            return false;
        }

        string numericTail = version.Substring(firstDigitIndex);
        string[] segments = numericTail.Split('.');
        var parsed = new List<int>(segments.Length);
        for (int s = 0; s < segments.Length; s++)
        {
            string segment = segments[s];
            if (segment.Length == 0)
            {
                parsed.Add(0);
                continue;
            }

            int digitEnd = 0;
            while (digitEnd < segment.Length && char.IsDigit(segment[digitEnd]))
            {
                digitEnd++;
            }

            if (digitEnd == 0)
            {
                parsed.Add(0);
                continue;
            }

            if (!int.TryParse(segment.Substring(0, digitEnd), out int value))
            {
                return false;
            }

            parsed.Add(value);
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        components = parsed.ToArray();
        return true;
    }
}
