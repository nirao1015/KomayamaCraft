using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KomayamaCraft
{
    /// <summary>
    /// 熱嚢草アンビエント。静止 → 2〜6 回鳴動 → 静止を個体ごと独立に回す。
    /// 動きは等方呼吸（A）＋弱い X/Y 逆相歪み（C）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaHeatSacGrassAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum GrassPhase
        {
            Idle,
            Swaying
        }

        [Serializable]
        private sealed class GrassRuntime
        {
            public SpriteRenderer visual;
            public Vector3 baseLocalScale = Vector3.one;
            public float cycleSeconds = 3f;
            public float idleSeconds = 60f;
            public int swayCycles = 3;
            public float phaseElapsed;
            public float swayWaveElapsed;
            public GrassPhase phase = GrassPhase.Idle;
            public bool ready;
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer[] grassVisuals;

        [Header("静止（個体ごと・ゲーム秒）")]
        [SerializeField, Min(0f)] private float idleMin = 40f;
        [SerializeField, Min(0f)] private float idleMax = 80f;

        [Header("鳴動（1 回＝正弦 1 周期）")]
        [SerializeField, Min(1)] private int swayCyclesMin = 2;
        [SerializeField, Min(1)] private int swayCyclesMax = 6;
        [SerializeField, Min(0.5f)] private float cycleSecondsMin = 2.5f;
        [SerializeField, Min(0.5f)] private float cycleSecondsMax = 4.5f;

        [Header("A: 等方呼吸（ベース Scale に対する振幅）")]
        [SerializeField, Range(0f, 0.2f)] private float breathAmplitude = 0.04f;

        [Header("C: 弱い X/Y 逆相歪み")]
        [SerializeField, Range(0f, 0.2f)] private float warpAmplitude = 0.0175f;

        private bool running;
        private GrassRuntime[] runtimes = Array.Empty<GrassRuntime>();

        public override bool WantsStart =>
            !running && runtimes != null && HasAnyReadyRuntime();

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
            // 常設のため初回ティックを待たず開始（予算が取れなければティックで再挑戦）
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
            if (grassVisuals == null || grassVisuals.Length == 0)
            {
                runtimes = Array.Empty<GrassRuntime>();
                return;
            }

            runtimes = new GrassRuntime[grassVisuals.Length];
            for (int i = 0; i < grassVisuals.Length; i++)
            {
                GrassRuntime rt = new GrassRuntime();
                runtimes[i] = rt;
                CaptureOne(rt, grassVisuals[i]);
            }
        }

        private static void CaptureOne(GrassRuntime rt, SpriteRenderer visual)
        {
            rt.visual = visual;
            if (visual == null)
            {
                rt.ready = false;
                return;
            }

            KCContinentAmbientSprite ambient = visual.GetComponent<KCContinentAmbientSprite>();
            if (ambient != null)
            {
                ambient.SetAutoFitToDisplaySize(false);
                ambient.SetPulseAlpha(false);
            }

            rt.baseLocalScale = visual.transform.localScale;
            rt.ready = true;
        }

        private bool HasAnyReadyRuntime()
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
            for (int i = 0; i < runtimes.Length; i++)
            {
                GrassRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.visual == null)
                {
                    continue;
                }

                if (!rt.visual.gameObject.activeSelf)
                {
                    rt.visual.gameObject.SetActive(true);
                }

                // 起動時は静止の途中から（同時に鳴動しない）
                BeginIdle(rt, Random.Range(0f, 1f));
            }

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

            float dt = Time.deltaTime;
            for (int i = 0; i < runtimes.Length; i++)
            {
                TickOne(runtimes[i], dt);
            }
        }

        private void TickOne(GrassRuntime rt, float dt)
        {
            if (rt == null || !rt.ready || rt.visual == null)
            {
                return;
            }

            if (rt.phase == GrassPhase.Idle)
            {
                rt.phaseElapsed += dt;
                if (rt.phaseElapsed >= rt.idleSeconds)
                {
                    BeginSway(rt);
                }

                return;
            }

            rt.swayWaveElapsed += dt;
            float cycle = Mathf.Max(0.05f, rt.cycleSeconds);
            float angle = (rt.swayWaveElapsed / cycle) * Mathf.PI * 2f;
            float wave = Mathf.Sin(angle);
            ApplyScale(rt, wave);

            if (rt.swayWaveElapsed >= cycle * rt.swayCycles)
            {
                BeginIdle(rt, 0f);
            }
        }

        private void BeginIdle(GrassRuntime rt, float progress01)
        {
            float iMin = Mathf.Min(idleMin, idleMax);
            float iMax = Mathf.Max(idleMin, idleMax);
            rt.idleSeconds = Random.Range(iMin, iMax);
            rt.phase = GrassPhase.Idle;
            rt.phaseElapsed = Mathf.Clamp01(progress01) * rt.idleSeconds;
            rt.swayWaveElapsed = 0f;
            ResetScale(rt);
        }

        private void BeginSway(GrassRuntime rt)
        {
            int cMin = Mathf.Min(swayCyclesMin, swayCyclesMax);
            int cMax = Mathf.Max(swayCyclesMin, swayCyclesMax);
            float pMin = Mathf.Min(cycleSecondsMin, cycleSecondsMax);
            float pMax = Mathf.Max(cycleSecondsMin, cycleSecondsMax);

            rt.swayCycles = Random.Range(cMin, cMax + 1);
            rt.cycleSeconds = Random.Range(pMin, pMax);
            rt.phase = GrassPhase.Swaying;
            rt.phaseElapsed = 0f;
            rt.swayWaveElapsed = 0f;
            ApplyScale(rt, 0f);
        }

        private void ApplyScale(GrassRuntime rt, float wave)
        {
            float breath = 1f + (breathAmplitude * wave);
            float warpX = 1f + (warpAmplitude * wave);
            float warpY = 1f - (warpAmplitude * wave);

            Vector3 s = rt.baseLocalScale;
            s.x *= breath * warpX;
            s.y *= breath * warpY;
            rt.visual.transform.localScale = s;
        }

        private static void ResetScale(GrassRuntime rt)
        {
            if (rt.visual != null)
            {
                rt.visual.transform.localScale = rt.baseLocalScale;
            }
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
                GrassRuntime rt = runtimes[i];
                if (rt == null || !rt.ready)
                {
                    continue;
                }

                ResetScale(rt);
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
