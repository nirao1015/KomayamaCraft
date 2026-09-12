using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全条件を満たす必要があるAND条件グループ。
/// UnlockDefinitionは複数グループのいずれかを満たすORとして扱う。
/// </summary>
[Serializable]
public struct UnlockConditionGroup
{
    [SerializeField] private UnlockCondition[] allConditions;

    public IReadOnlyList<UnlockCondition> AllConditions =>
        allConditions ?? Array.Empty<UnlockCondition>();

    public void CollectValidationErrors(string ownerName, List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (allConditions == null || allConditions.Length == 0)
        {
            errors.Add($"{ownerName}: Condition group is empty.");
            return;
        }

        for (int i = 0; i < allConditions.Length; i++)
        {
            allConditions[i].CollectValidationErrors(
                $"{ownerName} condition {i}",
                errors);
        }
    }
}
