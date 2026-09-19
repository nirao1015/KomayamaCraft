using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KomayamaCraft
{
    /// <summary>
    /// 胞子雲アンビエント（常設・複数）。各ビジュアルが別周期でうごめき、
    /// α はオーサリング時のベース α に比率をかけて往復する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaSporeCloudAmbience : KomayamaMapAmbienceMapChannel
    {
        [Serializable]
        private sealed class SporeRuntime
        {
            public SpriteRenderer visual;
            public Vector3 homePosition;
            public Vector3 cycleStartPosition;
            public Vector3 cycleTargetPosition;
            public Color baseRgb = Color.white;
            public float baseAlpha = 1f;
            public float periodSeconds = 10f;
            public float periodElapsed;
            public bool ready;
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer[] sporeVisuals;

        [Header("周期（ゲーム秒・個体ごと独立）")]
        [SerializeField, Min(0.5f)] private float periodMin = 8f;
        [SerializeField, Min(0.5f)] private float periodMax = 14f;

        [Header("アルファ往復（ベース α × 比率）")]
        [SerializeField, Min(0f), Tooltip("薄い寄り。例: 0.5 → ベースの半分まで薄く")]
        private float alphaRatioMin = 0.5f;
        [SerializeField, Min(0f), Tooltip("濃い寄り。例: 1.0 → ベースまで（またはそれ以上）")]
        private float alphaRatioMax = 1.2f;

        [Header("周期ごとの座標ずれ（画面幅に対する割合）")]
        [SerializeField, Range(0f, 0.2f)] private float driftScreenWidthMin = 0.01f;
        [SerializeField, Range(0f, 0.2f)] private float driftScreenWidthMax = 0.03f;

        private bool running;
        private SporeRuntime[] runtimes = Array.Empty<SporeRuntime>();

        public override bool WantsStart =>
            !running && runtimes != null && HasAnyReadyRuntime();

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            RebuildRuntimes();
        }

        private void RebuildRuntimes()
        {
            if (sporeVisuals == null || sporeVisuals.Length == 0)
            {
                runtimes = Array.Empty<SporeRuntime>();
                return;
            }

            runtimes = new SporeRuntime[sporeVisuals.Length];
            for (int i = 0; i < sporeVisuals.Length; i++)
            {
                SporeRuntime rt = new SporeRuntime();
                runtimes[i] = rt;
                CaptureOne(rt, sporeVisuals[i]);
            }
        }

        private static void CaptureOne(SporeRuntime rt, SpriteRenderer visual)
        {
            rt.visual = visual;
            if (visual == null)
            {
                rt.ready = false;
                return;
            }

            Color c = visual.color;
            rt.homePosition = visual.transform.position;
            rt.baseRgb = new Color(c.r, c.g, c.b, 1f);
            rt.baseAlpha = Mathf.Clamp01(c.a);
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
                SporeRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.visual == null)
                {
                    continue;
                }

                if (!rt.visual.gameObject.activeSelf)
                {
                    rt.visual.gameObject.SetActive(true);
                }

                // 開始位相をずらして同時に揃わないようにする
                BeginNewCycle(rt, snapPosition: true);
                rt.periodElapsed = Random.Range(0f, rt.periodSeconds);
            }

            return true;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            if (KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < runtimes.Length; i++)
            {
                TickOne(runtimes[i], dt);
            }
        }

        private void TickOne(SporeRuntime rt, float dt)
        {
            if (rt == null || !rt.ready || rt.visual == null)
            {
                return;
            }

            rt.periodElapsed += dt;
            float t = rt.periodSeconds > 0.001f
                ? Mathf.Clamp01(rt.periodElapsed / rt.periodSeconds)
                : 1f;

            float wave = t <= 0.5f ? (t * 2f) : ((1f - t) * 2f);
            float aMin = Mathf.Clamp01(rt.baseAlpha * alphaRatioMin);
            float aMax = Mathf.Clamp01(rt.baseAlpha * alphaRatioMax);
            if (aMax < aMin)
            {
                float swap = aMin;
                aMin = aMax;
                aMax = swap;
            }

            Color color = rt.baseRgb;
            color.a = Mathf.Lerp(aMin, aMax, wave);
            rt.visual.color = color;

            rt.visual.transform.position = Vector3.Lerp(
                rt.cycleStartPosition,
                rt.cycleTargetPosition,
                Smooth01(t));

            if (rt.periodElapsed >= rt.periodSeconds)
            {
                BeginNewCycle(rt, snapPosition: false);
            }
        }

        private void BeginNewCycle(SporeRuntime rt, bool snapPosition)
        {
            rt.periodSeconds = Random.Range(
                Mathf.Min(periodMin, periodMax),
                Mathf.Max(periodMin, periodMax));
            rt.periodElapsed = 0f;

            rt.cycleStartPosition = snapPosition
                ? rt.homePosition
                : rt.visual.transform.position;

            float viewWidth = 20f;
            if (targetCamera != null)
            {
                viewWidth = targetCamera.orthographicSize * 2f * targetCamera.aspect;
            }

            float drift = viewWidth * Random.Range(driftScreenWidthMin, driftScreenWidthMax);
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            dir.Normalize();
            rt.cycleTargetPosition = rt.homePosition + new Vector3(dir.x, dir.y, 0f) * drift;

            if (snapPosition)
            {
                rt.visual.transform.position = rt.cycleStartPosition;
            }
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
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
                SporeRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.visual == null)
                {
                    continue;
                }

                rt.visual.transform.position = rt.homePosition;
                Color c = rt.baseRgb;
                c.a = rt.baseAlpha;
                rt.visual.color = c;
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
