using UnityEngine;
using UnityEngine.UI;

public class Weapon07Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform w07ImageRect;
    [SerializeField] private Image w07Image;
    [SerializeField] private RectTransform w07HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 1;
    [SerializeField, Min(0.01f)] private float initialSpeedPixelsPerSecond = 520f;
    [SerializeField, Min(0.01f)] private float gravityPixelsPerSecond2 = 820f;
    [SerializeField] private float launchAngleMinDeg = -40f;
    [SerializeField] private float launchAngleMaxDeg = 40f;
    [SerializeField, Min(0.01f)] private float horizontalBiasPower = 2f;
    [SerializeField, Min(0f), Tooltip("発射時、AttackPower 1 につき付加する上方向初速（px/s）。重力弧でわずかに届く距離が伸びる。")]
    private float attackPowerInitialUpwardPixelsPerSecondPerPower = 6f;
    [SerializeField, Min(0.01f)] private float attackRangeScale = 1f;
    [SerializeField, Min(1), Tooltip("斧が異なる敵にダメージを与えられる回数のベース（同一敵には1回のみ）。maxPierceCount・UG と合算が貫通上限。")]
    private int maxHitCount = 3;
    [SerializeField, Min(0), Tooltip("追加貫通（異なる敵へのヒット許容回数）。MaxHitCount と加算。")]
    private int maxPierceCount = 0;
    [SerializeField, Min(0f)] private float lifeSeconds = 0f;
    [SerializeField, Min(0f)] private float knockbackLarge = 24f;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private int runtimeAttackCountBonus = 0;
    [SerializeField] private float runtimeInitialSpeedMultiplier = 1f;
    [SerializeField] private float runtimeAttackRangeScaleMultiplier = 1f;
    [SerializeField] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0)] private int runtimeMaxPierceBonus = 0;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;

    public RectTransform W07ImageRect => w07ImageRect;
    public Image W07Image => w07Image;
    public RectTransform W07HitRect => w07HitRect;
    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float InitialSpeedPixelsPerSecond => initialSpeedPixelsPerSecond * Mathf.Max(0.01f, runtimeInitialSpeedMultiplier);
    public float GravityPixelsPerSecond2 => gravityPixelsPerSecond2;
    public float LaunchAngleMinDeg => launchAngleMinDeg;
    public float LaunchAngleMaxDeg => launchAngleMaxDeg;
    public float HorizontalBiasPower => horizontalBiasPower;
    /// <summary>AttackPower に応じた発射直後の上方向キック（px/s × AttackPower）。</summary>
    public float AttackPowerInitialUpwardPixelsPerSecondPerPower => attackPowerInitialUpwardPixelsPerSecondPerPower;
    public float AttackRangeScale => attackRangeScale * Mathf.Max(0.01f, runtimeAttackRangeScaleMultiplier);
    public int MaxHitCount => maxHitCount;
    public int MaxPierceCount => Mathf.Max(0, maxPierceCount + runtimeMaxPierceBonus);
    /// <summary>異なる敵へヒットさせられる合計回数（これ以上ダメージを与えると斧が消える）。</summary>
    public int AxeDistinctEnemyPierceBudget => Mathf.Max(1, MaxHitCount + MaxPierceCount);
    public float LifeSeconds => lifeSeconds;
    public float KnockbackLarge => knockbackLarge * Mathf.Max(0.01f, runtimeKnockbackMultiplier);

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

    public void ApplyInitialSpeedMultiplier(float multiplier)
    {
        runtimeInitialSpeedMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyAttackRangeScaleMultiplier(float multiplier)
    {
        runtimeAttackRangeScaleMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyKnockbackMultiplier(float multiplier)
    {
        runtimeKnockbackMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyMaxPierceBonus(int amount)
    {
        runtimeMaxPierceBonus = Mathf.Max(0, runtimeMaxPierceBonus + amount);
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
        if (w07ImageRect == null)
        {
            w07ImageRect = transform.Find("W07Image") as RectTransform;
        }

        if (w07Image == null && w07ImageRect != null)
        {
            w07Image = w07ImageRect.GetComponent<Image>();
        }

        if (w07HitRect == null)
        {
            w07HitRect = transform.Find("W07hit") as RectTransform;
        }
    }
}
