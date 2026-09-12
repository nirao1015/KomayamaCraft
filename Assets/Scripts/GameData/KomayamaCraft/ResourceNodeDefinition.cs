using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceNodeKind
{
    Biological = 0,
    Plant = 1,
    NonBiological = 2
}

public enum ResourceNodeReharvestMode
{
    Never = 0,
    AfterCooldown = 1,
    OnSceneReload = 2,
    Immediate = 3
}

/// <summary>
/// 採集対象の種類と産出条件を表す不変の定義データ。
/// 採集進捗や再採集待ち時間などの実行時状態は保持しない。
/// </summary>
[CreateAssetMenu(
    fileName = "ResourceNodeDefinition",
    menuName = "KomayamaCraft/Game Data/Resource Node Definition")]
public sealed class ResourceNodeDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId = string.Empty;

    [Header("Display")]
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea(2, 5)] private string description = string.Empty;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject prefab;

    [Header("Gathering")]
    [SerializeField] private ResourceNodeKind kind;
    [SerializeField] private ItemAmount[] yields = Array.Empty<ItemAmount>();
    [SerializeField, Min(0f)] private float gatheringSeconds;

    [Header("Reharvest")]
    [SerializeField] private ResourceNodeReharvestMode reharvestMode;
    [SerializeField, Min(0f)] private float reharvestSeconds;

    [Header("Progression")]
    [SerializeField] private string requiredUnlockId = string.Empty;

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public ResourceNodeKind Kind => kind;
    public IReadOnlyList<ItemAmount> Yields => yields;
    public float GatheringSeconds => gatheringSeconds;
    public ResourceNodeReharvestMode ReharvestMode => reharvestMode;
    public float ReharvestSeconds => reharvestSeconds;
    public string RequiredUnlockId => requiredUnlockId;

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (!GameDataId.IsValidDefinitionId(definitionId, GameDataId.ResourceNodePrefix))
        {
            errors.Add($"{name}: Resource node ID '{definitionId}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add($"{name}: Display name is empty.");
        }

        if (prefab == null)
        {
            errors.Add($"{name}: Prefab reference is missing.");
        }

        if (yields.Length == 0)
        {
            errors.Add($"{name}: Resource node has no yields.");
        }

        for (int i = 0; i < yields.Length; i++)
        {
            yields[i].CollectValidationErrors($"{name} yield {i}", errors);
        }

        if (gatheringSeconds < 0f)
        {
            errors.Add($"{name}: Gathering time cannot be negative.");
        }

        if (reharvestMode == ResourceNodeReharvestMode.AfterCooldown)
        {
            if (reharvestSeconds <= 0f)
            {
                errors.Add($"{name}: Reharvest cooldown must be greater than zero.");
            }
        }
        else if (reharvestSeconds != 0f)
        {
            errors.Add($"{name}: Reharvest cooldown is set for a mode that does not use it.");
        }

        if (!string.IsNullOrEmpty(requiredUnlockId) &&
            !GameDataId.IsValidDefinitionId(requiredUnlockId, GameDataId.UnlockPrefix))
        {
            errors.Add($"{name}: Required unlock ID '{requiredUnlockId}' is invalid.");
        }
    }
}
