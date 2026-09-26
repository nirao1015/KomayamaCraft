using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KomayamaCraft
{
    /// <summary>
    /// 沼の気泡アンビエント。待ち（非表示）→成長→一瞬で消えるを個体ごと固定周期で回す。
    /// 成長中だけ予算を消費。起動時は半数を成長途中から始める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaSwampBubbleAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum BubblePhase
        {
            Waiting,
            Growing
        }

        [Serializable]
        private sealed class BubbleRuntime
        {
            public SpriteRenderer visual;
            public Vector3 maxLocalScale = Vector3.one;
            public Color baseRgb = Color.white;
            public float peakAlpha = 0.7f;
            public float waitSeconds = 7f;
            public float growSeconds = 8f;
            public float phaseElapsed;
            public BubblePhase phase = BubblePhase.Waiting;
            public bool costHeld;
            public bool ready;
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer[] bubbleVisuals;

        [Header("待ち（起動時に個体ごと固定・ゲーム秒）")]
        [SerializeField, Min(0f)] private float waitMin = 5f;
        [SerializeField, Min(0f)] private float waitMax = 10f;

        [Header("成長（起動時に個体ごと固定・ゲーム秒）")]
        [SerializeField, Min(0.1f)] private float growMin = 4f;
        [SerializeField, Min(0.1f)] private float growMax = 12f;

        [Header("見た目")]
        [SerializeField, Min(0.01f)] private float startScale = 0.1f;
        [SerializeField, Range(0f, 1f)] private float peakAlphaMin = 0.3f;
        [SerializeField, Range(0f, 1f)] private float peakAlphaMax = 0.4f;

        [Header("起動")]
        [SerializeField, Min(0)] private int midGrowCountAtStart = 2;

        private BubbleRuntime[] runtimes = Array.Empty<BubbleRuntime>();

        public override bool WantsStart => AnyWaitingReady();

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            RebuildRuntimes();
            RollFixedTimings();
            ApplyStartupPhases();
        }

        private void RebuildRuntimes()
        {
            if (bubbleVisuals == null || bubbleVisuals.Length == 0)
            {
                runtimes = Array.Empty<BubbleRuntime>();
                return;
            }

            runtimes = new BubbleRuntime[bubbleVisuals.Length];
            for (int i = 0; i < bubbleVisuals.Length; i++)
            {
                BubbleRuntime rt = new BubbleRuntime();
                runtimes[i] = rt;
                CaptureOne(rt, bubbleVisuals[i]);
            }
        }

        private static void CaptureOne(BubbleRuntime rt, SpriteRenderer visual)
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

            Color c = visual.color;
            rt.maxLocalScale = visual.transform.localScale;
            rt.baseRgb = new Color(c.r, c.g, c.b, 1f);
            rt.ready = true;
        }

        private void RollFixedTimings()
        {
            float wMin = Mathf.Min(waitMin, waitMax);
            float wMax = Mathf.Max(waitMin, waitMax);
            float gMin = Mathf.Min(growMin, growMax);
            float gMax = Mathf.Max(growMin, growMax);
            float aMin = Mathf.Clamp01(Mathf.Min(peakAlphaMin, peakAlphaMax));
            float aMax = Mathf.Clamp01(Mathf.Max(peakAlphaMin, peakAlphaMax));

            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready)
                {
                    continue;
                }

                rt.waitSeconds = Random.Range(wMin, wMax);
                rt.growSeconds = Random.Range(gMin, gMax);
                rt.peakAlpha = Random.Range(aMin, aMax);
            }
        }

        private void ApplyStartupPhases()
        {
            int readyCount = 0;
            for (int i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i] != null && runtimes[i].ready)
                {
                    readyCount++;
                }
            }

            int midCount = Mathf.Clamp(midGrowCountAtStart, 0, readyCount);
            int assignedMid = 0;

            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.visual == null)
                {
                    continue;
                }

                if (assignedMid < midCount)
                {
                    float t = Random.Range(0.25f, 0.75f);
                    BeginGrow(rt, reserveCost: true, initialElapsed: rt.growSeconds * t);
                    assignedMid++;
                }
                else
                {
                    BeginWait(rt, Random.Range(0f, rt.waitSeconds));
                }
            }
        }

        private bool AnyWaitingReady()
        {
            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready)
                {
                    continue;
                }

                if (rt.phase == BubblePhase.Waiting && rt.phaseElapsed >= rt.waitSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnAmbienceTick(float tickIntervalSeconds)
        {
            if (KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.phase != BubblePhase.Waiting)
                {
                    continue;
                }

                rt.phaseElapsed += tickIntervalSeconds;
            }
        }

        public override bool TryStartAmbience()
        {
            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.visual == null)
                {
                    continue;
                }

                if (rt.phase != BubblePhase.Waiting || rt.phaseElapsed < rt.waitSeconds)
                {
                    continue;
                }

                // コントローラ側で Cost は既に予約済み
                BeginGrow(rt, reserveCost: false, initialElapsed: 0f);
                rt.costHeld = true;
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (KomayamaGameClock.ResolveIsPaused() || KomayamaCraftLoadGate.HoldGameTime)
            {
                return;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready || rt.phase != BubblePhase.Growing)
                {
                    continue;
                }

                TickGrow(rt, dt);
            }
        }

        private void TickGrow(BubbleRuntime rt, float dt)
        {
            rt.phaseElapsed += dt;
            float t = rt.growSeconds > 0.001f
                ? Mathf.Clamp01(rt.phaseElapsed / rt.growSeconds)
                : 1f;

            ApplyGrowVisual(rt, t);

            if (t >= 1f)
            {
                FinishGrow(rt);
            }
        }

        private void BeginGrow(BubbleRuntime rt, bool reserveCost, float initialElapsed)
        {
            if (reserveCost)
            {
                if (controller == null || !controller.TryReserveCost(Cost))
                {
                    BeginWait(rt, 0f);
                    return;
                }

                rt.costHeld = true;
            }

            rt.phase = BubblePhase.Growing;
            rt.phaseElapsed = Mathf.Clamp(initialElapsed, 0f, Mathf.Max(0.001f, rt.growSeconds));
            if (!rt.visual.gameObject.activeSelf)
            {
                rt.visual.gameObject.SetActive(true);
            }

            float t = rt.growSeconds > 0.001f
                ? Mathf.Clamp01(rt.phaseElapsed / rt.growSeconds)
                : 0f;
            ApplyGrowVisual(rt, t);
        }

        private void FinishGrow(BubbleRuntime rt)
        {
            if (rt.visual != null)
            {
                rt.visual.gameObject.SetActive(false);
                rt.visual.transform.localScale = rt.maxLocalScale;
                Color c = rt.baseRgb;
                c.a = rt.peakAlpha;
                rt.visual.color = c;
            }

            if (rt.costHeld && controller != null)
            {
                controller.ReleaseCost(Cost);
            }

            rt.costHeld = false;
            BeginWait(rt, 0f);
        }

        private void BeginWait(BubbleRuntime rt, float initialElapsed)
        {
            rt.phase = BubblePhase.Waiting;
            rt.phaseElapsed = Mathf.Max(0f, initialElapsed);
            if (rt.visual != null)
            {
                rt.visual.gameObject.SetActive(false);
            }
        }

        private void ApplyGrowVisual(BubbleRuntime rt, float t)
        {
            if (rt.visual == null)
            {
                return;
            }

            Vector3 start = BuildStartScale(rt.maxLocalScale);
            rt.visual.transform.localScale = Vector3.Lerp(start, rt.maxLocalScale, t);

            Color c = rt.baseRgb;
            c.a = Mathf.Lerp(0f, rt.peakAlpha, t);
            rt.visual.color = c;
        }

        private Vector3 BuildStartScale(Vector3 maxScale)
        {
            float sx = Mathf.Approximately(maxScale.x, 0f) ? startScale : Mathf.Sign(maxScale.x) * startScale;
            float sy = Mathf.Approximately(maxScale.y, 0f) ? startScale : Mathf.Sign(maxScale.y) * startScale;
            return new Vector3(sx, sy, maxScale.z);
        }

        private void OnDisable()
        {
            for (int i = 0; i < runtimes.Length; i++)
            {
                BubbleRuntime rt = runtimes[i];
                if (rt == null || !rt.ready)
                {
                    continue;
                }

                if (rt.costHeld && controller != null)
                {
                    controller.ReleaseCost(Cost);
                    rt.costHeld = false;
                }

                if (rt.visual != null)
                {
                    rt.visual.transform.localScale = rt.maxLocalScale;
                    Color c = rt.baseRgb;
                    c.a = rt.peakAlpha;
                    rt.visual.color = c;
                }
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
