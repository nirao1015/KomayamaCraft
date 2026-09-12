using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 設備が行う材料変換を表す不変の定義データ。
/// 加工中の進捗や投入済み数量は保持しない。
/// </summary>
[CreateAssetMenu(
    fileName = "RecipeDefinition",
    menuName = "KomayamaCraft/Game Data/Recipe Definition")]
public sealed class RecipeDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId = string.Empty;
    [SerializeField] private string displayName = string.Empty;

    [Header("Requirements")]
    [SerializeField] private ItemAmount[] inputs = Array.Empty<ItemAmount>();
    [SerializeField] private string requiredFacilityId = string.Empty;
    [SerializeField] private string requiredUnlockId = string.Empty;
    [SerializeField, Min(0.01f)] private float processingSeconds = 1f;
    [SerializeField] private bool usesFuel;

    [Header("Outputs")]
    [SerializeField] private RecipeOutputMode outputMode;
    [SerializeField] private RecipeOutput[] outputs = Array.Empty<RecipeOutput>();

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public IReadOnlyList<ItemAmount> Inputs => inputs;
    public string RequiredFacilityId => requiredFacilityId;
    public string RequiredUnlockId => requiredUnlockId;
    public float ProcessingSeconds => processingSeconds;
    public bool UsesFuel => usesFuel;
    public RecipeOutputMode OutputMode => outputMode;
    public IReadOnlyList<RecipeOutput> Outputs => outputs;

    public bool TrySelectWeightedOutput(int roll, out RecipeOutput selected)
    {
        selected = default;
        if (outputMode != RecipeOutputMode.WeightedSingle)
        {
            return false;
        }

        int totalWeight = 0;
        for (int i = 0; i < outputs.Length; i++)
        {
            if (outputs[i].Weight > 0)
            {
                totalWeight += outputs[i].Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return false;
        }

        int normalizedRoll = roll % totalWeight;
        if (normalizedRoll < 0)
        {
            normalizedRoll += totalWeight;
        }

        int cursor = 0;
        for (int i = 0; i < outputs.Length; i++)
        {
            int weight = outputs[i].Weight;
            if (weight <= 0)
            {
                continue;
            }

            cursor += weight;
            if (normalizedRoll < cursor)
            {
                selected = outputs[i];
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

        if (!GameDataId.IsValidDefinitionId(definitionId, GameDataId.RecipePrefix))
        {
            errors.Add($"{name}: Recipe ID '{definitionId}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add($"{name}: Display name is empty.");
        }

        if (inputs.Length == 0)
        {
            errors.Add($"{name}: Recipe has no inputs.");
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i].CollectValidationErrors($"{name} input {i}", errors);
        }

        if (!GameDataId.IsValidDefinitionId(requiredFacilityId, GameDataId.FacilityPrefix))
        {
            errors.Add($"{name}: Required facility ID '{requiredFacilityId}' is invalid.");
        }

        if (!string.IsNullOrEmpty(requiredUnlockId) &&
            !GameDataId.IsValidDefinitionId(requiredUnlockId, GameDataId.UnlockPrefix))
        {
            errors.Add($"{name}: Required unlock ID '{requiredUnlockId}' is invalid.");
        }

        if (processingSeconds <= 0f)
        {
            errors.Add($"{name}: Processing time must be greater than zero.");
        }

        if (outputs.Length == 0)
        {
            errors.Add($"{name}: Recipe has no outputs.");
        }

        for (int i = 0; i < outputs.Length; i++)
        {
            outputs[i].CollectValidationErrors($"{name} output {i}", outputMode, errors);
        }
    }
}
