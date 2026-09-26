using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 間欠泉アンビエント。1→5 → 4↔5 を 10 回（その間に α→0）を 1 サイクルとする。同時最大 1。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaGeyserAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum BurstPhase
        {
            Intro,
            Alternate
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer geyserVisual;
        [SerializeField] private Sprite[] frames;

        [Header("導入フレーム時間（1〜5・ゲーム秒）")]
        [SerializeField] private float[] frameDurations =
        {
            0.12f, 0.10f, 0.08f, 0.08f, 0.18f
        };

        [Header("4↔5 切り替え（この間に α→0）")]
        [SerializeField, Min(1)] private int alternateToggleCount = 10;
        [SerializeField, Min(0.01f)] private float alternateFrameSeconds = 0.08f;

        [Header("再出現待機（ゲーム秒）")]
        [SerializeField, Min(0f)] private float respawnWaitMin = 4f;
        [SerializeField, Min(0f)] private float respawnWaitRandom = 11f;

        private float waitRemaining;
        private bool active;
        private BurstPhase phase;
        private int frameIndex;
        private float phaseElapsed;
        private int alternateDone;
        private Color baseColor = Color.white;

        public override bool WantsStart =>
            !active &&
            waitRemaining <= 0f &&
            geyserVisual != null &&
            frames != null &&
            frames.Length >= 5;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            if (geyserVisual != null)
            {
                baseColor = geyserVisual.color;
                geyserVisual.gameObject.SetActive(false);
            }

            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
        }

        public override void OnAmbienceTick(float tickIntervalSeconds)
        {
            if (active || KomayamaGameClock.ResolveIsPaused())
            {
                return;
            }

            if (waitRemaining > 0f)
            {
                waitRemaining = Mathf.Max(0f, waitRemaining - tickIntervalSeconds);
            }
        }

        public override bool TryStartAmbience()
        {
            if (!WantsStart)
            {
                return false;
            }

            Sprite first = frames[0];
            if (first == null)
            {
                return false;
            }

            phase = BurstPhase.Intro;
            frameIndex = 0;
            phaseElapsed = 0f;
            alternateDone = 0;
            ApplyFrame(0);
            SetVisualAlpha(baseColor.a);
            if (!geyserVisual.gameObject.activeSelf)
            {
                geyserVisual.gameObject.SetActive(true);
            }

            active = true;
            return true;
        }

        private void Update()
        {
            if (!active || geyserVisual == null)
            {
                return;
            }

            if (KomayamaGameClock.ResolveIsPaused() || KomayamaCraftLoadGate.HoldGameTime)
            {
                return;
            }

            float dt = Time.deltaTime;
            switch (phase)
            {
                case BurstPhase.Intro:
                    TickIntro(dt);
                    break;
                case BurstPhase.Alternate:
                    TickAlternate(dt);
                    break;
            }
        }

        private void TickIntro(float dt)
        {
            phaseElapsed += dt;
            float duration = ResolveIntroDuration(frameIndex);
            while (phaseElapsed >= duration)
            {
                phaseElapsed -= duration;
                if (frameIndex >= 4)
                {
                    BeginAlternate();
                    return;
                }

                frameIndex++;
                ApplyFrame(frameIndex);
                duration = ResolveIntroDuration(frameIndex);
            }
        }

        private void BeginAlternate()
        {
            // 導入で 5 に到達済み。ここから 4→5→4→5… を alternateToggleCount 回しつつ α→0
            phase = BurstPhase.Alternate;
            alternateDone = 0;
            phaseElapsed = 0f;
            frameIndex = 3;
            ApplyFrame(frameIndex);
            SetVisualAlpha(baseColor.a);
        }

        private void TickAlternate(float dt)
        {
            phaseElapsed += dt;
            float duration = Mathf.Max(0.01f, alternateFrameSeconds);
            float totalSeconds = duration * Mathf.Max(1, alternateToggleCount);
            float fadeT = Mathf.Clamp01(phaseElapsed / totalSeconds);
            SetVisualAlpha(Mathf.Lerp(baseColor.a, 0f, fadeT));

            while (phaseElapsed >= duration * (alternateDone + 1))
            {
                alternateDone++;
                if (alternateDone >= alternateToggleCount)
                {
                    FinishBurst();
                    return;
                }

                // 3↔4（表示上の 4↔5）
                frameIndex = frameIndex == 3 ? 4 : 3;
                ApplyFrame(frameIndex);
            }
        }

        private void ApplyFrame(int index)
        {
            if (geyserVisual == null || frames == null || index < 0 || index >= frames.Length)
            {
                return;
            }

            Sprite sprite = frames[index];
            if (sprite != null)
            {
                geyserVisual.sprite = sprite;
            }
        }

        private void SetVisualAlpha(float alpha)
        {
            if (geyserVisual == null)
            {
                return;
            }

            Color c = baseColor;
            c.a = alpha;
            geyserVisual.color = c;
        }

        private float ResolveIntroDuration(int index)
        {
            if (frameDurations != null &&
                index >= 0 &&
                index < frameDurations.Length &&
                frameDurations[index] > 0.001f)
            {
                return frameDurations[index];
            }

            return 0.1f;
        }

        private void FinishBurst()
        {
            if (geyserVisual != null)
            {
                SetVisualAlpha(baseColor.a);
                geyserVisual.gameObject.SetActive(false);
            }

            active = false;
            waitRemaining = respawnWaitMin + Random.Range(0f, respawnWaitRandom);
            if (controller != null)
            {
                controller.ReleaseCost(Cost);
            }
        }

        private void OnDisable()
        {
            if (active)
            {
                FinishBurst();
            }
        }
    }
}
