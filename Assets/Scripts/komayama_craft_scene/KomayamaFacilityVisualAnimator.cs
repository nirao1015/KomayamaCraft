using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 加工設備の見た目。Processing 中のみ稼働フレームを再生し、
    /// それ以外（待機・燃料待ち・素材待ち）はベース画像を固定表示する。
    /// フレーム待機は <see cref="KomayamaFacilityAnimSettings"/> を参照する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KomayamaFacilityVisualAnimator : MonoBehaviour
    {
        private const string SettingsResourcePath = "GameData/FacilityAnimSettings";

        [Header("参照")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private KomayamaProcessingFacility facility;
        [SerializeField] private KomayamaFacilityAnimSettings settings;

        [Header("スプライト")]
        [Tooltip("未稼働・ブループリント用。ナンバリングなしベース画像。")]
        [SerializeField] private Sprite idleSprite;
        [Tooltip("稼働中コマ（-1, -2, …）。")]
        [SerializeField] private Sprite[] operatingFrames = System.Array.Empty<Sprite>();

        [Header("再生")]
        [Tooltip("オン: 端で折り返し。オフ: 末尾の次は先頭へ（loop 時）。")]
        [SerializeField] private bool pingPong;
        [Tooltip("オフかつ非ピンポン時は末尾で停止。通常はオン。")]
        [SerializeField] private bool loop = true;

        private float frameTimer;
        private int frameIndex;
        private int frameDirection = 1;
        private bool operating;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (facility == null)
            {
                facility = GetComponent<KomayamaProcessingFacility>();
            }

            if (settings == null)
            {
                settings = Resources.Load<KomayamaFacilityAnimSettings>(SettingsResourcePath);
            }
        }

        private void OnEnable()
        {
            if (facility != null)
            {
                facility.StateChanged += OnFacilityStateChanged;
            }

            SyncFromFacilityState(resetFrame: true);
        }

        private void OnDisable()
        {
            if (facility != null)
            {
                facility.StateChanged -= OnFacilityStateChanged;
            }
        }

        private void Update()
        {
            if (!operating ||
                spriteRenderer == null ||
                operatingFrames == null ||
                operatingFrames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float step = ResolveSecondsPerFrame();
            while (frameTimer >= step)
            {
                frameTimer -= step;
                if (!AdvanceFrame())
                {
                    break;
                }

                step = ResolveSecondsPerFrame();
                if (step <= 0f)
                {
                    break;
                }
            }

            ApplyOperatingFrame(frameIndex);
        }

        private void OnFacilityStateChanged()
        {
            SyncFromFacilityState(resetFrame: false);
        }

        private void SyncFromFacilityState(bool resetFrame)
        {
            bool shouldOperate = facility != null &&
                                 facility.State == KomayamaFacilityState.Processing;

            if (shouldOperate)
            {
                if (!operating || resetFrame)
                {
                    BeginOperating(resetFrame || !operating);
                }

                return;
            }

            ShowIdle();
        }

        private void BeginOperating(bool resetFrame)
        {
            operating = true;
            if (resetFrame)
            {
                frameIndex = 0;
                frameDirection = 1;
                frameTimer = 0f;
            }

            if (operatingFrames == null || operatingFrames.Length == 0)
            {
                ShowIdle();
                return;
            }

            ApplyOperatingFrame(frameIndex);
        }

        private void ShowIdle()
        {
            operating = false;
            frameTimer = 0f;
            frameIndex = 0;
            frameDirection = 1;
            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        private bool AdvanceFrame()
        {
            if (operatingFrames == null || operatingFrames.Length == 0)
            {
                return false;
            }

            if (operatingFrames.Length <= 1)
            {
                frameIndex = 0;
                return loop || pingPong;
            }

            if (pingPong)
            {
                frameIndex += frameDirection;
                if (frameIndex >= operatingFrames.Length - 1)
                {
                    frameIndex = operatingFrames.Length - 1;
                    frameDirection = -1;
                    return true;
                }

                if (frameIndex <= 0)
                {
                    frameIndex = 0;
                    frameDirection = 1;
                    return true;
                }

                return true;
            }

            frameIndex++;
            if (frameIndex < operatingFrames.Length)
            {
                return true;
            }

            if (loop)
            {
                frameIndex = 0;
                return true;
            }

            frameIndex = operatingFrames.Length - 1;
            return false;
        }

        private void ApplyOperatingFrame(int index)
        {
            if (spriteRenderer == null ||
                operatingFrames == null ||
                operatingFrames.Length == 0)
            {
                return;
            }

            spriteRenderer.sprite =
                operatingFrames[Mathf.Clamp(index, 0, operatingFrames.Length - 1)];
        }

        private float ResolveSecondsPerFrame()
        {
            if (settings != null)
            {
                return settings.SecondsPerFrame;
            }

            return 0.12f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (facility == null)
            {
                facility = GetComponent<KomayamaProcessingFacility>();
            }

            if (!Application.isPlaying &&
                spriteRenderer != null &&
                idleSprite != null &&
                spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
#endif
    }
}
