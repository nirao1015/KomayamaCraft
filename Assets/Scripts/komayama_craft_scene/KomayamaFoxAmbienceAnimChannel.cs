using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 狐のうたたね（待機後 doze_enter）。追従・起床は <see cref="KCMouseFoxFollower"/>、開始判定はこちら。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaFoxAmbienceAnimChannel : KomayamaMapAmbienceAnimChannel
    {
        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private KCMouseFoxFollower foxFollower;

        [Header("うたたね待機（無操作・unscaled）")]
        [SerializeField, Min(0f)] private float dozeIdleBaseSeconds = 12f;
        [SerializeField, Min(0f)] private float dozeIdleRandomSeconds = 8f;

        [Header("クリップ")]
        [SerializeField, Range(1, 5)] private int maxClipPriority = 2;

        private float dozeThresholdSeconds;
        private bool active;

        public override bool WantsStart =>
            !active &&
            foxFollower != null &&
            foxFollower.CanAcceptDozeAmbience &&
            foxFollower.IdleSecondsUnscaled >= dozeThresholdSeconds;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            if (foxFollower == null)
            {
                foxFollower = FindFirstObjectByType<KCMouseFoxFollower>();
            }

            ResetDozeThreshold();
        }

        public override void OnAmbienceTick(float tickIntervalSeconds)
        {
            // 待機は狐側の IdleSecondsUnscaled を見る。ここはキック可否の再評価のみ。
        }

        public override bool TryStartAmbience()
        {
            if (!WantsStart)
            {
                return false;
            }

            if (!foxFollower.TryRequestAmbienceClip(KCMouseFoxFollower.ClipDozeEnter, maxClipPriority))
            {
                return false;
            }

            active = true;
            return true;
        }

        private void Update()
        {
            if (!active || foxFollower == null)
            {
                return;
            }

            if (foxFollower.IsDozePlaying)
            {
                return;
            }

            // 起床・クリップ終了・拒否後など、うたたね再生が終わったら予算解放
            Finish();
        }

        private void Finish()
        {
            if (!active)
            {
                return;
            }

            active = false;
            ResetDozeThreshold();
            if (controller != null)
            {
                controller.ReleaseCost(Cost);
            }
        }

        private void ResetDozeThreshold()
        {
            dozeThresholdSeconds =
                dozeIdleBaseSeconds +
                Random.Range(0f, Mathf.Max(0f, dozeIdleRandomSeconds));
        }

        private void OnDisable()
        {
            Finish();
        }
    }
}
