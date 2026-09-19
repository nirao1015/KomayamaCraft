#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft.EditorTools
{
    /// <summary>
    /// 描画バンド整理のワンショット適用（メニューから実行）。
    /// </summary>
    public static class KomayamaDrawBandSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/komayama_craft_scene.unity";

        [MenuItem("KomayamaCraft/Apply Draw Band Scene Setup")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform world = GameObject.Find("World") != null
                ? GameObject.Find("World").transform
                : null;
            GameObject hudGo = GameObject.Find("HudCanvas");
            if (world == null || hudGo == null)
            {
                Debug.LogError("[DrawBand] World or HudCanvas missing.");
                return;
            }

            Camera mainCam = Camera.main;
            KomayamaCraftDebugManager dm =
                Object.FindFirstObjectByType<KomayamaCraftDebugManager>(FindObjectsInactive.Include);
            KomayamaCraftHud craftHud =
                Object.FindFirstObjectByType<KomayamaCraftHud>(FindObjectsInactive.Include);

            Transform layerDrops = EnsureChild(world, "Layer_Drops");
            Transform fieldDrop = world.Find("FieldDropArea");
            if (fieldDrop != null)
            {
                for (int i = fieldDrop.childCount - 1; i >= 0; i--)
                {
                    Transform ch = fieldDrop.GetChild(i);
                    if (ch.GetComponent<KomayamaDroppedItem>() != null ||
                        ch.name.StartsWith("Drop"))
                    {
                        ch.SetParent(layerDrops, true);
                    }
                }

                KomayamaDropArea dropArea = fieldDrop.GetComponent<KomayamaDropArea>();
                if (dropArea != null)
                {
                    SerializedObject soDrop = new SerializedObject(dropArea);
                    soDrop.FindProperty("droppedItemRoot").objectReferenceValue = layerDrops;
                    soDrop.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EnsureChild(world, "Layer_WorldOverlay");

            Transform systemCanvas = FindNamed(scene.GetRootGameObjects(), "SystemCanvas");
            if (systemCanvas == null)
            {
                Transform layerSystem = world.Find("Layer_System");
                if (layerSystem != null)
                {
                    systemCanvas = layerSystem.Find("SystemCanvas");
                }
            }

            if (systemCanvas != null && systemCanvas.parent != null)
            {
                systemCanvas.SetParent(null, true);
            }

            GameObject debugCanvasGo = GameObject.Find("DebugCanvas");
            if (debugCanvasGo == null)
            {
                debugCanvasGo = new GameObject(
                    "DebugCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(FixedAspectCanvasFitter));
            }

            Canvas debugCanvas = debugCanvasGo.GetComponent<Canvas>();
            ConfigureScreenCanvas(debugCanvas, mainCam, "UiDebug");
            ConfigureFitter(debugCanvasGo.GetComponent<FixedAspectCanvasFitter>(), debugCanvas, mainCam);
            CanvasScaler dbgScaler = debugCanvasGo.GetComponent<CanvasScaler>();
            dbgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            dbgScaler.referenceResolution = new Vector2(1920f, 1080f);
            dbgScaler.matchWidthOrHeight = 0.5f;

            string[] moveNames =
            {
                "GuideText", "StateText", "HandText", "DebugOverlay", "CursorHand"
            };
            for (int i = 0; i < moveNames.Length; i++)
            {
                Transform t = hudGo.transform.Find(moveNames[i]);
                if (t != null)
                {
                    t.SetParent(debugCanvasGo.transform, false);
                }
            }

            Transform debugOverlay = debugCanvasGo.transform.Find("DebugOverlay");
            if (debugOverlay != null)
            {
                KomayamaCraftSpeedCycleButton[] buttons =
                    Object.FindObjectsByType<KomayamaCraftSpeedCycleButton>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].transform.parent != debugOverlay)
                    {
                        buttons[i].transform.SetParent(debugOverlay, false);
                    }
                }
            }

            Canvas hudCanvas = hudGo.GetComponent<Canvas>();
            ConfigureScreenCanvas(hudCanvas, mainCam, "UiHud");
            FixedAspectCanvasFitter hudFitter = hudGo.GetComponent<FixedAspectCanvasFitter>();
            if (hudFitter != null)
            {
                ConfigureFitter(hudFitter, hudCanvas, mainCam);
            }

            if (systemCanvas != null)
            {
                Canvas sc = systemCanvas.GetComponent<Canvas>();
                ConfigureScreenCanvas(sc, mainCam, "UiSystem");
                FixedAspectCanvasFitter sf = systemCanvas.GetComponent<FixedAspectCanvasFitter>();
                if (sf != null)
                {
                    ConfigureFitter(sf, sc, mainCam);
                }
            }

            if (craftHud != null)
            {
                SerializedObject soHud = new SerializedObject(craftHud);
                AssignTmp(soHud, "guideText", debugCanvasGo.transform.Find("GuideText"));
                AssignTmp(soHud, "stateText", debugCanvasGo.transform.Find("StateText"));
                AssignTmp(soHud, "handText", debugCanvasGo.transform.Find("HandText"));
                AssignTmp(soHud, "messageText", hudGo.transform.Find("MessageText"));
                Transform cursor = debugCanvasGo.transform.Find("CursorHand");
                if (cursor != null)
                {
                    soHud.FindProperty("cursorHandRoot").objectReferenceValue =
                        cursor.GetComponent<RectTransform>();
                }

                soHud.ApplyModifiedPropertiesWithoutUndo();
            }

            if (dm != null && debugOverlay != null)
            {
                SerializedObject soDm = new SerializedObject(dm);
                soDm.FindProperty("debugOverlay").objectReferenceValue = debugOverlay.gameObject;
                soDm.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject bandsHost = GameObject.Find("KomayamaCraftSystems");
            if (bandsHost == null && dm != null)
            {
                bandsHost = dm.gameObject;
            }

            if (bandsHost != null)
            {
                KomayamaScreenCanvasBands bands =
                    bandsHost.GetComponent<KomayamaScreenCanvasBands>();
                if (bands == null)
                {
                    bands = bandsHost.AddComponent<KomayamaScreenCanvasBands>();
                }

                SerializedObject soB = new SerializedObject(bands);
                SerializedProperty arr = soB.FindProperty("bands");
                arr.arraySize = 3;
                SetBand(arr, 0, hudCanvas, "UiHud");
                SetBand(
                    arr,
                    1,
                    systemCanvas != null ? systemCanvas.GetComponent<Canvas>() : null,
                    "UiSystem");
                SetBand(arr, 2, debugCanvas, "UiDebug");
                soB.ApplyModifiedPropertiesWithoutUndo();
                bands.Apply();
            }

            KomayamaWorldLayerDrawOrder wdo = world.GetComponent<KomayamaWorldLayerDrawOrder>();
            if (wdo != null)
            {
                // Force default rules refresh by re-applying (serialized defaults may be old on component)
                SerializedObject soW = new SerializedObject(wdo);
                SerializedProperty rules = soW.FindProperty("spriteRules");
                rules.arraySize = 11;
                SetSpriteRule(rules, 0, "WorldLayers/Layer_Sea", "WorldSea");
                SetSpriteRule(rules, 1, "WorldLayers/Layer_Continent", "WorldContinent");
                SetSpriteRule(rules, 2, "WorldLayers/Layer_Objects", "WorldObject");
                SetSpriteRule(rules, 3, "WorldLayers/Layer_Effects", "WorldEffect");
                SetSpriteRule(rules, 4, "Layer_Facilities", "WorldFacility");
                SetSpriteRule(rules, 5, "Layer_Drops", "WorldDrop");
                SetSpriteRule(rules, 6, "Layer_Mouse", "WorldMouse");
                SetSpriteRule(rules, 7, "Layer_WorldOverlay", "WorldOverlay");
                SetSpriteRule(rules, 8, "DropRestrictionGrid", "WorldOverlay");
                SetSpriteRule(rules, 9, "BuildRestrictionGrid", "WorldOverlay");
                SetSpriteRule(rules, 10, "BuildPreview", "WorldOverlay");
                // clear old canvasRules
                SerializedProperty canvasRules = soW.FindProperty("canvasRules");
                if (canvasRules != null)
                {
                    canvasRules.arraySize = 0;
                }

                soW.ApplyModifiedPropertiesWithoutUndo();
                wdo.Apply();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DrawBand] Applied and saved " + ScenePath);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindNamed(GameObject[] roots, string name)
        {
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r].name == name)
                {
                    return roots[r].transform;
                }

                Transform[] ts = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < ts.Length; i++)
                {
                    if (ts[i].name == name)
                    {
                        return ts[i];
                    }
                }
            }

            return null;
        }

        private static void ConfigureScreenCanvas(Canvas canvas, Camera cam, string layer)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.sortingLayerName = layer;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 0;
            canvas.planeDistance = 1f;
            EditorUtility.SetDirty(canvas);
        }

        private static void ConfigureFitter(
            FixedAspectCanvasFitter fitter,
            Canvas canvas,
            Camera cam)
        {
            if (fitter == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(fitter);
            so.FindProperty("targetCanvas").objectReferenceValue = canvas;
            so.FindProperty("targetCamera").objectReferenceValue = cam;
            so.FindProperty("forceFrontSorting").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignTmp(SerializedObject soHud, string prop, Transform target)
        {
            if (target == null)
            {
                return;
            }

            soHud.FindProperty(prop).objectReferenceValue =
                target.GetComponent<TextMeshProUGUI>();
        }

        private static void SetBand(
            SerializedProperty arr,
            int index,
            Canvas canvas,
            string layer)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("canvas").objectReferenceValue = canvas;
            e.FindPropertyRelative("sortingLayerName").stringValue = layer;
            e.FindPropertyRelative("sortingOrder").intValue = 0;
            e.FindPropertyRelative("planeDistance").floatValue = 1f;
        }

        private static void SetSpriteRule(
            SerializedProperty arr,
            int index,
            string path,
            string layer)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("rootPath").stringValue = path;
            e.FindPropertyRelative("sortingLayerName").stringValue = layer;
        }
    }
}
#endif
