using UnityEngine;
using UnityEngine.UI;

public class Weapon02Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform w02ImageRect;
    [SerializeField] private Image w02Image;
    [SerializeField] private RectTransform w02ImageFieldRect;
    [SerializeField] private Image w02ImageField;
    [SerializeField] private RectTransform w02HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(1)] private int attackCount = 3;
    [SerializeField, Min(0f)] private float throwDistancePixels = 160f;
    [SerializeField, Min(0.01f)] private float travelSeconds = 0.8f;
    [SerializeField, Min(0f)] private float arcMaxHeightPixels = 24f;
    [SerializeField, Min(0.01f)] private float attackRangeScale = 1f;
    [SerializeField, Min(0.01f)] private float durationSeconds = 3f;
    [SerializeField, Min(0)] private int targetSelectionSkipCount = 2;

    [Header("Runtime Upgrade")]
    [SerializeField] private float runtimeCooldownMultiplier = 1f;
    [SerializeField] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField] private float runtimeAttackRangeScaleMultiplier = 1f;
    [SerializeField] private float runtimeDurationMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 0f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;
    [SerializeField, Min(0)] private int runtimeAttackCountBonus = 0;

    public RectTransform W02ImageRect => w02ImageRect;
    public Image W02Image => w02Image;
    public RectTransform W02ImageFieldRect => w02ImageFieldRect;
    public Image W02ImageField => w02ImageField;
    public RectTransform W02HitRect => w02HitRect;

    public float CooldownSeconds => cooldownSeconds * Mathf.Max(0.01f, runtimeCooldownMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public int AttackCount => Mathf.Max(1, attackCount + runtimeAttackCountBonus);
    public float ThrowDistancePixels => throwDistancePixels;
    public float TravelSeconds => travelSeconds;
    public float ArcMaxHeightPixels => arcMaxHeightPixels;
    public float AttackRangeScale => attackRangeScale * Mathf.Max(0.01f, runtimeAttackRangeScaleMultiplier);
    public float DurationSeconds => durationSeconds * Mathf.Max(0.01f, runtimeDurationMultiplier);
    public int TargetSelectionSkipCount => targetSelectionSkipCount;

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

    public void ApplyDurationMultiplier(float multiplier)
    {
        runtimeDurationMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    /// <summary>設置型向け：クリ率は低めのカーブ。</summary>
    public void ApplyCriticalRateLevelDeployed(int nextLevel)
    {
        if (nextLevel <= 1)
        {
            runtimeCriticalRatePercent = 2f;
        }
        else if (nextLevel == 2)
        {
            runtimeCriticalRatePercent = 4f;
        }
        else if (nextLevel == 3)
        {
            runtimeCriticalRatePercent = 7f;
        }
        else
        {
            runtimeCriticalRatePercent = Mathf.Min(100f, runtimeCriticalRatePercent + 3f);
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
        if (w02ImageRect == null)
        {
            w02ImageRect = transform.Find("W02Image") as RectTransform;
        }

        if (w02Image == null && w02ImageRect != null)
        {
            w02Image = w02ImageRect.GetComponent<Image>();
        }

        if (w02ImageFieldRect == null)
        {
            w02ImageFieldRect = transform.Find("W02ImageField") as RectTransform;
        }

        if (w02ImageField == null && w02ImageFieldRect != null)
        {
            w02ImageField = w02ImageFieldRect.GetComponent<Image>();
        }

        if (w02HitRect == null)
        {
            w02HitRect = transform.Find("W02hit") as RectTransform;
        }
    }
}
