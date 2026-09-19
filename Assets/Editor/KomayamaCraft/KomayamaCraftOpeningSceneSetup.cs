#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft.EditorTools
{
    /// <summary>
    /// OP 目覚め UI・OpeningController・立ち絵辞書・Tutorial 無効を一括セットアップ。
    /// </summary>
    public static class KomayamaCraftOpeningSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/komayama_craft_scene.unity";
        private const string WakeSpritePath = "Assets/Sprites/演出/演出-目覚め.png";
        private const string Unit05Path = "Assets/Sprites/立ち絵/unit05.png";
        private const string MainUnitPath = "Assets/Sprites/立ち絵/main_unit.png";

        [MenuItem("KomayamaCraft/Setup Opening Sequence")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera mainCam = Camera.main;

            GameObject canvasGo = FindIncludingInactive("DialogueOverlayCanvas");
            if (canvasGo == null)
            {
                Debug.LogError("[Opening] DialogueOverlayCanvas missing. Run Setup Dialogue Overlay Canvas first.");
                return;
            }

            GameObject hostGo = GameObject.Find("OpeningHost");
            if (hostGo == null)
            {
                hostGo = new GameObject("OpeningHost");
            }

            KomayamaCraftOpeningController opening =
                hostGo.GetComponent<KomayamaCraftOpeningController>();
            if (opening == null)
            {
                opening = hostGo.AddComponent<KomayamaCraftOpeningController>();
            }

            Transform wake = EnsureChild(canvasGo.transform, "WakeRoot");
            RectTransform wakeRt = wake as RectTransform;
            StretchFull(wakeRt);

            Image white = EnsureImage(wake, "WhiteVeil", Color.white);
            StretchFull(white.rectTransform);
            white.raycastTarget = true;

            Sprite wakeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WakeSpritePath);
            Image topLid = EnsureImage(wake, "EyelidTop", Color.white);
            ConfigureEyelid(topLid.rectTransform, top: true);
            if (wakeSprite != null)
            {
                topLid.sprite = wakeSprite;
                topLid.type = Image.Type.Simple;
                topLid.preserveAspect = false;
            }

            Image bottomLid = EnsureImage(wake, "EyelidBottom", Color.white);
            ConfigureEyelid(bottomLid.rectTransform, top: false);
            if (wakeSprite != null)
            {
                bottomLid.sprite = wakeSprite;
                bottomLid.type = Image.Type.Simple;
                bottomLid.preserveAspect = false;
            }

            // ConfigureEyelid 内で scale.y = -1 を設定済み

            Transform callWindow = EnsureChild(wake, "CallWindow");
            RectTransform callRt = callWindow as RectTransform;
            callRt.anchorMin = new Vector2(0.5f, 0f);
            callRt.anchorMax = new Vector2(0.5f, 0f);
            callRt.pivot = new Vector2(0.5f, 0f);
            callRt.anchoredPosition = new Vector2(0f, 40f);
            callRt.sizeDelta = new Vector2(1400f, 280f);
            Image callBg = callWindow.GetComponent<Image>();
            if (callBg == null)
            {
                callBg = callWindow.gameObject.AddComponent<Image>();
            }

            callBg.color = new Color(0f, 0f, 0f, 0.72f);

            TextMeshProUGUI speaker = EnsureTmp(callWindow, "CallSpeaker", 28f);
            RectTransform spRt = speaker.rectTransform;
            spRt.anchorMin = new Vector2(0f, 1f);
            spRt.anchorMax = new Vector2(1f, 1f);
            spRt.pivot = new Vector2(0f, 1f);
            spRt.anchoredPosition = new Vector2(36f, -18f);
            spRt.sizeDelta = new Vector2(-72f, 40f);

            TextMeshProUGUI body = EnsureTmp(callWindow, "CallBody", 32f);
            RectTransform bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(36f, 48f);
            bodyRt.offsetMax = new Vector2(-36f, -24f);

            wake.gameObject.SetActive(false);

            // OpeningController は上で OpeningHost に付与済み

            KomayamaCraftDialogueOverlay overlay =
                canvasGo.GetComponent<KomayamaCraftDialogueOverlay>();
            KomayamaCraftDialogueRunner runner =
                canvasGo.GetComponent<KomayamaCraftDialogueRunner>();
            KomayamaGameClock clock =
                Object.FindFirstObjectByType<KomayamaGameClock>(FindObjectsInactive.Include);
            KomayamaCraftCameraController camCtrl =
                Object.FindFirstObjectByType<KomayamaCraftCameraController>(FindObjectsInactive.Include);
            KomayamaCraftDebugManager debug =
                Object.FindFirstObjectByType<KomayamaCraftDebugManager>(FindObjectsInactive.Include);
            KomayamaSaveService save =
                Object.FindFirstObjectByType<KomayamaSaveService>(FindObjectsInactive.Include);
            GameObject ship = FindIncludingInactive("CrashedShip");

            SerializedObject soOp = new SerializedObject(opening);
            soOp.FindProperty("debugManager").objectReferenceValue = debug;
            soOp.FindProperty("gameClock").objectReferenceValue = clock;
            soOp.FindProperty("cameraController").objectReferenceValue = camCtrl;
            soOp.FindProperty("dialogueOverlay").objectReferenceValue = overlay;
            soOp.FindProperty("shipFocusTarget").objectReferenceValue =
                ship != null ? ship.transform : null;
            soOp.FindProperty("saveService").objectReferenceValue = save;
            soOp.FindProperty("dialogueCanvasRoot").objectReferenceValue = canvasGo;
            soOp.FindProperty("wakeRoot").objectReferenceValue = wake.gameObject;
            soOp.FindProperty("whiteVeil").objectReferenceValue = white;
            soOp.FindProperty("eyelidTop").objectReferenceValue = topLid.rectTransform;
            soOp.FindProperty("eyelidBottom").objectReferenceValue = bottomLid.rectTransform;
            soOp.FindProperty("callSpeakerText").objectReferenceValue = speaker;
            soOp.FindProperty("callBodyText").objectReferenceValue = body;
            soOp.FindProperty("callWindowRoot").objectReferenceValue = callWindow.gameObject;
            soOp.FindProperty("openingDialogueStageKey").stringValue = "craft_op_01";
            soOp.ApplyModifiedPropertiesWithoutUndo();

            if (runner != null)
            {
                Sprite unit05 = AssetDatabase.LoadAssetAtPath<Sprite>(Unit05Path);
                Sprite mainUnit = AssetDatabase.LoadAssetAtPath<Sprite>(MainUnitPath);
                SerializedObject soRunner = new SerializedObject(runner);
                SerializedProperty stands = soRunner.FindProperty("standingSprites");
                stands.arraySize = 2;
                SetNamedSprite(stands, 0, "unit05", unit05);
                SetNamedSprite(stands, 1, "main_unit", mainUnit);
                soRunner.ApplyModifiedPropertiesWithoutUndo();
            }

            KomayamaTutorialController tutorial =
                Object.FindFirstObjectByType<KomayamaTutorialController>(FindObjectsInactive.Include);
            if (tutorial != null)
            {
                SerializedObject soTut = new SerializedObject(tutorial);
                soTut.FindProperty("runOnNewGame").boolValue = false;
                soTut.ApplyModifiedPropertiesWithoutUndo();
            }

            // Opening 開始時に Canvas を起こし、会話終了で Overlay が非表示にする。
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Opening] Setup saved: " + ScenePath);
        }

        private static void SetNamedSprite(
            SerializedProperty arr,
            int index,
            string key,
            Sprite sprite)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("key").stringValue = key;
            e.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        }

        private static void ConfigureEyelid(RectTransform rt, bool top)
        {
            // 縦ストレッチではなく「上端／下端固定＋高さ」にして、上下退避の開閉を安定させる
            const float lidHeight = 600f;
            if (top)
            {
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, lidHeight);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                // 同じ絵を Y 反転。pivot 下端＋ scale.y=-1 だと絵が画面下へ出るため、
                // 閉じ位置は pivot を lidHeight まで上げて、反転後に 0〜lidHeight を覆う。
                rt.localScale = new Vector3(1f, -1f, 1f);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, lidHeight);
                rt.anchoredPosition = new Vector2(0f, lidHeight);
            }
        }

        private static GameObject FindIncludingInactive(string name)
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r].name == name)
                {
                    return roots[r];
                }

                Transform[] ts = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < ts.Length; i++)
                {
                    if (ts[i].name == name)
                    {
                        return ts[i].gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            Transform t = EnsureChild(parent, name);
            Image img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
            }

            img.color = color;
            return img;
        }

        private static TextMeshProUGUI EnsureTmp(Transform parent, string name, float size)
        {
            Transform t = EnsureChild(parent, name);
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
            }

            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
#endif
