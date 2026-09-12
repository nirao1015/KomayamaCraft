using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game02
{
    /// <summary>
    /// Parson01 専用。バズ切り替え（通常／バズの画像）と、Parson01 ルートの定期ゆらゆら（Z 回転）。
    /// <see cref="PersonEffectManager"/> は子 Rect を書くため、揺れは親ルートで行う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Parson01Controller : MonoBehaviour
    {
        [Tooltip("通常表示の画像オブジェクト（未設定時は子から Parson01Image を検索）。")]
        [SerializeField] private RectTransform parson01ImageRect;

        [Tooltip("バズ表示の画像オブジェクト（未設定時は親直下から Parson01Image_2 を検索）。")]
        [SerializeField] private GameObject parson01BuzzOverlayGo;

        [Tooltip("ゆらゆら演出をかけるルート（通常は Parson01）。未設定ならこのコンポーネントが付いたオブジェクトの RectTransform。")]
        [SerializeField] private RectTransform parson01SwayRootRect;

        [Header("定期演出（ルート Z 回転）")]
        [SerializeField] private float swayPeriodSeconds = 4f;
        [SerializeField] private float periodRandomJitterSeconds = 0.5f;

        [FormerlySerializedAs("swayAmplitudePixels")]
        [SerializeField]
        [Tooltip("頭〜尻の振りとしての最大 Z 回転角（度）。目標角は ±この範囲でランダム。")]
        private float swayAmplitudeDegrees = 8f;

        [FormerlySerializedAs("swaySpeedPixelsPerSecond")]
        [SerializeField]
        [Tooltip("回転が目標角へ向かう角速度（度/秒）。")]
        private float swayRotationSpeedDegreesPerSecond = 80f;

        [Tooltip("オンにすると、タイマー発火・1 演出完了時にログを出す。")]
        [SerializeField] private bool debugLogSway;

        private enum SwayPerformPhase
        {
            IdleCountdown,
            PerformingOutbound,
            PerformingInbound,
        }

        private SwayPerformPhase swayPhase;
        private float swayCountdownRemaining;
        private float swayBurstBaseEulerZ;
        private float swayBurstTargetEulerZ;

        private bool lastBuzzResolved;
        private bool hasSyncedBuzzOnce;

        private Coroutine deferredSaveBuzzCoroutine;

        /// <summary>セーブ適用直後（スロット状態が確定した次フレーム）に呼ぶ。</summary>
        public static void RefreshAllAfterSaveApplied()
        {
            Parson01Controller[] arr =
                FindObjectsByType<Parson01Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < arr.Length; i++)
            {
                Parson01Controller c = arr[i];
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

            float dt = ResolveSwayDeltaTime();
            TickParson01RotationSway(dt);
        }

        /// <summary>未設定時はこのオブジェクトの RectTransform。</summary>
        private RectTransform ResolveParson01SwayRect()
        {
            return parson01SwayRootRect != null ? parson01SwayRootRect : transform as RectTransform;
        }

        private float ResolveSwayDeltaTime()
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
            if (parson01ImageRect == null)
            {
                Transform t = transform.Find("Parson01Image");
                if (t != null)
                {
                    parson01ImageRect = t as RectTransform;
                }
            }

            if (parson01BuzzOverlayGo == null && parson01ImageRect != null)
            {
                Transform parent = parson01ImageRect.parent != null ? parson01ImageRect.parent : transform;
                Transform overlay = parent.Find("Parson01Image_2");
                if (overlay != null)
                {
                    parson01BuzzOverlayGo = overlay.gameObject;
                }
            }
        }

        private void ApplyBuzzVisual(bool buzzMovieActive)
        {
            ResolveRefsIfMissing();

            if (parson01ImageRect != null)
            {
                parson01ImageRect.gameObject.SetActive(!buzzMovieActive);
            }

            if (parson01BuzzOverlayGo != null)
            {
                parson01BuzzOverlayGo.SetActive(buzzMovieActive);
            }

            ResetSwayToIdleAndSchedule();
        }

        private void ResetSwayToIdleAndSchedule()
        {
            RectTransform swayRt = ResolveParson01SwayRect();
            ResetRotationSwayToIdle(
                swayRt,
                ref swayPhase,
                ref swayCountdownRemaining,
                swayBurstBaseEulerZ,
                swayPhase != SwayPerformPhase.IdleCountdown);
            ScheduleNextCountdown(ref swayCountdownRemaining);
        }

        private static void ResetRotationSwayToIdle(
            RectTransform rt,
            ref SwayPerformPhase phase,
            ref float countdownRemaining,
            float burstBaseEulerZ,
            bool wasPerforming)
        {
            if (rt != null && rt.gameObject.activeInHierarchy && wasPerforming)
            {
                SetLocalEulerZ(rt, burstBaseEulerZ);
            }

            phase = SwayPerformPhase.IdleCountdown;
            countdownRemaining = 0f;
        }

        private void ScheduleNextCountdown(ref float countdownRemaining)
        {
            float j = Mathf.Max(0f, periodRandomJitterSeconds);
            countdownRemaining = Mathf.Max(0.05f, swayPeriodSeconds + Random.Range(-j, j));
        }

        private void LogSway(string message)
        {
            if (!debugLogSway)
            {
                return;
            }

            Debug.Log($"[Parson01Controller][Sway] {message}", this);
        }

        private void TickParson01RotationSway(float dt)
        {
            RectTransform rt = ResolveParson01SwayRect();
            if (rt == null || !rt.gameObject.activeInHierarchy || dt <= 0f)
            {
                return;
            }

            float speed = Mathf.Max(0f, swayRotationSpeedDegreesPerSecond);
            const float angleEpsilon = 0.08f;

            switch (swayPhase)
            {
                case SwayPerformPhase.IdleCountdown:
                    swayCountdownRemaining -= dt;
                    if (swayCountdownRemaining <= 0f)
                    {
                        swayBurstBaseEulerZ = rt.localEulerAngles.z;
                        float amp = Mathf.Max(0f, swayAmplitudeDegrees);
                        swayBurstTargetEulerZ = swayBurstBaseEulerZ + Random.Range(-amp, amp);
                        swayPhase = SwayPerformPhase.PerformingOutbound;
                        LogSway(
                            $"Parson01 timer fired → sway start time={Time.time:F3}s baseZ={swayBurstBaseEulerZ:F2}° targetZ={swayBurstTargetEulerZ:F2}° " +
                            $"speedDeg/s={swayRotationSpeedDegreesPerSecond}");
                    }

                    break;

                case SwayPerformPhase.PerformingOutbound:
                    {
                        float z = Mathf.MoveTowardsAngle(rt.localEulerAngles.z, swayBurstTargetEulerZ, speed * dt);
                        SetLocalEulerZ(rt, z);
                        if (Mathf.Abs(Mathf.DeltaAngle(z, swayBurstTargetEulerZ)) <= angleEpsilon)
                        {
                            SetLocalEulerZ(rt, swayBurstTargetEulerZ);
                            swayPhase = SwayPerformPhase.PerformingInbound;
                        }
                    }

                    break;

                case SwayPerformPhase.PerformingInbound:
                    {
                        float z = Mathf.MoveTowardsAngle(rt.localEulerAngles.z, swayBurstBaseEulerZ, speed * dt);
                        SetLocalEulerZ(rt, z);
                        if (Mathf.Abs(Mathf.DeltaAngle(z, swayBurstBaseEulerZ)) <= angleEpsilon)
                        {
                            SetLocalEulerZ(rt, swayBurstBaseEulerZ);
                            swayPhase = SwayPerformPhase.IdleCountdown;
                            ScheduleNextCountdown(ref swayCountdownRemaining);
                            LogSway($"Parson01 sway done → rest Z={swayBurstBaseEulerZ:F2}° nextWaitSec={swayCountdownRemaining:F2}");
                        }
                    }

                    break;
            }
        }

        private static void SetLocalEulerZ(RectTransform rt, float zDeg)
        {
            if (rt == null)
            {
                return;
            }

            Vector3 e = rt.localEulerAngles;
            e.z = zDeg;
            rt.localEulerAngles = e;
        }
    }
}
