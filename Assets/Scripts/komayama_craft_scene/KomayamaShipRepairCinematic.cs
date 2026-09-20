using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// 雨漏り納品達成後の宇宙船修理演出。
    /// 仕様：spec/KomayamaCraft_宇宙船修理演出_詳細仕様.md
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaShipRepairCinematic : MonoBehaviour
    {
        public static KomayamaShipRepairCinematic Instance { get; private set; }

        public static bool IsPlaying =>
            Instance != null && Instance.playing;

        [Header("参照")]
        [SerializeField] private KomayamaShipVisual shipVisual;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform skipIndicatorRoot;
        [SerializeField] private Image skipIndicatorFill;

        [Header("ズーム")]
        [SerializeField, InspectorName("ズーム Ortho Size"), Tooltip(
            "0 以下なら KCConfigValues / ワールド設定のズーム下限（最大ズーム）を使う。通常範囲外は Clamp される。")]
        private float cinematicOrthographicSize;
        [SerializeField, Min(0.05f), InspectorName("ズームイン秒")]
        private float zoomInSeconds = 0.75f;
        [SerializeField, Min(0.05f), InspectorName("ズームアウト秒")]
        private float zoomOutSeconds = 0.6f;

        [Header("どくん")]
        [SerializeField, Min(1), InspectorName("回数")]
        private int thumpCount = 3;
        [SerializeField, Min(0.05f), InspectorName("散らす秒")]
        private float thumpSeconds = 1f;
        [SerializeField, Min(0f), InspectorName("散らしたあと停止秒")]
        private float thumpPauseSeconds = 0.4f;
        [SerializeField, Min(1f), InspectorName("ピーク拡大倍率")]
        private float thumpPeakScale = 1.55f;

        [Header("繭")]
        [SerializeField] private Sprite[] cocoonFrames = new Sprite[4];
        [SerializeField, Min(0.05f), InspectorName("1コマ秒")]
        private float cocoonFrameSeconds = 0.3f;
        [SerializeField, Min(0.1f), InspectorName("繭の拡大倍率")]
        private float cocoonScale = 1.4f;
        [SerializeField, Min(0f), InspectorName("光ったあと静止秒"), Tooltip(
            "発光イン完了後、OPEN までの待ち。既定 0.4 秒。")]
        private float cocoonGlowHoldBeforeOpenSeconds = 0.4f;

        [Header("繭・白発光（加算グロー）")]
        [SerializeField, Tooltip("Sprite-Unlit-Additive。未設定ならシェーダから生成。")]
        private Material cocoonGlowMaterial;
        [SerializeField, Min(0.05f), InspectorName("発光イン秒"), Tooltip(
            "CLOSE 直後に光らせる秒数。既定 0.6 秒。")]
        private float cocoonGlowInSeconds = 0.6f;
        [SerializeField, Min(0.05f), InspectorName("発光アウト秒")]
        private float cocoonGlowOutSeconds = 0.8f;
        [SerializeField, Min(1f), InspectorName("グロー拡大（内側）")]
        private float cocoonGlowScaleInner = 1.08f;
        [SerializeField, Range(0f, 2f), InspectorName("グロー強度（内側）")]
        private float cocoonGlowIntensityInner = 1.2f;
        [SerializeField, InspectorName("外側グローを使う")]
        private bool useCocoonGlowOuter;
        [SerializeField, Min(1f), InspectorName("グロー拡大（外側）")]
        private float cocoonGlowScaleOuter = 1.18f;
        [SerializeField, Range(0f, 2f), InspectorName("グロー強度（外側）")]
        private float cocoonGlowIntensityOuter = 0.45f;
        [SerializeField, Min(0f), InspectorName("解除後の静止秒")]
        private float afterClearHoldSeconds = 0.8f;

        [Header("スキップ")]
        [SerializeField, Min(0.1f), InspectorName("長押しスキップ秒")]
        private float skipHoldSeconds = 1.2f;

        [Header("喜びモーション（ship_2 直後・ズーム中）")]
        [SerializeField] private KCMouseFoxFollower foxFollower;
        [SerializeField] private KomayamaCraftBgmManager bgmManager;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField, Tooltip("未設定なら無音。いまは鳴らさない想定。")]
        private bool playCelebrationAudio;
        [SerializeField] private Sprite[] foxJoyFrames = new Sprite[24];
        [SerializeField, Tooltip("空なら内蔵の 60fps CSV 秒数を使う。")]
        private float[] foxJoyFrameSeconds = System.Array.Empty<float>();
        [SerializeField, Min(0f), InspectorName("開始静止秒")]
        private float foxJoyHoldFirstSeconds = 0.6f;
        [SerializeField, Min(0f), InspectorName("終了静止秒")]
        private float foxJoyHoldLastSeconds = 0.6f;
        [SerializeField, InspectorName("船左オフセット")]
        private Vector2 foxJoyLocalOffset = new(-2.4f, 0f);
        [SerializeField, Min(0.01f), InspectorName("表示高さ")]
        private float foxJoyDisplayHeight = 2.4f;

        [Header("喜びモーション・ジャンプ位置（枚目は1始まり）")]
        [SerializeField, Min(1), InspectorName("ジャンプ枚目A")]
        private int foxJoyJumpFrameA = 12;
        [SerializeField, InspectorName("ジャンプ上げA（Y）")]
        private float foxJoyJumpLiftA = 0.6f;
        [SerializeField, Min(1), InspectorName("ジャンプ枚目B")]
        private int foxJoyJumpFrameB = 13;
        [SerializeField, InspectorName("ジャンプ上げB（Y）")]
        private float foxJoyJumpLiftB = 0.35f;

        /// <summary>60fps CSV（animation_timing_60fps.csv）の duration_ms / 1000。</summary>
        private static readonly float[] DefaultFoxJoyFrameSeconds =
        {
            0.100f, 0.083333f, 0.116667f, 0.083333f, 0.066667f, 0.116667f,
            0.083333f, 0.100f, 0.133333f, 0.166667f, 0.116667f, 0.033333f,
            0.033333f, 0.033333f, 0.066667f, 0.033333f, 0.033333f, 0.033333f,
            0.083333f, 0.066667f, 0.100f, 0.116667f, 0.100f, 0.200f
        };

        private bool playing;
        private bool skipRequested;
        private float skipHoldElapsed;
        private Coroutine routine;
        private SpriteRenderer thumpRenderer;
        private SpriteRenderer cocoonRenderer;
        private SpriteRenderer cocoonGlowInner;
        private SpriteRenderer cocoonGlowOuter;
        private SpriteRenderer foxJoyRenderer;
        private Color baseColor = Color.white;

        private void Awake()
        {
            Instance = this;
            if (shipVisual == null)
            {
                shipVisual = GetComponent<KomayamaShipVisual>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (foxFollower == null)
            {
                foxFollower = FindFirstObjectByType<KCMouseFoxFollower>();
            }

            if (bgmManager == null)
            {
                bgmManager = FindFirstObjectByType<KomayamaCraftBgmManager>();
            }

            if (seManager == null)
            {
                seManager = FindFirstObjectByType<KomayamaCraftSeManager>();
            }

            SetSkipIndicator(0f, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public IEnumerator PlayRoutine()
        {
            if (playing)
            {
                yield break;
            }

            if (shipVisual == null || shipVisual.SpriteRenderer == null)
            {
                yield break;
            }

            playing = true;
            skipRequested = false;
            skipHoldElapsed = 0f;
            baseColor = shipVisual.GetSpriteColor();
            EnsureOverlayRenderers();
            // 自動プレイ中もスキップしない（仕様 §2.1.1）。AutoPlay は演出完了まで待機する。

            float zoomSize = ResolveCinematicOrthoSize();
            if (cameraController != null)
            {
                cameraController.BeginCinematicFramingSnapshot();
                cameraController.FrameForCinematic(
                    shipVisual.WorldCenter,
                    zoomSize,
                    zoomInSeconds);
            }

            float zoomWait = zoomInSeconds;
            while (zoomWait > 0f && !skipRequested)
            {
                zoomWait -= Time.unscaledDeltaTime;
                UpdateSkipHold();
                yield return null;
            }

            if (!skipRequested)
            {
                yield return PlayThumps();
            }

            if (!skipRequested)
            {
                yield return PlayCocoonThenWhiten();
            }
            else
            {
                FinishToShip2();
                CleanupOverlaysVisual();
            }

            // ship_2 直後・ズーム中に喜びモーション（スキップ時は飛ばす）
            if (!skipRequested)
            {
                yield return PlayFoxCelebrationJoy();
            }
            else
            {
                HideFoxJoyAccessory();
                foxFollower?.SetSuppressedForCinematic(false);
            }

            if (cameraController != null)
            {
                cameraController.FrameForCinematicRestore(zoomOutSeconds);
                float outWait = zoomOutSeconds;
                while (outWait > 0f)
                {
                    outWait -= Time.unscaledDeltaTime;
                    yield return null;
                }

                cameraController.EndCinematicFramingAndRestore();
            }

            HideFoxJoyAccessory();
            foxFollower?.SetSuppressedForCinematic(false);
            CleanupOverlaysVisual();
            SetSkipIndicator(0f, false);
            playing = false;
            routine = null;
        }

        public void ForceSkip()
        {
            skipRequested = true;
            skipHoldElapsed = skipHoldSeconds;
        }

        private float ResolveCinematicOrthoSize()
        {
            if (cinematicOrthographicSize > 0.01f)
            {
                return cinematicOrthographicSize;
            }

            return cameraController != null
                ? cameraController.MinimumOrthographicSize
                : 2.5f;
        }

        private IEnumerator PlayFoxCelebrationJoy()
        {
            EnsureFoxJoyRenderer();
            if (foxJoyRenderer == null ||
                foxJoyFrames == null ||
                foxJoyFrames.Length == 0 ||
                foxJoyFrames[0] == null)
            {
                yield break;
            }

            // マウス狐は displayMode を変えず一時非表示（セーブに Off が残らない）
            if (foxFollower != null)
            {
                foxFollower.SetSuppressedForCinematic(true);
            }

            try
            {
                // 音の仕組み（いまは playCelebrationAudio=false で無音）
                if (playCelebrationAudio)
                {
                    if (bgmManager != null)
                    {
                        yield return bgmManager.FadeOutAndStopRoutine();
                    }

                    seManager?.Play(KomayamaCraftSeCue.ShipRepairCompleteJoy);
                }

                PlaceFoxJoyAccessory(0);
                foxJoyRenderer.enabled = true;
                foxJoyRenderer.sprite = foxJoyFrames[0];
                ApplyFoxJoyScale(foxJoyFrames[0]);

                // 最初の画像で静止
                yield return WaitUnscaled(foxJoyHoldFirstSeconds);
                if (skipRequested)
                {
                    yield break;
                }

                // 1 回再生（フレーム秒。ジャンプ枚目だけ位置を上げる）
                int count = foxJoyFrames.Length;
                for (int i = 0; i < count; i++)
                {
                    if (skipRequested)
                    {
                        yield break;
                    }

                    Sprite frame = foxJoyFrames[i];
                    if (frame != null)
                    {
                        foxJoyRenderer.sprite = frame;
                        ApplyFoxJoyScale(frame);
                    }

                    PlaceFoxJoyAccessory(i);
                    yield return WaitUnscaled(ResolveFoxJoyFrameSeconds(i));
                }

                // 最終フレームで静止（ジャンプ上げは戻す）
                int last = count - 1;
                if (last >= 0 && foxJoyFrames[last] != null)
                {
                    foxJoyRenderer.sprite = foxJoyFrames[last];
                    ApplyFoxJoyScale(foxJoyFrames[last]);
                }

                PlaceFoxJoyAccessory(last);
                yield return WaitUnscaled(foxJoyHoldLastSeconds);

                if (playCelebrationAudio && bgmManager != null)
                {
                    yield return bgmManager.ResumeGameplayRoutine();
                }
            }
            finally
            {
                HideFoxJoyAccessory();
                if (foxFollower != null)
                {
                    foxFollower.SetSuppressedForCinematic(false);
                }
            }
        }

        private IEnumerator WaitUnscaled(float seconds)
        {
            float wait = Mathf.Max(0f, seconds);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                if (skipRequested)
                {
                    yield break;
                }

                UpdateSkipHold();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private float ResolveFoxJoyFrameSeconds(int index)
        {
            if (foxJoyFrameSeconds != null &&
                index >= 0 &&
                index < foxJoyFrameSeconds.Length &&
                foxJoyFrameSeconds[index] > 0.001f)
            {
                return foxJoyFrameSeconds[index];
            }

            if (index >= 0 && index < DefaultFoxJoyFrameSeconds.Length)
            {
                return DefaultFoxJoyFrameSeconds[index];
            }

            return 2.1f / 24f;
        }

        private void PlaceFoxJoyAccessory(int frameIndex0Based)
        {
            if (foxJoyRenderer == null || shipVisual == null)
            {
                return;
            }

            Vector3 center = shipVisual.WorldCenter;
            Vector2 offset = foxJoyLocalOffset;
            offset.y += ResolveFoxJoyJumpLift(frameIndex0Based);
            foxJoyRenderer.transform.position = center + (Vector3)offset;
            foxJoyRenderer.transform.rotation = Quaternion.identity;
        }

        /// <summary>1始まりの枚目に対応するジャンプ上げ量。該当しなければ 0。</summary>
        private float ResolveFoxJoyJumpLift(int frameIndex0Based)
        {
            int frame1Based = frameIndex0Based + 1;
            if (frame1Based == foxJoyJumpFrameA)
            {
                return foxJoyJumpLiftA;
            }

            if (frame1Based == foxJoyJumpFrameB)
            {
                return foxJoyJumpLiftB;
            }

            return 0f;
        }

        private void ApplyFoxJoyScale(Sprite sprite)
        {
            if (foxJoyRenderer == null || sprite == null)
            {
                return;
            }

            Vector2 native = sprite.bounds.size;
            float scale = foxJoyDisplayHeight / Mathf.Max(0.0001f, native.y);
            foxJoyRenderer.transform.localScale = Vector3.one * scale;
        }

        private void HideFoxJoyAccessory()
        {
            if (foxJoyRenderer == null)
            {
                return;
            }

            foxJoyRenderer.enabled = false;
            foxJoyRenderer.sprite = null;
        }

        private void EnsureFoxJoyRenderer()
        {
            if (foxJoyRenderer != null)
            {
                ApplyFoxJoyDrawStyle();
                return;
            }

            // 以前船配下に作った場合は破棄してマウス帯へ移す
            Transform shipParent = shipVisual != null ? shipVisual.transform : transform;
            Transform stale = shipParent.Find("FoxRepairJoyAccessory");
            if (stale != null && stale.parent != ResolveMouseLayerRoot())
            {
                Object.Destroy(stale.gameObject);
            }

            Transform parent = ResolveMouseLayerRoot();
            Transform existing = parent.Find("FoxRepairJoyAccessory");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("FoxRepairJoyAccessory");
            if (existing == null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }

            foxJoyRenderer = go.GetComponent<SpriteRenderer>();
            if (foxJoyRenderer == null)
            {
                foxJoyRenderer = go.AddComponent<SpriteRenderer>();
            }

            if (go.GetComponent<KomayamaPreserveSpriteMaterial>() == null)
            {
                go.AddComponent<KomayamaPreserveSpriteMaterial>();
            }

            ApplyFoxJoyDrawStyle();
            foxJoyRenderer.enabled = false;
        }

        private Transform ResolveMouseLayerRoot()
        {
            GameObject mouseLayer = GameObject.Find("Layer_Mouse");
            if (mouseLayer != null)
            {
                return mouseLayer.transform;
            }

            if (foxFollower != null)
            {
                return foxFollower.transform.parent != null
                    ? foxFollower.transform.parent
                    : foxFollower.transform;
            }

            return transform;
        }

        private void ApplyFoxJoyDrawStyle()
        {
            if (foxJoyRenderer == null)
            {
                return;
            }

            // マウス狐と同じ帯：WorldMouse + Unlit（アウトラインなし）
            foxJoyRenderer.sortingLayerName = "WorldMouse";
            foxJoyRenderer.sortingOrder = -9;
            foxJoyRenderer.sharedMaterial = ResolveSpriteUnlitMaterial();
            foxJoyRenderer.color = Color.white;
        }

        private static Material sharedSpriteUnlit;
        private static Material sharedAdditiveUnlit;

        private static Material ResolveSpriteUnlitMaterial()
        {
            if (sharedSpriteUnlit != null)
            {
                return sharedSpriteUnlit;
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit == null)
            {
                return null;
            }

            sharedSpriteUnlit = new Material(unlit)
            {
                name = "Sprite-Unlit-Default (ShipRepairJoy)"
            };
            return sharedSpriteUnlit;
        }

        private Material ResolveCocoonGlowMaterial()
        {
            if (cocoonGlowMaterial != null)
            {
                return cocoonGlowMaterial;
            }

            if (sharedAdditiveUnlit != null)
            {
                return sharedAdditiveUnlit;
            }

#if UNITY_EDITOR
            sharedAdditiveUnlit = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/KomayamaCraft/SpriteAdditiveUnlit.mat");
            if (sharedAdditiveUnlit != null)
            {
                cocoonGlowMaterial = sharedAdditiveUnlit;
                return sharedAdditiveUnlit;
            }
#endif
            Shader shader = Shader.Find("KomayamaCraft/Sprite-Unlit-Additive");
            if (shader == null)
            {
                return null;
            }

            sharedAdditiveUnlit = new Material(shader)
            {
                name = "Sprite-Unlit-Additive (Runtime)"
            };
            return sharedAdditiveUnlit;
        }

        private IEnumerator PlayThumps()
        {
            Sprite sprite = shipVisual.CurrentSprite;
            if (thumpRenderer == null || sprite == null)
            {
                yield break;
            }

            thumpRenderer.sprite = sprite;
            thumpRenderer.enabled = true;
            for (int i = 0; i < thumpCount; i++)
            {
                if (skipRequested)
                {
                    yield break;
                }

                float elapsed = 0f;
                float duration = Mathf.Max(0.05f, thumpSeconds);
                while (elapsed < duration)
                {
                    if (skipRequested)
                    {
                        yield break;
                    }

                    UpdateSkipHold();
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float scale = Mathf.Lerp(1f, thumpPeakScale, t);
                    float alpha = 1f - t;
                    thumpRenderer.transform.localScale = Vector3.one * scale;
                    Color c = Color.white;
                    c.a = alpha;
                    thumpRenderer.color = c;
                    yield return null;
                }

                thumpRenderer.color = new Color(1f, 1f, 1f, 0f);
                thumpRenderer.transform.localScale = Vector3.one;

                float pause = Mathf.Max(0f, thumpPauseSeconds);
                float pauseElapsed = 0f;
                while (pauseElapsed < pause)
                {
                    if (skipRequested)
                    {
                        yield break;
                    }

                    UpdateSkipHold();
                    pauseElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            thumpRenderer.enabled = false;
        }

        private IEnumerator PlayCocoonThenWhiten()
        {
            EnsureCocoonGlowRenderers();

            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = true;
                cocoonRenderer.color = Color.white;
                cocoonRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, cocoonScale);
            }

            SetCocoonGlowVisible(false, 0f);

            int frameCount = cocoonFrames != null ? cocoonFrames.Length : 0;
            float frameDur = Mathf.Max(0.05f, cocoonFrameSeconds);

            // 1) CLOSE
            if (frameCount > 0 && cocoonRenderer != null)
            {
                yield return PlayCocoonFrames(0, frameCount - 1, frameDur);
                if (skipRequested)
                {
                    yield break;
                }
            }

            // 2) ship_2 差し替え → 0.6 秒かけて発光
            FinishToShip2();
            EnsureCocoonGlowRenderers();
            SyncCocoonGlowFromCocoon();
            yield return FadeCocoonGlow(0f, 1f, cocoonGlowInSeconds);
            if (skipRequested)
            {
                yield break;
            }

            // 3) 光ったまま 0.4 秒静止してから OPEN
            yield return WaitUnscaled(cocoonGlowHoldBeforeOpenSeconds);
            if (skipRequested)
            {
                yield break;
            }

            // 4) 光ったまま OPEN（グローも同コマ追従）
            // 色付き繭は隠し、白い加算レイヤだけ見せる
            if (cocoonRenderer != null)
            {
                cocoonRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            if (frameCount > 1 && cocoonRenderer != null)
            {
                yield return PlayCocoonFrames(frameCount - 2, 0, frameDur);
                if (skipRequested)
                {
                    yield break;
                }
            }

            // 5) 発光アウトの前に色付き繭を完全に消す（α戻しで端が残らないように）
            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = false;
                cocoonRenderer.color = Color.white;
            }

            yield return FadeCocoonGlow(1f, 0f, cocoonGlowOutSeconds);
            if (skipRequested)
            {
                yield break;
            }

            SetCocoonGlowVisible(false, 0f);
            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = false;
                cocoonRenderer.color = Color.white;
                cocoonRenderer.transform.localScale = Vector3.one;
            }

            shipVisual.SetSpriteColor(Color.white);

            // 6) 解除後静止
            yield return WaitUnscaled(afterClearHoldSeconds);
        }

        private IEnumerator FadeCocoonGlow(float from01, float to01, float seconds)
        {
            EnsureCocoonGlowRenderers();
            SyncCocoonGlowFromCocoon();
            SetCocoonGlowVisible(true, from01);

            float dur = Mathf.Max(0.05f, seconds);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (skipRequested)
                {
                    yield break;
                }

                UpdateSkipHold();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float w = Mathf.Lerp(from01, to01, t);
                ApplyCocoonGlowWeight(w);
                yield return null;
            }

            ApplyCocoonGlowWeight(to01);
        }

        private void ApplyCocoonGlowWeight(float weight01)
        {
            float w = Mathf.Clamp01(weight01);
            if (cocoonGlowInner != null)
            {
                // 加算なので α=強度。白固定（テクスチャ色はシェーダ側で捨てる）
                Color c = Color.white;
                c.a = Mathf.Clamp01(cocoonGlowIntensityInner) * w;
                cocoonGlowInner.color = c;
            }

            if (cocoonGlowOuter != null)
            {
                Color c = Color.white;
                c.a = useCocoonGlowOuter
                    ? Mathf.Clamp01(cocoonGlowIntensityOuter) * w
                    : 0f;
                cocoonGlowOuter.color = c;
            }

            // 発光イン時だけ色付き繭を徐々に消す。アウト時は戻さない（端が残るのを防ぐ）
            if (cocoonRenderer != null && cocoonRenderer.enabled)
            {
                Color body = cocoonRenderer.color;
                body.r = 1f;
                body.g = 1f;
                body.b = 1f;
                body.a = Mathf.Min(body.a, 1f - w);
                cocoonRenderer.color = body;
            }
        }

        private void SetCocoonGlowVisible(bool visible, float weight01)
        {
            if (cocoonGlowInner != null)
            {
                cocoonGlowInner.enabled = visible;
            }

            if (cocoonGlowOuter != null)
            {
                cocoonGlowOuter.enabled = visible && useCocoonGlowOuter;
            }

            ApplyCocoonGlowWeight(weight01);
        }

        private void SyncCocoonGlowFromCocoon()
        {
            if (cocoonRenderer == null)
            {
                return;
            }

            Sprite sprite = cocoonRenderer.sprite;
            Vector3 baseScale = Vector3.one * Mathf.Max(0.1f, cocoonScale);
            if (cocoonGlowInner != null)
            {
                cocoonGlowInner.sprite = sprite;
                cocoonGlowInner.transform.localPosition = cocoonRenderer.transform.localPosition;
                cocoonGlowInner.transform.localRotation = cocoonRenderer.transform.localRotation;
                cocoonGlowInner.transform.localScale = baseScale * Mathf.Max(1f, cocoonGlowScaleInner);
            }

            if (cocoonGlowOuter != null)
            {
                cocoonGlowOuter.sprite = sprite;
                cocoonGlowOuter.transform.localPosition = cocoonRenderer.transform.localPosition;
                cocoonGlowOuter.transform.localRotation = cocoonRenderer.transform.localRotation;
                cocoonGlowOuter.transform.localScale = baseScale * Mathf.Max(1f, cocoonGlowScaleOuter);
            }
        }

        /// <summary>
        /// 繭コマを fromIndex→toIndex（含む）へ順送り／逆送りする。
        /// </summary>
        private IEnumerator PlayCocoonFrames(int fromIndex, int toIndex, float frameDur)
        {
            if (cocoonRenderer == null || cocoonFrames == null || cocoonFrames.Length == 0)
            {
                yield break;
            }

            int step = toIndex >= fromIndex ? 1 : -1;
            for (int i = fromIndex; ; i += step)
            {
                if (skipRequested)
                {
                    yield break;
                }

                if (i >= 0 && i < cocoonFrames.Length && cocoonFrames[i] != null)
                {
                    cocoonRenderer.sprite = cocoonFrames[i];
                    SyncCocoonGlowFromCocoon();
                }

                float frameElapsed = 0f;
                while (frameElapsed < frameDur)
                {
                    if (skipRequested)
                    {
                        yield break;
                    }

                    UpdateSkipHold();
                    frameElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (i == toIndex)
                {
                    yield break;
                }
            }
        }

        private void FinishToShip2()
        {
            shipVisual.SetStage(KomayamaShipVisual.StageAfterRainLeak, force: true);
            shipVisual.SetSpriteColor(Color.white);

            if (thumpRenderer != null)
            {
                thumpRenderer.enabled = false;
            }
        }

        private void CleanupOverlaysVisual()
        {
            if (thumpRenderer != null)
            {
                thumpRenderer.enabled = false;
                thumpRenderer.transform.localScale = Vector3.one;
            }

            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = false;
                cocoonRenderer.color = new Color(1f, 1f, 1f, 0f);
                cocoonRenderer.transform.localScale = Vector3.one;
            }

            SetCocoonGlowVisible(false, 0f);
            if (cocoonGlowInner != null)
            {
                cocoonGlowInner.transform.localScale = Vector3.one;
            }

            if (cocoonGlowOuter != null)
            {
                cocoonGlowOuter.transform.localScale = Vector3.one;
            }

            HideFoxJoyAccessory();

            if (shipVisual != null)
            {
                shipVisual.SetSpriteColor(Color.white);
            }
        }

        private void UpdateSkipHold()
        {
            bool held = Mouse.current != null &&
                        Mouse.current.leftButton.isPressed;
            if (!held)
            {
                skipHoldElapsed = 0f;
                SetSkipIndicator(0f, false);
                return;
            }

            skipHoldElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(skipHoldElapsed / Mathf.Max(0.1f, skipHoldSeconds));
            SetSkipIndicator(t, true);
            if (t >= 1f)
            {
                skipRequested = true;
            }
        }

        private void SetSkipIndicator(float fill01, bool visible)
        {
            if (skipIndicatorRoot != null)
            {
                if (skipIndicatorRoot.gameObject.activeSelf != visible)
                {
                    skipIndicatorRoot.gameObject.SetActive(visible);
                }

                if (visible && targetCamera != null && Mouse.current != null)
                {
                    Vector2 screen = Mouse.current.position.ReadValue();
                    Canvas canvas = skipIndicatorRoot.GetComponentInParent<Canvas>();
                    Camera eventCam = null;
                    if (canvas != null &&
                        canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        eventCam = canvas.worldCamera != null
                            ? canvas.worldCamera
                            : targetCamera;
                    }

                    RectTransform parent = skipIndicatorRoot.parent as RectTransform;
                    if (parent != null &&
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            parent,
                            screen,
                            eventCam,
                            out Vector2 local))
                    {
                        skipIndicatorRoot.anchoredPosition = local;
                    }
                }
            }

            if (skipIndicatorFill != null)
            {
                skipIndicatorFill.fillAmount = Mathf.Clamp01(fill01);
            }
        }

        private void EnsureOverlayRenderers()
        {
            SpriteRenderer baseSr = shipVisual.SpriteRenderer;
            if (baseSr == null)
            {
                return;
            }

            if (thumpRenderer == null)
            {
                thumpRenderer = CreateOverlay("ThumpOverlay", baseSr.sortingOrder + 1);
            }

            if (cocoonRenderer == null)
            {
                cocoonRenderer = CreateOverlay("CocoonOverlay", baseSr.sortingOrder + 2);
            }

            EnsureCocoonGlowRenderers();

            thumpRenderer.transform.localPosition = Vector3.zero;
            cocoonRenderer.transform.localPosition = Vector3.zero;
            // エフェクトはアウトラインなし Unlit（船の Outline 材を流用しない）
            Material unlit = ResolveSpriteUnlitMaterial();
            if (unlit != null)
            {
                thumpRenderer.sharedMaterial = unlit;
                cocoonRenderer.sharedMaterial = unlit;
            }
            else
            {
                thumpRenderer.sharedMaterial = baseSr.sharedMaterial;
                cocoonRenderer.sharedMaterial = baseSr.sharedMaterial;
            }
        }

        private void EnsureCocoonGlowRenderers()
        {
            SpriteRenderer baseSr = shipVisual != null ? shipVisual.SpriteRenderer : null;
            if (baseSr == null)
            {
                return;
            }

            // 旧・白塗りオーバーレイが残っていれば無効化
            Transform parent = baseSr.transform;
            Transform legacyFlash = parent.Find("CocoonWhiteFlashOverlay");
            if (legacyFlash != null)
            {
                SpriteRenderer legacySr = legacyFlash.GetComponent<SpriteRenderer>();
                if (legacySr != null)
                {
                    legacySr.enabled = false;
                }
            }

            if (cocoonGlowInner == null)
            {
                cocoonGlowInner = CreateOverlay(
                    "CocoonGlowInner",
                    baseSr.sortingOrder + 3);
            }

            if (cocoonGlowOuter == null)
            {
                cocoonGlowOuter = CreateOverlay(
                    "CocoonGlowOuter",
                    baseSr.sortingOrder + 4);
            }

            Material glowMat = ResolveCocoonGlowMaterial();
            // αブレンド Unlit へ落とすと暗い繭が黒半透明に見えるので、加算材以外は付けない
            if (glowMat == null)
            {
                cocoonGlowInner.enabled = false;
                cocoonGlowOuter.enabled = false;
                return;
            }

            cocoonGlowInner.sharedMaterial = glowMat;
            cocoonGlowOuter.sharedMaterial = glowMat;
            cocoonGlowInner.transform.localPosition = Vector3.zero;
            cocoonGlowOuter.transform.localPosition = Vector3.zero;
            cocoonGlowInner.color = new Color(1f, 1f, 1f, 0f);
            cocoonGlowOuter.color = new Color(1f, 1f, 1f, 0f);
            cocoonGlowInner.enabled = false;
            cocoonGlowOuter.enabled = false;
        }

        private SpriteRenderer CreateOverlay(string name, int sortingOrder)
        {
            Transform parent = shipVisual.SpriteRenderer != null
                ? shipVisual.SpriteRenderer.transform
                : transform;
            Transform existing = parent.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(name);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
            }

            // DrawOrder の Outline／Unlit 強制から守る
            if (go.GetComponent<KomayamaPreserveSpriteMaterial>() == null)
            {
                go.AddComponent<KomayamaPreserveSpriteMaterial>();
            }

            sr.sortingLayerID = shipVisual.SpriteRenderer.sortingLayerID;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            return sr;
        }
    }
}
