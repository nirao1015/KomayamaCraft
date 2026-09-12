using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KomayamaCraftで使用する資源発生点定義の明示参照カタログ。
/// </summary>
[CreateAssetMenu(
    fileName = "KomayamaResourceNodeCatalog",
    menuName = "KomayamaCraft/Game Data/Resource Node Catalog")]
public sealed class ResourceNodeDefinitionCatalog : ScriptableObject
{
    [SerializeField] private ResourceNodeDefinition[] resourceNodes =
        Array.Empty<ResourceNodeDefinition>();

    private Dictionary<string, ResourceNodeDefinition> resourceNodesById;

    public IReadOnlyList<ResourceNodeDefinition> ResourceNodes => resourceNodes;

    public bool TryGet(string definitionId, out ResourceNodeDefinition resourceNode)
    {
        resourceNode = null;
        if (string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        EnsureLookup();
        return resourceNodesById.TryGetValue(definitionId, out resourceNode);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < resourceNodes.Length; i++)
        {
            ResourceNodeDefinition resourceNode = resourceNodes[i];
            if (resourceNode == null)
            {
                errors.Add($"{name}: Resource node entry {i} is missing.");
                continue;
            }

            resourceNode.CollectValidationErrors(errors);
            if (!seenIds.Add(resourceNode.DefinitionId))
            {
                errors.Add(
                    $"{name}: Resource node ID '{resourceNode.DefinitionId}' is duplicated.");
            }
        }
    }

    private void EnsureLookup()
    {
        if (resourceNodesById != null)
        {
            return;
        }

        resourceNodesById =
            new Dictionary<string, ResourceNodeDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < resourceNodes.Length; i++)
        {
            ResourceNodeDefinition resourceNode = resourceNodes[i];
            if (resourceNode == null ||
                !GameDataId.IsValidDefinitionId(
                    resourceNode.DefinitionId,
                    GameDataId.ResourceNodePrefix) ||
                resourceNodesById.ContainsKey(resourceNode.DefinitionId))
            {
                continue;
            }

            resourceNodesById.Add(resourceNode.DefinitionId, resourceNode);
        }
    }

#if UNITY_EDITOR
    public void SetResourceNodesForEditor(ResourceNodeDefinition[] newResourceNodes)
    {
        resourceNodes = newResourceNodes ?? Array.Empty<ResourceNodeDefinition>();
        resourceNodesById = null;
    }
#endif
}
