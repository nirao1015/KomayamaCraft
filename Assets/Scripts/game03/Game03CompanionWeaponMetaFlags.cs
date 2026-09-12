/// <summary>
/// game03 メタセーブ上の仲間武器（Weapon02〜07）入手フラグ。ビット0=02 … ビット5=07。
/// </summary>
public static class Game03CompanionWeaponMetaFlags
{
    public const int AllWeaponsMask = 63;

    public static bool IsCompanionWeaponNumber(int weaponNumber) => weaponNumber >= 2 && weaponNumber <= 7;

    public static int WeaponNumberToBit(int weaponNumber) => 1 << (weaponNumber - 2);

    public static bool HasWeapon(int mask, int weaponNumber)
    {
        if (!IsCompanionWeaponNumber(weaponNumber))
        {
            return false;
        }

        return (mask & WeaponNumberToBit(weaponNumber)) != 0;
    }

    public static bool HasAllWeapons(int mask) => (mask & AllWeaponsMask) == AllWeaponsMask;

    public static int AddWeapon(int mask, int weaponNumber)
    {
        if (!IsCompanionWeaponNumber(weaponNumber))
        {
            return mask;
        }

        return mask | WeaponNumberToBit(weaponNumber);
    }

    public static int ClampMask(int mask) => mask & AllWeaponsMask;
}
