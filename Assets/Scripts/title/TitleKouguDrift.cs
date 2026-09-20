using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル kougu 画像を、一定角速度の 8 の字で漂わせつつゆっくり回転させる。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleKouguDrift : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private RectTransform driftTarget;

    [Header("8の字漂い")]
    [SerializeField] private bool enableDrift = true;
    [SerializeField, Min(0.1f), Tooltip("初期位置に戻るまでの秒。")]
    private float loopSeconds = 120f;
    [SerializeField, Min(0f), Tooltip("8の字の半幅（ローカル）。")]
    private float amplitude = 28f;
    [SerializeField, Tooltip("8の字の向き（度）。0で横向き。右上↔左下はだいたい -45。")]
    private float figureEightAxisDegrees = -45f;

    [Header("回転")]
    [SerializeField, Tooltip("時計回りを正。駒山（既定6°/秒）より遅く。")]
    private float spinDegreesPerSecond = 2f;

    private Vector2 baseAnchoredPosition;
    private Vector3 baseLocalEuler;
    private float elapsed;
    private float spinDegrees;
    private bool cached;

    private void Awake()
    {
        EnsureTarget();
        CacheBasePose();
    }

    private void OnEnable()
    {
        EnsureTarget();
        CacheBasePose();
        elapsed = 0f;
        spinDegrees = 0f;
        ApplyPose(0f);
    }

    private void Update()
    {
        if (!enableDrift || driftTarget == null)
        {
            return;
        }

        if (!cached)
        {
            CacheBasePose();
        }

        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        // 時計回り＝Unity Z マイナス
        spinDegrees -= spinDegreesPerSecond * dt;
        ApplyPose(elapsed);
    }

    private void OnDisable()
    {
        if (driftTarget == null || !cached)
        {
            return;
        }

        driftTarget.anchoredPosition = baseAnchoredPosition;
        driftTarget.localEulerAngles = baseLocalEuler;
    }

    private void EnsureTarget()
    {
        if (driftTarget != null)
        {
            return;
        }

        Transform image = transform.Find("Image");
        if (image != null)
        {
            driftTarget = image as RectTransform;
            return;
        }

        Image childImage = GetComponentInChildren<Image>(true);
        if (childImage != null)
        {
            driftTarget = childImage.rectTransform;
        }
    }

    private void CacheBasePose()
    {
        if (driftTarget == null)
        {
            cached = false;
            return;
        }

        baseAnchoredPosition = driftTarget.anchoredPosition;
        baseLocalEuler = driftTarget.localEulerAngles;
        cached = true;
    }

    private void ApplyPose(float timeSeconds)
    {
        float period = Mathf.Max(0.1f, loopSeconds);
        // 一定角速度。t=0 と t=period で同一点。
        float t = (Mathf.PI * 2f) * (timeSeconds / period);

        // 横向き 8 の字（レムニスケート近似）: x=sin t, y=sin t cos t
        float localX = Mathf.Sin(t) * amplitude;
        float localY = Mathf.Sin(t) * Mathf.Cos(t) * amplitude;

        float rad = figureEightAxisDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float x = localX * cos - localY * sin;
        float y = localX * sin + localY * cos;

        driftTarget.anchoredPosition = baseAnchoredPosition + new Vector2(x, y);

        Vector3 euler = baseLocalEuler;
        euler.z += spinDegrees;
        driftTarget.localEulerAngles = euler;
    }
}
