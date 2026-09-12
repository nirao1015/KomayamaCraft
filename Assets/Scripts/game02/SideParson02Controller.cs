using UnityEngine;

namespace Game02
{
    /// <summary>
    /// SidePanelParsonObject / SideParson02 専用の定期演出。
    /// 周期 + [0, jitter] 秒ごとに、水平 anchored X を往復回数ぶん震わせ、最後に元座標へ戻す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SideParson02Controller : MonoBehaviour
    {
        [Tooltip("揺らす RectTransform（省略時はこのオブジェクトの Rect）。")]
        [SerializeField] private RectTransform sideParson02SwayRootRect;

        [Header("定期演出")]
        [SerializeField] private float swayPeriodSeconds = 4f;

        [SerializeField]
        [Tooltip("周期に加算するランダム秒（0〜この値、マイナスは付かない）。")]
        private float periodRandomJitterSeconds = 0.5f;

        [SerializeField]
        [Tooltip("左右へずらす最大値（ピクセル・親ローカル anchored X）。")]
        private float swayHorizontalAmplitudePixels = 4f;

        [SerializeField]
        [Tooltip("1回の演出時間（秒）。この中で往復し、元の座標へ戻る。")]
        private float swayDurationSeconds = 1f;

        [SerializeField, Min(1)]
        [Tooltip("左右の往復回数。")]
        private int swayRoundTrips = 5;

        [SerializeField] private bool debugLogSway;

        private enum SwayPhase
        {
            IdleCountdown,
            Performing,
        }

        private SwayPhase swayPhase;
        private float swayCountdownRemaining;
        private float swayElapsedSeconds;
        private Vector2 swayBaseAnchoredPosition;

        private void Awake()
        {
            ResolveRefsIfMissing();
        }

        private void OnEnable()
        {
            ResolveRefsIfMissing();
            ResetToIdleAndSchedule();
        }

        private void OnDisable()
        {
            RectTransform rt = ResolveSwayRect();
            if (rt != null)
            {
                rt.anchoredPosition = swayBaseAnchoredPosition;
            }
        }

        private void LateUpdate()
        {
            TickSway(ResolveSwayDeltaTime());
        }

        private void ResolveRefsIfMissing()
        {
            if (sideParson02SwayRootRect == null)
            {
                sideParson02SwayRootRect = transform as RectTransform;
            }
        }

        private RectTransform ResolveSwayRect()
        {
            return sideParson02SwayRootRect != null ? sideParson02SwayRootRect : transform as RectTransform;
        }

        private static float ResolveSwayDeltaTime()
        {
            float gameplayDelta = GameManager.GameplayDelta;
            if (gameplayDelta > 0f)
            {
                return gameplayDelta;
            }

            GameManager gm = GameManager.Instance;
            if (gm != null && (gm.HasFatalError || gm.IsPaused))
            {
                return 0f;
            }

            float speedMul = gm != null ? gm.GameSpeedMultiplier : 1f;
            return Time.deltaTime * speedMul;
        }

        private void ResetToIdleAndSchedule()
        {
            RectTransform rt = ResolveSwayRect();
            if (rt != null)
            {
                swayBaseAnchoredPosition = rt.anchoredPosition;
                rt.anchoredPosition = swayBaseAnchoredPosition;
            }

            swayElapsedSeconds = 0f;
            swayPhase = SwayPhase.IdleCountdown;
            ScheduleNextCountdown();
        }

        private void ScheduleNextCountdown()
        {
            float jitter = Mathf.Max(0f, periodRandomJitterSeconds);
            swayCountdownRemaining = Mathf.Max(0.05f, swayPeriodSeconds + Random.Range(0f, jitter));
        }

        private void TickSway(float dt)
        {
            RectTransform rt = ResolveSwayRect();
            if (rt == null || !rt.gameObject.activeInHierarchy || dt <= 0f)
            {
                return;
            }

            switch (swayPhase)
            {
                case SwayPhase.IdleCountdown:
                    rt.anchoredPosition = swayBaseAnchoredPosition;
                    swayCountdownRemaining -= dt;
                    if (swayCountdownRemaining <= 0f)
                    {
                        swayElapsedSeconds = 0f;
                        swayBaseAnchoredPosition = rt.anchoredPosition;
                        swayPhase = SwayPhase.Performing;
                        LogSway(
                            $"SideParson02 sway start t={Time.time:F3}s ampPx={swayHorizontalAmplitudePixels:F1} dur={swayDurationSeconds:F2}s trips={swayRoundTrips}");
                    }

                    break;

                case SwayPhase.Performing:
                    {
                        float dur = Mathf.Max(0.05f, swayDurationSeconds);
                        swayElapsedSeconds += dt;
                        float t = Mathf.Clamp01(swayElapsedSeconds / dur);
                        float cycles = Mathf.Max(1, swayRoundTrips);
                        float dx = Mathf.Sin(t * Mathf.PI * 2f * cycles) * Mathf.Max(0f, swayHorizontalAmplitudePixels);
                        Vector2 p = swayBaseAnchoredPosition;
                        p.x += dx;
                        rt.anchoredPosition = p;

                        if (t >= 1f)
                        {
                            rt.anchoredPosition = swayBaseAnchoredPosition;
                            swayPhase = SwayPhase.IdleCountdown;
                            ScheduleNextCountdown();
                            LogSway($"SideParson02 sway done nextWaitSec={swayCountdownRemaining:F2}");
                        }
                    }

                    break;
            }
        }

        private void LogSway(string message)
        {
            if (!debugLogSway)
            {
                return;
            }

            Debug.Log($"[SideParson02Controller][Sway] {message}", this);
        }
    }
}
