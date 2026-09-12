using UnityEngine;

namespace Game02
{
    /// <summary>
    /// ParsonObject / ParsonL01 専用の定期演出。
    /// 周期 + [0, jitter] 秒ごとに、底面中心を支点として左右へ往復回数ぶん揺らし、最後に元の姿勢へ戻す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParsonL01Controller : MonoBehaviour
    {
        [Tooltip("揺れの支点となる RectTransform（通常は ParsonL01Deform 直下の ParsonL01Sway）。")]
        [SerializeField] private RectTransform parsonL01SwayRootRect;

        [Header("定期演出")]
        [SerializeField] private float swayPeriodSeconds = 4f;

        [SerializeField]
        [Tooltip("周期に加算するランダム秒（0〜この値、マイナスは付かない）。")]
        private float periodRandomJitterSeconds = 0.5f;

        [SerializeField]
        [Tooltip("左右へ振る角度の最大値（度）。")]
        private float swayAngleDegrees = 12f;

        [SerializeField]
        [Tooltip("1回の演出時間（秒）。この中で往復を実行して元姿勢へ戻る。")]
        private float swayDurationSeconds = 1.2f;

        [SerializeField, Min(1)]
        [Tooltip("左右の往復回数。既定は 2。")]
        private int swayRoundTrips = 2;

        [SerializeField] private bool debugLogSway;

        private enum SwayPhase
        {
            IdleCountdown,
            Performing,
        }

        private SwayPhase swayPhase;
        private float swayCountdownRemaining;
        private float swayElapsedSeconds;
        private Vector3 swayBaseLocalEuler;

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
                rt.localEulerAngles = swayBaseLocalEuler;
            }
        }

        private void LateUpdate()
        {
            TickSway(ResolveSwayDeltaTime());
        }

        private void ResolveRefsIfMissing()
        {
            if (parsonL01SwayRootRect != null)
            {
                return;
            }

            Transform deform = transform.Find("ParsonL01Deform");
            if (deform != null)
            {
                Transform sway = deform.Find("ParsonL01Sway");
                if (sway != null)
                {
                    parsonL01SwayRootRect = sway as RectTransform;
                    return;
                }
            }

            parsonL01SwayRootRect = transform as RectTransform;
        }

        private RectTransform ResolveSwayRect()
        {
            return parsonL01SwayRootRect != null ? parsonL01SwayRootRect : transform as RectTransform;
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
                swayBaseLocalEuler = rt.localEulerAngles;
                rt.localEulerAngles = swayBaseLocalEuler;
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
                    rt.localEulerAngles = swayBaseLocalEuler;
                    swayCountdownRemaining -= dt;
                    if (swayCountdownRemaining <= 0f)
                    {
                        swayElapsedSeconds = 0f;
                        swayBaseLocalEuler = rt.localEulerAngles;
                        swayPhase = SwayPhase.Performing;
                        LogSway($"ParsonL01 sway start t={Time.time:F3}s duration={swayDurationSeconds:F2}s");
                    }

                    break;

                case SwayPhase.Performing:
                    {
                        float dur = Mathf.Max(0.05f, swayDurationSeconds);
                        swayElapsedSeconds += dt;
                        float t = Mathf.Clamp01(swayElapsedSeconds / dur);
                        float cycles = Mathf.Max(1, swayRoundTrips);
                        float angle = Mathf.Sin(t * Mathf.PI * 2f * cycles) * Mathf.Max(0f, swayAngleDegrees);
                        rt.localEulerAngles = swayBaseLocalEuler + new Vector3(0f, 0f, angle);

                        if (t >= 1f)
                        {
                            rt.localEulerAngles = swayBaseLocalEuler;
                            swayPhase = SwayPhase.IdleCountdown;
                            ScheduleNextCountdown();
                            LogSway($"ParsonL01 sway done nextWaitSec={swayCountdownRemaining:F2}");
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

            Debug.Log($"[ParsonL01Controller][Sway] {message}", this);
        }
    }
}
