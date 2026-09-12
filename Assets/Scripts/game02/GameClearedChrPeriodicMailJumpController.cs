using System.Collections;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// <c>GameClearedChrObject</c> 配置中、周期＋正のランダムで子 <c>ClearedMailCleared</c> を軽くジャンプさせる。
    /// 演出中は次の発火を貯めず、終了後に次の間隔を設定する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameClearedChrPeriodicMailJumpController : MonoBehaviour
    {
        [SerializeField] private RectTransform jumpTarget;
        [SerializeField, Tooltip("次の演出までの基準秒（unscaled）。")]
        private float periodSeconds = 5f;
        [SerializeField, Tooltip("基準に加算するランダム秒（0〜この値、unscaled）。負は入れない。")]
        private float randomJitterMaxSeconds = 1.5f;
        [SerializeField] private float jumpHeightPixels = 36f;
        [SerializeField] private float jumpUpSeconds = 0.11f;
        [SerializeField] private float fallSeconds = 0.13f;
        [SerializeField] private float bounceHeightPixels = 10f;
        [SerializeField] private float bounceUpSeconds = 0.07f;
        [SerializeField] private float bounceDownSeconds = 0.09f;

        private float countdownSeconds;
        private bool isPlayingJump;
        private Coroutine jumpRoutine;
        private float restAnchoredY;
        private bool hasCapturedRestY;

        private void Awake()
        {
            if (jumpTarget == null)
            {
                Transform t = transform.Find("ClearedMailCleared");
                if (t != null)
                {
                    jumpTarget = t as RectTransform;
                }
            }
        }

        private void OnEnable()
        {
            ScheduleNextInterval();
        }

        private void OnDisable()
        {
            if (jumpRoutine != null)
            {
                StopCoroutine(jumpRoutine);
                jumpRoutine = null;
            }

            isPlayingJump = false;
            RestoreRestYIfNeeded();
        }

        private void Update()
        {
            if (jumpTarget == null || isPlayingJump)
            {
                return;
            }

            countdownSeconds -= Time.unscaledDeltaTime;
            if (countdownSeconds > 0f)
            {
                return;
            }

            jumpRoutine = StartCoroutine(CoMailJumpSequence());
        }

        private void ScheduleNextInterval()
        {
            float jitter = Mathf.Max(0f, randomJitterMaxSeconds);
            countdownSeconds = Mathf.Max(0.05f, periodSeconds) + Random.Range(0f, jitter);
        }

        private IEnumerator CoMailJumpSequence()
        {
            isPlayingJump = true;
            Vector2 p = jumpTarget.anchoredPosition;
            restAnchoredY = p.y;
            hasCapturedRestY = true;

            float peakY = restAnchoredY + Mathf.Max(1f, jumpHeightPixels);
            float bounceY = restAnchoredY + Mathf.Max(0f, bounceHeightPixels);

            yield return CoTweenAnchoredY(jumpTarget, restAnchoredY, peakY, Mathf.Max(0.02f, jumpUpSeconds), EaseOutQuad);
            yield return CoTweenAnchoredY(jumpTarget, peakY, restAnchoredY, Mathf.Max(0.02f, fallSeconds), EaseInQuad);

            if (bounceHeightPixels > 0.5f && bounceUpSeconds > 0.001f && bounceDownSeconds > 0.001f)
            {
                yield return CoTweenAnchoredY(jumpTarget, restAnchoredY, bounceY, bounceUpSeconds, EaseOutQuad);
                yield return CoTweenAnchoredY(jumpTarget, bounceY, restAnchoredY, bounceDownSeconds, EaseInQuad);
            }

            p = jumpTarget.anchoredPosition;
            p.y = restAnchoredY;
            jumpTarget.anchoredPosition = p;

            isPlayingJump = false;
            jumpRoutine = null;
            hasCapturedRestY = false;
            ScheduleNextInterval();
        }

        private static IEnumerator CoTweenAnchoredY(RectTransform rt, float fromY, float toY, float duration, System.Func<float, float> ease)
        {
            Vector2 p = rt.anchoredPosition;
            float elapsed = 0f;
            float dur = duration;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / Mathf.Max(1e-4f, dur));
                float e = ease(u);
                p.y = Mathf.LerpUnclamped(fromY, toY, e);
                rt.anchoredPosition = p;
                yield return null;
            }

            p.y = toY;
            rt.anchoredPosition = p;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseInQuad(float t)
        {
            return t * t;
        }

        private void RestoreRestYIfNeeded()
        {
            if (jumpTarget == null || !hasCapturedRestY)
            {
                hasCapturedRestY = false;
                return;
            }

            Vector2 p = jumpTarget.anchoredPosition;
            p.y = restAnchoredY;
            jumpTarget.anchoredPosition = p;
            hasCapturedRestY = false;
        }
    }
}
