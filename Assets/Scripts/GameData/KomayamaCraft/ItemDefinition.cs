using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    RawMaterial = 0,
    IntermediateMaterial = 1,
    Component = 2,
    RepairPart = 3,
    Fuel = 4,
    Tool = 5,
    Quest = 6
}

/// <summary>
/// アイテムの種類を表す不変の定義データ。
/// 在庫数や加工状態などの実行時状態は保持しない。
/// </summary>
[CreateAssetMenu(
    fileName = "ItemDefinition",
    menuName = "KomayamaCraft/Game Data/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId = string.Empty;

    [Header("Display")]
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea(2, 5)] private string description = string.Empty;
    [SerializeField] private Sprite icon;

    [Header("Inventory")]
    [SerializeField] private ItemCategory category;
    [SerializeField] private int maxStack = 99;

    [Header("Traits")]
    [Tooltip("検索・用途判定用。英小文字、数字、アンダースコアのみ使用できます。")]
    [SerializeField] private string[] traitIds = Array.Empty<string>();

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public ItemCategory Category => category;
    public int MaxStack => maxStack;
    public IReadOnlyList<string> TraitIds => traitIds;

    public bool HasTrait(string traitId)
    {
        if (string.IsNullOrEmpty(traitId))
        {
            return false;
        }

        for (int i = 0; i < traitIds.Length; i++)
        {
            if (string.Equals(traitIds[i], traitId, StringComparison.Ordinal))
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

        if (!GameDataId.IsValidDefinitionId(definitionId, GameDataId.ItemPrefix))
        {
            errors.Add($"{name}: Item ID '{definitionId}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add($"{name}: Display name is empty.");
        }

        if (maxStack <= 0)
        {
            errors.Add($"{name}: Max stack must be greater than zero.");
        }

        var seenTraits = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < traitIds.Length; i++)
        {
            string traitId = traitIds[i];
            if (!GameDataId.IsValidIdentifier(traitId))
            {
                errors.Add($"{name}: Trait ID '{traitId}' is invalid.");
                continue;
            }

            if (!seenTraits.Add(traitId))
            {
                errors.Add($"{name}: Trait ID '{traitId}' is duplicated.");
            }
        }
    }
}
