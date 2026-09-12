using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class MovieNameCatalog : MonoBehaviour
{
    public const string DefaultMovieName = "禁忌";

    [Header("Movie Name Sources (TextAsset)")]
    [SerializeField] private TextAsset normalMovieNameListJson;
    [SerializeField] private TextAsset specialMovieNameListJson;
    [SerializeField] private TextAsset execMovieNameListJson;

    [Header("Selection Rule")]
    [Range(0f, 1f)]
    [SerializeField] private float normalListProbability = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float execSuffixProbability = 0.65f;
    [SerializeField] private int maxMovieNameLength = 40;
    [SerializeField] private int maxRetryCount = 5;

    public string PickRandomMovieName(string streamGenre)
    {
        string safeGenre = RemoveAllLineBreaks(streamGenre);
        int retries = Mathf.Max(1, maxRetryCount);
        for (int attempt = 0; attempt < retries; attempt++)
        {
            string baseTemplate = PickBaseTemplate();
            if (string.IsNullOrEmpty(baseTemplate))
            {
                continue;
            }

            string name = baseTemplate.Replace("{genre}", safeGenre ?? string.Empty);
            if (ShouldAppendExecSuffix())
            {
                string suffixTemplate = PickExecTemplate();
                if (!string.IsNullOrEmpty(suffixTemplate))
                {
                    int n = PickWeightedPartNumber();
                    name += suffixTemplate.Replace("{n}", n.ToString());
                }
            }

            string sanitized = RemoveAllLineBreaks(name);
            if (string.IsNullOrEmpty(sanitized))
            {
                continue;
            }

            if (sanitized.Length <= Mathf.Max(1, maxMovieNameLength))
            {
                return sanitized;
            }
        }

        return DefaultMovieName;
    }

    private string PickBaseTemplate()
    {
        List<string> selected = ShouldUseNormalList() ? GetValidNormalTemplates() : GetValidSpecialTemplates();
        if (selected.Count <= 0)
        {
            return string.Empty;
        }

        int index = UnityEngine.Random.Range(0, selected.Count);
        return selected[index];
    }

    private string PickExecTemplate()
    {
        List<string> selected = GetValidExecTemplates();
        if (selected.Count <= 0)
        {
            return string.Empty;
        }

        int index = UnityEngine.Random.Range(0, selected.Count);
        return selected[index];
    }

    private int PickWeightedPartNumber()
    {
        int totalWeight = 0;
        for (int n = 1; n <= 29; n++)
        {
            totalWeight += 30 - n;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;
        for (int n = 1; n <= 29; n++)
        {
            cumulative += 30 - n;
            if (roll < cumulative)
            {
                return n;
            }
        }

        return 29;
    }

    private bool ShouldUseNormalList()
    {
        return UnityEngine.Random.value < Mathf.Clamp01(normalListProbability);
    }

    private bool ShouldAppendExecSuffix()
    {
        return UnityEngine.Random.value < Mathf.Clamp01(execSuffixProbability);
    }

    private List<string> GetValidNormalTemplates()
    {
        return ParseStringArray(normalMovieNameListJson);
    }

    private List<string> GetValidSpecialTemplates()
    {
        return ParseStringArray(specialMovieNameListJson);
    }

    private List<string> GetValidExecTemplates()
    {
        return ParseStringArray(execMovieNameListJson);
    }

    private static List<string> ParseStringArray(TextAsset source)
    {
        var result = new List<string>();
        if (source == null || string.IsNullOrEmpty(source.text))
        {
            return result;
        }

        MatchCollection matches = Regex.Matches(source.text, "\"((?:[^\"\\\\]|\\\\.)*)\"");
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            string raw = Regex.Unescape(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(raw))
            {
                result.Add(raw);
            }
        }

        return result;
    }

    private static string RemoveAllLineBreaks(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
