using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// menu建設 用。右端建設パネルとメニューリボン列を一緒にスライド開閉する。
    /// Item_0 で鱗圧延作業台の配置モードへ入る（本段階）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaBuildMenuSlide : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private RectTransform panel;
        [Tooltip("リボン列（MenuObject）。パネルと同じ距離だけ左右に追従する。")]
        [SerializeField] private RectTransform ribbonRoot;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private Button firstFacilityButton;
        [SerializeField] private int firstFacilityBuildIndex;
        [SerializeField] private float closedAnchoredX = 520f;
        [SerializeField] private float openAnchoredX = 0f;
        [SerializeField] private float slideSeconds = 0.28f;
        [SerializeField] private bool startClosed = true;

        private bool isOpen;
        private bool animating;
        private Coroutine running;
        private float ribbonClosedX;
        private float ribbonOpenX;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (openButton != null)
            {
                openButton.onClick.AddListener(Toggle);
            }

            if (firstFacilityButton != null)
            {
                firstFacilityButton.onClick.AddListener(BeginFirstFacilityBuild);
            }

            CacheRibbonPositions();

            if (panel == null)
            {
                return;
            }

            if (startClosed)
            {
                ApplySlide(0f);
                isOpen = false;
                panel.gameObject.SetActive(true);
            }
            else
            {
                ApplySlide(1f);
                isOpen = true;
            }
        }

        private void OnDestroy()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(Toggle);
            }

            if (firstFacilityButton != null)
            {
                firstFacilityButton.onClick.RemoveListener(BeginFirstFacilityBuild);
            }
        }

        public void Toggle()
        {
            if (animating || panel == null)
            {
                return;
            }

            KomayamaQuestController quest = KomayamaQuestController.Instance;
            if (quest != null && !quest.AreBuildAndSettingsUnlocked)
            {
                return;
            }

            CancelBuildIfNeeded();

            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (panel == null || isOpen)
            {
                return;
            }

            RestartSlide(true);
        }

        public void Close()
        {
            if (panel == null || !isOpen)
            {
                return;
            }

            RestartSlide(false);
        }

        /// <summary>
        /// パネルを閉じ、指定インデックスの施設を配置モードにする。
        /// </summary>
        public void BeginFirstFacilityBuild()
        {
            if (buildController == null)
            {
                return;
            }

            buildController.SelectBuildIndex(firstFacilityBuildIndex);
            if (isOpen)
            {
                Close();
            }
        }

        private void CancelBuildIfNeeded()
        {
            if (buildController != null &&
                buildController.Mode == KomayamaInputMode.Build)
            {
                buildController.SetMode(KomayamaInputMode.Field);
            }
        }

        private void CacheRibbonPositions()
        {
            if (ribbonRoot == null)
            {
                ribbonClosedX = 0f;
                ribbonOpenX = 0f;
                return;
            }

            ribbonClosedX = ribbonRoot.anchoredPosition.x;
            float panelTravel = closedAnchoredX - openAnchoredX;
            ribbonOpenX = ribbonClosedX - panelTravel;
        }

        private void RestartSlide(bool open)
        {
            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(SlideRoutine(open));
        }

        private IEnumerator SlideRoutine(bool open)
        {
            animating = true;
            panel.gameObject.SetActive(true);

            float from = isOpen ? 1f : 0f;
            float to = open ? 1f : 0f;
            if (Mathf.Abs(closedAnchoredX - openAnchoredX) > 0.01f)
            {
                from = Mathf.InverseLerp(closedAnchoredX, openAnchoredX, panel.anchoredPosition.x);
            }

            float duration = Mathf.Max(0.01f, slideSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                ApplySlide(Mathf.Lerp(from, to, t));
                yield return null;
            }

            ApplySlide(to);
            isOpen = open;
            animating = false;
            running = null;
        }

        private void ApplySlide(float open01)
        {
            float panelX = Mathf.Lerp(closedAnchoredX, openAnchoredX, open01);
            SetAnchoredX(panel, panelX);

            if (ribbonRoot != null)
            {
                SetAnchoredX(ribbonRoot, Mathf.Lerp(ribbonClosedX, ribbonOpenX, open01));
            }
        }

        private static void SetAnchoredX(RectTransform target, float x)
        {
            if (target == null)
            {
                return;
            }

            Vector2 pos = target.anchoredPosition;
            pos.x = x;
            target.anchoredPosition = pos;
        }
    }
}
