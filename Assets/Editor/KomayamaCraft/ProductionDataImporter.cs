#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KomayamaCraft.Editor
{
    /// <summary>
    /// Source CSV から加工系 Item/Facility/Recipe を生成し、カタログを一新する。
    /// 原本 xlsx は読まない。
    /// </summary>
    public static class ProductionDataImporter
    {
        private const string MenuPath = "KomayamaCraft/Game Data/Import Production Source";
        private const string SourceFolder = "Assets/GameData/KomayamaCraft/Source";
        private const string ItemsFolder = "Assets/GameData/KomayamaCraft/Items";
        private const string FacilitiesFolder = "Assets/GameData/KomayamaCraft/Facilities";
        private const string RecipesFolder = "Assets/GameData/KomayamaCraft/Recipes";
        private const string ObsoleteFolder = "Assets/GameData/KomayamaCraft/_Obsolete";
        private const string ItemCatalogPath = "Assets/Resources/GameData/KomayamaItemCatalog.asset";
        private const string FacilityCatalogPath = "Assets/Resources/GameData/KomayamaFacilityCatalog.asset";
        private const string RecipeCatalogPath = "Assets/Resources/GameData/KomayamaRecipeCatalog.asset";

        [MenuItem(MenuPath)]
        public static void Import()
        {
            EnsureFolder(SourceFolder);
            EnsureFolder(ItemsFolder);
            EnsureFolder(FacilitiesFolder);
            EnsureFolder(RecipesFolder);

            var nameRows = ReadCsv(Path.Combine(SourceFolder, "names.csv"));
            var facilityRows = ReadCsv(Path.Combine(SourceFolder, "facilities.csv"));
            var recipeRows = ReadCsv(Path.Combine(SourceFolder, "recipes.csv"));
            var sorterRows = ReadCsv(Path.Combine(SourceFolder, "sorter.csv"));

            var itemsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            var keepItemIds = new HashSet<string>(StringComparer.Ordinal);
            var keepFacilityIds = new HashSet<string>(StringComparer.Ordinal);
            var keepRecipeIds = new HashSet<string>(StringComparer.Ordinal);

            // --- Items from names.csv (non-facility rows) ---
            foreach (var row in nameRows)
            {
                string category = Get(row, "category");
                if (string.Equals(category, "facility", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string itemId = Get(row, "item_id");
                string japanese = Get(row, "japanese");
                string itemCategory = Get(row, "item_category");
                if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith("item.", StringComparison.Ordinal))
                {
                    continue;
                }

                ItemDefinition item = LoadOrCreateItem(itemId);
                SerializedObject so = new SerializedObject(item);
                so.FindProperty("definitionId").stringValue = itemId;
                so.FindProperty("displayName").stringValue = japanese;
                so.FindProperty("category").enumValueIndex = ParseItemCategory(itemCategory);
                so.FindProperty("maxStack").intValue = 99;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                itemsById[itemId] = item;
                keepItemIds.Add(itemId);
            }

            ItemDefinition fuel = itemsById.ContainsKey("item.volt_jelly")
                ? itemsById["item.volt_jelly"]
                : null;

            // --- Facilities ---
            var facilitiesById = new Dictionary<string, FacilityDefinition>(StringComparer.Ordinal);
            foreach (var row in facilityRows)
            {
                string facilityId = Get(row, "facility_id");
                string displayName = Get(row, "display_name");
                if (string.IsNullOrEmpty(facilityId))
                {
                    continue;
                }

                FacilityDefinition facility = LoadOrCreateFacility(facilityId);
                SerializedObject so = new SerializedObject(facility);
                so.FindProperty("definitionId").stringValue = facilityId;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("capabilities").intValue = (int)FacilityCapability.Processing;
                so.FindProperty("footprintWidthBlocks").intValue = ParseInt(Get(row, "footprint_w"), 10);
                so.FindProperty("footprintHeightBlocks").intValue = ParseInt(Get(row, "footprint_h"), 8);
                so.FindProperty("inputCapacity").intValue = ParseInt(Get(row, "input_cap"), 8);
                so.FindProperty("outputCapacity").intValue = ParseInt(Get(row, "output_cap"), 8);
                so.FindProperty("storageCapacity").intValue = 0;
                so.FindProperty("usesFuel").boolValue = true;
                so.FindProperty("fuelCapacity").intValue = ParseInt(Get(row, "fuel_cap"), 8);
                so.FindProperty("requiredUnlockId").stringValue = string.Empty;

                GameObject prefab = FindPrefabByHint(Get(row, "prefab_hint"));
                so.FindProperty("prefab").objectReferenceValue = prefab;

                string costItemId = Get(row, "construction_item_id");
                int costAmount = ParseInt(Get(row, "construction_amount"), 1);
                ItemDefinition costItem = ResolveItem(itemsById, costItemId);
                SerializedProperty costProp = so.FindProperty("constructionCost");
                costProp.arraySize = costItem != null ? 1 : 0;
                if (costItem != null)
                {
                    SerializedProperty e = costProp.GetArrayElementAtIndex(0);
                    e.FindPropertyRelative("item").objectReferenceValue = costItem;
                    e.FindPropertyRelative("amount").intValue = Mathf.Max(1, costAmount);
                }

                SerializedProperty fuels = so.FindProperty("acceptedFuelItems");
                fuels.arraySize = fuel != null ? 1 : 0;
                if (fuel != null)
                {
                    fuels.GetArrayElementAtIndex(0).objectReferenceValue = fuel;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(facility);
                facilitiesById[facilityId] = facility;
                keepFacilityIds.Add(facilityId);
            }

            // --- Recipes ---
            var recipesById = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            var recipesByFacility = new Dictionary<string, List<RecipeDefinition>>(StringComparer.Ordinal);

            foreach (var row in recipeRows)
            {
                string recipeId = Get(row, "recipe_id");
                string facilityId = Get(row, "facility_id");
                if (string.IsNullOrEmpty(recipeId) || string.IsNullOrEmpty(facilityId))
                {
                    continue;
                }

                RecipeDefinition recipe = LoadOrCreateRecipe(recipeId);
                SerializedObject so = new SerializedObject(recipe);
                so.FindProperty("definitionId").stringValue = recipeId;
                so.FindProperty("requiredFacilityId").stringValue = facilityId;
                so.FindProperty("requiredUnlockId").stringValue = Get(row, "required_unlock_id");
                so.FindProperty("processingSeconds").floatValue =
                    ParseFloat(Get(row, "processing_seconds"), 2f);
                so.FindProperty("usesFuel").boolValue = ParseInt(Get(row, "uses_fuel"), 1) != 0;

                // inputs
                SerializedProperty inputsProp = so.FindProperty("inputs");
                var inputPairs = ParseInputs(Get(row, "inputs"));
                inputsProp.arraySize = inputPairs.Count;
                for (int i = 0; i < inputPairs.Count; i++)
                {
                    ItemDefinition item = ResolveItem(itemsById, inputPairs[i].Item1);
                    SerializedProperty e = inputsProp.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("item").objectReferenceValue = item;
                    e.FindPropertyRelative("amount").intValue = inputPairs[i].Item2;
                }

                bool isSorter = string.Equals(recipeId, "recipe.heat_sac_sort", StringComparison.Ordinal);
                if (isSorter)
                {
                    so.FindProperty("displayName").stringValue = "熱嚢選別";
                    so.FindProperty("outputMode").enumValueIndex = (int)RecipeOutputMode.WeightedSingle;
                    var weights = new List<Tuple<string, int>>();
                    foreach (var srow in sorterRows)
                    {
                        if (!string.Equals(Get(srow, "recipe_id"), recipeId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        weights.Add(Tuple.Create(
                            Get(srow, "output_item_id"),
                            ParseInt(Get(srow, "weight"), 0)));
                    }

                    SerializedProperty outputsProp = so.FindProperty("outputs");
                    outputsProp.arraySize = weights.Count;
                    for (int i = 0; i < weights.Count; i++)
                    {
                        SerializedProperty e = outputsProp.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("item").objectReferenceValue =
                            ResolveItem(itemsById, weights[i].Item1);
                        e.FindPropertyRelative("amount").intValue = 1;
                        e.FindPropertyRelative("weight").intValue = weights[i].Item2;
                    }
                }
                else
                {
                    string outputId = Get(row, "output_item_id");
                    int outputAmount = ParseInt(Get(row, "output_amount"), 1);
                    ItemDefinition outputItem = ResolveItem(itemsById, outputId);
                    so.FindProperty("displayName").stringValue =
                        outputItem != null ? outputItem.DisplayName : recipeId;
                    so.FindProperty("outputMode").enumValueIndex = (int)RecipeOutputMode.Fixed;
                    SerializedProperty outputsProp = so.FindProperty("outputs");
                    outputsProp.arraySize = 1;
                    SerializedProperty e = outputsProp.GetArrayElementAtIndex(0);
                    e.FindPropertyRelative("item").objectReferenceValue = outputItem;
                    e.FindPropertyRelative("amount").intValue = Mathf.Max(1, outputAmount);
                    e.FindPropertyRelative("weight").intValue = 1;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recipe);
                recipesById[recipeId] = recipe;
                keepRecipeIds.Add(recipeId);

                if (!recipesByFacility.TryGetValue(facilityId, out List<RecipeDefinition> list))
                {
                    list = new List<RecipeDefinition>();
                    recipesByFacility[facilityId] = list;
                }

                list.Add(recipe);
            }

            // Wire facility.supportedRecipes
            foreach (var pair in facilitiesById)
            {
                FacilityDefinition facility = pair.Value;
                SerializedObject so = new SerializedObject(facility);
                SerializedProperty recipesProp = so.FindProperty("supportedRecipes");
                if (!recipesByFacility.TryGetValue(pair.Key, out List<RecipeDefinition> list))
                {
                    list = new List<RecipeDefinition>();
                }

                recipesProp.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(facility);
            }

            // Catalogs: production items + keep non-production? Spec says 一新 for craft chain.
            // Include all generated production items. Also keep resource-node-only if needed —
            // resource nodes will be retargeted to new IDs.
            ItemDefinitionCatalog itemCatalog =
                LoadOrCreateCatalog<ItemDefinitionCatalog>(ItemCatalogPath);
            itemCatalog.SetItemsForEditor(ToArray(itemsById));
            EditorUtility.SetDirty(itemCatalog);

            FacilityDefinitionCatalog facilityCatalog =
                LoadOrCreateCatalog<FacilityDefinitionCatalog>(FacilityCatalogPath);
            // Keep PrototypeStorage / CourierGhost if present
            var facilityList = new List<FacilityDefinition>(facilitiesById.Values);
            AppendExtraFacilities(facilityList, keepFacilityIds);
            facilityCatalog.SetFacilitiesForEditor(facilityList.ToArray());
            EditorUtility.SetDirty(facilityCatalog);

            RecipeDefinitionCatalog recipeCatalog =
                LoadOrCreateCatalog<RecipeDefinitionCatalog>(RecipeCatalogPath);
            recipeCatalog.SetRecipesForEditor(ToArray(recipesById));
            EditorUtility.SetDirty(recipeCatalog);

            RetargetResourceNodes(itemsById);
            MoveObsoleteAssets(keepItemIds, keepFacilityIds, keepRecipeIds);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[ProductionDataImporter] items={itemsById.Count} facilities={facilitiesById.Count} recipes={recipesById.Count}");
        }

        private static void AppendExtraFacilities(
            List<FacilityDefinition> list,
            HashSet<string> keepFacilityIds)
        {
            string[] extras =
            {
                "Assets/GameData/KomayamaCraft/Facilities/PrototypeStorage.asset",
                "Assets/GameData/KomayamaCraft/Facilities/CourierGhost.asset"
            };
            for (int i = 0; i < extras.Length; i++)
            {
                FacilityDefinition f = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(extras[i]);
                if (f == null)
                {
                    continue;
                }

                list.Add(f);
                keepFacilityIds.Add(f.DefinitionId);
            }
        }

        private static void RetargetResourceNodes(Dictionary<string, ItemDefinition> itemsById)
        {
            // old yield item path/id -> new item id
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "item.iron_scale", "item.iron_scale" },
                { "item.heat_sac_herb", "item.heat_sac" },
                { "item.magnetic_horn", "item.flux_horn" },
                { "item.star_crest_crystal", "item.star_crystal" },
                { "item.phase_shard", "item.phase_shard" },
                { "item.orbit_silk", "item.orbit_silk" },
                { "item.charge_jellyfish", "item.volt_jelly" }
            };

            string[] guids = AssetDatabase.FindAssets("t:ResourceNodeDefinition", new[]
            {
                "Assets/GameData/KomayamaCraft/ResourceNodes"
            });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ResourceNodeDefinition node =
                    AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
                if (node == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(node);
                SerializedProperty yields = so.FindProperty("yields");
                if (yields == null)
                {
                    continue;
                }

                bool dirty = false;
                for (int y = 0; y < yields.arraySize; y++)
                {
                    SerializedProperty itemProp =
                        yields.GetArrayElementAtIndex(y).FindPropertyRelative("item");
                    ItemDefinition oldItem = itemProp.objectReferenceValue as ItemDefinition;
                    if (oldItem == null)
                    {
                        continue;
                    }

                    string oldId = oldItem.DefinitionId;
                    if (!map.TryGetValue(oldId, out string newId))
                    {
                        continue;
                    }

                    if (!itemsById.TryGetValue(newId, out ItemDefinition newItem) || newItem == oldItem)
                    {
                        continue;
                    }

                    itemProp.objectReferenceValue = newItem;
                    dirty = true;
                }

                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(node);
                }
            }
        }

        private static void MoveObsoleteAssets(
            HashSet<string> keepItems,
            HashSet<string> keepFacilities,
            HashSet<string> keepRecipes)
        {
            EnsureFolder(ObsoleteFolder);
            MoveObsoleteInFolder(ItemsFolder, keepItems, typeof(ItemDefinition));
            MoveObsoleteInFolder(FacilitiesFolder, keepFacilities, typeof(FacilityDefinition));
            MoveObsoleteInFolder(RecipesFolder, keepRecipes, typeof(RecipeDefinition));
        }

        private static void MoveObsoleteInFolder(string folder, HashSet<string> keepIds, Type type)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + type.Name, new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/_Obsolete/") || path.Contains("/Source/"))
                {
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset == null)
                {
                    continue;
                }

                string id = ReadDefinitionId(asset);
                if (string.IsNullOrEmpty(id) || keepIds.Contains(id))
                {
                    continue;
                }

                // Keep PrototypeStorage / CourierGhost already in keepFacilities
                string fileName = Path.GetFileName(path);
                string dest = ObsoleteFolder + "/" + fileName;
                dest = AssetDatabase.GenerateUniqueAssetPath(dest);
                AssetDatabase.MoveAsset(path, dest);
            }
        }

        private static string ReadDefinitionId(UnityEngine.Object asset)
        {
            SerializedObject so = new SerializedObject(asset);
            SerializedProperty p = so.FindProperty("definitionId");
            return p != null ? p.stringValue : string.Empty;
        }

        private static ItemDefinition LoadOrCreateItem(string itemId)
        {
            string fileName = ToAssetFileName(itemId, "item.");
            string path = ItemsFolder + "/" + fileName + ".asset";
            ItemDefinition existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            // Prefer updating existing asset with same definitionId if found elsewhere
            ItemDefinition byId = FindByDefinitionId<ItemDefinition>(itemId);
            if (byId != null)
            {
                return byId;
            }

            ItemDefinition created = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static FacilityDefinition LoadOrCreateFacility(string facilityId)
        {
            string fileName = ToAssetFileName(facilityId, "facility.");
            string path = FacilitiesFolder + "/" + fileName + ".asset";
            FacilityDefinition existing = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            FacilityDefinition byId = FindByDefinitionId<FacilityDefinition>(facilityId);
            if (byId != null)
            {
                return byId;
            }

            FacilityDefinition created = ScriptableObject.CreateInstance<FacilityDefinition>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static RecipeDefinition LoadOrCreateRecipe(string recipeId)
        {
            string fileName = ToAssetFileName(recipeId, "recipe.");
            string path = RecipesFolder + "/" + fileName + ".asset";
            RecipeDefinition existing = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            RecipeDefinition byId = FindByDefinitionId<RecipeDefinition>(recipeId);
            if (byId != null)
            {
                return byId;
            }

            RecipeDefinition created = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static T FindByDefinitionId<T>(string definitionId) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(asset);
                SerializedProperty p = so.FindProperty("definitionId");
                if (p != null && string.Equals(p.stringValue, definitionId, StringComparison.Ordinal))
                {
                    return asset;
                }
            }

            return null;
        }

        private static GameObject FindPrefabByHint(string hint)
        {
            if (string.IsNullOrEmpty(hint))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets(hint + " t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/" + hint + ".prefab", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(path)
                        .Equals(hint, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static ItemDefinition ResolveItem(
            Dictionary<string, ItemDefinition> map,
            string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            if (map.TryGetValue(itemId, out ItemDefinition item))
            {
                return item;
            }

            return FindByDefinitionId<ItemDefinition>(itemId);
        }

        private static List<Tuple<string, int>> ParseInputs(string raw)
        {
            var list = new List<Tuple<string, int>>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return list;
            }

            string[] parts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] kv = parts[i].Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }

                list.Add(Tuple.Create(kv[0].Trim(), ParseInt(kv[1].Trim(), 1)));
            }

            return list;
        }

        private static int ParseItemCategory(string raw)
        {
            if (Enum.TryParse(raw, true, out ItemCategory cat))
            {
                return (int)cat;
            }

            return (int)ItemCategory.IntermediateMaterial;
        }

        private static string ToAssetFileName(string definitionId, string prefix)
        {
            string slug = definitionId.StartsWith(prefix, StringComparison.Ordinal)
                ? definitionId.Substring(prefix.Length)
                : definitionId;
            string[] parts = slug.Split('_');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                {
                    continue;
                }

                sb.Append(char.ToUpperInvariant(parts[i][0]));
                if (parts[i].Length > 1)
                {
                    sb.Append(parts[i].Substring(1));
                }
            }

            return sb.ToString();
        }

        private static T[] ToArray<T>(Dictionary<string, T> map)
        {
            var list = new List<T>(map.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
            return list.ToArray();
        }

        private static T LoadOrCreateCatalog<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace("\\", "/"));
            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static List<Dictionary<string, string>> ReadCsv(string assetPath)
        {
            var rows = new List<Dictionary<string, string>>();
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            string content;
            if (text != null)
            {
                content = text.text;
            }
            else if (File.Exists(assetPath))
            {
                content = File.ReadAllText(assetPath, Encoding.UTF8);
            }
            else
            {
                Debug.LogError("[ProductionDataImporter] Missing CSV: " + assetPath);
                return rows;
            }

            string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0)
            {
                return rows;
            }

            string[] headers = SplitCsvLine(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] cols = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Length; c++)
                {
                    row[headers[c]] = c < cols.Length ? cols[c] : string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            return line.Split(',');
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out string v) ? v.Trim() : string.Empty;
        }

        private static int ParseInt(string raw, int fallback)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }

                cur = next;
            }
        }
    }
}
#endif
