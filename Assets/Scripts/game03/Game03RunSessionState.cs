using System.Collections.Generic;

/// <summary>
/// game03 1 ランの終了種別（クリア／ゲームオーバー排他）。
/// </summary>
public static class Game03RunSessionState
{
    private static readonly HashSet<int> BossEnhancedWeaponNumbers = new HashSet<int>();

    public static bool IsRunEnded { get; private set; }
    public static bool EndedByClear { get; private set; }
    public static bool EndedByGameOver { get; private set; }
    public static long LastMetaCurrencyGranted { get; private set; }
    public static long MidBossAcquisitionPointsTotal { get; private set; }
    public static long BossAcquisitionPointsTotal { get; private set; }

    public static void ResetForNewRun()
    {
        IsRunEnded = false;
        EndedByClear = false;
        EndedByGameOver = false;
        LastMetaCurrencyGranted = 0;
        MidBossAcquisitionPointsTotal = 0;
        BossAcquisitionPointsTotal = 0;
        BossEnhancedWeaponNumbers.Clear();
    }

    public static void AddMidBossAcquisitionPoints(long points)
    {
        if (points <= 0)
        {
            return;
        }

        MidBossAcquisitionPointsTotal += points;
    }

    public static void AddBossAcquisitionPoints(long points)
    {
        if (points <= 0)
        {
            return;
        }

        BossAcquisitionPointsTotal += points;
    }

    public static bool IsBossWeaponEnhanced(int weaponNumber) =>
        weaponNumber >= 1 && weaponNumber <= 7 && BossEnhancedWeaponNumbers.Contains(weaponNumber);

    /// <summary>BossUG 武器強化を記録。初回のみ true。</summary>
    public static bool TryMarkBossWeaponEnhanced(int weaponNumber)
    {
        if (weaponNumber < 1 || weaponNumber > 7)
        {
            return false;
        }

        return BossEnhancedWeaponNumbers.Add(weaponNumber);
    }

    public static void MarkCleared()
    {
        IsRunEnded = true;
        EndedByClear = true;
    }

    public static void MarkGameOver()
    {
        IsRunEnded = true;
        EndedByGameOver = true;
    }

    public static void RecordMetaCurrencyGranted(long amount)
    {
        LastMetaCurrencyGranted = amount > 0 ? amount : 0;
    }
}
