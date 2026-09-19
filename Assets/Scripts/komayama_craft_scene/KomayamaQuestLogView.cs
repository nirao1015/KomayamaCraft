using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// クエストログ HUD。親 QuestLog のピクセルサイズを正とし、子ボードが追従する。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
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
        [SerializeField] private TMP_Text minimizeButtonLabel;

        [Header("Scroll")]
        [SerializeField] private RectTransform boardRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private LayoutElement boardLayoutElement;

        [Header("Row")]
        [SerializeField] private KomayamaQuestLogRow rowTemplate;

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
            if (!isActiveAndEnabled)
            {
                return;
            }

            // エディタで親サイズを変えたとき、子へすぐ反映する
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
            // 折返し確認中はサンプル固定。実クエストで上書きしない。
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

            if (rows.Count == 1)
            {
                expandedQuestId = rows[0].QuestId;
            }
            else if (rows.Count >= 2 && string.IsNullOrEmpty(expandedQuestId))
            {
                expandedQuestId = rows[0].QuestId;
            }
            else if (rows.Count >= 2)
            {
                bool stillExists = false;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].QuestId == expandedQuestId)
                    {
                        stillExists = true;
                        break;
                    }
                }

                if (!stillExists)
                {
                    expandedQuestId = rows[0].QuestId;
                }
            }

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
            if (!minimized)
            {
                ApplyPanelLayout(force: false);
            }
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

            if (rows.Count == 1)
            {
                expandedQuestId = rows[0].QuestId;
            }
            else if (rows.Count >= 2 && string.IsNullOrEmpty(expandedQuestId))
            {
                expandedQuestId = rows[0].QuestId;
            }

            gameObject.SetActive(rows.Count > 0 && !presentationSuppressed);
            RefreshExpandState();
            Canvas.ForceUpdateCanvases();
            ApplyPanelLayout(force: true);
        }

        /// <summary>
        /// 会話・OP・演出中など、フィールド HUD を一時非表示にする。
        /// </summary>
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

        /// <summary>
        /// ボード展開中かつポインタがクエスト欄上にあるとき、大陸ズームを抑止する。
        /// </summary>
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

            if (minimizeButtonLabel != null)
            {
                minimizeButtonLabel.text = minimized ? "□" : "—";
            }
        }

        /// <summary>
        /// 親 QuestLog のピクセルサイズを正とし Board だけ合わせる。
        /// ScrollView 余白・テキスト開始／終端は Inspector 配置を尊重し上書きしない。
        /// </summary>
        private void ApplyPanelLayout(bool force)
        {
            EnsureRootRect();
            if (rootRect == null || boardRect == null)
            {
                return;
            }

            float width = Mathf.Max(1f, rootRect.rect.width);
            float height = Mathf.Max(1f, rootRect.rect.height);
            Vector2 size = new Vector2(width, height);
            if (!force &&
                Mathf.Abs(size.x - lastAppliedRootSize.x) < 0.1f &&
                Mathf.Abs(size.y - lastAppliedRootSize.y) < 0.1f)
            {
                UpdateScrollOnly();
                return;
            }

            lastAppliedRootSize = size;

            // Board は親と同サイズ（親の Width/Height がピクセル指定の正）
            boardRect.anchorMin = new Vector2(0f, 1f);
            boardRect.anchorMax = new Vector2(0f, 1f);
            boardRect.pivot = new Vector2(0f, 1f);
            boardRect.anchoredPosition = Vector2.zero;
            boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (boardLayoutElement != null)
            {
                boardLayoutElement.preferredWidth = width;
                boardLayoutElement.preferredHeight = height;
                boardLayoutElement.minWidth = width;
                boardLayoutElement.minHeight = height;
            }

            UpdateScrollOnly();
        }

        private void UpdateScrollOnly()
        {
            if (scrollRect == null || content == null || boardRect == null)
            {
                return;
            }

            float contentHeight = LayoutUtility.GetPreferredHeight(content);
            float viewportHeight = boardRect.rect.height;
            if (scrollRect.transform is RectTransform scrollRt)
            {
                // Inspector で付けた Left/Right/Top/Bottom をそのまま使う
                viewportHeight = Mathf.Max(
                    1f,
                    boardRect.rect.height - scrollRt.offsetMin.y + scrollRt.offsetMax.y);
            }

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
