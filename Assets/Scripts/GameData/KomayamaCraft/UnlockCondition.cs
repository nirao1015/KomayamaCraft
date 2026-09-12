using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnlockConditionType
{
    PrerequisiteUnlockCompleted = 0,
    TierReached = 1,
    ItemDelivered = 2,
    FacilityBuilt = 3
}

/// <summary>
/// 解放判定に必要な1条件。
/// 所持数ではなく納品済み数量を扱い、報告済み状態や解放済み状態とも区別する。
/// </summary>
[Serializable]
public struct UnlockCondition
{
    [SerializeField] private UnlockConditionType conditionType;

    [Header("Prerequisite Unlock")]
    [SerializeField] private string requiredUnlockId;

    [Header("Tier")]
    [SerializeField, Min(1)] private int requiredTier;

    [Header("Delivered Item")]
    [SerializeField] private ItemDefinition deliveredItem;
    [SerializeField, Min(1)] private int deliveredAmount;

    [Header("Built Facility")]
    [SerializeField] private FacilityDefinition builtFacility;
    [SerializeField, Min(1)] private int builtFacilityCount;

    public UnlockConditionType ConditionType => conditionType;
    public string RequiredUnlockId => requiredUnlockId;
    public int RequiredTier => requiredTier;
    public ItemDefinition DeliveredItem => deliveredItem;
    public int DeliveredAmount => deliveredAmount;
    public FacilityDefinition BuiltFacility => builtFacility;
    public int BuiltFacilityCount => builtFacilityCount;

    public void CollectValidationErrors(string ownerName, List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        switch (conditionType)
        {
            case UnlockConditionType.PrerequisiteUnlockCompleted:
                if (!GameDataId.IsValidDefinitionId(
                        requiredUnlockId,
                        GameDataId.UnlockPrefix))
                {
                    errors.Add(
                        $"{ownerName}: Required unlock ID '{requiredUnlockId}' is invalid.");
                }
                break;

            case UnlockConditionType.TierReached:
                if (requiredTier <= 0)
                {
                    errors.Add($"{ownerName}: Required tier must be greater than zero.");
                }
                break;

            case UnlockConditionType.ItemDelivered:
                if (deliveredItem == null)
                {
                    errors.Add($"{ownerName}: Delivered item reference is missing.");
                }

                if (deliveredAmount <= 0)
                {
                    errors.Add(
                        $"{ownerName}: Delivered item amount must be greater than zero.");
                }
                break;

            case UnlockConditionType.FacilityBuilt:
                if (builtFacility == null)
                {
                    errors.Add($"{ownerName}: Built facility reference is missing.");
                }

                if (builtFacilityCount <= 0)
                {
                    errors.Add(
                        $"{ownerName}: Built facility count must be greater than zero.");
                }
                break;

            default:
                errors.Add($"{ownerName}: Condition type '{conditionType}' is invalid.");
                break;
        }
    }
}
