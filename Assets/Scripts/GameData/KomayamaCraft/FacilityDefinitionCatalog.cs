using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KomayamaCraftで使用する設備定義の明示参照カタログ。
/// </summary>
[CreateAssetMenu(
    fileName = "KomayamaFacilityCatalog",
    menuName = "KomayamaCraft/Game Data/Facility Catalog")]
public sealed class FacilityDefinitionCatalog : ScriptableObject
{
    [SerializeField] private FacilityDefinition[] facilities = Array.Empty<FacilityDefinition>();

    private Dictionary<string, FacilityDefinition> facilitiesById;

    public IReadOnlyList<FacilityDefinition> Facilities => facilities;

    public bool TryGet(string definitionId, out FacilityDefinition facility)
    {
        facility = null;
        if (string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        EnsureLookup();
        return facilitiesById.TryGetValue(definitionId, out facility);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < facilities.Length; i++)
        {
            FacilityDefinition facility = facilities[i];
            if (facility == null)
            {
                errors.Add($"{name}: Facility entry {i} is missing.");
                continue;
            }

            facility.CollectValidationErrors(errors);
            if (!seenIds.Add(facility.DefinitionId))
            {
                errors.Add($"{name}: Facility ID '{facility.DefinitionId}' is duplicated.");
            }
        }
    }

    private void EnsureLookup()
    {
        if (facilitiesById != null)
        {
            return;
        }

        facilitiesById = new Dictionary<string, FacilityDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < facilities.Length; i++)
        {
            FacilityDefinition facility = facilities[i];
            if (facility == null ||
                !GameDataId.IsValidDefinitionId(
                    facility.DefinitionId,
                    GameDataId.FacilityPrefix) ||
                facilitiesById.ContainsKey(facility.DefinitionId))
            {
                continue;
            }

            facilitiesById.Add(facility.DefinitionId, facility);
        }
    }

#if UNITY_EDITOR
    public void SetFacilitiesForEditor(FacilityDefinition[] newFacilities)
    {
        facilities = newFacilities ?? Array.Empty<FacilityDefinition>();
        facilitiesById = null;
    }
#endif
}
