#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// game03 の <see cref="Game03EnemyManager.enemyPrefabsByEnemyNumber"/> が
/// JSON enemyKind（Enemy00→0 …）と一致しているか検証・修正する。
/// </summary>
internal static class Game03EnemyPrefabSlotValidator
{
    private const string Game03ScenePath = "Assets/Scenes/game03_scene.unity";
    private const string PrefabFolder = "Assets/Prefabs/game03";
    /// <summary>Enemy00〜Enemy24（JSON enemyKind の番号＝配列添字）。</summary>
    private const int EnemySlotCount = 25;

    [MenuItem("Tools/Game03/Validate Enemy Prefab Slots")]
    private static void ValidateFromMenu()
    {
        RunValidate(logToConsole: true, attemptFixActiveScene: false);
    }

    [MenuItem("Tools/Game03/Fix Enemy Prefab Slots In game03_scene")]
    private static void FixGame03SceneFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(Game03ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[Game03EnemyPrefabSlotValidator] シーンを開けません: {Game03ScenePath}");
            return;
        }

        bool changed = TryFixScene(scene, out string report);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log(report);
    }

    [InitializeOnLoad]
    private static class SceneOpenHook
    {
        static SceneOpenHook()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!IsGame03Scene(scene))
            {
                return;
            }

            RunValidate(logToConsole: false, attemptFixActiveScene: false);
        }
    }

    private static bool IsGame03Scene(Scene scene)
    {
        return scene.IsValid() && scene.path.Replace('\\', '/').EndsWith("game03_scene.unity");
    }

    private static void RunValidate(bool logToConsole, bool attemptFixActiveScene)
    {
        Game03EnemyManager manager = Object.FindFirstObjectByType<Game03EnemyManager>();
        if (manager == null)
        {
            if (logToConsole)
            {
                Debug.LogWarning("[Game03EnemyPrefabSlotValidator] シーンに Game03EnemyManager がありません。");
            }

            return;
        }

        bool ok = ValidateManager(manager, out string report);
        if (attemptFixActiveScene && !ok)
        {
            ok = TryFixManager(manager, out report);
            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
        }

        if (logToConsole)
        {
            Debug.Log(report);
        }
        else if (!ok)
        {
            Debug.LogWarning(report);
        }
    }

    private static bool TryFixScene(Scene scene, out string report)
    {
        Game03EnemyManager manager = Object.FindFirstObjectByType<Game03EnemyManager>();
        if (manager == null)
        {
            report = "[Game03EnemyPrefabSlotValidator] Game03EnemyManager が見つかりません。";
            return false;
        }

        return TryFixManager(manager, out report);
    }

    private static bool TryFixManager(Game03EnemyManager manager, out string report)
    {
        var slots = new RectTransform[EnemySlotCount];
        bool anyMissing = false;
        for (int i = 0; i < EnemySlotCount; i++)
        {
            string path = $"{PrefabFolder}/Enemy{i:D2}ImagePrefab.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                anyMissing = true;
                continue;
            }

            slots[i] = prefab.GetComponent<RectTransform>();
        }

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty arrayProp = so.FindProperty("enemyPrefabsByEnemyNumber");
        if (arrayProp == null)
        {
            report = "[Game03EnemyPrefabSlotValidator] enemyPrefabsByEnemyNumber が見つかりません。";
            return false;
        }

        arrayProp.arraySize = EnemySlotCount;
        for (int i = 0; i < EnemySlotCount; i++)
        {
            arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        RectTransform enemy00 = slots[0];
        SerializedProperty templateProp = so.FindProperty("enemyTemplate");
        if (templateProp != null && enemy00 != null)
        {
            templateProp.objectReferenceValue = enemy00;
        }

        AssignBossPrefab(so, "bossMiddle01ImagePrefab", $"{PrefabFolder}/BossMiddle01ImagePrefab.prefab");
        AssignBossPrefab(so, "boss01ImagePrefab", $"{PrefabFolder}/Boss01ImagePrefab.prefab");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);

        bool ok = ValidateManager(manager, out report);
        if (anyMissing)
        {
            report += "\n（一部 EnemyXX プレハブが Assets に無いためスロットが空のままです）";
        }

        if (ok)
        {
            report = "[Game03EnemyPrefabSlotValidator] 修正完了。スロット 0〜24 = Enemy00〜24 に揃えました（存在するプレハブのみ）。\n" + report;
        }

        return ok;
    }

    private static void AssignBossPrefab(SerializedObject so, string propertyName, string assetPath)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        prop.objectReferenceValue = prefab != null ? prefab.GetComponent<RectTransform>() : null;
    }

    private static bool ValidateManager(Game03EnemyManager manager, out string report)
    {
        var sb = new StringBuilder(256);
        bool ok = true;
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty arrayProp = so.FindProperty("enemyPrefabsByEnemyNumber");
        if (arrayProp == null || !arrayProp.isArray)
        {
            report = "[Game03EnemyPrefabSlotValidator] enemyPrefabsByEnemyNumber が未設定です。";
            return false;
        }

        if (arrayProp.arraySize < EnemySlotCount)
        {
            ok = false;
            sb.AppendLine($"配列サイズが {arrayProp.arraySize} です（{EnemySlotCount} 要素必要: Enemy00〜24）。");
        }

        int count = Mathf.Min(arrayProp.arraySize, EnemySlotCount);
        for (int i = 0; i < count; i++)
        {
            Object refObj = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue;
            string expectedName = $"Enemy{i:D2}ImagePrefab";
            string expectedPath = $"{PrefabFolder}/{expectedName}.prefab";
            if (refObj == null)
            {
                ok = false;
                sb.AppendLine($"スロット {i}: 未割り当て（期待: {expectedName}）");
                continue;
            }

            string actualPath = AssetDatabase.GetAssetPath(refObj);
            if (!string.Equals(actualPath, expectedPath, System.StringComparison.OrdinalIgnoreCase))
            {
                ok = false;
                sb.AppendLine($"スロット {i}: {refObj.name}（{actualPath}）→ 期待 {expectedPath}");
            }
        }

        ValidateBossRef(so, "bossMiddle01ImagePrefab", $"{PrefabFolder}/BossMiddle01ImagePrefab.prefab", sb, ref ok);
        ValidateBossRef(so, "boss01ImagePrefab", $"{PrefabFolder}/Boss01ImagePrefab.prefab", sb, ref ok);

        SerializedProperty templateProp = so.FindProperty("enemyTemplate");
        if (templateProp != null)
        {
            Object template = templateProp.objectReferenceValue;
            string expectedTemplate = $"{PrefabFolder}/Enemy00ImagePrefab.prefab";
            string templatePath = template != null ? AssetDatabase.GetAssetPath(template) : null;
            if (!string.Equals(templatePath, expectedTemplate, System.StringComparison.OrdinalIgnoreCase))
            {
                ok = false;
                sb.AppendLine($"enemyTemplate: {(template != null ? templatePath : "未設定")} → 期待 {expectedTemplate}");
            }
        }

        if (ok)
        {
            report = "[Game03EnemyPrefabSlotValidator] OK — スロット 0〜15 は Enemy00〜15 と一致しています。";
        }
        else
        {
            report = "[Game03EnemyPrefabSlotValidator] 不一致:\n" + sb +
                     "メニュー Tools/Game03/Fix Enemy Prefab Slots In game03_scene で修正できます。";
        }

        return ok;
    }

    private static void ValidateBossRef(
        SerializedObject so,
        string propertyName,
        string expectedPath,
        StringBuilder sb,
        ref bool ok)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            return;
        }

        Object refObj = prop.objectReferenceValue;
        string actualPath = refObj != null ? AssetDatabase.GetAssetPath(refObj) : null;
        if (!string.Equals(actualPath, expectedPath, System.StringComparison.OrdinalIgnoreCase))
        {
            ok = false;
            sb.AppendLine(
                $"{propertyName}: {(refObj != null ? $"{refObj.name} ({actualPath})" : "未設定")} → 期待 {expectedPath}");
        }
    }
}
#endif
