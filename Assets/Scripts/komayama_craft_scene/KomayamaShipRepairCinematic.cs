using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
        [SerializeField, Min(0f), InspectorName("閉じたあと静止秒")]
        private float cocoonClosedHoldSeconds = 0.4f;

        [Header("白飛び")]
        [SerializeField, Min(0.05f), InspectorName("白飛びイン秒")]
        [FormerlySerializedAs("whitenSeconds")]
        private float whitenInSeconds = 1.4f;
        [SerializeField, Min(0.05f), InspectorName("白飛び解除秒")]
        private float whitenOutSeconds = 1.4f;
        [SerializeField, Min(0f), InspectorName("解除後の静止秒")]
        private float afterClearHoldSeconds = 0.8f;

        [Header("スキップ")]
        [SerializeField, Min(0.1f), InspectorName("長押しスキップ秒")]
        private float skipHoldSeconds = 1.2f;

        [Header("喜びモーション（ship_2 直後・ズーム中）")]
        [SerializeField] private KCMouseFoxFollower foxFollower;
        [SerializeField] private KomayamaCraftBgmManager bgmManager;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField, Tooltip("繭の真っ白シルエット用。未設定ならシェーダから生成。")]
        private Material cocoonWhiteFlashMaterial;
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
        private SpriteRenderer cocoonFlashRenderer;
        private SpriteRenderer foxJoyRenderer;
        private Color baseColor = Color.white;
        private KCFoxDisplayMode foxModeBeforeJoy = KCFoxDisplayMode.FollowMouse;
        private bool foxModeCaptured;

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

            KomayamaCraftAutoPlayController autoPlay =
                FindFirstObjectByType<KomayamaCraftAutoPlayController>();
            if (autoPlay != null && autoPlay.IsRunning)
            {
                ForceSkip();
            }

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
                RestoreFoxFollowerAfterJoy();
                HideFoxJoyAccessory();
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

            RestoreFoxFollowerAfterJoy();
            HideFoxJoyAccessory();
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

            // マウス狐を隠し、演出前の表示モードを覚える
            if (foxFollower != null)
            {
                foxModeBeforeJoy = foxFollower.DisplayMode;
                foxModeCaptured = true;
                foxFollower.SetDisplayMode(KCFoxDisplayMode.Off);
            }

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

            HideFoxJoyAccessory();
            RestoreFoxFollowerAfterJoy();

            if (playCelebrationAudio && bgmManager != null)
            {
                yield return bgmManager.ResumeGameplayRoutine();
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

        private void RestoreFoxFollowerAfterJoy()
        {
            if (!foxModeCaptured || foxFollower == null)
            {
                return;
            }

            foxFollower.SetDisplayMode(foxModeBeforeJoy);
            foxModeCaptured = false;
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
        private static Material sharedWhiteFlash;

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
            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = true;
                cocoonRenderer.color = Color.white;
                cocoonRenderer.transform.localScale = Vector3.one * Mathf.Max(0.1f, cocoonScale);
            }

            // 白飛びシェーダは使わない
            if (cocoonFlashRenderer != null)
            {
                cocoonFlashRenderer.enabled = false;
            }

            int frameCount = cocoonFrames != null ? cocoonFrames.Length : 0;
            float frameDur = Mathf.Max(0.05f, cocoonFrameSeconds);

            // CLOSE：繭を閉じる
            if (frameCount > 0 && cocoonRenderer != null)
            {
                yield return PlayCocoonFrames(0, frameCount - 1, frameDur);
                if (skipRequested)
                {
                    yield break;
                }
            }

            FinishToShip2();

            // 閉じ切ったあと静止してから OPEN
            yield return WaitUnscaled(cocoonClosedHoldSeconds);
            if (skipRequested)
            {
                yield break;
            }

            // OPEN：繭を逆再生して開く
            if (frameCount > 1 && cocoonRenderer != null)
            {
                cocoonRenderer.enabled = true;
                cocoonRenderer.color = Color.white;
                yield return PlayCocoonFrames(frameCount - 2, 0, frameDur);
                if (skipRequested)
                {
                    yield break;
                }
            }

            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = false;
                cocoonRenderer.color = Color.white;
                cocoonRenderer.transform.localScale = Vector3.one;
            }

            shipVisual.SetSpriteColor(Color.white);

            float hold = Mathf.Max(0f, afterClearHoldSeconds);
            float holdElapsed = 0f;
            while (holdElapsed < hold)
            {
                if (skipRequested)
                {
                    yield break;
                }

                UpdateSkipHold();
                holdElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 白フラッシュ側だけコマ送り（OPEN 用）。
        /// </summary>
        private IEnumerator PlayCocoonFlashFrames(int fromIndex, int toIndex, float frameDur)
        {
            if (cocoonFlashRenderer == null ||
                cocoonFrames == null ||
                cocoonFrames.Length == 0)
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
                    cocoonFlashRenderer.sprite = cocoonFrames[i];
                    if (cocoonRenderer != null)
                    {
                        cocoonFlashRenderer.transform.localPosition =
                            cocoonRenderer.transform.localPosition;
                        cocoonFlashRenderer.transform.localScale =
                            cocoonRenderer.transform.localScale;
                        cocoonFlashRenderer.transform.localRotation =
                            cocoonRenderer.transform.localRotation;
                    }
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

        /// <summary>
        /// 閉じた繭の形状を真っ白シルエットへ飛ばす（RGB 無視・α 形状のみ）。
        /// </summary>
        private IEnumerator WhitenCocoon(float seconds)
        {
            EnsureCocoonFlashRenderer();
            SyncCocoonFlashSprite();
            if (cocoonFlashRenderer != null)
            {
                cocoonFlashRenderer.enabled = true;
                cocoonFlashRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

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
                if (cocoonFlashRenderer != null)
                {
                    Color f = Color.white;
                    f.a = t;
                    cocoonFlashRenderer.color = f;
                }

                // 色付き繭は白に埋もれさせる（黒く残らないよう α も下げる）
                if (cocoonRenderer != null)
                {
                    Color c = Color.white;
                    c.a = 1f - t;
                    cocoonRenderer.color = c;
                }

                shipVisual.SetSpriteColor(Color.Lerp(baseColor, Color.white, t));
                yield return null;
            }

            if (cocoonFlashRenderer != null)
            {
                cocoonFlashRenderer.color = Color.white;
            }

            if (cocoonRenderer != null)
            {
                cocoonRenderer.enabled = false;
                cocoonRenderer.color = Color.white;
            }

            shipVisual.SetSpriteColor(Color.white);
        }

        private void SyncCocoonFlashSprite()
        {
            if (cocoonFlashRenderer == null || cocoonRenderer == null)
            {
                return;
            }

            cocoonFlashRenderer.sprite = cocoonRenderer.sprite;
            cocoonFlashRenderer.transform.localPosition = cocoonRenderer.transform.localPosition;
            cocoonFlashRenderer.transform.localScale = cocoonRenderer.transform.localScale;
            cocoonFlashRenderer.transform.localRotation = cocoonRenderer.transform.localRotation;
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

            if (cocoonFlashRenderer != null)
            {
                cocoonFlashRenderer.enabled = false;
                cocoonFlashRenderer.color = new Color(1f, 1f, 1f, 0f);
                cocoonFlashRenderer.transform.localScale = Vector3.one;
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

            EnsureCocoonFlashRenderer();

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

        private void EnsureCocoonFlashRenderer()
        {
            SpriteRenderer baseSr = shipVisual != null ? shipVisual.SpriteRenderer : null;
            if (baseSr == null)
            {
                return;
            }

            if (cocoonFlashRenderer == null)
            {
                cocoonFlashRenderer = CreateOverlay(
                    "CocoonWhiteFlashOverlay",
                    baseSr.sortingOrder + 3);
            }

            cocoonFlashRenderer.transform.localPosition = Vector3.zero;
            Material flashMat = ResolveWhiteFlashMaterialInstance();
            if (flashMat == null)
            {
                flashMat = ResolveSpriteUnlitMaterial();
            }

            cocoonFlashRenderer.sharedMaterial = flashMat != null
                ? flashMat
                : baseSr.sharedMaterial;

            if (cocoonRenderer != null)
            {
                cocoonFlashRenderer.transform.localScale = cocoonRenderer.transform.localScale;
            }
        }

        private Material ResolveWhiteFlashMaterialInstance()
        {
            if (cocoonWhiteFlashMaterial != null)
            {
                return cocoonWhiteFlashMaterial;
            }

            if (sharedWhiteFlash != null)
            {
                return sharedWhiteFlash;
            }

#if UNITY_EDITOR
            sharedWhiteFlash = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/KomayamaCraft/SpriteWhiteFlash.mat");
            if (sharedWhiteFlash != null)
            {
                cocoonWhiteFlashMaterial = sharedWhiteFlash;
                return sharedWhiteFlash;
            }
#endif
            Shader shader = Shader.Find("KomayamaCraft/Sprite-WhiteFlash");
            if (shader == null)
            {
                return null;
            }

            sharedWhiteFlash = new Material(shader)
            {
                name = "SpriteWhiteFlash (Runtime)"
            };
            return sharedWhiteFlash;
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

            sr.sortingLayerID = shipVisual.SpriteRenderer.sortingLayerID;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            return sr;
        }
    }
}
