using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Menu03EffectManager : HoverOverlayEffectManagerBase
{
    [Header("SandImage 参照")]
    [SerializeField] private RectTransform sandImage;
    [SerializeField] private RectTransform sandImageV2;
    [SerializeField] private RectTransform windImage;
    [SerializeField] private RectTransform movementArea;

    [Header("SandImage 速度 (px/sec)")]
    [SerializeField] private float minimumSpeed = 120f;
    [SerializeField] private float maximumSpeed = 360f;

    [Header("SandImageV2 速度 (px/sec)")]
    [SerializeField] private float minimumSpeedV2 = 90f;
    [SerializeField] private float maximumSpeedV2 = 280f;

    [Header("WindImage 速度 (px/sec)")]
    [SerializeField] private float minimumSpeedWind = 150f;
    [SerializeField] private float maximumSpeedWind = 420f;

    [Header("速度変更タイミング")]
    [SerializeField] private float speedChangeTime = 8f;
    [SerializeField] private float speedChangeRandomTime = 8f;
    [SerializeField] private float speedEaseDuration = 3f;
    [SerializeField] private bool logSandSpeedChange;

    [Header("KusaImage 参照")]
    [SerializeField] private RectTransform kusaImage;
    [SerializeField] private RectTransform kusaKageImage;

    [Header("KusaImage 演出")]
    [SerializeField] private float kusaMoveSpeed = 260f;
    [SerializeField] private float kusaBounceMaxHeight = 70f;
    [SerializeField] private float kusaFirstSetDelay = 4f;
    [SerializeField] private float kusaRepeatInterval = 30f;
    [SerializeField] private bool invertKusaRotationDirection;

    private sealed class LoopImageState
    {
        public string name;
        public RectTransform image;
        public float minimum;
        public float maximum;
        public bool keepInitialPositionForFirstLoop;
        public float fixedY;
        public bool firstLoopPending;
        public float currentSpeed;
        public float speedFrom;
        public float speedTo;
        public float speedEaseElapsed;
        public float nextSpeedChangeAfter;
        public float speedChangeElapsed;
        public bool initialized;
    }

    private const int SpeedBandCount = 5;

    private readonly Vector3[] worldCornersScratch = new Vector3[4];
    private readonly LoopImageState sandState = new LoopImageState();
    private readonly LoopImageState sandV2State = new LoopImageState();
    private readonly LoopImageState windState = new LoopImageState();
    private bool hasInitialized;
    private bool hasKusaInitialized;
    private Vector2 kusaInitialAnchoredPosition;
    private float kusaInitialZRotation;
    private float kusaSetTimer;
    private bool kusaSetRunning;
    private float kusaRotationZ;
    private float kusaSetProgress;
    private Vector2 kusaKageInitialAnchoredPosition;
    private float kusaKageInitialZRotation;
    private Vector2 kusaKageOffsetFromKusa;
    private Vector3 kusaKageInitialScale;
    private bool kusaAutomationPaused;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        SetupLoopStates();
        InitializeLoopState();

        InitializeKusaSetLoop();
    }

    protected override void Update()
    {
        base.Update();

        if (!hasInitialized)
        {
            SetupLoopStates();
            InitializeLoopState();
        }

        UpdateMovement(Time.deltaTime);

        UpdateKusaSet(Time.deltaTime);
    }

    private void InitializeLoopState()
    {
        hasInitialized = true;
        InitializeOneLoopState(sandState);
        InitializeOneLoopState(sandV2State);
        InitializeOneLoopState(windState);
    }

    private void SetupLoopStates()
    {
        SetupOneLoopState(sandState, sandImage, minimumSpeed, maximumSpeed, false);
        sandState.name = "SandImage";
        SetupOneLoopState(sandV2State, sandImageV2, minimumSpeedV2, maximumSpeedV2, true);
        sandV2State.name = "SandImageV2";
        SetupOneLoopState(windState, windImage, minimumSpeedWind, maximumSpeedWind, true);
        windState.name = "WindImage";
    }

    private void UpdateMovement(float deltaTime)
    {
        RectTransform area = ResolveMovementArea();
        if (area == null)
        {
            return;
        }

        UpdateOneLoopImage(sandState, area, deltaTime);
        UpdateOneLoopImage(sandV2State, area, deltaTime);
        UpdateOneLoopImage(windState, area, deltaTime);
    }

    private void SetupOneLoopState(LoopImageState state, RectTransform image, float min, float max, bool keepInitialForFirstLoop)
    {
        state.image = image;
        state.minimum = Mathf.Max(0f, Mathf.Min(min, max));
        state.maximum = Mathf.Max(state.minimum, max);
        state.keepInitialPositionForFirstLoop = keepInitialForFirstLoop;
        state.initialized = false;
    }

    private void InitializeOneLoopState(LoopImageState state)
    {
        if (state.image == null)
        {
            return;
        }

        state.initialized = true;
        state.fixedY = state.image.anchoredPosition.y;
        state.firstLoopPending = state.keepInitialPositionForFirstLoop;
        float initial = SampleSpeedFromLowestBand(state.minimum, state.maximum);
        state.currentSpeed = initial;
        state.speedFrom = initial;
        state.speedTo = initial;
        state.speedEaseElapsed = speedEaseDuration;
        state.speedChangeElapsed = 0f;
        state.nextSpeedChangeAfter = GetNextSpeedChangeTime();

        if (!state.firstLoopPending)
        {
            PlaceOneAtRightOutside(state, ResolveMovementArea());
        }
        else
        {
            Vector2 p = state.image.anchoredPosition;
            p.y = state.fixedY;
            state.image.anchoredPosition = p;
        }
    }

    private RectTransform ResolveMovementArea()
    {
        if (movementArea != null)
        {
            return movementArea;
        }

        if (sandImage != null)
        {
            return sandImage.parent as RectTransform;
        }

        if (sandImageV2 != null)
        {
            return sandImageV2.parent as RectTransform;
        }

        return windImage != null ? windImage.parent as RectTransform : null;
    }

    private void UpdateOneLoopImage(LoopImageState state, RectTransform area, float deltaTime)
    {
        if (state.image == null || area == null)
        {
            return;
        }

        if (!state.initialized)
        {
            InitializeOneLoopState(state);
        }

        UpdateOneSpeed(state, deltaTime);

        float halfWidth = GetRectWidthInParentLocal(area, state.image) * 0.5f;
        Rect r = area.rect;
        float leftOutsideX = r.xMin - halfWidth;
        float rightOutsideX = r.xMax + halfWidth;

        Vector2 p = state.image.anchoredPosition;
        p.x -= state.currentSpeed * deltaTime;
        p.y = state.fixedY;
        if (p.x < leftOutsideX)
        {
            p.x = rightOutsideX;
            state.firstLoopPending = false;
        }

        state.image.anchoredPosition = p;
    }

    private void UpdateOneSpeed(LoopImageState state, float deltaTime)
    {
        state.speedChangeElapsed += deltaTime;
        if (state.speedChangeElapsed >= state.nextSpeedChangeAfter)
        {
            state.speedChangeElapsed = 0f;
            state.nextSpeedChangeAfter = GetNextSpeedChangeTime();
            state.speedFrom = state.currentSpeed;
            state.speedTo = SampleSpeedFromFiveBandsExcludingCurrentAndAdjacent(
                state.minimum,
                state.maximum,
                state.currentSpeed);
            state.speedEaseElapsed = 0f;

            if (logSandSpeedChange && ReferenceEquals(state, sandState))
            {
                Debug.Log($"[Menu03EffectManager] {state.name} speed change current={state.speedFrom:F2} target={state.speedTo:F2}");
            }
        }

        if (state.speedEaseElapsed < speedEaseDuration)
        {
            state.speedEaseElapsed += deltaTime;
            float t = Mathf.Clamp01(state.speedEaseElapsed / Mathf.Max(0.01f, speedEaseDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            state.currentSpeed = Mathf.Lerp(state.speedFrom, state.speedTo, eased);
        }
        else
        {
            state.currentSpeed = state.speedTo;
        }
    }

    private void PlaceOneAtRightOutside(LoopImageState state, RectTransform area)
    {
        if (state.image == null || area == null)
        {
            return;
        }

        float halfWidth = GetRectWidthInParentLocal(area, state.image) * 0.5f;
        float rightOutsideX = area.rect.xMax + halfWidth;
        state.image.anchoredPosition = new Vector2(rightOutsideX, state.fixedY);
    }

    private void InitializeKusaSetLoop()
    {
        if (kusaImage == null)
        {
            return;
        }

        kusaInitialAnchoredPosition = kusaImage.anchoredPosition;
        kusaInitialZRotation = kusaImage.localEulerAngles.z;
        if (kusaKageImage != null)
        {
            kusaKageInitialAnchoredPosition = kusaKageImage.anchoredPosition;
            kusaKageInitialZRotation = kusaKageImage.localEulerAngles.z;
            kusaKageOffsetFromKusa = kusaKageInitialAnchoredPosition - kusaInitialAnchoredPosition;
            kusaKageInitialScale = kusaKageImage.localScale;
        }
        kusaRotationZ = kusaInitialZRotation;
        kusaSetProgress = 0f;
        kusaSetRunning = false;
        kusaSetTimer = -Mathf.Max(0f, kusaFirstSetDelay);
        hasKusaInitialized = true;
        ApplyKusaPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);
    }

    private void UpdateKusaSet(float deltaTime)
    {
        if (kusaImage == null)
        {
            return;
        }

        if (!hasKusaInitialized)
        {
            InitializeKusaSetLoop();
        }

        if (kusaAutomationPaused)
        {
            return;
        }

        if (!kusaSetRunning)
        {
            kusaSetTimer += deltaTime;
            if (kusaSetTimer >= 0f)
            {
                StartKusaSet();
            }

            return;
        }

        RectTransform area = ResolveKusaMovementArea();
        if (area == null)
        {
            return;
        }

        float halfWidth = GetRectWidthInParentLocal(area, kusaImage) * 0.5f;
        float leftOutsideX = area.rect.xMin - halfWidth;

        Vector2 p = kusaImage.anchoredPosition;
        p.x -= Mathf.Max(0f, kusaMoveSpeed) * deltaTime;

        float travelDistance = Mathf.Max(0.01f, (kusaInitialAnchoredPosition.x - leftOutsideX));
        float movedDistance = Mathf.Clamp(kusaInitialAnchoredPosition.x - p.x, 0f, travelDistance);
        kusaSetProgress = Mathf.Clamp01(movedDistance / travelDistance);
        float bounceY = ComputeKusaBounceOffset(kusaSetProgress);
        ApplyKusaPose(p.x, bounceY, deltaTime, true);

        if (p.x <= leftOutsideX)
        {
            EndKusaSet();
        }
    }

    private void StartKusaSet()
    {
        kusaSetRunning = true;
        kusaSetProgress = 0f;
        kusaRotationZ = kusaInitialZRotation;
        ApplyKusaPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);
    }

    private void EndKusaSet()
    {
        kusaSetRunning = false;
        kusaSetTimer = -Mathf.Max(0f, kusaRepeatInterval);
        kusaSetProgress = 0f;
        kusaRotationZ = kusaInitialZRotation;
        ApplyKusaPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);
    }

    /// <summary>通常時の <see cref="UpdateKusaSet"/> を止める（OP の単発演出など）。</summary>
    public void SetKusaAutomationPaused(bool paused)
    {
        kusaAutomationPaused = paused;
    }

    /// <summary>
    /// メニュー Canvas と同じ移動・バウンド・影ロジックで、草を左画面外まで1パス転がす（unscaled）。
    /// </summary>
    public IEnumerator CoRunKusaSinglePassUnscaled()
    {
        if (kusaImage == null)
        {
            yield break;
        }

        if (!hasKusaInitialized)
        {
            InitializeKusaSetLoop();
        }

        if (kusaImage == null)
        {
            yield break;
        }

        SetKusaAutomationPaused(true);
        try
        {
            kusaSetRunning = true;
            kusaSetProgress = 0f;
            kusaRotationZ = kusaInitialZRotation;
            ApplyKusaPose(kusaInitialAnchoredPosition.x, 0f, 0f, false);

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                RectTransform area = ResolveKusaMovementArea();
                if (area == null)
                {
                    EndKusaSet();
                    yield break;
                }

                float halfWidth = GetRectWidthInParentLocal(area, kusaImage) * 0.5f;
                float leftOutsideX = area.rect.xMin - halfWidth;

                Vector2 p = kusaImage.anchoredPosition;
                p.x -= Mathf.Max(0f, kusaMoveSpeed) * dt;

                float travelDistance = Mathf.Max(0.01f, kusaInitialAnchoredPosition.x - leftOutsideX);
                float movedDistance = Mathf.Clamp(kusaInitialAnchoredPosition.x - p.x, 0f, travelDistance);
                kusaSetProgress = Mathf.Clamp01(movedDistance / travelDistance);
                float bounceY = ComputeKusaBounceOffset(kusaSetProgress);
                ApplyKusaPose(p.x, bounceY, dt, true);

                if (p.x <= leftOutsideX)
                {
                    break;
                }

                yield return null;
            }

            EndKusaSet();
        }
        finally
        {
            SetKusaAutomationPaused(false);
        }
    }

    private float ComputeKusaBounceOffset(float t01)
    {
        float height = Mathf.Max(0f, kusaBounceMaxHeight);
        if (height <= 0f)
        {
            return 0f;
        }

        return height * (Pulse01(t01, 0.34f, 0.15f) + Pulse01(t01, 0.68f, 0.14f));
    }

    private static float Pulse01(float t, float center, float halfWidth)
    {
        float start = center - halfWidth;
        float end = center + halfWidth;
        if (t <= start || t >= end)
        {
            return 0f;
        }

        float local = (t - start) / Mathf.Max(0.001f, end - start);
        return 4f * local * (1f - local);
    }

    private void ApplyKusaPose(float x, float bounceYOffset, float rotationDeltaTime, bool advanceRotation)
    {
        Vector2 pos = new Vector2(x, kusaInitialAnchoredPosition.y + bounceYOffset);
        kusaImage.anchoredPosition = pos;

        if (advanceRotation)
        {
            float rotationSpeed = Mathf.Max(0f, kusaMoveSpeed) * 0.65f;
            float rotationSign = invertKusaRotationDirection ? 1f : -1f;
            kusaRotationZ += rotationSign * rotationSpeed * rotationDeltaTime;
        }

        kusaImage.localRotation = Quaternion.Euler(0f, 0f, kusaRotationZ);

        if (kusaKageImage != null)
        {
            Vector2 kagePos = new Vector2(pos.x + kusaKageOffsetFromKusa.x, kusaKageInitialAnchoredPosition.y);
            kusaKageImage.anchoredPosition = kagePos;
            kusaKageImage.localRotation = Quaternion.Euler(0f, 0f, kusaKageInitialZRotation);

            // 影は回転させず、バウンドで高さが増えるほど縮小させる。
            float maxHeight = Mathf.Max(0.01f, kusaBounceMaxHeight);
            float heightRate = Mathf.Clamp01(bounceYOffset / maxHeight);
            float scaleFactor = Mathf.Lerp(1f, 0.72f, heightRate);
            kusaKageImage.localScale = new Vector3(
                kusaKageInitialScale.x * scaleFactor,
                kusaKageInitialScale.y * scaleFactor,
                kusaKageInitialScale.z);
        }
    }

    private RectTransform ResolveKusaMovementArea()
    {
        if (movementArea != null)
        {
            return movementArea;
        }

        return kusaImage != null ? kusaImage.parent as RectTransform : null;
    }

    private float GetMinimumSpeed()
    {
        return Mathf.Max(0f, Mathf.Min(minimumSpeed, maximumSpeed));
    }

    private float GetMaximumSpeed()
    {
        return Mathf.Max(GetMinimumSpeed(), maximumSpeed);
    }

    private float GetNextSpeedChangeTime()
    {
        return Mathf.Max(0.01f, speedChangeTime) + Random.Range(0f, Mathf.Max(0f, speedChangeRandomTime));
    }

    private static float SampleSpeedFromLowestBand(float min, float max)
    {
        float safeMin = Mathf.Max(0f, Mathf.Min(min, max));
        float safeMax = Mathf.Max(safeMin, max);
        if (Mathf.Approximately(safeMin, safeMax))
        {
            return safeMin;
        }

        float bandWidth = (safeMax - safeMin) / SpeedBandCount;
        float bandEnd = safeMin + bandWidth;
        return Random.Range(safeMin, bandEnd);
    }

    private static float SampleSpeedFromFiveBandsExcludingCurrentAndAdjacent(float min, float max, float currentSpeed)
    {
        float safeMin = Mathf.Max(0f, Mathf.Min(min, max));
        float safeMax = Mathf.Max(safeMin, max);
        if (Mathf.Approximately(safeMin, safeMax))
        {
            return safeMin;
        }

        float bandWidth = (safeMax - safeMin) / SpeedBandCount;
        if (bandWidth <= 0f)
        {
            return safeMin;
        }

        int currentBand = Mathf.Clamp(Mathf.FloorToInt((currentSpeed - safeMin) / bandWidth), 0, SpeedBandCount - 1);
        int[] candidates = new int[SpeedBandCount];
        int count = 0;
        for (int i = 0; i < SpeedBandCount; i++)
        {
            if (Mathf.Abs(i - currentBand) <= 1)
            {
                continue;
            }

            candidates[count++] = i;
        }

        int selectedBand;
        if (count > 0)
        {
            selectedBand = candidates[Random.Range(0, count)];
        }
        else
        {
            selectedBand = Random.Range(0, SpeedBandCount);
        }

        float bandStart = safeMin + bandWidth * selectedBand;
        float bandEnd = selectedBand == SpeedBandCount - 1 ? safeMax : bandStart + bandWidth;
        return Random.Range(bandStart, bandEnd);
    }

    private float GetRectWidthInParentLocal(RectTransform parent, RectTransform target)
    {
        if (parent == null || target == null)
        {
            return 0f;
        }

        target.GetWorldCorners(worldCornersScratch);
        Vector2 p0 = ParentLocal(parent, worldCornersScratch[0]);
        Vector2 min = p0;
        Vector2 max = p0;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = ParentLocal(parent, worldCornersScratch[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return Mathf.Abs(max.x - min.x);
    }

    private static Vector2 ParentLocal(RectTransform parent, Vector3 worldPoint)
    {
        Vector3 local = parent.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumSpeed = Mathf.Max(0f, minimumSpeed);
        maximumSpeed = Mathf.Max(minimumSpeed, maximumSpeed);
        minimumSpeedV2 = Mathf.Max(0f, minimumSpeedV2);
        maximumSpeedV2 = Mathf.Max(minimumSpeedV2, maximumSpeedV2);
        minimumSpeedWind = Mathf.Max(0f, minimumSpeedWind);
        maximumSpeedWind = Mathf.Max(minimumSpeedWind, maximumSpeedWind);
        speedChangeTime = Mathf.Max(0.01f, speedChangeTime);
        speedChangeRandomTime = Mathf.Max(0f, speedChangeRandomTime);
        speedEaseDuration = Mathf.Max(0.01f, speedEaseDuration);
        kusaMoveSpeed = Mathf.Max(0f, kusaMoveSpeed);
        kusaBounceMaxHeight = Mathf.Max(0f, kusaBounceMaxHeight);
        kusaFirstSetDelay = Mathf.Max(0f, kusaFirstSetDelay);
        kusaRepeatInterval = Mathf.Max(0f, kusaRepeatInterval);
    }
#endif
}
