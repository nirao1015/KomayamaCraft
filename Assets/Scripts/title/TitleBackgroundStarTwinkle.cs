using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル背景上の星を、固定周期・固定位相でごくゆっくり明滅させる。
/// 再生のたびに同じタイミング（シーン経過の unscaledTime のみ使用）。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleBackgroundStarTwinkle : MonoBehaviour
{
    [System.Serializable]
    public sealed class StarSlot
    {
        public Graphic graphic;
        [Min(0.1f)] public float periodSeconds = 7f;
        public float phaseDegrees;
        [Range(0f, 1f)] public float minAlpha = 0.22f;
        [Range(0f, 1f)] public float maxAlpha = 0.95f;
    }

    [SerializeField] private bool enableTwinkle = true;
    [SerializeField] private StarSlot[] stars = new StarSlot[3];

    private float[] baseAlpha;
    private bool cached;

    private void OnEnable()
    {
        CacheBaseAlpha();
        ApplyTwinkle(0f);
    }

    private void OnDisable()
    {
        RestoreBaseAlpha();
    }

    private void Update()
    {
        if (!enableTwinkle)
        {
            return;
        }

        ApplyTwinkle(Time.unscaledTime);
    }

    private void CacheBaseAlpha()
    {
        if (stars == null)
        {
            baseAlpha = System.Array.Empty<float>();
            cached = true;
            return;
        }

        baseAlpha = new float[stars.Length];
        for (int i = 0; i < stars.Length; i++)
        {
            StarSlot slot = stars[i];
            if (slot != null && slot.graphic != null)
            {
                baseAlpha[i] = slot.graphic.color.a;
            }
            else
            {
                baseAlpha[i] = 1f;
            }
        }

        cached = true;
    }

    private void RestoreBaseAlpha()
    {
        if (!cached || stars == null || baseAlpha == null)
        {
            return;
        }

        int count = Mathf.Min(stars.Length, baseAlpha.Length);
        for (int i = 0; i < count; i++)
        {
            StarSlot slot = stars[i];
            if (slot == null || slot.graphic == null)
            {
                continue;
            }

            Color c = slot.graphic.color;
            c.a = baseAlpha[i];
            slot.graphic.color = c;
        }
    }

    private void ApplyTwinkle(float timeSeconds)
    {
        if (stars == null)
        {
            return;
        }

        if (!cached)
        {
            CacheBaseAlpha();
        }

        for (int i = 0; i < stars.Length; i++)
        {
            StarSlot slot = stars[i];
            if (slot == null || slot.graphic == null)
            {
                continue;
            }

            float period = Mathf.Max(0.1f, slot.periodSeconds);
            float phase = slot.phaseDegrees * Mathf.Deg2Rad;
            // 0..1 のサイン波。開始時刻は常にシーンの unscaledTime=0 基準で同一。
            float wave = 0.5f + 0.5f * Mathf.Sin((Mathf.PI * 2f * timeSeconds / period) + phase);
            float minA = Mathf.Clamp01(slot.minAlpha);
            float maxA = Mathf.Clamp01(slot.maxAlpha);
            if (maxA < minA)
            {
                float tmp = minA;
                minA = maxA;
                maxA = tmp;
            }

            Color c = slot.graphic.color;
            c.a = Mathf.Lerp(minA, maxA, wave);
            slot.graphic.color = c;
        }
    }
}
