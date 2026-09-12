using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnlockTargetType
{
    Item = 0,
    Recipe = 1,
    Facility = 2,
    ResourceNode = 3,
    Feature = 4,
    Region = 5,
    Tier = 6
}

/// <summary>
/// 解放によって利用可能になる対象。
/// 表示名ではなく定義参照、安定識別子、またはTier番号で対象を指定する。
/// </summary>
[Serializable]
public struct UnlockTarget
{
    [SerializeField] private UnlockTargetType targetType;

    [Header("Game Data")]
    [SerializeField] private ItemDefinition item;
    [SerializeField] private RecipeDefinition recipe;
    [SerializeField] private FacilityDefinition facility;
    [SerializeField] private ResourceNodeDefinition resourceNode;

    [Header("Feature or Region")]
    [SerializeField] private string identifier;

    [Header("Tier")]
    [SerializeField, Min(1)] private int tier;

    public UnlockTargetType TargetType => targetType;
    public ItemDefinition Item => item;
    public RecipeDefinition Recipe => recipe;
    public FacilityDefinition Facility => facility;
    public ResourceNodeDefinition ResourceNode => resourceNode;
    public string Identifier => identifier;
    public int Tier => tier;

    public string GetStableKey()
    {
        switch (targetType)
        {
            case UnlockTargetType.Item:
                return item != null ? item.DefinitionId : string.Empty;
            case UnlockTargetType.Recipe:
                return recipe != null ? recipe.DefinitionId : string.Empty;
            case UnlockTargetType.Facility:
                return facility != null ? facility.DefinitionId : string.Empty;
            case UnlockTargetType.ResourceNode:
                return resourceNode != null ? resourceNode.DefinitionId : string.Empty;
            case UnlockTargetType.Feature:
                return $"feature.{identifier}";
            case UnlockTargetType.Region:
                return $"region.{identifier}";
            case UnlockTargetType.Tier:
                return $"tier.{tier}";
            default:
                return string.Empty;
        }
    }

    public void CollectValidationErrors(string ownerName, List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        switch (targetType)
        {
            case UnlockTargetType.Item:
                ValidateReference(item, "Item", ownerName, errors);
                break;
            case UnlockTargetType.Recipe:
                ValidateReference(recipe, "Recipe", ownerName, errors);
                break;
            case UnlockTargetType.Facility:
                ValidateReference(facility, "Facility", ownerName, errors);
                break;
            case UnlockTargetType.ResourceNode:
                ValidateReference(resourceNode, "Resource node", ownerName, errors);
                break;
            case UnlockTargetType.Feature:
            case UnlockTargetType.Region:
                if (!GameDataId.IsValidIdentifier(identifier))
                {
                    errors.Add($"{ownerName}: Target identifier '{identifier}' is invalid.");
                }
                break;
            case UnlockTargetType.Tier:
                if (tier <= 0)
                {
                    errors.Add($"{ownerName}: Target tier must be greater than zero.");
                }
                break;
            default:
                errors.Add($"{ownerName}: Target type '{targetType}' is invalid.");
                break;
        }
    }

    private static void ValidateReference(
        UnityEngine.Object reference,
        string referenceName,
        string ownerName,
        List<string> errors)
    {
        if (reference == null)
        {
            errors.Add($"{ownerName}: {referenceName} reference is missing.");
        }
    }
}
