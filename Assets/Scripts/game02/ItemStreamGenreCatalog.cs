using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class ItemStreamGenreCatalog : MonoBehaviour
{
    public const string DefaultGenre = "未設定";

    [Header("Genre Sources (TextAsset)")]
    [SerializeField] private TextAsset normalGenreListJson;
    [SerializeField] private TextAsset specialGenreListJson;

    [Header("Selection Rule")]
    [Range(0f, 1f)]
    [SerializeField] private float normalListProbability = 0.8f;
    [SerializeField] private int maxGenreLength = 14;

    public string PickRandomGenre()
    {
        List<string> selected = ShouldUseNormalList() ? GetValidNormalGenres() : GetValidSpecialGenres();
        if (selected.Count <= 0)
        {
            return DefaultGenre;
        }

        int index = UnityEngine.Random.Range(0, selected.Count);
        return selected[index];
    }

    private bool ShouldUseNormalList()
    {
        float threshold = Mathf.Clamp01(normalListProbability);
        return UnityEngine.Random.value < threshold;
    }

    private List<string> GetValidNormalGenres()
    {
        return ParseAndFilterGenres(normalGenreListJson);
    }

    private List<string> GetValidSpecialGenres()
    {
        return ParseAndFilterGenres(specialGenreListJson);
    }

    private List<string> ParseAndFilterGenres(TextAsset source)
    {
        var result = new List<string>();
        if (source == null || string.IsNullOrEmpty(source.text))
        {
            return result;
        }

        string text = source.text;
        MatchCollection matches = Regex.Matches(text, "\"((?:[^\"\\\\]|\\\\.)*)\"");
        int lengthLimit = Math.Max(0, maxGenreLength);

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            string raw = Regex.Unescape(match.Groups[1].Value);
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            if (raw.Length > lengthLimit)
            {
                continue;
            }

            result.Add(raw);
        }

        return result;
    }
}
