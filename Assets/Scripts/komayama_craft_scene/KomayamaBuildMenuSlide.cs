using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// menu建設 用。MenuObject（ImageMenuSlide＋各 menu*＋BuildMenuPanel）を左右にスライド開閉する。
    /// BuildMenuPanel は MenuObject 配下に置き、相対位置は固定。移動量は slideTravel のみ。
    /// Item_0 で基礎加工台の配置モードへ入る（本段階）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaBuildMenuSlide : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [Tooltip("MenuObject（ImageMenuSlide＋menu*＋BuildMenuPanel）。この Rect の X だけを動かす。")]
        [FormerlySerializedAs("ribbonRoot")]
        [SerializeField] private RectTransform slideRoot;
        [SerializeField] private KomayamaBuildController buildController;
        [SerializeField] private Button firstFacilityButton;
        [SerializeField] private int firstFacilityBuildIndex;
        [Tooltip("閉じ(0)→開きで slideRoot が左へ動く量（px）。")]
        [FormerlySerializedAs("closedAnchoredX")]
        [SerializeField] private float slideTravel = 420f;
        [SerializeField] private float slideSeconds = 0.28f;
        [SerializeField] private bool startClosed = true;

        private bool isOpen;
        private bool animating;
        private Coroutine running;

        public bool IsOpen => isOpen;

        private float ClosedX => 0f;
        private float OpenX => -Mathf.Max(0f, slideTravel);

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

            if (slideRoot == null)
            {
                return;
            }

            if (startClosed)
            {
                ApplySlide(0f);
                isOpen = false;
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
            if (animating || slideRoot == null)
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
            if (slideRoot == null || isOpen)
            {
                return;
            }

            if (KomayamaCraftPlaceholderMenuController.Instance != null &&
                KomayamaCraftPlaceholderMenuController.Instance.IsOpen)
            {
                KomayamaCraftPlaceholderMenuController.Instance.CloseAll();
            }

            RestartSlide(true);
        }

        public void Close()
        {
            if (slideRoot == null || !isOpen)
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

            float from = isOpen ? 1f : 0f;
            float to = open ? 1f : 0f;
            float travel = Mathf.Abs(OpenX - ClosedX);
            if (travel > 0.01f)
            {
                from = Mathf.InverseLerp(ClosedX, OpenX, slideRoot.anchoredPosition.x);
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
            SetAnchoredX(slideRoot, Mathf.Lerp(ClosedX, OpenX, open01));
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
