using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    public sealed class BuzzPaperConfettiController : MonoBehaviour
    {
        private static readonly Color[] MetallicPalette =
        {
            new Color32(196, 199, 202, 255), // Silver
            new Color32(212, 175, 55, 255),  // Gold
            new Color32(80, 200, 120, 255),  // Emerald
            new Color32(15, 82, 186, 255),   // Sapphire
            new Color32(155, 17, 30, 255),   // Ruby
            new Color32(255, 214, 10, 255)   // Yellow
        };

        private enum FallPattern
        {
            Flutter = 0,
            Straight = 1,
            Spiral = 2
        }

        private sealed class Piece
        {
            public RectTransform Rect;
            public Image Image;
            public FallPattern Pattern;
            public Vector2 Position;
            public float FallSpeed;
            public float RotationSpeed;
            public float SwayAmplitude;
            public float SwayFrequency;
            public float SwayPhase;
            public float Life;
            public float Age;
            public float BaseScale;
            public float InitialAlpha;
            public float InitialRotationY;
            public float CurrentRotationY;
            public float CurrentRotationZ;
            public float RotationYSpeed;
        }

        [Header("Playback")]
        [SerializeField] private float defaultPlaySeconds = 10f;

        [Header("Particle Budget")]
        [SerializeField] private int maxAliveParticlesTotal = 100;
        [SerializeField] private float minParticleSize = 0.045f;
        [SerializeField] private float maxParticleSize = 0.11f;
        [SerializeField] private float spawnPerSecond = 22f;
        [SerializeField] private float spawnRandomYOffsetMax = 120f;
        [SerializeField, Range(0f, 180f)] private float spawnRandomYRotationMax = 35f;
        [SerializeField] private float spawnRotationZMin = -40f;
        [SerializeField] private float spawnRotationZMax = 40f;
        [SerializeField] private float fallRotationYSpeedMin = 12f;
        [SerializeField] private float fallRotationYSpeedMax = 36f;

        [Header("Alpha")]
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.75f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 1.0f;

        [Header("Optional Sprite Slots")]
        [SerializeField] private Sprite paperSprite01;
        [SerializeField] private Sprite paperSprite02;
        [SerializeField] private Sprite paperSprite03;

        [Header("Visual")]
        [SerializeField] private bool forceOpaqueSolidPiece = false;

        [Header("SE")]
        [SerializeField] private bool playSeOnPlay = true;
        [SerializeField] private float seDelaySeconds = 0f;

        private readonly List<Piece> activePieces = new List<Piece>(256);
        private RectTransform buzzPaperRect;
        private float emitRemainingSeconds;
        private float spawnAccumulator;
        private Sprite fallbackSprite;
        private bool isPlaying;
        private bool isSePending;
        private float seRemainingSeconds;

        public static BuzzPaperConfettiController EnsureSceneController()
        {
            GameObject buzzPaper = FindBuzzPaperRoot();
            if (buzzPaper == null)
            {
                buzzPaper = CreateBuzzPaperRoot();
            }

            if (buzzPaper == null)
            {
                return null;
            }

            BuzzPaperConfettiController controller = buzzPaper.GetComponent<BuzzPaperConfettiController>();
            if (controller == null)
            {
                controller = buzzPaper.AddComponent<BuzzPaperConfettiController>();
            }

            return controller;
        }

        public void Play()
        {
            PlayForSeconds(defaultPlaySeconds);
        }

        public void PlayForSeconds(float durationSeconds)
        {
            float duration = Mathf.Max(0.01f, durationSeconds > 0f ? durationSeconds : defaultPlaySeconds);
            EnsureRootCached();
            emitRemainingSeconds = Mathf.Max(emitRemainingSeconds, duration);
            isPlaying = true;
            ScheduleSePlayback();
        }

        /// <summary>ゲームクリア演出など、BuzzPaper SE を鳴らさず紙吹雪のみ伸ばすとき。</summary>
        public void PlayForSecondsWithoutBuzzPaperSe(float durationSeconds)
        {
            float duration = Mathf.Max(0.01f, durationSeconds > 0f ? durationSeconds : defaultPlaySeconds);
            EnsureRootCached();
            emitRemainingSeconds = Mathf.Max(emitRemainingSeconds, duration);
            isPlaying = true;
            isSePending = false;
            seRemainingSeconds = 0f;
        }

        /// <summary>新規パーティクルの放出のみ止める（落下中はそのまま）。</summary>
        public void StopEmittingNewPieces()
        {
            emitRemainingSeconds = 0f;
        }

        private void Awake()
        {
            EnsureRootCached();
            EnsureFallbackSpriteIfNeeded();
        }

        private void Update()
        {
            UpdatePendingSeTimer();

            if (!isPlaying)
            {
                return;
            }

            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            if (emitRemainingSeconds > 0f)
            {
                emitRemainingSeconds = Mathf.Max(0f, emitRemainingSeconds - dt);
                SpawnPieces(dt);
            }

            UpdatePieces(dt);

            if (emitRemainingSeconds <= 0f && activePieces.Count == 0)
            {
                isPlaying = false;
            }
        }

        private void ScheduleSePlayback()
        {
            if (!playSeOnPlay)
            {
                isSePending = false;
                seRemainingSeconds = 0f;
                return;
            }

            isSePending = true;
            seRemainingSeconds = Mathf.Max(0f, seDelaySeconds);
        }

        private void UpdatePendingSeTimer()
        {
            if (!isSePending)
            {
                return;
            }

            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            seRemainingSeconds -= dt;
            if (seRemainingSeconds > 0f)
            {
                return;
            }

            isSePending = false;
            seRemainingSeconds = 0f;
            PlayConfiguredSe();
        }

        private void PlayConfiguredSe()
        {
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.BuzzPaper);
        }

        private void SpawnPieces(float dt)
        {
            if (buzzPaperRect == null)
            {
                return;
            }
            if (activePieces.Count >= Mathf.Max(1, maxAliveParticlesTotal))
            {
                return;
            }

            spawnAccumulator += Mathf.Max(0f, spawnPerSecond) * dt;
            int spawnCount = Mathf.FloorToInt(spawnAccumulator);
            if (spawnCount <= 0)
            {
                return;
            }
            spawnAccumulator -= spawnCount;

            int allowed = Mathf.Max(0, Mathf.Max(1, maxAliveParticlesTotal) - activePieces.Count);
            spawnCount = Mathf.Min(spawnCount, allowed);
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnOnePiece();
            }
        }

        private void SpawnOnePiece()
        {
            if (buzzPaperRect == null)
            {
                return;
            }

            Rect rect = buzzPaperRect.rect;
            float width = Mathf.Max(320f, rect.width);
            float height = Mathf.Max(180f, rect.height);

            GameObject go = new GameObject("ConfettiPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(buzzPaperRect, false);

            RectTransform r = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            if (r == null || image == null)
            {
                Destroy(go);
                return;
            }

            FallPattern pattern = (FallPattern)UnityEngine.Random.Range(0, 3);
            Sprite sprite = ResolveSpriteByPattern(pattern);
            if (forceOpaqueSolidPiece)
            {
                image.sprite = fallbackSprite;
            }
            else
            {
                image.sprite = sprite != null ? sprite : fallbackSprite;
            }
            image.raycastTarget = false;
            Color baseColor = MetallicPalette[UnityEngine.Random.Range(0, MetallicPalette.Length)];
            float minAlpha = Mathf.Clamp01(Mathf.Min(alphaMin, alphaMax));
            float maxAlpha = Mathf.Clamp01(Mathf.Max(alphaMin, alphaMax));
            float initialAlpha = UnityEngine.Random.Range(minAlpha, maxAlpha);
            baseColor.a = initialAlpha;
            image.color = baseColor;

            float canvasHeightRef = 1080f;
            float sizeMin = Mathf.Max(0.001f, minParticleSize) * canvasHeightRef;
            float sizeMax = Mathf.Max(sizeMin, maxParticleSize * canvasHeightRef);
            float size = UnityEngine.Random.Range(sizeMin, sizeMax);

            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size * 0.55f, size);

            float initialRotationY = UnityEngine.Random.Range(-Mathf.Abs(spawnRandomYRotationMax), Mathf.Abs(spawnRandomYRotationMax));
            float initialRotationZ = UnityEngine.Random.Range(
                Mathf.Min(spawnRotationZMin, spawnRotationZMax),
                Mathf.Max(spawnRotationZMin, spawnRotationZMax));

            Piece piece = new Piece
            {
                Rect = r,
                Image = image,
                Pattern = pattern,
                Position = new Vector2(
                    UnityEngine.Random.Range(-width * 0.5f, width * 0.5f),
                    (height * 0.5f) + 40f - UnityEngine.Random.Range(0f, Mathf.Max(0f, spawnRandomYOffsetMax))),
                Age = 0f,
                Life = UnityEngine.Random.Range(4.0f, 8.0f),
                BaseScale = UnityEngine.Random.Range(0.9f, 1.2f),
                SwayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                InitialAlpha = initialAlpha,
                InitialRotationY = initialRotationY,
                CurrentRotationY = initialRotationY,
                CurrentRotationZ = initialRotationZ,
                RotationYSpeed = UnityEngine.Random.Range(
                    Mathf.Min(fallRotationYSpeedMin, fallRotationYSpeedMax),
                    Mathf.Max(fallRotationYSpeedMin, fallRotationYSpeedMax))
            };

            switch (pattern)
            {
                case FallPattern.Flutter:
                    piece.FallSpeed = UnityEngine.Random.Range(160f, 230f);
                    piece.RotationSpeed = UnityEngine.Random.Range(-220f, 220f);
                    piece.SwayAmplitude = UnityEngine.Random.Range(20f, 45f);
                    piece.SwayFrequency = UnityEngine.Random.Range(2.0f, 3.4f);
                    break;
                case FallPattern.Straight:
                    piece.FallSpeed = UnityEngine.Random.Range(260f, 380f);
                    piece.RotationSpeed = UnityEngine.Random.Range(-80f, 80f);
                    piece.SwayAmplitude = UnityEngine.Random.Range(2f, 10f);
                    piece.SwayFrequency = UnityEngine.Random.Range(0.7f, 1.4f);
                    break;
                default:
                    piece.FallSpeed = UnityEngine.Random.Range(200f, 300f);
                    piece.RotationSpeed = UnityEngine.Random.Range(-540f, 540f);
                    piece.SwayAmplitude = UnityEngine.Random.Range(12f, 28f);
                    piece.SwayFrequency = UnityEngine.Random.Range(1.6f, 2.6f);
                    break;
            }
            ApplyPieceVisual(piece);
            activePieces.Add(piece);
        }

        private static GameObject FindBuzzPaperRoot()
        {
            GameObject byPath = GameObject.Find("PanelCanvas/BuzzPaper");
            if (byPath != null)
            {
                return byPath;
            }

            Transform[] all = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == "BuzzPaper")
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private static GameObject CreateBuzzPaperRoot()
        {
            GameObject panelCanvas = GameObject.Find("PanelCanvas");
            if (panelCanvas == null)
            {
                panelCanvas = new GameObject("PanelCanvas", typeof(RectTransform), typeof(Canvas));
                Canvas canvas = panelCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }

            GameObject buzzPaper = new GameObject("BuzzPaper", typeof(RectTransform));
            buzzPaper.transform.SetParent(panelCanvas.transform, false);
            RectTransform rect = buzzPaper.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition3D = Vector3.zero;
            }

            return buzzPaper;
        }

        private void EnsureRootCached()
        {
            if (buzzPaperRect != null)
            {
                return;
            }

            buzzPaperRect = transform as RectTransform;
            if (buzzPaperRect == null)
            {
                buzzPaperRect = gameObject.AddComponent<RectTransform>();
                buzzPaperRect.anchorMin = Vector2.zero;
                buzzPaperRect.anchorMax = Vector2.one;
                buzzPaperRect.offsetMin = Vector2.zero;
                buzzPaperRect.offsetMax = Vector2.zero;
            }
        }

        private void EnsureFallbackSpriteIfNeeded()
        {
            if (fallbackSprite != null)
            {
                return;
            }

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            fallbackSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private Sprite ResolveSpriteByPattern(FallPattern pattern)
        {
            return pattern switch
            {
                FallPattern.Flutter => paperSprite01 != null ? paperSprite01 : fallbackSprite,
                FallPattern.Straight => paperSprite02 != null ? paperSprite02 : fallbackSprite,
                FallPattern.Spiral => paperSprite03 != null ? paperSprite03 : fallbackSprite,
                _ => fallbackSprite
            };
        }

        private void UpdatePieces(float dt)
        {
            if (buzzPaperRect == null)
            {
                return;
            }

            Rect rect = buzzPaperRect.rect;
            float bottomY = -Mathf.Max(180f, rect.height) * 0.5f - 80f;

            for (int i = activePieces.Count - 1; i >= 0; i--)
            {
                Piece piece = activePieces[i];
                if (piece == null || piece.Rect == null)
                {
                    RemovePieceAt(i);
                    continue;
                }

                piece.Age += dt;
                float tLife = piece.Life > 0.001f ? Mathf.Clamp01(piece.Age / piece.Life) : 1f;
                float sway = Mathf.Sin((piece.Age * piece.SwayFrequency) + piece.SwayPhase) * piece.SwayAmplitude;

                piece.Position.y -= piece.FallSpeed * dt;
                piece.Position.x += sway * dt;

                if (piece.Pattern == FallPattern.Spiral)
                {
                    float spiralX = Mathf.Cos(piece.Age * 7.5f + piece.SwayPhase) * 12f;
                    piece.Position.x += spiralX * dt;
                }

                piece.Rect.anchoredPosition = piece.Position;
                piece.CurrentRotationZ += piece.RotationSpeed * dt;
                piece.CurrentRotationY += piece.RotationYSpeed * dt;
                piece.Rect.localRotation = Quaternion.Euler(0f, piece.CurrentRotationY, piece.CurrentRotationZ);

                float visibleScale = Mathf.Lerp(piece.BaseScale, 0.82f * piece.BaseScale, tLife);
                piece.Rect.localScale = Vector3.one * visibleScale;

                Color c = piece.Image.color;
                c.a = piece.InitialAlpha * (1f - Mathf.SmoothStep(0.7f, 1f, tLife));
                piece.Image.color = c;

                if (piece.Age >= piece.Life || piece.Position.y <= bottomY)
                {
                    RemovePieceAt(i);
                }
            }
        }

        private void ApplyPieceVisual(Piece piece)
        {
            if (piece == null || piece.Rect == null || piece.Image == null)
            {
                return;
            }

            piece.Rect.anchoredPosition = piece.Position;
            piece.Rect.localRotation = Quaternion.Euler(0f, piece.CurrentRotationY, piece.CurrentRotationZ);
            piece.Rect.localScale = Vector3.one * piece.BaseScale;
        }

        private void RemovePieceAt(int index)
        {
            if (index < 0 || index >= activePieces.Count)
            {
                return;
            }

            Piece piece = activePieces[index];
            activePieces.RemoveAt(index);
            if (piece != null && piece.Rect != null)
            {
                Destroy(piece.Rect.gameObject);
            }
        }

        private void OnDisable()
        {
            ClearAllPieces();
            isPlaying = false;
            emitRemainingSeconds = 0f;
            spawnAccumulator = 0f;
            isSePending = false;
            seRemainingSeconds = 0f;
        }

        private void OnDestroy()
        {
            ClearAllPieces();
        }

        private void ClearAllPieces()
        {
            for (int i = activePieces.Count - 1; i >= 0; i--)
            {
                Piece piece = activePieces[i];
                if (piece != null && piece.Rect != null)
                {
                    Destroy(piece.Rect.gameObject);
                }
            }

            activePieces.Clear();
        }
    }
}
