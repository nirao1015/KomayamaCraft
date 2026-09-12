using System;
using UnityEngine;

/// <summary>
/// メタ通貨・人気表示の換算（spec/game03/game03_meta_growth_menu03_spec.md）。
/// </summary>
public static class Game03MetaEconomyRules
{
    /// <summary>人気 UI・リザルト表示用の倍率（実値 × この値を表示）。</summary>
    public const int PopularDisplayMultiplier = 100;

    /// <summary>経験値実績→メタ換算の基礎係数（生存時間帯・フェーズ倍率の前）。</summary>
    public const float LifetimeExperienceToMetaScale = 1.20f;

    /// <summary>生存 0〜2 分（秒）の時間帯倍率。</summary>
    public const float RunTimeMultiplierMinutes0To2 = 3f;

    /// <summary>生存 2〜5 分（秒）の時間帯倍率。3〜5 分意図＋2〜3 分のつなぎ。</summary>
    public const float RunTimeMultiplierMinutes2To5 = 2f;

    /// <summary>生存 5〜8 分（秒）の時間帯倍率。6〜8 分意図＋5〜6 分のつなぎ。</summary>
    public const float RunTimeMultiplierMinutes5To8 = 1.5f;

    /// <summary>8 分超の時間帯倍率。</summary>
    public const float RunTimeMultiplierAfter8Minutes = 1f;

    /// <summary>ラン終了時メタ通貨へ換算する入力。</summary>
    public readonly struct MetaPopularityGrantInputs
    {
        public readonly int LifetimeExperienceEarned;
        public readonly float GameplayElapsedSeconds;
        public readonly int EnemySpawnPhaseAtRunEnd;

        public MetaPopularityGrantInputs(int lifetimeExperienceEarned, float gameplayElapsedSeconds, int enemySpawnPhaseAtRunEnd)
        {
            LifetimeExperienceEarned = Mathf.Max(0, lifetimeExperienceEarned);
            GameplayElapsedSeconds = Mathf.Max(0f, gameplayElapsedSeconds);
            EnemySpawnPhaseAtRunEnd = Mathf.Clamp(enemySpawnPhaseAtRunEnd, 1, Game03EnemyPhaseTimeline.MaxEnemySpawnPhase);
        }
    }

    public static int ComputePopularDisplayValue(int popularDisplayExperienceTotal)
    {
        long scaled = (long)popularDisplayExperienceTotal * PopularDisplayMultiplier;
        if (scaled > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)scaled;
    }

    /// <summary>
    /// 経験値実績のみをメタに換算（スマホ等の表示専用ボーナスは含めない）。
    /// 換算 = Round(実績 × <see cref="LifetimeExperienceToMetaScale"/> × 生存時間帯倍率 × フェーズ倍率)。
    /// </summary>
    public static long ComputeMetaCurrencyFromLifetimeExperience(in MetaPopularityGrantInputs inputs)
    {
        if (inputs.LifetimeExperienceEarned <= 0)
        {
            return 0;
        }

        float timeMultiplier = ComputeRunTimePopularityMultiplier(inputs.GameplayElapsedSeconds);
        float phaseMultiplier = GetPhasePopularityMultiplier(inputs.EnemySpawnPhaseAtRunEnd);
        double scaled = inputs.LifetimeExperienceEarned
            * LifetimeExperienceToMetaScale
            * timeMultiplier
            * phaseMultiplier;
        if (scaled <= 0d)
        {
            return 0;
        }

        return Math.Max(0L, (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    public static long ComputeMetaCurrencyFromLifetimeExperience(
        int lifetimeExperienceEarned,
        float gameplayElapsedSeconds,
        int enemySpawnPhaseAtRunEnd)
    {
        return ComputeMetaCurrencyFromLifetimeExperience(
            new MetaPopularityGrantInputs(lifetimeExperienceEarned, gameplayElapsedSeconds, enemySpawnPhaseAtRunEnd));
    }

    /// <summary>
    /// ラン生存時間による人気→メタ換算の倍率（序盤厚めの段階倍率。旧 0.7〜1.1 線形補間は廃止）。
    /// </summary>
    public static float ComputeRunTimePopularityMultiplier(float gameplayElapsedSeconds)
    {
        float seconds = Mathf.Max(0f, gameplayElapsedSeconds);
        if (seconds <= 120f)
        {
            return RunTimeMultiplierMinutes0To2;
        }

        if (seconds <= 300f)
        {
            return RunTimeMultiplierMinutes2To5;
        }

        if (seconds <= 480f)
        {
            return RunTimeMultiplierMinutes5To8;
        }

        return RunTimeMultiplierAfter8Minutes;
    }

    /// <summary>敵出現フェーズ（1〜7）による人気→メタ換算の倍率。序盤 GO を厚く、後半を抑える。</summary>
    public static float GetPhasePopularityMultiplier(int enemySpawnPhase1Based)
    {
        switch (Mathf.Clamp(enemySpawnPhase1Based, 1, Game03EnemyPhaseTimeline.MaxEnemySpawnPhase))
        {
            case 1:
                return 1.50f;
            case 2:
                return 1.00f;
            case 3:
                return 0.94f;
            case 4:
                return 0.86f;
            case 5:
                return 0.78f;
            case 6:
                return 0.72f;
            case 7:
                return 0.66f;
            default:
                return 0.66f;
        }
    }

    /// <summary>デバッグ／ログ用。換算内訳を返す。</summary>
    public static void DecomposeMetaCurrencyFromLifetimeExperience(
        in MetaPopularityGrantInputs inputs,
        out float timeMultiplier,
        out float phaseMultiplier,
        out long grant)
    {
        timeMultiplier = ComputeRunTimePopularityMultiplier(inputs.GameplayElapsedSeconds);
        phaseMultiplier = GetPhasePopularityMultiplier(inputs.EnemySpawnPhaseAtRunEnd);
        grant = ComputeMetaCurrencyFromLifetimeExperience(inputs);
    }
}
