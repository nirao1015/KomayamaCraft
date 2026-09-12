using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 解放フラグの条件と対象を表す不変の定義データ。
/// 条件グループなしは無条件、各グループ内はAND、グループ間はORとして扱う。
/// </summary>
[CreateAssetMenu(
    fileName = "UnlockDefinition",
    menuName = "KomayamaCraft/Game Data/Unlock Definition")]
public sealed class UnlockDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId = string.Empty;

    [Header("Display")]
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea(2, 5)] private string description = string.Empty;
    [SerializeField] private Sprite icon;

    [Header("Conditions (OR of AND groups)")]
    [Tooltip("空なら条件なし。各グループ内はAND、複数グループ間はORです。")]
    [SerializeField] private UnlockConditionGroup[] anyOfConditionGroups =
        Array.Empty<UnlockConditionGroup>();

    [Header("Targets")]
    [SerializeField] private UnlockTarget[] targets = Array.Empty<UnlockTarget>();

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<UnlockConditionGroup> AnyOfConditionGroups =>
        anyOfConditionGroups;
    public IReadOnlyList<UnlockTarget> Targets => targets;
    public bool HasNoConditions => anyOfConditionGroups.Length == 0;

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (!GameDataId.IsValidDefinitionId(definitionId, GameDataId.UnlockPrefix))
        {
            errors.Add($"{name}: Unlock ID '{definitionId}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add($"{name}: Display name is empty.");
        }

        for (int i = 0; i < anyOfConditionGroups.Length; i++)
        {
            anyOfConditionGroups[i].CollectValidationErrors(
                $"{name} condition group {i}",
                errors);
        }

        if (targets.Length == 0)
        {
            errors.Add($"{name}: Unlock has no targets.");
            return;
        }

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < targets.Length; i++)
        {
            UnlockTarget target = targets[i];
            target.CollectValidationErrors($"{name} target {i}", errors);

            string stableKey = target.GetStableKey();
            if (!string.IsNullOrEmpty(stableKey) && !seenTargets.Add(stableKey))
            {
                errors.Add($"{name}: Unlock target '{stableKey}' is duplicated.");
            }
        }
    }
}
