using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game02
{
    [Serializable]
    public sealed class Game02SaveData
    {
        public const int CurrentVersion = 5;

        public int version = CurrentVersion;
        [FormerlySerializedAs("savedAtUtc")]
        public string updatedAtUtc = string.Empty;
        public string buildVersion = string.Empty;
        public GameManagerState gameManagerState = new GameManagerState();
        public UpgradesState upgradesState = new UpgradesState();
        public WorldState worldState = new WorldState();
        public AlienProgressState alienProgress = new AlienProgressState();
    }

    [Serializable]
    public sealed class AlienProgressState
    {
        public bool moneyInitialGrantRecorded;
        public long lifetimeTotalMoneyEarned;
        public long lifetimeTotalMoneySpent;
        public int lifetimeWorkMovie1UploadCount;
        public int lifetimeBuzzMovieUploadCount;
        public int lifetimeSidePanelOpenCount;
        public int lifetimeWorkStreamingCompleteCount;
        public int lifetimeWorkEditorCompleteCount;
        public float lifetimeWorkEditorExtWorkSeconds;
        public int lifetimePositiveBuzzEventCount;
        public long lifetimeBuzzPositiveDeltaSum;
        public long maxMovieAccumulatedViewsObserved;
        public int lifetimeCatButtonPressCount;
        public bool unlockedParson01;
        public bool unlockedParson02;
        public bool unlockedParson03;
        public bool unlockedParson04;
        public bool unlockedParson05;
        public bool unlockedParsonL01;
        public bool unlockedParsonR01;
        public bool unlockedParsonR02;
        public bool unlockedSideParson01;
        public bool unlockedSideParson02;
        public List<UpgradePurchaseLogEntry> upgradePurchaseLog = new List<UpgradePurchaseLogEntry>();
    }

    [Serializable]
    public sealed class UpgradePurchaseLogEntry
    {
        public string purchasedAtUtc = string.Empty;
        public float gameplayElapsedSeconds;
        public string productId = string.Empty;
        public int ordinalForProduct;
        public long pricePaid;
    }

    [Serializable]
    public sealed class GameManagerState
    {
        public long currentMoney;
        public long currentPopularity;
        public long currentBuzz;
        public float gameplayElapsedSeconds;
        public int gameSpeedStepIndex;
        public bool isPaused;
        public bool hasConsumedFirstEditSecondsOverride;
        public int lifetimeEditWorkSessionsStarted;
        public bool hasConsumedFirstUploadFirstIncomeBonus;

        /// <summary>人気マイルストーン表示済みビット（<see cref="Game02MsgManager.PopularityMilestoneThresholds"/> と対応）。</summary>
        public ulong popularityMilestoneMsgShownMask;

        public bool hasShownFirstFanParson01Message;
        public bool hasShownCatNeko100Message;
        public bool hasShownCatNeko1000Message;

        public bool hasShownParsonR01UnlockMessage;
        public bool hasShownBuzzMovieCount1Message;
        public bool hasShownBuzzMovieCount10Message;
        public bool hasShownGameplayElapsed1HourMessage;
        public bool hasShownGameplayElapsed100HourMessage;
        public bool hasShownFirstTrashDropMessage;
        public bool hasShownFirstWorkEditorExtAssignMessage;
        public bool hasShownWorkEditorExt15MinutesMessage;
        public bool hasShownFirstWorkMovieSlot1UploadMessage;
        public bool hasShownFirstMailCharaWorkEditor1Message;
        public bool hasShownFirstItemEditorWorkEditor1Message;
        public bool hasShownFirstEd51AutomationMessage;
        public bool hasShownFirstWorkUpgradeStMessage;

        /// <summary>セーブ v2 まで。移行時のみ読み取り、新規セーブでは書き戻さない。</summary>
        public bool hasPlayedMovieRateTierStage1Se;

        public bool hasPlayedMovieRateTierStage2Se;
        public bool hasPlayedMovieRateTierGoalSe;
        public bool hasShownFirstMovieIncomeMessage;
        public bool hasShownSupportEndedMessage;
        public bool hasReceivedSupportReward;
        /// <summary>ゲームクリア演出（GameClearedPanel 表示・一度きり）を既に行ったか。セーブで保持する。</summary>
        public bool hasShownGameClearedPanel;
        public bool isPreGameSequenceActive;
        public bool gameplayTimerStarted;
        public bool isGameCleared;
        public int itemStreamSequence;
        public int itemMovieSequence;
    }

    [Serializable]
    public sealed class UpgradesState
    {
        public int purchasedEditorSlotAdds;
        public int workUpgradeEd01PurchaseCount;
        public int workUpgradeEd02PurchaseCount;
        public int workUpgradeEd03PurchaseCount;
        public int workUpgradeEd04PurchaseCount;
        public int workUpgradeEd05PurchaseCount;
        public int workUpgradeEd51PurchaseCount;
        public int streamingRelatedUpgradeTotalCount;
        public int streamEnvironmentUpgradeCount;
        public int talkPowerUpgradeLevel;
        public int trendPowerUpgradeLevel;
        public int seoPowerUpgradeLevel;
        public int streamEnvironmentUpgradeLevel;
    }

    [Serializable]
    public sealed class WorldState
    {
        public List<SpawnedItemState> looseItems = new List<SpawnedItemState>();
        public List<EditorBuyState> editorBuys = new List<EditorBuyState>();
        public List<WorkEditorState> workEditors = new List<WorkEditorState>();
        public List<WorkEditorExtState> workEditorExts = new List<WorkEditorExtState>();
        public List<WorkStreamingState> workStreamings = new List<WorkStreamingState>();
        public List<WorkUpgradeWorkplaceState> workUpgradeWorkplaces = new List<WorkUpgradeWorkplaceState>();
        public List<WorkMovieSlotState> workMovieSlots = new List<WorkMovieSlotState>();
        public WorkMovieUploadState workMovieUpload = new WorkMovieUploadState();
    }

    [Serializable]
    public sealed class EditorBuyState
    {
        public string objectName = string.Empty;
        public ItemType targetItemType;
        public bool isPurchased;
    }

    [Serializable]
    public sealed class SpawnedItemState
    {
        public ItemType itemType;
        public string itemName = string.Empty;
        public string parentPath = string.Empty;
        public float anchoredX;
        public float anchoredY;
        public long streamSpawnPopularity;
        public string streamGenre = string.Empty;
        public long moviePower;
        public long movieBuzzGain;
        public string movieName = string.Empty;
    }

    [Serializable]
    public sealed class WorkEditorState
    {
        public string objectName = string.Empty;
        public bool isDormant;
        public bool isWorking;
        public bool hasEditorAssigned;
        public int rememberedEditorKind;
        public string rememberedEditorDisplayName = string.Empty;
        public string rememberedEditorSpriteName = string.Empty;
        public float rememberedEditorEditSpeed = 1f;
        public float rememberedEditorPopularityMultiplier = 1f;
        public long rememberedEditorBuzzBaseValue;
        public long rememberedStreamPopularity;
        public string rememberedStreamGenre = string.Empty;
        public float elapsedWorkSeconds;
        public float workVisualElapsedSeconds;
        public float currentSessionRequiredWorkSeconds;
    }

    [Serializable]
    public sealed class WorkEditorExtState
    {
        public string objectName = string.Empty;
        public bool hasEditorAssigned;
        public int rememberedEditorType;
        public string rememberedEditorDisplayName = string.Empty;
        public string rememberedEditorSpriteName = string.Empty;
    }

    [Serializable]
    public sealed class WorkStreamingState
    {
        public string objectName = string.Empty;
        public bool isAcceptingItems;
        public bool isWorking;
        public bool hasAcceptedRuntimeItem;
        public string acceptedSpriteName = string.Empty;
        public float elapsedWorkSeconds;
        public float workVisualElapsedSeconds;
        public float currentSessionRequiredWorkSeconds;
    }

    [Serializable]
    public sealed class WorkUpgradeWorkplaceState
    {
        public string objectName = string.Empty;
        public bool isAcceptingItems;
        public bool isWorking;
        public bool hasAcceptedRuntimeItem;
        public string acceptedSpriteName = string.Empty;
        public float elapsedWorkSeconds;
        public float workVisualElapsedSeconds;
    }

    [Serializable]
    public sealed class WorkMovieSlotState
    {
        public int slotIndex;
        public bool isWorkplaceAvailable;
    }

    [Serializable]
    public sealed class WorkMovieUploadState
    {
        public long totalViews;
        public float viewsTickAccumulator;
        public bool hasAssignedVeryFirstUploadedMovie;
        public List<WorkMovieUploadSessionState> sessions = new List<WorkMovieUploadSessionState>();
    }

    [Serializable]
    public sealed class WorkMovieUploadSessionState
    {
        public int index;
        public bool isWorking;
        public bool isVeryFirstUploadedMovie;
        public bool hasAppliedFirstIncomeBonus;
        public int moneyPayoutCount;
        public bool hasAppliedFixedIncomeAtTenthTiming;
        public string movieName = string.Empty;
        public string displaySpriteName = string.Empty;
        public long moviePopularityBase;
        public long movieBuzzGain;
        public bool isBuzzMovie;
        public float elapsedSeconds;
        public long accumulatedViews;
        public long pendingMonetizedViews;
        public double pendingViewFraction;
        public double pendingMoneyFraction;
        public long lastMoneyDeltaDisplay;
        public bool hasAppliedInitialViews;
        public int fiveSecondTickCount;
        public int moneyTickAccumulator;
        public bool isMoneyAddFloating;
        public long moneyAddFloatingValue;
        public float moneyAddFloatingElapsedSeconds;
        public bool isBuzzEffectActive;
        public bool isBuzzEffectStopRequested;
        public float buzzEffectCycleElapsedSeconds;
        public bool isBuzzBgmRequested;
    }
}
