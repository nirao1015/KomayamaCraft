using System;
using System.Globalization;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// 異星人解放・分析用の進行カウンタとセーブ状態。仕様: alien_parson_unlock_spec.md
    /// </summary>
    [DefaultExecutionOrder(-120)]
    [DisallowMultipleComponent]
    public sealed class Game02AlienProgressTracker : MonoBehaviour
    {
        public const int MaxUpgradePurchaseLogEntries = 400;

        public static Game02AlienProgressTracker Instance { get; private set; }

        private AlienProgressState state = new AlienProgressState();

        public event Action ProgressChanged;

        /// <summary>
        /// 人口気などトラッカー外で変化した値を参照する解放条件のため、購読側へ再評価を依頼する。
        /// </summary>
        public void NotifyProgressRelevantWorldStateChanged()
        {
            ProgressChanged?.Invoke();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            TryBootstrapInitialMoneyGrant();
        }

        public static Game02AlienProgressTracker EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            Instance = FindFirstObjectByType<Game02AlienProgressTracker>(FindObjectsInactive.Include);
            if (Instance != null)
            {
                return Instance;
            }

            GameObject go = new GameObject("Game02AlienProgressTracker");
            Instance = go.AddComponent<Game02AlienProgressTracker>();
            return Instance;
        }

        public void ApplyAlienProgressState(AlienProgressState saved)
        {
            state = CloneState(saved);
            if (state.upgradePurchaseLog == null)
            {
                state.upgradePurchaseLog = new System.Collections.Generic.List<UpgradePurchaseLogEntry>();
            }

            ProgressChanged?.Invoke();
        }

        public AlienProgressState CaptureAlienProgressState()
        {
            TrimPurchaseLogIfNeeded();
            return CloneState(state);
        }

        private static AlienProgressState CloneState(AlienProgressState src)
        {
            if (src == null)
            {
                return new AlienProgressState();
            }

            string json = JsonUtility.ToJson(src);
            AlienProgressState copy = JsonUtility.FromJson<AlienProgressState>(json);
            return copy ?? new AlienProgressState();
        }

        private void TrimPurchaseLogIfNeeded()
        {
            if (state.upgradePurchaseLog == null)
            {
                state.upgradePurchaseLog =
                    new System.Collections.Generic.List<UpgradePurchaseLogEntry>();
                return;
            }

            int excess = state.upgradePurchaseLog.Count - MaxUpgradePurchaseLogEntries;
            if (excess > 0)
            {
                state.upgradePurchaseLog.RemoveRange(0, excess);
            }
        }

        public void TryBootstrapInitialMoneyGrant()
        {
            if (state.moneyInitialGrantRecorded)
            {
                return;
            }

            if (Game02SaveCoordinator.DidLoadSaveThisSession)
            {
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            long initial = Math.Max(0L, gm.InitialMoney);
            try
            {
                checked
                {
                    state.lifetimeTotalMoneyEarned += initial;
                }
            }
            catch (OverflowException)
            {
                state.lifetimeTotalMoneyEarned = long.MaxValue;
            }

            state.moneyInitialGrantRecorded = true;
            ProgressChanged?.Invoke();
        }

        public void RecordProcessedMoneyDelta(long delta, string reason, string sourceId)
        {
            if (delta > 0L)
            {
                try
                {
                    checked
                    {
                        state.lifetimeTotalMoneyEarned += delta;
                    }
                }
                catch (OverflowException)
                {
                    state.lifetimeTotalMoneyEarned = long.MaxValue;
                }
            }
            else if (delta < 0L)
            {
                long spent = -delta;
                try
                {
                    checked
                    {
                        state.lifetimeTotalMoneySpent += spent;
                    }
                }
                catch (OverflowException)
                {
                    state.lifetimeTotalMoneySpent = long.MaxValue;
                }

                TryAppendPurchaseLog(spent, reason ?? string.Empty, sourceId ?? string.Empty);
            }

            ProgressChanged?.Invoke();
        }

        private void TryAppendPurchaseLog(long pricePaid, string reason, string sourceId)
        {
            if (pricePaid <= 0L)
            {
                return;
            }

            string r = reason ?? string.Empty;
            if (!r.Contains("purchase", StringComparison.OrdinalIgnoreCase) &&
                !r.Contains("placement", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string productId = string.IsNullOrEmpty(sourceId) ? r : $"{r}::{sourceId}";
            int ordinal = 1;
            if (state.upgradePurchaseLog != null)
            {
                for (int i = 0; i < state.upgradePurchaseLog.Count; i++)
                {
                    UpgradePurchaseLogEntry e = state.upgradePurchaseLog[i];
                    if (e != null && string.Equals(e.productId, productId, StringComparison.Ordinal))
                    {
                        ordinal++;
                    }
                }
            }

            GameManager gm = GameManager.Instance;
            float elapsed = gm != null ? gm.GameplayElapsedSeconds : 0f;

            var entry = new UpgradePurchaseLogEntry
            {
                purchasedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                gameplayElapsedSeconds = elapsed,
                productId = productId,
                ordinalForProduct = ordinal,
                pricePaid = pricePaid,
            };

            if (state.upgradePurchaseLog == null)
            {
                state.upgradePurchaseLog = new System.Collections.Generic.List<UpgradePurchaseLogEntry>();
            }

            state.upgradePurchaseLog.Add(entry);
            TrimPurchaseLogIfNeeded();
        }

        public void RecordProcessedBuzzDelta(long delta)
        {
            if (delta <= 0L)
            {
                return;
            }

            state.lifetimePositiveBuzzEventCount++;
            try
            {
                checked
                {
                    state.lifetimeBuzzPositiveDeltaSum += delta;
                }
            }
            catch (OverflowException)
            {
                state.lifetimeBuzzPositiveDeltaSum = long.MaxValue;
            }

            ProgressChanged?.Invoke();
        }

        public void NotifyWorkMovieAccepted(int slotIndex)
        {
            if (slotIndex != 1)
            {
                return;
            }

            state.lifetimeWorkMovie1UploadCount = Math.Min(int.MaxValue - 1,
                state.lifetimeWorkMovie1UploadCount + 1);
            Game02MsgManager.TryGet()?.NotifyWorkMovieSlot1UploadCount(state.lifetimeWorkMovie1UploadCount, GameManager.Instance);
            ProgressChanged?.Invoke();
        }

        /// <summary>スロット1へバズ動画がアップロードされたときに 1 増やす（ParsonB* 出現条件用）。</summary>
        public void NotifyBuzzMovieUploaded()
        {
            state.lifetimeBuzzMovieUploadCount = Math.Min(int.MaxValue - 1,
                state.lifetimeBuzzMovieUploadCount + 1);
            Game02MsgManager.TryGet()?.NotifyBuzzMovieUploadCount(state.lifetimeBuzzMovieUploadCount, GameManager.Instance);
            ProgressChanged?.Invoke();
        }

        public void NotifySidePanelOpened()
        {
            state.lifetimeSidePanelOpenCount = Math.Min(int.MaxValue - 1,
                state.lifetimeSidePanelOpenCount + 1);
            ProgressChanged?.Invoke();
        }

        public void NotifyWorkStreamingComplete()
        {
            state.lifetimeWorkStreamingCompleteCount = Math.Min(int.MaxValue - 1,
                state.lifetimeWorkStreamingCompleteCount + 1);
            ProgressChanged?.Invoke();
        }

        public void NotifyWorkEditorComplete()
        {
            state.lifetimeWorkEditorCompleteCount = Math.Min(int.MaxValue - 1,
                state.lifetimeWorkEditorCompleteCount + 1);
            ProgressChanged?.Invoke();
        }

        public void AddWorkEditorExtWorkSeconds(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            float prev = state.lifetimeWorkEditorExtWorkSeconds;
            state.lifetimeWorkEditorExtWorkSeconds += deltaSeconds;
            Game02MsgManager.TryGet()?.NotifyWorkEditorExtLifetimeSeconds(prev, state.lifetimeWorkEditorExtWorkSeconds, GameManager.Instance);
            ProgressChanged?.Invoke();
        }

        public void NotifyMovieAccumulatedViews(long accumulatedViews)
        {
            long v = Math.Max(0L, accumulatedViews);
            if (v > state.maxMovieAccumulatedViewsObserved)
            {
                state.maxMovieAccumulatedViewsObserved = v;
                ProgressChanged?.Invoke();
            }
        }

        /// <summary>
        /// CatButton のクリックのたびに呼ぶ。異星人解放とは無関係のため <see cref="ProgressChanged"/> は出さない。
        /// </summary>
        public void NotifyCatButtonPressed()
        {
            state.lifetimeCatButtonPressCount = Math.Min(int.MaxValue - 1,
                state.lifetimeCatButtonPressCount + 1);
        }

        public int LifetimeWorkMovie1UploadCount => state.lifetimeWorkMovie1UploadCount;
        public int LifetimeBuzzMovieUploadCount => state.lifetimeBuzzMovieUploadCount;
        public int LifetimeSidePanelOpenCount => state.lifetimeSidePanelOpenCount;
        public int LifetimeWorkStreamingCompleteCount => state.lifetimeWorkStreamingCompleteCount;
        public int LifetimeWorkEditorCompleteCount => state.lifetimeWorkEditorCompleteCount;
        public int LifetimeCatButtonPressCount => state.lifetimeCatButtonPressCount;

        /// <summary>WorkEditorExt 演出で蓄積した秒（セーブ同期）。</summary>
        public float LifetimeWorkEditorExtWorkSeconds => state.lifetimeWorkEditorExtWorkSeconds;

        public bool UnlockedParson01 => state.unlockedParson01;
        public bool UnlockedParson02 => state.unlockedParson02;
        public bool UnlockedParson03 => state.unlockedParson03;
        public bool UnlockedParson04 => state.unlockedParson04;
        public bool UnlockedParson05 => state.unlockedParson05;
        public bool UnlockedParsonL01 => state.unlockedParsonL01;
        public bool UnlockedParsonR01 => state.unlockedParsonR01;
        public bool UnlockedParsonR02 => state.unlockedParsonR02;
        public bool UnlockedSideParson01 => state.unlockedSideParson01;
        public bool UnlockedSideParson02 => state.unlockedSideParson02;

        public void AssignUnlockFlags(
            bool p01, bool p02, bool p03, bool p04, bool p05,
            bool pl01, bool pr01, bool pr02, bool s01, bool s02)
        {
            if (state.unlockedParson01 == p01 &&
                state.unlockedParson02 == p02 &&
                state.unlockedParson03 == p03 &&
                state.unlockedParson04 == p04 &&
                state.unlockedParson05 == p05 &&
                state.unlockedParsonL01 == pl01 &&
                state.unlockedParsonR01 == pr01 &&
                state.unlockedParsonR02 == pr02 &&
                state.unlockedSideParson01 == s01 &&
                state.unlockedSideParson02 == s02)
            {
                return;
            }

            state.unlockedParson01 = p01;
            state.unlockedParson02 = p02;
            state.unlockedParson03 = p03;
            state.unlockedParson04 = p04;
            state.unlockedParson05 = p05;
            state.unlockedParsonL01 = pl01;
            state.unlockedParsonR01 = pr01;
            state.unlockedParsonR02 = pr02;
            state.unlockedSideParson01 = s01;
            state.unlockedSideParson02 = s02;
            ProgressChanged?.Invoke();
        }

        public static int CountPurchasedEditorBuysInScene()
        {
            EditorBuyController[] buys = FindObjectsByType<EditorBuyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < buys.Length; i++)
            {
                if (buys[i] != null && buys[i].IsPurchased)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
