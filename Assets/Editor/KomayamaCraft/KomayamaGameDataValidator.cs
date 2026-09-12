using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class KomayamaGameDataValidationIssue
{
    public KomayamaGameDataValidationIssue(string message, UnityEngine.Object context)
    {
        Message = message;
        Context = context;
    }

    public string Message { get; }
    public UnityEngine.Object Context { get; }
}

/// <summary>
/// AssetDatabaseへの依存を境界に閉じ込めるための検証入力。
/// テストではカタログと探索済み定義を直接渡せる。
/// </summary>
public sealed class KomayamaGameDataValidationInput
{
    public ItemDefinitionCatalog ItemCatalog { get; set; }
    public RecipeDefinitionCatalog RecipeCatalog { get; set; }
    public FacilityDefinitionCatalog FacilityCatalog { get; set; }
    public ResourceNodeDefinitionCatalog ResourceNodeCatalog { get; set; }
    public UnlockDefinitionCatalog UnlockCatalog { get; set; }

    public ItemDefinition[] ItemAssets { get; set; } = Array.Empty<ItemDefinition>();
    public RecipeDefinition[] RecipeAssets { get; set; } = Array.Empty<RecipeDefinition>();
    public FacilityDefinition[] FacilityAssets { get; set; } =
        Array.Empty<FacilityDefinition>();
    public ResourceNodeDefinition[] ResourceNodeAssets { get; set; } =
        Array.Empty<ResourceNodeDefinition>();
    public UnlockDefinition[] UnlockAssets { get; set; } =
        Array.Empty<UnlockDefinition>();
}

public sealed class KomayamaGameDataValidationResult
{
    private readonly List<KomayamaGameDataValidationIssue> issues =
        new List<KomayamaGameDataValidationIssue>();

    public IReadOnlyList<KomayamaGameDataValidationIssue> Issues => issues;
    public int ErrorCount => issues.Count;
    public bool HasErrors => issues.Count > 0;

    internal void Add(string message, UnityEngine.Object context)
    {
        issues.Add(new KomayamaGameDataValidationIssue(message, context));
    }
}

/// <summary>
/// KomayamaCraft定義のカタログ内検証と横断参照検証。
/// メニュー、ビルド前処理、テストから同じ入口を使用する。
/// </summary>
public static class KomayamaGameDataValidator
{
    public static KomayamaGameDataValidationResult Validate(
        KomayamaGameDataValidationInput input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var result = new KomayamaGameDataValidationResult();

        ValidateCatalogs(input, result);

        HashSet<ItemDefinition> registeredItems =
            CreateRegisteredSet(input.ItemCatalog != null ? input.ItemCatalog.Items : null);
        HashSet<RecipeDefinition> registeredRecipes =
            CreateRegisteredSet(input.RecipeCatalog != null ? input.RecipeCatalog.Recipes : null);
        HashSet<FacilityDefinition> registeredFacilities =
            CreateRegisteredSet(
                input.FacilityCatalog != null ? input.FacilityCatalog.Facilities : null);
        HashSet<ResourceNodeDefinition> registeredResourceNodes =
            CreateRegisteredSet(
                input.ResourceNodeCatalog != null
                    ? input.ResourceNodeCatalog.ResourceNodes
                    : null);
        HashSet<UnlockDefinition> registeredUnlocks =
            CreateRegisteredSet(input.UnlockCatalog != null ? input.UnlockCatalog.Unlocks : null);

        ValidateUnregisteredAssets(input, registeredItems, registeredRecipes,
            registeredFacilities, registeredResourceNodes, registeredUnlocks, result);
        ValidateGlobalDefinitionIds(input, result);
        ValidateDefinitionReferences(input, registeredItems, registeredRecipes,
            registeredFacilities, registeredResourceNodes, registeredUnlocks, result);
        ValidateRequiredIds(input, registeredFacilities, registeredUnlocks, result);
        ValidateUnlockCycles(registeredUnlocks, result);

        return result;
    }

    private static void ValidateCatalogs(
        KomayamaGameDataValidationInput input,
        KomayamaGameDataValidationResult result)
    {
        UnityEngine.Object[] allDefinitions = CollectAllDefinitionObjects(input);

        ValidateCatalog(
            "KomayamaItemCatalog",
            input.ItemCatalog,
            input.ItemCatalog != null
                ? new Action<List<string>>(input.ItemCatalog.CollectValidationErrors)
                : null,
            allDefinitions,
            result);
        ValidateCatalog(
            "KomayamaRecipeCatalog",
            input.RecipeCatalog,
            input.RecipeCatalog != null
                ? new Action<List<string>>(input.RecipeCatalog.CollectValidationErrors)
                : null,
            allDefinitions,
            result);
        ValidateCatalog(
            "KomayamaFacilityCatalog",
            input.FacilityCatalog,
            input.FacilityCatalog != null
                ? new Action<List<string>>(input.FacilityCatalog.CollectValidationErrors)
                : null,
            allDefinitions,
            result);
        ValidateCatalog(
            "KomayamaResourceNodeCatalog",
            input.ResourceNodeCatalog,
            input.ResourceNodeCatalog != null
                ? new Action<List<string>>(input.ResourceNodeCatalog.CollectValidationErrors)
                : null,
            allDefinitions,
            result);
        ValidateCatalog(
            "KomayamaUnlockCatalog",
            input.UnlockCatalog,
            input.UnlockCatalog != null
                ? new Action<List<string>>(input.UnlockCatalog.CollectValidationErrors)
                : null,
            allDefinitions,
            result);
    }

    private static void ValidateCatalog(
        string expectedName,
        ScriptableObject catalog,
        Action<List<string>> collectErrors,
        UnityEngine.Object[] allDefinitions,
        KomayamaGameDataValidationResult result)
    {
        if (catalog == null)
        {
            result.Add($"{expectedName}: Required catalog asset is missing.", null);
            return;
        }

        var errors = new List<string>();
        collectErrors(errors);
        for (int i = 0; i < errors.Count; i++)
        {
            result.Add(
                errors[i],
                ResolveDefinitionContext(errors[i], allDefinitions, catalog));
        }
    }

    private static UnityEngine.Object ResolveDefinitionContext(
        string message,
        UnityEngine.Object[] allDefinitions,
        UnityEngine.Object fallback)
    {
        for (int i = 0; i < allDefinitions.Length; i++)
        {
            UnityEngine.Object definition = allDefinitions[i];
            if (definition != null &&
                message.StartsWith(definition.name + ":", StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return fallback;
    }

    private static HashSet<T> CreateRegisteredSet<T>(IReadOnlyList<T> definitions)
        where T : UnityEngine.Object
    {
        var set = new HashSet<T>();
        if (definitions == null)
        {
            return set;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null)
            {
                set.Add(definitions[i]);
            }
        }

        return set;
    }

    private static void ValidateUnregisteredAssets(
        KomayamaGameDataValidationInput input,
        HashSet<ItemDefinition> registeredItems,
        HashSet<RecipeDefinition> registeredRecipes,
        HashSet<FacilityDefinition> registeredFacilities,
        HashSet<ResourceNodeDefinition> registeredResourceNodes,
        HashSet<UnlockDefinition> registeredUnlocks,
        KomayamaGameDataValidationResult result)
    {
        ValidateUnregistered(
            input.ItemAssets,
            registeredItems,
            "item",
            result);
        ValidateUnregistered(
            input.RecipeAssets,
            registeredRecipes,
            "recipe",
            result);
        ValidateUnregistered(
            input.FacilityAssets,
            registeredFacilities,
            "facility",
            result);
        ValidateUnregistered(
            input.ResourceNodeAssets,
            registeredResourceNodes,
            "resource node",
            result);
        ValidateUnregistered(
            input.UnlockAssets,
            registeredUnlocks,
            "unlock",
            result);
    }

    private static void ValidateUnregistered<T>(
        T[] assets,
        HashSet<T> registered,
        string definitionType,
        KomayamaGameDataValidationResult result)
        where T : UnityEngine.Object
    {
        T[] safeAssets = assets ?? Array.Empty<T>();
        for (int i = 0; i < safeAssets.Length; i++)
        {
            T asset = safeAssets[i];
            if (asset != null && !registered.Contains(asset))
            {
                result.Add(
                    $"{asset.name}: {definitionType} definition asset is not registered " +
                    "in its KomayamaCraft catalog.",
                    asset);
            }
        }
    }

    private static void ValidateGlobalDefinitionIds(
        KomayamaGameDataValidationInput input,
        KomayamaGameDataValidationResult result)
    {
        var ownersById = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        AddDefinitionIds(input.ItemAssets, ownersById, result);
        AddDefinitionIds(input.RecipeAssets, ownersById, result);
        AddDefinitionIds(input.FacilityAssets, ownersById, result);
        AddDefinitionIds(input.ResourceNodeAssets, ownersById, result);
        AddDefinitionIds(input.UnlockAssets, ownersById, result);
    }

    private static void AddDefinitionIds(
        ItemDefinition[] definitions,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            AddDefinitionId(
                definitions[i] != null ? definitions[i].DefinitionId : null,
                definitions[i],
                ownersById,
                result);
        }
    }

    private static void AddDefinitionIds(
        RecipeDefinition[] definitions,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            AddDefinitionId(
                definitions[i] != null ? definitions[i].DefinitionId : null,
                definitions[i],
                ownersById,
                result);
        }
    }

    private static void AddDefinitionIds(
        FacilityDefinition[] definitions,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            AddDefinitionId(
                definitions[i] != null ? definitions[i].DefinitionId : null,
                definitions[i],
                ownersById,
                result);
        }
    }

    private static void AddDefinitionIds(
        ResourceNodeDefinition[] definitions,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            AddDefinitionId(
                definitions[i] != null ? definitions[i].DefinitionId : null,
                definitions[i],
                ownersById,
                result);
        }
    }

    private static void AddDefinitionIds(
        UnlockDefinition[] definitions,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            AddDefinitionId(
                definitions[i] != null ? definitions[i].DefinitionId : null,
                definitions[i],
                ownersById,
                result);
        }
    }

    private static void AddDefinitionId(
        string definitionId,
        UnityEngine.Object owner,
        Dictionary<string, UnityEngine.Object> ownersById,
        KomayamaGameDataValidationResult result)
    {
        if (owner == null || string.IsNullOrEmpty(definitionId))
        {
            return;
        }

        if (ownersById.TryGetValue(definitionId, out UnityEngine.Object existing) &&
            existing != owner)
        {
            result.Add(
                $"{owner.name}: Definition ID '{definitionId}' is also used by " +
                $"'{existing.name}'.",
                owner);
            return;
        }

        ownersById[definitionId] = owner;
    }

    private static void ValidateDefinitionReferences(
        KomayamaGameDataValidationInput input,
        HashSet<ItemDefinition> registeredItems,
        HashSet<RecipeDefinition> registeredRecipes,
        HashSet<FacilityDefinition> registeredFacilities,
        HashSet<ResourceNodeDefinition> registeredResourceNodes,
        HashSet<UnlockDefinition> registeredUnlocks,
        KomayamaGameDataValidationResult result)
    {
        ValidateRecipeReferences(input.RecipeAssets, registeredItems, result);
        ValidateFacilityReferences(
            input.FacilityAssets,
            registeredItems,
            registeredRecipes,
            result);
        ValidateResourceNodeReferences(
            input.ResourceNodeAssets,
            registeredItems,
            result);
        ValidateUnlockReferences(
            input.UnlockAssets,
            registeredItems,
            registeredRecipes,
            registeredFacilities,
            registeredResourceNodes,
            registeredUnlocks,
            result);
    }

    private static void ValidateRecipeReferences(
        RecipeDefinition[] recipes,
        HashSet<ItemDefinition> registeredItems,
        KomayamaGameDataValidationResult result)
    {
        if (recipes == null)
        {
            return;
        }

        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeDefinition recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            for (int inputIndex = 0; inputIndex < recipe.Inputs.Count; inputIndex++)
            {
                RequireRegistered(
                    recipe.Inputs[inputIndex].Item,
                    registeredItems,
                    recipe,
                    $"input item {inputIndex}",
                    result);
            }

            for (int outputIndex = 0; outputIndex < recipe.Outputs.Count; outputIndex++)
            {
                RequireRegistered(
                    recipe.Outputs[outputIndex].Item,
                    registeredItems,
                    recipe,
                    $"output item {outputIndex}",
                    result);
            }
        }
    }

    private static void ValidateFacilityReferences(
        FacilityDefinition[] facilities,
        HashSet<ItemDefinition> registeredItems,
        HashSet<RecipeDefinition> registeredRecipes,
        KomayamaGameDataValidationResult result)
    {
        if (facilities == null)
        {
            return;
        }

        for (int i = 0; i < facilities.Length; i++)
        {
            FacilityDefinition facility = facilities[i];
            if (facility == null)
            {
                continue;
            }

            for (int costIndex = 0; costIndex < facility.ConstructionCost.Count; costIndex++)
            {
                RequireRegistered(
                    facility.ConstructionCost[costIndex].Item,
                    registeredItems,
                    facility,
                    $"construction item {costIndex}",
                    result);
            }

            for (int recipeIndex = 0;
                 recipeIndex < facility.SupportedRecipes.Count;
                 recipeIndex++)
            {
                RequireRegistered(
                    facility.SupportedRecipes[recipeIndex],
                    registeredRecipes,
                    facility,
                    $"supported recipe {recipeIndex}",
                    result);
            }

            for (int fuelIndex = 0;
                 fuelIndex < facility.AcceptedFuelItems.Count;
                 fuelIndex++)
            {
                RequireRegistered(
                    facility.AcceptedFuelItems[fuelIndex],
                    registeredItems,
                    facility,
                    $"accepted fuel item {fuelIndex}",
                    result);
            }
        }
    }

    private static void ValidateResourceNodeReferences(
        ResourceNodeDefinition[] resourceNodes,
        HashSet<ItemDefinition> registeredItems,
        KomayamaGameDataValidationResult result)
    {
        if (resourceNodes == null)
        {
            return;
        }

        for (int i = 0; i < resourceNodes.Length; i++)
        {
            ResourceNodeDefinition resourceNode = resourceNodes[i];
            if (resourceNode == null)
            {
                continue;
            }

            for (int yieldIndex = 0; yieldIndex < resourceNode.Yields.Count; yieldIndex++)
            {
                RequireRegistered(
                    resourceNode.Yields[yieldIndex].Item,
                    registeredItems,
                    resourceNode,
                    $"yield item {yieldIndex}",
                    result);
            }
        }
    }

    private static void ValidateUnlockReferences(
        UnlockDefinition[] unlocks,
        HashSet<ItemDefinition> registeredItems,
        HashSet<RecipeDefinition> registeredRecipes,
        HashSet<FacilityDefinition> registeredFacilities,
        HashSet<ResourceNodeDefinition> registeredResourceNodes,
        HashSet<UnlockDefinition> registeredUnlocks,
        KomayamaGameDataValidationResult result)
    {
        if (unlocks == null)
        {
            return;
        }

        for (int i = 0; i < unlocks.Length; i++)
        {
            UnlockDefinition unlock = unlocks[i];
            if (unlock == null)
            {
                continue;
            }

            for (int groupIndex = 0;
                 groupIndex < unlock.AnyOfConditionGroups.Count;
                 groupIndex++)
            {
                IReadOnlyList<UnlockCondition> conditions =
                    unlock.AnyOfConditionGroups[groupIndex].AllConditions;
                for (int conditionIndex = 0;
                     conditionIndex < conditions.Count;
                     conditionIndex++)
                {
                    UnlockCondition condition = conditions[conditionIndex];
                    if (condition.ConditionType == UnlockConditionType.ItemDelivered)
                    {
                        RequireRegistered(
                            condition.DeliveredItem,
                            registeredItems,
                            unlock,
                            $"condition item {groupIndex}:{conditionIndex}",
                            result);
                    }
                    else if (condition.ConditionType == UnlockConditionType.FacilityBuilt)
                    {
                        RequireRegistered(
                            condition.BuiltFacility,
                            registeredFacilities,
                            unlock,
                            $"condition facility {groupIndex}:{conditionIndex}",
                            result);
                    }
                }
            }

            for (int targetIndex = 0; targetIndex < unlock.Targets.Count; targetIndex++)
            {
                UnlockTarget target = unlock.Targets[targetIndex];
                switch (target.TargetType)
                {
                    case UnlockTargetType.Item:
                        RequireRegistered(
                            target.Item,
                            registeredItems,
                            unlock,
                            $"target item {targetIndex}",
                            result);
                        break;
                    case UnlockTargetType.Recipe:
                        RequireRegistered(
                            target.Recipe,
                            registeredRecipes,
                            unlock,
                            $"target recipe {targetIndex}",
                            result);
                        break;
                    case UnlockTargetType.Facility:
                        RequireRegistered(
                            target.Facility,
                            registeredFacilities,
                            unlock,
                            $"target facility {targetIndex}",
                            result);
                        break;
                    case UnlockTargetType.ResourceNode:
                        RequireRegistered(
                            target.ResourceNode,
                            registeredResourceNodes,
                            unlock,
                            $"target resource node {targetIndex}",
                            result);
                        break;
                }
            }
        }
    }

    private static void RequireRegistered<T>(
        T reference,
        HashSet<T> registered,
        UnityEngine.Object owner,
        string fieldName,
        KomayamaGameDataValidationResult result)
        where T : UnityEngine.Object
    {
        if (reference != null && !registered.Contains(reference))
        {
            result.Add(
                $"{owner.name}: {fieldName} '{reference.name}' is not registered " +
                "in its KomayamaCraft catalog.",
                owner);
        }
    }

    private static void ValidateRequiredIds(
        KomayamaGameDataValidationInput input,
        HashSet<FacilityDefinition> registeredFacilities,
        HashSet<UnlockDefinition> registeredUnlocks,
        KomayamaGameDataValidationResult result)
    {
        var facilityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilityDefinition facility in registeredFacilities)
        {
            facilityIds.Add(facility.DefinitionId);
        }

        var unlockIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (UnlockDefinition unlock in registeredUnlocks)
        {
            unlockIds.Add(unlock.DefinitionId);
        }

        RecipeDefinition[] recipes = input.RecipeAssets ?? Array.Empty<RecipeDefinition>();
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeDefinition recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            RequireExistingId(
                recipe.RequiredFacilityId,
                facilityIds,
                "facility",
                recipe,
                false,
                result);
            RequireExistingId(
                recipe.RequiredUnlockId,
                unlockIds,
                "unlock",
                recipe,
                true,
                result);
        }

        FacilityDefinition[] facilities =
            input.FacilityAssets ?? Array.Empty<FacilityDefinition>();
        for (int i = 0; i < facilities.Length; i++)
        {
            if (facilities[i] != null)
            {
                RequireExistingId(
                    facilities[i].RequiredUnlockId,
                    unlockIds,
                    "unlock",
                    facilities[i],
                    true,
                    result);
            }
        }

        ResourceNodeDefinition[] resourceNodes =
            input.ResourceNodeAssets ?? Array.Empty<ResourceNodeDefinition>();
        for (int i = 0; i < resourceNodes.Length; i++)
        {
            if (resourceNodes[i] != null)
            {
                RequireExistingId(
                    resourceNodes[i].RequiredUnlockId,
                    unlockIds,
                    "unlock",
                    resourceNodes[i],
                    true,
                    result);
            }
        }

        UnlockDefinition[] unlocks =
            input.UnlockAssets ?? Array.Empty<UnlockDefinition>();
        for (int unlockIndex = 0; unlockIndex < unlocks.Length; unlockIndex++)
        {
            UnlockDefinition unlock = unlocks[unlockIndex];
            if (unlock == null)
            {
                continue;
            }

            for (int groupIndex = 0;
                 groupIndex < unlock.AnyOfConditionGroups.Count;
                 groupIndex++)
            {
                IReadOnlyList<UnlockCondition> conditions =
                    unlock.AnyOfConditionGroups[groupIndex].AllConditions;
                for (int conditionIndex = 0;
                     conditionIndex < conditions.Count;
                     conditionIndex++)
                {
                    UnlockCondition condition = conditions[conditionIndex];
                    if (condition.ConditionType ==
                        UnlockConditionType.PrerequisiteUnlockCompleted)
                    {
                        RequireExistingId(
                            condition.RequiredUnlockId,
                            unlockIds,
                            "prerequisite unlock",
                            unlock,
                            false,
                            result);
                    }
                }
            }
        }
    }

    private static void RequireExistingId(
        string definitionId,
        HashSet<string> registeredIds,
        string referenceType,
        UnityEngine.Object owner,
        bool allowEmpty,
        KomayamaGameDataValidationResult result)
    {
        if (allowEmpty && string.IsNullOrEmpty(definitionId))
        {
            return;
        }

        if (!string.IsNullOrEmpty(definitionId) && registeredIds.Contains(definitionId))
        {
            return;
        }

        result.Add(
            $"{owner.name}: Required {referenceType} ID '{definitionId}' is not registered.",
            owner);
    }

    private static void ValidateUnlockCycles(
        HashSet<UnlockDefinition> registeredUnlocks,
        KomayamaGameDataValidationResult result)
    {
        var unlocksById =
            new Dictionary<string, UnlockDefinition>(StringComparer.Ordinal);
        foreach (UnlockDefinition unlock in registeredUnlocks)
        {
            if (!string.IsNullOrEmpty(unlock.DefinitionId) &&
                !unlocksById.ContainsKey(unlock.DefinitionId))
            {
                unlocksById.Add(unlock.DefinitionId, unlock);
            }
        }

        var visitState = new Dictionary<UnlockDefinition, int>();
        var stack = new List<UnlockDefinition>();
        var reportedCycles = new HashSet<string>(StringComparer.Ordinal);

        foreach (UnlockDefinition unlock in registeredUnlocks)
        {
            if (!visitState.ContainsKey(unlock))
            {
                VisitUnlock(
                    unlock,
                    unlocksById,
                    visitState,
                    stack,
                    reportedCycles,
                    result);
            }
        }
    }

    private static void VisitUnlock(
        UnlockDefinition unlock,
        Dictionary<string, UnlockDefinition> unlocksById,
        Dictionary<UnlockDefinition, int> visitState,
        List<UnlockDefinition> stack,
        HashSet<string> reportedCycles,
        KomayamaGameDataValidationResult result)
    {
        visitState[unlock] = 1;
        stack.Add(unlock);

        foreach (string prerequisiteId in EnumeratePrerequisiteIds(unlock))
        {
            if (!unlocksById.TryGetValue(
                    prerequisiteId,
                    out UnlockDefinition prerequisite))
            {
                continue;
            }

            visitState.TryGetValue(prerequisite, out int prerequisiteState);
            if (prerequisiteState == 0)
            {
                VisitUnlock(
                    prerequisite,
                    unlocksById,
                    visitState,
                    stack,
                    reportedCycles,
                    result);
            }
            else if (prerequisiteState == 1)
            {
                ReportCycle(prerequisite, unlock, stack, reportedCycles, result);
            }
        }

        stack.RemoveAt(stack.Count - 1);
        visitState[unlock] = 2;
    }

    private static IEnumerable<string> EnumeratePrerequisiteIds(UnlockDefinition unlock)
    {
        for (int groupIndex = 0;
             groupIndex < unlock.AnyOfConditionGroups.Count;
             groupIndex++)
        {
            IReadOnlyList<UnlockCondition> conditions =
                unlock.AnyOfConditionGroups[groupIndex].AllConditions;
            for (int conditionIndex = 0;
                 conditionIndex < conditions.Count;
                 conditionIndex++)
            {
                UnlockCondition condition = conditions[conditionIndex];
                if (condition.ConditionType ==
                    UnlockConditionType.PrerequisiteUnlockCompleted)
                {
                    yield return condition.RequiredUnlockId;
                }
            }
        }
    }

    private static void ReportCycle(
        UnlockDefinition cycleStart,
        UnlockDefinition owner,
        List<UnlockDefinition> stack,
        HashSet<string> reportedCycles,
        KomayamaGameDataValidationResult result)
    {
        int startIndex = stack.IndexOf(cycleStart);
        if (startIndex < 0)
        {
            return;
        }

        var builder = new StringBuilder();
        for (int i = startIndex; i < stack.Count; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append(" -> ");
            }

            builder.Append(stack[i].DefinitionId);
        }

        builder.Append(" -> ");
        builder.Append(cycleStart.DefinitionId);
        string cycle = builder.ToString();
        if (reportedCycles.Add(cycle))
        {
            result.Add($"{owner.name}: Unlock prerequisite cycle detected: {cycle}.", owner);
        }
    }

    private static UnityEngine.Object[] CollectAllDefinitionObjects(
        KomayamaGameDataValidationInput input)
    {
        var definitions = new List<UnityEngine.Object>();
        AddObjects(input.ItemAssets, definitions);
        AddObjects(input.RecipeAssets, definitions);
        AddObjects(input.FacilityAssets, definitions);
        AddObjects(input.ResourceNodeAssets, definitions);
        AddObjects(input.UnlockAssets, definitions);
        return definitions.ToArray();
    }

    private static void AddObjects<T>(T[] source, List<UnityEngine.Object> destination)
        where T : UnityEngine.Object
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
            {
                destination.Add(source[i]);
            }
        }
    }
}
