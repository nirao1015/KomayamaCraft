using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KomayamaCraftで使用するアイテム定義の明示参照カタログ。
/// </summary>
[CreateAssetMenu(
    fileName = "KomayamaItemCatalog",
    menuName = "KomayamaCraft/Game Data/Item Catalog")]
public sealed class ItemDefinitionCatalog : ScriptableObject
{
    [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

    private Dictionary<string, ItemDefinition> itemsById;

    public IReadOnlyList<ItemDefinition> Items => items;

    public bool TryGet(string definitionId, out ItemDefinition item)
    {
        item = null;
        if (string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        EnsureLookup();
        return itemsById.TryGetValue(definitionId, out item);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            ItemDefinition item = items[i];
            if (item == null)
            {
                errors.Add($"{name}: Item entry {i} is missing.");
                continue;
            }

            item.CollectValidationErrors(errors);
            if (!seenIds.Add(item.DefinitionId))
            {
                errors.Add($"{name}: Item ID '{item.DefinitionId}' is duplicated.");
            }
        }
    }

    private void EnsureLookup()
    {
        if (itemsById != null)
        {
            return;
        }

        itemsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            ItemDefinition item = items[i];
            if (item == null ||
                !GameDataId.IsValidDefinitionId(item.DefinitionId, GameDataId.ItemPrefix) ||
                itemsById.ContainsKey(item.DefinitionId))
            {
                continue;
            }

            itemsById.Add(item.DefinitionId, item);
        }
    }

#if UNITY_EDITOR
    public void SetItemsForEditor(ItemDefinition[] newItems)
    {
        items = newItems ?? Array.Empty<ItemDefinition>();
        itemsById = null;
    }
#endif
}
