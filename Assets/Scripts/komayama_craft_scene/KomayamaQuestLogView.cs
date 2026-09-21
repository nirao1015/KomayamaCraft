using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// クエストログ HUD。枠は Top／Middle／Bottom の3スプライト。
    /// 高さは内容に追従（最小〜親の最大）。超過時のみスクロール。
    /// 画面上の位置は QuestLog の Anchored Position（Inspector）が正。コードでは位置・アンカー・pivot を変更しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaQuestLogView : MonoBehaviour
    {
        [Serializable]
        public sealed class QuestDisplay
        {
            public string id = string.Empty;
            public string title = string.Empty;
            public string body = string.Empty;
        }

        [Serializable]
        public sealed class SampleQuest
        {
            public string id = "q1";
            public string title = "クエスト";
            [TextArea(2, 8)] public string body = "内容";
        }

        [Header("Roots")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private GameObject boardRoot;
        [SerializeField] private Button minimizeButton;
        [SerializeField] private Image minimizeButtonImage;
        [SerializeField] private TMP_Text minimizeButtonLabel;

        [Header("最小化時")]
        [SerializeField, Tooltip("閉じたときの帯（UI_クエストパネル枠-閉じた）。")]
        private GameObject minimizedBarRoot;
        [SerializeField] private RectTransform minimizedBarRect;
        [SerializeField] private Image minimizedBarImage;
        [SerializeField, Tooltip("展開中の MinimizeButton 画像（閉じる）。")]
        private Sprite minimizeButtonSpriteOpenBoard;
        [SerializeField, Tooltip("最小化中の MinimizeButton 画像（開く）。")]
        private Sprite minimizeButtonSpriteClosedBoard;

        [Header("Frame (Top / Middle / Bottom)")]
        [SerializeField] private RectTransform frameTop;
        [SerializeField] private RectTransform frameMiddle;
        [SerializeField] private RectTransform frameBottom;
        [SerializeField] private Image frameTopImage;
        [SerializeField] private Image frameMiddleImage;
        [SerializeField] private Image frameBottomImage;

        [Header("Background（内側・中エリアにフィット）")]
        [SerializeField] private RectTransform backgroundRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField, Min(0f), Tooltip("Background の左 inset（FrameMiddle より狭くする）。")]
        private float backgroundPaddingLeft = 10f;
        [SerializeField, Min(0f), Tooltip("Background の右 inset（FrameMiddle より狭くする）。")]
        private float backgroundPaddingRight = 10f;
        [SerializeField, Min(0f), Tooltip("Background の下 inset。丸枠に合わせて内側を開ける（既定 5）。")]
        private float backgroundPaddingBottom = 5f;
        [SerializeField, Min(0f), Tooltip("Background の上 inset。丸枠に合わせて内側を開ける（既定 5）。")]
        private float backgroundPaddingTop = 5f;

        [Header("Scroll")]
        [SerializeField] private RectTransform boardRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private LayoutElement boardLayoutElement;

        [Header("Row")]
        [SerializeField] private KomayamaQuestLogRow rowTemplate;

        [Header("高さ")]
        [SerializeField, Min(1f), Tooltip("内容が短くてもこれ未満にはしない（枠 Top+Bottom より大きく）。")]
        private float minBoardHeight = 120f;
        [SerializeField, Min(1f), Tooltip("内容が長くてもこれ以上にはしない（超過分はスクロール）。親の初期 Height と揃える。")]
        private float maxBoardHeight = 500f;

        [Header("文字領域の左右余白")]
        [SerializeField, Min(0f), Tooltip("ScrollView（文字）の左 inset。FrameMiddle 基準。")]
        private float contentPaddingLeft = 12f;
        [SerializeField, Min(0f), Tooltip("ScrollView（文字）の右 inset。FrameMiddle 基準。")]
        private float contentPaddingRight = 12f;

        [Header("レイアウト")]
        [SerializeField, Tooltip(
            "ON のとき実クエスト表示を抑え、下の Sample Data だけを出す（折返し・余白の確認用）。本番確認時は OFF。")]
        private bool useSampleQuestsForLayoutPreview;

        [Header("Sample Data（折返し確認用・上の Preview が ON のとき表示）")]
        [SerializeField] private List<SampleQuest> sampleQuests = new List<SampleQuest>();

        private readonly List<KomayamaQuestLogRow> rows = new List<KomayamaQuestLogRow>();
        private bool minimized;
        private bool presentationSuppressed;
        private string expandedQuestId = string.Empty;
        private Vector2 lastAppliedRootSize = new Vector2(-1f, -1f);
        private float lastContentHeight = -1f;

        private void Awake()
        {
            EnsureRootRect();
            if (rowTemplate != null)
            {
                rowTemplate.gameObject.SetActive(false);
            }

            if (minimizeButton != null)
            {
                minimizeButton.onClick.AddListener(ToggleMinimized);
            }

            if (minimizeButtonImage == null && minimizeButton != null)
            {
                minimizeButtonImage = minimizeButton.GetComponent<Image>();
            }

            // 展開中スプライト未設定なら、現在のボタン画像を閉じる用として覚える
            if (minimizeButtonSpriteOpenBoard == null && minimizeButtonImage != null)
            {
                minimizeButtonSpriteOpenBoard = minimizeButtonImage.sprite;
            }
        }

        private void OnDestroy()
        {
            if (minimizeButton != null)
            {
                minimizeButton.onClick.RemoveListener(ToggleMinimized);
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (useSampleQuestsForLayoutPreview)
            {
                RebuildRowsFromSample();
            }
            else
            {
                ClearRows();
                gameObject.SetActive(false);
            }

            ApplyMinimizedVisual();
            RefreshExpandState();
            Canvas.ForceUpdateCanvases();
            ApplyPanelLayout(force: true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureRootRect();
            // エディタで QuestLog 位置を動かしているときにレイアウトで干渉しない。
            // Play 中のみ高さ再計算する。
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                ApplyPanelLayout(force: true);
            };
        }
#endif

        /// <summary>クエストコントローラーから表示内容を差し替える。</summary>
        public void SetQuests(IReadOnlyList<QuestDisplay> quests)
        {
            if (useSampleQuestsForLayoutPreview)
            {
                return;
            }

            ClearRows();
            if (rowTemplate == null || content == null || quests == null || quests.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < quests.Count; i++)
            {
                QuestDisplay sample = quests[i];
                if (sample == null || string.IsNullOrEmpty(sample.id))
                {
                    continue;
                }

                KomayamaQuestLogRow row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.gameObject.name = "QuestRow_" + sample.id;
                row.Bind(HandleRowToggle);
                row.SetQuest(sample.id, sample.title, sample.body);
                rows.Add(row);
            }

            ResolveExpandedQuestId();
            gameObject.SetActive(rows.Count > 0 && !presentationSuppressed);
            if (!minimized && boardRoot != null)
            {
                boardRoot.SetActive(true);
            }

            RefreshExpandState();
            Canvas.ForceUpdateCanvases();
            ApplyPanelLayout(force: true);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || minimized)
            {
                return;
            }

            ApplyPanelLayout(force: false);
        }

        [ContextMenu("Rebuild Sample Rows")]
        public void RebuildRowsFromSample()
        {
            ClearRows();
            if (rowTemplate == null || content == null || sampleQuests == null)
            {
                return;
            }

            for (int i = 0; i < sampleQuests.Count; i++)
            {
                SampleQuest sample = sampleQuests[i];
                if (sample == null)
                {
                    continue;
                }

                KomayamaQuestLogRow row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.gameObject.name = "QuestRow_" + sample.id;
                row.Bind(HandleRowToggle);
                row.SetQuest(sample.id, sample.title, sample.body);
                rows.Add(row);
            }

            ResolveExpandedQuestId();
            gameObject.SetActive(rows.Count > 0 && !presentationSuppressed);
            RefreshExpandState();
            Canvas.ForceUpdateCanvases();
            ApplyPanelLayout(force: true);
        }

        public void SetPresentationSuppressed(bool suppressed)
        {
            if (presentationSuppressed == suppressed)
            {
                return;
            }

            presentationSuppressed = suppressed;
            bool wantVisible = rows.Count > 0 && !presentationSuppressed;
            if (gameObject.activeSelf != wantVisible)
            {
                gameObject.SetActive(wantVisible);
            }

            if (wantVisible && !minimized)
            {
                Canvas.ForceUpdateCanvases();
                ApplyPanelLayout(force: true);
            }
        }

        public void ToggleMinimized()
        {
            minimized = !minimized;
            ApplyMinimizedVisual();
            if (!minimized)
            {
                Canvas.ForceUpdateCanvases();
                ApplyPanelLayout(force: true);
            }
        }

        public bool ShouldBlockWorldZoom()
        {
            if (minimized || boardRoot == null || !boardRoot.activeInHierarchy)
            {
                return false;
            }

            RectTransform hitTarget = boardRect != null
                ? boardRect
                : (scrollRect != null ? scrollRect.transform as RectTransform : null);
            if (hitTarget == null)
            {
                return false;
            }

            return IsPointerOverRect(hitTarget);
        }

        private static bool IsPointerOverRect(RectTransform target)
        {
            if (target == null)
            {
                return false;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            Vector2 screen = mouse.position.ReadValue();
            Camera eventCamera = null;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(target, screen, eventCamera);
        }

        private void HandleRowToggle(string questId)
        {
            if (rows.Count <= 1)
            {
                return;
            }

            if (expandedQuestId == questId)
            {
                expandedQuestId = string.Empty;
            }
            else
            {
                expandedQuestId = questId;
            }

            RefreshExpandState();
            Canvas.ForceUpdateCanvases();
            ApplyPanelLayout(force: true);
        }

        private void ResolveExpandedQuestId()
        {
            if (rows.Count == 1)
            {
                expandedQuestId = rows[0].QuestId;
                return;
            }

            if (rows.Count < 2)
            {
                expandedQuestId = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(expandedQuestId))
            {
                expandedQuestId = rows[0].QuestId;
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].QuestId == expandedQuestId)
                {
                    return;
                }
            }

            expandedQuestId = rows[0].QuestId;
        }

        private void RefreshExpandState()
        {
            bool showToggles = rows.Count >= 2;
            for (int i = 0; i < rows.Count; i++)
            {
                KomayamaQuestLogRow row = rows[i];
                if (row == null)
                {
                    continue;
                }

                row.SetExpandControlsVisible(showToggles);
                bool expanded = !showToggles || row.QuestId == expandedQuestId;
                if (showToggles && string.IsNullOrEmpty(expandedQuestId))
                {
                    expanded = false;
                }

                if (!showToggles)
                {
                    expanded = true;
                }

                row.SetExpanded(expanded);
            }
        }

        private void ApplyMinimizedVisual()
        {
            if (boardRoot != null)
            {
                boardRoot.SetActive(!minimized);
            }

            if (minimizedBarRoot != null)
            {
                minimizedBarRoot.SetActive(minimized);
            }

            if (minimizeButtonImage != null)
            {
                Sprite sprite = minimized
                    ? minimizeButtonSpriteClosedBoard
                    : minimizeButtonSpriteOpenBoard;
                if (sprite != null)
                {
                    minimizeButtonImage.sprite = sprite;
                }
            }

            if (minimizeButtonLabel != null)
            {
                // スプライト側に記号がある想定。文字は出さない。
                minimizeButtonLabel.text = string.Empty;
                minimizeButtonLabel.gameObject.SetActive(false);
            }

            if (minimized)
            {
                ApplyMinimizedBarLayout();
            }
        }

        /// <summary>閉じた帯を親幅に合わせ、アスペクト比を保って高さを決める。</summary>
        private void ApplyMinimizedBarLayout()
        {
            EnsureRootRect();
            if (rootRect == null)
            {
                return;
            }

            float width = Mathf.Max(1f, rootRect.rect.width);
            float height = GetAspectHeight(minimizedBarImage, width);
            height = Mathf.Max(height, 40f);

            // サイズのみ変更。位置・アンカー・pivot は Inspector 値を維持する。
            PreserveRootPlacement(() =>
            {
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            });

            if (minimizedBarRect != null)
            {
                minimizedBarRect.anchorMin = new Vector2(0f, 0f);
                minimizedBarRect.anchorMax = new Vector2(1f, 1f);
                minimizedBarRect.offsetMin = Vector2.zero;
                minimizedBarRect.offsetMax = Vector2.zero;
                if (minimizedBarImage != null)
                {
                    minimizedBarImage.type = Image.Type.Simple;
                    minimizedBarImage.preserveAspect = false;
                    minimizedBarImage.raycastTarget = false;
                }
            }
        }

        /// <summary>
        /// 幅は親 Width 固定。高さは内容追従（min〜親 Height を最大）。枠 Middle が縦伸縮。
        /// rootRect の位置・アンカー・pivot は変更しない（画面配置は Inspector の QuestLog が正）。
        /// </summary>
        private void ApplyPanelLayout(bool force)
        {
            EnsureRootRect();
            if (rootRect == null || boardRect == null)
            {
                return;
            }

            if (minimized)
            {
                ApplyMinimizedBarLayout();
                return;
            }

            float width = Mathf.Max(1f, rootRect.rect.width);
            float maxHeight = Mathf.Max(minBoardHeight, maxBoardHeight);
            float topH = GetAspectHeight(frameTopImage, width);
            float bottomH = GetAspectHeight(frameBottomImage, width);
            float chrome = topH + bottomH;

            float contentHeight = 0f;
            if (content != null)
            {
                // 幅確定 → TMP 折返し → preferredHeight の順で測る
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                contentHeight = LayoutUtility.GetPreferredHeight(content);
            }

            float desired = contentHeight + chrome;
            float minH = Mathf.Max(minBoardHeight, topH + bottomH + 8f);
            float boardH = Mathf.Clamp(desired, minH, maxHeight);

            Vector2 size = new Vector2(width, boardH);
            if (!force &&
                Mathf.Abs(size.x - lastAppliedRootSize.x) < 0.1f &&
                Mathf.Abs(size.y - lastAppliedRootSize.y) < 0.1f &&
                Mathf.Abs(contentHeight - lastContentHeight) < 0.1f)
            {
                UpdateScrollOnly(boardH, contentHeight, chrome);
                return;
            }

            lastAppliedRootSize = size;
            lastContentHeight = contentHeight;

            // 親もボード高さに合わせ、下に空洞のヒット領域を残さない（位置は触らない）
            PreserveRootPlacement(() =>
            {
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, boardH);
            });

            boardRect.anchorMin = new Vector2(0f, 1f);
            boardRect.anchorMax = new Vector2(0f, 1f);
            boardRect.pivot = new Vector2(0f, 1f);
            boardRect.anchoredPosition = Vector2.zero;
            boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, boardH);

            if (boardLayoutElement != null)
            {
                boardLayoutElement.preferredWidth = width;
                boardLayoutElement.preferredHeight = boardH;
                boardLayoutElement.minWidth = width;
                boardLayoutElement.minHeight = boardH;
            }

            ApplyFramePieces(width, boardH, topH, bottomH);
            ApplyBackgroundToMiddleArea(topH, bottomH);
            UpdateScrollOnly(boardH, contentHeight, chrome);
        }

        /// <summary>
        /// SetSizeWithCurrentAnchors 等が placement をいじっても戻す。コード側では位置を決めない。
        /// </summary>
        private void PreserveRootPlacement(Action mutateSize)
        {
            if (rootRect == null || mutateSize == null)
            {
                return;
            }

            Vector2 anchorMin = rootRect.anchorMin;
            Vector2 anchorMax = rootRect.anchorMax;
            Vector2 pivot = rootRect.pivot;
            Vector2 anchoredPosition = rootRect.anchoredPosition;
            mutateSize();
            rootRect.anchorMin = anchorMin;
            rootRect.anchorMax = anchorMax;
            rootRect.pivot = pivot;
            rootRect.anchoredPosition = anchoredPosition;
        }

        private void ApplyFramePieces(float width, float boardH, float topH, float bottomH)
        {
            // Top / Bottom: 横幅＝ボード幅。高さはスプライト縦横比を維持（歪ませない）
            if (frameTop != null)
            {
                frameTop.anchorMin = new Vector2(0f, 1f);
                frameTop.anchorMax = new Vector2(1f, 1f);
                frameTop.pivot = new Vector2(0.5f, 1f);
                frameTop.anchoredPosition = Vector2.zero;
                frameTop.sizeDelta = new Vector2(0f, topH);
                if (frameTopImage != null)
                {
                    frameTopImage.type = Image.Type.Simple;
                    frameTopImage.preserveAspect = false;
                    frameTopImage.raycastTarget = false;
                }
            }

            if (frameBottom != null)
            {
                frameBottom.anchorMin = new Vector2(0f, 0f);
                frameBottom.anchorMax = new Vector2(1f, 0f);
                frameBottom.pivot = new Vector2(0.5f, 0f);
                frameBottom.anchoredPosition = Vector2.zero;
                frameBottom.sizeDelta = new Vector2(0f, bottomH);
                if (frameBottomImage != null)
                {
                    frameBottomImage.type = Image.Type.Simple;
                    frameBottomImage.preserveAspect = false;
                    frameBottomImage.raycastTarget = false;
                }
            }

            // Middle: Top/Bottom と同じ横幅（左右 0）、上下はアスペクト維持した Top/Bottom の内側
            if (frameMiddle != null)
            {
                frameMiddle.anchorMin = new Vector2(0f, 0f);
                frameMiddle.anchorMax = new Vector2(1f, 1f);
                frameMiddle.pivot = new Vector2(0.5f, 0.5f);
                frameMiddle.offsetMin = new Vector2(0f, bottomH);
                frameMiddle.offsetMax = new Vector2(0f, -topH);
                if (frameMiddleImage != null)
                {
                    frameMiddleImage.type = Image.Type.Simple;
                    frameMiddleImage.preserveAspect = false;
                    frameMiddleImage.raycastTarget = false;
                }
            }
        }

        /// <summary>
        /// 内側背景は枠の下に敷く（上下は Board いっぱい、左右だけ inset）。
        /// Top/Bottom 切り出しを広げても、角の透明部分に隙間が出ない。
        /// 文字の ScrollView は枠高さぶん inset。
        /// </summary>
        private void ApplyBackgroundToMiddleArea(float topH, float bottomH)
        {
            if (backgroundRect != null)
            {
                float bgPadL = Mathf.Max(0f, backgroundPaddingLeft);
                float bgPadR = Mathf.Max(0f, backgroundPaddingRight);
                float bgPadB = Mathf.Max(0f, backgroundPaddingBottom);
                float bgPadT = Mathf.Max(0f, backgroundPaddingTop);
                backgroundRect.anchorMin = new Vector2(0f, 0f);
                backgroundRect.anchorMax = new Vector2(1f, 1f);
                backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                backgroundRect.offsetMin = new Vector2(bgPadL, bgPadB);
                backgroundRect.offsetMax = new Vector2(-bgPadR, -bgPadT);

                if (backgroundImage != null)
                {
                    backgroundImage.raycastTarget = false;
                    backgroundImage.preserveAspect = false;
                }
            }

            if (scrollRect != null && scrollRect.transform is RectTransform scrollRt)
            {
                float padL = Mathf.Max(0f, contentPaddingLeft);
                float padR = Mathf.Max(0f, contentPaddingRight);
                scrollRt.anchorMin = new Vector2(0f, 0f);
                scrollRt.anchorMax = new Vector2(1f, 1f);
                scrollRt.pivot = new Vector2(0.5f, 0.5f);
                scrollRt.offsetMin = new Vector2(padL, bottomH);
                scrollRt.offsetMax = new Vector2(-padR, -topH);
            }
        }

        /// <summary>横幅に合わせたとき、スプライトの縦横比を保つ高さ。</summary>
        private static float GetAspectHeight(Image image, float width)
        {
            if (image == null || image.sprite == null)
            {
                return 30f;
            }

            Rect r = image.sprite.rect;
            if (r.width < 1f)
            {
                return Mathf.Max(1f, r.height);
            }

            return Mathf.Max(1f, width * (r.height / r.width));
        }

        private void UpdateScrollOnly(float boardH, float contentHeight, float chrome)
        {
            if (scrollRect == null)
            {
                return;
            }

            float viewportHeight = Mathf.Max(1f, boardH - chrome);
            bool needScroll = contentHeight > viewportHeight + 0.5f;
            scrollRect.vertical = needScroll;
            if (!needScroll)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void EnsureRootRect()
        {
            if (rootRect == null)
            {
                rootRect = transform as RectTransform;
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    Destroy(rows[i].gameObject);
                }
            }

            rows.Clear();

            if (content == null)
            {
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (rowTemplate != null && child == rowTemplate.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }
    }
}
