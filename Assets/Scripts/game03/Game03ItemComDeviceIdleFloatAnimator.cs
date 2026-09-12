using UnityEngine;

/// <summary>
/// ItemComDevice／Item01〜Item03（カーゴ用ノーマル）など、配下の ItemEffect / Itemimage を配置中ループで別位相の上下揺れさせる。
/// 1秒で +振幅、1秒で戻る、1秒で -振幅、1秒で戻る（4秒周期）。Itemimage は指定秒だけ時間を遅らせる。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03ItemComDeviceIdleFloatAnimator : MonoBehaviour
{
    [Header("揺らす対象（未設定のものは無視）")]
    [SerializeField] private RectTransform itemEffectRect;
    [SerializeField] private RectTransform itemImageRect;

    [Header("タイミング")]
    [SerializeField, Min(0f)] private float amplitudePixels = 8f;
    [SerializeField, Min(0.01f)] private float segmentSeconds = 1f;
    [SerializeField, Min(0f)] private float itemImageLoopDelaySeconds = 0.5f;

    [Header("任意")]
    [SerializeField, Tooltip("設定時、CanRunGameplay が false の間は anchored を更新せず揺れ見た目を止める。")]
    private Game03Manager game03Manager;

    private Vector2 effectBaseAnchored;
    private Vector2 imageBaseAnchored;
    private bool cachedBases;

    private void OnEnable()
    {
        CacheBasesIfNeeded();
    }

    private void OnDisable()
    {
        RestoreBases();
    }

    private void Update()
    {
        if (!cachedBases)
        {
            CacheBasesIfNeeded();
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        float now = Time.unscaledTime;
        float cycle = Mathf.Max(0.01f, segmentSeconds) * 4f;
        ApplyOffset(itemEffectRect, effectBaseAnchored, now, 0f, cycle);
        ApplyOffset(itemImageRect, imageBaseAnchored, now, itemImageLoopDelaySeconds, cycle);
    }

    private void CacheBasesIfNeeded()
    {
        if (itemEffectRect != null)
        {
            effectBaseAnchored = itemEffectRect.anchoredPosition;
        }

        if (itemImageRect != null)
        {
            imageBaseAnchored = itemImageRect.anchoredPosition;
        }

        cachedBases = itemEffectRect != null || itemImageRect != null;
    }

    private void RestoreBases()
    {
        if (itemEffectRect != null)
        {
            itemEffectRect.anchoredPosition = effectBaseAnchored;
        }

        if (itemImageRect != null)
        {
            itemImageRect.anchoredPosition = imageBaseAnchored;
        }
    }

    private void ApplyOffset(RectTransform rect, Vector2 baseAnchored, float timeSeconds, float timeDelaySeconds, float cycleSeconds)
    {
        if (rect == null)
        {
            return;
        }

        float t = Mathf.Repeat(timeSeconds - timeDelaySeconds, cycleSeconds);
        float seg = Mathf.Max(0.01f, cycleSeconds * 0.25f);
        float y = SampleBobY(t, seg);
        rect.anchoredPosition = new Vector2(baseAnchored.x, baseAnchored.y + y);
    }

    /// <summary>4セグメント（各 seg 秒）で +A → 0 → -A → 0 のピークを線形補間。</summary>
    private float SampleBobY(float tInCycle, float segmentDuration)
    {
        float a = Mathf.Max(0f, amplitudePixels);
        if (a <= 0f)
        {
            return 0f;
        }

        if (tInCycle < segmentDuration)
        {
            return Mathf.Lerp(0f, a, tInCycle / segmentDuration);
        }

        if (tInCycle < segmentDuration * 2f)
        {
            return Mathf.Lerp(a, 0f, (tInCycle - segmentDuration) / segmentDuration);
        }

        if (tInCycle < segmentDuration * 3f)
        {
            return Mathf.Lerp(0f, -a, (tInCycle - segmentDuration * 2f) / segmentDuration);
        }

        return Mathf.Lerp(-a, 0f, (tInCycle - segmentDuration * 3f) / segmentDuration);
    }
}
