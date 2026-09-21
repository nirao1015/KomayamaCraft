using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// メニューアイコン用ホバーガイド（半透明黒背景＋タイトル／本文）。
    /// 表示遅延は本コンポーネントの Inspector（グローバル）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaMenuHoverGuidePresenter : MonoBehaviour
    {
        public static KomayamaMenuHoverGuidePresenter Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text guideText;
        [SerializeField] private Canvas rootCanvas;

        [Header("グローバル（表示タイミング）")]
        [SerializeField, Min(0f), InspectorName("ホバーから表示までの秒数")]
        private float showDelaySeconds = 0.35f;

        [Header("レイアウト")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(12f, -12f);
        [SerializeField, Min(0f)] private float paddingLeft = 14f;
        [SerializeField, Min(0f)] private float paddingRight = 14f;
        [SerializeField, Min(0f)] private float paddingTop = 2f;
        [SerializeField, Min(0f)] private float paddingBottom = 2f;
        [SerializeField, Min(0f)] private float titleGuideSpacing = 2f;
        [SerializeField, Min(40f)] private float maxWidth = 420f;

        private Coroutine showRoutine;
        private KomayamaMenuHoverGuideHit activeHit;
        private bool visible;
        private Camera eventCamera;

        public float ShowDelaySeconds => Mathf.Max(0f, showDelaySeconds);

        private void Awake()
        {
            Instance = this;
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!visible || tooltipRoot == null)
            {
                return;
            }

            SetTooltipPosition(GetPointerScreenPosition());
        }

        public void RequestShow(KomayamaMenuHoverGuideHit hit)
        {
            if (hit == null || !isActiveAndEnabled)
            {
                return;
            }

            activeHit = hit;
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(ShowAfterDelay(hit));
        }

        public void RequestHide(KomayamaMenuHoverGuideHit hit)
        {
            if (hit != null && activeHit != null && hit != activeHit)
            {
                return;
            }

            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            activeHit = null;
            HideImmediate();
        }

        private IEnumerator ShowAfterDelay(KomayamaMenuHoverGuideHit hit)
        {
            float delay = ShowDelaySeconds;
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (activeHit != hit || hit == null)
            {
                showRoutine = null;
                yield break;
            }

            ApplyContent(hit);
            SetTooltipPosition(GetPointerScreenPosition());
            SetVisible(true);
            showRoutine = null;
        }

        private void ApplyContent(KomayamaMenuHoverGuideHit hit)
        {
            if (titleText != null)
            {
                if (hit.Font != null)
                {
                    titleText.font = hit.Font;
                }

                titleText.fontSize = hit.TitleFontSize;
                titleText.text = hit.Title ?? string.Empty;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(titleText.text));
            }

            if (guideText != null)
            {
                if (hit.Font != null)
                {
                    guideText.font = hit.Font;
                }

                guideText.fontSize = hit.GuideFontSize;
                guideText.text = hit.Guide ?? string.Empty;
                guideText.gameObject.SetActive(!string.IsNullOrEmpty(guideText.text));
            }

            RebuildSize();
        }

        private void RebuildSize()
        {
            if (tooltipRoot == null)
            {
                return;
            }

            float contentWidthLimit = Mathf.Max(40f, maxWidth - paddingLeft - paddingRight);
            float titleW = 0f;
            float titleH = 0f;
            float guideW = 0f;
            float guideH = 0f;

            if (titleText != null && titleText.gameObject.activeSelf)
            {
                Vector2 pref = titleText.GetPreferredValues(titleText.text, contentWidthLimit, 0f);
                titleW = Mathf.Min(pref.x, contentWidthLimit);
                titleH = titleText.GetPreferredValues(titleText.text, titleW, 0f).y;
            }

            if (guideText != null && guideText.gameObject.activeSelf)
            {
                Vector2 pref = guideText.GetPreferredValues(guideText.text, contentWidthLimit, 0f);
                guideW = Mathf.Min(pref.x, contentWidthLimit);
                guideH = guideText.GetPreferredValues(guideText.text, guideW, 0f).y;
            }

            float innerW = Mathf.Max(titleW, guideW);
            float innerH = titleH + guideH;
            if (titleH > 0f && guideH > 0f)
            {
                innerH += titleGuideSpacing;
            }

            float width = innerW + paddingLeft + paddingRight;
            float height = innerH + paddingTop + paddingBottom;
            tooltipRoot.sizeDelta = new Vector2(width, height);

            float y = -paddingTop;
            if (titleText != null && titleText.gameObject.activeSelf)
            {
                RectTransform tr = titleText.rectTransform;
                tr.anchorMin = new Vector2(0f, 1f);
                tr.anchorMax = new Vector2(1f, 1f);
                tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = new Vector2(0f, y);
                tr.sizeDelta = new Vector2(-(paddingLeft + paddingRight), titleH);
                y -= titleH + (guideH > 0f ? titleGuideSpacing : 0f);
            }

            if (guideText != null && guideText.gameObject.activeSelf)
            {
                RectTransform gr = guideText.rectTransform;
                gr.anchorMin = new Vector2(0f, 1f);
                gr.anchorMax = new Vector2(1f, 1f);
                gr.pivot = new Vector2(0.5f, 1f);
                gr.anchoredPosition = new Vector2(0f, y);
                gr.sizeDelta = new Vector2(-(paddingLeft + paddingRight), guideH);
            }
        }

        private void SetTooltipPosition(Vector2 screenPosition)
        {
            if (tooltipRoot == null)
            {
                return;
            }

            RectTransform parent = tooltipRoot.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Camera cam = ResolveEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenPosition + cursorOffset,
                    cam,
                    out Vector2 local))
            {
                return;
            }

            tooltipRoot.anchoredPosition = local;
            ClampToParent(parent);
        }

        private void ClampToParent(RectTransform parent)
        {
            Vector2 size = tooltipRoot.sizeDelta;
            Vector2 pivot = tooltipRoot.pivot;
            Rect prect = parent.rect;
            Vector2 pos = tooltipRoot.anchoredPosition;

            float minX = prect.xMin + size.x * pivot.x;
            float maxX = prect.xMax - size.x * (1f - pivot.x);
            float minY = prect.yMin + size.y * pivot.y;
            float maxY = prect.yMax - size.y * (1f - pivot.y);

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            tooltipRoot.anchoredPosition = pos;
        }

        private void SetVisible(bool on)
        {
            visible = on;
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(on);
            }
        }

        private void HideImmediate()
        {
            SetVisible(false);
        }

        private Camera ResolveEventCamera()
        {
            if (eventCamera != null)
            {
                return eventCamera;
            }

            if (rootCanvas != null)
            {
                if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    eventCamera = null;
                    return null;
                }

                eventCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
                return eventCamera;
            }

            eventCamera = Camera.main;
            return eventCamera;
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Input.mousePosition;
        }
    }
}
