using UnityEngine;
using UnityEngine.UI;

public class Weapon01Stats : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform wo1ImageRect;
    [SerializeField] private Image wo1Image;
[SerializeField] private RectTransform wo01HitRect;

    [Header("Weapon Status")]
    [SerializeField, Min(0.01f)] private float attackIntervalSeconds = 3f;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(0.01f)] private float attackRangeBaseScale = 1f;
    [SerializeField, Min(0f)] private float attackEffectDisplaySeconds = 0.4f;
    [SerializeField, Min(0f)] private float attackActiveSeconds = 0.3f;
    [SerializeField] private bool continuousHit;
    [SerializeField] private Vector2 displayOffsetFromUnit = new Vector2(140f, 0f);
    [SerializeField] private float displayOffsetXFromUnit = 140f;
    [SerializeField] private float attackStartAngleDegrees = 45f;
    [SerializeField] private float attackEndAngleDegrees = -45f;
    [SerializeField] private Vector2 swingPivotBasePixelsFromLeftCenter = Vector2.zero;
    [SerializeField] private Vector2 swingPivotOffsetPixels = Vector2.zero;

    [Header("基礎ノックバック")]
    [SerializeField, Min(0f), Tooltip("ヒット時に敵をプレイヤーから離れる方向へ押す基準強さ（enemyRoot ローカル・ピクセル相当）。ノックバックアップ UG の倍率が乗算されます。")]
    private float kisoKnockbackStrengthPx = 8f;

    [Header("Runtime Upgrade Values (read only in play)")]
    [SerializeField, Min(0f)] private float runtimeAttackIntervalMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeAttackPowerFlatBonus = 0f;
    [SerializeField, Min(0f)] private float runtimeAttackRangeMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeAttackEffectDisplayMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeAttackActiveSecondsMultiplier = 1f;
    [SerializeField, Min(0f)] private float runtimeSwingAngleWidthMultiplier = 1f;
    [SerializeField, Min(1)] private int runtimeShotCount = 1;
    [SerializeField, Min(0f)] private float runtimeKnockbackMultiplier = 1f;
    [SerializeField, Min(0)] private int runtimePierceBonus = 0;
    [SerializeField, Min(0f)] private float runtimeCriticalRatePercent = 2f;
    [SerializeField, Min(1f)] private float runtimeCriticalDamageMultiplier = 2f;

    public RectTransform Wo1ImageRect => wo1ImageRect;
    public Image Wo1Image => wo1Image;
    public RectTransform Wo01HitRect => wo01HitRect;
    public float AttackIntervalSeconds => attackIntervalSeconds * Mathf.Max(0f, runtimeAttackIntervalMultiplier);
    public int AttackPower => Mathf.Max(1, attackPower + Mathf.RoundToInt(runtimeAttackPowerFlatBonus));
    public float AttackRangeBaseScale => attackRangeBaseScale * Mathf.Max(0f, runtimeAttackRangeMultiplier);
    public float AttackEffectDisplaySeconds => attackEffectDisplaySeconds * Mathf.Max(0f, runtimeAttackEffectDisplayMultiplier);
    public float AttackActiveSeconds => attackActiveSeconds * Mathf.Max(0f, runtimeAttackActiveSecondsMultiplier);
    public bool ContinuousHit => continuousHit;
    public Vector2 DisplayOffsetFromUnit => displayOffsetFromUnit;
    public float DisplayOffsetXFromUnit => displayOffsetXFromUnit;
    public float AttackStartAngleDegrees => attackStartAngleDegrees;
    public float AttackEndAngleDegrees => attackEndAngleDegrees;
    public float SwingAngleWidthMultiplier => runtimeSwingAngleWidthMultiplier;
    public int ShotCount => Mathf.Max(1, runtimeShotCount);
    public float KnockbackMultiplier => runtimeKnockbackMultiplier;
    /// <summary>基礎ノックバックの実効強さ（基準値 × ランタイム倍率）。</summary>
    public float KisoKnockbackStrengthPx => kisoKnockbackStrengthPx * Mathf.Max(0f, runtimeKnockbackMultiplier);
    public int PierceBonus => runtimePierceBonus;
    public float CriticalRatePercent => runtimeCriticalRatePercent;
    public float CriticalDamageMultiplier => runtimeCriticalDamageMultiplier;

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

    public void ResetRuntimeUpgrades()
    {
        runtimeAttackIntervalMultiplier = 1f;
        runtimeAttackPowerFlatBonus = 0f;
        runtimeAttackRangeMultiplier = 1f;
        runtimeAttackEffectDisplayMultiplier = 1f;
        runtimeAttackActiveSecondsMultiplier = 1f;
        runtimeSwingAngleWidthMultiplier = 1f;
        runtimeShotCount = 1;
        runtimeKnockbackMultiplier = 1f;
        runtimePierceBonus = 0;
        runtimeCriticalRatePercent = 2f;
        runtimeCriticalDamageMultiplier = 2f;
    }

    public void ApplyAttackPowerFlat(int amount)
    {
        runtimeAttackPowerFlatBonus += amount;
    }

    /// <summary>振り見た目・ヒット窓（AttackActive）を同率で短縮。表示だけ短くすると振り切る前に非表示になる。</summary>
    public void ApplyAttackSpeedDisplaySecondsMultiplier(float multiplier)
    {
        float m = Mathf.Max(0.01f, multiplier);
        runtimeAttackEffectDisplayMultiplier *= m;
        runtimeAttackActiveSecondsMultiplier *= m;
    }

    public void ApplyCooldownIntervalMultiplier(float multiplier)
    {
        runtimeAttackIntervalMultiplier *= Mathf.Max(0f, multiplier);
    }

    public void ApplyRangeMultiplier(float multiplier)
    {
        float m = Mathf.Max(0f, multiplier);
        runtimeAttackRangeMultiplier *= m;
        runtimeSwingAngleWidthMultiplier *= m;
        // 角度幅拡大に合わせてヒット窓も伸ばし、1フレームあたりの角速度を維持する
        runtimeAttackActiveSecondsMultiplier *= m;
    }

    public void ApplyDurationMultiplier(float multiplier)
    {
        runtimeAttackActiveSecondsMultiplier *= Mathf.Max(0f, multiplier);
    }

    public void ApplyKnockbackMultiplier(float multiplier)
    {
        runtimeKnockbackMultiplier *= Mathf.Max(0f, multiplier);
    }

    public void ApplyPierceBonus(int amount)
    {
        runtimePierceBonus = Mathf.Max(0, runtimePierceBonus + amount);
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

    public void ApplyShotCountPlusOne()
    {
        runtimeShotCount = Mathf.Max(1, runtimeShotCount + 1);
    }

    public Vector2 SwingPivotBasePixelsFromLeftCenter => swingPivotBasePixelsFromLeftCenter;
    public Vector2 SwingPivotOffsetPixels => swingPivotOffsetPixels;

    public Vector2 GetSwingPivotNormalized(RectTransform targetRect)
    {
        if (targetRect == null)
        {
            return new Vector2(0f, 0.5f);
        }

        Rect r = targetRect.rect;
        float width = Mathf.Max(0.0001f, r.width);
        float height = Mathf.Max(0.0001f, r.height);

        float px = swingPivotBasePixelsFromLeftCenter.x + swingPivotOffsetPixels.x;
        float py = (height * 0.5f) + swingPivotBasePixelsFromLeftCenter.y + swingPivotOffsetPixels.y;

        return new Vector2(
            Mathf.Clamp01(px / width),
            Mathf.Clamp01(py / height));
    }

    private void Reset()
    {
        if (wo1ImageRect == null)
        {
            wo1ImageRect = GetComponentInChildren<RectTransform>();
        }

        if (wo1Image == null)
        {
            wo1Image = GetComponentInChildren<Image>();
        }

    if (wo01HitRect == null)
    {
        wo01HitRect = transform.Find("Wo01hit") as RectTransform;
    }
    }
}
