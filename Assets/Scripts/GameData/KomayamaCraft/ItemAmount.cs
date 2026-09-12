using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アイテム定義と正の個数を組み合わせた共通データ。
/// </summary>
[Serializable]
public struct ItemAmount
{
    [SerializeField] private ItemDefinition item;
    [SerializeField] private int amount;

    public ItemDefinition Item => item;
    public int Amount => amount;

    public ItemAmount(ItemDefinition item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void CollectValidationErrors(string ownerName, List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (item == null)
        {
            errors.Add($"{ownerName}: Item reference is missing.");
        }

        if (amount <= 0)
        {
            errors.Add($"{ownerName}: Item amount must be greater than zero.");
        }
    }
}
