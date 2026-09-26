using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 西海岸の白波。初期位置から岸へ前進→沖へ後退。画像順・時間は固定（乱数なし）。
    /// α は岸に最も近い地点（前進端）でピーク。待ちは現実の波程度の固定秒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaWestCoastWaveAmbience : KomayamaMapAmbienceMapChannel
    {
        private enum WavePhase
        {
            Advance,
            Retreat,
            Waiting
        }

        [Header("参照")]
        [SerializeField] private KomayamaMapAmbienceController controller;
        [SerializeField] private SpriteRenderer waveVisual;
        [SerializeField] private Sprite[] waveSprites;

        [Header("移動（固定・ゲーム秒）ざーん／ざーーん")]
        [SerializeField, Min(0.1f), Tooltip("寄せ（ざーん）")]
        private float advanceSeconds = 0.65f;
        [SerializeField, Min(0.1f), Tooltip("引き（ざーーん・長め）")]
        private float retreatSeconds = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("右方向（岸）への移動距離（ワールド）")]
        private float travelDistance = 12f;

        [Header("待ち（固定・現実の波程度）")]
        [SerializeField, Min(0f)] private float waitSeconds = 7f;

        private bool running;
        private bool costHeld;
        private WavePhase phase = WavePhase.Waiting;
        private float phaseElapsed;
        private Vector3 homePosition;
        private Vector3 shorePosition;
        private Vector3 baseLocalScale = Vector3.one;
        private Color baseColor = Color.white;
        private bool hasBase;
        private int nextSpriteIndex;

        // 初回だけコントローラ経由で開始。以降は自走（待ち→前進→後退）
        public override bool WantsStart =>
            !running &&
            waveVisual != null &&
            waveSprites != null &&
            waveSprites.Length > 0 &&
            hasBase;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<KomayamaMapAmbienceController>();
            }

            CaptureBaseFromVisual();
            if (waveVisual != null)
            {
                homePosition = waveVisual.transform.position;
                shorePosition = homePosition + Vector3.right * travelDistance;
                waveVisual.gameObject.SetActive(false);
            }
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

        private void CaptureBaseFromVisual()
        {
            if (waveVisual == null)
            {
                hasBase = false;
                return;
            }

            baseLocalScale = waveVisual.transform.localScale;
            baseColor = waveVisual.color;
            hasBase = true;
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
            // コントローラ側で Cost は既に予約済み
            costHeld = true;
            BeginAdvance();
            return phase == WavePhase.Advance;
        }

        private void Update()
        {
            if (!running || !hasBase)
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
                case WavePhase.Waiting:
                    TickWaiting(dt);
                    break;
                case WavePhase.Advance:
                    TickAdvance(dt);
                    break;
                case WavePhase.Retreat:
                    TickRetreat(dt);
                    break;
            }
        }

        private void TickWaiting(float dt)
        {
            phaseElapsed += dt;
            if (phaseElapsed < waitSeconds)
            {
                return;
            }

            BeginAdvance();
        }

        private void BeginAdvance()
        {
            if (waveVisual == null || waveSprites == null || waveSprites.Length == 0)
            {
                return;
            }

            Sprite sprite = waveSprites[nextSpriteIndex % waveSprites.Length];
            nextSpriteIndex = (nextSpriteIndex + 1) % waveSprites.Length;
            if (sprite == null)
            {
                return;
            }

            if (!costHeld && controller != null)
            {
                if (!controller.TryReserveCost(Cost))
                {
                    // 予算不足時は待ちを少し戻して再挑戦
                    phase = WavePhase.Waiting;
                    phaseElapsed = Mathf.Max(0f, waitSeconds - 1f);
                    return;
                }

                costHeld = true;
            }

            shorePosition = homePosition + Vector3.right * travelDistance;

            Transform t = waveVisual.transform;
            t.localScale = baseLocalScale;
            waveVisual.sprite = sprite;
            t.position = homePosition;
            SetAlpha(0f);
            if (!waveVisual.gameObject.activeSelf)
            {
                waveVisual.gameObject.SetActive(true);
            }

            phase = WavePhase.Advance;
            phaseElapsed = 0f;
        }

        private void TickAdvance(float dt)
        {
            phaseElapsed += dt;
            float duration = Mathf.Max(0.1f, advanceSeconds);
            float u = Mathf.Clamp01(phaseElapsed / duration);
            // ざーん: 勢いよく寄せて岸でふわり止まる
            float e = EaseOutCubic(u);
            waveVisual.transform.position = Vector3.Lerp(homePosition, shorePosition, e);
            SetAlpha(baseColor.a * e);

            if (u >= 1f)
            {
                phase = WavePhase.Retreat;
                phaseElapsed = 0f;
                waveVisual.transform.position = shorePosition;
                SetAlpha(baseColor.a);
            }
        }

        private void TickRetreat(float dt)
        {
            phaseElapsed += dt;
            float duration = Mathf.Max(0.1f, retreatSeconds);
            float u = Mathf.Clamp01(phaseElapsed / duration);
            // ざーーん: いったん粘ってから長く引く
            float e = EaseInOutCubic(u);
            waveVisual.transform.position = Vector3.Lerp(shorePosition, homePosition, e);
            SetAlpha(baseColor.a * (1f - e));

            if (u >= 1f)
            {
                FinishCycle();
            }
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - (inv * inv * inv);
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

        private void FinishCycle()
        {
            if (waveVisual != null)
            {
                waveVisual.transform.position = homePosition;
                waveVisual.transform.localScale = baseLocalScale;
                SetAlpha(baseColor.a);
                waveVisual.gameObject.SetActive(false);
            }

            if (costHeld && controller != null)
            {
                controller.ReleaseCost(Cost);
            }

            costHeld = false;
            phase = WavePhase.Waiting;
            phaseElapsed = 0f;
        }

        private void SetAlpha(float alpha)
        {
            if (waveVisual == null)
            {
                return;
            }

            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            waveVisual.color = c;
        }

        private void OnDisable()
        {
            if (!running)
            {
                return;
            }

            if (costHeld && controller != null)
            {
                controller.ReleaseCost(Cost);
                costHeld = false;
            }

            if (waveVisual != null)
            {
                waveVisual.transform.position = homePosition;
                waveVisual.color = baseColor;
            }

            running = false;
            phase = WavePhase.Waiting;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (waveVisual != null && !Application.isPlaying)
            {
                CaptureBaseFromVisual();
                homePosition = waveVisual.transform.position;
                shorePosition = homePosition + Vector3.right * travelDistance;
            }
        }
#endif
    }
}
