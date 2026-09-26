using UnityEngine;

/// <summary>
/// 全体設定のフレームレート上限。<see cref="SoundSettingsManager"/>／設定 UI から切り替える。
/// </summary>
public enum GameFrameRateMode
{
    /// <summary>上限なし（VSync オフ・targetFrameRate = -1）。</summary>
    Unlimited = 0,
    /// <summary>約 60 FPS。</summary>
    Cap60 = 1,
    /// <summary>約 30 FPS。</summary>
    Cap30 = 2
}

/// <summary>
/// <see cref="Application.targetFrameRate"/> への反映。設定変更・起動時に呼ぶ。
/// </summary>
public static class GameFrameRate
{
    public const string PersistUnlimited = "unlimited";
    public const string Persist60 = "60";
    public const string Persist30 = "30";

    public static GameFrameRateMode DefaultMode => GameFrameRateMode.Cap60;

    public static void Apply(GameFrameRateMode mode)
    {
        // 上限指定を効かせるため VSync は切る（Quality の VSync と競合させない）
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = ResolveTargetFrameRate(mode);
    }

    public static int ResolveTargetFrameRate(GameFrameRateMode mode)
    {
        switch (mode)
        {
            case GameFrameRateMode.Unlimited:
                return -1;
            case GameFrameRateMode.Cap30:
                return 30;
            default:
                return 60;
        }
    }

    public static string ToPersistId(GameFrameRateMode mode)
    {
        switch (mode)
        {
            case GameFrameRateMode.Unlimited:
                return PersistUnlimited;
            case GameFrameRateMode.Cap30:
                return Persist30;
            default:
                return Persist60;
        }
    }

    public static GameFrameRateMode FromPersistId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return DefaultMode;
        }

        string normalized = id.Trim().ToLowerInvariant();
        if (normalized == PersistUnlimited ||
            normalized == "0" ||
            normalized == "-1" ||
            normalized == "none" ||
            normalized == "uncapped")
        {
            return GameFrameRateMode.Unlimited;
        }

        if (normalized == Persist30 || normalized == "cap30")
        {
            return GameFrameRateMode.Cap30;
        }

        if (normalized == Persist60 ||
            normalized == "cap60" ||
            normalized == "default")
        {
            return GameFrameRateMode.Cap60;
        }

        return DefaultMode;
    }
}
