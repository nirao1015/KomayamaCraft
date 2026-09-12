using UnityEngine;

/// <summary>
/// ラン開始時にメタ進行をプレイへ反映する（Inspector 参照のみ）。
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class Game03MetaGameplayApplier : MonoBehaviour
{
    private const float SwiftnessPercentPerLevel = 0.08f;
    private const float AutoHealHpPerSecondPerLevel = 0.25f;
    private const int RerollChargesPerMetaLevel = 3;

    [Header("References")]
    [SerializeField] private Game03UnitManager unitManager;
    [SerializeField] private Game03StatusManager statusManager;
    [SerializeField] private Game03WeaponManager weaponManager;

    private void Start()
    {
        Game03MetaProgressController meta = Game03MetaProgressController.Instance;
        if (meta == null)
        {
            return;
        }

        if (unitManager != null)
        {
            float swiftnessAdditive = unitManager.BaseMoveSpeed * (meta.SwiftnessLevel * SwiftnessPercentPerLevel);
            float healPerSecond = meta.AutoHealLevel * AutoHealHpPerSecondPerLevel;
            unitManager.SetMetaGameplayModifiers(swiftnessAdditive, healPerSecond);
        }

        if (statusManager != null && meta.RerollLevel > 0)
        {
            statusManager.AddUgRerollCharges(meta.RerollLevel * RerollChargesPerMetaLevel);
        }

        if (weaponManager != null)
        {
            weaponManager.ApplyMetaStartingUpgrades(
                meta.AttackPowerLevel,
                meta.AttackCountLevel,
                meta.CooldownReductionLevel);
        }
    }
}
