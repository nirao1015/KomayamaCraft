using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// WorkUpgradeSt01 トーク力仕事場（work_upgrade_st01_spec.md）。
    /// </summary>
    public sealed class WorkUpgradeSt01Controller : MonoBehaviour, IWorkplaceTarget
    {
        public const int MaxTalkPowerCompletions = int.MaxValue;
        public const long BasePrice = 65000L;
        public const double PriceMultiplier = 4.20d;
        public const double PriceMultiplierIncrementPerLevel = 1.00d;

        [Header("Accept State")]
        [SerializeField] private bool isAcceptingItems = true;
        [SerializeField] private bool isWorking;

        [Header("Accepted Item Type")]
        [SerializeField] private bool acceptItemMailChara = true;

        [Header("Work Rule")]
        [SerializeField] private float basicWorkSeconds = 30f;
        [SerializeField] private float workTimeVariation = 1f;

        [Header("Accept Visuals")]
        [SerializeField] private WorkUpgradeSt01AcceptEffect acceptEffect;
        [SerializeField] private Image acceptedItemView;
        [SerializeField] private AcceptedItemViewBounceController acceptedItemViewBounce;
        [SerializeField] private Image dropRaycastArea;

        [Header("Work Remaining Time UI")]
        [SerializeField] private Image imageTimeB;
        [SerializeField] private Image imageTimeF;

        [Header("Direction Views")]
        [SerializeField] private DirectionView01Controller directionView01;
        [SerializeField] private DirectionView02Controller directionView02;
        [SerializeField] private DirectionView03Controller directionView03;
        [SerializeField] private DirectionView04Controller directionView04;

        [Header("Price UI")]
        [SerializeField] private TMP_Text priceText;

        [Header("Sold out (optional)")]
        [SerializeField] private TMP_Text soldOutLabel;
        [SerializeField] private string soldOutString = "Sold out";

        private static Sprite cachedUiWhiteSprite;

        private Coroutine acceptFlowRoutine;
        private float elapsedWorkSeconds;
        private float workVisualElapsedSeconds;
        private ItemSpawnController cachedSpawnController;
        private bool hasAcceptedRuntimeItem;
        private string lastAcceptedSpriteName;
        private Transform imageTimeFOriginalParent;
        private int imageTimeFOriginalSiblingIndex;
        private bool imageTimeFReparentedUnderBackground;
        private float priceUiRefreshAccumulator;

        public bool IsAcceptingItems
        {
            get => isAcceptingItems;
            set => isAcceptingItems = value;
        }

        private void Awake()
        {
            if (acceptEffect == null)
            {
                acceptEffect = GetComponent<WorkUpgradeSt01AcceptEffect>();
            }

            if (acceptedItemView == null)
            {
                Transform t = transform.Find("AcceptedItemView");
                if (t != null)
                {
                    acceptedItemView = t.GetComponent<Image>();
                }
            }

            WorkUpgradeStPresentation.ResolveAcceptedItemViewReference(
                transform,
                ref acceptedItemView,
                ref acceptedItemViewBounce);

            EnsureDropRaycastArea();

            if (acceptedItemViewBounce == null && acceptedItemView != null)
            {
                acceptedItemViewBounce = acceptedItemView.GetComponent<AcceptedItemViewBounceController>();
                if (acceptedItemViewBounce == null)
                {
                    acceptedItemViewBounce = acceptedItemView.gameObject.AddComponent<AcceptedItemViewBounceController>();
                }
            }

            if (directionView01 == null)
            {
                directionView01 = FindChildComponent<DirectionView01Controller>("DirectionView01");
            }

            if (directionView02 == null)
            {
                directionView02 = FindChildComponent<DirectionView02Controller>("DirectionView02");
            }

            if (directionView03 == null)
            {
                directionView03 = FindChildComponent<DirectionView03Controller>("DirectionView03");
            }

            if (directionView04 == null)
            {
                directionView04 = FindChildComponent<DirectionView04Controller>("DirectionView04");
            }

            if (priceText == null)
            {
                Transform priceRoot = transform.Find("Price");
                if (priceRoot != null)
                {
                    priceText = priceRoot.GetComponentInChildren<TMP_Text>(true);
                }
            }

            ResolveWorkTimeImagesIfNeeded();
            SetWorkTimeImagesActive(false);

            SetDirectionViewsActive(false);
            acceptedItemViewBounce?.SetCarrierActive(false);
            hasAcceptedRuntimeItem = isWorking && acceptedItemView != null && acceptedItemView.sprite != null;

            cachedSpawnController = FindObjectOfType<ItemSpawnController>(true);
        }

        private void OnEnable()
        {
            if (hasAcceptedRuntimeItem)
            {
                RestoreAcceptedItemPresentationFromState();
            }
        }

        private void Start()
        {
            RefreshPriceDisplayImmediate();
        }

        private void Update()
        {
            UpdateWorkRemainingTimeBarVisual();
            priceUiRefreshAccumulator += Time.unscaledDeltaTime;
            if (priceUiRefreshAccumulator >= 0.5f)
            {
                priceUiRefreshAccumulator = 0f;
                RefreshPriceDisplayImmediate();
            }
        }

        public void OnGameManagerWorkTick(float deltaSeconds)
        {
            if (!isWorking || deltaSeconds <= 0f)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                return;
            }

            elapsedWorkSeconds += deltaSeconds;
            workVisualElapsedSeconds = Mathf.Max(workVisualElapsedSeconds, elapsedWorkSeconds);
            PlayWorkingEffect();

            float requiredWorkSeconds = GetRequiredWorkSeconds();
            if (elapsedWorkSeconds >= requiredWorkSeconds)
            {
                EndWorkSession();
            }
        }

        public bool CanAcceptItem(DraggableItemController item)
        {
            if (IsDropCheckBlockedByPause())
            {
                return false;
            }

            if (!isAcceptingItems)
            {
                return false;
            }

            if (isWorking)
            {
                return false;
            }

            if (IsAcceptedSlotOccupied())
            {
                return false;
            }

            if (item == null)
            {
                return false;
            }

            if (!acceptItemMailChara)
            {
                return false;
            }

            if (item.ItemType != ItemType.ItemMailChara)
            {
                return false;
            }

            if (IsSoldOut())
            {
                return false;
            }

            long price = GetCurrentPlacementPrice();
            if (GameManager.Instance == null || GameManager.Instance.CurrentMoney < price)
            {
                return false;
            }

            return true;
        }

        public void OnItemDropped(DraggableItemController item)
        {
            if (item == null)
            {
                return;
            }

            if (IsSoldOut())
            {
                TryPlayPurchaseFailureSe();
                RefreshPriceDisplayImmediate();
                return;
            }

            if (!CanAcceptItem(item))
            {
                return;
            }

            long price = GetCurrentPlacementPrice();
            if (GameManager.Instance == null || GameManager.Instance.CurrentMoney < price)
            {
                TryPlayPurchaseFailureSe();
                RefreshPriceDisplayImmediate();
                return;
            }

            if (!GameManager.Instance.RequestMoneyDelta(-price, "WorkUpgradeSt01 placement", "WorkUpgradeSt01", -1))
            {
                TryPlayPurchaseFailureSe();
                return;
            }

            TryPlayPurchaseSuccessSe();

            if (item == null || !item.TryGetDisplaySprite(out Sprite acceptedSprite))
            {
                Debug.LogError("[WorkUpgradeSt01] Accepted sprite is null.");
                return;
            }

            if (acceptFlowRoutine != null)
            {
                StopCoroutine(acceptFlowRoutine);
            }

            acceptFlowRoutine = StartCoroutine(RunAcceptFlow(acceptedSprite));
        }

        private IEnumerator RunAcceptFlow(Sprite acceptedSprite)
        {
            // 金銭キューは GameManager.Update 先頭で処理されるため、1 フレーム待ってから着任演出へ進む。
            yield return null;

            if (acceptEffect != null)
            {
                yield return acceptEffect.Play(acceptedSprite, acceptedItemView);
                if (acceptedItemView == null || acceptedItemView.sprite == null)
                {
                    Debug.LogError("[WorkUpgradeSt01] AcceptedItemView is missing or sprite not applied after accept effect.");
                    acceptFlowRoutine = null;
                    yield break;
                }
            }
            else if (!ApplyAcceptedItemViewCore(acceptedSprite))
            {
                acceptFlowRoutine = null;
                yield break;
            }

            if (!CompleteAcceptedItemPresentation())
            {
                acceptFlowRoutine = null;
                yield break;
            }

            StartWorkSession();
            acceptFlowRoutine = null;
        }

        private bool IsDropCheckBlockedByPause()
        {
            return GameManager.Instance != null && GameManager.Instance.ShouldSuppressPlayerInteractions;
        }

        private bool IsAcceptedSlotOccupied()
        {
            return hasAcceptedRuntimeItem;
        }

        private bool IsSoldOut()
        {
            return false;
        }

        private int GetPriceLevelForNextPlacement()
        {
            if (UpgradesManager.Instance == null)
            {
                return 0;
            }

            return Mathf.Max(0, UpgradesManager.Instance.GetTalkPowerUpgradeLevel());
        }

        private long GetCurrentPlacementPrice()
        {
            int level = GetPriceLevelForNextPlacement();
            return Game02Economy.ComputeScaledSalePriceWithProgressiveMultiplier(
                BasePrice,
                PriceMultiplier,
                PriceMultiplierIncrementPerLevel,
                level);
        }

        /// <summary>金額変化直後などに呼ぶ。</summary>
        public void RefreshPriceDisplayImmediate()
        {
            priceUiRefreshAccumulator = 0f;
            if (priceText == null)
            {
                return;
            }

            bool hasSeparateSoldOutLabel = soldOutLabel != null && !ReferenceEquals(soldOutLabel, priceText);

            if (IsSoldOut())
            {
                priceText.text = string.IsNullOrEmpty(soldOutString) ? "Sold out" : soldOutString;
                if (hasSeparateSoldOutLabel)
                {
                    soldOutLabel.text = priceText.text;
                    soldOutLabel.gameObject.SetActive(true);
                }

                UpgradePriceTextVisual.ApplySoldOutPriceStyle(priceText, hasSeparateSoldOutLabel ? soldOutLabel : null);
                return;
            }

            if (hasSeparateSoldOutLabel)
            {
                soldOutLabel.gameObject.SetActive(false);
            }

            long price = GetCurrentPlacementPrice();
            priceText.text = price.ToString("N0");
            bool canBuy = GameManager.Instance != null && GameManager.Instance.CurrentMoney >= price;
            UpgradePriceTextVisual.ApplySalePriceGradient(priceText, canBuy);
        }

        private static void TryPlayPurchaseSuccessSe()
        {
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.WorkUpgradeAccepted);
        }

        private static void TryPlayPurchaseFailureSe()
        {
            GameManager.Instance?.PlayPurchaseFailureSe();
        }

        private bool ApplyAcceptedItemViewCore(Sprite acceptedSprite)
        {
            if (acceptedItemView == null)
            {
                Debug.LogError("[WorkUpgradeSt01] AcceptedItemView is missing.");
                return false;
            }

            return WorkUpgradeStPresentation.TryApplyAcceptedSprite(
                acceptedItemView,
                acceptedSprite,
                ref lastAcceptedSpriteName);
        }

        private bool CompleteAcceptedItemPresentation()
        {
            if (acceptedItemView == null)
            {
                Debug.LogError("[WorkUpgradeSt01] AcceptedItemView is missing.");
                return false;
            }

            hasAcceptedRuntimeItem = true;
            RestoreAcceptedItemPresentationFromState();
            return true;
        }

        private void RestoreAcceptedItemPresentationFromState()
        {
            if (!hasAcceptedRuntimeItem)
            {
                return;
            }

            WorkUpgradeStPresentation.RestoreSpriteOnAcceptedItemView(acceptedItemView, ref lastAcceptedSpriteName);
            SetDirectionViewsActive(true);
            acceptedItemViewBounce?.SetCarrierActive(true);
        }

        private void StartWorkSession()
        {
            Game02MsgManager.TryGet()?.NotifyFirstWorkUpgradeStUsed(GameManager.Instance);
            isWorking = true;
            elapsedWorkSeconds = 0f;
            workVisualElapsedSeconds = 0f;
            EnsureWorkTimeBarSpritesIfMissing();
            BeginWorkTimeFrontBarLayout();

            SetWorkTimeImagesActive(true);
        }

        private void EndWorkSession()
        {
            long popularityWhenWorkEnds = GameManager.Instance != null
                ? GameManager.Instance.CurrentPopularity
                : 0L;

            if (UpgradesManager.Instance != null)
            {
                UpgradesManager.Instance.ApplySt01WorkComplete(popularityWhenWorkEnds);
            }

            isWorking = false;
            elapsedWorkSeconds = 0f;
            workVisualElapsedSeconds = 0f;
            RestoreWorkTimeFrontBarParentIfNeeded();
            SetWorkTimeImagesActive(false);
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.WorkUpgradeComplete);
            PlayWorkExitEffect();
            RespawnMailCharaAfterWorkExit();
            ClearAcceptedItemView();
            RefreshPriceDisplayImmediate();
        }

        private void ClearAcceptedItemView()
        {
            if (acceptedItemView == null)
            {
                return;
            }

            acceptedItemView.sprite = null;
            acceptedItemView.enabled = false;
            lastAcceptedSpriteName = string.Empty;
            hasAcceptedRuntimeItem = false;
            SetDirectionViewsActive(false);
            acceptedItemViewBounce?.SetCarrierActive(false);
        }

        private float GetRequiredWorkSeconds()
        {
            float multiplied = Mathf.Max(0f, basicWorkSeconds) * Mathf.Max(0f, workTimeVariation);
            return Mathf.Max(1f, Mathf.Floor(multiplied));
        }

        private void PlayWorkingEffect()
        {
        }

        private void PlayWorkExitEffect()
        {
        }

        private void ResolveWorkTimeImagesIfNeeded()
        {
            if (imageTimeB == null)
            {
                Transform t = transform.Find("ImageTimeB");
                if (t != null)
                {
                    imageTimeB = t.GetComponent<Image>();
                }
            }

            if (imageTimeF == null)
            {
                Transform t = transform.Find("ImageTimeF");
                if (t != null)
                {
                    imageTimeF = t.GetComponent<Image>();
                }
            }
        }

        private static Sprite GetOrCreateUiWhiteSprite()
        {
            if (cachedUiWhiteSprite != null)
            {
                return cachedUiWhiteSprite;
            }

            Texture2D tex = Texture2D.whiteTexture;
            cachedUiWhiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedUiWhiteSprite;
        }

        private void EnsureWorkTimeBarSpritesIfMissing()
        {
            if (imageTimeB != null && imageTimeB.sprite == null)
            {
                imageTimeB.sprite = GetOrCreateUiWhiteSprite();
                imageTimeB.type = Image.Type.Simple;
            }

            if (imageTimeF != null && imageTimeF.sprite == null)
            {
                imageTimeF.sprite = GetOrCreateUiWhiteSprite();
                imageTimeF.type = Image.Type.Simple;
            }
        }

        private void BeginWorkTimeFrontBarLayout()
        {
            if (imageTimeB == null || imageTimeF == null)
            {
                return;
            }

            RectTransform f = imageTimeF.rectTransform;
            if (!imageTimeFReparentedUnderBackground)
            {
                imageTimeFOriginalParent = f.parent;
                imageTimeFOriginalSiblingIndex = f.GetSiblingIndex();
                f.SetParent(imageTimeB.transform, false);
                imageTimeFReparentedUnderBackground = true;
            }

            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.pivot = new Vector2(0.5f, 0.5f);
            f.offsetMin = Vector2.zero;
            f.offsetMax = Vector2.zero;
            f.localScale = Vector3.one;
            f.localRotation = Quaternion.identity;
            ApplyWorkTimeFrontBarRemainingVisual(1f);
        }

        private void RestoreWorkTimeFrontBarParentIfNeeded()
        {
            if (!imageTimeFReparentedUnderBackground || imageTimeF == null)
            {
                return;
            }

            RectTransform f = imageTimeF.rectTransform;
            if (imageTimeFOriginalParent != null)
            {
                f.SetParent(imageTimeFOriginalParent, false);
                int max = Mathf.Max(0, imageTimeFOriginalParent.childCount - 1);
                f.SetSiblingIndex(Mathf.Clamp(imageTimeFOriginalSiblingIndex, 0, max));
            }

            imageTimeFReparentedUnderBackground = false;
        }

        private void ApplyWorkTimeFrontBarRemainingVisual(float remaining01)
        {
            if (imageTimeF == null)
            {
                return;
            }

            float r = Mathf.Clamp01(remaining01);
            if (imageTimeFReparentedUnderBackground)
            {
                RectTransform f = imageTimeF.rectTransform;
                f.anchorMin = Vector2.zero;
                f.anchorMax = new Vector2(r, 1f);
                f.offsetMin = Vector2.zero;
                f.offsetMax = Vector2.zero;
            }
            else
            {
                imageTimeF.type = Image.Type.Filled;
                imageTimeF.fillMethod = Image.FillMethod.Horizontal;
                imageTimeF.fillOrigin = (int)Image.OriginHorizontal.Left;
                imageTimeF.fillAmount = r;
            }
        }

        private void SetWorkTimeImagesActive(bool active)
        {
            if (imageTimeB != null)
            {
                imageTimeB.gameObject.SetActive(active);
            }

            if (imageTimeF != null)
            {
                imageTimeF.gameObject.SetActive(active);
            }
        }

        private void UpdateWorkRemainingTimeBarVisual()
        {
            if (imageTimeF == null || !isWorking)
            {
                return;
            }

            float required = GetRequiredWorkSeconds();
            if (required <= 0f)
            {
                ApplyWorkTimeFrontBarRemainingVisual(0f);
                return;
            }

            bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
            if (!paused)
            {
                workVisualElapsedSeconds += GameManager.GameplayDelta;
                workVisualElapsedSeconds = Mathf.Max(workVisualElapsedSeconds, elapsedWorkSeconds);
            }

            float displayElapsed = Mathf.Min(workVisualElapsedSeconds, required);
            float remaining = Mathf.Clamp01(1f - displayElapsed / required);
            ApplyWorkTimeFrontBarRemainingVisual(remaining);
        }

        private void RespawnMailCharaAfterWorkExit()
        {
            cachedSpawnController = cachedSpawnController != null
                ? cachedSpawnController
                : FindObjectOfType<ItemSpawnController>(true);

            if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
            {
                Debug.LogWarning("[WorkUpgradeSt01] Work exit respawn skipped: ItemSpawnController or ItemCanvas not found.");
                return;
            }

            if (!TryGetSpawnPointCenterSelfInItemCanvas(cachedSpawnController.ItemCanvas, out Vector2 centerAnchored))
            {
                Debug.LogWarning("[WorkUpgradeSt01] Work exit respawn skipped: could not resolve center in ItemCanvas.");
                return;
            }

            if (!cachedSpawnController.TrySpawnItem(
                    ItemType.ItemMailChara,
                    centerAnchored,
                    true,
                    out DraggableItemController mailSpawned,
                    null))
            {
                LogWorkExitSpawnFailure(nameof(ItemType.ItemMailChara));
            }
            else if (mailSpawned == null)
            {
                Debug.LogWarning("[WorkUpgradeSt01] ItemMailChara work-exit respawn produced no DraggableItemController on instance.");
            }
        }

        private static void LogWorkExitSpawnFailure(string itemLabel)
        {
            if (GameManager.Instance != null && GameManager.Instance.HasFatalError)
            {
                Debug.LogWarning($"[WorkUpgradeSt01] {itemLabel} work-exit respawn skipped: GameManager fatal state (spawn blocked).");
            }
            else
            {
                Debug.LogWarning($"[WorkUpgradeSt01] {itemLabel} work-exit respawn failed: TrySpawnItem returned false (prefab / canvas / controller).");
            }
        }

        private bool TryGetSpawnPointCenterSelfInItemCanvas(RectTransform itemCanvasRect, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            RectTransform selfRect = transform as RectTransform;
            if (selfRect == null)
            {
                return false;
            }

            Vector3 worldCenter = selfRect.TransformPoint(selfRect.rect.center);

            Canvas canvas = itemCanvasRect.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(itemCanvasRect, screenPoint, cam, out anchoredPosition))
            {
                return true;
            }

            anchoredPosition = itemCanvasRect.InverseTransformPoint(worldCenter);
            return true;
        }

        private void SetDirectionViewsActive(bool active)
        {
            directionView01?.SetCarrierActive(active);
            directionView02?.SetCarrierActive(active);
            directionView03?.SetCarrierActive(active);
            directionView04?.SetCarrierActive(active);
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            T component = child.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return child.gameObject.AddComponent<T>();
        }

        private void EnsureDropRaycastArea()
        {
            if (dropRaycastArea == null)
            {
                dropRaycastArea = GetComponent<Image>();
            }

            if (dropRaycastArea == null)
            {
                dropRaycastArea = gameObject.AddComponent<Image>();
            }

            if (dropRaycastArea == null)
            {
                return;
            }

            dropRaycastArea.enabled = true;
            dropRaycastArea.raycastTarget = true;
            Color c = dropRaycastArea.color;
            c.a = 0f;
            dropRaycastArea.color = c;
        }

        public WorkUpgradeWorkplaceState CaptureSaveState()
        {
            return new WorkUpgradeWorkplaceState
            {
                objectName = name,
                isAcceptingItems = isAcceptingItems,
                isWorking = isWorking,
                hasAcceptedRuntimeItem = hasAcceptedRuntimeItem,
                acceptedSpriteName = WorkUpgradeStPresentation.ResolveCaptureSpriteName(
                    acceptedItemView,
                    lastAcceptedSpriteName),
                elapsedWorkSeconds = elapsedWorkSeconds,
                workVisualElapsedSeconds = workVisualElapsedSeconds
            };
        }

        public void ApplySaveState(WorkUpgradeWorkplaceState state)
        {
            if (state == null)
            {
                return;
            }

            isAcceptingItems = state.isAcceptingItems;
            isWorking = state.isWorking;
            hasAcceptedRuntimeItem = state.hasAcceptedRuntimeItem;
            elapsedWorkSeconds = Mathf.Max(0f, state.elapsedWorkSeconds);
            workVisualElapsedSeconds = Mathf.Max(0f, state.workVisualElapsedSeconds);
            if (!string.IsNullOrEmpty(state.acceptedSpriteName))
            {
                lastAcceptedSpriteName = state.acceptedSpriteName;
            }

            if (hasAcceptedRuntimeItem)
            {
                RestoreAcceptedItemPresentationFromState();
            }
            else
            {
                if (acceptedItemView != null)
                {
                    acceptedItemView.sprite = null;
                    acceptedItemView.enabled = false;
                }

                lastAcceptedSpriteName = string.Empty;
                SetDirectionViewsActive(false);
                acceptedItemViewBounce?.SetCarrierActive(false);
            }

            if (isWorking)
            {
                EnsureWorkTimeBarSpritesIfMissing();
                BeginWorkTimeFrontBarLayout();
            }
            else
            {
                RestoreWorkTimeFrontBarParentIfNeeded();
            }

            SetWorkTimeImagesActive(isWorking);
            RefreshPriceDisplayImmediate();
        }
    }
}
