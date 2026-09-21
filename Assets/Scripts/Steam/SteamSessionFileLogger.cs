using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// <see cref="TitleDebugManager"/> の Steam ログファイルモード用セッションログ。
/// セーブ書き込み・実績判定を 1 ファイルに追記する（Auto-Cloud 対象外の診断用）。
/// </summary>
public static class SteamSessionFileLogger
{
    private static readonly object WriteLock = new object();

    private static bool isActive;
    private static string logFilePath;

    public static bool IsActive => isActive;

    public static string LogFilePath => logFilePath;

    public static void BeginSessionIfEnabled(bool enabled)
    {
        if (!enabled)
        {
            isActive = false;
            logFilePath = null;
            return;
        }

        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "SteamDebugLogs");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            logFilePath = Path.Combine(directory, $"steam_session_{stamp}.log");
            isActive = true;

            WriteLineCore(
                "SESSION",
                $"log started path={logFilePath} persistentDataPath={Application.persistentDataPath}");
        }
        catch (Exception ex)
        {
            isActive = false;
            logFilePath = null;
            Debug.LogWarning($"[SteamSessionFileLogger] failed to start: {ex.Message}");
        }
    }

    public static void LogSaveWrite(string fileLabel, string fullPath, string contentSummary)
    {
        if (!isActive)
        {
            return;
        }

        WriteLineCore(
            "SAVE",
            $"file={fileLabel} path={fullPath} data={contentSummary}");
    }

    public static void LogAchievement(
        string achievementId,
        string outcome,
        string detail = null)
    {
        if (!isActive || string.IsNullOrEmpty(achievementId))
        {
            return;
        }

        string message = $"id={achievementId} result={outcome}";
        if (!string.IsNullOrEmpty(detail))
        {
            message += $" detail={detail}";
        }

        WriteLineCore("ACHIEVEMENT", message);
    }

    public static string SummarizeGame02Save(Game02.Game02SaveData data)
    {
        if (data == null)
        {
            return "null";
        }

        long pop = data.gameManagerState != null ? data.gameManagerState.currentPopularity : 0L;
        return
            $"version={data.version} updatedAtUtc={data.updatedAtUtc} buildVersion={data.buildVersion} popularity={pop} " +
            $"gameCleared={data.gameManagerState?.isGameCleared} hasShownGameClearedPanel={data.gameManagerState?.hasShownGameClearedPanel}";
    }

    public static string SummarizeGame03MetaSave(Game03MetaSaveData data)
    {
        if (data == null)
        {
            return "null";
        }

        return
            $"version={data.version} updatedAtUtc={data.updatedAtUtc} buildVersion={data.buildVersion} metaCurrency={data.metaCurrency} " +
            $"spent={data.totalSpentOnUpgrades} atk={data.attackPowerLevel} heal={data.autoHealLevel} " +
            $"atkCnt={data.attackCountLevel} swift={data.swiftnessLevel} reroll={data.rerollLevel} cd={data.cooldownReductionLevel} " +
            $"companionMask={data.acquiredCompanionWeaponMask}";
    }

    public static string SummarizePlayerDataSave(
        int version,
        string updatedAtUtc,
        string buildVersion,
        int master,
        int bgm,
        int se,
        bool game01Cleared,
        bool game02Cleared,
        bool game03Cleared)
    {
        return
            $"version={version} updatedAtUtc={updatedAtUtc} buildVersion={buildVersion} master={master} bgm={bgm} se={se} " +
            $"cleared01={game01Cleared} cleared02={game02Cleared} cleared03={game03Cleared}";
    }

    private static void WriteLineCore(string category, string message)
    {
        if (!isActive || string.IsNullOrEmpty(logFilePath))
        {
            return;
        }

        string line = $"{DateTime.UtcNow:o} [{category}] {message}";
        lock (WriteLock)
        {
            try
            {
                File.AppendAllText(logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamSessionFileLogger] write failed: {ex.Message}");
            }
        }
    }
}
