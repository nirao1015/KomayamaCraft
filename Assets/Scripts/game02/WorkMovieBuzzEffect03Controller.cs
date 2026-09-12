using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バズ連動で画像を放物線投射する BuzzEffect03。
/// バズ中のみ有効化し、停止予約後は新規生成を止めて既存演出完了後に停止する。
/// </summary>
public sealed class WorkMovieBuzzEffect03Controller : MonoBehaviour
{
    private sealed class SpawnedPiece
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 StartPos;
        public float HorizontalDistance;
        public float ArcHeight;
        public float Duration;
        public float Elapsed;
        public float StartAlpha;
        public float EndFadeStartT;
    }

    [Header("Enable")]
    [SerializeField] private bool enableBuzzEffect03 = true;
    [SerializeField] private bool deactivateWhenNotBuzz = true;

    [Header("Source Images")]
    [SerializeField] private Sprite[] sourceSprites;

    [Header("Spawn Timing")]
    [SerializeField] private float startDelaySeconds = 0.5f;
    [SerializeField] private float spawnIntervalMinSeconds = 0.28f;
    [SerializeField] private float spawnIntervalMaxSeconds = 0.65f;
    [SerializeField, Range(0f, 1f)] private float twinSpawnChance = 0.35f;

    [Header("Throw")]
    [SerializeField] private float throwDistanceMin = 120f;
    [SerializeField] private float throwDistanceMax = 260f;
    [SerializeField] private float arcHeightMin = 36f;
    [SerializeField] private float arcHeightMax = 110f;
    [SerializeField] private float flyDurationMinSeconds = 0.65f;
    [SerializeField] private float flyDurationMaxSeconds = 1.1f;
    [SerializeField] private float pieceSizeMin = 36f;
    [SerializeField] private float pieceSizeMax = 72f;

    [Header("Visual")]
    [SerializeField, Range(0f, 1f)] private float pieceAlphaMin = 0.7f;
    [SerializeField, Range(0f, 1f)] private float pieceAlphaMax = 1f;
    [SerializeField] private Color[] brightPalette =
    {
        new Color32(255, 99, 132, 255),
        new Color32(255, 205, 86, 255),
        new Color32(54, 235, 162, 255),
        new Color32(75, 192, 192, 255),
        new Color32(102, 126, 234, 255),
        new Color32(255, 159, 64, 255),
        new Color32(234, 102, 255, 255)
    };

    [Header("Limit")]
    [SerializeField] private int maxSimultaneousCount = 5;

    [Header("Refs (Optional)")]
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private RectTransform spawnCenter;

    private readonly List<SpawnedPiece> activePieces = new List<SpawnedPiece>(8);
    private bool isBuzzActive;
    private bool isStopRequested;
    private float delayRemainingSeconds;
    private float spawnRemainingSeconds;
    private bool hasStartedAfterDelay;
    private Sprite fallbackSprite;

    private void Awake()
    {
        ResolveRefsIfNeeded();
        EnsureFallbackSpriteIfNeeded();
        ResetAllPiecesAndVisibility();
    }

    public void SetBuzzState(bool buzzActive, bool stopRequested)
    {
        ResolveRefsIfNeeded();
        bool nextActive = buzzActive && enableBuzzEffect03;
        bool wasActive = isBuzzActive;
        isBuzzActive = nextActive;
        isStopRequested = stopRequested;

        if (!isBuzzActive)
        {
            ResetAllPiecesAndVisibility();
            return;
        }

        SetTargetActive(true);
        if (!wasActive)
        {
            delayRemainingSeconds = Mathf.Max(0f, startDelaySeconds);
            spawnRemainingSeconds = 0f;
            hasStartedAfterDelay = false;
        }
    }

    public void ManualTick(float deltaSeconds)
    {
        if (!enableBuzzEffect03)
        {
            ResetAllPiecesAndVisibility();
            return;
        }

        float dt = Mathf.Max(0f, deltaSeconds);
        if (dt <= 0f)
        {
            return;
        }

        UpdateActivePieces(dt);

        if (!isBuzzActive)
        {
            return;
        }

        if (!hasStartedAfterDelay)
        {
            delayRemainingSeconds = Mathf.Max(0f, delayRemainingSeconds - dt);
            if (delayRemainingSeconds > 0f)
            {
                return;
            }

            hasStartedAfterDelay = true;
            spawnRemainingSeconds = 0f;
        }

        if (isStopRequested)
        {
            if (activePieces.Count == 0)
            {
                isBuzzActive = false;
                SetTargetActive(!deactivateWhenNotBuzz);
            }

            return;
        }

        spawnRemainingSeconds -= dt;
        if (spawnRemainingSeconds > 0f)
        {
            return;
        }

        SpawnOneOrTwoPieces();
        spawnRemainingSeconds = UnityEngine.Random.Range(
            Mathf.Min(spawnIntervalMinSeconds, spawnIntervalMaxSeconds),
            Mathf.Max(spawnIntervalMinSeconds, spawnIntervalMaxSeconds));
    }

    private void SpawnOneOrTwoPieces()
    {
        int capacity = Mathf.Max(1, maxSimultaneousCount) - activePieces.Count;
        if (capacity <= 0)
        {
            return;
        }

        int spawnCount = 1;
        if (capacity >= 2 && UnityEngine.Random.value < Mathf.Clamp01(twinSpawnChance))
        {
            spawnCount = 2;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            if (activePieces.Count >= Mathf.Max(1, maxSimultaneousCount))
            {
                break;
            }

            SpawnPiece();
        }
    }

    private void SpawnPiece()
    {
        ResolveRefsIfNeeded();
        if (targetRoot == null || spawnCenter == null)
        {
            return;
        }

        GameObject go = new GameObject("BuzzEffect03Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(targetRoot.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Image image = go.GetComponent<Image>();
        if (rect == null || image == null)
        {
            Destroy(go);
            return;
        }

        Sprite sprite = PickRandomSprite();
        image.sprite = sprite != null ? sprite : fallbackSprite;
        image.raycastTarget = false;

        float alpha = UnityEngine.Random.Range(
            Mathf.Clamp01(Mathf.Min(pieceAlphaMin, pieceAlphaMax)),
            Mathf.Clamp01(Mathf.Max(pieceAlphaMin, pieceAlphaMax)));
        Color color = PickRandomBrightColor();
        color.a = alpha;
        image.color = color;

        float size = UnityEngine.Random.Range(
            Mathf.Max(1f, Mathf.Min(pieceSizeMin, pieceSizeMax)),
            Mathf.Max(1f, Mathf.Max(pieceSizeMin, pieceSizeMax)));
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Vector2 startPos = spawnCenter.anchoredPosition;
        rect.anchoredPosition = startPos;
        rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-45f, 45f));

        float directionSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float distance = UnityEngine.Random.Range(
            Mathf.Max(1f, Mathf.Min(throwDistanceMin, throwDistanceMax)),
            Mathf.Max(1f, Mathf.Max(throwDistanceMin, throwDistanceMax)));
        float arcHeight = UnityEngine.Random.Range(
            Mathf.Max(1f, Mathf.Min(arcHeightMin, arcHeightMax)),
            Mathf.Max(1f, Mathf.Max(arcHeightMin, arcHeightMax)));
        float duration = UnityEngine.Random.Range(
            Mathf.Max(0.05f, Mathf.Min(flyDurationMinSeconds, flyDurationMaxSeconds)),
            Mathf.Max(0.05f, Mathf.Max(flyDurationMinSeconds, flyDurationMaxSeconds)));

        activePieces.Add(new SpawnedPiece
        {
            Rect = rect,
            Image = image,
            StartPos = startPos,
            HorizontalDistance = distance * directionSign,
            ArcHeight = arcHeight,
            Duration = duration,
            Elapsed = 0f,
            StartAlpha = alpha,
            EndFadeStartT = UnityEngine.Random.Range(0.6f, 0.82f)
        });
    }

    private void UpdateActivePieces(float dt)
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            SpawnedPiece piece = activePieces[i];
            if (piece == null || piece.Rect == null || piece.Image == null)
            {
                RemovePieceAt(i);
                continue;
            }

            piece.Elapsed += dt;
            float t = piece.Duration > 0.0001f ? Mathf.Clamp01(piece.Elapsed / piece.Duration) : 1f;
            float x = piece.StartPos.x + (piece.HorizontalDistance * t);
            float y = piece.StartPos.y + (4f * piece.ArcHeight * t * (1f - t));
            piece.Rect.anchoredPosition = new Vector2(x, y);
            piece.Rect.Rotate(0f, 0f, (piece.HorizontalDistance >= 0f ? 180f : -180f) * dt, Space.Self);

            float fadeT = Mathf.InverseLerp(piece.EndFadeStartT, 1f, t);
            Color c = piece.Image.color;
            c.a = piece.StartAlpha * (1f - Mathf.Clamp01(fadeT));
            piece.Image.color = c;

            if (t >= 1f)
            {
                RemovePieceAt(i);
            }
        }
    }

    private Sprite PickRandomSprite()
    {
        if (sourceSprites == null || sourceSprites.Length == 0)
        {
            return fallbackSprite;
        }

        int index = UnityEngine.Random.Range(0, sourceSprites.Length);
        Sprite sprite = sourceSprites[index];
        return sprite != null ? sprite : fallbackSprite;
    }

    private Color PickRandomBrightColor()
    {
        if (brightPalette == null || brightPalette.Length == 0)
        {
            return Color.white;
        }

        return brightPalette[UnityEngine.Random.Range(0, brightPalette.Length)];
    }

    private void RemovePieceAt(int index)
    {
        if (index < 0 || index >= activePieces.Count)
        {
            return;
        }

        SpawnedPiece piece = activePieces[index];
        activePieces.RemoveAt(index);
        if (piece != null && piece.Rect != null)
        {
            Destroy(piece.Rect.gameObject);
        }
    }

    private void ResetAllPiecesAndVisibility()
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            SpawnedPiece piece = activePieces[i];
            if (piece != null && piece.Rect != null)
            {
                Destroy(piece.Rect.gameObject);
            }
        }

        activePieces.Clear();
        isBuzzActive = false;
        isStopRequested = false;
        delayRemainingSeconds = 0f;
        spawnRemainingSeconds = 0f;
        hasStartedAfterDelay = false;
        SetTargetActive(!deactivateWhenNotBuzz);
    }

    private void ResolveRefsIfNeeded()
    {
        if (targetRoot == null)
        {
            targetRoot = gameObject;
        }

        if (spawnCenter == null)
        {
            spawnCenter = targetRoot.GetComponent<RectTransform>();
        }
    }

    private void SetTargetActive(bool active)
    {
        if (targetRoot == null)
        {
            return;
        }

        if (targetRoot.activeSelf != active)
        {
            targetRoot.SetActive(active);
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

    private void OnDisable()
    {
        ResetAllPiecesAndVisibility();
    }

    private void OnDestroy()
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            SpawnedPiece piece = activePieces[i];
            if (piece != null && piece.Rect != null)
            {
                Destroy(piece.Rect.gameObject);
            }
        }

        activePieces.Clear();
    }
}
