using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;

/// <summary>
/// メニュー Illust のスプライン周回に同期した Z 傾き（§menu_illust_character_motion）。
/// スカイダイビング中の風圧として、ノット基準傾き＋小刻み揺れを重ねる。
/// シーン開始直後はシーン配置された初期位置からノット0へ進入し、到達後に Spline 周回を開始する。
/// </summary>
[DefaultExecutionOrder(50)]
public class MenuIllustSplineTilt : MonoBehaviour
{
    const float WindEnvelopeFreqRatio = 0.12f;

    [SerializeField]
    private SplineContainer splineContainer;

    [SerializeField]
    private SplineAnimate splineAnimate;

    [SerializeField]
    private RectTransform targetRect;

    [Header("侵入演出")]
    [SerializeField]
    [Tooltip("有効時はシーン開始直後にシーン上の初期位置からノット0へ進入する")]
    private bool playEntryMotion = true;

    [SerializeField]
    [Tooltip("進入開始時の倍率（1.5 推奨）")]
    private float entryInitialScaleMultiplier = 1.5f;

    [SerializeField]
    [Tooltip("初期位置からノット0へ向かう速度（UIローカル座標/秒）")]
    private float entrySpeed = 900f;

    [Header("移動速度（スプライン一周の所要秒・Time モード）")]
    [SerializeField]
    [Tooltip("小さいほど周回が速い。SplineAnimate.Duration に反映する。")]
    private float movementLoopDurationSeconds = 18f;

    [Header("傾き（度・正の値＝右肩上げ）")]
    [SerializeField]
    [FormerlySerializedAs("tiltDegreesA")]
    [Tooltip("ノット 1・3 相当のそらし量（傾き低）")]
    private float tiltDegreesLow = 4f;

    [SerializeField]
    [FormerlySerializedAs("tiltDegreesB")]
    [Tooltip("ノット 2 相当の最大そらし量（傾き高）")]
    private float tiltDegreesHigh = 8f;

    [Header("風圧揺れ（スカイダイビング）")]
    [SerializeField]
    [Tooltip("小刻み揺れの主周波数（Hz）")]
    private float windShakeFrequencyHz = 2.2f;

    [SerializeField]
    [Tooltip("揺れの振幅（度）。0 に近いとノット傾きのみ。")]
    private float windShakeAmountDegrees = 1.25f;

    [Header("補間")]
    [SerializeField]
    private AnimationCurve segmentEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    SplinePath<Spline> _path;
    float _totalLength;
    float[] _cumDistance;
    float _startOffsetNormalized;
    int _branchKnotCount;
    bool _anySplineClosed;

    float _baseEulerX;
    float _baseEulerY;
    float _baseEulerZ;
    Vector3 _baseLocalScale;
    bool _capturedBaseEuler;
    bool _capturedBaseScale;

    bool _entryRunning;
    Vector3 _entryStartLocalPos;
    Vector3 _entryTargetLocalPos;
    float _entryDuration;
    float _entryElapsed;
    bool _capturedEntryStartPosition;

    public SplineContainer Container
    {
        get => splineContainer;
        set => splineContainer = value;
    }

    public SplineAnimate Animate
    {
        get => splineAnimate;
        set => splineAnimate = value;
    }

    void Awake()
    {
        if (targetRect == null)
        {
            targetRect = transform as RectTransform;
        }

        if (splineAnimate == null)
        {
            splineAnimate = GetComponent<SplineAnimate>();
        }

        CaptureEntryStartPositionIfNeeded();
    }

    void OnEnable()
    {
        Spline.Changed += OnSplineChanged;
        RebuildPathCache();
        ApplyMovementDuration();
        CaptureBaseEulerIfNeeded();
        CaptureBaseScaleIfNeeded();
        BeginMotionFlow();
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
    {
        if (splineContainer == null || splineContainer.Splines == null)
        {
            return;
        }

        foreach (var s in splineContainer.Splines)
        {
            if (s == spline)
            {
                RebuildPathCache();
                return;
            }
        }
    }

    /// <summary>コンテナ／Animate 割り当て後に外部から呼び出してキャッシュを再計算する。</summary>
    public void RefreshSplineCache()
    {
        RebuildPathCache();
        ApplyMovementDuration();
        BeginMotionFlow();
    }

    /// <summary>Inspector の移動速度を SplineAnimate に反映する（Time モード）。</summary>
    public void ApplyMovementDuration()
    {
        if (splineAnimate == null)
        {
            return;
        }

        splineAnimate.AnimationMethod = SplineAnimate.Method.Time;
        splineAnimate.Duration = Mathf.Max(0.05f, movementLoopDurationSeconds);
    }

    void RebuildPathCache()
    {
        _cumDistance = null;
        _totalLength = 0f;
        _branchKnotCount = 0;
        _anySplineClosed = false;

        if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
        {
            return;
        }

        foreach (var s in splineContainer.Splines)
        {
            if (s.Closed)
            {
                _anySplineClosed = true;
            }
        }

        _path = new SplinePath<Spline>(splineContainer.Splines);
        _totalLength = _path.GetLength();
        if (_totalLength <= math.EPSILON)
        {
            return;
        }

        _branchKnotCount = _path.Count;
        int curveCount = _branchKnotCount - 1;
        _cumDistance = new float[_branchKnotCount];
        _cumDistance[0] = 0f;
        for (int i = 0; i < curveCount; i++)
        {
            _cumDistance[i + 1] = _cumDistance[i] + _path.GetCurveLength(i);
        }

        UpdateStartOffsetNormalized();
    }

    void UpdateStartOffsetNormalized()
    {
        _startOffsetNormalized = 0f;
        if (splineAnimate == null || _totalLength <= math.EPSILON)
        {
            return;
        }

        float d = splineAnimate.StartOffset * _totalLength;
        _startOffsetNormalized = _path.ConvertIndexUnit(d, PathIndexUnit.Distance, PathIndexUnit.Normalized);
    }

    void LateUpdate()
    {
        if (targetRect == null || splineAnimate == null || splineContainer == null)
        {
            return;
        }

        if (_cumDistance == null || _totalLength <= math.EPSILON || _path == null)
        {
            RebuildPathCache();
            if (_cumDistance == null || _totalLength <= math.EPSILON || _path == null)
            {
                return;
            }
        }

        CaptureBaseEulerIfNeeded();
        CaptureBaseScaleIfNeeded();

        if (_entryRunning)
        {
            UpdateEntryMotion();
            return;
        }

        float pathT = GetCurrentNormalizedPathT();
        float dist = pathT * _totalLength;
        float zBase = EvaluateTiltZAtDistance(dist);
        float zWind = EvaluateWindShakeZ();

        targetRect.localEulerAngles = new Vector3(_baseEulerX, _baseEulerY, _baseEulerZ + zBase + zWind);
    }

    void CaptureBaseEulerIfNeeded()
    {
        if (_capturedBaseEuler || targetRect == null)
        {
            return;
        }

        Vector3 e = targetRect.localEulerAngles;
        _baseEulerX = e.x;
        _baseEulerY = e.y;
        _baseEulerZ = e.z;
        _capturedBaseEuler = true;
    }

    void CaptureBaseScaleIfNeeded()
    {
        if (_capturedBaseScale || targetRect == null)
        {
            return;
        }

        _baseLocalScale = targetRect.localScale;
        _capturedBaseScale = true;
    }

    void CaptureEntryStartPositionIfNeeded()
    {
        if (_capturedEntryStartPosition || targetRect == null)
        {
            return;
        }

        _entryStartLocalPos = targetRect.localPosition;
        _capturedEntryStartPosition = true;
    }

    void BeginMotionFlow()
    {
        if (!isActiveAndEnabled || targetRect == null || splineAnimate == null || splineContainer == null)
        {
            return;
        }

        if (_cumDistance == null || _totalLength <= math.EPSILON || _path == null)
        {
            return;
        }

        splineAnimate.Pause();
        splineAnimate.NormalizedTime = 0f;

        if (!playEntryMotion)
        {
            MoveToKnot0AndStartLoop();
            return;
        }

        CaptureEntryStartPositionIfNeeded();
        _entryTargetLocalPos = ConvertWorldToParentLocal(GetKnot0WorldPosition());
        Vector3 entryStart = _entryStartLocalPos;
        targetRect.localPosition = entryStart;
        targetRect.localScale = _baseLocalScale * Mathf.Max(0.01f, entryInitialScaleMultiplier);
        float entryDistance = Vector3.Distance(entryStart, _entryTargetLocalPos);
        _entryDuration = entryDistance / Mathf.Max(1f, entrySpeed);
        _entryDuration = Mathf.Max(0.12f, _entryDuration);
        _entryElapsed = 0f;
        _entryRunning = entryDistance > 1e-4f;
        if (!_entryRunning)
        {
            MoveToKnot0AndStartLoop();
        }
    }

    void UpdateEntryMotion()
    {
        _entryElapsed += Time.deltaTime;
        float progress = _entryDuration > 1e-4f ? Mathf.Clamp01(_entryElapsed / _entryDuration) : 1f;
        targetRect.localPosition = Vector3.Lerp(_entryStartLocalPos, _entryTargetLocalPos, progress);
        float currentScaleMul = Mathf.Lerp(entryInitialScaleMultiplier, 1f, progress);
        targetRect.localScale = _baseLocalScale * Mathf.Max(0.01f, currentScaleMul);

        float zWind = EvaluateWindShakeZ();
        targetRect.localEulerAngles = new Vector3(_baseEulerX, _baseEulerY, _baseEulerZ + zWind);

        if (progress >= 1f)
        {
            MoveToKnot0AndStartLoop();
        }
    }

    void MoveToKnot0AndStartLoop()
    {
        _entryRunning = false;
        targetRect.localPosition = _entryTargetLocalPos;
        targetRect.localScale = _baseLocalScale;
        targetRect.localEulerAngles = new Vector3(_baseEulerX, _baseEulerY, _baseEulerZ);
        splineAnimate.Restart(true);
    }

    Vector3 GetKnot0WorldPosition()
    {
        float3 p = splineContainer.EvaluatePosition(_path, 0f);
        return new Vector3(p.x, p.y, p.z);
    }

    Vector3 ConvertWorldToParentLocal(Vector3 worldPos)
    {
        if (targetRect != null && targetRect.parent != null)
        {
            return targetRect.parent.InverseTransformPoint(worldPos);
        }

        return worldPos;
    }

    /// <summary>SplineAnimate と同じループ正規化 t（0～1）。</summary>
    float GetCurrentNormalizedPathT()
    {
        float nt = splineAnimate.NormalizedTime;
        float loopT = nt - Mathf.Floor(nt);
        return Mathf.Repeat(loopT + _startOffsetNormalized, 1f);
    }

    float EvaluateTiltZAtDistance(float distance)
    {
        distance = math.clamp(distance, 0f, _totalLength);
        int last = _branchKnotCount - 1;
        if (last < 1)
        {
            return 0f;
        }

        int seg = last - 1;
        for (int i = 0; i < last; i++)
        {
            if (distance < _cumDistance[i + 1] - 1e-5f)
            {
                seg = i;
                break;
            }
        }

        float segStart = _cumDistance[seg];
        float segEnd = _cumDistance[seg + 1];
        float len = segEnd - segStart;
        float u = len > 1e-5f ? (distance - segStart) / len : 1f;
        u = math.saturate(u);
        float eased = segmentEase != null ? segmentEase.Evaluate(u) : u;

        float z0 = ZAtBranchKnot(seg);
        float z1 = ZAtBranchKnot(seg + 1);
        return math.lerp(z0, z1, eased);
    }

    float ZAtBranchKnot(int branchIndex)
    {
        if (_anySplineClosed && branchIndex == _branchKnotCount - 1)
        {
            return ZAtBranchKnot(0);
        }

        return ZFromPhase(branchIndex % 10, tiltDegreesLow, tiltDegreesHigh);
    }

    /// <summary>間欠的な強弱（低速エンベロープ）× 小刻み正弦で風圧感を出す。</summary>
    float EvaluateWindShakeZ()
    {
        if (windShakeAmountDegrees <= 1e-5f || windShakeFrequencyHz <= 1e-5f)
        {
            return 0f;
        }

        float t = Time.time;
        float fFast = windShakeFrequencyHz;
        float fEnv = Mathf.Max(0.02f, fFast * WindEnvelopeFreqRatio);
        float envelope = 0.42f + 0.58f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * fEnv));
        float micro = Mathf.Sin(t * 2f * Mathf.PI * fFast);
        float flutter = 0.22f * Mathf.Sin(t * 2f * Mathf.PI * fFast * 2.13f);
        return windShakeAmountDegrees * (micro + flutter) * envelope;
    }

    /// <summary>ノット位相 0～9（仕様 §4.3・10 ノット周期）。</summary>
    static float ZFromPhase(int phase, float a, float b)
    {
        switch (phase % 10)
        {
            case 0:
            case 4:
            case 5:
            case 9:
                return 0f;
            case 1:
            case 3:
                return a;
            case 2:
                return b;
            case 6:
            case 8:
                return -a;
            case 7:
                return -b;
            default:
                return 0f;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (targetRect == null)
        {
            targetRect = transform as RectTransform;
        }

        if (splineAnimate == null)
        {
            splineAnimate = GetComponent<SplineAnimate>();
        }

        entryInitialScaleMultiplier = Mathf.Max(0.01f, entryInitialScaleMultiplier);
        entrySpeed = Mathf.Max(1f, entrySpeed);
        movementLoopDurationSeconds = Mathf.Max(0.05f, movementLoopDurationSeconds);
        windShakeFrequencyHz = Mathf.Max(0f, windShakeFrequencyHz);
        windShakeAmountDegrees = Mathf.Max(0f, windShakeAmountDegrees);

        ApplyMovementDuration();

        if (splineContainer != null && _path != null && _totalLength > 0f)
        {
            UpdateStartOffsetNormalized();
        }
    }
#endif
}
