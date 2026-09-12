using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// game02 開始演出（StageStartOverlay）。設定値は <see cref="Game02EffectManager"/>「ゲーム開始演出設定」、
    /// SE は <see cref="Game02SeManager"/> の StageStart* Cue を参照する。
    /// 実行は本コンポーネントが担当し、手順は <c>spec/game02/stage_start_overlay_spec.md</c> に準拠。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageStartOverlayController : MonoBehaviour
    {
        private const string OpStampImageName = "OpStampImage";
        private const string ItemMailCharaImageName = "ItemMailCharaImage";

        [Header("Refs")]
        [SerializeField, Tooltip("StageStartOverlay 配下のスタンプ画像（Image）。未設定時は子の OpStampImage を自動検出。")]
        private Image opStampImage;

        [SerializeField, Tooltip("StageStartOverlay 配下のキャラ画像（Image）。未設定時は子の ItemMailCharaImage を自動検出。")]
        private Image itemMailCharaImage;

        [SerializeField] private ItemSpawnController itemSpawnController;
        [SerializeField] private ItemPlacementZone itemPlacementZone;

        private Coroutine playRoutine;
        private GameManager kickTarget;
        private readonly List<GameObject> hiddenRuntimeMailCharaObjects = new List<GameObject>();

        public static StageStartOverlayController EnsureSceneController()
        {
            GameObject overlay = FindStageStartOverlayInScene();
            if (overlay == null)
            {
                return null;
            }

            StageStartOverlayController controller = overlay.GetComponent<StageStartOverlayController>();
            if (controller == null)
            {
                controller = overlay.AddComponent<StageStartOverlayController>();
            }

            return controller;
        }

        private static GameObject FindStageStartOverlayInScene()
        {
            GameObject panelCanvas = GameObject.Find("PanelCanvas");
            if (panelCanvas != null)
            {
                Transform panelRt = panelCanvas.transform;
                for (int i = 0; i < panelRt.childCount; i++)
                {
                    Transform child = panelRt.GetChild(i);
                    if (child != null && child.name == "StageStartOverlay")
                    {
                        return child.gameObject;
                    }
                }
            }

            GameObject byPath = GameObject.Find("PanelCanvas/StageStartOverlay");
            if (byPath != null)
            {
                return byPath;
            }

            return FindInactiveByName("StageStartOverlay");
        }

        public void PlaySequence(GameManager gameManager)
        {
            kickTarget = gameManager;
            Game02SceneLifecycleLog.StageStartSequenceBegin("StageStartOverlayController.PlaySequence");
            CacheRefsIfNeeded();

            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            // Unity は非アクティブな GameObject では StartCoroutine を開始できない。
            // CoPlaySequence 内で初めて有効化すると開始に失敗するため、ホストを先にアクティブにする。
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    "[StageStartOverlay] 親が非アクティブなどでヒエラルキー上も無効のためコルーチンを開始できません。開始演出をスキップします。",
                    this);
                CompleteSequence(spawnFallbackMailCharaIfMissing: true);
                return;
            }

            playRoutine = StartCoroutine(CoPlaySequence());
        }

        private IEnumerator CoPlaySequence()
        {
            CacheRefsIfNeeded();

            // 1. アイテム配置ゾーン左端の座標（最終的に ItemMailChara を置く位置）を取得。
            //    オーバーレイ座標系での目標位置と、ItemCanvas 座標系での目標位置を両方確保する。
            if (!TryResolveTargetPositions(out Vector2 targetAnchoredInOverlay, out Vector2 targetAnchoredInItemCanvas))
            {
                Debug.LogWarning(
                    "[StageStartOverlay] アイテム配置ゾーン左端の座標を解決できません。開始演出をスキップします。",
                    this);
                CompleteSequence(spawnFallbackMailCharaIfMissing: true);
                yield break;
            }

            // 演出中は実体 ItemMailChara を見せない（回転収束完了後にのみ再表示）。
            HideRuntimeMailCharaVisuals();

            // 2. OpStampImage / ItemMailCharaImage を非表示初期化（StageStartOverlay 有効化後に出さないため）。
            if (opStampImage != null)
            {
                opStampImage.gameObject.SetActive(false);
            }

            if (itemMailCharaImage != null)
            {
                itemMailCharaImage.gameObject.SetActive(false);
            }

            // 3. StageStartOverlay を有効化。
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            Game02EffectManager effects = Game02EffectManager.TryGet();
            float stampDelay = effects != null ? effects.GameStartStampDelaySeconds : 0.4f;
            float charaAppearDelay = effects != null ? effects.GameStartCharaAppearDelaySeconds : 0.5f;
            float charaMoveDelay = effects != null ? effects.GameStartCharaMoveDelaySeconds : 0.4f;
            float charaMoveDuration = effects != null ? effects.GameStartCharaMoveDurationSeconds : 1.2f;
            float spinDegPerSecond = effects != null ? effects.GameStartCharaSpinDegreesPerSecond : 540f;
            float spinSettleDuration = effects != null ? effects.GameStartCharaSpinSettleDurationSeconds : 0.45f;

            // 4. <スタンプ表示待機時間> 待機。
            yield return WaitUnscaled(stampDelay);

            // 5. SE1 を鳴らすと同時に OpStampImage を表示。
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.StageStartStamp);
            if (opStampImage != null)
            {
                opStampImage.gameObject.SetActive(true);
            }

            // 6. <キャラ表示待機時間> 待機。
            yield return WaitUnscaled(charaAppearDelay);

            // 7. SE2 を鳴らすと同時に ItemMailCharaImage を表示。
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.StageStartCharaAppear);
            if (itemMailCharaImage != null)
            {
                itemMailCharaImage.gameObject.SetActive(true);
            }

            // 8. <移動待機時間> 待機。
            yield return WaitUnscaled(charaMoveDelay);

            // 9. SE3 を鳴らすと同時に ItemMailCharaImage を移動。
            Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.StageStartCharaMove);
            yield return CoMoveItemMailCharaImage(targetAnchoredInOverlay, charaMoveDuration, spinDegPerSecond, spinSettleDuration);

            // 10. ItemMailChara をアイテム配置ゾーン（左端）に配置。
            TrySpawnRealMailCharaAtZoneLeft(targetAnchoredInItemCanvas);
            RestoreHiddenRuntimeMailCharaVisuals();

            // 11. StageStartOverlay を無効化すると同時にゲーム開始通知。
            CompleteSequence(spawnFallbackMailCharaIfMissing: false);
        }

        private IEnumerator CoMoveItemMailCharaImage(Vector2 targetAnchoredInOverlay, float moveDuration, float spinDegPerSecond, float settleDuration)
        {
            if (itemMailCharaImage == null)
            {
                yield break;
            }

            RectTransform rect = itemMailCharaImage.rectTransform;
            if (rect == null)
            {
                yield break;
            }

            Vector2 startAnchored = rect.anchoredPosition;
            float duration = Mathf.Max(0.01f, moveDuration);
            float elapsed = 0f;
            float currentZ = NormalizeAngleSigned(rect.localEulerAngles.z);

            while (elapsed < duration)
            {
                float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / duration);

                rect.anchoredPosition = Vector2.LerpUnclamped(startAnchored, targetAnchoredInOverlay, t);

                currentZ = NormalizeAngleSigned(currentZ + spinDegPerSecond * dt);
                rect.localEulerAngles = new Vector3(0f, 0f, currentZ);

                yield return null;
            }

            rect.anchoredPosition = targetAnchoredInOverlay;

            // 終端到達後、現在の角度から 0 度まで線形に減速して合わせる（B 案）。
            float settle = Mathf.Max(0f, settleDuration);
            if (settle <= 0f)
            {
                rect.localEulerAngles = new Vector3(0f, 0f, 0f);
                yield break;
            }

            float startZ = NormalizeAngleSigned(currentZ);
            float settleElapsed = 0f;
            while (settleElapsed < settle)
            {
                float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
                settleElapsed += dt;
                float t = Mathf.Clamp01(settleElapsed / settle);
                float z = Mathf.LerpUnclamped(startZ, 0f, EaseOutCubic(t));
                rect.localEulerAngles = new Vector3(0f, 0f, z);
                yield return null;
            }

            rect.localEulerAngles = new Vector3(0f, 0f, 0f);
        }

        private void TrySpawnRealMailCharaAtZoneLeft(Vector2 targetAnchoredInItemCanvas)
        {
            CacheRefsIfNeeded();
            if (itemSpawnController == null || itemSpawnController.ItemCanvas == null)
            {
                return;
            }

            Vector3 worldAtZoneLeft = itemSpawnController.ItemCanvas.TransformPoint(targetAnchoredInItemCanvas);
            itemSpawnController.TrySpawnInitialMailCharaAtWorldPositionIfAbsent(worldAtZoneLeft);
        }

        private bool TryResolveTargetPositions(out Vector2 anchoredInOverlay, out Vector2 anchoredInItemCanvas)
        {
            anchoredInOverlay = Vector2.zero;
            anchoredInItemCanvas = Vector2.zero;

            if (itemPlacementZone == null || itemPlacementZone.ZoneRect == null)
            {
                return false;
            }

            RectTransform zoneRect = itemPlacementZone.ZoneRect;
            Vector3[] corners = new Vector3[4];
            zoneRect.GetWorldCorners(corners);
            // 左下（初期配置想定）。
            Vector3 leftBottomWorld = corners[0];

            // ItemCanvas 座標系（実体スポーン用）。
            RectTransform itemCanvas = itemSpawnController != null ? itemSpawnController.ItemCanvas : null;
            if (itemCanvas != null)
            {
                if (!TryWorldToAnchored(itemCanvas, leftBottomWorld, out anchoredInItemCanvas))
                {
                    return false;
                }
            }
            else
            {
                anchoredInItemCanvas = Vector2.zero;
            }

            // オーバーレイ座標系（itemMailCharaImage が住む空間）。
            RectTransform overlayParent = itemMailCharaImage != null ? itemMailCharaImage.rectTransform.parent as RectTransform : null;
            if (overlayParent == null)
            {
                overlayParent = transform as RectTransform;
            }

            if (overlayParent == null)
            {
                return false;
            }

            if (!TryWorldToAnchored(overlayParent, leftBottomWorld, out Vector2 overlayLocal))
            {
                return false;
            }

            anchoredInOverlay = overlayLocal;
            return true;
        }

        private static bool TryWorldToAnchored(RectTransform space, Vector3 world, out Vector2 anchored)
        {
            anchored = Vector2.zero;
            if (space == null)
            {
                return false;
            }

            Canvas canvas = space.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, cam, out anchored);
        }

        private void CompleteSequence(bool spawnFallbackMailCharaIfMissing)
        {
            playRoutine = null;
            RestoreHiddenRuntimeMailCharaVisuals();

            if (spawnFallbackMailCharaIfMissing)
            {
                CacheRefsIfNeeded();
                itemSpawnController?.ExecuteInitialMainCharacterSpawnCheckOnce();
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            Game02SceneLifecycleLog.StageStartSequenceEnd(
                $"spawnFallbackMailCharaIfMissing={spawnFallbackMailCharaIfMissing} → NotifyStageStartSequenceCompleted");
            kickTarget?.NotifyStageStartSequenceCompleted();
        }

        private void HideRuntimeMailCharaVisuals()
        {
            hiddenRuntimeMailCharaObjects.Clear();
            if (itemSpawnController == null || itemSpawnController.ItemCanvas == null)
            {
                return;
            }

            DraggableItemController[] all = itemSpawnController.ItemCanvas.GetComponentsInChildren<DraggableItemController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null || item.ItemType != ItemType.ItemMailChara)
                {
                    continue;
                }

                GameObject go = item.gameObject;
                if (go != null && go.activeSelf)
                {
                    go.SetActive(false);
                    hiddenRuntimeMailCharaObjects.Add(go);
                }
            }
        }

        private void RestoreHiddenRuntimeMailCharaVisuals()
        {
            for (int i = 0; i < hiddenRuntimeMailCharaObjects.Count; i++)
            {
                GameObject go = hiddenRuntimeMailCharaObjects[i];
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            hiddenRuntimeMailCharaObjects.Clear();
        }

        private void CacheRefsIfNeeded()
        {
            if (opStampImage == null)
            {
                Transform t = FindChildByName(transform, OpStampImageName);
                if (t != null)
                {
                    opStampImage = t.GetComponent<Image>();
                }
            }

            if (itemMailCharaImage == null)
            {
                Transform t = FindChildByName(transform, ItemMailCharaImageName);
                if (t != null)
                {
                    itemMailCharaImage = t.GetComponent<Image>();
                }
            }

            if (itemSpawnController == null)
            {
                itemSpawnController = FindObjectOfType<ItemSpawnController>(true);
            }

            if (itemPlacementZone == null && itemSpawnController != null)
            {
                itemPlacementZone = itemSpawnController.PlacementZone;
            }

            if (itemPlacementZone == null)
            {
                itemPlacementZone = FindObjectOfType<ItemPlacementZone>(true);
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            // 直下に見つからなければ深い階層も走査（非アクティブ含む）。
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static GameObject FindInactiveByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == objectName)
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Mathf.Max(0f, Time.unscaledDeltaTime);
                yield return null;
            }
        }

        private static float NormalizeAngleSigned(float deg)
        {
            float v = deg % 360f;
            if (v > 180f)
            {
                v -= 360f;
            }
            else if (v < -180f)
            {
                v += 360f;
            }

            return v;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
