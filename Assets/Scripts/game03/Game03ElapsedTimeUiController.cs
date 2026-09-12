using TMPro;
using UnityEngine;

/// <summary>
/// TimeUIBackground 配下の TimeUI に、ゲーム内経過時間を表示する。
/// 表示形式は game02 の Game02ElapsedTimeUiController と同じ（1時間超えれば Xh:YYm:ZZs、未満なら XXm:YYs）。
/// 経過時間は Game03Manager.GameplayElapsedSeconds（ポーズ・カットシーン中は進まない）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03ElapsedTimeUiController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private TMP_Text timeUiText;
    [SerializeField] private string timeUiChildName = "TimeUI";

    private const int MaxDisplaySeconds = (999 * 3600) + (59 * 60) + 59;
    private int lastDisplayedWholeSeconds = -1;

    private void Awake()
    {
        ResolveTimeUiTextIfNeeded();
        RefreshTimeText(force: true);
    }

    private void Update()
    {
        RefreshTimeText(force: false);
    }

    private void ResolveTimeUiTextIfNeeded()
    {
        if (timeUiText != null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(timeUiChildName))
        {
            Transform byName = transform.Find(timeUiChildName);
            if (byName != null)
            {
                timeUiText = byName.GetComponent<TMP_Text>();
                if (timeUiText != null)
                {
                    return;
                }
            }
        }

        TMP_Text[] allText = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allText.Length; i++)
        {
            TMP_Text candidate = allText[i];
            if (candidate != null && candidate.name == "TimeUI")
            {
                timeUiText = candidate;
                return;
            }
        }
    }

    private void RefreshTimeText(bool force)
    {
        ResolveTimeUiTextIfNeeded();
        if (timeUiText == null)
        {
            return;
        }

        float rawElapsedSeconds = 0f;
        if (game03Manager != null)
        {
            rawElapsedSeconds = Mathf.Max(0f, game03Manager.GameplayElapsedSeconds);
        }

        int wholeSeconds = Mathf.Clamp(Mathf.FloorToInt(rawElapsedSeconds), 0, MaxDisplaySeconds);
        if (!force && wholeSeconds == lastDisplayedWholeSeconds)
        {
            return;
        }

        lastDisplayedWholeSeconds = wholeSeconds;
        timeUiText.text = FormatElapsed(wholeSeconds);
    }

    private static string FormatElapsed(int seconds)
    {
        int clamped = Mathf.Clamp(seconds, 0, MaxDisplaySeconds);
        int hours = clamped / 3600;
        int minutes = (clamped % 3600) / 60;
        int secs = clamped % 60;

        if (hours > 0)
        {
            return $"{hours}h:{minutes:00}m:{secs:00}s";
        }

        return $"{minutes:00}m:{secs:00}s";
    }
}
