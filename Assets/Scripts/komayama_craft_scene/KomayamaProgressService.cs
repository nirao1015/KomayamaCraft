using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaProgressService : MonoBehaviour
    {
        public static KomayamaProgressService Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public const string GatherHasteSkillId = "gather_haste";
        public const string Tier2RegionId = "tier2";

        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private KomayamaCraftInputController input;
        [SerializeField] private int experiencePerGather = 1;
        [SerializeField] private int experiencePerCraft = 2;
        [SerializeField] private int experiencePerLevel = 10;
        [SerializeField] private float gatherHasteInterval = 0.25f;

        private readonly HashSet<string> unlockedSkills = new();
        private readonly HashSet<string> unlockedBlueprints = new();
        private readonly HashSet<string> unlockedRegions = new();
        private readonly Dictionary<string, int> deliveredAmounts = new();
        private readonly HashSet<string> completedUnlocks = new();
        private readonly HashSet<string> craftedItemIds = new();
        private bool hasClearedEnding;
        private int experience;
        private int level = 1;
        private int skillPoints;
        private int currentTier = 1;

        public int Experience => experience;
        public int Level => level;
        public int SkillPoints => skillPoints;
        public int CurrentTier => currentTier;
        public bool HasGatherHaste => unlockedSkills.Contains(GatherHasteSkillId);
        public bool HasClearedEnding => hasClearedEnding;

        public void NotifyGathered()
        {
            AddExperience(experiencePerGather);
        }

        public void NotifyCrafted()
        {
            AddExperience(experiencePerCraft);
        }

        public void NotifyCrafted(ItemDefinition item)
        {
            NotifyCrafted();
            if (item == null || string.IsNullOrEmpty(item.DefinitionId))
            {
                return;
            }

            craftedItemIds.Add(item.DefinitionId);
        }

        public bool HasCrafted(ItemDefinition item)
        {
            return item != null && craftedItemIds.Contains(item.DefinitionId);
        }

        public void MarkEndingCleared()
        {
            hasClearedEnding = true;
        }

        public void RecordDelivery(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            deliveredAmounts.TryGetValue(item.DefinitionId, out int current);
            deliveredAmounts[item.DefinitionId] = current + amount;
            EvaluateUnlocks();
        }

        public bool TryUnlockGatherHaste(out string failureReason)
        {
            failureReason = null;
            if (unlockedSkills.Contains(GatherHasteSkillId))
            {
                failureReason = "取得済みです";
                return false;
            }

            if (skillPoints <= 0)
            {
                failureReason = "スキルポイントが足りません";
                return false;
            }

            skillPoints--;
            unlockedSkills.Add(GatherHasteSkillId);
            ApplySkills();
            hud?.ShowMessage("スキル: 採集間隔短縮");
            return true;
        }

        public bool IsRegionUnlocked(string regionId)
        {
            return string.IsNullOrEmpty(regionId) || unlockedRegions.Contains(regionId);
        }

        public bool CanBuild(FacilityDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(definition.RequiredUnlockId))
            {
                return true;
            }

            return completedUnlocks.Contains(definition.RequiredUnlockId) ||
                   unlockedBlueprints.Contains(definition.DefinitionId);
        }

        public void CaptureSave(KomayamaCraftSaveData data)
        {
            data.currentTier = currentTier;
            data.progress.experience = experience;
            data.progress.level = level;
            data.progress.skillPoints = skillPoints;
            data.progress.EnsureCollections();
            data.progress.unlockedSkillIds.Clear();
            data.progress.unlockedSkillIds.AddRange(unlockedSkills);
            data.progress.unlockedBlueprintIds.Clear();
            data.progress.unlockedBlueprintIds.AddRange(unlockedBlueprints);
            data.progress.unlockedRegionIds.Clear();
            data.progress.unlockedRegionIds.AddRange(unlockedRegions);
            data.progress.craftedItemIds.Clear();
            data.progress.craftedItemIds.AddRange(craftedItemIds);
            data.progress.hasClearedEnding = hasClearedEnding;
            data.unlockStates.Clear();
            foreach (string unlockId in completedUnlocks)
            {
                data.unlockStates.Add(new UnlockStateRecord(unlockId, true));
            }

            data.deliveryProgress.Clear();
            foreach (KeyValuePair<string, int> pair in deliveredAmounts)
            {
                var dto = new DeliveryProgressSaveDto
                {
                    progressId = pair.Key,
                    isDeliveryComplete = true
                };
                dto.EnsureCollections();
                dto.deliveredItems.Add(new DeliveredItemProgressSaveDto
                {
                    itemDefinitionId = pair.Key,
                    deliveredAmount = pair.Value
                });
                data.deliveryProgress.Add(dto);
            }
        }

        public void ApplySave(KomayamaCraftSaveData data)
        {
            currentTier = Mathf.Max(1, data.currentTier);
            experience = data.progress != null ? data.progress.experience : 0;
            level = data.progress != null ? Mathf.Max(1, data.progress.level) : 1;
            skillPoints = data.progress != null ? data.progress.skillPoints : 0;
            unlockedSkills.Clear();
            unlockedBlueprints.Clear();
            unlockedRegions.Clear();
            completedUnlocks.Clear();
            deliveredAmounts.Clear();
            craftedItemIds.Clear();
            hasClearedEnding = data.progress != null && data.progress.hasClearedEnding;
            if (data.progress != null)
            {
                Copy(data.progress.unlockedSkillIds, unlockedSkills);
                Copy(data.progress.unlockedBlueprintIds, unlockedBlueprints);
                Copy(data.progress.unlockedRegionIds, unlockedRegions);
                Copy(data.progress.craftedItemIds, craftedItemIds);
            }

            if (data.unlockStates != null)
            {
                for (int i = 0; i < data.unlockStates.Count; i++)
                {
                    if (data.unlockStates[i].isUnlocked)
                    {
                        completedUnlocks.Add(data.unlockStates[i].unlockId);
                    }
                }
            }

            if (data.deliveryProgress != null)
            {
                for (int i = 0; i < data.deliveryProgress.Count; i++)
                {
                    DeliveryProgressSaveDto dto = data.deliveryProgress[i];
                    if (dto?.deliveredItems == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < dto.deliveredItems.Count; j++)
                    {
                        deliveredAmounts[dto.deliveredItems[j].itemDefinitionId] =
                            dto.deliveredItems[j].deliveredAmount;
                    }
                }
            }

            ApplySkills();
            EvaluateUnlocks();
        }

        private void Start()
        {
            ApplySkills();
            EvaluateUnlocks();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                TryUnlockGatherHaste(out string reason);
                if (!string.IsNullOrEmpty(reason))
                {
                    hud?.ShowMessage(reason);
                }
            }
        }

        private void AddExperience(int amount)
        {
            experience += Mathf.Max(0, amount);
            while (experience >= experiencePerLevel)
            {
                experience -= experiencePerLevel;
                level++;
                skillPoints++;
                hud?.ShowMessage($"レベル{level} 技能+1");
            }
        }

        private void ApplySkills()
        {
            if (input != null && HasGatherHaste)
            {
                input.SetGatherHoldIntervalSeconds(gatherHasteInterval);
            }
        }

        private void EvaluateUnlocks()
        {
            UnlockDefinitionCatalog catalog = GameDataCatalogs.KomayamaUnlocks;
            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Unlocks.Count; i++)
            {
                UnlockDefinition unlock = catalog.Unlocks[i];
                if (unlock == null || completedUnlocks.Contains(unlock.DefinitionId))
                {
                    continue;
                }

                if (!IsSatisfied(unlock))
                {
                    continue;
                }

                completedUnlocks.Add(unlock.DefinitionId);
                ApplyTargets(unlock);
            }
        }

        private bool IsSatisfied(UnlockDefinition unlock)
        {
            if (unlock.HasNoConditions)
            {
                return true;
            }

            for (int i = 0; i < unlock.AnyOfConditionGroups.Count; i++)
            {
                if (IsGroupSatisfied(unlock.AnyOfConditionGroups[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGroupSatisfied(UnlockConditionGroup group)
        {
            IReadOnlyList<UnlockCondition> conditions = group.AllConditions;
            if (conditions == null || conditions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (!IsConditionSatisfied(conditions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsConditionSatisfied(UnlockCondition condition)
        {
            switch (condition.ConditionType)
            {
                case UnlockConditionType.PrerequisiteUnlockCompleted:
                    return completedUnlocks.Contains(condition.RequiredUnlockId);
                case UnlockConditionType.TierReached:
                    return currentTier >= condition.RequiredTier;
                case UnlockConditionType.ItemDelivered:
                    return condition.DeliveredItem != null &&
                           deliveredAmounts.TryGetValue(
                               condition.DeliveredItem.DefinitionId,
                               out int amount) &&
                           amount >= condition.DeliveredAmount;
                case UnlockConditionType.FacilityBuilt:
                    return CountFacilities(condition.BuiltFacility) >= condition.BuiltFacilityCount;
                default:
                    return false;
            }
        }

        private void ApplyTargets(UnlockDefinition unlock)
        {
            for (int i = 0; i < unlock.Targets.Count; i++)
            {
                UnlockTarget target = unlock.Targets[i];
                if (target.TargetType == UnlockTargetType.Tier)
                {
                    currentTier = Mathf.Max(currentTier, target.Tier);
                    hud?.ShowMessage($"Tier {currentTier} 解放");
                }
                else if (target.TargetType == UnlockTargetType.Region &&
                         !string.IsNullOrEmpty(target.Identifier))
                {
                    unlockedRegions.Add(target.Identifier);
                    hud?.ShowMessage($"地域解放: {target.Identifier}");
                }
                else if (target.TargetType == UnlockTargetType.Facility &&
                         target.Facility != null)
                {
                    unlockedBlueprints.Add(target.Facility.DefinitionId);
                }
            }
        }

        private static int CountFacilities(FacilityDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            int count = 0;
            KomayamaProcessingFacility[] processors = FindObjectsByType<KomayamaProcessingFacility>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < processors.Length; i++)
            {
                if (processors[i] != null && processors[i].Definition == definition)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Copy(List<string> source, HashSet<string> destination)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(source[i]))
                {
                    destination.Add(source[i]);
                }
            }
        }
    }
}
