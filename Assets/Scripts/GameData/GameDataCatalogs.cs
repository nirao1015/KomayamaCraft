using UnityEngine;

/// <summary>
/// ビルド同梱用 GameData カタログ（Resources/GameData/*.asset）の読み込み口。
/// </summary>
public static class GameDataCatalogs
{
    private const string ResourcesFolder = "GameData";

    private static DialogueScriptCatalog dialogue;
    private static Game03EnemySpawnPhaseCatalog game03EnemySpawn;
    private static Game01EnemySpawnCatalog game01EnemySpawn;
    private static ItemDefinitionCatalog komayamaItems;
    private static RecipeDefinitionCatalog komayamaRecipes;
    private static FacilityDefinitionCatalog komayamaFacilities;
    private static ResourceNodeDefinitionCatalog komayamaResourceNodes;
    private static UnlockDefinitionCatalog komayamaUnlocks;

    public static DialogueScriptCatalog Dialogue =>
        dialogue != null ? dialogue : (dialogue = Load<DialogueScriptCatalog>("DialogueScriptCatalog"));

    public static Game03EnemySpawnPhaseCatalog Game03EnemySpawn =>
        game03EnemySpawn != null
            ? game03EnemySpawn
            : (game03EnemySpawn = Load<Game03EnemySpawnPhaseCatalog>("Game03EnemySpawnPhaseCatalog"));

    public static Game01EnemySpawnCatalog Game01EnemySpawn =>
        game01EnemySpawn != null
            ? game01EnemySpawn
            : (game01EnemySpawn = Load<Game01EnemySpawnCatalog>("Game01EnemySpawnCatalog"));

    public static ItemDefinitionCatalog KomayamaItems =>
        komayamaItems != null
            ? komayamaItems
            : (komayamaItems = Load<ItemDefinitionCatalog>("KomayamaItemCatalog"));

    public static RecipeDefinitionCatalog KomayamaRecipes =>
        komayamaRecipes != null
            ? komayamaRecipes
            : (komayamaRecipes = Load<RecipeDefinitionCatalog>("KomayamaRecipeCatalog"));

    public static FacilityDefinitionCatalog KomayamaFacilities =>
        komayamaFacilities != null
            ? komayamaFacilities
            : (komayamaFacilities =
                Load<FacilityDefinitionCatalog>("KomayamaFacilityCatalog"));

    public static ResourceNodeDefinitionCatalog KomayamaResourceNodes =>
        komayamaResourceNodes != null
            ? komayamaResourceNodes
            : (komayamaResourceNodes =
                Load<ResourceNodeDefinitionCatalog>("KomayamaResourceNodeCatalog"));

    public static UnlockDefinitionCatalog KomayamaUnlocks =>
        komayamaUnlocks != null
            ? komayamaUnlocks
            : (komayamaUnlocks = Load<UnlockDefinitionCatalog>("KomayamaUnlockCatalog"));

    private static T Load<T>(string assetName) where T : Object
    {
        return Resources.Load<T>($"{ResourcesFolder}/{assetName}");
    }
}
