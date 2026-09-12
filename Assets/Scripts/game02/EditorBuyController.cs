using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// SidePanelView 配下の EditorBuy01/02/03 共通販売コントローラー。
    /// 未購入時は AcceptedItemView を表示し、購入成立で該当 ItemEditor を 1 体スポーンして売り切れ化する。
    /// </summary>
    public sealed class EditorBuyController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Buy Target")]
        [SerializeField] private ItemType targetItemType = ItemType.ItemEditor01;
        [SerializeField] private GameObject targetItemPrefab;
        [SerializeField] private long purchasePrice = 1000L;
        [SerializeField] private bool autoAssignTargetByObjectName = true;
        [SerializeField] private bool autoAssignPriceByObjectName = true;
        [SerializeField] private long editorBuy01Price = 10000L;
        [SerializeField] private long editorBuy02Price = 14800L;
        [SerializeField] private long editorBuy03Price = 148000L;

        [Header("Unpurchased View")]
        [SerializeField] private Image acceptedItemView;
        [SerializeField] private Sprite acceptedItemSprite;
        [SerializeField] private AcceptedItemViewBounceController acceptedItemViewBounce;
        [SerializeField] private DirectionView01Controller directionView01;
        [SerializeField] private DirectionView02Controller directionView02;
        [SerializeField] private DirectionView03Controller directionView03;
        [SerializeField] private DirectionView04Controller directionView04;

        [Header("Price UI")]
        [SerializeField] private TMP_Text priceText;

        [Header("Sold out (optional)")]
        [SerializeField] private TMP_Text soldOutLabel;
        [SerializeField] private string soldOutString = "Sold out";

        [Header("Input")]
        [SerializeField] private Image rootRaycastArea;

        [Header("AcceptedItemView Bounce Start Offset (seconds)")]
        [SerializeField] private bool autoAssignAcceptedItemBounceOffsetByObjectName = true;
        [Min(0f)] [SerializeField] private float acceptedItemBounceStartOffsetSeconds;

        private static bool hasAutoConfiguredSceneEditorBuy;

        private ItemSpawnController cachedSpawnController;
        private bool isPurchased;
        private float priceUiRefreshAccumulator;

        public ItemType TargetItemType => targetItemType;
        public bool IsPurchased => isPurchased;

        public long GetCurrentSalePrice()
        {
            long safePrice = System.Math.Max(1L, purchasePrice);
            return UpgradePriceUtility.NormalizeFinalSalePrice(safePrice);
        }

        public static void EnsureSceneEditorBuyExists()
        {
            if (hasAutoConfiguredSceneEditorBuy)
            {
                return;
            }

            hasAutoConfiguredSceneEditorBuy = true;
            EnsureOne("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/EditorBuy01");
            EnsureOne("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/EditorBuy02");
            EnsureOne("PanelCanvas/PanelObject/SidePanelObject/SidePanelView/EditorBuy03");
            EnsureOne("EditorBuy01");
            EnsureOne("EditorBuy02");
            EnsureOne("EditorBuy03");
        }

        private static void EnsureOne(string objectPath)
        {
            GameObject go = GameObject.Find(objectPath);
            if (go == null)
            {
                return;
            }

            if (go.GetComponent<EditorBuyController>() != null)
            {
                return;
            }

            go.AddComponent<EditorBuyController>();
        }

        private void Awake()
        {
            AutoSetupByObjectNameIfNeeded();
            ResolveRefsIfNeeded();
            AutoAssignAcceptedItemBounceOffsetIfNeeded();
            ApplyAcceptedItemBounceTimingOffset();
            ResolveAcceptedSpriteFromTargetPrefabIfNeeded();
            EnsureDropRaycastArea();
            ApplyUnpurchasedVisual();
            RefreshPriceDisplayImmediate();
        }

        private void Update()
        {
            priceUiRefreshAccumulator += Time.unscaledDeltaTime;
            if (priceUiRefreshAccumulator >= 0.5f)
            {
                priceUiRefreshAccumulator = 0f;
                RefreshPriceDisplayImmediate();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            TryPurchase();
        }

        public void RefreshPriceDisplayImmediate()
        {
            priceUiRefreshAccumulator = 0f;
            if (priceText == null)
            {
                return;
            }

            bool hasSeparateSoldOutLabel = soldOutLabel != null && !ReferenceEquals(soldOutLabel, priceText);
            if (isPurchased)
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

            long safePrice = System.Math.Max(1L, purchasePrice);
            safePrice = UpgradePriceUtility.NormalizeFinalSalePrice(safePrice);
            priceText.text = safePrice.ToString("N0");
            bool canBuy = GameManager.Instance != null && GameManager.Instance.CurrentMoney >= safePrice;
            UpgradePriceTextVisual.ApplySalePriceGradient(priceText, canBuy);
        }

        private void TryPurchase()
        {
            if (isPurchased)
            {
                GameManager.Instance?.PlayPurchaseFailureSe();
                RefreshPriceDisplayImmediate();
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.ShouldSuppressPlayerInteractions)
            {
                return;
            }

            long safePrice = System.Math.Max(1L, purchasePrice);
            safePrice = UpgradePriceUtility.NormalizeFinalSalePrice(safePrice);
            if (GameManager.Instance.CurrentMoney < safePrice)
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                RefreshPriceDisplayImmediate();
                return;
            }

            if (!GameManager.Instance.RequestMoneyDelta(-safePrice, "EditorBuy purchase", gameObject.name, -1))
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                return;
            }

            if (!TrySpawnPurchasedEditor())
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                return;
            }

            bool isFirstEditorBuyInRun = Game02AlienProgressTracker.CountPurchasedEditorBuysInScene() == 0;
            isPurchased = true;
            GameManager.Instance.PlayPurchaseSuccessSe();
            ApplyPurchasedVisual();
            RefreshPriceDisplayImmediate();
            if (isFirstEditorBuyInRun)
            {
                Game02MsgManager.TryGet()?.NotifyFirstEditorBuyPurchased(GameManager.Instance);
            }

            Game02AlienProgressTracker.EnsureExists()?.NotifyProgressRelevantWorldStateChanged();
        }

        private bool TrySpawnPurchasedEditor()
        {
            cachedSpawnController = cachedSpawnController != null
                ? cachedSpawnController
                : FindObjectOfType<ItemSpawnController>(true);

            if (cachedSpawnController == null || cachedSpawnController.ItemCanvas == null)
            {
                Debug.LogWarning("[EditorBuy] ItemSpawnController または ItemCanvas が見つからないため生成できません。");
                return false;
            }

            if (!TryGetSpawnPointCenterSelfInItemCanvas(cachedSpawnController.ItemCanvas, out Vector2 spawnPoint))
            {
                Debug.LogWarning("[EditorBuy] ItemCanvas 上の生成座標を解決できませんでした。");
                return false;
            }

            if (!cachedSpawnController.TrySpawnItem(targetItemType, spawnPoint, true, out _))
            {
                Debug.LogWarning($"[EditorBuy] {targetItemType} の生成に失敗しました。");
                return false;
            }

            return true;
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

        private void ResolveRefsIfNeeded()
        {
            if (acceptedItemView == null)
            {
                Transform t = transform.Find("AcceptedItemView");
                if (t != null)
                {
                    acceptedItemView = t.GetComponent<Image>();
                }
            }

            if (acceptedItemViewBounce == null && acceptedItemView != null)
            {
                acceptedItemViewBounce = acceptedItemView.GetComponent<AcceptedItemViewBounceController>();
                if (acceptedItemViewBounce == null)
                {
                    acceptedItemViewBounce = acceptedItemView.gameObject.AddComponent<AcceptedItemViewBounceController>();
                }
            }

            if (priceText == null)
            {
                Transform priceRoot = transform.Find("Price");
                if (priceRoot != null)
                {
                    priceText = priceRoot.GetComponentInChildren<TMP_Text>(true);
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

            cachedSpawnController = cachedSpawnController != null
                ? cachedSpawnController
                : FindObjectOfType<ItemSpawnController>(true);
        }

        private void ApplyUnpurchasedVisual()
        {
            isPurchased = false;
            if (acceptedItemView != null)
            {
                acceptedItemView.sprite = acceptedItemSprite;
                acceptedItemView.enabled = acceptedItemSprite != null;
            }

            acceptedItemViewBounce?.SetCarrierActive(acceptedItemSprite != null);
            SetDirectionViewsActive(acceptedItemSprite != null);
        }

        private void ApplyPurchasedVisual()
        {
            if (acceptedItemView != null)
            {
                acceptedItemView.enabled = false;
            }

            acceptedItemViewBounce?.SetCarrierActive(false);
            SetDirectionViewsActive(false);
        }

        public EditorBuyState CaptureSaveState()
        {
            return new EditorBuyState
            {
                objectName = name,
                targetItemType = targetItemType,
                isPurchased = isPurchased
            };
        }

        public void ApplySaveState(EditorBuyState state)
        {
            if (state == null)
            {
                return;
            }

            targetItemType = state.targetItemType;
            isPurchased = state.isPurchased;
            if (isPurchased)
            {
                ApplyPurchasedVisual();
            }
            else
            {
                ApplyUnpurchasedVisual();
            }

            RefreshPriceDisplayImmediate();
        }

        private void SetDirectionViewsActive(bool active)
        {
            directionView01?.SetCarrierActive(active);
            directionView02?.SetCarrierActive(active);
            directionView03?.SetCarrierActive(active);
            directionView04?.SetCarrierActive(active);
        }

        private void AutoSetupByObjectNameIfNeeded()
        {
            bool shouldAutoAssignTarget = autoAssignTargetByObjectName;
            bool shouldAutoAssignPrice = autoAssignPriceByObjectName;
            if (!shouldAutoAssignTarget && !shouldAutoAssignPrice)
            {
                return;
            }

            string objectName = gameObject.name;
            if (objectName.Contains("02"))
            {
                if (shouldAutoAssignTarget)
                {
                    targetItemType = ItemType.ItemEditor02;
                }

                if (shouldAutoAssignPrice)
                {
                    purchasePrice = System.Math.Max(1L, editorBuy02Price);
                }
            }
            else if (objectName.Contains("03"))
            {
                if (shouldAutoAssignTarget)
                {
                    targetItemType = ItemType.ItemEditor03;
                }

                if (shouldAutoAssignPrice)
                {
                    purchasePrice = System.Math.Max(1L, editorBuy03Price);
                }
            }
            else
            {
                if (shouldAutoAssignTarget)
                {
                    targetItemType = ItemType.ItemEditor01;
                }

                if (shouldAutoAssignPrice)
                {
                    purchasePrice = System.Math.Max(1L, editorBuy01Price);
                }
            }
        }

        private void ResolveAcceptedSpriteFromTargetPrefabIfNeeded()
        {
            if (targetItemPrefab == null)
            {
                return;
            }

            if (TryResolveSpriteFromPrefab(targetItemPrefab, out Sprite sprite))
            {
                acceptedItemSprite = sprite;
            }

            // prefab 側 DraggableItemController.itemType はコピー元値のまま残ることがあるため、
            // ここでは itemType を信頼せず、prefab 名からのみ推定して上書きする。
            if (TryInferTargetItemTypeFromName(targetItemPrefab.name, out ItemType inferredType))
            {
                targetItemType = inferredType;
            }
        }

        private static bool TryInferTargetItemTypeFromName(string rawName, out ItemType inferredType)
        {
            inferredType = ItemType.Unknown;
            if (string.IsNullOrEmpty(rawName))
            {
                return false;
            }

            string name = rawName;
            if (name.Contains("ItemEditor03"))
            {
                inferredType = ItemType.ItemEditor03;
                return true;
            }

            if (name.Contains("ItemEditor02"))
            {
                inferredType = ItemType.ItemEditor02;
                return true;
            }

            if (name.Contains("ItemEditor01") || name.Contains("ItemEditor"))
            {
                inferredType = ItemType.ItemEditor01;
                return true;
            }

            return false;
        }

        private static bool TryResolveSpriteFromPrefab(GameObject prefab, out Sprite sprite)
        {
            sprite = null;
            if (prefab == null)
            {
                return false;
            }

            DraggableItemController draggable = prefab.GetComponent<DraggableItemController>();
            if (draggable != null && draggable.TryGetDisplaySprite(out sprite) && sprite != null)
            {
                return true;
            }

            Image image = prefab.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                sprite = image.sprite;
                return true;
            }

            return false;
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            return child.GetComponent<T>();
        }

        private void EnsureDropRaycastArea()
        {
            if (rootRaycastArea == null)
            {
                rootRaycastArea = GetComponent<Image>();
            }

            if (rootRaycastArea == null)
            {
                rootRaycastArea = gameObject.AddComponent<Image>();
            }

            if (rootRaycastArea == null)
            {
                return;
            }

            rootRaycastArea.enabled = true;
            rootRaycastArea.raycastTarget = true;
            Color c = rootRaycastArea.color;
            c.a = 0f;
            rootRaycastArea.color = c;
        }

        private void ApplyAcceptedItemBounceTimingOffset()
        {
            float offset = Mathf.Max(0f, acceptedItemBounceStartOffsetSeconds);
            acceptedItemViewBounce?.SetEffectStartOffsetSeconds(offset);
        }

        private void AutoAssignAcceptedItemBounceOffsetIfNeeded()
        {
            if (!autoAssignAcceptedItemBounceOffsetByObjectName)
            {
                return;
            }

            acceptedItemBounceStartOffsetSeconds = ResolveDefaultTimingOffsetByObjectName();
        }

        private float ResolveDefaultTimingOffsetByObjectName()
        {
            string objectName = gameObject.name;
            if (objectName.Contains("03"))
            {
                return 0.7f;
            }

            if (objectName.Contains("02"))
            {
                return 0.35f;
            }

            return 0f;
        }
    }
}
