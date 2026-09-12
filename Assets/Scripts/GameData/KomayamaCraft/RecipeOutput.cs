using System;
using System.Collections.Generic;
using UnityEngine;

public enum RecipeOutputMode
{
    Fixed = 0,
    WeightedSingle = 1
}

/// <summary>
/// レシピの出力候補。Fixedでは全件、WeightedSingleでは重みに従い1件を使用する。
/// </summary>
[Serializable]
public struct RecipeOutput
{
    [SerializeField] private ItemDefinition item;
    [SerializeField] private int amount;
    [SerializeField] private int weight;

    public ItemDefinition Item => item;
    public int Amount => amount;
    public int Weight => weight;

    public RecipeOutput(ItemDefinition item, int amount, int weight = 1)
    {
        this.item = item;
        this.amount = amount;
        this.weight = weight;
    }

    public void CollectValidationErrors(
        string ownerName,
        RecipeOutputMode outputMode,
        List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (item == null)
        {
            errors.Add($"{ownerName}: Output item reference is missing.");
        }

        if (amount <= 0)
        {
            errors.Add($"{ownerName}: Output amount must be greater than zero.");
        }

        if (outputMode == RecipeOutputMode.WeightedSingle && weight <= 0)
        {
            errors.Add($"{ownerName}: Weighted output weight must be greater than zero.");
        }
    }
}
