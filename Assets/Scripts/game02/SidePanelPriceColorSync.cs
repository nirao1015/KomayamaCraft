using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// SidePanelView 配下の Price (TMP) を <see cref="Game02EffectManager"/> の販売色で定期的に同期する。
    /// </summary>
    public sealed class SidePanelPriceColorSync : MonoBehaviour
    {
        [Header("Scan")]
        [SerializeField] private Transform sidePanelViewRoot;
        [SerializeField] private bool includeInactive = true;

        [Header("Refresh")]
        [SerializeField] private float refreshIntervalSeconds = 0.5f;

        private static bool hasAutoConfiguredInScene;
        private readonly List<TMP_Text> trackedPriceTexts = new List<TMP_Text>();
        private float refreshAccumulator;

        public static void EnsureSceneSidePanelPriceColorSyncExists()
        {
            if (hasAutoConfiguredInScene)
            {
                return;
            }

            hasAutoConfiguredInScene = true;
            EnsureOne("PanelCanvas/PanelObject/SidePanelObject/SidePanelView");
            EnsureOne("SidePanelView");
        }

        private static void EnsureOne(string objectPath)
        {
            GameObject go = GameObject.Find(objectPath);
            if (go == null)
            {
                return;
            }

            if (go.GetComponent<SidePanelPriceColorSync>() != null)
            {
                return;
            }

            go.AddComponent<SidePanelPriceColorSync>();
        }

        private void Awake()
        {
            ResolveRootIfNeeded();
            RebuildTargetList();
            ApplyPriceColorsImmediate();
        }

        private void OnEnable()
        {
            refreshAccumulator = 0f;
            ResolveRootIfNeeded();
            RebuildTargetList();
            ApplyPriceColorsImmediate();
        }

        private void Update()
        {
            refreshAccumulator += Time.unscaledDeltaTime;
            if (refreshAccumulator < Mathf.Max(0.05f, refreshIntervalSeconds))
            {
                return;
            }

            refreshAccumulator = 0f;
            if (trackedPriceTexts.Count == 0)
            {
                ResolveRootIfNeeded();
                RebuildTargetList();
            }

            ApplyPriceColorsImmediate();
        }

        public void ApplyPriceColorsImmediate()
        {
            long money = GameManager.Instance != null ? GameManager.Instance.CurrentMoney : 0L;
            for (int i = 0; i < trackedPriceTexts.Count; i++)
            {
                TMP_Text text = trackedPriceTexts[i];
                if (text == null)
                {
                    continue;
                }

                if (!TryParseDisplayedPrice(text.text, out long price))
                {
                    continue;
                }

                UpgradePriceTextVisual.ApplySalePriceGradient(text, money >= price);
            }
        }

        private void ResolveRootIfNeeded()
        {
            if (sidePanelViewRoot != null)
            {
                return;
            }

            sidePanelViewRoot = transform;
        }

        private void RebuildTargetList()
        {
            trackedPriceTexts.Clear();
            if (sidePanelViewRoot == null)
            {
                return;
            }

            TMP_Text[] allTexts = sidePanelViewRoot.GetComponentsInChildren<TMP_Text>(includeInactive);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text text = allTexts[i];
                if (text == null)
                {
                    continue;
                }

                if (!IsPriceText(text))
                {
                    continue;
                }

                trackedPriceTexts.Add(text);
            }
        }

        private static bool IsPriceText(TMP_Text text)
        {
            if (text == null)
            {
                return false;
            }

            if (text.name == "Price (TMP)")
            {
                return true;
            }

            Transform parent = text.transform.parent;
            if (parent != null && parent.name == "Price")
            {
                return true;
            }

            return false;
        }

        private static bool TryParseDisplayedPrice(string raw, out long price)
        {
            price = 0L;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();
            if (trimmed.Equals("Sold out", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = trimmed.Replace(",", string.Empty).Replace(" ", string.Empty);
            return long.TryParse(normalized, out price) && price >= 0L;
        }
    }
}
