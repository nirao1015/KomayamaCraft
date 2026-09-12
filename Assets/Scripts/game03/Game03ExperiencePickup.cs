using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class Game03ExperiencePickup : MonoBehaviour
{
    public enum AbsorbPhase
    {
        None = 0,
        FloatUp = 1,
        Homing = 2
    }

    [SerializeField, Tooltip("ティア色・スプライトを当てる Image。未設定時は同一 GameObject の Image。")]
    private Image targetImage;

    private Game03ExperienceFieldController owner;
    private RectTransform rectTransform;
    private float pickupDelayRemaining;
    private int experienceValue;
    private Game03ExpTier tier;

    private AbsorbPhase absorbPhase;
    private float floatPhaseElapsed;
    private Vector2 floatPhaseStartAnchored;
    private float homingAlongSpeed;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (rectTransform = GetComponent<RectTransform>());
    public Game03ExpTier Tier => tier;
    public int ExperienceValue => experienceValue;
    public AbsorbPhase CurrentAbsorbPhase => absorbPhase;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    public void Initialize(Game03ExperienceFieldController controller, Vector2 anchoredPosition, int value, Game03ExpTier expTier, float pickupDelaySeconds)
    {
        owner = controller;
        experienceValue = Mathf.Max(1, value);
        tier = expTier;
        pickupDelayRemaining = Mathf.Max(0f, pickupDelaySeconds);
        absorbPhase = AbsorbPhase.None;
        floatPhaseElapsed = 0f;
        homingAlongSpeed = 0f;
        RectTransform.anchoredPosition = anchoredPosition;
        ApplyVisuals();
    }

    /// <summary>
    /// フィールド上のマージ時のみ呼ばれる。経験値を合算し、1回でもまとめたら黄色ティアにする。
    /// 吸い込みフェーズは <paramref name="absorbedPickupPhase"/> とより進んだ方を維持する（爆弾直後の Float が None に戻る不具合対策）。
    /// </summary>
    public void AddExperienceFromMerge(int absorbedAmount, AbsorbPhase absorbedPickupPhase)
    {
        experienceValue += Mathf.Max(1, absorbedAmount);
        tier = Game03ExpTier.Yellow;
        absorbPhase = (AbsorbPhase)Mathf.Max((int)absorbPhase, (int)absorbedPickupPhase);
        if (absorbPhase == AbsorbPhase.FloatUp)
        {
            floatPhaseStartAnchored = RectTransform.anchoredPosition;
            floatPhaseElapsed = 0f;
        }

        if (absorbPhase == AbsorbPhase.Homing)
        {
            homingAlongSpeed = 0f;
        }

        if (absorbPhase == AbsorbPhase.None)
        {
            floatPhaseElapsed = 0f;
            homingAlongSpeed = 0f;
        }

        ApplyVisuals();
    }

    /// <summary>
    /// 爆弾アイテム: ユニット・取得物との距離に関わず、待ち時間を飛ばして浮き→ホーミングを開始する（<see cref="AbsorbPhase.None"/> のときのみ遷移）。
    /// </summary>
    public void ForceBeginAbsorbSequenceFromBombItem()
    {
        pickupDelayRemaining = 0f;
        if (absorbPhase != AbsorbPhase.None)
        {
            return;
        }

        absorbPhase = AbsorbPhase.FloatUp;
        floatPhaseElapsed = 0f;
        floatPhaseStartAnchored = RectTransform.anchoredPosition;
        homingAlongSpeed = 0f;
    }

    public void Tick(float deltaTime, Vector2 playerAnchoredPosition)
    {
        if (owner == null)
        {
            return;
        }

        if (pickupDelayRemaining > 0f)
        {
            pickupDelayRemaining -= deltaTime;
            return;
        }

        float acquireRadius = Mathf.Max(1f, owner.AbsorbEngageRadiusPixels);
        float acquireSqr = acquireRadius * acquireRadius;
        float collectTol = owner.ExperienceCollectCenterTolerancePixels;

        Vector2 pos = RectTransform.anchoredPosition;
        Vector2 delta = playerAnchoredPosition - pos;
        float sqr = delta.sqrMagnitude;

        if (absorbPhase == AbsorbPhase.FloatUp)
        {
            floatPhaseElapsed += deltaTime;
            float duration = Mathf.Max(0.0001f, owner.AbsorbFloatDurationSeconds);
            float u = Mathf.Clamp01(floatPhaseElapsed / duration);
            float ease = u * u * (3f - 2f * u);
            RectTransform.anchoredPosition = floatPhaseStartAnchored + new Vector2(0f, owner.AbsorbFloatUpPixels * ease);
            if (u >= 1f)
            {
                homingAlongSpeed = 0f;
                absorbPhase = AbsorbPhase.Homing;
            }

            return;
        }

        if (absorbPhase == AbsorbPhase.Homing)
        {
            Vector2 pickupMinusPlayer = RectTransform.anchoredPosition - playerAnchoredPosition;
            if (Game03ExperienceFieldController.IsExperienceCollectAtCenterTolerance(pickupMinusPlayer, collectTol))
            {
                owner.CollectPickup(this);
                return;
            }

            delta = playerAnchoredPosition - RectTransform.anchoredPosition;
            sqr = delta.sqrMagnitude;
            if (sqr > 0.0001f)
            {
                float dist = Mathf.Sqrt(sqr);
                Vector2 dir = delta / Mathf.Max(0.0001f, dist);
                float maxSp = Mathf.Max(1f, owner.AbsorbHomingSpeedPixelsPerSecond);
                float cheb = Mathf.Max(Mathf.Abs(pickupMinusPlayer.x), Mathf.Abs(pickupMinusPlayer.y));
                float step = ComputeHomingStepPixels(cheb, collectTol, maxSp, deltaTime);
                if (step >= dist)
                {
                    RectTransform.anchoredPosition = playerAnchoredPosition;
                }
                else
                {
                    RectTransform.anchoredPosition += dir * step;
                }
            }

            return;
        }

        if (sqr <= acquireSqr)
        {
            absorbPhase = AbsorbPhase.FloatUp;
            floatPhaseElapsed = 0f;
            floatPhaseStartAnchored = pos;
            return;
        }
    }

    private void ApplyVisuals()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage == null)
        {
            return;
        }

        if (owner != null)
        {
            owner.GetPresentationForTier(tier, out Color c, out Sprite spriteOverride);
            targetImage.color = c;
            if (spriteOverride != null)
            {
                targetImage.sprite = spriteOverride;
            }
        }
        else
        {
            targetImage.color = GetFallbackTierColor(tier);
        }

        float scale = 1f + 0.06f * Mathf.Log(Mathf.Max(1, experienceValue), 2f);
        RectTransform.localScale = Vector3.one * Mathf.Clamp(scale, 1f, 1.45f);
    }

    private static Color GetFallbackTierColor(Game03ExpTier expTier)
    {
        return expTier switch
        {
            Game03ExpTier.Green => new Color(0.25f, 0.95f, 0.35f, 1f),
            Game03ExpTier.Yellow => new Color(1f, 0.92f, 0.2f, 1f),
            Game03ExpTier.Red => new Color(1f, 0.28f, 0.28f, 1f),
            Game03ExpTier.Purple => new Color(0.78f, 0.42f, 1f, 1f),
            _ => Color.white
        };
    }

    private float ComputeHomingStepPixels(float chebyshevDistanceToUnitCenter, float collectTolerancePixels, float maxSpeedPps, float deltaTime)
    {
        Game03ExpHomingMoveStyle style = owner.AbsorbHomingMoveStyle;
        float step;

        switch (style)
        {
            case Game03ExpHomingMoveStyle.AccelerateAlongPath:
            {
                float accel = Mathf.Max(0.0001f, owner.AbsorbHomingAccelerationPixelsPerSecondSq);
                homingAlongSpeed = Mathf.Min(maxSpeedPps, homingAlongSpeed + accel * deltaTime);
                step = homingAlongSpeed * deltaTime;
                break;
            }

            case Game03ExpHomingMoveStyle.SlowNearCore:
            {
                homingAlongSpeed = 0f;
                float extent = Mathf.Max(8f, owner.AbsorbHomingNearSoftPixels);
                float inner = Mathf.Max(0f, collectTolerancePixels);
                float outer = inner + extent;
                float t = Mathf.InverseLerp(inner, outer, Mathf.Clamp(chebyshevDistanceToUnitCenter, inner, outer));
                float mul = Mathf.Lerp(0.08f, 1f, t * t);
                step = maxSpeedPps * deltaTime * mul;
                break;
            }

            default:
                homingAlongSpeed = 0f;
                step = maxSpeedPps * deltaTime;
                break;
        }

        return step;
    }
}
