using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KomayamaCraftで使用するレシピ定義の明示参照カタログ。
/// </summary>
[CreateAssetMenu(
    fileName = "KomayamaRecipeCatalog",
    menuName = "KomayamaCraft/Game Data/Recipe Catalog")]
public sealed class RecipeDefinitionCatalog : ScriptableObject
{
    [SerializeField] private RecipeDefinition[] recipes = Array.Empty<RecipeDefinition>();

    private Dictionary<string, RecipeDefinition> recipesById;

    public IReadOnlyList<RecipeDefinition> Recipes => recipes;

    public bool TryGet(string definitionId, out RecipeDefinition recipe)
    {
        recipe = null;
        if (string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        EnsureLookup();
        return recipesById.TryGetValue(definitionId, out recipe);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeDefinition recipe = recipes[i];
            if (recipe == null)
            {
                errors.Add($"{name}: Recipe entry {i} is missing.");
                continue;
            }

            recipe.CollectValidationErrors(errors);
            if (!seenIds.Add(recipe.DefinitionId))
            {
                errors.Add($"{name}: Recipe ID '{recipe.DefinitionId}' is duplicated.");
            }
        }
    }

    private void EnsureLookup()
    {
        if (recipesById != null)
        {
            return;
        }

        recipesById = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeDefinition recipe = recipes[i];
            if (recipe == null ||
                !GameDataId.IsValidDefinitionId(recipe.DefinitionId, GameDataId.RecipePrefix) ||
                recipesById.ContainsKey(recipe.DefinitionId))
            {
                continue;
            }

            recipesById.Add(recipe.DefinitionId, recipe);
        }
    }

#if UNITY_EDITOR
    public void SetRecipesForEditor(RecipeDefinition[] newRecipes)
    {
        recipes = newRecipes ?? Array.Empty<RecipeDefinition>();
        recipesById = null;
    }
#endif
}
