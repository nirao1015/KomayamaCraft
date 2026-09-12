using UnityEngine;
using UnityEngine.UI;

public class Weapon06Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform w06ImageRect;
    [SerializeField] private Image w06Image;
    [SerializeField] private RectTransform w06HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 1.2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 1;
    [SerializeField, Min(0.01f)] private float speedPixelsPerSecond = 420f;
    [SerializeField, Min(0f)] private float turnRateDegPerSecond = 720f;
    [SerializeField, Min(0.01f)] private float attackRangeScale = 1f;
    [SerializeField, Min(0f)] private float lifeSeconds = 3f;
    [SerializeField] private bool reacquireTarget = true;
    [SerializeField, Min(0f)] private float knockbackSmall = 8f;
    [SerializeField, Min(1), Tooltip("この発射体がダメージを与えられる敵の数の上限（同一敵には1回のみ）。超えると消滅。貫通アップ UG で runtime が加算されます。")]
    private int magicEnemyPierceBudget = 999;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private int runtimeAttackCountBonus = 0;
    [SerializeField] private float runtimeSpeedMultiplier = 1f;
    [SerializeField] private float runtimeAttackRangeScaleMultiplier = 1f;
    [SerializeField] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0)] private int runtimePierceKillBonus = 0;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;

    public RectTransform W06ImageRect => w06ImageRect;
    public Image W06Image => w06Image;
    public RectTransform W06HitRect => w06HitRect;
    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float SpeedPixelsPerSecond => speedPixelsPerSecond * Mathf.Max(0.01f, runtimeSpeedMultiplier);
    public float TurnRateDegPerSecond => turnRateDegPerSecond;
    public float AttackRangeScale => attackRangeScale * Mathf.Max(0.01f, runtimeAttackRangeScaleMultiplier);
    public float LifeSeconds => lifeSeconds;
    public bool ReacquireTarget => reacquireTarget;
    public float KnockbackSmall => knockbackSmall * Mathf.Max(0.01f, runtimeKnockbackMultiplier);

    /// <summary>異なる敵にヒットさせられる上限（ベース + 貫通 UG）。達すると発射体が消える。</summary>
    public int MagicEnemyPierceBudget => Mathf.Max(1, magicEnemyPierceBudget + runtimePierceKillBonus);

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

    public void ApplyPierceKillBonus(int amount)
    {
        runtimePierceKillBonus = Mathf.Max(0, runtimePierceKillBonus + amount);
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

    private void Reset()
    {
        if (w06ImageRect == null)
        {
            w06ImageRect = transform.Find("W06Image") as RectTransform;
        }

        if (w06Image == null && w06ImageRect != null)
        {
            w06Image = w06ImageRect.GetComponent<Image>();
        }

        if (w06HitRect == null)
        {
            w06HitRect = transform.Find("W06hit") as RectTransform;
        }
    }
}
