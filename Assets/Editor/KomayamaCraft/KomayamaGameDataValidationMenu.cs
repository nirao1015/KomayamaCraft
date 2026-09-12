using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class KomayamaGameDataValidationMenu
{
    private const string MenuPath = "Tools/KomayamaCraft/Game Data/Validate All";
    private const string ResourcesGameDataFolder = "Assets/Resources/GameData";

    [MenuItem("KomayamaCraft/ゲームデータを検証", false, 30)]
    [MenuItem(MenuPath, false, 210)]
    public static void ValidateAllFromMenu()
    {
        RunValidation(true);
    }

    public static KomayamaGameDataValidationResult RunValidation(bool logSuccess)
    {
        KomayamaGameDataValidationResult result =
            KomayamaGameDataValidator.Validate(CreateInput());

        for (int i = 0; i < result.Issues.Count; i++)
        {
            KomayamaGameDataValidationIssue issue = result.Issues[i];
            string message = $"[KomayamaGameDataValidation] {issue.Message}";
            if (issue.Context != null)
            {
                Debug.LogError(message, issue.Context);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        if (!result.HasErrors && logSuccess)
        {
            Debug.Log("[KomayamaGameDataValidation] Validation succeeded with 0 errors.");
        }

        return result;
    }

    public static KomayamaGameDataValidationInput CreateInput()
    {
        return new KomayamaGameDataValidationInput
        {
            ItemCatalog = LoadCatalog<ItemDefinitionCatalog>("KomayamaItemCatalog"),
            RecipeCatalog =
                LoadCatalog<RecipeDefinitionCatalog>("KomayamaRecipeCatalog"),
            FacilityCatalog =
                LoadCatalog<FacilityDefinitionCatalog>("KomayamaFacilityCatalog"),
            ResourceNodeCatalog =
                LoadCatalog<ResourceNodeDefinitionCatalog>("KomayamaResourceNodeCatalog"),
            UnlockCatalog =
                LoadCatalog<UnlockDefinitionCatalog>("KomayamaUnlockCatalog"),
            ItemAssets = FindDefinitionAssets<ItemDefinition>(),
            RecipeAssets = FindDefinitionAssets<RecipeDefinition>(),
            FacilityAssets = FindDefinitionAssets<FacilityDefinition>(),
            ResourceNodeAssets = FindDefinitionAssets<ResourceNodeDefinition>(),
            UnlockAssets = FindDefinitionAssets<UnlockDefinition>()
        };
    }

    private static T LoadCatalog<T>(string assetName) where T : ScriptableObject
    {
        string path = $"{ResourcesGameDataFolder}/{assetName}.asset";
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static T[] FindDefinitionAssets<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        Array.Sort(guids, StringComparer.Ordinal);

        var assets = new List<T>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets.ToArray();
    }
}

public sealed class KomayamaGameDataBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        KomayamaGameDataValidationResult result =
            KomayamaGameDataValidationMenu.RunValidation(false);
        if (result.HasErrors)
        {
            throw new BuildFailedException(
                $"KomayamaCraft game data validation failed with " +
                $"{result.ErrorCount} error(s). See Console for details.");
        }
    }
}
