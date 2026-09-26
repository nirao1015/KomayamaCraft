using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// Layer_Continent 配下の環境デカル／小物用。
    /// 地表オーバーレイ（<see cref="KCContinentOverlaySprite"/>／OJ）とは別系統。
    /// WASD 追従は親が Layer_Continent 配下であればそのまま。
    /// 周期透過・簡易フリップブックを Inspector で回せる。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KCContinentAmbientSprite : MonoBehaviour
    {
        [Header("表示枠")]
        [SerializeField]
        [Tooltip("枠フィット時の表示枠（ワールド単位）。自由変形中は無視。")]
        private Vector2 displaySize = new(1.5f, 1.5f);

        [SerializeField]
        [Tooltip("枠フィット時: ON=アスペクト比維持（contain）／OFF=枠へ引き伸ばし。")]
        private bool preserveAspect = true;

        [SerializeField]
        [Tooltip("ON: Display Size に合わせて Scale を自動更新。OFF: Scale を自由編集。")]
        private bool autoFitToDisplaySize = true;

        [Header("見た目")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Tooltip("同 Sorting Layer 内でベースより手前に出すオフセット。")]
        private int sortingOrderOffset = 10;

        [Header("周期透過")]
        [SerializeField] private bool pulseAlpha = true;
        [SerializeField, Min(0.05f)] private float pulsePeriodSeconds = 2f;
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.35f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 1f;
        [SerializeField, Tooltip("ON: ゲーム時間（ポーズで止まる）。OFF: unscaled。")]
        private bool useGameClock = true;

        [Header("フリップブック（任意）")]
        [SerializeField] private Sprite[] flipbookFrames;
        [SerializeField, Min(0.01f)] private float flipbookSecondsPerFrame = 0.25f;

        private Sprite lastSprite;
        private bool lastPreserveAspect;
        private bool lastAutoFit;
        private Vector2 lastDisplaySize;
        private float pulseElapsed;
        private float flipbookElapsed;
        private int flipbookIndex;
        private Color baseTint = Color.white;

        public bool PreserveAspect => preserveAspect;
        public bool AutoFitToDisplaySize => autoFitToDisplaySize;
        public bool PulseAlpha => pulseAlpha;

        private void OnEnable()
        {
            EnsureRenderer();
            ApplySortingOffset();
            ApplyTintBase();
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
            else
            {
                CacheSpriteOnly();
            }
        }

        private void OnValidate()
        {
            EnsureRenderer();
            ApplySortingOffset();
            ApplyTintBase();
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
            else
            {
                CacheSpriteOnly();
            }
        }

        private void LateUpdate()
        {
            EnsureRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            if (autoFitToDisplaySize &&
                (spriteRenderer.sprite != lastSprite ||
                 preserveAspect != lastPreserveAspect ||
                 autoFitToDisplaySize != lastAutoFit ||
                 displaySize != lastDisplaySize))
            {
                ApplyDisplaySizeFit();
            }

            if (!Application.isPlaying)
            {
                ApplyStaticAlpha();
                return;
            }

            float dt = ResolveDeltaTime();
            UpdateFlipbook(dt);
            // pulse オフ時は Color を触らない（アンビエントチャンネル等の外部駆動用）
            if (pulseAlpha)
            {
                UpdatePulseAlpha(dt);
            }
        }

        public void SetPreserveAspect(bool value)
        {
            preserveAspect = value;
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        public void SetAutoFitToDisplaySize(bool value)
        {
            autoFitToDisplaySize = value;
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        public void SetPulseAlpha(bool value)
        {
            pulseAlpha = value;
            if (!pulseAlpha)
            {
                ApplyStaticAlpha();
            }
        }

        [ContextMenu("Fit To Display Size Now")]
        public void ApplyDisplaySizeFit()
        {
            EnsureRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            lastSprite = spriteRenderer.sprite;
            lastPreserveAspect = preserveAspect;
            lastAutoFit = autoFitToDisplaySize;
            lastDisplaySize = displaySize;

            if (spriteRenderer.sprite == null ||
                displaySize.x <= 0f ||
                displaySize.y <= 0f)
            {
                return;
            }

            Vector2 native = spriteRenderer.sprite.bounds.size;
            float nx = Mathf.Max(0.0001f, native.x);
            float ny = Mathf.Max(0.0001f, native.y);

            float scaleX;
            float scaleY;
            if (preserveAspect)
            {
                float uniform = Mathf.Min(displaySize.x / nx, displaySize.y / ny);
                scaleX = uniform;
                scaleY = uniform;
            }
            else
            {
                scaleX = displaySize.x / nx;
                scaleY = displaySize.y / ny;
            }

            Vector3 localScale = transform.localScale;
            localScale.x = scaleX;
            localScale.y = scaleY;
            transform.localScale = localScale;
        }

        private void UpdateFlipbook(float dt)
        {
            if (flipbookFrames == null || flipbookFrames.Length == 0)
            {
                return;
            }

            flipbookElapsed += dt;
            float step = Mathf.Max(0.01f, flipbookSecondsPerFrame);
            while (flipbookElapsed >= step)
            {
                flipbookElapsed -= step;
                flipbookIndex = (flipbookIndex + 1) % flipbookFrames.Length;
                Sprite next = flipbookFrames[flipbookIndex];
                if (next != null)
                {
                    spriteRenderer.sprite = next;
                    if (autoFitToDisplaySize)
                    {
                        ApplyDisplaySizeFit();
                    }
                }
            }
        }

        private void UpdatePulseAlpha(float dt)
        {
            float period = Mathf.Max(0.05f, pulsePeriodSeconds);
            pulseElapsed += dt;
            float t = (Mathf.Sin((pulseElapsed / period) * Mathf.PI * 2f) + 1f) * 0.5f;
            float a = Mathf.Lerp(alphaMin, alphaMax, t);
            Color c = baseTint;
            c.a = a;
            spriteRenderer.color = c;
        }

        private void ApplyStaticAlpha()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Color c = baseTint;
            if (!pulseAlpha)
            {
                c.a = baseTint.a;
            }
            else if (!Application.isPlaying)
            {
                c.a = alphaMax;
            }

            spriteRenderer.color = c;
        }

        private void ApplyTintBase()
        {
            baseTint = tint;
            if (spriteRenderer != null && (!Application.isPlaying || !pulseAlpha))
            {
                spriteRenderer.color = tint;
            }
        }

        private void ApplySortingOffset()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            // Layer 名は DrawOrder が配下全体に付ける。オーダーだけ押し上げる。
            if (sortingOrderOffset != 0 && spriteRenderer.sortingOrder < sortingOrderOffset)
            {
                spriteRenderer.sortingOrder = sortingOrderOffset;
            }
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void CacheSpriteOnly()
        {
            EnsureRenderer();
            if (spriteRenderer != null)
            {
                lastSprite = spriteRenderer.sprite;
            }

            lastPreserveAspect = preserveAspect;
            lastAutoFit = autoFitToDisplaySize;
            lastDisplaySize = displaySize;
        }

        private float ResolveDeltaTime()
        {
            if (!useGameClock)
            {
                return Time.unscaledDeltaTime;
            }

            if (KomayamaGameClock.ResolveIsPaused() || KomayamaCraftLoadGate.HoldGameTime)
            {
                return 0f;
            }

            return Time.deltaTime;
        }
    }
}
