using System;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 砂漠砂嵐。南端寄りの帯を左→右へ流す。絵順・時間は固定（乱数なし）。
    /// 1 周期 = パターン A→B→C（各: 穏やか→急）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaDesertDustAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum Mood
        {
            Calm,
            Intense
        }

        [Serializable]
        private sealed class PatternTiming
        {
            public float calmSeconds = 30f;
            public float intenseSeconds = 12f;
        }

        [Serializable]
        private sealed class BandRuntime
        {
            public SpriteRenderer visual;
            public bool active;
            public float travelElapsed;
            public float travelDuration;
            public float fadeInSeconds;
            public float fadeOutSeconds;
            public float baseAlpha = 1f;
            public float peakAlpha;
            public float homeY;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Vector3 baseLocalScale = Vector3.one;
            public Color baseRgb = Color.white;
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer[] bandVisuals;
        [SerializeField] private Sprite[] dustSprites;

        [Header("流す帯 X（Y・Scale は各 Band のシーン値を使用）")]
        [SerializeField] private float corridorCenterX = 4f;
        [SerializeField] private float corridorHalfWidth = 22f;

        [Header("横断時間（ゲーム秒・ゆっくり寄り）")]
        [SerializeField, Min(0.5f)] private float calmCrossSeconds = 8f;
        [SerializeField, Min(0.5f)] private float intenseCrossSeconds = 6f;

        [Header("フェード")]
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.8f;

        [Header("α（オーサリング Color.a × 比率）")]
        [SerializeField, Range(0f, 1f)] private float calmAlphaRatio = 0.35f;
        [SerializeField, Range(0f, 1f)] private float intenseAlphaRatio = 0.7f;

        [Header("本数")]
        [SerializeField, Min(1)] private int calmBandCount = 1;
        [SerializeField, Min(1)] private int intenseBandCount = 2;

        [Header("周期 A→B→C（固定・乱数なし）")]
        [SerializeField] private PatternTiming[] patterns =
        {
            new PatternTiming { calmSeconds = 30f, intenseSeconds = 12f },
            new PatternTiming { calmSeconds = 45f, intenseSeconds = 18f },
            new PatternTiming { calmSeconds = 20f, intenseSeconds = 10f }
        };

        private bool running;
        private BandRuntime[] bands = Array.Empty<BandRuntime>();
        private int patternIndex;
        private Mood mood = Mood.Calm;
        private float moodElapsed;
        private int nextSpriteIndex;
        private float spawnCooldown;

        public override bool WantsStart =>
            !running &&
            bands != null &&
            HasReadyBand() &&
            dustSprites != null &&
            dustSprites.Length > 0;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            RebuildBands();
            HideAllBands();
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

        private void RebuildBands()
        {
            if (bandVisuals == null || bandVisuals.Length == 0)
            {
                bands = Array.Empty<BandRuntime>();
                return;
            }

            bands = new BandRuntime[bandVisuals.Length];
            for (int i = 0; i < bandVisuals.Length; i++)
            {
                BandRuntime rt = new BandRuntime();
                bands[i] = rt;
                CaptureOne(rt, bandVisuals[i]);
            }
        }

        private static void CaptureOne(BandRuntime rt, SpriteRenderer visual)
        {
            rt.visual = visual;
            if (visual == null)
            {
                return;
            }

            Color c = visual.color;
            rt.baseLocalScale = visual.transform.localScale;
            rt.homeY = visual.transform.position.y;
            rt.baseRgb = new Color(c.r, c.g, c.b, 1f);
            rt.baseAlpha = Mathf.Clamp01(c.a);
            rt.peakAlpha = rt.baseAlpha;
        }

        private bool HasReadyBand()
        {
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i] != null && bands[i].visual != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void HideAllBands()
        {
            for (int i = 0; i < bands.Length; i++)
            {
                BandRuntime rt = bands[i];
                if (rt == null || rt.visual == null)
                {
                    continue;
                }

                rt.active = false;
                rt.visual.gameObject.SetActive(false);
            }
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
            patternIndex = 0;
            mood = Mood.Calm;
            moodElapsed = 0f;
            nextSpriteIndex = 0;
            spawnCooldown = 0f;
            HideAllBands();
            TrySpawnToFill();
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
            TickMood(dt);
            TickBands(dt);

            if (spawnCooldown > 0f)
            {
                spawnCooldown -= dt;
            }

            TrySpawnToFill();
        }

        private void TickMood(float dt)
        {
            if (patterns == null || patterns.Length == 0)
            {
                return;
            }

            PatternTiming timing = patterns[Mathf.Clamp(patternIndex, 0, patterns.Length - 1)];
            float moodDuration = mood == Mood.Calm
                ? Mathf.Max(0.1f, timing.calmSeconds)
                : Mathf.Max(0.1f, timing.intenseSeconds);

            moodElapsed += dt;
            if (moodElapsed < moodDuration)
            {
                return;
            }

            moodElapsed = 0f;
            if (mood == Mood.Calm)
            {
                mood = Mood.Intense;
            }
            else
            {
                mood = Mood.Calm;
                patternIndex = (patternIndex + 1) % patterns.Length;
            }

            // ムード切替直後に本数を合わせる
            spawnCooldown = 0f;
            TrySpawnToFill();
        }

        private void TickBands(float dt)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                BandRuntime rt = bands[i];
                if (rt == null || !rt.active || rt.visual == null)
                {
                    continue;
                }

                rt.travelElapsed += dt;
                float t = rt.travelDuration > 0.001f
                    ? Mathf.Clamp01(rt.travelElapsed / rt.travelDuration)
                    : 1f;

                rt.visual.transform.position = Vector3.Lerp(rt.startPosition, rt.endPosition, t);
                ApplyAlpha(rt);

                if (t >= 1f)
                {
                    rt.active = false;
                    rt.visual.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyAlpha(BandRuntime rt)
        {
            float a = rt.peakAlpha;
            float fadeIn = Mathf.Max(0.01f, rt.fadeInSeconds);
            float fadeOut = Mathf.Max(0.01f, rt.fadeOutSeconds);
            float duration = Mathf.Max(0.01f, rt.travelDuration);

            if (rt.travelElapsed < fadeIn)
            {
                a *= Mathf.Clamp01(rt.travelElapsed / fadeIn);
            }
            else
            {
                float remaining = duration - rt.travelElapsed;
                if (remaining < fadeOut)
                {
                    a *= Mathf.Clamp01(remaining / fadeOut);
                }
            }

            Color c = rt.baseRgb;
            c.a = a;
            rt.visual.color = c;
        }

        private void TrySpawnToFill()
        {
            if (spawnCooldown > 0f)
            {
                return;
            }

            int target = mood == Mood.Calm
                ? Mathf.Max(1, calmBandCount)
                : Mathf.Max(1, intenseBandCount);
            target = Mathf.Min(target, bands.Length);

            int activeCount = CountActiveBands();
            if (activeCount >= target)
            {
                return;
            }

            // コア（帯の中央付近）が空かないよう、先頭が半ばを過ぎたら次を出す
            if (activeCount > 0 && !NeedsFollowUpBand())
            {
                return;
            }

            if (!TrySpawnBand())
            {
                return;
            }

            // 急は続けて2本目を少し遅らせて出す
            spawnCooldown = mood == Mood.Intense ? intenseCrossSeconds * 0.35f : calmCrossSeconds * 0.55f;
        }

        private bool NeedsFollowUpBand()
        {
            // どれか1本でも travelT < 0.45 ならまだコア手前〜コア内入口。追加は急がらない
            for (int i = 0; i < bands.Length; i++)
            {
                BandRuntime rt = bands[i];
                if (rt == null || !rt.active)
                {
                    continue;
                }

                float t = rt.travelDuration > 0.001f
                    ? rt.travelElapsed / rt.travelDuration
                    : 1f;
                if (t < 0.45f)
                {
                    return false;
                }
            }

            return true;
        }

        private int CountActiveBands()
        {
            int n = 0;
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i] != null && bands[i].active)
                {
                    n++;
                }
            }

            return n;
        }

        private bool TrySpawnBand()
        {
            BandRuntime slot = null;
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i] != null && bands[i].visual != null && !bands[i].active)
                {
                    slot = bands[i];
                    break;
                }
            }

            if (slot == null || dustSprites == null || dustSprites.Length == 0)
            {
                return false;
            }

            Sprite sprite = dustSprites[nextSpriteIndex % dustSprites.Length];
            nextSpriteIndex = (nextSpriteIndex + 1) % dustSprites.Length;
            if (sprite == null)
            {
                return false;
            }

            float cross = mood == Mood.Calm ? calmCrossSeconds : intenseCrossSeconds;
            float alphaRatio = mood == Mood.Calm ? calmAlphaRatio : intenseAlphaRatio;

            slot.visual.sprite = sprite;
            slot.visual.transform.localScale = slot.baseLocalScale;
            float y = slot.homeY;
            float halfW = corridorHalfWidth;
            float spriteHalf = sprite.bounds.extents.x * Mathf.Abs(slot.baseLocalScale.x);
            float z = slot.visual.transform.position.z;
            slot.startPosition = new Vector3(corridorCenterX - halfW - spriteHalf, y, z);
            slot.endPosition = new Vector3(corridorCenterX + halfW + spriteHalf, y, z);
            slot.travelDuration = Mathf.Max(0.5f, cross);
            slot.travelElapsed = 0f;
            slot.fadeInSeconds = fadeInSeconds;
            slot.fadeOutSeconds = fadeOutSeconds;
            slot.peakAlpha = Mathf.Clamp01(slot.baseAlpha * alphaRatio);
            slot.visual.transform.position = slot.startPosition;
            Color c = slot.baseRgb;
            c.a = 0f;
            slot.visual.color = c;
            slot.visual.gameObject.SetActive(true);
            slot.active = true;
            return true;
        }

        private void OnDisable()
        {
            if (!running)
            {
                return;
            }

            running = false;
            HideAllBands();
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
                RebuildBands();
            }
        }
#endif
    }
}
