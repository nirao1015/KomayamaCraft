using System;
using System.Text.RegularExpressions;

/// <summary>
/// KomayamaCraft の永続IDに関する共通規則。
/// </summary>
public static class GameDataId
{
    public const string ItemPrefix = "item.";
    public const string RecipePrefix = "recipe.";
    public const string FacilityPrefix = "facility.";
    public const string ResourceNodePrefix = "resource_node.";
    public const string UnlockPrefix = "unlock.";

    private static readonly Regex DefinitionIdPattern = new Regex(
        @"^(item|recipe|facility|resource_node|unlock)\.[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IdentifierPattern = new Regex(
        @"^[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex InstanceIdPattern = new Regex(
        @"^[0-9a-f]{32}$",
        RegexOptions.CultureInvariant);

    public static bool IsValidDefinitionId(string definitionId)
    {
        return !string.IsNullOrEmpty(definitionId) &&
               DefinitionIdPattern.IsMatch(definitionId);
    }

    public static bool IsValidDefinitionId(string definitionId, string expectedPrefix)
    {
        return IsValidDefinitionId(definitionId) &&
               !string.IsNullOrEmpty(expectedPrefix) &&
               definitionId.StartsWith(expectedPrefix, StringComparison.Ordinal);
    }

    public static bool IsValidIdentifier(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) &&
               IdentifierPattern.IsMatch(identifier);
    }

    public static bool IsValidInstanceId(string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId) &&
               InstanceIdPattern.IsMatch(instanceId);
    }

    public static string CreateInstanceId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
