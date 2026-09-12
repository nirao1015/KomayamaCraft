using UnityEngine;
using UnityEngine.UI;

public class Weapon03Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform w03ImageRect;
    [SerializeField] private Image w03Image;
    [SerializeField] private RectTransform w03HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 4;
    [SerializeField, Min(0f)] private float spawnDistancePixels = 60f;
    [SerializeField, Min(0f)] private float throwDistancePixels = 140f;
    [SerializeField, Min(0.01f)] private float reachTimeSeconds = 0.6f;
    [SerializeField, Min(0.01f)] private float attackRangeScale = 1f;
    [SerializeField, Min(0.01f)] private float attackSpeedSecondsPerTurn = 1f;
    [SerializeField, Min(0.01f)] private float durationSeconds = 3f;
    [SerializeField] private bool reverseRotation;
    [SerializeField, Min(0f)] private float knockbackSmall = 8f;
    [SerializeField, Min(0.02f), Tooltip("黒球：knockbackSmall をこの間隔で分割適用（秒）。毎フレーム全適用しない。")]
    private float orbitKnockbackPulseIntervalSeconds = 0.2f;
    [SerializeField, Min(1), Tooltip("黒球：1回あたり knockbackSmall / この値 の距離で押す。")]
    private int orbitKnockbackSubdivisions = 5;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private int runtimeAttackCountBonus = 0;
    [SerializeField] private float runtimeAttackRangeScaleMultiplier = 1f;
    [SerializeField] private float runtimeOrbitTurnSecondsMultiplier = 1f;
    [SerializeField] private float runtimeDurationMultiplier = 1f;
    [SerializeField] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;

    public RectTransform W03ImageRect => w03ImageRect;
    public Image W03Image => w03Image;
    public RectTransform W03HitRect => w03HitRect;
    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float SpawnDistancePixels => spawnDistancePixels;
    public float ThrowDistancePixels => throwDistancePixels;
    public float ReachTimeSeconds => reachTimeSeconds;
    public float AttackRangeScale => attackRangeScale * Mathf.Max(0.01f, runtimeAttackRangeScaleMultiplier);
    public float AttackSpeedSecondsPerTurn => attackSpeedSecondsPerTurn * Mathf.Max(0.01f, runtimeOrbitTurnSecondsMultiplier);
    public float DurationSeconds => durationSeconds * Mathf.Max(0.01f, runtimeDurationMultiplier);
    public bool ReverseRotation => reverseRotation;
    public float KnockbackSmall => knockbackSmall * Mathf.Max(0.01f, runtimeKnockbackMultiplier);
    public float OrbitKnockbackPulseIntervalSeconds => Mathf.Max(0.02f, orbitKnockbackPulseIntervalSeconds);
    public int OrbitKnockbackSubdivisions => Mathf.Max(1, orbitKnockbackSubdivisions);

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

    public void ApplyAttackRangeScaleMultiplier(float multiplier)
    {
        runtimeAttackRangeScaleMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    /// <summary>値を下げるほど回転が速い（秒／周）。</summary>
    public void ApplyOrbitTurnSecondsMultiplier(float multiplier)
    {
        runtimeOrbitTurnSecondsMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyDurationMultiplier(float multiplier)
    {
        runtimeDurationMultiplier *= Mathf.Max(0.01f, multiplier);
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

    private void Reset()
    {
        if (w03ImageRect == null)
        {
            w03ImageRect = transform.Find("W03Image") as RectTransform;
        }

        if (w03Image == null && w03ImageRect != null)
        {
            w03Image = w03ImageRect.GetComponent<Image>();
        }

        if (w03HitRect == null)
        {
            w03HitRect = transform.Find("W03hit") as RectTransform;
        }
    }
}
