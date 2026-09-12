using UnityEngine;
using UnityEngine.UI;

public class Weapon04Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform w04ImageRect;
    [SerializeField] private Image w04Image;
    [SerializeField] private RectTransform[] w04ImageVariants = new RectTransform[5];
    [SerializeField] private RectTransform w04HitRect;
    [SerializeField] private RectTransform w04HitViewRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 3;
    [SerializeField, Min(0.01f)] private float chainIntervalSeconds = 0.2f;
    [SerializeField, Min(0f)] private float rangeOffsetX = 120f;
    [SerializeField, Min(1f)] private float rangeWidth = 220f;
    [SerializeField, Min(1f)] private float rangeHeight = 120f;
    [SerializeField, Min(0f)] private float knockbackSmall = 8f;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private int runtimeAttackCountBonus = 0;
    [SerializeField] private float runtimeRangeMultiplier = 1f;
    [SerializeField] private float runtimeChainIntervalMultiplier = 1f;
    [SerializeField] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;

    public RectTransform W04ImageRect => w04ImageRect;
    public Image W04Image => w04Image;
    public RectTransform[] W04ImageVariants => w04ImageVariants;
    public RectTransform W04HitRect => w04HitRect;
    public RectTransform W04HitViewRect => w04HitViewRect;
    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float ChainIntervalSeconds => chainIntervalSeconds * Mathf.Max(0.01f, runtimeChainIntervalMultiplier);
    public float RangeOffsetX => rangeOffsetX;
    public float RangeWidth => rangeWidth * Mathf.Max(0.01f, runtimeRangeMultiplier);
    public float RangeHeight => rangeHeight * Mathf.Max(0.01f, runtimeRangeMultiplier);
    public float KnockbackSmall => knockbackSmall * Mathf.Max(0.01f, runtimeKnockbackMultiplier);

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

    public void ApplyRangeMultiplier(float multiplier)
    {
        runtimeRangeMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyChainIntervalMultiplier(float multiplier)
    {
        runtimeChainIntervalMultiplier *= Mathf.Max(0.01f, multiplier);
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
            runtimeCriticalRatePercent = 14f;
        }
        else
        {
            runtimeCriticalRatePercent = Mathf.Min(100f, runtimeCriticalRatePercent * 1.2f);
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

    private void Reset()
    {
        if (w04ImageRect == null)
        {
            w04ImageRect = transform.Find("W04Image") as RectTransform;
        }

        if (w04Image == null && w04ImageRect != null)
        {
            w04Image = w04ImageRect.GetComponent<Image>();
        }

        if (w04ImageVariants == null || w04ImageVariants.Length == 0)
        {
            w04ImageVariants = new RectTransform[5];
        }

        for (int i = 0; i < 5; i++)
        {
            if (w04ImageVariants[i] == null)
            {
                w04ImageVariants[i] = transform.Find($"W04Image-{i + 1}") as RectTransform;
            }
        }

        if (w04HitViewRect == null)
        {
            w04HitViewRect = transform.Find("W04hitView") as RectTransform;
        }

        if (w04HitRect == null)
        {
            w04HitRect = transform.Find("W04hit") as RectTransform;
        }
    }
}
