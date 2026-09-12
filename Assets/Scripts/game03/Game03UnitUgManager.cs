using System;
using System.Collections.Generic;
using UnityEngine;

public enum Game03UnitUgRequestSource
{
    Debug,
    MidBossDrop,
}

/// <summary>
/// UnitUG の抽選・獲得フラグ・表示リクエストキュー。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03UnitUgManager : MonoBehaviour
{
    [Serializable]
    public struct UnitUgDefinition
    {
        public Game03UnitUgType type;
        public Sprite iconSprite;
        public string displayName;
        [TextArea(2, 5)] public string description;
    }

    [SerializeField] private Game03Manager game03Manager;
    [SerializeField] private Game03LevelUpManager levelUpManager;
    [SerializeField] private Game03BossUgManager bossUgManager;
    [SerializeField] private Game03UnitUgPresentationController presentationController;
    [SerializeField] private UnitUgDefinition[] definitions = Array.Empty<UnitUgDefinition>();

    private readonly HashSet<Game03UnitUgType> acquiredTypes = new HashSet<Game03UnitUgType>();
    private readonly Queue<Game03UnitUgRequestSource> pendingSources = new Queue<Game03UnitUgRequestSource>();
    private bool presentationRunning;

    public bool IsUnitUgPanelOpen =>
        presentationController != null && presentationController.IsPanelOpen;

    public bool IsPresentationRunning => presentationRunning;

    public bool HasRemainingPool()
    {
        return GetRemainingPoolCount() > 0;
    }

    public int GetRemainingPoolCount()
    {
        if (definitions == null || definitions.Length == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (!acquiredTypes.Contains(definitions[i].type))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsTypeAcquired(Game03UnitUgType type) => acquiredTypes.Contains(type);

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

        if (bossUgManager == null)
        {
            bossUgManager = FindAnyObjectByType<Game03BossUgManager>(FindObjectsInactive.Include);
        }

        if (presentationController == null)
        {
            presentationController = FindAnyObjectByType<Game03UnitUgPresentationController>(FindObjectsInactive.Include);
        }

        EnsureDefinitionsPopulated();
    }

    private void EnsureDefinitionsPopulated()
    {
        if (definitions == null || definitions.Length == 0)
        {
            definitions = CreateDefaultDefinitions();
            return;
        }

        bool anyValid = false;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (!string.IsNullOrEmpty(definitions[i].displayName))
            {
                anyValid = true;
                break;
            }
        }

        if (!anyValid)
        {
            definitions = CreateDefaultDefinitions();
        }
    }

    private static UnitUgDefinition[] CreateDefaultDefinitions()
    {
        return new[]
        {
            Def(Game03UnitUgType.ExpPickupRadius, "経験値吸引範囲 UP", "経験値を吸い寄せる範囲が広がる。"),
            Def(Game03UnitUgType.MoveSpeed, "移動速度 UP", "ユニットの移動速度が上がる。"),
            Def(Game03UnitUgType.AllWeaponCooldown, "全武器CD短縮", "装備中の全武器のクールダウンが短くなる。"),
            Def(Game03UnitUgType.AllWeaponAttackPower, "全武器攻撃力 UP", "装備中の全武器の攻撃力が上がる。"),
            Def(Game03UnitUgType.SupportCargoFrequency, "支援物資頻度 UP", "支援物資の到着が早くなる。"),
            Def(Game03UnitUgType.MaxHp, "最大HP UP", "最大HPが増える。"),
            Def(Game03UnitUgType.Armor, "アーマー UP", "受けるダメージが減る。"),
        };
    }

    private static UnitUgDefinition Def(Game03UnitUgType type, string name, string desc)
    {
        return new UnitUgDefinition
        {
            type = type,
            displayName = name,
            description = desc,
        };
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

    /// <summary>デバッグ: DebugPanel.ButtonUnitUG から呼ぶ。</summary>
    public void DebugRequestPresentation()
    {
        TryRequestPresentation(Game03UnitUgRequestSource.Debug);
    }

    public bool TryRequestPresentation(Game03UnitUgRequestSource source)
    {
        if (Game03RunSessionState.IsRunEnded)
        {
            return false;
        }

        if (!HasRemainingPool())
        {
            Debug.LogWarning("[Game03UnitUg] 抽選候補がすべて獲得済みのため UnitUG を開始しません。", this);
            return false;
        }

        if (presentationController == null)
        {
            Debug.LogWarning("[Game03UnitUg] Game03UnitUgPresentationController が未設定です。", this);
            return false;
        }

        if (presentationRunning || (presentationController != null && presentationController.IsPanelOpen))
        {
            pendingSources.Enqueue(source);
            return true;
        }

        if (bossUgManager != null && (bossUgManager.IsPresentationRunning || bossUgManager.IsBossUgPanelOpen))
        {
            pendingSources.Enqueue(source);
            return true;
        }

        if (levelUpManager != null && levelUpManager.IsLevelUpPanelOpen)
        {
            pendingSources.Enqueue(source);
            return true;
        }

        return TryStartNextPresentation();
    }

    private void OnLevelUpPanelClosed()
    {
        TryProcessPendingQueue();
        bossUgManager?.NotifyExternalQueueTick();
    }

    public void NotifyExternalQueueTick()
    {
        TryProcessPendingQueue();
    }

    private void OnPresentationFinished()
    {
        presentationRunning = false;
        TryProcessPendingQueue();
        bossUgManager?.NotifyExternalQueueTick();
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

        if (bossUgManager != null && (bossUgManager.IsPresentationRunning || bossUgManager.IsBossUgPanelOpen))
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

    private bool TryStartNextPresentation()
    {
        if (presentationRunning || presentationController == null)
        {
            return false;
        }

        if (Game03RunSessionState.IsRunEnded)
        {
            return false;
        }

        if (levelUpManager != null && levelUpManager.IsLevelUpPanelOpen)
        {
            return false;
        }

        if (bossUgManager != null && (bossUgManager.IsPresentationRunning || bossUgManager.IsBossUgPanelOpen))
        {
            return false;
        }

        if (!TryDrawWinner(out Game03UnitUgType winner, out UnitUgDefinition definition))
        {
            Debug.LogWarning("[Game03UnitUg] 抽選候補がすべて獲得済みのため UnitUG を開始しません。", this);
            return false;
        }

        var buzzSprites = new List<Sprite>(16);
        CollectBuzzSpritePool(winner, buzzSprites);
        acquiredTypes.Add(winner);
        presentationRunning = true;
        presentationController.BeginPresentation(definition, buzzSprites, OnPresentationFinished, this);
        return true;
    }

    private bool TryDrawWinner(out Game03UnitUgType winner, out UnitUgDefinition definition)
    {
        winner = default;
        definition = default;

        List<int> indices = new List<int>(definitions != null ? definitions.Length : 0);
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (!acquiredTypes.Contains(definitions[i].type))
                {
                    indices.Add(i);
                }
            }
        }

        if (indices.Count == 0)
        {
            return false;
        }

        int pick = indices[UnityEngine.Random.Range(0, indices.Count)];
        definition = definitions[pick];
        winner = definition.type;
        return true;
    }

    private List<Sprite> GetLoserSprites(Game03UnitUgType winner)
    {
        List<Sprite> losers = new List<Sprite>(16);
        if (definitions == null)
        {
            return losers;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            UnitUgDefinition def = definitions[i];
            if (def.type == winner)
            {
                continue;
            }

            if (acquiredTypes.Contains(def.type))
            {
                continue;
            }

            if (def.iconSprite != null)
            {
                losers.Add(def.iconSprite);
            }
        }

        return losers;
    }

    /// <summary>Buzz 用。ハズレ候補に加え、当選以外の定義スプライトも混ぜる（未設定時の見た目確保）。</summary>
    public void CollectBuzzSpritePool(Game03UnitUgType winner, List<Sprite> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        List<Sprite> losers = GetLoserSprites(winner);
        for (int i = 0; i < losers.Count; i++)
        {
            if (losers[i] != null)
            {
                destination.Add(losers[i]);
            }
        }

        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            UnitUgDefinition def = definitions[i];
            if (def.type == winner || def.iconSprite == null)
            {
                continue;
            }

            if (!destination.Contains(def.iconSprite))
            {
                destination.Add(def.iconSprite);
            }
        }
    }

    public bool TryGetDefinition(Game03UnitUgType type, out UnitUgDefinition definition)
    {
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].type == type)
                {
                    definition = definitions[i];
                    return true;
                }
            }
        }

        definition = default;
        return false;
    }

    public void ResetForNewRun()
    {
        acquiredTypes.Clear();
        pendingSources.Clear();
        presentationRunning = false;
        presentationController?.ForceCloseImmediate();
    }
}
