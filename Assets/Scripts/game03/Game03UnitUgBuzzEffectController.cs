using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UnitUG 抽選演出用。game02 の <see cref="WorkMovieBuzzEffect03Controller"/> を参考にした game03 専用実装。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03UnitUgBuzzEffectController : MonoBehaviour
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
        public Color BaseColor;
    }

    [Header("Refs")]
    [SerializeField] private RectTransform spawnCenter;
    [SerializeField] private RectTransform pieceParent;

    [Header("にぎやかし（何度でも再利用）")]
    [SerializeField] private Sprite[] fillerSprites = System.Array.Empty<Sprite>();

    [Header("Spawn Timing")]
    [SerializeField] private float spawnIntervalMinSeconds = 0.22f;
    [SerializeField] private float spawnIntervalMaxSeconds = 0.48f;
    [SerializeField, Range(0f, 1f)] private float twinSpawnChance = 0.4f;

    [Header("Throw")]
    [SerializeField] private float throwDistanceMin = 120f;
    [SerializeField] private float throwDistanceMax = 280f;
    [SerializeField] private float arcHeightMin = 40f;
    [SerializeField] private float arcHeightMax = 120f;
    [SerializeField] private float flyDurationMinSeconds = 0.55f;
    [SerializeField] private float flyDurationMaxSeconds = 1.05f;
    [SerializeField] private float pieceSizeMin = 40f;
    [SerializeField] private float pieceSizeMax = 76f;

    [Header("Visual")]
    [SerializeField, Range(0f, 1f)] private float pieceAlphaMin = 0.75f;
    [SerializeField, Range(0f, 1f)] private float pieceAlphaMax = 1f;
    [SerializeField] private bool tintWithBrightPalette = true;
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
    [SerializeField] private int maxSimultaneousCount = 8;

    private readonly List<SpawnedPiece> activePieces = new List<SpawnedPiece>(12);
    private readonly List<Sprite> lotterySpritePool = new List<Sprite>(16);

    private bool isRunning;
    private bool stopSpawnRequested;
    private float spawnRemainingSeconds;
    private Sprite fallbackSprite;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        ResolveRefs();
        EnsureFallbackSprite();
    }

    public void SetSpawnCenterAnchoredPosition(Vector2 anchoredPosition)
    {
        ResolveRefs();
        if (spawnCenter != null)
        {
            spawnCenter.anchoredPosition = anchoredPosition;
        }
    }

    /// <summary>
    /// ピースの親を指定。center が null のとき spawnCenter は Inspector の配置のまま使う。
    /// </summary>
    public void ConfigureSpawnRects(RectTransform center, RectTransform parentForPieces)
    {
        if (parentForPieces != null)
        {
            pieceParent = parentForPieces;
        }

        if (center != null)
        {
            spawnCenter = center;
        }

        ResolveRefs();
    }

    public void BeginLottery(IReadOnlyList<Sprite> loserCandidateSprites, float durationSeconds)
    {
        ResolveRefs();
        BuildLotteryPool(loserCandidateSprites);
        stopSpawnRequested = false;
        isRunning = true;
        spawnRemainingSeconds = 0f;
        gameObject.SetActive(true);

        float duration = Mathf.Max(0.05f, durationSeconds);
        StopAllCoroutines();
        StartCoroutine(StopSpawningAfterSeconds(duration));
    }

    public void RequestStopSpawning()
    {
        stopSpawnRequested = true;
    }

    public void StopImmediate()
    {
        stopSpawnRequested = true;
        isRunning = false;
        StopAllCoroutines();
        ClearAllPieces();
    }

    public void ManualTick(float deltaSeconds)
    {
        float dt = Mathf.Max(0f, deltaSeconds);
        if (dt <= 0f)
        {
            return;
        }

        UpdateActivePieces(dt);

        if (!isRunning || stopSpawnRequested)
        {
            if (stopSpawnRequested && activePieces.Count == 0)
            {
                isRunning = false;
            }

            return;
        }

        spawnRemainingSeconds -= dt;
        if (spawnRemainingSeconds > 0f)
        {
            return;
        }

        SpawnOneOrTwoPieces();
        spawnRemainingSeconds = Random.Range(
            Mathf.Min(spawnIntervalMinSeconds, spawnIntervalMaxSeconds),
            Mathf.Max(spawnIntervalMinSeconds, spawnIntervalMaxSeconds));
    }

    private System.Collections.IEnumerator StopSpawningAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        RequestStopSpawning();
    }

    private void BuildLotteryPool(IReadOnlyList<Sprite> loserCandidateSprites)
    {
        lotterySpritePool.Clear();
        if (loserCandidateSprites != null)
        {
            for (int i = 0; i < loserCandidateSprites.Count; i++)
            {
                Sprite s = loserCandidateSprites[i];
                if (s != null)
                {
                    lotterySpritePool.Add(s);
                }
            }
        }

        if (fillerSprites != null)
        {
            for (int i = 0; i < fillerSprites.Length; i++)
            {
                Sprite s = fillerSprites[i];
                if (s != null)
                {
                    lotterySpritePool.Add(s);
                }
            }
        }

        EnsureFallbackSprite();
        if (lotterySpritePool.Count == 0 && fallbackSprite != null)
        {
            lotterySpritePool.Add(fallbackSprite);
        }
    }

    private void SpawnOneOrTwoPieces()
    {
        if (lotterySpritePool.Count == 0)
        {
            return;
        }

        int capacity = Mathf.Max(1, maxSimultaneousCount) - activePieces.Count;
        if (capacity <= 0)
        {
            return;
        }

        int spawnCount = capacity >= 2 && Random.value < twinSpawnChance ? 2 : 1;
        for (int i = 0; i < spawnCount; i++)
        {
            if (activePieces.Count >= maxSimultaneousCount)
            {
                break;
            }

            SpawnPiece();
        }
    }

    private void SpawnPiece()
    {
        ResolveRefs();
        if (pieceParent == null || spawnCenter == null)
        {
            return;
        }

        GameObject go = new GameObject("UnitUgBuzzPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(pieceParent, false);
        go.transform.SetAsLastSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        Image image = go.GetComponent<Image>();
        if (rect == null || image == null)
        {
            Destroy(go);
            return;
        }

        Sprite sprite = lotterySpritePool[Random.Range(0, lotterySpritePool.Count)];
        image.sprite = sprite != null ? sprite : fallbackSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        float alpha = Random.Range(
            Mathf.Clamp01(Mathf.Min(pieceAlphaMin, pieceAlphaMax)),
            Mathf.Clamp01(Mathf.Max(pieceAlphaMin, pieceAlphaMax)));
        Color color = tintWithBrightPalette ? PickRandomBrightColor() : Color.white;
        color.a = alpha;
        image.color = color;

        float size = Random.Range(
            Mathf.Max(1f, Mathf.Min(pieceSizeMin, pieceSizeMax)),
            Mathf.Max(1f, Mathf.Max(pieceSizeMin, pieceSizeMax)));
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Vector2 startPos = ResolveSpawnStartInPieceParentSpace();
        rect.anchoredPosition = startPos;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f));

        float directionSign = Random.value < 0.5f ? -1f : 1f;
        float distance = Random.Range(
            Mathf.Max(1f, Mathf.Min(throwDistanceMin, throwDistanceMax)),
            Mathf.Max(1f, Mathf.Max(throwDistanceMin, throwDistanceMax)));
        float arcHeight = Random.Range(
            Mathf.Max(1f, Mathf.Min(arcHeightMin, arcHeightMax)),
            Mathf.Max(1f, Mathf.Max(arcHeightMin, arcHeightMax)));
        float duration = Random.Range(
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
            EndFadeStartT = Random.Range(0.58f, 0.82f),
            BaseColor = color,
        });
    }

    private void UpdateActivePieces(float dt)
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            SpawnedPiece piece = activePieces[i];
            if (piece?.Rect == null || piece.Image == null)
            {
                RemovePieceAt(i);
                continue;
            }

            piece.Elapsed += dt;
            float t = piece.Duration > 0.0001f ? Mathf.Clamp01(piece.Elapsed / piece.Duration) : 1f;
            float x = piece.StartPos.x + piece.HorizontalDistance * t;
            float y = piece.StartPos.y + 4f * piece.ArcHeight * t * (1f - t);
            piece.Rect.anchoredPosition = new Vector2(x, y);
            piece.Rect.Rotate(0f, 0f, (piece.HorizontalDistance >= 0f ? 180f : -180f) * dt, Space.Self);

            float fadeT = Mathf.InverseLerp(piece.EndFadeStartT, 1f, t);
            Color c = piece.BaseColor;
            c.a = piece.StartAlpha * (1f - Mathf.Clamp01(fadeT));
            piece.Image.color = c;

            if (t >= 1f)
            {
                RemovePieceAt(i);
            }
        }
    }

    private void RemovePieceAt(int index)
    {
        if (index < 0 || index >= activePieces.Count)
        {
            return;
        }

        SpawnedPiece piece = activePieces[index];
        activePieces.RemoveAt(index);
        if (piece?.Rect != null)
        {
            Destroy(piece.Rect.gameObject);
        }
    }

    private void ClearAllPieces()
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            RemovePieceAt(i);
        }

        activePieces.Clear();
    }

    private Color PickRandomBrightColor()
    {
        if (!tintWithBrightPalette || brightPalette == null || brightPalette.Length == 0)
        {
            return Color.white;
        }

        return brightPalette[Random.Range(0, brightPalette.Length)];
    }

    private Vector2 ResolveSpawnStartInPieceParentSpace()
    {
        if (spawnCenter == null)
        {
            return Vector2.zero;
        }

        if (pieceParent == null || pieceParent == spawnCenter)
        {
            return spawnCenter.anchoredPosition;
        }

        Vector3 world = spawnCenter.TransformPoint(Vector3.zero);
        Vector2 local = pieceParent.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private void ResolveRefs()
    {
        if (spawnCenter == null)
        {
            spawnCenter = transform as RectTransform;
        }

        if (pieceParent == null || pieceParent == spawnCenter)
        {
            RectTransform effectRoot = transform.parent as RectTransform;
            pieceParent = effectRoot != null ? effectRoot : spawnCenter;
        }
    }

    private void EnsureFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return;
        }

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDisable()
    {
        StopImmediate();
    }
}
