using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// <c>PanelCanvas/.../CatButton</c> に付与。クリックでスケール変更と猫 SE（間隔制御は <see cref="Game02SeManager"/>）。拡大は <see cref="PersonEffectManager.EnlargeSmoothSeconds"/> に応じて追従。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class CatButtonController : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private PersonEffectManager personEffectManager;

        private RectTransform rectTransform;

        /// <summary>クリックのたびに即更新する目標スケール。拡大スムーズ時は表示がこれに追従する。</summary>
        private Vector3 committedScale;

        private Vector3 scaleVelocity;

        /// <summary>初回 <see cref="Awake"/> 時の <c>localScale</c>。<c>ResetScaleToBaseline</c> で戻す。</summary>
        private Vector3 baselineLocalScale;

        private bool baselineCaptured;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                committedScale = rectTransform.localScale;
                CaptureBaselineIfNeeded();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnClickCat);
                button.onClick.AddListener(OnClickCat);
            }
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform != null)
            {
                committedScale = rectTransform.localScale;
                scaleVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 猫ボタンの表示・内部目標スケールを、シーン読み込み後初回 <c>Awake</c> で記録した値へ即戻す（拡大スムーズの速度もクリア）。
        /// </summary>
        public void ResetScaleToBaseline()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform == null)
            {
                return;
            }

            CaptureBaselineIfNeeded();
            committedScale = baselineLocalScale;
            rectTransform.localScale = baselineLocalScale;
            scaleVelocity = Vector3.zero;
        }

        private void CaptureBaselineIfNeeded()
        {
            if (baselineCaptured || rectTransform == null)
            {
                return;
            }

            baselineLocalScale = rectTransform.localScale;
            baselineCaptured = true;
        }

        private void LateUpdate()
        {
            if (rectTransform == null || personEffectManager == null)
            {
                return;
            }

            float smooth = personEffectManager.EnlargeSmoothSeconds;
            if (smooth <= 0f)
            {
                return;
            }

            Vector3 current = rectTransform.localScale;
            Vector3 next = Vector3.SmoothDamp(
                current,
                committedScale,
                ref scaleVelocity,
                smooth,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            rectTransform.localScale = next;
        }

        private void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickCat);
            }
        }

        private void OnClickCat()
        {
            Game02AlienProgressTracker tracker = Game02AlienProgressTracker.EnsureExists();
            tracker?.NotifyCatButtonPressed();
            int lifetimePushCount = tracker != null ? tracker.LifetimeCatButtonPressCount : 0;
            Game02MsgManager.TryGet()?.NotifyLifetimeCatButtonCount(lifetimePushCount, GameManager.Instance);

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform != null && personEffectManager != null)
            {
                int p = personEffectManager.EnlargeProbabilityPercent;
                int r = Random.Range(0, 100);
                bool enlarge = r < p;
                float factor = enlarge
                    ? personEffectManager.EnlargeScaleFactor
                    : personEffectManager.ShrinkScaleFactor;
                if (factor <= 0f)
                {
                    factor = 1f;
                }

                committedScale.x *= factor;
                committedScale.y *= factor;
                committedScale.z *= factor;

                float enlargeSmooth = personEffectManager.EnlargeSmoothSeconds;
                if (!enlarge || enlargeSmooth <= 0f)
                {
                    rectTransform.localScale = committedScale;
                    scaleVelocity = Vector3.zero;
                }
            }

            float interval = personEffectManager != null
                ? personEffectManager.CatSeIntervalSeconds
                : 0f;
            Game02SeManager.TryGet()?.TryPlayCatMeowIfIntervalAllows(interval);
        }
    }
}
