using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ExperiencePointsCanvas 配下の視聴者数ドロップ生成・統合・吸引・取得。
/// 吸い込み開始距離は <see cref="absorbEngageRadiusPixels"/> のみ。取得はユニット中心 ± <see cref="experienceCollectCenterTolerancePixels"/>。
/// </summary>
public class Game03ExperienceFieldController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField, Tooltip("経験値ドロップの親 RectTransform（ExperiencePointsCanvas）。未設定時はこのコンポーネントの RectTransform。")]
    private RectTransform experienceRoot;
    [SerializeField, Tooltip("生成する ExperiencePrefab（ルートに RectTransform と Game03ExperiencePickup が必要）。")]
    private GameObject experiencePrefab;
    [SerializeField, Tooltip("ユニット中心・フィールドスクロール変換に使用。")]
    private Game03UnitManager unitManager;
    [SerializeField, Tooltip("取得時の経験値加算・ドロップ量計算に使用。")]
    private Game03StatusManager statusManager;
    [SerializeField, Tooltip("ゲームプレイ時間・ポーズ判定。未設定時は Time.deltaTime を使用。")]
    private Game03Manager game03Manager;

    [Header("生成")]
    [SerializeField, Min(0f), Tooltip("敵位置からスポーンするとき、錨座標に足すランダム散らばり（px）。")]
    private float scatterPixels = 10f;
    [SerializeField, Min(0f), Tooltip("スポーン直後、この秒数は吸引・取得判定を行わない。")]
    private float pickupDelaySeconds = 0.12f;

    [Header("吸引・取得")]
    [SerializeField, Min(1f), Tooltip("ユニット中心からこの距離（experienceRoot ローカル px）以内のドロップが、浮き→ホーミングの吸い込みを開始する。距離判定はこの値のみ。")]
    private float absorbEngageRadiusPixels = 72f;
    [SerializeField, Min(0f), Tooltip("吸い込み開始時、一瞬上にずらす量（px）。")]
    private float absorbFloatUpPixels = 16f;
    [SerializeField, Min(0.02f), Tooltip("上方向へ浮かせる演出の長さ（秒）。")]
    private float absorbFloatDurationSeconds = 0.1f;
    [SerializeField, Min(0f), Tooltip("取得判定: ドロップ位置とユニット中心の差が X・Y それぞれこの px 以内で経験値を加算。")]
    private float experienceCollectCenterTolerancePixels = 2f;
    [SerializeField, Min(1f), Tooltip("ホーミング移動の上限速度（px/s）。等速・減速モードの基準、加速モードの上限。")]
    private float absorbHomingSpeedPixelsPerSecond = 720f;
    [SerializeField, Tooltip("ユニットへ寄るときの速度の付け方（等速／加速／手前で減速）。")]
    private Game03ExpHomingMoveStyle absorbHomingMoveStyle = Game03ExpHomingMoveStyle.ConstantSpeed;
    [SerializeField, Min(0.0001f), Tooltip("移動スタイルが AccelerateAlongPath のとき、1秒あたり増える速度（px/s² 相当）。")]
    private float absorbHomingAccelerationPixelsPerSecondSq = 2400f;
    [SerializeField, Min(8f), Tooltip("移動スタイルが SlowNearCore のとき、取得許容の外側からこの幅（px）で減速をかける。")]
    private float absorbHomingNearSoftPixels = 48f;

    [Header("ティア見た目")]
    [SerializeField] private Game03ExpTierPresentation tierGreen = new Game03ExpTierPresentation
    {
        color = new Color(0.25f, 0.95f, 0.35f, 1f)
    };
    [SerializeField] private Game03ExpTierPresentation tierYellow = new Game03ExpTierPresentation
    {
        color = new Color(1f, 0.92f, 0.2f, 1f)
    };
    [SerializeField] private Game03ExpTierPresentation tierRed = new Game03ExpTierPresentation
    {
        color = new Color(1f, 0.28f, 0.28f, 1f)
    };
    [SerializeField] private Game03ExpTierPresentation tierPurple = new Game03ExpTierPresentation
    {
        color = new Color(0.78f, 0.42f, 1f, 1f)
    };

    [Header("マージ")]
    [SerializeField, Min(0.05f), Tooltip("同ティア同士をまとめる判定の間隔（秒）。")]
    private float mergeIntervalSeconds = 0.25f;
    [SerializeField, Min(1f), Tooltip("この距離（px）以内の同ティアドロップを1つに合算（合算後は黄色表示）。")]
    private float mergeDistancePixels = 52f;

    private readonly List<Game03ExperiencePickup> pickups = new List<Game03ExperiencePickup>(128);
    private float mergeTimer;

    private void Awake()
    {
        if (experienceRoot == null)
        {
            experienceRoot = transform as RectTransform;
        }
    }

    private void Update()
    {
        if (experienceRoot == null || unitManager == null || statusManager == null)
        {
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        float dt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;

        ApplyFieldScrollToPickups();

        if (!unitManager.TryGetUnitCenterOnRect(experienceRoot, out Vector2 playerLocal))
        {
            return;
        }

        mergeTimer += dt;
        if (mergeTimer >= Mathf.Max(0.05f, mergeIntervalSeconds))
        {
            mergeTimer = 0f;
            MergePickups();
        }

        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            Game03ExperiencePickup pickup = pickups[i];
            if (pickup == null)
            {
                pickups.RemoveAt(i);
                continue;
            }

            pickup.Tick(dt, playerLocal);
        }
    }

    /// <summary>経験値取得: ユニット中心との差が X/Y それぞれこの px 以内なら取得。</summary>
    public float ExperienceCollectCenterTolerancePixels => Mathf.Max(0f, experienceCollectCenterTolerancePixels);

    /// <summary>ドロップ錨座標 − ユニット中心（experienceRoot ローカル）が軸別 ±tolerance 以内か。</summary>
    public static bool IsExperienceCollectAtCenterTolerance(Vector2 pickupAnchoredMinusPlayerCenter, float tolerancePixels)
    {
        float t = Mathf.Max(0f, tolerancePixels);
        return Mathf.Abs(pickupAnchoredMinusPlayerCenter.x) <= t && Mathf.Abs(pickupAnchoredMinusPlayerCenter.y) <= t;
    }

    public float AbsorbEngageRadiusPixels => absorbEngageRadiusPixels;
    public float AbsorbFloatUpPixels => absorbFloatUpPixels;
    public float AbsorbFloatDurationSeconds => absorbFloatDurationSeconds;
    public float AbsorbHomingSpeedPixelsPerSecond => absorbHomingSpeedPixelsPerSecond;
    public Game03ExpHomingMoveStyle AbsorbHomingMoveStyle => absorbHomingMoveStyle;
    public float AbsorbHomingAccelerationPixelsPerSecondSq => absorbHomingAccelerationPixelsPerSecondSq;
    public float AbsorbHomingNearSoftPixels => absorbHomingNearSoftPixels;

    /// <summary>
    /// sprite が null のときはプレハブ既定のスプライトを維持する。
    /// </summary>
    public void GetPresentationForTier(Game03ExpTier tier, out Color color, out Sprite spriteOverride)
    {
        Game03ExpTierPresentation p = tier switch
        {
            Game03ExpTier.Green => tierGreen,
            Game03ExpTier.Yellow => tierYellow,
            Game03ExpTier.Red => tierRed,
            Game03ExpTier.Purple => tierPurple,
            _ => tierRed
        };
        color = p.color;
        spriteOverride = p.sprite;
    }

    /// <summary>
    /// 敵など別 Canvas 上の UI の位置に合わせてドロップする（スクリーン座標で変換）。
    /// </summary>
    public void SpawnPickup(RectTransform sourceUiRect, int amount, Game03ExpTier tier)
    {
        if (experienceRoot == null || experiencePrefab == null || statusManager == null || sourceUiRect == null)
        {
            return;
        }

        Camera cam = GetUiEventCamera(experienceRoot);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, sourceUiRect.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(experienceRoot, screen, cam, out Vector2 localPos))
        {
            localPos = experienceRoot.InverseTransformPoint(sourceUiRect.position);
        }

        Vector2 jitter = new Vector2(Random.Range(-scatterPixels, scatterPixels), Random.Range(-scatterPixels, scatterPixels));
        SpawnPickupAtLocal(localPos + jitter, amount, tier);
    }

    /// <summary>
    /// experienceRoot ローカル座標での生成（デバッグ用クラスタなど）。
    /// </summary>
    public void SpawnPickupAtLocal(Vector2 anchoredPositionInExperienceRoot, int amount, Game03ExpTier tier)
    {
        if (experienceRoot == null || experiencePrefab == null || statusManager == null)
        {
            return;
        }

        Vector2 clampedSpawnPosition = ClampToReachableVerticalRange(anchoredPositionInExperienceRoot);

        GameObject instanceGo = Instantiate(experiencePrefab, experienceRoot, false);
        RectTransform instance = instanceGo.GetComponent<RectTransform>();
        if (instance == null)
        {
            Destroy(instanceGo);
            return;
        }

        instance.gameObject.SetActive(true);
        Game03ExperiencePickup pickup = instance.GetComponent<Game03ExperiencePickup>();
        if (pickup == null)
        {
            Destroy(instance.gameObject);
            return;
        }

        pickup.Initialize(this, clampedSpawnPosition, amount, tier, pickupDelaySeconds);
        pickups.Add(pickup);
    }

    /// <summary>
    /// デバッグ用: メインユニット(UnitOj)中心を experienceRoot 上で取得し、右へオフセットした点を中心に円内ランダムで低ティア（緑）を複数生成する。
    /// </summary>
    public void DebugSpawnLowTierPickupsCluster(int count, float offsetRightPixels, float radiusPixels)
    {
        if (experienceRoot == null || experiencePrefab == null || statusManager == null || unitManager == null)
        {
            return;
        }

        if (!unitManager.TryGetUnitCenterOnRect(experienceRoot, out Vector2 unitCenterLocal))
        {
            return;
        }

        Vector2 clusterCenter = unitCenterLocal + Vector2.right * offsetRightPixels;
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.Range(0f, 1f)) * Mathf.Max(0f, radiusPixels);
            Vector2 pos = clusterCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            int amount = Mathf.Max(1, statusManager.ComputeExperienceDropAmount(Game03ExpTier.Green));
            SpawnPickupAtLocal(pos, amount, Game03ExpTier.Green);
        }
    }

    private void ApplyFieldScrollToPickups()
    {
        Vector2 worldDelta = unitManager.LastAppliedFieldDeltaWorld;
        if (worldDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (!TryConvertWorldDeltaToLocalWithFallback(worldDelta, out Vector2 localDelta))
        {
            return;
        }

        for (int i = 0; i < pickups.Count; i++)
        {
            Game03ExperiencePickup pickup = pickups[i];
            if (pickup?.RectTransform != null)
            {
                pickup.RectTransform.anchoredPosition += localDelta;
            }
        }
    }

    private static Camera GetUiEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    /// <summary>
    /// ワールド座標を経験値キャンバス上の到達可能な縦帯へ寄せる（エマージェンシーカーゴ投下位置の Y 制限に使用）。
    /// </summary>
    public Vector3 AdjustWorldPositionByReachableVerticalClamp(Vector3 worldPosition, Camera worldCameraForPoint)
    {
        if (experienceRoot == null)
        {
            return worldPosition;
        }

        Camera wc = worldCameraForPoint != null ? worldCameraForPoint : Camera.main;
        if (wc == null)
        {
            return worldPosition;
        }

        Camera eventCamera = GetUiEventCamera(experienceRoot);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(wc, worldPosition);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(experienceRoot, screen, eventCamera, out Vector2 local))
        {
            return worldPosition;
        }

        Vector2 clamped = ClampToReachableVerticalRange(local);
        Vector3 w0 = experienceRoot.TransformPoint(new Vector3(local.x, local.y, 0f));
        Vector3 w1 = experienceRoot.TransformPoint(new Vector3(clamped.x, clamped.y, 0f));
        return worldPosition + (w1 - w0);
    }

    /// <summary>
    /// 爆弾アイテム: 撃破で湧いたドロップを含む全ドロップに、距離に関係なく吸い込み（浮き→ホーミング）を開始する。即時加算はしない。
    /// </summary>
    public void BeginAbsorbSequenceForAllPickupsFromBombItem()
    {
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            Game03ExperiencePickup pickup = pickups[i];
            if (pickup != null)
            {
                pickup.ForceBeginAbsorbSequenceFromBombItem();
            }
        }
    }

    private Vector2 ClampToReachableVerticalRange(Vector2 localPosition)
    {
        return ClampReachableVerticalOnCanvas(experienceRoot, localPosition);
    }

    /// <summary>経験値ドロップと同様、ユニットが縦移動で到達できる Y 帯へ寄せる（任意 Canvas ローカル）。</summary>
    public Vector2 ClampReachableVerticalOnCanvas(RectTransform canvas, Vector2 localPosition)
    {
        if (canvas == null || unitManager == null)
        {
            return localPosition;
        }

        if (!unitManager.TryGetUnitCenterOnRect(canvas, out Vector2 unitCenterLocal))
        {
            return localPosition;
        }

        float currentFieldY = unitManager.CurrentFieldRootY;
        float movableWorldUp = Mathf.Max(0f, unitManager.MaxFieldRootY - currentFieldY);
        float movableWorldDown = Mathf.Max(0f, currentFieldY - unitManager.MinFieldRootY);

        if (!TryConvertWorldDeltaToLocalWithFallback(canvas, new Vector2(0f, movableWorldUp), out Vector2 upLocalDelta))
        {
            return localPosition;
        }

        if (!TryConvertWorldDeltaToLocalWithFallback(canvas, new Vector2(0f, -movableWorldDown), out Vector2 downLocalDelta))
        {
            return localPosition;
        }

        float minReachableY = unitCenterLocal.y + Mathf.Min(upLocalDelta.y, downLocalDelta.y);
        float maxReachableY = unitCenterLocal.y + Mathf.Max(upLocalDelta.y, downLocalDelta.y);
        float minClampY = Mathf.Max(minReachableY, canvas.rect.yMin);
        float maxClampY = Mathf.Min(maxReachableY, canvas.rect.yMax);
        if (minClampY > maxClampY)
        {
            float mid = (minClampY + maxClampY) * 0.5f;
            minClampY = mid;
            maxClampY = mid;
        }

        localPosition.y = Mathf.Clamp(localPosition.y, minClampY, maxClampY);
        return localPosition;
    }

    private bool TryConvertWorldDeltaToLocalWithFallback(Vector2 worldDelta, out Vector2 localDelta)
    {
        return TryConvertWorldDeltaToLocalWithFallback(experienceRoot, worldDelta, out localDelta);
    }

    private bool TryConvertWorldDeltaToLocalWithFallback(RectTransform canvas, Vector2 worldDelta, out Vector2 localDelta)
    {
        localDelta = Vector2.zero;
        if (canvas == null || unitManager == null)
        {
            return false;
        }

        if (unitManager.TryConvertWorldDeltaToLocalOnRect(canvas, worldDelta, out localDelta))
        {
            return true;
        }

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return false;
        }

        Camera eventCamera = GetUiEventCamera(canvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, Vector2.zero, eventCamera, out Vector2 p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, new Vector2(Screen.width, Screen.height), eventCamera, out Vector2 p1);
        float localHeight = Mathf.Abs(p1.y - p0.y);
        float worldHeight = cam.orthographicSize * 2f;
        if (localHeight <= 0.0001f || worldHeight <= 0.0001f)
        {
            return false;
        }

        float localUnitsPerWorld = localHeight / worldHeight;
        localDelta = worldDelta * localUnitsPerWorld;
        return true;
    }

    public void CollectPickup(Game03ExperiencePickup pickup)
    {
        if (pickup == null || statusManager == null)
        {
            return;
        }

        statusManager.AddExperience(pickup.ExperienceValue);
        pickups.Remove(pickup);
        Destroy(pickup.gameObject);
    }

    private void MergePickups()
    {
        float mergeSqr = mergeDistancePixels * mergeDistancePixels;
        for (int i = 0; i < pickups.Count; i++)
        {
            Game03ExperiencePickup a = pickups[i];
            if (a == null)
            {
                continue;
            }

            for (int j = pickups.Count - 1; j > i; j--)
            {
                Game03ExperiencePickup b = pickups[j];
                if (b == null)
                {
                    pickups.RemoveAt(j);
                    continue;
                }

                if (!CanMerge(a.Tier, b.Tier))
                {
                    continue;
                }

                Vector2 delta = a.RectTransform.anchoredPosition - b.RectTransform.anchoredPosition;
                if (delta.sqrMagnitude > mergeSqr)
                {
                    continue;
                }

                a.AddExperienceFromMerge(b.ExperienceValue, b.CurrentAbsorbPhase);
                pickups.RemoveAt(j);
                Destroy(b.gameObject);
            }
        }

        pickups.RemoveAll(static p => p == null);
    }

    private static bool CanMerge(Game03ExpTier a, Game03ExpTier b)
    {
        return a == b;
    }
}
