using UnityEngine;
using UnityEngine.UI;

public class Weapon05Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform wo5ImageRect;
    [SerializeField] private Image wo5Image;
    [SerializeField] private RectTransform wo5HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 3;
    [SerializeField, Min(0f)] private float throwDistancePixels = 0f;
    [SerializeField, Min(0.01f)] private float speedPixelsPerSecond = 300f;
    [SerializeField, Min(0f)] private float attackRangeScale = 1f;
    [SerializeField, Min(0f)] private float lifeSeconds = 0f;
    [SerializeField, Min(0f)] private float maxPlacementDistance = 250f;
    [SerializeField, Min(0f)] private float minPlacementDistance = 60f;
    [SerializeField, Min(0f)] private float placementOffsetX = 5f;
    [SerializeField, Min(0f)] private float placementOffsetY = 12f;
    [SerializeField, Min(0f)] private float knockbackSmall = 8f;
    [SerializeField, Min(0f), Tooltip("Weapon05 の 2 グループ目以降を出すまでの間隔（秒）。その時点の進行方向で配置する。")]
    private float extraGroupSpawnDelaySeconds = 0.12f;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private int runtimeAttackCountBonus = 0;
    [SerializeField] private float runtimeSpeedMultiplier = 1f;
    [SerializeField] private float runtimeAttackRangeScaleMultiplier = 1f;
    [SerializeField] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;
    [SerializeField, Min(0)] private int runtimeDistinctEnemyPierceBonus = 0;

    public RectTransform Wo5ImageRect => wo5ImageRect;
    public Image Wo5Image => wo5Image;
    public RectTransform Wo5HitRect => wo5HitRect;
    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float ThrowDistancePixels => throwDistancePixels;
    public float SpeedPixelsPerSecond => speedPixelsPerSecond * Mathf.Max(0.01f, runtimeSpeedMultiplier);
    public float AttackRangeScale => attackRangeScale * Mathf.Max(0.01f, runtimeAttackRangeScaleMultiplier);
    public float LifeSeconds => lifeSeconds;
    public float MaxPlacementDistance => maxPlacementDistance;
    public float MinPlacementDistance => minPlacementDistance;
    public float PlacementOffsetX => placementOffsetX;
    public float PlacementOffsetY => placementOffsetY;
    public float ExtraGroupSpawnDelaySeconds => extraGroupSpawnDelaySeconds;
    public float KnockbackSmall => knockbackSmall * Mathf.Max(0.01f, runtimeKnockbackMultiplier);
    public int DistinctEnemyPierceBudget => Mathf.Max(1, 1 + runtimeDistinctEnemyPierceBonus);

    public int RollAttackDamage()
    {
        int b = AttackPower;
        if (runtimeCriticalRatePercent <= 0f)
        {
            return b;
        }

        if (UnityEngine.Random.value < Mathf.Clamp01(runtimeCriticalRatePercent / 100f))
        {
            return Mathf.Max(1, Mathf.RoundToInt(b * Mathf.Max(1f, runtimeCriticalDamageMultiplier)));
        }

        return b;
    }

    public void ApplyAttackPowerFlat(int amount)
    {
        runtimeAttackPowerFlatBonus += amount;
    }

    public void ApplyCooldownMultiplier(float multiplier)
    {
        runtimeCooldownMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        runtimeSpeedMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyAttackRangeScaleMultiplier(float multiplier)
    {
        runtimeAttackRangeScaleMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyKnockbackMultiplier(float multiplier)
    {
        runtimeKnockbackMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyCriticalRateLevelRule(int nextLevel)
    {
        if (nextLevel <= 1)
        {
            runtimeCriticalRatePercent = 4f;
        }
        else if (nextLevel == 2)
        {
            runtimeCriticalRatePercent = 8f;
        }
        else if (nextLevel == 3)
        {
            runtimeCriticalRatePercent = 16f;
        }
        else
        {
            runtimeCriticalRatePercent = Mathf.Min(100f, runtimeCriticalRatePercent * 1.25f);
        }
    }

    public void ApplyCriticalDamageMultiplierPlusOne()
    {
        runtimeCriticalDamageMultiplier += 1f;
    }

    public void ApplyAttackCountPlusOne()
    {
        runtimeAttackCountBonus++;
    }

    public void ApplyDistinctEnemyPierceBonus(int amount)
    {
        runtimeDistinctEnemyPierceBonus = Mathf.Max(0, runtimeDistinctEnemyPierceBonus + amount);
    }

    private void Reset()
    {
        if (wo5ImageRect == null)
        {
            wo5ImageRect = GetComponentInChildren<RectTransform>();
        }

        if (wo5Image == null)
        {
            wo5Image = GetComponentInChildren<Image>();
        }

        if (wo5HitRect == null)
        {
            wo5HitRect = transform.Find("Wo05hit") as RectTransform;
        }
    }
}
