using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum FacilityCapability
{
    None = 0,
    Processing = 1 << 0,
    Storage = 1 << 1,
    Automation = 1 << 2
}

/// <summary>
/// 配置可能な設備の能力と参照先を表す不変の定義データ。
/// 在庫、燃料残量、加工進捗などの実行時状態は保持しない。
/// </summary>
[CreateAssetMenu(
    fileName = "FacilityDefinition",
    menuName = "KomayamaCraft/Game Data/Facility Definition")]
public sealed class FacilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId = string.Empty;

    [Header("Display")]
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea(2, 5)] private string description = string.Empty;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject prefab;

    [Header("Capabilities")]
    [SerializeField] private FacilityCapability capabilities;
    [SerializeField] private RecipeDefinition[] supportedRecipes = Array.Empty<RecipeDefinition>();

    [Header("Construction")]
    [SerializeField] private ItemAmount[] constructionCost = Array.Empty<ItemAmount>();
    [SerializeField, Min(1), InspectorName("占有幅（ブロック）")]
    private int footprintWidthBlocks = 3;
    [SerializeField, Min(1), InspectorName("占有高さ（ブロック）")]
    private int footprintHeightBlocks = 2;

    [Header("Capacity")]
    [SerializeField, Min(0)] private int inputCapacity;
    [SerializeField, Min(0)] private int outputCapacity;
    [SerializeField, Min(0)] private int storageCapacity;

    [Header("Fuel")]
    [SerializeField] private bool usesFuel;
    [SerializeField] private ItemDefinition[] acceptedFuelItems = Array.Empty<ItemDefinition>();
    [SerializeField, Min(0)] private int fuelCapacity;

    [Header("Progression")]
    [SerializeField] private string requiredUnlockId = string.Empty;

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public FacilityCapability Capabilities => capabilities;
    public IReadOnlyList<RecipeDefinition> SupportedRecipes => supportedRecipes;
    public IReadOnlyList<ItemAmount> ConstructionCost => constructionCost;
    public int FootprintWidthBlocks => Mathf.Max(1, footprintWidthBlocks);
    public int FootprintHeightBlocks => Mathf.Max(1, footprintHeightBlocks);
    public int InputCapacity => inputCapacity;
    public int OutputCapacity => outputCapacity;
    public int StorageCapacity => storageCapacity;
    public bool UsesFuel => usesFuel;
    public IReadOnlyList<ItemDefinition> AcceptedFuelItems => acceptedFuelItems;
    public int FuelCapacity => fuelCapacity;
    public string RequiredUnlockId => requiredUnlockId;

    public bool HasCapability(FacilityCapability capability)
    {
        return capability != FacilityCapability.None &&
               (capabilities & capability) == capability;
    }

    public bool SupportsRecipe(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            return false;
        }

        for (int i = 0; i < supportedRecipes.Length; i++)
        {
            RecipeDefinition recipe = supportedRecipes[i];
            if (recipe != null &&
                string.Equals(recipe.DefinitionId, recipeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (!GameDataId.IsValidDefinitionId(definitionId, GameDataId.FacilityPrefix))
        {
            errors.Add($"{name}: Facility ID '{definitionId}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add($"{name}: Display name is empty.");
        }

        if (prefab == null)
        {
            errors.Add($"{name}: Prefab reference is missing.");
        }

        if (capabilities == FacilityCapability.None)
        {
            errors.Add($"{name}: Facility has no capabilities.");
        }

        ValidateConstructionCost(errors);
        ValidateRecipes(errors);
        ValidateCapacities(errors);
        ValidateFuel(errors);

        if (!string.IsNullOrEmpty(requiredUnlockId) &&
            !GameDataId.IsValidDefinitionId(requiredUnlockId, GameDataId.UnlockPrefix))
        {
            errors.Add($"{name}: Required unlock ID '{requiredUnlockId}' is invalid.");
        }
    }

    private void ValidateConstructionCost(List<string> errors)
    {
        if (constructionCost.Length == 0)
        {
            errors.Add($"{name}: Construction cost is empty.");
            return;
        }

        for (int i = 0; i < constructionCost.Length; i++)
        {
            constructionCost[i].CollectValidationErrors($"{name} construction cost {i}", errors);
        }
    }

    private void ValidateRecipes(List<string> errors)
    {
        bool supportsProcessing = HasCapability(FacilityCapability.Processing);
        if (supportsProcessing && supportedRecipes.Length == 0)
        {
            errors.Add($"{name}: Processing facility has no supported recipes.");
        }
        else if (!supportsProcessing && supportedRecipes.Length > 0)
        {
            errors.Add($"{name}: Non-processing facility has supported recipes.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < supportedRecipes.Length; i++)
        {
            RecipeDefinition recipe = supportedRecipes[i];
            if (recipe == null)
            {
                errors.Add($"{name}: Supported recipe entry {i} is missing.");
                continue;
            }

            if (!seenIds.Add(recipe.DefinitionId))
            {
                errors.Add($"{name}: Supported recipe ID '{recipe.DefinitionId}' is duplicated.");
            }

            if (!string.Equals(recipe.RequiredFacilityId, definitionId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{name}: Recipe '{recipe.DefinitionId}' requires facility " +
                    $"'{recipe.RequiredFacilityId}' instead of '{definitionId}'.");
            }

            if (recipe.UsesFuel && !usesFuel)
            {
                errors.Add(
                    $"{name}: Recipe '{recipe.DefinitionId}' uses fuel, " +
                    "but this facility does not accept fuel.");
            }
        }
    }

    private void ValidateCapacities(List<string> errors)
    {
        if (inputCapacity < 0 || outputCapacity < 0 || storageCapacity < 0)
        {
            errors.Add($"{name}: Capacities must not be negative.");
        }

        if (HasCapability(FacilityCapability.Processing) &&
            (inputCapacity <= 0 || outputCapacity <= 0))
        {
            errors.Add($"{name}: Processing facility must have positive input and output capacities.");
        }

        if (HasCapability(FacilityCapability.Storage) && storageCapacity <= 0)
        {
            errors.Add($"{name}: Storage facility must have a positive storage capacity.");
        }
    }

    private void ValidateFuel(List<string> errors)
    {
        if (!usesFuel)
        {
            if (acceptedFuelItems.Length > 0 || fuelCapacity != 0)
            {
                errors.Add($"{name}: Non-fuel facility has fuel settings.");
            }

            return;
        }

        if (fuelCapacity <= 0)
        {
            errors.Add($"{name}: Fuel capacity must be greater than zero.");
        }

        if (acceptedFuelItems.Length == 0)
        {
            errors.Add($"{name}: Fuel facility has no accepted fuel items.");
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < acceptedFuelItems.Length; i++)
        {
            ItemDefinition fuelItem = acceptedFuelItems[i];
            if (fuelItem == null)
            {
                errors.Add($"{name}: Accepted fuel item entry {i} is missing.");
                continue;
            }

            if (!seenIds.Add(fuelItem.DefinitionId))
            {
                errors.Add($"{name}: Accepted fuel item ID '{fuelItem.DefinitionId}' is duplicated.");
            }
        }
    }
}
