using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// WorkUpgradeEd01〜05 共通の買い切りクリック購入基底。
    /// </summary>
    public abstract class WorkUpgradeEdBuyControllerBase : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [Header("Price")]
        [SerializeField] protected long basePrice = 1000L;
        [SerializeField] protected double priceMultiplier = 1d;
        [SerializeField] protected int purchaseLimit = 1;

        [Header("UI")]
        [SerializeField] protected TMP_Text priceText;
        [SerializeField] protected string soldOutString = "Sold out";
        [SerializeField] protected Image clickHitImage;
        [SerializeField] protected SidePanelPriceColorSync sidePanelPriceColorSync;

        [Header("Input")]
        [SerializeField] protected float purchaseInputCooldownSeconds = 0.4f;

        private float refreshAccumulator;
        private bool hasInitializedBootState;
        private bool isPurchased;
        private float nextAllowedPurchaseInputTime;
        private int lastHandledPressFrame = -1;

        protected abstract int GetPurchasedCount();
        protected abstract bool ApplyPurchase();

        protected virtual string PurchaseReason => $"{GetType().Name} purchase";

        public bool IsSoldOut => GetPurchasedCount() >= Mathf.Max(1, purchaseLimit);

        public virtual long GetCurrentSalePrice()
        {
            int purchasedCount = GetPurchasedCount();
            return Game02Economy.ComputeScaledSalePrice(basePrice, priceMultiplier, purchasedCount);
        }

        protected virtual void ConfigureControllerDefaults()
        {
        }

        protected virtual void RunInitialStatusCheckOnce()
        {
        }

        protected virtual void AfterPurchaseApplied()
        {
        }

        protected static GameObject FindInactiveObjectByName(string objectName)
        {
            Transform[] all = Object.FindObjectsOfType<Transform>(true);
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

        protected void EnsureControllerOnNamedObject(string objectPath, string fallbackName)
        {
            GameObject go = GameObject.Find(objectPath);
            if (go == null)
            {
                go = FindInactiveObjectByName(fallbackName);
            }

            if (go == null)
            {
                return;
            }

            if (go.GetComponent(GetType()) == null)
            {
                go.AddComponent(GetType());
            }
        }

        private void Awake()
        {
            ConfigureControllerDefaults();
            ResolveRefsIfNeeded();
            RefreshPriceDisplayImmediate();
        }

        private void Start()
        {
            if (!hasInitializedBootState)
            {
                hasInitializedBootState = true;
                RunInitialStatusCheckOnce();
                RefreshPriceDisplayImmediate();
            }
        }

        private void Update()
        {
            refreshAccumulator += Time.unscaledDeltaTime;
            if (refreshAccumulator < 0.5f)
            {
                return;
            }

            refreshAccumulator = 0f;
            RefreshPriceDisplayImmediate();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TryHandlePurchasePress(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            TryHandlePurchasePress(eventData);
        }

        public void RefreshPriceDisplayImmediate()
        {
            refreshAccumulator = 0f;
            if (priceText == null)
            {
                return;
            }

            int purchasedCount = GetPurchasedCount();
            isPurchased = purchasedCount >= Mathf.Max(1, purchaseLimit);
            if (isPurchased)
            {
                priceText.text = soldOutString;
                UpgradePriceTextVisual.ApplySoldOutPriceStyle(priceText);
                return;
            }

            long salePrice = GetCurrentSalePrice();
            priceText.text = salePrice.ToString("N0");
            bool canBuy = GameManager.Instance != null && GameManager.Instance.CurrentMoney >= salePrice;
            UpgradePriceTextVisual.ApplySalePriceGradient(priceText, canBuy);
        }

        protected void NotifyMoneyOrUpgradeStateChanged()
        {
            RefreshPriceDisplayImmediate();
            if (sidePanelPriceColorSync != null)
            {
                sidePanelPriceColorSync.ApplyPriceColorsImmediate();
            }
        }

        private void TryPurchase()
        {
            if (isPurchased)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.PlayPurchaseFailureSe();
                }

                RefreshPriceDisplayImmediate();
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.ShouldSuppressPlayerInteractions || GameManager.Instance.HasFatalError)
            {
                return;
            }

            int purchasedCount = GetPurchasedCount();
            if (purchasedCount >= Mathf.Max(1, purchaseLimit))
            {
                isPurchased = true;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.PlayPurchaseFailureSe();
                }

                RefreshPriceDisplayImmediate();
                return;
            }

            long salePrice = GetCurrentSalePrice();
            if (GameManager.Instance.CurrentMoney < salePrice)
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                RefreshPriceDisplayImmediate();
                return;
            }

            if (!GameManager.Instance.RequestMoneyDelta(-salePrice, PurchaseReason, gameObject.name, -1))
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                return;
            }

            if (!ApplyPurchase())
            {
                GameManager.Instance.PlayPurchaseFailureSe();
                return;
            }

            isPurchased = true;
            GameManager.Instance.PlayPurchaseSuccessSe();
            AfterPurchaseApplied();
            NotifyMoneyOrUpgradeStateChanged();
        }

        private void TryHandlePurchasePress(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            int currentFrame = Time.frameCount;
            if (currentFrame == lastHandledPressFrame)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextAllowedPurchaseInputTime)
            {
                return;
            }

            lastHandledPressFrame = currentFrame;
            nextAllowedPurchaseInputTime = now + Mathf.Max(0f, purchaseInputCooldownSeconds);
            TryPurchase();
        }

        private void ResolveRefsIfNeeded()
        {
            if (clickHitImage == null)
            {
                clickHitImage = GetComponent<Image>();
            }

            if (priceText == null)
            {
                Transform root = transform;
                Transform priceRoot = root.Find("Price");
                if (priceRoot == null && root.parent != null)
                {
                    priceRoot = root.parent.Find("Price");
                }

                if (priceRoot != null)
                {
                    priceText = priceRoot.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (sidePanelPriceColorSync == null)
            {
                sidePanelPriceColorSync = FindObjectOfType<SidePanelPriceColorSync>(true);
            }

            if (clickHitImage != null)
            {
                clickHitImage.raycastTarget = true;
                Color c = clickHitImage.color;
                c.a = 0f;
                clickHitImage.color = c;
            }

            NormalizeChildRaycastTargets();
        }

        private void NormalizeChildRaycastTargets()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null)
                {
                    continue;
                }

                if (clickHitImage != null && g == clickHitImage)
                {
                    g.raycastTarget = true;
                    continue;
                }

                g.raycastTarget = false;
            }
        }
    }
}
