using UnityEngine;

/// <summary>
/// エマージェンシーカーゴ（ItemCanvas.ItemCargo）の投下間隔・落下演出・着弾後の抽選とアイテム生成（仕様 ■5）。
/// 演出（警告マーカー等）は仕様上未実装。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03EmergencyCargoDropController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Game03FieldItemCoordinator fieldItemCoordinator;
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03UnitManager unitManager;
    [SerializeField] private Transform fieldRoot;
    [SerializeField] private RectTransform itemCargoRect;
    [SerializeField] private Game03ExperienceFieldController experienceFieldController;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Game03SeManager game03SeManager;

    [Header("Schedule (gameplay time)")]
    [SerializeField, Min(0.1f)] private float firstDropAfterGameplaySeconds = 20f;
    [SerializeField, Min(0.1f)] private float repeatDropIntervalGameplaySeconds = 70f;

    [Header("Drop destination (world, vs main unit)")]
    [SerializeField, Min(0.1f), Tooltip("着弾点をメインユニットの右または左に、このワールド距離だけずらす。")]
    private float landingSideOffsetWorld = 6f;
    [SerializeField, Min(0f)] private float landingVerticalJitterWorld = 1.5f;
    [SerializeField, Min(0f), Tooltip("カメラ pixelRect の上端よりこのピクセルだけ外側を発射 Y とする（プレイヤー画面上の真上・ちょうど画面外）。")]
    private float viewportTopOutsidePixels = 8f;
    [SerializeField, Min(0.5f), Tooltip("カメラが無い等でビューポート計算できないときの、プレイヤーより上のワールド距離フォールバック。")]
    private float fallStartAbovePlayerFallbackWorld = 12f;

    [Header("Fall motion")]
    [SerializeField, Min(0.05f)] private float fallDurationGameplaySeconds = 1.25f;

    private Transform cargoWorldAnchor;
    private float gameplayTimer;
    private bool hasCompletedFirstDrop;
    private bool isFalling;
    private float fallElapsed;

    private Vector3 cargoFallBeginPlayerWorld;
    private float cargoLandingSideSign;
    private float cargoLandingVerticalBiasWorld;
    private float fallStartWorldY;

    private void Awake()
    {
        EnsureCargoAnchor();
        if (itemCargoRect != null)
        {
            itemCargoRect.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (fieldItemCoordinator == null || unitManager == null || fieldRoot == null || itemCargoRect == null)
        {
            return;
        }

        if (isFalling)
        {
            float fallDt = game03Manager != null && game03Manager.CanRunGameplay
                ? game03Manager.GameplayDeltaTime
                : Time.unscaledDeltaTime;
            TickFall(fallDt);
            return;
        }

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            return;
        }

        float dt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
        gameplayTimer += dt;
        float threshold = hasCompletedFirstDrop ? repeatDropIntervalGameplaySeconds : firstDropAfterGameplaySeconds;
        if (gameplayTimer >= threshold)
        {
            BeginCargoDrop();
        }
    }

    private void EnsureCargoAnchor()
    {
        if (cargoWorldAnchor != null || fieldRoot == null)
        {
            return;
        }

        GameObject go = new GameObject("CargoDropWorldAnchor");
        go.transform.SetParent(fieldRoot, false);
        cargoWorldAnchor = go.transform;
    }

    private Vector3 ClampCargoLandingWorld(Vector3 candidateDropWorld, Camera cam)
    {
        Vector3 dropWorld = candidateDropWorld;
        if (experienceFieldController != null && cam != null)
        {
            dropWorld = experienceFieldController.AdjustWorldPositionByReachableVerticalClamp(dropWorld, cam);
        }

        Vector3 localInField = fieldRoot.InverseTransformPoint(dropWorld);
        localInField.y = Mathf.Clamp(localInField.y, unitManager.MinFieldRootY, unitManager.MaxFieldRootY);
        return fieldRoot.TransformPoint(localInField);
    }

    private void BeginCargoDrop()
    {
        EnsureCargoAnchor();
        if (cargoWorldAnchor == null || unitManager.MainUnitRect == null)
        {
            return;
        }

        Vector3 playerWorld = unitManager.MainUnitRect.position;
        cargoFallBeginPlayerWorld = playerWorld;
        cargoLandingSideSign = Random.value < 0.5f ? -1f : 1f;
        cargoLandingVerticalBiasWorld = landingVerticalJitterWorld > 0f
            ? Random.Range(-landingVerticalJitterWorld, landingVerticalJitterWorld)
            : 0f;

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;

        Vector3 initialLandingCandidate = new Vector3(
            playerWorld.x + cargoLandingSideSign * landingSideOffsetWorld,
            playerWorld.y + cargoLandingVerticalBiasWorld,
            playerWorld.z);
        Vector3 initialLanding = ClampCargoLandingWorld(initialLandingCandidate, cam);

        if (!TryComputeFallStartWorldYJustOutsideViewportTop(cam, playerWorld, viewportTopOutsidePixels, out fallStartWorldY))
        {
            fallStartWorldY = playerWorld.y + fallStartAbovePlayerFallbackWorld;
        }

        float minGapAboveLanding = 0.75f;
        if (fallStartWorldY < initialLanding.y + minGapAboveLanding)
        {
            fallStartWorldY = initialLanding.y + Mathf.Max(minGapAboveLanding, fallStartAbovePlayerFallbackWorld * 0.35f);
        }

        RectTransform parentRt = itemCargoRect.parent as RectTransform;
        if (parentRt == null)
        {
            gameplayTimer = 0f;
            return;
        }

        Vector3 startWorld = new Vector3(cargoFallBeginPlayerWorld.x, fallStartWorldY, cargoFallBeginPlayerWorld.z);
        cargoWorldAnchor.position = startWorld;
        if (!TryComputeAnchoredForWorld(parentRt, cargoWorldAnchor.position, out Vector2 startAnchored))
        {
            gameplayTimer = 0f;
            return;
        }

        itemCargoRect.anchoredPosition = startAnchored;
        itemCargoRect.gameObject.SetActive(true);
        fallElapsed = 0f;
        isFalling = true;

        Game03SeManager se = game03SeManager != null ? game03SeManager : Game03SeManager.TryGet();
        se?.PlayEmergencyCargoFallStartSe();
    }

    /// <summary>
    /// プレイヤーと画面上で横位置が一致するスクリーン点を、ビューポート上端のすぐ外側に置いたときのワールド Y を返す。
    /// </summary>
    private static bool TryComputeFallStartWorldYJustOutsideViewportTop(
        Camera cam,
        Vector3 playerWorld,
        float outsidePixels,
        out float worldY)
    {
        worldY = playerWorld.y;
        if (cam == null)
        {
            return false;
        }

        Vector3 playerScreen = RectTransformUtility.WorldToScreenPoint(cam, playerWorld);
        float top = cam.pixelRect.yMax;
        Vector3 screenAbove = new Vector3(playerScreen.x, top + Mathf.Max(0f, outsidePixels), playerScreen.z);
        Vector3 worldAbove = cam.ScreenToWorldPoint(screenAbove);
        worldY = worldAbove.y;
        return true;
    }

    private bool TryComputeAnchoredForWorld(RectTransform parentRect, Vector3 world, out Vector2 anchored)
    {
        anchored = Vector2.zero;
        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, eventCamera, out anchored);
    }

    private void TickFall(float dt)
    {
        RectTransform parentRt = itemCargoRect.parent as RectTransform;
        if (parentRt == null || cargoWorldAnchor == null || unitManager.MainUnitRect == null)
        {
            return;
        }

        Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;

        Vector3 playerWorld = unitManager.MainUnitRect.position;

        Vector3 landingCandidate = new Vector3(
            playerWorld.x + cargoLandingSideSign * landingSideOffsetWorld,
            playerWorld.y + cargoLandingVerticalBiasWorld,
            playerWorld.z);
        Vector3 landingTarget = ClampCargoLandingWorld(landingCandidate, cam);

        float dur = Mathf.Max(0.05f, fallDurationGameplaySeconds);
        fallElapsed += dt;
        float t = Mathf.Clamp01(fallElapsed / dur);
        float eased = t * t * (3f - 2f * t);

        Vector3 startWorld = new Vector3(cargoFallBeginPlayerWorld.x, fallStartWorldY, cargoFallBeginPlayerWorld.z);
        cargoWorldAnchor.position = Vector3.Lerp(startWorld, landingTarget, eased);

        if (!TryComputeAnchoredForWorld(parentRt, cargoWorldAnchor.position, out Vector2 anchored))
        {
            return;
        }

        itemCargoRect.anchoredPosition = anchored;
        if (t >= 1f)
        {
            Vector3 finalLandingCandidate = new Vector3(
                playerWorld.x + cargoLandingSideSign * landingSideOffsetWorld,
                playerWorld.y + cargoLandingVerticalBiasWorld,
                playerWorld.z);
            cargoWorldAnchor.position = ClampCargoLandingWorld(finalLandingCandidate, cam);
            CompleteCargoLanding();
        }
    }

    private void CompleteCargoLanding()
    {
        isFalling = false;
        itemCargoRect.gameObject.SetActive(false);
        hasCompletedFirstDrop = true;
        gameplayTimer = 0f;

        Game03CargoItemKind kind = Game03CargoLottery.DrawCargoContents();
        fieldItemCoordinator.TrySpawnCargoFieldItem(cargoWorldAnchor, kind);
    }

    /// <summary>
    /// デバッグ用: 間隔を待たずに 1 回カーゴ落下〜着弾と同じ処理を開始する（落下中は無視）。
    /// </summary>
    public void DebugRequestCargoDropNow()
    {
        if (fieldItemCoordinator == null || unitManager == null || fieldRoot == null || itemCargoRect == null)
        {
            Debug.LogWarning("[Game03EmergencyCargoDropController] Debug cargo: missing references.");
            return;
        }

        if (isFalling)
        {
            Debug.LogWarning("[Game03EmergencyCargoDropController] Debug cargo: already falling.");
            return;
        }

        BeginCargoDrop();
    }
}
