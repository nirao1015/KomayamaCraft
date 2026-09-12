using System;
using System.Collections.Generic;
using UnityEngine;

public enum Game03BossUgRequestSource
{
    Debug,
    BossItemDrop,
}

/// <summary>
/// BossUG（ボスアイテム）の武器抽選・別衣装適用・演出リクエスト。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03BossUgManager : MonoBehaviour
{
    [Serializable]
    public struct BossUgOutfitCopy
    {
        [Tooltip("武器番号 1〜7。装備中かつ未獲得のときのみ抽選対象。")]
        public int weaponNumber;
        [Tooltip("ImageExtraOutfit の番号（1=ImageExtraOutfit01、2=ImageExtraOutfit02…）。0 は当該武器の共通フォールバック。")]
        public int outfitIndex;
        public string displayName;
        [TextArea(2, 5)] public string description;
    }

    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03LevelUpManager levelUpManager;
    [SerializeField] private Game03UnitUgManager unitUgManager;
    [SerializeField] private Game03WeaponManager weaponManager;
    [SerializeField] private Game03BossUgPresentationController presentationController;
    [SerializeField] private BossUgOutfitCopy[] weaponCopyByNumber = Array.Empty<BossUgOutfitCopy>();
    [SerializeField, Min(1), Tooltip("1 ランあたりの BossUG イベント上限。")]
    private int maxBossUgEventsPerRun = 2;

    private readonly HashSet<int> weaponsWithBossOutfitThisRun = new HashSet<int>();
    private readonly Queue<Game03BossUgRequestSource> pendingSources = new Queue<Game03BossUgRequestSource>();
    private readonly List<Game03WeaponManager.UpgradeWeaponCandidate> candidateScratch = new List<Game03WeaponManager.UpgradeWeaponCandidate>(8);
    private readonly List<Sprite> buzzSpriteScratch = new List<Sprite>(8);
    private int bossUgEventsCompletedThisRun;
    private bool presentationRunning;

    public bool IsBossUgPanelOpen =>
        presentationController != null && presentationController.IsPanelOpen;

    public bool IsPresentationRunning => presentationRunning;

    private void Awake()
    {
        if (game03Manager == null)
        {
            game03Manager = FindAnyObjectByType<Game03Manager>(FindObjectsInactive.Include);
        }

        if (levelUpManager == null)
        {
            levelUpManager = FindAnyObjectByType<Game03LevelUpManager>(FindObjectsInactive.Include);
        }

        if (unitUgManager == null)
        {
            unitUgManager = FindAnyObjectByType<Game03UnitUgManager>(FindObjectsInactive.Include);
        }

        if (weaponManager == null)
        {
            weaponManager = FindAnyObjectByType<Game03WeaponManager>(FindObjectsInactive.Include);
        }

        if (presentationController == null)
        {
            presentationController = FindAnyObjectByType<Game03BossUgPresentationController>(FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        if (levelUpManager != null)
        {
            levelUpManager.LevelUpPanelClosed += OnLevelUpPanelClosed;
        }
    }

    private void OnDisable()
    {
        if (levelUpManager != null)
        {
            levelUpManager.LevelUpPanelClosed -= OnLevelUpPanelClosed;
        }
    }

    public void DebugRequestPresentation()
    {
        TryRequestPresentation(Game03BossUgRequestSource.Debug);
    }

    public bool TryRequestPresentation(Game03BossUgRequestSource source)
    {
        if (Game03RunSessionState.IsRunEnded)
        {
            return false;
        }

        if (bossUgEventsCompletedThisRun >= maxBossUgEventsPerRun)
        {
            Debug.LogWarning("[Game03BossUg] ラン中の BossUG 回数上限に達したため開始しません。", this);
            return false;
        }

        if (!HasRemainingWeaponPool())
        {
            Debug.LogWarning("[Game03BossUg] 抽選可能な装備武器がありません。", this);
            return false;
        }

        if (presentationController == null || weaponManager == null)
        {
            Debug.LogWarning("[Game03BossUg] 参照が未設定のため BossUG を開始しません。", this);
            return false;
        }

        if (IsBlockedByOtherPresentation())
        {
            pendingSources.Enqueue(source);
            return true;
        }

        if (presentationRunning || IsBossUgPanelOpen)
        {
            pendingSources.Enqueue(source);
            return true;
        }

        return TryStartNextPresentation();
    }

    public void NotifyExternalQueueTick()
    {
        TryProcessPendingQueue();
    }

    private void OnLevelUpPanelClosed()
    {
        NotifyExternalQueueTick();
        unitUgManager?.NotifyExternalQueueTick();
    }

    private void OnPresentationFinished()
    {
        presentationRunning = false;
        NotifyExternalQueueTick();
        unitUgManager?.NotifyExternalQueueTick();
    }

    private void TryProcessPendingQueue()
    {
        if (presentationRunning)
        {
            return;
        }

        if (Game03RunSessionState.IsRunEnded)
        {
            pendingSources.Clear();
            return;
        }

        if (levelUpManager != null && levelUpManager.IsLevelUpPanelOpen)
        {
            return;
        }

        if (IsBlockedByOtherPresentation())
        {
            return;
        }

        while (pendingSources.Count > 0 && !presentationRunning)
        {
            pendingSources.Dequeue();
            if (TryStartNextPresentation())
            {
                break;
            }
        }
    }

    private bool IsBlockedByOtherPresentation()
    {
        if (unitUgManager != null && (unitUgManager.IsPresentationRunning || unitUgManager.IsUnitUgPanelOpen))
        {
            return true;
        }

        return false;
    }

    private bool HasRemainingWeaponPool()
    {
        if (weaponManager == null)
        {
            return false;
        }

        weaponManager.CollectEquippedWeaponCandidatesExcluding(weaponsWithBossOutfitThisRun, candidateScratch);
        return candidateScratch.Count > 0;
    }

    private bool TryStartNextPresentation()
    {
        if (presentationRunning || presentationController == null || weaponManager == null)
        {
            return false;
        }

        if (Game03RunSessionState.IsRunEnded || bossUgEventsCompletedThisRun >= maxBossUgEventsPerRun)
        {
            return false;
        }

        if (levelUpManager != null && levelUpManager.IsLevelUpPanelOpen)
        {
            return false;
        }

        if (IsBlockedByOtherPresentation())
        {
            return false;
        }

        weaponManager.CollectEquippedWeaponCandidatesExcluding(weaponsWithBossOutfitThisRun, candidateScratch);
        if (candidateScratch.Count == 0)
        {
            Debug.LogWarning("[Game03BossUg] 抽選可能な装備武器がありません。", this);
            return false;
        }

        int pickIndex = UnityEngine.Random.Range(0, candidateScratch.Count);
        Game03WeaponManager.UpgradeWeaponCandidate winner = candidateScratch[pickIndex];
        int weaponNumber = winner.WeaponNumber;

        if (!weaponManager.TryPickBossExtraOutfitSprite(weaponNumber, out Sprite outfitSprite, out int outfitIndex)
            || outfitSprite == null)
        {
            Debug.LogWarning($"[Game03BossUg] Weapon{weaponNumber:D2} の別衣装スプライトを取得できません。", this);
            return false;
        }

        ResolveOutfitCopy(weaponNumber, outfitIndex, out string displayName, out string description);
        CollectBuzzSprites(weaponNumber, winner.WeaponSprite, buzzSpriteScratch);

        var payload = new Game03BossUgPresentationController.BossUgPresentationPayload
        {
            weaponNumber = weaponNumber,
            equippedUnitRect = winner.EquippedUnitRect,
            weaponBodySprite = winner.WeaponSprite,
            outfitSprite = outfitSprite,
            displayName = displayName,
            description = description,
        };

        weaponsWithBossOutfitThisRun.Add(weaponNumber);
        bossUgEventsCompletedThisRun++;
        presentationRunning = true;
        presentationController.BeginPresentation(payload, buzzSpriteScratch, OnPresentationFinished, this);
        return true;
    }

    private void CollectBuzzSprites(int winnerWeaponNumber, Sprite winnerBodySprite, List<Sprite> destination)
    {
        destination.Clear();
        if (candidateScratch.Count <= 1)
        {
            return;
        }

        for (int i = 0; i < candidateScratch.Count; i++)
        {
            Game03WeaponManager.UpgradeWeaponCandidate candidate = candidateScratch[i];
            if (candidate.WeaponNumber == winnerWeaponNumber)
            {
                continue;
            }

            if (candidate.WeaponSprite != null)
            {
                destination.Add(candidate.WeaponSprite);
            }
        }

        if (winnerBodySprite != null && destination.Count == 0)
        {
            for (int i = 0; i < candidateScratch.Count; i++)
            {
                Sprite sprite = candidateScratch[i].WeaponSprite;
                if (sprite != null && sprite != winnerBodySprite)
                {
                    destination.Add(sprite);
                }
            }
        }
    }

    private void ResolveOutfitCopy(int weaponNumber, int outfitIndex, out string displayName, out string description)
    {
        displayName = $"Weapon {weaponNumber}";
        description = string.Empty;
        if (weaponCopyByNumber == null || weaponCopyByNumber.Length == 0)
        {
            return;
        }

        bool hasWeaponFallback;
        BossUgOutfitCopy weaponFallback = default;
        hasWeaponFallback = false;

        for (int i = 0; i < weaponCopyByNumber.Length; i++)
        {
            BossUgOutfitCopy entry = weaponCopyByNumber[i];
            if (entry.weaponNumber != weaponNumber)
            {
                continue;
            }

            if (entry.outfitIndex == outfitIndex)
            {
                ApplyOutfitCopy(entry, ref displayName, ref description);
                return;
            }

            if (entry.outfitIndex == 0)
            {
                weaponFallback = entry;
                hasWeaponFallback = true;
            }
        }

        if (hasWeaponFallback)
        {
            ApplyOutfitCopy(weaponFallback, ref displayName, ref description);
        }
    }

    private static void ApplyOutfitCopy(BossUgOutfitCopy entry, ref string displayName, ref string description)
    {
        if (!string.IsNullOrEmpty(entry.displayName))
        {
            displayName = entry.displayName;
        }

        description = entry.description ?? string.Empty;
    }

    public void ResetForNewRun()
    {
        weaponsWithBossOutfitThisRun.Clear();
        pendingSources.Clear();
        bossUgEventsCompletedThisRun = 0;
        presentationRunning = false;
        presentationController?.ForceCloseImmediate();
    }
}
