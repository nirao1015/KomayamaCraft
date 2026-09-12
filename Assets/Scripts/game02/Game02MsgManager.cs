using System;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// MsgOb（<see cref="Game02MessagePresenter"/>）への表示と SE を一元管理する。
    /// 表示済みフラグは <see cref="GameManagerState"/> に保持する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game02MsgManager : MonoBehaviour
    {
        public static readonly long[] PopularityMilestoneThresholds =
        {
            1_000L,
            10_000L,
            100_000L,
            1_000_000L,
            10_000_000L,
            100_000_000L,
            1_000_000_000L,
            10_000_000_000L
        };

        /// <summary>このインデックス未満のマイルストーンでは単価行付きテンプレートを使う。</summary>
        public const int PopularityMilestoneWithUnitPriceCount = 3;

        private const long TenMillionPopularityAchievementThreshold = 10_000_000L;

        private static Game02MsgManager instance;

        [Header("Refs")]
        [SerializeField, Tooltip("未設定時は MsgOb を探索して Game02MessagePresenter を確保します。")]
        private Game02MessagePresenter messagePresenter;

        [SerializeField, Tooltip("メイン編集仕事場（MailChara / ItemEditor 初回メッセージの対象）。この Transform と同一であるときのみ通知します。")]
        private Transform primaryWorkEditorTutorialRoot;

        [Header("文言（異星人・猫）")]
        [SerializeField]
        private string firstFanParson01Message = "初めてのファンを獲得しました";

        [SerializeField]
        private string catNekoMessageTemplate = "NEKOと {push_count} 回遊びました";

        [Header("文言（人気マイルストーン）")]
        [SerializeField]
        private string popularityWithUnitPriceTemplate =
            "人気 {popular} 達成！\n広告単価アップ！ {unit_price}円/再生";

        [SerializeField]
        private string popularityHighOnlyTemplate = "人気 {popular} 達成！";

        [Header("文言（進行・ネタ）")]
        [SerializeField]
        private string parsonR01UnlockMessage = "編集のプロが味方に！";

        [SerializeField]
        private string buzzMovieCount1Message = "バズ動画デビュー";

        [SerializeField]
        private string buzzMovieCount10Message = "\u2726\u22C6\u30B3\u30BA\u30DF\u30C3\u30AF\u30D0\u30BA\u25C8\u25C7";

        [SerializeField]
        private string gameplayElapsed1HourMessage = "ゲーム開始してから1時間経過";

        [SerializeField]
        private string gameplayElapsed100HourMessage = "ゲーム開始してから100時間経過";

        [SerializeField]
        private string firstTrashDropMessage = "整理整頓から始まる成功";

        [SerializeField]
        private string firstWorkEditorExtAssignMessage = "アイデア出しに散歩は必要";

        [SerializeField]
        private string workEditorExt15MinutesMessage = "歩くの楽しいなぁ";

        [SerializeField]
        private string firstWorkMovieSlot1UploadMessage = "初めての動画公開";

        [SerializeField]
        private string firstMailCharaWorkEditor1Message = "最初の動画は自分で編集";

        [SerializeField]
        private string firstItemEditorWorkEditor1Message = "動画編集はプロに任せるのが一番";

        [SerializeField]
        private string firstEd51AutomationMessage = "自動化は神";

        [SerializeField]
        private string firstWorkUpgradeStMessage = "良い配信は\n良い環境から生まれる";

        private const float GameplaySeconds1Hour = 3600f;
        private const float GameplaySeconds100Hours = 360000f;
        private const float WorkEditorExt15MinutesSeconds = 900f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static Game02MsgManager TryGet()
        {
            if (instance != null)
            {
                return instance;
            }

            return FindAnyObjectByType<Game02MsgManager>(FindObjectsInactive.Include);
        }

        /// <summary>セーブ適用後、現在人気で未到達ではないマイルストーンのビットを黙って立てる。</summary>
        public static ulong MergeSilentCatchupPopularityMask(ulong existing, long popularity)
        {
            ulong mask = existing;
            long p = Math.Max(0L, popularity);
            for (int i = 0; i < PopularityMilestoneThresholds.Length; i++)
            {
                if (p >= PopularityMilestoneThresholds[i])
                {
                    mask |= 1ul << i;
                }
            }

            return mask;
        }

        /// <summary>1000万人気初回到達の Steam 実績（表示済みマスクとは独立）。</summary>
        public static void TryUnlockTenMillionPopularityAchievementIfReached(long previousPopularity, long nextPopularity)
        {
            long before = Math.Max(0L, previousPopularity);
            long after = Math.Max(0L, nextPopularity);
            if (before < TenMillionPopularityAchievementThreshold && after >= TenMillionPopularityAchievementThreshold)
            {
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_07);
            }
        }

        /// <summary>セーブ読込後など、すでに 1000万 以上のときの実績救済。</summary>
        public static void TryUnlockTenMillionPopularityAchievementIfAlreadyAt(long currentPopularity)
        {
            if (Math.Max(0L, currentPopularity) >= TenMillionPopularityAchievementThreshold)
            {
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_07);
            }
        }

        internal static void MigrateGameManagerStateFromV2ToV3(GameManagerState s)
        {
            if (s == null)
            {
                return;
            }

            ulong mask = s.popularityMilestoneMsgShownMask;
            const long legacyStage1 = 10_000L;
            const long legacyStage2 = 100_000L;
            const long legacyGoal = 1_000_000L;
            if (s.hasPlayedMovieRateTierStage1Se)
            {
                mask = MergeSilentCatchupPopularityMask(mask, legacyStage1);
            }

            if (s.hasPlayedMovieRateTierStage2Se)
            {
                mask = MergeSilentCatchupPopularityMask(mask, legacyStage2);
            }

            if (s.hasPlayedMovieRateTierGoalSe)
            {
                mask = MergeSilentCatchupPopularityMask(mask, legacyGoal);
            }

            mask = MergeSilentCatchupPopularityMask(mask, s.currentPopularity);
            s.popularityMilestoneMsgShownMask = mask;
        }

        /// <summary>EditorBuy01/02/03 のいずれかを初めて購入した直後に呼ぶ（一度だけ）。</summary>
        public void NotifyFirstEditorBuyPurchased(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownParsonR01UnlockMessage)
            {
                return;
            }

            gameManager.MarkParsonR01UnlockMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(parsonR01UnlockMessage) ? "編集のプロが味方に！" : parsonR01UnlockMessage,
                gameManager.GetMessageHoldSeconds());
        }

        /// <summary>Parson01 が非表示→表示になったタイミングで呼ぶ（セーブの一度きりフラグを見る）。</summary>
        public void NotifyParson01BecameVisible(GameManager gameManager)
        {
            if (gameManager == null)
            {
                return;
            }

            if (gameManager.HasShownFirstFanParson01Message)
            {
                return;
            }

            gameManager.MarkFirstFanParson01MessageShown();
            string message = string.IsNullOrEmpty(firstFanParson01Message)
                ? "初めてのファンを獲得しました"
                : firstFanParson01Message;
            PresentGeneric(message, gameManager.GetMessageHoldSeconds());
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_02);
        }

        /// <summary>生涯 CatButton 回数が更新された直後に呼ぶ。</summary>
        public void NotifyLifetimeCatButtonCount(int lifetimePushCount, GameManager gameManager)
        {
            if (gameManager == null)
            {
                return;
            }

            int c = Mathf.Max(0, lifetimePushCount);
            string tpl = string.IsNullOrEmpty(catNekoMessageTemplate)
                ? "NEKOと {push_count} 回遊びました"
                : catNekoMessageTemplate;

            if (!gameManager.HasShownCatNeko100Message && c >= 100)
            {
                gameManager.MarkCatNeko100MessageShown();
                PresentGeneric(tpl.Replace("{push_count}", "100"), gameManager.GetMessageHoldSeconds());
                SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_05);
            }

            if (!gameManager.HasShownCatNeko1000Message && c >= 1000)
            {
                gameManager.MarkCatNeko1000MessageShown();
                PresentGeneric(tpl.Replace("{push_count}", "1000"), gameManager.GetMessageHoldSeconds());
            }
        }

        public void NotifyBuzzMovieUploadCount(int lifetimeBuzzUploadCount, GameManager gameManager)
        {
            if (gameManager == null)
            {
                return;
            }

            int n = Mathf.Max(0, lifetimeBuzzUploadCount);
            if (!gameManager.HasShownBuzzMovieCount1Message && n >= 1)
            {
                gameManager.MarkBuzzMovieCount1MessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(buzzMovieCount1Message) ? "バズ動画デビュー" : buzzMovieCount1Message,
                    gameManager.GetMessageHoldSeconds());
            }

            if (!gameManager.HasShownBuzzMovieCount10Message && n >= 10)
            {
                gameManager.MarkBuzzMovieCount10MessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(buzzMovieCount10Message)
                    ? "\u2726\u22C6\u30B3\u30BA\u30DF\u30C3\u30AF\u30D0\u30BA\u25C8\u25C7"
                    : buzzMovieCount10Message,
                    gameManager.GetMessageHoldSeconds());
            }
        }

        public void NotifyWorkMovieSlot1UploadCount(int lifetimeSlot1UploadCount, GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstWorkMovieSlot1UploadMessage)
            {
                return;
            }

            if (lifetimeSlot1UploadCount < 1)
            {
                return;
            }

            gameManager.MarkFirstWorkMovieSlot1UploadMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstWorkMovieSlot1UploadMessage) ? "初めての動画公開" : firstWorkMovieSlot1UploadMessage,
                gameManager.GetMessageHoldSeconds());
        }

        public void NotifyGameplayHourMilestones(float previousElapsedSeconds, float nextElapsedSeconds, GameManager gameManager)
        {
            if (gameManager == null || nextElapsedSeconds <= previousElapsedSeconds)
            {
                return;
            }

            if (!gameManager.HasShownGameplayElapsed1HourMessage &&
                previousElapsedSeconds < GameplaySeconds1Hour &&
                nextElapsedSeconds >= GameplaySeconds1Hour)
            {
                gameManager.MarkGameplayElapsed1HourMessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(gameplayElapsed1HourMessage)
                        ? "ゲーム開始してから1時間経過"
                        : gameplayElapsed1HourMessage,
                    gameManager.GetMessageHoldSeconds());
            }

            if (!gameManager.HasShownGameplayElapsed100HourMessage &&
                previousElapsedSeconds < GameplaySeconds100Hours &&
                nextElapsedSeconds >= GameplaySeconds100Hours)
            {
                gameManager.MarkGameplayElapsed100HourMessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(gameplayElapsed100HourMessage)
                        ? "ゲーム開始してから100時間経過"
                        : gameplayElapsed100HourMessage,
                    gameManager.GetMessageHoldSeconds());
            }
        }

        public void NotifyFirstTrashDrop(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstTrashDropMessage)
            {
                return;
            }

            gameManager.MarkFirstTrashDropMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstTrashDropMessage) ? "整理整頓から始まる成功" : firstTrashDropMessage,
                gameManager.GetMessageHoldSeconds());
        }

        public void NotifyFirstWorkEditorExtAssigned(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstWorkEditorExtAssignMessage)
            {
                return;
            }

            gameManager.MarkFirstWorkEditorExtAssignMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstWorkEditorExtAssignMessage) ? "アイデア出しに散歩は必要" : firstWorkEditorExtAssignMessage,
                gameManager.GetMessageHoldSeconds());
            SteamAchievementController.TryUnlock(SteamAchievementIds.Game02_04);
        }

        public void NotifyWorkEditorExtLifetimeSeconds(float previousTotalSeconds, float nextTotalSeconds, GameManager gameManager)
        {
            if (gameManager == null ||
                gameManager.HasShownWorkEditorExt15MinutesMessage ||
                nextTotalSeconds <= previousTotalSeconds)
            {
                return;
            }

            if (previousTotalSeconds < WorkEditorExt15MinutesSeconds && nextTotalSeconds >= WorkEditorExt15MinutesSeconds)
            {
                gameManager.MarkWorkEditorExt15MinutesMessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(workEditorExt15MinutesMessage) ? "歩くの楽しいなぁ" : workEditorExt15MinutesMessage,
                    gameManager.GetMessageHoldSeconds());
            }
        }

        /// <summary><see cref="primaryWorkEditorTutorialRoot"/> と同一の仕事場でのみ、ItemMailChara / ItemEditor 初回メッセージ。</summary>
        public void NotifyWorkEditorPrimaryAcceptedEditorItem(GameManager gameManager, UnityEngine.Transform workplaceRoot, ItemType itemType)
        {
            if (gameManager == null || workplaceRoot == null || primaryWorkEditorTutorialRoot == null)
            {
                return;
            }

            if (workplaceRoot != primaryWorkEditorTutorialRoot)
            {
                return;
            }

            if (itemType == ItemType.ItemMailChara)
            {
                if (gameManager.HasShownFirstMailCharaWorkEditor1Message)
                {
                    return;
                }

                gameManager.MarkFirstMailCharaWorkEditor1MessageShown();
                PresentGeneric(
                    string.IsNullOrEmpty(firstMailCharaWorkEditor1Message) ? "最初の動画は自分で編集" : firstMailCharaWorkEditor1Message,
                    gameManager.GetMessageHoldSeconds());
                return;
            }

            if (!IsItemEditorKind(itemType))
            {
                return;
            }

            if (gameManager.HasShownFirstItemEditorWorkEditor1Message)
            {
                return;
            }

            gameManager.MarkFirstItemEditorWorkEditor1MessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstItemEditorWorkEditor1Message) ? "動画編集はプロに任せるのが一番" : firstItemEditorWorkEditor1Message,
                gameManager.GetMessageHoldSeconds());
        }

        private static bool IsItemEditorKind(ItemType itemType)
        {
            return itemType == ItemType.ItemEditor01 ||
                   itemType == ItemType.ItemEditor02 ||
                   itemType == ItemType.ItemEditor03;
        }

        public void NotifyFirstEd51AutomationUsed(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstEd51AutomationMessage)
            {
                return;
            }

            gameManager.MarkFirstEd51AutomationMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstEd51AutomationMessage) ? "自動化は神" : firstEd51AutomationMessage,
                gameManager.GetMessageHoldSeconds());
        }

        public void NotifyFirstWorkUpgradeStUsed(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstWorkUpgradeStMessage)
            {
                return;
            }

            gameManager.MarkFirstWorkUpgradeStMessageShown();
            PresentGeneric(
                string.IsNullOrEmpty(firstWorkUpgradeStMessage) ? "良い配信は\n良い環境から生まれる" : firstWorkUpgradeStMessage,
                gameManager.GetMessageHoldSeconds());
        }

        public void NotifyPopularityIncreased(long previousPopularity, long nextPopularity, GameManager gameManager)
        {
            if (gameManager == null)
            {
                return;
            }

            long before = Math.Max(0L, previousPopularity);
            long after = Math.Max(0L, nextPopularity);
            if (after <= before)
            {
                return;
            }

            TryUnlockTenMillionPopularityAchievementIfReached(before, after);

            Game02SeManager se = Game02SeManager.TryGet();
            for (int i = 0; i < PopularityMilestoneThresholds.Length; i++)
            {
                long threshold = PopularityMilestoneThresholds[i];
                if (before >= threshold || after < threshold)
                {
                    continue;
                }

                if (!gameManager.TryClaimPopularityMilestoneBit(i))
                {
                    continue;
                }

                se?.PlayByCue(Game02SeCue.PopularityMilestone);
                string body = BuildPopularityMilestoneMessage(i, threshold, after, gameManager);
                PresentVisualOnly(body, gameManager.GetMessageHoldSeconds());
            }
        }

        /// <summary>初回の動画収入メッセージ（GameManager のフラグと連動）。</summary>
        public bool TryPresentFirstMovieIncomeMessage(GameManager gameManager)
        {
            if (gameManager == null || gameManager.HasShownFirstMovieIncomeMessage)
            {
                return false;
            }

            gameManager.MarkFirstMovieIncomeMessageShown();
            string message = gameManager.GetFirstMovieIncomeMessageText();
            PresentGeneric(message, gameManager.GetMessageHoldSeconds());
            return true;
        }

        /// <summary>運営援助終了メッセージ（呼び出し側で条件判定済み）。</summary>
        public void PresentSupportEndedMessage(string message, float holdSeconds)
        {
            PresentGeneric(message, holdSeconds);
        }

        /// <summary>デバッグ等：汎用 SE 付きでそのまま表示。</summary>
        public void PresentDebugAdHoc(string message, float holdSeconds)
        {
            PresentGeneric(message, holdSeconds);
        }

        private string BuildPopularityMilestoneMessage(
            int milestoneIndex,
            long milestoneThreshold,
            long popularityAfterCross,
            GameManager gameManager)
        {
            string popularTxt = Math.Max(0L, milestoneThreshold).ToString("N0");
            if (milestoneIndex < PopularityMilestoneWithUnitPriceCount)
            {
                double unitPrice = gameManager.ResolveMovieUnitPriceByPopularity(popularityAfterCross);
                string template = string.IsNullOrEmpty(popularityWithUnitPriceTemplate)
                    ? "人気 {popular} 達成！\n広告単価アップ！ {unit_price}円/再生"
                    : popularityWithUnitPriceTemplate;
                return template
                    .Replace("{popular}", popularTxt)
                    .Replace("{unit_price}", FormatUnitPrice(unitPrice));
            }

            string highTpl = string.IsNullOrEmpty(popularityHighOnlyTemplate)
                ? "人気 {popular} 達成！"
                : popularityHighOnlyTemplate;
            return highTpl.Replace("{popular}", popularTxt);
        }

        private static string FormatUnitPrice(double unitPrice)
        {
            double safe = System.Math.Max(0d, unitPrice);
            return safe.ToString("0.###");
        }

        private void PresentGeneric(string message, float holdSeconds)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.GenericMessage);
            PresentVisualOnly(message, holdSeconds);
        }

        private void PresentVisualOnly(string message, float holdSeconds)
        {
            Game02MessagePresenter presenter = ResolvePresenter();
            if (presenter == null)
            {
                return;
            }

            presenter.ShowMessage(message, holdSeconds, playGenericMessageSound: false);
        }

        private Game02MessagePresenter ResolvePresenter()
        {
            if (messagePresenter != null)
            {
                return messagePresenter;
            }

            return Game02MessagePresenter.EnsureSceneController();
        }
    }
}