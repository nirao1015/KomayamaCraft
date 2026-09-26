using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KomayamaCraft
{
    /// <summary>
    /// 浮島の浮小岩・小島の上下ゆらぎ。常設・Cost 0。乱数は起動時のみ。
    /// 浮小岩: 同振幅・等速往復・開始位相／周期のみ個体差。
    /// 小島: 終端スロー（EaseInOut）＋上下端停止。振幅／移動時間／停止／位相は個体差。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaFloatBobAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum BobMode
        {
            /// <summary>等速（三角波）。</summary>
            ConstantSpeed,
            /// <summary>終端でゆっくり（EaseInOut）＋端停止可。</summary>
            EaseEnds
        }

        [Serializable]
        private sealed class BobRuntime
        {
            public Transform target;
            public Vector3 homePosition;
            public float periodSeconds = 3f;
            public float amplitude = 0.1f;
            public float peakHoldSeconds;
            public float phase01;
            public BobMode mode;
            public bool ready;
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private Transform[] floatRocks;
        [SerializeField] private Transform[] floatIsles;

        [Header("浮小岩（等速・同振幅）")]
        [SerializeField, Min(0.01f)] private float rockAmplitude = 0.1f;
        [SerializeField, Min(0.5f)] private float rockPeriodMin = 1.8f;
        [SerializeField, Min(0.5f)] private float rockPeriodMax = 3.5f;

        [Header("小島（終端スロー・上下端停止・個体差）")]
        [SerializeField, Min(0.01f)] private float isleAmplitudeMin = 0.17f;
        [SerializeField, Min(0.01f)] private float isleAmplitudeMax = 0.39f;
        [SerializeField, Min(0.5f), Tooltip("上昇＋下降のみの時間（両端停止は含まない）")]
        private float isleMoveSecondsMin = 10.5f;
        [SerializeField, Min(0.5f)] private float isleMoveSecondsMax = 19.5f;
        [SerializeField, Min(0f), Tooltip("上下端それぞれでの停止時間（秒）")]
        private float islePeakHoldMin = 8f;
        [SerializeField, Min(0f)] private float islePeakHoldMax = 12f;

        private bool running;
        private float elapsed;
        private BobRuntime[] runtimes = Array.Empty<BobRuntime>();

        public override bool WantsStart =>
            !running && runtimes != null && HasAnyReady();

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            RebuildRuntimes();
        }

        private void Start()
        {
            TryBeginNow();
        }

        private void TryBeginNow()
        {
            if (!WantsStart || controller == null)
            {
                return;
            }

            if (!controller.TryReserveCost(Cost))
            {
                return;
            }

            if (!TryStartAmbience())
            {
                controller.ReleaseCost(Cost);
            }
        }

        private void RebuildRuntimes()
        {
            int rockCount = floatRocks != null ? floatRocks.Length : 0;
            int isleCount = floatIsles != null ? floatIsles.Length : 0;
            runtimes = new BobRuntime[rockCount + isleCount];
            int index = 0;

            float rMin = Mathf.Min(rockPeriodMin, rockPeriodMax);
            float rMax = Mathf.Max(rockPeriodMin, rockPeriodMax);
            for (int i = 0; i < rockCount; i++)
            {
                BobRuntime rt = new BobRuntime();
                runtimes[index++] = rt;
                Capture(rt, floatRocks[i], BobMode.ConstantSpeed);
                if (!rt.ready)
                {
                    continue;
                }

                rt.amplitude = rockAmplitude;
                rt.periodSeconds = Random.Range(rMin, rMax);
                rt.peakHoldSeconds = 0f;
                rt.phase01 = Random.Range(0f, 1f);
            }

            float aMin = Mathf.Min(isleAmplitudeMin, isleAmplitudeMax);
            float aMax = Mathf.Max(isleAmplitudeMin, isleAmplitudeMax);
            float mMin = Mathf.Min(isleMoveSecondsMin, isleMoveSecondsMax);
            float mMax = Mathf.Max(isleMoveSecondsMin, isleMoveSecondsMax);
            float hMin = Mathf.Min(islePeakHoldMin, islePeakHoldMax);
            float hMax = Mathf.Max(islePeakHoldMin, islePeakHoldMax);
            for (int i = 0; i < isleCount; i++)
            {
                BobRuntime rt = new BobRuntime();
                runtimes[index++] = rt;
                Capture(rt, floatIsles[i], BobMode.EaseEnds);
                if (!rt.ready)
                {
                    continue;
                }

                float moveSeconds = Random.Range(mMin, mMax);
                float holdSeconds = Random.Range(hMin, hMax);
                rt.amplitude = Random.Range(aMin, aMax);
                rt.peakHoldSeconds = holdSeconds;
                // 1周期 = 上昇＋頂点停止＋下降＋底停止
                rt.periodSeconds = moveSeconds + (holdSeconds * 2f);
                rt.phase01 = Random.Range(0f, 1f);
            }
        }

        private static void Capture(BobRuntime rt, Transform target, BobMode mode)
        {
            rt.target = target;
            rt.mode = mode;
            if (target == null)
            {
                rt.ready = false;
                return;
            }

            rt.homePosition = target.position;
            rt.ready = true;
        }

        private bool HasAnyReady()
        {
            for (int i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i] != null && runtimes[i].ready)
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnAmbienceTick(float tickIntervalSeconds)
        {
        }

        public override bool TryStartAmbience()
        {
            if (!WantsStart)
            {
                return false;
            }

            running = true;
            elapsed = 0f;
            return true;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            if (KomayamaGameClock.ResolveIsPaused() || KomayamaCraftLoadGate.HoldGameTime)
            {
                return;
            }

            elapsed += Time.deltaTime;
            for (int i = 0; i < runtimes.Length; i++)
            {
                TickOne(runtimes[i]);
            }
        }

        private void TickOne(BobRuntime rt)
        {
            if (rt == null || !rt.ready || rt.target == null)
            {
                return;
            }

            float period = Mathf.Max(0.05f, rt.periodSeconds);
            float cycle = ((elapsed / period) + rt.phase01) % 1f;
            if (cycle < 0f)
            {
                cycle += 1f;
            }

            float shaped;
            if (rt.mode == BobMode.ConstantSpeed)
            {
                // 三角波（等速）
                shaped = cycle < 0.5f ? (cycle * 2f) : (2f - (cycle * 2f));
            }
            else
            {
                shaped = EvaluateEasePingWithHold(cycle, period, rt.peakHoldSeconds);
            }

            float offset = (shaped * 2f - 1f) * rt.amplitude;
            Vector3 p = rt.homePosition;
            p.y += offset;
            rt.target.position = p;
        }

        /// <summary>
        /// 0→1→0。移動区間は EaseInOut。上下端で peakHoldSeconds 停止。
        /// period = move + 2*hold を前提。
        /// </summary>
        private static float EvaluateEasePingWithHold(float cycle01, float periodSeconds, float peakHoldSeconds)
        {
            float hold = Mathf.Max(0f, peakHoldSeconds);
            float holdFrac = periodSeconds > 0.001f ? Mathf.Clamp01(hold / periodSeconds) : 0f;
            float moveFracTotal = Mathf.Max(0.05f, 1f - (holdFrac * 2f));
            float riseFrac = moveFracTotal * 0.5f;
            float fallFrac = moveFracTotal * 0.5f;

            float t = cycle01;
            if (t < riseFrac)
            {
                float u = riseFrac > 0.0001f ? (t / riseFrac) : 1f;
                return EaseInOutCubic(u);
            }

            t -= riseFrac;
            if (t < holdFrac)
            {
                return 1f; // 頂点
            }

            t -= holdFrac;
            if (t < fallFrac)
            {
                float u = fallFrac > 0.0001f ? (t / fallFrac) : 1f;
                return EaseInOutCubic(1f - u);
            }

            return 0f; // 底
        }

        private static float EaseInOutCubic(float t)
        {
            if (t < 0.5f)
            {
                return 4f * t * t * t;
            }

            float u = (-2f * t) + 2f;
            return 1f - ((u * u * u) * 0.5f);
        }

        private void OnDisable()
        {
            if (!running)
            {
                return;
            }

            running = false;
            for (int i = 0; i < runtimes.Length; i++)
            {
                BobRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.target == null)
                {
                    continue;
                }

                rt.target.position = rt.homePosition;
            }

            if (controller != null)
            {
                controller.ReleaseCost(Cost);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                RebuildRuntimes();
            }
        }
#endif
    }
}
