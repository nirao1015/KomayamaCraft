#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft.EditorTools
{
    /// <summary>
    /// DialogueOverlayCanvas を craft シーンへ配置し、参照を結線する。
    /// </summary>
    public static class KomayamaCraftDialogueOverlaySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/komayama_craft_scene.unity";

        [MenuItem("KomayamaCraft/Setup Dialogue Overlay Canvas")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[DialogueOverlay] Main Camera missing.");
                return;
            }

            GameObject canvasGo = GameObject.Find("DialogueOverlayCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject(
                    "DialogueOverlayCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(FixedAspectCanvasFitter));
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCam;
            canvas.sortingLayerName = "UiSystem";
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
            canvas.planeDistance = 1f;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            FixedAspectCanvasFitter fitter = canvasGo.GetComponent<FixedAspectCanvasFitter>();
            SerializedObject soFitter = new SerializedObject(fitter);
            soFitter.FindProperty("targetCanvas").objectReferenceValue = canvas;
            soFitter.FindProperty("targetCamera").objectReferenceValue = mainCam;
            soFitter.FindProperty("forceFrontSorting").boolValue = false;
            soFitter.ApplyModifiedPropertiesWithoutUndo();

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.zero;
            canvasRt.pivot = Vector2.zero;
            canvasRt.anchoredPosition = Vector2.zero;
            canvasRt.sizeDelta = Vector2.zero;

            Image dimmer = EnsureFullImage(canvasGo.transform, "Dimmer", new Color(0f, 0f, 0f, 0.45f));
            Image overlayBg = EnsureFullImage(canvasGo.transform, "OverlayBackground", Color.white);
            overlayBg.gameObject.SetActive(false);

            Transform standRoot = EnsureRectChild(canvasGo.transform, "StandingArtRoot");
            StretchFull(standRoot as RectTransform);
            Image standL = EnsureImage(standRoot, "StandingLeft", new Vector2(0.2f, 0.36f), new Vector2(700f, 780f));
            Image standR = EnsureImage(standRoot, "StandingRight", new Vector2(0.8f, 0.36f), new Vector2(420f, 780f));
            standL.gameObject.SetActive(false);
            standR.gameObject.SetActive(false);

            Transform window = EnsureRectChild(canvasGo.transform, "DialogueWindow");
            RectTransform windowRt = window as RectTransform;
            windowRt.anchorMin = new Vector2(0.5f, 0f);
            windowRt.anchorMax = new Vector2(0.5f, 0f);
            windowRt.pivot = new Vector2(0.5f, 0f);
            windowRt.anchoredPosition = new Vector2(0f, 40f);
            windowRt.sizeDelta = new Vector2(1400f, 280f);

            Image windowBg = window.GetComponent<Image>();
            if (windowBg == null)
            {
                windowBg = window.gameObject.AddComponent<Image>();
            }

            windowBg.color = new Color(0f, 0f, 0f, 0.72f);

            TextMeshProUGUI speaker = EnsureTmp(window, "SpeakerName", 28f, TextAlignmentOptions.Left);
            RectTransform speakerRt = speaker.rectTransform;
            speakerRt.anchorMin = new Vector2(0f, 1f);
            speakerRt.anchorMax = new Vector2(1f, 1f);
            speakerRt.pivot = new Vector2(0f, 1f);
            speakerRt.anchoredPosition = new Vector2(36f, -18f);
            speakerRt.sizeDelta = new Vector2(-72f, 40f);

            TextMeshProUGUI body = EnsureTmp(window, "BodyText", 32f, TextAlignmentOptions.TopLeft);
            RectTransform bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(36f, 48f);
            bodyRt.offsetMax = new Vector2(-36f, -66f);

            Transform cursorGo = EnsureRectChild(window, "AdvanceCursor");
            RectTransform cursorRt = cursorGo as RectTransform;
            cursorRt.anchorMin = new Vector2(1f, 0f);
            cursorRt.anchorMax = new Vector2(1f, 0f);
            cursorRt.pivot = new Vector2(1f, 0f);
            cursorRt.anchoredPosition = new Vector2(-28f, 18f);
            cursorRt.sizeDelta = new Vector2(28f, 28f);
            Image cursorImage = cursorGo.GetComponent<Image>();
            if (cursorImage == null)
            {
                cursorImage = cursorGo.gameObject.AddComponent<Image>();
            }

            cursorImage.color = Color.white;
            CanvasGroup cursorGroup = cursorGo.GetComponent<CanvasGroup>();
            if (cursorGroup == null)
            {
                cursorGroup = cursorGo.gameObject.AddComponent<CanvasGroup>();
            }

            cursorGroup.alpha = 0f;

            Button skip = EnsureSkipButton(window);

            Transform audioRoot = EnsureRectChild(canvasGo.transform, "Audio");
            AudioSource bgm = EnsureAudio(audioRoot, "BgmSource");
            AudioSource se = EnsureAudio(audioRoot, "SeSource");

            KomayamaCraftDialogueRunner runner =
                canvasGo.GetComponent<KomayamaCraftDialogueRunner>();
            if (runner == null)
            {
                runner = canvasGo.AddComponent<KomayamaCraftDialogueRunner>();
            }

            KomayamaCraftDialogueOverlay overlay =
                canvasGo.GetComponent<KomayamaCraftDialogueOverlay>();
            if (overlay == null)
            {
                overlay = canvasGo.AddComponent<KomayamaCraftDialogueOverlay>();
            }

            KomayamaGameClock clock =
                Object.FindFirstObjectByType<KomayamaGameClock>(FindObjectsInactive.Include);
            KomayamaCraftCameraController camCtrl =
                Object.FindFirstObjectByType<KomayamaCraftCameraController>(FindObjectsInactive.Include);

            SerializedObject soRunner = new SerializedObject(runner);
            soRunner.FindProperty("dimmerImage").objectReferenceValue = dimmer;
            soRunner.FindProperty("overlayBackgroundImage").objectReferenceValue = overlayBg;
            soRunner.FindProperty("standingLeftImage").objectReferenceValue = standL;
            soRunner.FindProperty("standingRightImage").objectReferenceValue = standR;
            soRunner.FindProperty("speakerText").objectReferenceValue = speaker;
            soRunner.FindProperty("bodyText").objectReferenceValue = body;
            soRunner.FindProperty("advanceCursorCanvasGroup").objectReferenceValue = cursorGroup;
            soRunner.FindProperty("skipButton").objectReferenceValue = skip;
            soRunner.FindProperty("bgmSource").objectReferenceValue = bgm;
            soRunner.FindProperty("seSource").objectReferenceValue = se;
            soRunner.FindProperty("cameraController").objectReferenceValue = camCtrl;
            soRunner.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject soOverlay = new SerializedObject(overlay);
            soOverlay.FindProperty("runner").objectReferenceValue = runner;
            soOverlay.FindProperty("gameClock").objectReferenceValue = clock;
            soOverlay.FindProperty("cameraController").objectReferenceValue = camCtrl;
            soOverlay.FindProperty("overlayRoot").objectReferenceValue = canvasGo;
            soOverlay.FindProperty("defaultDimAlpha").floatValue = 0.45f;
            soOverlay.ApplyModifiedPropertiesWithoutUndo();

            KomayamaScreenCanvasBands bands =
                Object.FindFirstObjectByType<KomayamaScreenCanvasBands>(FindObjectsInactive.Include);
            if (bands != null)
            {
                SerializedObject soBands = new SerializedObject(bands);
                SerializedProperty arr = soBands.FindProperty("bands");
                int dialogueBandIndex = -1;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    Object c = e.FindPropertyRelative("canvas").objectReferenceValue;
                    if (c == canvas)
                    {
                        dialogueBandIndex = i;
                        break;
                    }
                }

                if (dialogueBandIndex < 0)
                {
                    dialogueBandIndex = arr.arraySize;
                    arr.arraySize++;
                }

                SerializedProperty band = arr.GetArrayElementAtIndex(dialogueBandIndex);
                band.FindPropertyRelative("canvas").objectReferenceValue = canvas;
                band.FindPropertyRelative("sortingLayerName").stringValue = "UiSystem";
                band.FindPropertyRelative("sortingOrder").intValue = 10;
                band.FindPropertyRelative("planeDistance").floatValue = 1f;
                soBands.ApplyModifiedPropertiesWithoutUndo();
                bands.Apply();
            }

            canvasGo.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DialogueOverlay] Setup saved: " + ScenePath);
        }

        private static Image EnsureFullImage(Transform parent, string name, Color color)
        {
            Transform t = EnsureRectChild(parent, name);
            StretchFull(t as RectTransform);
            Image img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
            }

            img.color = color;
            img.raycastTarget = name == "Dimmer";
            return img;
        }

        private static Image EnsureImage(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            Transform t = EnsureRectChild(parent, name);
            RectTransform rt = t as RectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, -100f);
            Image img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
            }

            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI EnsureTmp(
            Transform parent,
            string name,
            float fontSize,
            TextAlignmentOptions align)
        {
            Transform t = EnsureRectChild(parent, name);
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
            }

            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button EnsureSkipButton(Transform window)
        {
            Transform t = EnsureRectChild(window, "SkipButton");
            RectTransform rt = t as RectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -12f);
            rt.sizeDelta = new Vector2(160f, 48f);

            Image img = t.GetComponent<Image>();
            if (img == null)
            {
                img = t.gameObject.AddComponent<Image>();
            }

            img.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

            Button button = t.GetComponent<Button>();
            if (button == null)
            {
                button = t.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = img;

            TextMeshProUGUI label = EnsureTmp(t, "Label", 28f, TextAlignmentOptions.Center);
            RectTransform labelRt = label.rectTransform;
            StretchFull(labelRt);
            label.text = "スキップ";
            label.raycastTarget = false;
            return button;
        }

        private static AudioSource EnsureAudio(Transform parent, string name)
        {
            Transform t = EnsureRectChild(parent, name);
            AudioSource src = t.GetComponent<AudioSource>();
            if (src == null)
            {
                src = t.gameObject.AddComponent<AudioSource>();
            }

            src.playOnAwake = false;
            return src;
        }

        private static Transform EnsureRectChild(Transform parent, string name)
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
