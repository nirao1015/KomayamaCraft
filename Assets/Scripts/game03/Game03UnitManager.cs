using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

[ExecuteAlways]
[DefaultExecutionOrder(-60)]
public class Game03UnitManager : MonoBehaviour
{
    private const string SubUnitAfterImageParentName = "MainUnitImg";

    private sealed class SubUnitState
    {
        public int SlotIndex;
        public RectTransform UnitRect;
        public RectTransform AfterImageParent;
        public RectTransform AfterImagePrefab;
        public Vector2 BaseAnchoredPosition;
        public Vector2 CurrentOffset;
        public float AfterImageSpawnTimer;
    }

    private sealed class AfterImageInstance
    {
        public RectTransform Rect;
        public Image Image;
        public RectTransform SourcePrefab;
        public float Age;
    }

    private enum ControlMode
    {
        Wasd,
        Mouse
    }

    [Header("Player Status")]
    [SerializeField, Min(1)] private int initialLife = 100;
    [SerializeField, Min(0)] private int currentLife;
    [SerializeField, Min(0.01f)] private float playerCoreRadius = 30f;

    [Header("Player Management")]
    [SerializeField, Min(0f)] private float invincibleDurationMs = 50f;

    [Header("References")]
    [SerializeField] private Game03Manager game03Manager;
    [SerializeField, Tooltip("無敵などデバッグの参照。未設定ならダメージ処理は通常どおり。")]
    private Game03DebugManager game03DebugManager;
    [SerializeField, Tooltip("HP 0 時のゲームオーバー演出。未設定なら演出なし。")]
    private Game03GameOverPresentation gameOverPresentation;
    [SerializeField] private Game03WeaponManager game03WeaponManager;
    [SerializeField] private RectTransform unitOjRect;
    [SerializeField] private RectTransform unit01Rect;
    [SerializeField] private RectTransform unit02Rect;
    [SerializeField] private RectTransform unit03Rect;
    [SerializeField] private RectTransform unit04Rect;
    [SerializeField] private Transform fieldRoot;
    [SerializeField] private Camera worldCamera;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 240f;
    [SerializeField, Min(0f)] private float mouseDeadZonePixels = 2f;
    [SerializeField] private Vector2 unitScreenOffsetPixels = Vector2.zero;
    [SerializeField] private bool applyOffsetToUnitRect = true;
    [SerializeField, Min(0f)] private float unitMarchSpacingPixels = 6f;
    [SerializeField, Min(0.01f)] private float unitMarchLerpSpeed = 12f;
    [SerializeField, Min(1f), Tooltip("Weapon07 装備時の最大遅れ距離倍率（unitMarchSpacingPixels 基準）。")]
    private float weapon07MarchSpacingMultiplier = 2f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Weapon07 装備時の追従補間速度倍率（小さいほど開始・停止で遅れる）。")]
    private float weapon07MarchLerpSpeedMultiplier = 0.5f;

    [Header("Vertical Limits (field local Y offset)")]
    [SerializeField] private float minFieldRootY = -8f;
    [SerializeField] private float maxFieldRootY = 8f;

    [Header("Horizontal Loop")]
    [SerializeField, Min(3)] private int loopColumnCount = 7;
    [SerializeField, Min(0.001f)] private float tileWidth = 15.36f;

    [Header("Auto Background Build")]
    [SerializeField] private bool autoBuildBackgroundTiles = false;
    [SerializeField] private Transform bgMidMakeC;
    [SerializeField] private Transform bgTopMakeC;
    [SerializeField] private Transform bgBotMakeC;
    [SerializeField] private Sprite[] bgMidLeftSprites = new Sprite[0];
    [SerializeField] private Sprite[] bgMidRightSprites = new Sprite[0];
    [SerializeField] private Vector2 backgroundGlobalOffset = Vector2.zero;
    [SerializeField] private float horizontalOverlapOffset = 0f;
    [SerializeField] private float verticalOverlapOffsetTop = 0f;
    [SerializeField] private float verticalOverlapOffsetBottom = 0f;
    [SerializeField] private bool usePixelOffsets = true;
    [SerializeField] private float bgTopYOffset = 0f;
    [SerializeField] private float bgBotYOffset = 0f;
    [SerializeField] private float bgMidLeftXOffset = 0f;
    [SerializeField] private float bgMidRightYOffset = 0f;

    [Header("After Image")]
    [SerializeField] private bool enableAfterImage = true;
    [SerializeField] private RectTransform afterImagePrefab;
    [SerializeField] private RectTransform afterImageParent;
    [SerializeField, Min(1)] private int maxActiveAfterImages = 5;
    [SerializeField, Min(0.01f)] private float afterImageSpawnInterval = 0.05f;
    [SerializeField, Min(0f)] private float afterImageDistancePixels = 72f;
    [SerializeField, Min(0.01f)] private float afterImageLifetime = 0.25f;
    [SerializeField, Min(0f)] private float afterImageScale = 1f;
    [SerializeField, Min(0.01f)] private float pausedAfterImageTickInterval = 0.1f;

    private ControlMode currentMode = ControlMode.Mouse;
    private Vector2 previousMousePosition;
    private Vector2 targetMousePosition;
    private readonly List<AfterImageInstance> afterImages = new List<AfterImageInstance>();
    private readonly Stack<AfterImageInstance> afterImagePool = new Stack<AfterImageInstance>();
    private readonly List<Transform> generatedBackgroundTiles = new List<Transform>();
    private readonly List<Transform> activeLoopTiles = new List<Transform>();
    private float cachedHorizontalLoopPeriod;
    private float afterImageSpawnTimer;
    private float pausedAfterImageTickTimer;

    private float runtimeMoveSpeedAdditive;
    private float metaPassiveHealRatePerSecond;
    private float metaPassiveHealAccumulator;
    private float lastDamagedTime = float.NegativeInfinity;
    private int lastHorizontalFacing = 1;
    private Vector2 lastAppliedFieldDeltaWorld = Vector2.zero;
    private Vector2 lastMovementIntentDirection = Vector2.right;
    private readonly List<SubUnitState> subUnits = new List<SubUnitState>(4);
    private bool IsEditMode => !Application.isPlaying;

    public int CurrentLife => currentLife;
    /// <summary>最大 HP（Inspector の initialLife）。HP バー割合の分母。</summary>
    public int MaxLife => Mathf.Max(1, initialLife);
    /// <summary>プレイヤー基準移動速度 Vp（ワールド単位/秒）。敵出現 JSON の速度換算に使用。</summary>
    public float BaseMoveSpeed => Mathf.Max(0f, moveSpeed);
    public float MoveSpeedWorldPerSecond => Mathf.Max(0f, moveSpeed + runtimeMoveSpeedAdditive);
    public float PlayerCoreRadius => playerCoreRadius;
    public int LastHorizontalFacing => lastHorizontalFacing >= 0 ? 1 : -1;
    public Vector2 LastAppliedFieldDeltaWorld => lastAppliedFieldDeltaWorld;
    /// <summary>
    /// WASD／マウスで「動こうとしている」進行方向（画面 XY・単位ベクトル）。斜め入力もそのまま。
    /// 入力が無いフレームでは直近の値を維持（Weapon05 の投射方向など）。
    /// </summary>
    public Vector2 LastMovementIntentDirection => lastMovementIntentDirection;
    public RectTransform MainUnitRect => unitOjRect;

    /// <summary>UnitUG 演出の Buzz 起点（プレイヤーコアのスクリーン座標）。</summary>
    public bool TryGetMainUnitScreenPoint(Camera eventCamera, out Vector2 screenPoint)
    {
        screenPoint = GetUnitScreenCenter();
        return unitOjRect != null;
    }
    public float MinFieldRootY => minFieldRootY;
    public float MaxFieldRootY => maxFieldRootY;
    public float CurrentFieldRootY => fieldRoot != null ? fieldRoot.localPosition.y : 0f;
    public Transform FieldRoot => fieldRoot;
    /// <summary>水平タイルループ 1 周のワールド X 幅（ナビの最短ループ判定用）。</summary>
    public float HorizontalLoopWorldPeriod => GetHorizontalLoopPeriod();
    private void Awake()
    {
        previousMousePosition = GetMousePosition();
        targetMousePosition = previousMousePosition;
        currentLife = Mathf.Max(0, initialLife);
    }

    private void Start()
    {
        ApplyUnitScreenOffsetIfNeeded();
        BuildSubUnitStates();
        BuildBackgroundTilesIfNeeded();
        RebuildLoopTargets();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CleanupAfterImagesInEditMode();
        }

        if (!autoBuildBackgroundTiles || !IsEditMode)
        {
            return;
        }

        if (fieldRoot == null || bgMidMakeC == null || bgTopMakeC == null || bgBotMakeC == null)
        {
            return;
        }

        BuildBackgroundTilesIfNeeded();
        RebuildLoopTargets();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            CleanupAfterImagesInEditMode();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        lastAppliedFieldDeltaWorld = Vector2.zero;

        if (game03Manager != null && !game03Manager.CanRunGameplay)
        {
            previousMousePosition = GetMousePosition();
            UpdateSubUnitMarchOffsets(Vector2.zero);
            pausedAfterImageTickTimer += Time.unscaledDeltaTime;
            if (pausedAfterImageTickTimer >= pausedAfterImageTickInterval)
            {
                TickAfterImages(pausedAfterImageTickTimer);
                pausedAfterImageTickTimer = 0f;
            }
            return;
        }

        pausedAfterImageTickTimer = 0f;

        float gameplayDtHeal = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
        TickMetaPassiveHeal(gameplayDtHeal);

        Vector2 currentMouse = GetMousePosition();
        bool mouseMoved = (currentMouse - previousMousePosition).sqrMagnitude > 0.0001f;
        if (mouseMoved)
        {
            currentMode = ControlMode.Mouse;
            targetMousePosition = currentMouse;
        }

        if (IsAnyWasdPressed())
        {
            currentMode = ControlMode.Wasd;
        }

        Vector2 direction = currentMode == ControlMode.Wasd
            ? GetWasdDirection()
            : GetMouseDirection();

        if (direction.sqrMagnitude > 0f)
        {
            lastMovementIntentDirection = direction.normalized;
            if (direction.x > 0.0001f)
            {
                lastHorizontalFacing = 1;
            }
            else if (direction.x < -0.0001f)
            {
                lastHorizontalFacing = -1;
            }

            float gameplayDt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
            float effectiveSpeed = Mathf.Max(0f, moveSpeed + runtimeMoveSpeedAdditive);
            Vector2 appliedFieldDelta = MoveField(-direction * effectiveSpeed * gameplayDt);
            lastAppliedFieldDeltaWorld = appliedFieldDelta;
            if (appliedFieldDelta.sqrMagnitude > 0.000001f)
            {
                // Player-facing movement is opposite of field movement.
                Vector2 trailDirection = -appliedFieldDelta.normalized;
                TrySpawnAfterImage(trailDirection);
                TrySpawnAllSubUnitAfterImages(trailDirection);
            }
            else
            {
                afterImageSpawnTimer = 0f;
            }
        }
        else
        {
            afterImageSpawnTimer = 0f;
        }

        UpdateSubUnitMarchOffsets(direction);

        TickAfterImages(game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime);
        previousMousePosition = currentMouse;
    }

    private void BuildSubUnitStates()
    {
        subUnits.Clear();
        AddSubUnit(1, unit01Rect);
        AddSubUnit(2, unit02Rect);
        AddSubUnit(3, unit03Rect);
        AddSubUnit(4, unit04Rect);
    }

    private static RectTransform ResolveSubUnitAfterImageParent(RectTransform unitRect)
    {
        if (unitRect == null)
        {
            return null;
        }

        RectTransform found = unitRect.Find(SubUnitAfterImageParentName) as RectTransform;
        if (found != null)
        {
            return found;
        }

        Transform[] descendants = unitRect.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform t = descendants[i];
            if (t != unitRect && t.name == SubUnitAfterImageParentName)
            {
                return t as RectTransform;
            }
        }

        return null;
    }

    private void EnsureSubUnitAfterImageResolved(SubUnitState state)
    {
        if (state == null || state.UnitRect == null)
        {
            return;
        }

        if (state.AfterImageParent == null)
        {
            state.AfterImageParent = ResolveSubUnitAfterImageParent(state.UnitRect);
        }

        state.AfterImagePrefab = null;
        if (game03WeaponManager != null)
        {
            game03WeaponManager.TryGetAfterImagePrefabForEquippedSlot(state.SlotIndex, out RectTransform prefab);
            state.AfterImagePrefab = prefab;
        }
    }

    private void AddSubUnit(int slotIndex, RectTransform unitRect)
    {
        if (unitRect == null)
        {
            return;
        }

        subUnits.Add(new SubUnitState
        {
            SlotIndex = slotIndex,
            UnitRect = unitRect,
            AfterImageParent = ResolveSubUnitAfterImageParent(unitRect),
            BaseAnchoredPosition = unitRect.anchoredPosition
        });
    }

    private void TrySpawnAllSubUnitAfterImages(Vector2 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        for (int i = 0; i < subUnits.Count; i++)
        {
            SubUnitState s = subUnits[i];
            if (s?.UnitRect == null || !s.UnitRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureSubUnitAfterImageResolved(s);
            if (s.AfterImageParent == null || s.AfterImagePrefab == null)
            {
                continue;
            }

            TrySpawnAfterImageForUnit(
                s.UnitRect,
                s.AfterImagePrefab,
                s.AfterImageParent,
                ref s.AfterImageSpawnTimer,
                moveDirection,
                useMainUnitScreenCenter: false);
        }
    }

    private void GetSubUnitMarchFollowParams(
        int slotIndex,
        out float spacingPixels,
        out float lerpSpeed,
        out bool holdInitialFormationPosition)
    {
        spacingPixels = unitMarchSpacingPixels;
        lerpSpeed = unitMarchLerpSpeed;
        holdInitialFormationPosition = false;

        if (game03WeaponManager == null
            || !game03WeaponManager.TryGetEquippedWeaponForSlot(slotIndex, out int weaponNumber, out _))
        {
            return;
        }

        switch (weaponNumber)
        {
            case 3:
                holdInitialFormationPosition = true;
                spacingPixels = 0f;
                break;
            case 7:
                spacingPixels = unitMarchSpacingPixels * weapon07MarchSpacingMultiplier;
                lerpSpeed = unitMarchLerpSpeed * weapon07MarchLerpSpeedMultiplier;
                break;
        }
    }

    private void UpdateSubUnitMarchOffsets(Vector2 moveDirection)
    {
        for (int i = 0; i < subUnits.Count; i++)
        {
            SubUnitState s = subUnits[i];
            if (s?.UnitRect == null)
            {
                continue;
            }

            if (!s.UnitRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            GetSubUnitMarchFollowParams(
                s.SlotIndex,
                out float spacingPixels,
                out float lerpSpeed,
                out bool holdInitialFormationPosition);

            if (holdInitialFormationPosition)
            {
                s.CurrentOffset = Vector2.zero;
                s.UnitRect.anchoredPosition = s.BaseAnchoredPosition;
                continue;
            }

            Vector2 trailingOffset = Vector2.zero;
            if (moveDirection.sqrMagnitude > 0.000001f)
            {
                trailingOffset = -moveDirection.normalized * spacingPixels;
            }

            float gameplayDt = game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, lerpSpeed) * gameplayDt);
            s.CurrentOffset = Vector2.Lerp(s.CurrentOffset, trailingOffset, t);
            s.UnitRect.anchoredPosition = s.BaseAnchoredPosition + s.CurrentOffset;
        }
    }

    private static bool IsAnyWasdPressed()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.wKey.isPressed
               || Keyboard.current.aKey.isPressed
               || Keyboard.current.sKey.isPressed
               || Keyboard.current.dKey.isPressed;
    }

    private static Vector2 GetWasdDirection()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        bool up = Keyboard.current.wKey.isPressed;
        bool down = Keyboard.current.sKey.isPressed;
        bool left = Keyboard.current.aKey.isPressed;
        bool right = Keyboard.current.dKey.isPressed;

        if ((up && down) || (left && right))
        {
            return Vector2.zero;
        }

        float x = 0f;
        float y = 0f;
        if (left)
        {
            x -= 1f;
        }

        if (right)
        {
            x += 1f;
        }

        if (up)
        {
            y += 1f;
        }

        if (down)
        {
            y -= 1f;
        }

        return new Vector2(x, y).normalized;
    }

    private static Vector2 GetMousePosition()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    private Vector2 GetMouseDirection()
    {
        Vector2 center = GetUnitScreenCenter();
        Vector2 toTarget = targetMousePosition - center;
        if (toTarget.sqrMagnitude <= mouseDeadZonePixels * mouseDeadZonePixels)
        {
            return Vector2.zero;
        }

        return toTarget.normalized;
    }

    private Vector2 GetUnitScreenCenter()
    {
        if (unitOjRect == null)
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + unitScreenOffsetPixels;
        }

        if (applyOffsetToUnitRect)
        {
            if (worldCamera == null)
            {
                return RectTransformUtility.WorldToScreenPoint(null, unitOjRect.position);
            }

            return RectTransformUtility.WorldToScreenPoint(worldCamera, unitOjRect.position);
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + unitScreenOffsetPixels;
    }

    private void ApplyUnitScreenOffsetIfNeeded()
    {
        if (!applyOffsetToUnitRect || unitOjRect == null)
        {
            return;
        }

        unitOjRect.anchoredPosition = unitScreenOffsetPixels;
    }

    private Vector2 MoveField(Vector2 delta)
    {
        if (fieldRoot == null)
        {
            return Vector2.zero;
        }

        Vector3 before = fieldRoot.position;
        Vector3 nextPosition = before + new Vector3(delta.x, delta.y, 0f);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minFieldRootY, maxFieldRootY);
        fieldRoot.position = nextPosition;
        WrapLoopTiles();
        Vector3 applied = nextPosition - before;
        return new Vector2(applied.x, applied.y);
    }

    private void WrapLoopTiles()
    {
        if (activeLoopTiles.Count == 0 || tileWidth <= 0f)
        {
            return;
        }

        float cameraHalfWidth = GetCameraHalfWidth();
        float cameraCenterX = worldCamera != null ? worldCamera.transform.position.x : 0f;
        float totalLoopWidth = GetHorizontalLoopPeriod();
        float leftLimit = cameraCenterX - cameraHalfWidth - tileWidth;
        float rightLimit = cameraCenterX + cameraHalfWidth + tileWidth;

        for (int i = 0; i < activeLoopTiles.Count; i++)
        {
            Transform tile = activeLoopTiles[i];
            if (tile == null)
            {
                continue;
            }

            Vector3 worldPos = tile.position;
            if (worldPos.x < leftLimit)
            {
                tile.localPosition += new Vector3(totalLoopWidth, 0f, 0f);
                continue;
            }

            if (worldPos.x > rightLimit)
            {
                tile.localPosition -= new Vector3(totalLoopWidth, 0f, 0f);
            }
        }
    }

    private float GetCameraHalfWidth()
    {
        if (worldCamera == null)
        {
            return 0f;
        }

        if (worldCamera.orthographic)
        {
            return worldCamera.orthographicSize * worldCamera.aspect;
        }

        return 0f;
    }

    private void BuildBackgroundTilesIfNeeded()
    {
        if (!autoBuildBackgroundTiles || fieldRoot == null || bgMidMakeC == null || bgTopMakeC == null || bgBotMakeC == null)
        {
            return;
        }

        generatedBackgroundTiles.Clear();

        Vector3 mainScale = bgMidMakeC.localScale;
        bgTopMakeC.localScale = mainScale;
        bgBotMakeC.localScale = mainScale;

        Vector3 midCenter = bgMidMakeC.localPosition;
        midCenter += new Vector3(ToWorldOffset(backgroundGlobalOffset.x), ToWorldOffset(backgroundGlobalOffset.y), 0f);
        bgMidMakeC.localPosition = midCenter;

        float midHeight = GetTileHeight(bgMidMakeC);
        float topHeight = GetTileHeight(bgTopMakeC);
        float botHeight = GetTileHeight(bgBotMakeC);

        float topOverlap = ToWorldOffset(verticalOverlapOffsetTop);
        float botOverlap = ToWorldOffset(verticalOverlapOffsetBottom);
        float topYAdjust = ToWorldOffset(bgTopYOffset);
        float botYAdjust = ToWorldOffset(bgBotYOffset);
        Vector3 topCenter = midCenter + new Vector3(0f, (midHeight + topHeight) * 0.5f + topOverlap + topYAdjust, 0f);
        Vector3 botCenter = midCenter - new Vector3(0f, (midHeight + botHeight) * 0.5f + botOverlap - botYAdjust, 0f);
        bgTopMakeC.localPosition = topCenter;
        bgBotMakeC.localPosition = botCenter;

        Sprite topSprite = GetTileSprite(bgTopMakeC);
        Sprite botSprite = GetTileSprite(bgBotMakeC);

        // 1) Top row auto tiles
        List<Transform> midLeftTiles = BuildSideTiles(bgMidMakeC, "BgMid-Make-L", bgMidLeftSprites, true);
        List<Transform> midRightTiles = BuildSideTiles(bgMidMakeC, "BgMid-Make-R", bgMidRightSprites, false);
        BuildSideTilesFromTemplate(bgTopMakeC, "BgTop-Make-L", topSprite, midLeftTiles, true);
        BuildSideTilesFromTemplate(bgTopMakeC, "BgTop-Make-R", topSprite, midRightTiles, false);

        // 2) Bottom row auto tiles
        BuildSideTilesFromTemplate(bgBotMakeC, "BgBot-Make-L", botSprite, midLeftTiles, true);
        BuildSideTilesFromTemplate(bgBotMakeC, "BgBot-Make-R", botSprite, midRightTiles, false);

        // 3) Mid row auto tiles already created above.

        generatedBackgroundTiles.Add(bgTopMakeC);
        generatedBackgroundTiles.Add(bgBotMakeC);
        generatedBackgroundTiles.Add(bgMidMakeC);

        ApplyBackgroundSiblingOrder(midLeftTiles.Count, midRightTiles.Count);

        tileWidth = Mathf.Max(0.001f, GetTileWidth(bgMidMakeC) + ToWorldOffset(horizontalOverlapOffset));
        RebuildLoopTargets();
    }

    private List<Transform> BuildSideTiles(Transform center, string prefix, Sprite[] sprites, bool isLeft)
    {
        List<Transform> created = new List<Transform>();
        if (sprites == null || sprites.Length == 0)
        {
            return created;
        }

        Transform previous = center;
        for (int i = 0; i < sprites.Length; i++)
        {
            string name = $"{prefix}{(i + 1):00}";
            Transform tile = GetOrCreateTile(center, name, sprites[i]);
            if (tile == null)
            {
                continue;
            }

            float step = GetNeighborCenterDistance(previous, tile, ToWorldOffset(horizontalOverlapOffset));
            Vector3 localPos = previous.localPosition + new Vector3(isLeft ? -step : step, 0f, 0f);
            float y = center.localPosition.y;
            if (isLeft)
            {
                localPos.x += ToWorldOffset(bgMidLeftXOffset);
            }
            else
            {
                y += ToWorldOffset(bgMidRightYOffset);
            }

            tile.localPosition = new Vector3(localPos.x, y, center.localPosition.z);

            created.Add(tile);
            generatedBackgroundTiles.Add(tile);
            previous = tile;
        }

        return created;
    }

    private void BuildSideTilesFromTemplate(
        Transform templateCenter,
        string prefix,
        Sprite sprite,
        List<Transform> midRowTiles,
        bool isLeft)
    {
        if (templateCenter == null || midRowTiles == null || midRowTiles.Count == 0)
        {
            return;
        }

        for (int i = 0; i < midRowTiles.Count; i++)
        {
            Transform midTile = midRowTiles[i];
            if (midTile == null)
            {
                continue;
            }

            string name = $"{prefix}{(i + 1):00}";
            Transform tile = GetOrCreateTile(templateCenter, name, sprite);
            if (tile == null)
            {
                continue;
            }

            tile.localPosition = new Vector3(midTile.localPosition.x, templateCenter.localPosition.y, templateCenter.localPosition.z);
            generatedBackgroundTiles.Add(tile);
        }
    }

    private Transform GetOrCreateTile(Transform source, string name, Sprite spriteOverride)
    {
        if (source == null || fieldRoot == null)
        {
            return null;
        }

        Transform existing = FindChildByName(fieldRoot, name);
        if (existing != null)
        {
            existing.localScale = bgMidMakeC != null ? bgMidMakeC.localScale : source.localScale;
            SetTileSprite(existing, spriteOverride);
            existing.gameObject.SetActive(true);
            return existing;
        }

        // In Edit Mode, do not create missing naming-rule objects.
        // Existing objects are only overwritten, while missing objects can be spawned transiently during Play.
        if (IsEditMode)
        {
            return null;
        }

        Transform created = Instantiate(source, fieldRoot);
        created.name = name;
        created.localScale = bgMidMakeC != null ? bgMidMakeC.localScale : source.localScale;
        SetTileSprite(created, spriteOverride);
        return created;
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private void ApplyBackgroundSiblingOrder(int leftCount, int rightCount)
    {
        if (fieldRoot == null)
        {
            return;
        }

        int index = 0;
        SetSiblingIfExists(bgTopMakeC, index++);
        for (int i = 1; i <= leftCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgTop-Make-L{i:00}"), index++);
        }

        for (int i = 1; i <= rightCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgTop-Make-R{i:00}"), index++);
        }

        SetSiblingIfExists(bgBotMakeC, index++);
        for (int i = 1; i <= leftCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgBot-Make-L{i:00}"), index++);
        }

        for (int i = 1; i <= rightCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgBot-Make-R{i:00}"), index++);
        }

        for (int i = 1; i <= leftCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgMid-Make-L{i:00}"), index++);
        }

        for (int i = 1; i <= rightCount; i++)
        {
            SetSiblingIfExists(FindChildByName(fieldRoot, $"BgMid-Make-R{i:00}"), index++);
        }

        // Keep mid center image foremost as requested.
        SetSiblingIfExists(bgMidMakeC, index);
    }

    private static void SetSiblingIfExists(Transform target, int index)
    {
        if (target != null)
        {
            target.SetSiblingIndex(index);
        }
    }

    private void RebuildLoopTargets()
    {
        activeLoopTiles.Clear();
        if (fieldRoot == null)
        {
            return;
        }

        int requestedColumns = Mathf.Max(3, loopColumnCount);
        int extraColumns = requestedColumns - 1;
        int leftCount = (extraColumns + 1) / 2;
        int rightCount = extraColumns / 2;

        // Center column
        AddLoopTileIfExists(bgTopMakeC);
        AddLoopTileIfExists(bgBotMakeC);
        AddLoopTileIfExists(bgMidMakeC);

        for (int i = 1; i <= leftCount; i++)
        {
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgTop-Make-L{i:00}"));
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgBot-Make-L{i:00}"));
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgMid-Make-L{i:00}"));
        }

        for (int i = 1; i <= rightCount; i++)
        {
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgTop-Make-R{i:00}"));
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgBot-Make-R{i:00}"));
            AddLoopTileIfExists(FindChildByName(fieldRoot, $"BgMid-Make-R{i:00}"));
        }

        if (activeLoopTiles.Count == 0)
        {
            // Fallback: keep previous behavior for non-auto scenes.
            for (int i = 0; i < fieldRoot.childCount; i++)
            {
                AddLoopTileIfExists(fieldRoot.GetChild(i));
            }
        }

        CollectLoopMapBuildingsFromFieldRoot();

        RefreshHorizontalLoopPeriod();
    }

    /// <summary>
    /// ObstaclesRoot 直下のマップ建物（BG-Saloon / BG-Water / BG-Watch 系）を水平ループの巻き戻し対象に含める。
    /// </summary>
    private void CollectLoopMapBuildingsFromFieldRoot()
    {
        if (fieldRoot == null)
        {
            return;
        }

        for (int i = 0; i < fieldRoot.childCount; i++)
        {
            Transform child = fieldRoot.GetChild(i);
            if (child != null && IsHorizontalLoopMapBuilding(child.name))
            {
                AddLoopTileIfExists(child);
            }
        }
    }

    private static bool IsHorizontalLoopMapBuilding(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        return objectName.StartsWith("BG-Saloon", StringComparison.Ordinal)
            || objectName.StartsWith("BG-Water", StringComparison.Ordinal)
            || objectName.StartsWith("BG-Watch", StringComparison.Ordinal);
    }

    private void RefreshHorizontalLoopPeriod()
    {
        cachedHorizontalLoopPeriod = ComputeHorizontalLoopPeriodFromMidTiles();
    }

    private float GetHorizontalLoopPeriod()
    {
        if (cachedHorizontalLoopPeriod > 0f)
        {
            return cachedHorizontalLoopPeriod;
        }

        return Mathf.Max(0.001f, tileWidth * Mathf.Max(3, loopColumnCount));
    }

    private float ComputeHorizontalLoopPeriodFromMidTiles()
    {
        List<Transform> midColumns = new List<Transform>(Mathf.Max(3, loopColumnCount));
        if (bgMidMakeC != null)
        {
            midColumns.Add(bgMidMakeC);
        }

        for (int i = 0; i < activeLoopTiles.Count; i++)
        {
            Transform tile = activeLoopTiles[i];
            if (tile == null || tile == bgMidMakeC || !IsMidRowLoopColumn(tile.name))
            {
                continue;
            }

            if (!midColumns.Contains(tile))
            {
                midColumns.Add(tile);
            }
        }

        if (midColumns.Count < 2)
        {
            return 0f;
        }

        midColumns.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
        Transform left = midColumns[0];
        Transform right = midColumns[midColumns.Count - 1];
        float span = right.localPosition.x - left.localPosition.x;
        float edgePad = (GetTileWidth(left) + GetTileWidth(right)) * 0.5f;
        return Mathf.Max(0.001f, span + edgePad);
    }

    private static bool IsMidRowLoopColumn(string tileName)
    {
        if (string.IsNullOrEmpty(tileName))
        {
            return false;
        }

        return tileName.StartsWith("BgMid-Make-L", StringComparison.Ordinal)
            || tileName.StartsWith("BgMid-Make-R", StringComparison.Ordinal);
    }

    private void AddLoopTileIfExists(Transform tile)
    {
        if (tile != null && !activeLoopTiles.Contains(tile))
        {
            activeLoopTiles.Add(tile);
        }
    }

    private void CleanupAfterImagesInEditMode()
    {
        afterImages.Clear();
        afterImagePool.Clear();

        CleanupAfterImageClonesUnderParent(afterImageParent);
        for (int i = 0; i < subUnits.Count; i++)
        {
            SubUnitState s = subUnits[i];
            if (s?.AfterImageParent != null)
            {
                CleanupAfterImageClonesUnderParent(s.AfterImageParent);
            }
            else if (s?.UnitRect != null)
            {
                CleanupAfterImageClonesUnderParent(ResolveSubUnitAfterImageParent(s.UnitRect));
            }
        }
    }

    private void CleanupAfterImageClonesUnderParent(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null && IsAfterImageCloneName(child.name))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static bool IsAfterImageCloneName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)
            || objectName.IndexOf("(Clone)", StringComparison.Ordinal) < 0)
        {
            return false;
        }

        return objectName.StartsWith("MainUnitImagePrefab", StringComparison.Ordinal)
            || (objectName.StartsWith("Weapon", StringComparison.Ordinal)
                && objectName.IndexOf("ImagePrefab", StringComparison.Ordinal) > 0);
    }

    private static float GetNeighborCenterDistance(Transform a, Transform b, float overlapOffset)
    {
        return Mathf.Max(0.001f, (GetTileWidth(a) + GetTileWidth(b)) * 0.5f + overlapOffset);
    }

    private float ToWorldOffset(float value)
    {
        if (!usePixelOffsets || worldCamera == null || !worldCamera.orthographic)
        {
            return value;
        }

        float unitsPerPixel = (worldCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
        return value * unitsPerPixel;
    }

    private static float GetTileWidth(Transform tile)
    {
        if (tile == null)
        {
            return 0.001f;
        }

        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            return Mathf.Abs(renderer.sprite.bounds.size.x * tile.localScale.x);
        }

        return Mathf.Max(0.001f, tile.localScale.x);
    }

    private static float GetTileHeight(Transform tile)
    {
        if (tile == null)
        {
            return 0.001f;
        }

        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            return Mathf.Abs(renderer.sprite.bounds.size.y * tile.localScale.y);
        }

        return Mathf.Max(0.001f, tile.localScale.y);
    }

    private static Sprite GetTileSprite(Transform tile)
    {
        if (tile == null)
        {
            return null;
        }

        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }

    private static void SetTileSprite(Transform tile, Sprite sprite)
    {
        if (tile == null || sprite == null)
        {
            return;
        }

        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = sprite;
        }
    }

    private void TrySpawnAfterImage(Vector2 moveDirection)
    {
        if (!enableAfterImage || afterImagePrefab == null || unitOjRect == null)
        {
            return;
        }

        RectTransform parent = afterImageParent != null ? afterImageParent : unitOjRect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        TrySpawnAfterImageForUnit(
            unitOjRect,
            afterImagePrefab,
            parent,
            ref afterImageSpawnTimer,
            moveDirection,
            useMainUnitScreenCenter: true);
    }

    private void TrySpawnAfterImageForUnit(
        RectTransform unitRect,
        RectTransform prefab,
        RectTransform parent,
        ref float spawnTimer,
        Vector2 moveDirection,
        bool useMainUnitScreenCenter)
    {
        if (!enableAfterImage || prefab == null || unitRect == null || parent == null)
        {
            return;
        }

        if (moveDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        spawnTimer += game03Manager != null ? game03Manager.GameplayDeltaTime : Time.deltaTime;
        if (spawnTimer < afterImageSpawnInterval)
        {
            return;
        }

        spawnTimer = 0f;

        AfterImageInstance instance = AcquireAfterImage(parent, prefab);
        if (instance == null)
        {
            return;
        }

        instance.Rect.localScale = unitRect.localScale * Mathf.Max(0f, afterImageScale);
        if (useMainUnitScreenCenter)
        {
            PlaceAfterImageAtScreenCenter(instance.Rect, parent, moveDirection);
        }
        else
        {
            PlaceAfterImageAtUnit(unitRect, instance.Rect, parent, moveDirection);
        }

        instance.Age = 0f;
        instance.Rect.gameObject.SetActive(true);
        SetAfterImageAlpha(instance.Image, 1f);
        afterImages.Add(instance);
    }

    private AfterImageInstance AcquireAfterImage(RectTransform parent, RectTransform prefab)
    {
        while (afterImagePool.Count > 0)
        {
            AfterImageInstance pooled = afterImagePool.Pop();
            if (pooled == null || pooled.Rect == null || pooled.Image == null)
            {
                continue;
            }

            if (pooled.SourcePrefab != prefab)
            {
                Destroy(pooled.Rect.gameObject);
                continue;
            }

            if (pooled.Rect.parent != parent)
            {
                pooled.Rect.SetParent(parent, false);
            }

            return pooled;
        }

        if (afterImages.Count >= Mathf.Max(1, maxActiveAfterImages))
        {
            if (afterImages.Count == 0)
            {
                return null;
            }

            AfterImageInstance oldest = afterImages[0];
            afterImages.RemoveAt(0);
            if (oldest?.Rect != null)
            {
                oldest.Rect.gameObject.SetActive(false);
            }

            afterImagePool.Push(oldest);
            return AcquireAfterImage(parent, prefab);
        }

        RectTransform instanceRect = Instantiate(prefab, parent);
        Image image = instanceRect.GetComponent<Image>();
        if (image == null)
        {
            Destroy(instanceRect.gameObject);
            return null;
        }

        return new AfterImageInstance
        {
            Rect = instanceRect,
            Image = image,
            SourcePrefab = prefab,
            Age = 0f
        };
    }

    private void TickAfterImages(float deltaTime)
    {
        for (int i = afterImages.Count - 1; i >= 0; i--)
        {
            AfterImageInstance entry = afterImages[i];
            if (entry == null || entry.Rect == null || entry.Image == null)
            {
                afterImages.RemoveAt(i);
                continue;
            }

            entry.Age += deltaTime;
            float t = Mathf.Clamp01(entry.Age / afterImageLifetime);
            SetAfterImageAlpha(entry.Image, 1f - t);

            if (entry.Age >= afterImageLifetime)
            {
                entry.Rect.gameObject.SetActive(false);
                afterImagePool.Push(entry);
                afterImages.RemoveAt(i);
            }
        }
    }

    private static void SetAfterImageAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    private void PlaceAfterImageAtScreenCenter(RectTransform instanceRect, RectTransform parent, Vector2 moveDirection)
    {
        Vector2 spawnScreen = GetUnitScreenCenter() - moveDirection * afterImageDistancePixels;
        Camera eventCamera = GetUiEventCamera(parent);
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, spawnScreen, eventCamera, out Vector3 worldPoint))
        {
            instanceRect.position = worldPoint;
            return;
        }

        if (unitOjRect != null)
        {
            instanceRect.position = unitOjRect.position - (Vector3)(moveDirection * afterImageDistancePixels);
        }
    }

    private void PlaceAfterImageAtUnit(
        RectTransform unitRect,
        RectTransform instanceRect,
        RectTransform parent,
        Vector2 moveDirection)
    {
        Camera eventCamera = GetUiEventCamera(parent);
        Vector2 unitScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, unitRect.position);
        Vector2 spawnScreen = unitScreen - moveDirection * afterImageDistancePixels;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, spawnScreen, eventCamera, out Vector3 worldPoint))
        {
            instanceRect.position = worldPoint;
            return;
        }

        instanceRect.position = unitRect.position - (Vector3)(moveDirection * afterImageDistancePixels);
    }

    private Camera GetUiEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return worldCamera;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0 || currentLife <= 0)
        {
            return false;
        }

        if (game03DebugManager != null && game03DebugManager.EffectiveDebugInvincible)
        {
            return false;
        }

        float invincibleDurationSeconds = Mathf.Max(0f, invincibleDurationMs) / 1000f;
        if (Time.unscaledTime - lastDamagedTime < invincibleDurationSeconds)
        {
            return false;
        }

        lastDamagedTime = Time.unscaledTime;
        currentLife = Mathf.Max(0, currentLife - damage);
        Game03CombatBalanceLogger.TryGet()?.RecordPlayerHit();
        if (currentLife <= 0)
        {
            Game03CombatBalanceLogger.TryGet()?.RecordGameOver();
        }

        if (currentLife <= 0)
        {
            EnsureGameOverPresentationResolved();
            gameOverPresentation?.NotifyPlayerDefeated();
        }

        return true;
    }

    private void EnsureGameOverPresentationResolved()
    {
        if (gameOverPresentation != null)
        {
            return;
        }

        gameOverPresentation = FindAnyObjectByType<Game03GameOverPresentation>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 救急箱: 最大 HP に対する割合分を回復（最大 HP 超過なし）。仕様 ■3。
    /// </summary>
    public void ApplyMedkitHealFromCurrentHpFraction(float fraction01)
    {
        if (currentLife <= 0 || initialLife <= 0)
        {
            return;
        }

        int heal = Mathf.FloorToInt(initialLife * Mathf.Clamp01(fraction01));
        if (heal <= 0)
        {
            return;
        }

        currentLife = Mathf.Min(initialLife, currentLife + heal);
    }

    /// <summary>
    /// メタ進行由来の移動加算（ワールド速度への加算値）と HP 自動回復（秒あたり）。Inspector の moveSpeed 本体は変更しない。
    /// </summary>
    public void SetMetaGameplayModifiers(float moveSpeedAdditive, float passiveHealHpPerSecond)
    {
        runtimeMoveSpeedAdditive = Mathf.Max(0f, moveSpeedAdditive);
        metaPassiveHealRatePerSecond = Mathf.Max(0f, passiveHealHpPerSecond);
    }

    private void TickMetaPassiveHeal(float gameplayDeltaTime)
    {
        if (metaPassiveHealRatePerSecond <= 0f || currentLife <= 0 || currentLife >= initialLife)
        {
            metaPassiveHealAccumulator = 0f;
            return;
        }

        if (gameplayDeltaTime <= 0f)
        {
            return;
        }

        float rate = metaPassiveHealRatePerSecond;

        metaPassiveHealAccumulator += rate * gameplayDeltaTime;
        if (metaPassiveHealAccumulator < 1f)
        {
            return;
        }

        int heal = Mathf.FloorToInt(metaPassiveHealAccumulator);
        metaPassiveHealAccumulator -= heal;
        currentLife = Mathf.Min(initialLife, currentLife + heal);
    }

    public bool TryGetUnitCenterOnRect(RectTransform targetRect, out Vector2 localPoint)
    {
        return TryGetUnitCenterOnRect(unitOjRect, targetRect, out localPoint);
    }

    public bool TryConvertWorldDeltaToLocalOnRect(RectTransform targetRect, Vector2 worldDelta, out Vector2 localDelta)
    {
        localDelta = Vector2.zero;
        if (targetRect == null)
        {
            return false;
        }

        Camera cam = worldCamera;
        Vector3 worldOrigin = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 worldNext = worldOrigin + new Vector3(worldDelta.x, worldDelta.y, 0f);
        Vector2 screenOrigin = RectTransformUtility.WorldToScreenPoint(cam, worldOrigin);
        Vector2 screenNext = RectTransformUtility.WorldToScreenPoint(cam, worldNext);
        Camera eventCamera = GetUiEventCamera(targetRect);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenOrigin, eventCamera, out Vector2 localOrigin))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenNext, eventCamera, out Vector2 localNext))
        {
            return false;
        }

        localDelta = localNext - localOrigin;
        return true;
    }

    public bool TryGetUnitCenterOnRect(RectTransform unitRect, RectTransform targetRect, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (targetRect == null || unitRect == null)
        {
            return false;
        }

        Camera screenPointCamera = worldCamera;
        Canvas sourceCanvas = unitRect.GetComponentInParent<Canvas>();
        if (sourceCanvas != null && sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPointCamera = null;
        }

        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(screenPointCamera, unitRect.position);
        Camera eventCamera = GetUiEventCamera(targetRect);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenCenter, eventCamera, out localPoint);
    }

    /// <summary>
    /// フィールド背景ルート（<see cref="fieldRoot"/>）のワールド位置を <paramref name="targetRect"/> 上のローカル座標へ投影する。
    /// 出現種別 04/05 など、画面基準ではなく背景中心基準で並べるために使う。
    /// </summary>
    public bool TryGetFieldRootCenterOnRect(RectTransform targetRect, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (targetRect == null || fieldRoot == null)
        {
            return false;
        }

        Camera screenPointCamera = worldCamera;
        if (screenPointCamera == null)
        {
            return false;
        }

        Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(screenPointCamera, fieldRoot.position);
        Camera eventCamera = GetUiEventCamera(targetRect);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPt, eventCamera, out localPoint);
    }
}
