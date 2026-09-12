using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KomayamaCraftで使用する解放定義の明示参照カタログ。
/// </summary>
[CreateAssetMenu(
    fileName = "KomayamaUnlockCatalog",
    menuName = "KomayamaCraft/Game Data/Unlock Catalog")]
public sealed class UnlockDefinitionCatalog : ScriptableObject
{
    [SerializeField] private UnlockDefinition[] unlocks = Array.Empty<UnlockDefinition>();

    private Dictionary<string, UnlockDefinition> unlocksById;

    public IReadOnlyList<UnlockDefinition> Unlocks => unlocks;

    public bool TryGet(string definitionId, out UnlockDefinition unlock)
    {
        unlock = null;
        if (string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        EnsureLookup();
        return unlocksById.TryGetValue(definitionId, out unlock);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < unlocks.Length; i++)
        {
            UnlockDefinition unlock = unlocks[i];
            if (unlock == null)
            {
                errors.Add($"{name}: Unlock entry {i} is missing.");
                continue;
            }

            unlock.CollectValidationErrors(errors);
            if (!seenIds.Add(unlock.DefinitionId))
            {
                errors.Add($"{name}: Unlock ID '{unlock.DefinitionId}' is duplicated.");
            }
        }
    }

    private void EnsureLookup()
    {
        if (unlocksById != null)
        {
            return;
        }

        unlocksById = new Dictionary<string, UnlockDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < unlocks.Length; i++)
        {
            UnlockDefinition unlock = unlocks[i];
            if (unlock == null ||
                !GameDataId.IsValidDefinitionId(
                    unlock.DefinitionId,
                    GameDataId.UnlockPrefix) ||
                unlocksById.ContainsKey(unlock.DefinitionId))
            {
                continue;
            }

            unlocksById.Add(unlock.DefinitionId, unlock);
        }
    }

#if UNITY_EDITOR
    public void SetUnlocksForEditor(UnlockDefinition[] newUnlocks)
    {
        unlocks = newUnlocks ?? Array.Empty<UnlockDefinition>();
        unlocksById = null;
    }
#endif
}
