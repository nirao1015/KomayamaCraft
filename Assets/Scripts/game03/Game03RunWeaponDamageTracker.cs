using UnityEngine;

/// <summary>
/// ラン中に武器番号（W01〜W07）ごとに与えた HP ダメージを集計する（オーバーキル除外・非武器は記録しない）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03RunWeaponDamageTracker : MonoBehaviour
{
    private static Game03RunWeaponDamageTracker instance;

    private readonly long[] damageByWeaponNumber = new long[8];

    public static Game03RunWeaponDamageTracker TryGet()
    {
        if (instance != null)
        {
            return instance;
        }

        return FindAnyObjectByType<Game03RunWeaponDamageTracker>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        ResetRunTotals();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void ResetRunTotals()
    {
        for (int i = 0; i < damageByWeaponNumber.Length; i++)
        {
            damageByWeaponNumber[i] = 0L;
        }
    }

    /// <summary>敵に実際に削った HP（オーバーキル分を除く）。</summary>
    public void RecordWeaponHitDamage(int weaponNumber, int actualHpRemoved)
    {
        if (weaponNumber < 1 || weaponNumber > 7 || actualHpRemoved <= 0)
        {
            return;
        }

        damageByWeaponNumber[weaponNumber] += actualHpRemoved;
    }

    public long GetDamageForWeapon(int weaponNumber)
    {
        if (weaponNumber < 1 || weaponNumber > 7)
        {
            return 0L;
        }

        return damageByWeaponNumber[weaponNumber];
    }

    public static int ComputeActualHpRemoved(int damagePerHit, int remainingHpBeforeHit)
    {
        if (remainingHpBeforeHit <= 0)
        {
            return 0;
        }

        int dmg = Mathf.Max(1, damagePerHit);
        return Mathf.Min(dmg, remainingHpBeforeHit);
    }
}
