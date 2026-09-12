using TMPro;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// 同一 GameObject の <see cref="TMP_Text"/> に long を表示し、目標値へ加算・減算の両方で追従する。
    /// スロットのリール風に見せるため、終盤に向けてスムーズに収束しつつ前半で桁周りのノイズを載せる（単一テキスト用の近似）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TmpLongCounterRollPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("未指定時は同一オブジェクトの TMP_Text を使用する。")]
        private TMP_Text targetText;

        [SerializeField, Tooltip("一時停止中も演出を進めるならオン（unscaledDeltaTime）。")]
        private bool useUnscaledTime = true;

        [SerializeField, Tooltip("変化量が小さいときの最短演出時間（秒）。")]
        private float rollDurationMinSeconds = 0.45f;

        [SerializeField, Tooltip("演出が完了するまでの上限時間（秒）。桁が大きく変わってもこれを超えない。")]
        private float rollDurationMaxSeconds = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("演出の先頭からこの割合まではリール風ノイズを載せる。")]
        private float reelNoisePhaseEnd = 0.38f;

        [SerializeField, Range(0f, 0.5f), Tooltip("ノイズ幅の上限。変化幅に対する比率（0 でノイズなしの純補間）。")]
        private float reelNoiseMaxRatio = 0.012f;

        [SerializeField, Tooltip("表示の下限（負の再生数を出さない場合など）。")]
        private long displayClampMin = 0L;

        private double displayValue;
        private long goalValue;
        private bool animating;
        private float elapsed;
        private float duration;
        private double startValue;

        private void Awake()
        {
            EnsureTargetText();

            if (targetText == null)
            {
                return;
            }

            if (TryParseLongStrip(targetText.text, out long initial))
            {
                displayValue = initial;
                goalValue = initial;
            }
            else
            {
                displayValue = displayClampMin;
                goalValue = displayClampMin;
            }

            WriteText(RoundToLong(displayValue));
        }

        private void OnEnable()
        {
            EnsureTargetText();
            if (targetText != null)
            {
                WriteText(RoundToLong(displayValue));
            }
        }

        private void EnsureTargetText()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }
        }

        private void OnValidate()
        {
            rollDurationMinSeconds = Mathf.Max(0.01f, rollDurationMinSeconds);
            rollDurationMaxSeconds = Mathf.Max(rollDurationMinSeconds, rollDurationMaxSeconds);
        }

        /// <summary>演出なしで表示と目標を揃える（セーブ復帰・初期同期用）。</summary>
        public void SnapTo(long value)
        {
            EnsureTargetText();
            if (value < displayClampMin)
            {
                value = displayClampMin;
            }

            animating = false;
            displayValue = value;
            goalValue = value;
            elapsed = 0f;
            duration = 0f;
            WriteText(value);
        }

        /// <summary>目標値へ演出付きで追従。既に同目標で静止中なら何もしない。</summary>
        public void SetTarget(long value)
        {
            EnsureTargetText();
            if (value < displayClampMin)
            {
                value = displayClampMin;
            }

            if (!animating && value == goalValue && RoundToLong(displayValue) == value)
            {
                return;
            }

            goalValue = value;
            if (!animating)
            {
                startValue = displayValue;
            }
            else
            {
                startValue = displayValue;
            }

            elapsed = 0f;
            duration = ComputeDurationSeconds(startValue, goalValue);
            animating = duration > 0.0001f;
            if (!animating)
            {
                displayValue = goalValue;
                WriteText(goalValue);
            }
        }

        private void Update()
        {
            EnsureTargetText();
            if (!animating || targetText == null)
            {
                return;
            }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;
            float u = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            double eased = Smooth01(u);
            double core = startValue + (goalValue - startValue) * eased;
            double shown = core;
            if (reelNoiseMaxRatio > 0f && u < reelNoisePhaseEnd && goalValue != startValue)
            {
                double span = System.Math.Abs(goalValue - startValue);
                double noiseCap = span * reelNoiseMaxRatio * (1.0 - u / Mathf.Max(0.01f, reelNoisePhaseEnd));
                noiseCap = System.Math.Max(1d, noiseCap);
                int jmax = (int)System.Math.Min(noiseCap, 2_000_000d);
                jmax = Mathf.Max(1, jmax);
                int jitter = UnityEngine.Random.Range(-jmax, jmax + 1);
                shown = core + jitter;
            }

            displayValue = shown;
            long rounded = RoundToLong(displayValue);
            if (rounded < displayClampMin)
            {
                rounded = displayClampMin;
            }

            WriteText(rounded);
            if (u >= 1f)
            {
                animating = false;
                displayValue = goalValue;
                WriteText(goalValue);
            }
        }

        private float ComputeDurationSeconds(double from, long to)
        {
            float cap = Mathf.Max(rollDurationMinSeconds, rollDurationMaxSeconds);
            double span = System.Math.Abs(to - from);
            if (span < double.Epsilon)
            {
                return 0f;
            }

            float raw;
            if (span < 1d)
            {
                raw = Mathf.Max(0.08f, rollDurationMinSeconds * 0.45f);
            }
            else
            {
                double logSpan = System.Math.Log10(span + 1d);
                float t = Mathf.Clamp01((float)(logSpan / 6.0));
                raw = Mathf.Lerp(
                    Mathf.Max(0.01f, rollDurationMinSeconds),
                    cap,
                    t);
            }

            return Mathf.Min(raw, cap);
        }

        private static double Smooth01(float u)
        {
            return u * u * (3.0 - 2.0 * u);
        }

        private static long RoundToLong(double v)
        {
            if (v >= 0d)
            {
                return (long)System.Math.Floor(v + 0.5d);
            }

            return (long)System.Math.Ceiling(v - 0.5d);
        }

        private void WriteText(long v)
        {
            if (targetText != null)
            {
                targetText.text = v.ToString("N0");
            }
        }

        private static bool TryParseLongStrip(string s, out long value)
        {
            value = 0L;
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            string digits = string.Empty;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9' || c == '-')
                {
                    digits += c;
                }
            }

            return long.TryParse(digits, out value);
        }
    }
}
