using TMPro;
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game02
{
    [DisallowMultipleComponent]
    public sealed class DebugAutoPlayManager : MonoBehaviour
    {
        [Header("Status UI")]
        [SerializeField] private TMP_Text autoPlayStatusText;
        [SerializeField] private string autoPlayOnMessage = "AutoPlayON";
        [SerializeField] private float decisionIntervalSeconds = 0.5f;
        [SerializeField] private float actionCooldownSeconds = 3f;
        [SerializeField] private float logIntervalSeconds = 60f;
        [SerializeField] private bool enableDebugTraceLog = true;
        [SerializeField] private float editorBuySavingsRatio = 0.8f;

        private static bool hasAutoConfiguredSceneDebugAutoPlay;
        private static DebugAutoPlayManager activeInstance;
        private float decisionAccumulator;
        private float actionCooldownRemainingSeconds;
        private float logAccumulator;
        private float previousGameplayElapsedSecondsForLog = -1f;
        private bool metricsInitialized;
        private long previousMoney;
        private long previousPopularity;
        private long totalMoneyEarnedIncludingInitial;
        private long totalMoneySpent;
        private long totalPopularityEarnedIncludingInitial;
        private long totalBuzzCount;
        private long totalStreamingCount;
        private long totalEditCount;
        private long totalMovieUploadCount;
        private string logFilePath;

        public static void EnsureSceneController()
        {
            if (hasAutoConfiguredSceneDebugAutoPlay)
            {
                return;
            }

            hasAutoConfiguredSceneDebugAutoPlay = true;
            DebugAutoPlayManager[] existingManagers = FindObjectsByType<DebugAutoPlayManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingManagers != null && existingManagers.Length > 0)
            {
                return;
            }

            GameObject debugPanel = GameObject.Find("PanelCanvas/PanelObject/DebugPanel");
            if (debugPanel == null)
            {
                debugPanel = FindInactiveByName("DebugPanel");
                if (debugPanel == null)
                {
                    return;
                }
            }

            if (debugPanel.GetComponent<DebugAutoPlayManager>() == null)
            {
                DebugAutoPlayManager created = debugPanel.AddComponent<DebugAutoPlayManager>();
                // 明示的に有効化するまで自動再生しない。
                created.enabled = false;
            }
        }

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Destroy(this);
                return;
            }

            activeInstance = this;
            ResolveRefsIfNeeded();
            ApplyStatusLabel();
        }

        private void OnEnable()
        {
            ResolveRefsIfNeeded();
            ApplyStatusLabel();
            decisionAccumulator = 0f;
            actionCooldownRemainingSeconds = 0f;
            logAccumulator = 0f;
            previousGameplayElapsedSecondsForLog = -1f;
            metricsInitialized = false;
            // ドメインリロード無効時の重複購読対策。
            UnsubscribeMetricsEvents();
            SubscribeMetricsEvents();
            EnsureLogPath();
            WriteStartupLogLine();
        }

        private void OnDisable()
        {
            UnsubscribeMetricsEvents();
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            if (autoPlayStatusText != null)
            {
                autoPlayStatusText.text = string.Empty;
            }
        }

        private void Update()
        {
            if (!enabled)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null ||
                gm.HasFatalError ||
                gm.IsPreGameSequenceActive ||
                gm.IsPaused ||
                gm.IsGameCleared)
            {
                return;
            }

            EnsureMetricsInitialized(gm);
            UpdateDeltaMetrics(gm);
            UpdateLogIfNeeded(gm);

            if (actionCooldownRemainingSeconds > 0f)
            {
                actionCooldownRemainingSeconds = Mathf.Max(0f, actionCooldownRemainingSeconds - Time.unscaledDeltaTime);
                return;
            }

            decisionAccumulator += Time.unscaledDeltaTime;
            if (decisionAccumulator < Mathf.Max(0.01f, decisionIntervalSeconds))
            {
                return;
            }

            decisionAccumulator = 0f;
            bool performed = RunInitialAutoPlayStep();
            if (performed)
            {
                actionCooldownRemainingSeconds = Mathf.Max(0f, actionCooldownSeconds);
            }
        }

        private void ResolveRefsIfNeeded()
        {
            if (autoPlayStatusText != null)
            {
                return;
            }

            Transform byPath = transform.Find("AutoPlayText (TMP)");
            if (byPath != null)
            {
                autoPlayStatusText = byPath.GetComponent<TMP_Text>();
                if (autoPlayStatusText != null)
                {
                    return;
                }
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "AutoPlayText (TMP)")
                {
                    continue;
                }

                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null)
                {
                    autoPlayStatusText = text;
                    return;
                }
            }
        }

        private void ApplyStatusLabel()
        {
            if (autoPlayStatusText == null)
            {
                return;
            }

            autoPlayStatusText.text = enabled ? autoPlayOnMessage : string.Empty;
        }

        private bool RunInitialAutoPlayStep()
        {
            bool performed = false;
            if (TryHandlePriorityActions())
            {
                performed = true;
            }

            if (!performed)
            {
                if (TryHandleInitialStartupBehavior())
                {
                    performed = true;
                }
                else
                {
                    performed = TryHandleNormalBehavior();
                }
            }

            if (!performed)
            {
                performed = TryHandleImmediatePurchases();
            }

            if (!performed)
            {
                performed = TryHandleMovieUpload();
            }

            if (!performed)
            {
                performed = TryHandleTrashCleanup();
            }

            return performed;
        }

        private static bool IsEditorBuy01Purchased()
        {
            EditorBuyController[] buys = FindObjectsByType<EditorBuyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buys.Length; i++)
            {
                EditorBuyController c = buys[i];
                if (c == null)
                {
                    continue;
                }

                if (c.TargetItemType == ItemType.ItemEditor01)
                {
                    return c.IsPurchased;
                }
            }

            return false;
        }

        private static bool IsEditorBuyPurchased(ItemType editorType)
        {
            EditorBuyController[] buys = FindObjectsByType<EditorBuyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buys.Length; i++)
            {
                EditorBuyController c = buys[i];
                if (c != null && c.TargetItemType == editorType)
                {
                    return c.IsPurchased;
                }
            }

            return false;
        }

        private static bool IsInitialUploadCapacityAvailable()
        {
            WorkMovieUploadController[] slots = FindObjectsByType<WorkMovieUploadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            WorkMovieUploadController slot2 = null;
            for (int i = 0; i < slots.Length; i++)
            {
                WorkMovieUploadController c = slots[i];
                if (c != null && c.SlotIndex == 2)
                {
                    slot2 = c;
                    break;
                }
            }

            if (slot2 == null)
            {
                return true;
            }

            return !slot2.HasAnyWorkingSession();
        }

        private static WorkEditor FindWorkEditor1()
        {
            WorkEditor[] editors = FindObjectsByType<WorkEditor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < editors.Length; i++)
            {
                WorkEditor e = editors[i];
                if (e != null && e.name == "WorkEditor_1")
                {
                    return e;
                }
            }

            return null;
        }

        private static WorkEditor FindWorkEditor2()
        {
            WorkEditor[] editors = FindObjectsByType<WorkEditor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < editors.Length; i++)
            {
                WorkEditor e = editors[i];
                if (e != null && e.name == "WorkEditor_2")
                {
                    return e;
                }
            }

            return null;
        }

        private static void EnsureMailCharaAssignedToEditor(WorkEditor editor)
        {
            if (editor == null)
            {
                return;
            }

            DraggableItemController mail = FindFirstItem(ItemType.ItemMailChara, "ItemMailChara");
            if (mail == null)
            {
                return;
            }

            TryDropItemToWorkplace(editor, mail);
        }

        private static void EnsureMailCharaExtractedFromEditor(WorkEditor editor)
        {
            if (editor == null)
            {
                return;
            }

            if (FindFirstItem(ItemType.ItemMailChara, "ItemMailChara") != null)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            RectTransform rect = editor.transform as RectTransform;
            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (rect != null)
            {
                Canvas canvas = rect.GetComponentInParent<Canvas>();
                Camera cam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                }

                screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
            }

            PointerEventData eventData = new PointerEventData(eventSystem)
            {
                position = screenPoint,
                button = PointerEventData.InputButton.Left
            };

            editor.OnBeginDrag(eventData);
            editor.OnEndDrag(eventData);
        }

        private static bool TryDropItemToWorkplace(IWorkplaceTarget workplace, DraggableItemController item)
        {
            if (workplace == null || item == null)
            {
                return false;
            }

            if (!workplace.CanAcceptItem(item))
            {
                return false;
            }

            workplace.OnItemDropped(item);
            if (item != null)
            {
                UnityEngine.Object.Destroy(item.gameObject);
            }

            return true;
        }

        private bool TryHandlePriorityActions()
        {
            if (!IsEditorBuy01Purchased())
            {
                return TryClickEditorBuy(ItemType.ItemEditor01);
            }

            WorkEditor editor1 = FindWorkEditor1();
            if (editor1 != null)
            {
                DraggableItemController e01 = FindFirstItem(ItemType.ItemEditor01, "ItemEditor01");
                if (e01 != null && TryDropItemToWorkplace(editor1, e01))
                {
                    return true;
                }
            }

            if (IsEditorBuyPurchased(ItemType.ItemEditor02))
            {
                WorkEditor editor2 = FindWorkEditor2();
                DraggableItemController e02 = FindFirstItem(ItemType.ItemEditor02, "ItemEditor02");
                if (editor2 != null && e02 != null && TryDropItemToWorkplace(editor2, e02))
                {
                    return true;
                }
            }

            if (IsEditorBuyPurchased(ItemType.ItemEditor03))
            {
                if (editor1 == null)
                {
                    return false;
                }

                DraggableItemController e03 = FindFirstItem(ItemType.ItemEditor03, "ItemEditor03");
                if (e03 == null)
                {
                    return false;
                }

                if (TryDropItemToWorkplace(editor1, e03))
                {
                    return true;
                }

                EnsureMailCharaExtractedFromEditor(editor1);
                return TryDropItemToWorkplace(editor1, e03);
            }

            return false;
        }

        private bool TryHandleInitialStartupBehavior()
        {
            if (IsEditorBuy01Purchased())
            {
                return false;
            }

            if (!IsInitialUploadCapacityAvailable())
            {
                return false;
            }

            WorkEditor editor1 = FindWorkEditor1();
            WorkStreaming streaming = FindAnyObjectByType<WorkStreaming>();
            if (editor1 == null || streaming == null)
            {
                return false;
            }

            DraggableItemController streamItem = FindHighestPopularityStream();
            if (streamItem != null)
            {
                EnsureMailCharaAssignedToEditor(editor1);
                streamItem = FindHighestPopularityStream();
                if (streamItem != null && TryDropItemToWorkplace(editor1, streamItem))
                {
                    return true;
                }

                return false;
            }

            EnsureMailCharaExtractedFromEditor(editor1);
            DraggableItemController mail = FindFirstItem(ItemType.ItemMailChara, "ItemMailChara");
            return mail != null && TryDropItemToWorkplace(streaming, mail);
        }

        private bool TryHandleNormalBehavior()
        {
            if (!IsEditorBuy01Purchased())
            {
                return false;
            }

            WorkStreaming streaming = FindAnyObjectByType<WorkStreaming>();
            if (streaming == null)
            {
                return false;
            }

            // 仕様 debug_autoplay_spec.md 「通常の挙動」1: アップグレード仕事場にメール投入可能なら最優先
            if (TryDoUpgradeWorkplaceWithMail())
            {
                return true;
            }

            // 2–3: アップグレードができないときは配信仕事（素材数 ≤2 / >2 はいずれもここで処理）
            DraggableItemController mail = FindFirstItem(ItemType.ItemMailChara, "ItemMailChara");
            if (mail != null && TryDropItemToWorkplace(streaming, mail))
            {
                return true;
            }

            // 4–5: Ed51 購入済みは自動投入に委譲。未購入かつ編集者配置済みなら編集仕事
            UpgradesManager upgrades = UpgradesManager.Instance;
            bool ed51Purchased = upgrades != null && upgrades.IsWorkUpgradeEd51Purchased();
            if (!ed51Purchased)
            {
                DraggableItemController stream = FindHighestPopularityStream();
                if (stream != null)
                {
                    WorkEditor[] editors = { FindWorkEditor1(), FindWorkEditor2() };
                    for (int i = 0; i < editors.Length; i++)
                    {
                        WorkEditor editor = editors[i];
                        if (editor == null || !editor.HasEditorAssigned)
                        {
                            continue;
                        }

                        if (TryDropItemToWorkplace(editor, stream))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryDoUpgradeWorkplaceWithMail()
        {
            DraggableItemController mail = FindFirstItem(ItemType.ItemMailChara, "ItemMailChara");
            if (mail == null)
            {
                return false;
            }

            IWorkplaceTarget[] workplaces =
            {
                FindAnyObjectByType<WorkUpgradeSt01Controller>(),
                FindAnyObjectByType<WorkUpgradeSt02Controller>(),
                FindAnyObjectByType<WorkUpgradeSt03Controller>(),
                FindAnyObjectByType<WorkUpgradeSt04Controller>()
            };

            for (int i = 0; i < workplaces.Length; i++)
            {
                if (TryDropItemToWorkplace(workplaces[i], mail))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleImmediatePurchases()
        {
            // 要件: EditorBuy01 を購入するまでは他アップグレードを買わずに貯金する。
            if (!IsEditorBuy01Purchased())
            {
                return false;
            }

            if (TryClickBestEditorBuy())
            {
                return true;
            }

            WorkUpgradeEdBuyControllerBase[] edBuys = FindObjectsByType<WorkUpgradeEdBuyControllerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(edBuys, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
            for (int i = 0; i < edBuys.Length; i++)
            {
                WorkUpgradeEdBuyControllerBase c = edBuys[i];
                if (c == null || c.IsSoldOut)
                {
                    continue;
                }

                if (GameManager.Instance != null && GameManager.Instance.CurrentMoney < c.GetCurrentSalePrice())
                {
                    continue;
                }

                // 一時的に停止: 次Editor貯金ロジック
                // if (!CanSpendForNonEditorUpgrade(c.GetCurrentSalePrice()))
                // {
                //     continue;
                // }

                if (TryInvokePointerClick(c.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanSpendForNonEditorUpgrade(long spendPrice)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return true;
            }

            long nextPrice = FindNextEditorBuyPrice(out ItemType nextEditorType);
            if (nextPrice <= 0L)
            {
                return true;
            }

            // Ed03 は価格帯が大きく、ここで厳格に貯金すると他アップグレードが止まりやすい。
            // 貯金比率は「次が Ed02 のとき」に限定して適用する。
            if (nextEditorType != ItemType.ItemEditor02)
            {
                return true;
            }

            double ratio = Math.Max(0d, editorBuySavingsRatio);
            long reserveTarget = (long)Math.Ceiling(nextPrice * ratio);
            long afterSpend = gm.CurrentMoney - Math.Max(0L, spendPrice);
            return afterSpend >= reserveTarget;
        }

        private static long FindNextEditorBuyPrice(out ItemType nextEditorType)
        {
            nextEditorType = ItemType.Unknown;
            ItemType[] order = { ItemType.ItemEditor01, ItemType.ItemEditor02, ItemType.ItemEditor03 };
            EditorBuyController[] buys = FindObjectsByType<EditorBuyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < order.Length; i++)
            {
                ItemType target = order[i];
                for (int j = 0; j < buys.Length; j++)
                {
                    EditorBuyController c = buys[j];
                    if (c == null || c.TargetItemType != target || c.IsPurchased)
                    {
                        continue;
                    }

                    nextEditorType = target;
                    return Math.Max(0L, c.GetCurrentSalePrice());
                }
            }

            return 0L;
        }

        private bool TryClickBestEditorBuy()
        {
            ItemType[] order = { ItemType.ItemEditor01, ItemType.ItemEditor02, ItemType.ItemEditor03 };
            for (int i = 0; i < order.Length; i++)
            {
                if (TryClickEditorBuy(order[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryClickEditorBuy(ItemType itemType)
        {
            EditorBuyController[] buys = FindObjectsByType<EditorBuyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buys.Length; i++)
            {
                EditorBuyController c = buys[i];
                if (c == null || c.TargetItemType != itemType || c.IsPurchased)
                {
                    continue;
                }

                if (GameManager.Instance != null && GameManager.Instance.CurrentMoney < c.GetCurrentSalePrice())
                {
                    continue;
                }

                return TryInvokePointerClick(c.gameObject);
            }

            return false;
        }

        private bool TryHandleMovieUpload()
        {
            DraggableItemController movie = FindFirstItem(ItemType.ItemMovie, "ItemMovie_");
            if (movie == null)
            {
                return false;
            }

            WorkMovieUploadController[] uploads = FindObjectsByType<WorkMovieUploadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            WorkMovieUploadController slot1 = null;
            WorkMovieUploadController slot2 = null;
            for (int i = 0; i < uploads.Length; i++)
            {
                WorkMovieUploadController c = uploads[i];
                if (c == null)
                {
                    continue;
                }

                if (c.SlotIndex == 1)
                {
                    slot1 = c;
                }
                else if (c.SlotIndex == 2)
                {
                    slot2 = c;
                }
            }

            if (slot1 == null)
            {
                return false;
            }

            bool isInitialFull = slot1.HasAnyWorkingSession() && slot2 != null && slot2.HasAnyWorkingSession();
            if (enableDebugTraceLog)
            {
                int movieCountBefore = CountItems(ItemType.ItemMovie, "ItemMovie_");
                Debug.Log($"[DebugAutoPlay] MovieUploadCheck movieCount={movieCountBefore} slot1Working={slot1.HasAnyWorkingSession()} slot2Working={(slot2 != null && slot2.HasAnyWorkingSession())} slot1Stage={slot1.GetCurrentHeadSessionStage()} initialFull={isInitialFull}");
            }

            if (!isInitialFull)
            {
                bool dropped = TryDropItemToWorkplace(slot1, movie);
                if (enableDebugTraceLog)
                {
                    int movieCountAfter = CountItems(ItemType.ItemMovie, "ItemMovie_");
                    Debug.Log($"[DebugAutoPlay] MovieUploadTry target={slot1.name} dropped={dropped} movieCountAfter={movieCountAfter}");
                }

                return dropped;
            }

            int stage = slot1.GetCurrentHeadSessionStage();
            if (stage >= 2)
            {
                bool dropped = TryDropItemToWorkplace(slot1, movie);
                if (enableDebugTraceLog)
                {
                    int movieCountAfter = CountItems(ItemType.ItemMovie, "ItemMovie_");
                    Debug.Log($"[DebugAutoPlay] MovieUploadTry(full) target={slot1.name} dropped={dropped} movieCountAfter={movieCountAfter}");
                }

                return dropped;
            }

            return false;
        }

        private bool TryHandleTrashCleanup()
        {
            TrashDropTarget trash = TrashDropTarget.FindActiveTarget();
            if (trash == null)
            {
                return false;
            }

            // 仕様: 所持限界数-1 で破棄開始
            int streamCount = CountItems(ItemType.ItemStream, "ItemStream_");
            if (streamCount >= 7)
            {
                DraggableItemController lowestStream = FindLowestPopularityStream();
                if (lowestStream != null)
                {
                    return trash.TrySimulateManualDiscard(lowestStream);
                }
            }

            // 仕様: 所持限界数-1 で破棄開始
            int movieCount = CountItems(ItemType.ItemMovie, "ItemMovie_");
            if (movieCount >= 4)
            {
                DraggableItemController lowestMovie = FindLowestPopularityMovie();
                if (lowestMovie != null)
                {
                    return trash.TrySimulateManualDiscard(lowestMovie);
                }
            }

            return false;
        }

        private static DraggableItemController FindLowestPopularityStream()
        {
            DraggableItemController[] all = FindObjectsByType<DraggableItemController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            DraggableItemController best = null;
            long bestPopularity = long.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null || (item.ItemType != ItemType.ItemStream && !item.name.StartsWith("ItemStream_", StringComparison.Ordinal)))
                {
                    continue;
                }

                ItemStreamSpawnPopularity p = item.GetComponent<ItemStreamSpawnPopularity>();
                long popularity = p != null ? p.GetSpawnPopularity() : long.MaxValue;
                if (best == null || popularity < bestPopularity)
                {
                    best = item;
                    bestPopularity = popularity;
                }
            }

            return best;
        }

        private static DraggableItemController FindHighestPopularityStream()
        {
            DraggableItemController[] all = FindObjectsByType<DraggableItemController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            DraggableItemController best = null;
            long bestPopularity = long.MinValue;
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null || (item.ItemType != ItemType.ItemStream && !item.name.StartsWith("ItemStream_", StringComparison.Ordinal)))
                {
                    continue;
                }

                ItemStreamSpawnPopularity p = item.GetComponent<ItemStreamSpawnPopularity>();
                long popularity = p != null ? p.GetSpawnPopularity() : long.MinValue;
                if (best == null || popularity > bestPopularity)
                {
                    best = item;
                    bestPopularity = popularity;
                }
            }

            return best;
        }

        private static DraggableItemController FindLowestPopularityMovie()
        {
            DraggableItemController[] all = FindObjectsByType<DraggableItemController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            DraggableItemController best = null;
            long bestPopularity = long.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null || (item.ItemType != ItemType.ItemMovie && !item.name.StartsWith("ItemMovie_", StringComparison.Ordinal)))
                {
                    continue;
                }

                ItemMoviePower p = item.GetComponent<ItemMoviePower>();
                long popularity = p != null ? p.GetFinalPopularity() : long.MaxValue;
                if (best == null || popularity < bestPopularity)
                {
                    best = item;
                    bestPopularity = popularity;
                }
            }

            return best;
        }

        private static int CountItems(ItemType itemType, string prefix)
        {
            int count = 0;
            DraggableItemController[] all = FindObjectsByType<DraggableItemController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null)
                {
                    continue;
                }

                if (item.ItemType == itemType || (!string.IsNullOrEmpty(prefix) && item.name.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryInvokePointerClick(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            IPointerClickHandler handler = target.GetComponent<IPointerClickHandler>();
            if (handler == null)
            {
                return false;
            }

            handler.OnPointerClick(null);
            return true;
        }

        private static DraggableItemController FindFirstItem(ItemType itemType, string namePrefix)
        {
            DraggableItemController[] all = FindObjectsByType<DraggableItemController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null)
                {
                    continue;
                }

                if (item.ItemType == itemType)
                {
                    return item;
                }

                if (!string.IsNullOrEmpty(namePrefix) && item.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private void SubscribeMetricsEvents()
        {
            WorkStreaming.WorkCompleted += OnStreamingCompleted;
            WorkEditor.WorkCompleted += OnEditCompleted;
            WorkMovieUploadController.MovieUploadAccepted += OnMovieUploadAccepted;
            WorkMovieUploadController.BuzzTriggered += OnBuzzTriggered;
        }

        private void UnsubscribeMetricsEvents()
        {
            WorkStreaming.WorkCompleted -= OnStreamingCompleted;
            WorkEditor.WorkCompleted -= OnEditCompleted;
            WorkMovieUploadController.MovieUploadAccepted -= OnMovieUploadAccepted;
            WorkMovieUploadController.BuzzTriggered -= OnBuzzTriggered;
        }

        private void OnStreamingCompleted()
        {
            totalStreamingCount++;
        }

        private void OnEditCompleted()
        {
            totalEditCount++;
            if (enableDebugTraceLog)
            {
                int movieCount = CountItems(ItemType.ItemMovie, "ItemMovie_");
                int streamCount = CountItems(ItemType.ItemStream, "ItemStream_");
                Debug.Log($"[DebugAutoPlay] EditCompleted totalEditCount={totalEditCount} movieCount={movieCount} streamCount={streamCount}");
            }
        }

        private void OnMovieUploadAccepted(int _)
        {
            totalMovieUploadCount++;
            if (enableDebugTraceLog)
            {
                int movieCount = CountItems(ItemType.ItemMovie, "ItemMovie_");
                Debug.Log($"[DebugAutoPlay] MovieUploadAccepted totalMovieUploadCount={totalMovieUploadCount} movieCount={movieCount}");
            }
        }

        private void OnBuzzTriggered()
        {
            totalBuzzCount++;
        }

        private void EnsureMetricsInitialized(GameManager gm)
        {
            if (metricsInitialized || gm == null)
            {
                return;
            }

            previousMoney = gm.CurrentMoney;
            previousPopularity = gm.CurrentPopularity;
            totalMoneyEarnedIncludingInitial = gm.CurrentMoney;
            totalPopularityEarnedIncludingInitial = gm.CurrentPopularity;
            totalMoneySpent = 0;
            metricsInitialized = true;
            EnsureLogPath();
        }

        private void UpdateDeltaMetrics(GameManager gm)
        {
            if (!metricsInitialized || gm == null)
            {
                return;
            }

            long moneyDelta = gm.CurrentMoney - previousMoney;
            if (moneyDelta > 0)
            {
                totalMoneyEarnedIncludingInitial += moneyDelta;
            }
            else if (moneyDelta < 0)
            {
                totalMoneySpent += Math.Abs(moneyDelta);
            }

            long popularityDelta = gm.CurrentPopularity - previousPopularity;
            if (popularityDelta > 0)
            {
                totalPopularityEarnedIncludingInitial += popularityDelta;
            }

            previousMoney = gm.CurrentMoney;
            previousPopularity = gm.CurrentPopularity;
        }

        private void UpdateLogIfNeeded(GameManager gm)
        {
            if (!metricsInitialized || gm == null)
            {
                return;
            }

            float currentGameplayElapsedSeconds = Mathf.Max(0f, gm.GameplayElapsedSeconds);
            if (previousGameplayElapsedSecondsForLog < 0f)
            {
                previousGameplayElapsedSecondsForLog = currentGameplayElapsedSeconds;
                return;
            }

            float deltaGameplaySeconds = Mathf.Max(0f, currentGameplayElapsedSeconds - previousGameplayElapsedSecondsForLog);
            previousGameplayElapsedSecondsForLog = currentGameplayElapsedSeconds;
            logAccumulator += deltaGameplaySeconds;
            if (logAccumulator < Mathf.Max(1f, logIntervalSeconds))
            {
                return;
            }

            logAccumulator -= Mathf.Max(1f, logIntervalSeconds);
            WriteMetricsLog(gm);
        }

        private void EnsureLogPath()
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                return;
            }

            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "game02", "debug_autoplay");
                Directory.CreateDirectory(folder);
                string fileName = $"autoplay_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl";
                logFilePath = Path.Combine(folder, fileName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugAutoPlay] Failed to prepare log path. {ex.Message}");
                logFilePath = null;
            }
        }

        private void WriteMetricsLog(GameManager gm)
        {
            EnsureLogPath();
            if (string.IsNullOrEmpty(logFilePath))
            {
                return;
            }

            StringBuilder sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"timestamp\":\"").Append(DateTime.Now.ToString("o")).Append("\",");
            sb.Append("\"elapsedSeconds\":").Append(Mathf.FloorToInt(gm.GameplayElapsedSeconds)).Append(',');
            sb.Append("\"totalMoneyEarnedIncludingInitial\":").Append(totalMoneyEarnedIncludingInitial).Append(',');
            sb.Append("\"totalMoneySpent\":").Append(totalMoneySpent).Append(',');
            sb.Append("\"totalPopularityEarnedIncludingInitial\":").Append(totalPopularityEarnedIncludingInitial).Append(',');
            sb.Append("\"totalBuzzCount\":").Append(totalBuzzCount).Append(',');
            sb.Append("\"totalStreamingCount\":").Append(totalStreamingCount).Append(',');
            sb.Append("\"totalEditCount\":").Append(totalEditCount).Append(',');
            sb.Append("\"totalMovieUploadCount\":").Append(totalMovieUploadCount);
            sb.Append('}');
            sb.Append('\n');
            try
            {
                File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugAutoPlay] Failed to append metrics log. path={logFilePath} error={ex.Message}");
            }
        }

        private void WriteStartupLogLine()
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                return;
            }

            StringBuilder sb = new StringBuilder(192);
            sb.Append('{');
            sb.Append("\"timestamp\":\"").Append(DateTime.Now.ToString("o")).Append("\",");
            sb.Append("\"event\":\"autoplay_log_started\",");
            sb.Append("\"persistentDataPath\":\"").Append(Application.persistentDataPath.Replace("\\", "\\\\")).Append("\",");
            sb.Append("\"logPath\":\"").Append(logFilePath.Replace("\\", "\\\\")).Append("\"");
            sb.Append('}');
            sb.Append('\n');

            try
            {
                File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
                if (enableDebugTraceLog)
                {
                    Debug.Log($"[DebugAutoPlay] Log file initialized: {logFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugAutoPlay] Failed to write startup log. path={logFilePath} error={ex.Message}");
            }
        }

        private static GameObject FindInactiveByName(string objectName)
        {
            Transform[] all = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == objectName)
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
