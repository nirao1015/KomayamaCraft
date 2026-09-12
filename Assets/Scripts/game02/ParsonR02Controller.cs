using System.Collections;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// ParsonR02 専用。バズ切り替え（Parson01 と同じ <see cref="WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod"/>）と、
    /// 縦方向ゆらゆら。<see cref="PersonEffectManager"/> はスロット直下の子へ毎フレーム anchored を書くため、揺れ対象は
    /// 変形ラッパー内の <c>ParsonR02Sway</c> に限定する（シーンの <see cref="parsonR02SwayRootRect"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParsonR02Controller : MonoBehaviour
    {
        [Tooltip("通常表示（ParsonR02Image）。未設定時は子から名前検索。")]
        [SerializeField] private RectTransform parsonR02ImageRect;

        [Tooltip("バズ表示（ParsonR02Image_2）。未設定時は親直下から名前検索。")]
        [SerializeField] private GameObject parsonR02BuzzOverlayGo;

        [Tooltip("ゆらゆらをかける Rect（通常は ParsonR02Deform 直下の ParsonR02Sway）。PM が触らない階層にすること。")]
        [SerializeField] private RectTransform parsonR02SwayRootRect;

        [Header("定期演出（ルート・縦移動）")]
        [SerializeField] private float swayPeriodSeconds = 4f;

        [SerializeField]
        [Tooltip("待機時間に加算するランダム秒（0〜この値、マイナスは付かない）。")]
        private float periodRandomJitterSeconds = 0.5f;

        [SerializeField]
        [Tooltip("1 回の演出で Y に対してこの上限までランダムに移動（ピクセル・親ローカル）。実際の幅は下限ともに決まる。")]
        private float swayVerticalAmplitudePixels = 22f;

        [SerializeField]
        [Tooltip("1 往復（アウト＋イン）の目標時間（秒）。0 以下なら swaySpeedPixelsPerSecond で移動。")]
        private float swayRoundTripDurationSeconds = 4f;

        [SerializeField]
        [Tooltip("ランダム振幅の下限の足がかり（ピクセル）。上限 amplitude との間で決定し、薄い動きを避ける。")]
        private float swayVerticalMinExcursionPixels = 10f;

        [SerializeField]
        [Tooltip("swayRoundTripDurationSeconds が 0 のときの縦移動速度（ピクセル/秒）。")]
        private float swaySpeedPixelsPerSecond = 40f;

        [SerializeField] private bool debugLogSway;

        private enum SwayPerformPhase
        {
            IdleCountdown,
            PerformingOutbound,
            PerformingInbound,
        }

        private SwayPerformPhase swayPhase;
        private float swayCountdownRemaining;
        private Vector2 swayBurstBaseAnchored;
        private Vector2 swayBurstTargetAnchored;
        private float swayBurstStepSpeedPixelsPerSecond;

        private bool lastBuzzResolved;
        private bool hasSyncedBuzzOnce;

        private Coroutine deferredSaveBuzzCoroutine;

        public static void RefreshAllAfterSaveApplied()
        {
            ParsonR02Controller[] arr =
                FindObjectsByType<ParsonR02Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < arr.Length; i++)
            {
                ParsonR02Controller c = arr[i];
                if (c != null)
                {
                    c.NotifySaveAppliedDeferred();
                }
            }
        }

        public void NotifySaveAppliedDeferred()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (deferredSaveBuzzCoroutine != null)
            {
                StopCoroutine(deferredSaveBuzzCoroutine);
            }

            deferredSaveBuzzCoroutine = StartCoroutine(CoDeferredSaveBuzzSync());
        }

        private IEnumerator CoDeferredSaveBuzzSync()
        {
            yield return null;
            deferredSaveBuzzCoroutine = null;
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            lastBuzzResolved = w;
            hasSyncedBuzzOnce = true;
            ApplyBuzzVisual(w);
        }

        private void Awake()
        {
            ResolveRefsIfMissing();
        }

        private void OnEnable()
        {
            hasSyncedBuzzOnce = false;
            WorkMovieUploadController.BuzzTriggered += OnWorkMovieBuzzChanged;
            WorkMovieUploadController.MovieUploadAccepted += OnMovieUploadAccepted;

            ResolveRefsIfMissing();
            ResetSwayToIdleAndSchedule();
        }

        private void OnDisable()
        {
            WorkMovieUploadController.BuzzTriggered -= OnWorkMovieBuzzChanged;
            WorkMovieUploadController.MovieUploadAccepted -= OnMovieUploadAccepted;

            if (deferredSaveBuzzCoroutine != null)
            {
                StopCoroutine(deferredSaveBuzzCoroutine);
                deferredSaveBuzzCoroutine = null;
            }
        }

        private void LateUpdate()
        {
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            if (!hasSyncedBuzzOnce || w != lastBuzzResolved)
            {
                hasSyncedBuzzOnce = true;
                lastBuzzResolved = w;
                ApplyBuzzVisual(w);
            }

            TickVerticalSway(ResolveSwayDeltaTime());
        }

        private RectTransform ResolveParsonR02SwayRect()
        {
            return parsonR02SwayRootRect != null ? parsonR02SwayRootRect : transform as RectTransform;
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

        private void OnWorkMovieBuzzChanged()
        {
            ApplyBuzzFromWorldImmediate();
        }

        private void OnMovieUploadAccepted(int _)
        {
            ApplyBuzzFromWorldImmediate();
        }

        private void ApplyBuzzFromWorldImmediate()
        {
            bool w = WorkMovieUploadController.SceneHasBuzzMovieInBuzzPeriod();
            if (hasSyncedBuzzOnce && w == lastBuzzResolved)
            {
                return;
            }

            lastBuzzResolved = w;
            hasSyncedBuzzOnce = true;
            ApplyBuzzVisual(w);
        }

        private void ResolveRefsIfMissing()
        {
            if (parsonR02ImageRect == null)
            {
                Transform t = transform.Find("ParsonR02Image");
                if (t != null)
                {
                    parsonR02ImageRect = t as RectTransform;
                }
            }

            if (parsonR02BuzzOverlayGo == null)
            {
                Transform overlay = transform.Find("ParsonR02Image_2");
                if (overlay != null)
                {
                    parsonR02BuzzOverlayGo = overlay.gameObject;
                }
            }

            if (parsonR02SwayRootRect == null)
            {
                Transform deform = transform.Find("ParsonR02Deform");
                if (deform != null)
                {
                    Transform sway = deform.Find("ParsonR02Sway");
                    if (sway != null)
                    {
                        parsonR02SwayRootRect = sway as RectTransform;
                    }
                }
            }
        }

        private void ApplyBuzzVisual(bool buzzMovieActive)
        {
            ResolveRefsIfMissing();

            if (parsonR02ImageRect != null)
            {
                parsonR02ImageRect.gameObject.SetActive(!buzzMovieActive);
            }

            if (parsonR02BuzzOverlayGo != null)
            {
                parsonR02BuzzOverlayGo.SetActive(buzzMovieActive);
            }

            ResetSwayToIdleAndSchedule();
        }

        private void ResetSwayToIdleAndSchedule()
        {
            RectTransform swayRt = ResolveParsonR02SwayRect();
            bool wasPerforming = swayPhase != SwayPerformPhase.IdleCountdown;
            ResetVerticalSwayToIdle(
                swayRt,
                ref swayPhase,
                ref swayCountdownRemaining,
                swayBurstBaseAnchored,
                wasPerforming);
            if (swayRt != null && swayRt.gameObject.activeInHierarchy)
            {
                swayBurstBaseAnchored = swayRt.anchoredPosition;
            }

            ScheduleNextCountdown(ref swayCountdownRemaining);
        }

        private static void ResetVerticalSwayToIdle(
            RectTransform rt,
            ref SwayPerformPhase phase,
            ref float countdownRemaining,
            Vector2 burstBaseAnchored,
            bool wasPerforming)
        {
            if (rt != null && rt.gameObject.activeInHierarchy && wasPerforming)
            {
                rt.anchoredPosition = burstBaseAnchored;
            }

            phase = SwayPerformPhase.IdleCountdown;
            countdownRemaining = 0f;
        }

        /// <summary>周期 + [0, jitter] 秒（プラスのみ）。演出中はカウントしないので重ねない。</summary>
        private void ScheduleNextCountdown(ref float countdownRemaining)
        {
            float j = Mathf.Max(0f, periodRandomJitterSeconds);
            countdownRemaining = Mathf.Max(0.05f, swayPeriodSeconds + Random.Range(0f, j));
        }

        private void LogSway(string message)
        {
            if (!debugLogSway)
            {
                return;
            }

            Debug.Log($"[ParsonR02Controller][Sway] {message}", this);
        }

        private void TickVerticalSway(float dt)
        {
            RectTransform rt = ResolveParsonR02SwayRect();
            if (rt == null || !rt.gameObject.activeInHierarchy || dt <= 0f)
            {
                return;
            }

            float speed = Mathf.Max(0f, swayBurstStepSpeedPixelsPerSecond);
            const float arriveEpsilon = 0.35f;

            switch (swayPhase)
            {
                case SwayPerformPhase.IdleCountdown:
                    rt.anchoredPosition = swayBurstBaseAnchored;
                    swayCountdownRemaining -= dt;
                    if (swayCountdownRemaining <= 0f)
                    {
                        swayBurstBaseAnchored = rt.anchoredPosition;
                        PickVerticalBurstTarget(out swayBurstTargetAnchored);
                        ComputeBurstStepSpeed();
                        swayPhase = SwayPerformPhase.PerformingOutbound;
                        LogSway(
                            $"ParsonR02 timer fired → sway start time={Time.time:F3}s base={swayBurstBaseAnchored} target={swayBurstTargetAnchored} " +
                            $"stepSpeedPx/s={swayBurstStepSpeedPixelsPerSecond:F2} roundTripSec={swayRoundTripDurationSeconds}");
                    }

                    break;

                case SwayPerformPhase.PerformingOutbound:
                    {
                        Vector2 p = rt.anchoredPosition;
                        p.y = Mathf.MoveTowards(p.y, swayBurstTargetAnchored.y, speed * dt);
                        p.x = swayBurstBaseAnchored.x;
                        rt.anchoredPosition = p;
                        if (Mathf.Abs(p.y - swayBurstTargetAnchored.y) <= arriveEpsilon)
                        {
                            p.y = swayBurstTargetAnchored.y;
                            rt.anchoredPosition = p;
                            swayPhase = SwayPerformPhase.PerformingInbound;
                        }
                    }

                    break;

                case SwayPerformPhase.PerformingInbound:
                    {
                        Vector2 p = rt.anchoredPosition;
                        p.y = Mathf.MoveTowards(p.y, swayBurstBaseAnchored.y, speed * dt);
                        p.x = swayBurstBaseAnchored.x;
                        rt.anchoredPosition = p;
                        if (Mathf.Abs(p.y - swayBurstBaseAnchored.y) <= arriveEpsilon)
                        {
                            rt.anchoredPosition = swayBurstBaseAnchored;
                            swayPhase = SwayPerformPhase.IdleCountdown;
                            ScheduleNextCountdown(ref swayCountdownRemaining);
                            LogSway($"ParsonR02 sway done → rest={swayBurstBaseAnchored} nextWaitSec={swayCountdownRemaining:F2}");
                        }
                    }

                    break;
            }
        }

        private void PickVerticalBurstTarget(out Vector2 targetAnchored)
        {
            float amp = Mathf.Max(0f, swayVerticalAmplitudePixels);
            float minEx = Mathf.Max(0f, swayVerticalMinExcursionPixels);
            if (amp <= 0f)
            {
                targetAnchored = swayBurstBaseAnchored;
                return;
            }

            float lo = Mathf.Min(amp, Mathf.Max(minEx, amp * 0.5f));
            float hi = Mathf.Max(lo, amp);
            float mag = Random.Range(lo, hi);
            float dy = Random.value < 0.5f ? -mag : mag;
            targetAnchored = swayBurstBaseAnchored + new Vector2(0f, dy);
        }

        private void ComputeBurstStepSpeed()
        {
            float dist = Mathf.Abs(swayBurstTargetAnchored.y - swayBurstBaseAnchored.y);
            float halfTripSec = Mathf.Max(0.05f, swayRoundTripDurationSeconds * 0.5f);
            if (swayRoundTripDurationSeconds > 0.001f && dist > 0.01f)
            {
                swayBurstStepSpeedPixelsPerSecond = dist / halfTripSec;
            }
            else
            {
                swayBurstStepSpeedPixelsPerSecond = Mathf.Max(0f, swaySpeedPixelsPerSecond);
            }
        }
    }
}
