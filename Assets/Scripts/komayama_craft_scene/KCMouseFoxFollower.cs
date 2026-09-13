using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    public enum KCFoxDisplayMode
    {
        FollowMouse = 0,
        ScreenFixed = 1,
        Off = 2
    }

    /// <summary>
    /// カーソル追従／画面配置の狐。優先度付きアニメを1本だけ再生する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KCMouseFoxFollower : MonoBehaviour
    {
        public const string ClipBase = "base";
        public const string ClipDozeEnter = "doze_enter";
        public const string ClipDozeLoop = "doze_loop";
        public const string ClipDangle = "dangle";
        public const string ClipPickPlace = "pickplace";
        public const string ClipGatherSword = "gather_sword";
        public const string ClipSpinTest = "spin_test";

        [Serializable]
        public sealed class ClipDefinition
        {
            public string id = string.Empty;
            [Range(1, 5)] public int priority = 1;
            public Sprite[] frames = Array.Empty<Sprite>();
            public float[] secondsPerFrame = Array.Empty<float>();
            public bool pingPong;
            public bool loop = true;
            [Min(1)] public int playCount = 1;
            public string nextClipId = string.Empty;
        }

        [Header("参照")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("表示モード")]
        [SerializeField] private KCFoxDisplayMode displayMode = KCFoxDisplayMode.FollowMouse;
        [SerializeField]
        [Tooltip("カーソルからのオフセット。左へ離すなら X を負に。")]
        private Vector2 cursorOffset = new(-1.1f, -0.15f);
        [SerializeField, Min(0.01f)] private float followSpeed = 12f;
        [SerializeField, Min(0.01f)] private float displayHeight = 2.4f;
        [SerializeField, Min(0.01f)] private float minDisplayHeight = 0.5f;
        [SerializeField, Min(0.01f)] private float maxDisplayHeight = 4f;
        [SerializeField, Min(0.01f)] private float wheelHeightStep = 0.15f;
        [SerializeField] private Vector2 screenViewportPosition = new(0.2f, 0.5f);

        [Header("うたたね")]
        [SerializeField, Min(0f)] private float dozeIdleBaseSeconds = 12f;
        [SerializeField, Min(0f)] private float dozeIdleRandomSeconds = 8f;

        [Header("テスト：優先度5キック")]
        [SerializeField] private bool enableSpinTestKick = true;
        [SerializeField, Min(0f)] private float spinTestDelaySeconds = 6f;

        [Header("アニメ定義")]
        [SerializeField] private ClipDefinition[] clips = Array.Empty<ClipDefinition>();

        private Vector3 followVelocity;
        private bool hasFollowPosition;
        private float frameTimer;
        private int frameIndex;
        private int frameDirection = 1;
        private int cyclesCompleted;
        private ClipDefinition activeClip;
        private string activeClipId = ClipBase;

        private bool cameraMoving;
        private bool rightDropHolding;
        private bool gatherHoldActive;
        private float gatherHoldIntervalSeconds = 0.5f;
        private float idleSeconds;
        private float dozeThresholdSeconds;
        private Vector2 lastMouseScreen;
        private bool hasLastMouse;
        private bool spinTestFired;
        private float playElapsed;
        private bool draggingScreenFox;
        private Vector2 dragScreenOffset;

        public KCFoxDisplayMode DisplayMode => displayMode;

        public bool IsVisible =>
            displayMode != KCFoxDisplayMode.Off &&
            spriteRenderer != null &&
            spriteRenderer.enabled;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            EnsureBaseClipExists();
            ResetDozeThreshold();
            PlayClip(ClipBase, force: true);
            ApplyVisibility();
            ApplyDisplaySize();
        }

        private void OnValidate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            minDisplayHeight = Mathf.Max(0.01f, minDisplayHeight);
            maxDisplayHeight = Mathf.Max(minDisplayHeight, maxDisplayHeight);
            displayHeight = Mathf.Clamp(displayHeight, minDisplayHeight, maxDisplayHeight);
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    SyncClipDurations(clips[i]);
                }
            }

            ApplyVisibility();
            ApplyDisplaySize();
        }

        private void Update()
        {
            if (displayMode == KCFoxDisplayMode.Off)
            {
                return;
            }

            playElapsed += Time.unscaledDeltaTime;
            TrackUserActivity();
            EvaluateTriggers();
            TickAnimation();
            if (enableSpinTestKick &&
                !spinTestFired &&
                playElapsed >= spinTestDelaySeconds)
            {
                spinTestFired = true;
                RequestSystemClip(ClipSpinTest);
            }
        }

        private void LateUpdate()
        {
            if (displayMode == KCFoxDisplayMode.Off)
            {
                return;
            }

            if (displayMode == KCFoxDisplayMode.FollowMouse)
            {
                FollowCursor();
            }
            else if (displayMode == KCFoxDisplayMode.ScreenFixed)
            {
                ApplyScreenFixedPosition();
            }
        }

        public void SetDisplayMode(KCFoxDisplayMode mode)
        {
            displayMode = mode;
            hasFollowPosition = false;
            draggingScreenFox = false;
            ApplyVisibility();
        }

        public void NotifyCameraMoving(bool moving)
        {
            if (cameraMoving == moving)
            {
                return;
            }

            cameraMoving = moving;
            if (moving)
            {
                NotifyUserActivity();
                TryBeginClip(ClipDangle);
            }
            else if (IsPlaying(ClipDangle))
            {
                EndActiveClip();
            }
        }

        public void NotifyRightDropHolding(bool holding)
        {
            if (rightDropHolding == holding)
            {
                return;
            }

            rightDropHolding = holding;
            if (holding)
            {
                NotifyUserActivity();
                TryBeginClip(ClipPickPlace);
            }
            else if (IsPlaying(ClipPickPlace))
            {
                EndActiveClip();
            }
        }

        public void NotifyValidGatherStarted()
        {
            NotifyUserActivity();
            // 短押し：クリップに設定したコマ秒で1回再生。
            if (!gatherHoldActive)
            {
                TryBeginClip(ClipGatherSword);
            }
        }

        /// <summary>
        /// 採集の長押し中。intervalSeconds は長押し時の採集間隔。
        /// 押しているあいだ、その間隔をコマ数で等分した秒でループ再生する。
        /// </summary>
        public void NotifyGatherHolding(bool holding, float intervalSeconds)
        {
            float interval = Mathf.Max(0.02f, intervalSeconds);
            if (holding)
            {
                NotifyUserActivity();
                bool enteredHold = !gatherHoldActive;
                gatherHoldActive = true;
                gatherHoldIntervalSeconds = interval;
                if (enteredHold || !IsPlaying(ClipGatherSword))
                {
                    PlayClip(ClipGatherSword, force: true);
                }

                return;
            }

            if (!gatherHoldActive)
            {
                return;
            }

            gatherHoldActive = false;
            if (IsPlaying(ClipGatherSword))
            {
                EndActiveClip();
            }
        }

        public void RequestSystemClip(string clipId)
        {
            if (string.IsNullOrEmpty(clipId))
            {
                return;
            }

            TryBeginClip(clipId);
        }

        public void NotifyUserActivity()
        {
            idleSeconds = 0f;
            ResetDozeThreshold();
            if (IsPlaying(ClipDozeEnter) || IsPlaying(ClipDozeLoop))
            {
                EndActiveClip();
            }
        }

        /// <summary>
        /// 画面配置モードで狐がポインタ操作を占有するとき true。
        /// </summary>
        public bool TryConsumePointerInteraction()
        {
            if (displayMode != KCFoxDisplayMode.ScreenFixed ||
                targetCamera == null ||
                Mouse.current == null)
            {
                draggingScreenFox = false;
                return false;
            }

            Vector2 screen = Mouse.current.position.ReadValue();
            bool overFox = IsScreenPointOverFox(screen);
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (overFox && Mathf.Abs(scroll) >= 0.01f)
            {
                const float WheelNotch = 120f;
                float notches = Mathf.Abs(scroll) >= WheelNotch * 0.5f
                    ? scroll / WheelNotch
                    : Mathf.Sign(scroll);
                displayHeight = Mathf.Clamp(
                    displayHeight + notches * wheelHeightStep,
                    minDisplayHeight,
                    maxDisplayHeight);
                ApplyDisplaySize();
                NotifyUserActivity();
                return true;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && overFox)
            {
                draggingScreenFox = true;
                Vector2 foxScreen = WorldToScreen(transform.position);
                dragScreenOffset = foxScreen - screen;
                NotifyUserActivity();
                return true;
            }

            if (draggingScreenFox && Mouse.current.leftButton.isPressed)
            {
                Vector2 targetScreen = screen + dragScreenOffset;
                screenViewportPosition = new Vector2(
                    Mathf.Clamp01(targetScreen.x / Mathf.Max(1f, targetCamera.pixelWidth)),
                    Mathf.Clamp01(targetScreen.y / Mathf.Max(1f, targetCamera.pixelHeight)));
                ApplyScreenFixedPosition();
                NotifyUserActivity();
                return true;
            }

            if (!Mouse.current.leftButton.isPressed)
            {
                draggingScreenFox = false;
            }

            return draggingScreenFox;
        }

        public void CaptureSave(FoxFollowerSaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            dto.displayMode = (int)displayMode;
            dto.screenViewportX = screenViewportPosition.x;
            dto.screenViewportY = screenViewportPosition.y;
            dto.displayHeight = displayHeight;
        }

        public void ApplySave(FoxFollowerSaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            displayMode = (KCFoxDisplayMode)Mathf.Clamp(dto.displayMode, 0, 2);
            screenViewportPosition = new Vector2(
                Mathf.Clamp01(dto.screenViewportX),
                Mathf.Clamp01(dto.screenViewportY));
            if (dto.displayHeight > 0f)
            {
                displayHeight = Mathf.Clamp(
                    dto.displayHeight,
                    minDisplayHeight,
                    maxDisplayHeight);
            }

            hasFollowPosition = false;
            draggingScreenFox = false;
            ApplyVisibility();
            ApplyDisplaySize();
            if (displayMode == KCFoxDisplayMode.ScreenFixed)
            {
                ApplyScreenFixedPosition();
            }
        }

        private void TrackUserActivity()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool activity = false;

            if (mouse != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                if (hasLastMouse)
                {
                    if ((screen - lastMouseScreen).sqrMagnitude > 1f)
                    {
                        activity = true;
                    }
                }

                lastMouseScreen = screen;
                hasLastMouse = true;
                if (mouse.leftButton.isPressed ||
                    mouse.rightButton.isPressed ||
                    mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame)
                {
                    activity = true;
                }
            }

            if (keyboard != null && keyboard.anyKey.isPressed)
            {
                activity = true;
            }

            if (cameraMoving || rightDropHolding)
            {
                activity = true;
            }

            if (activity)
            {
                NotifyUserActivity();
            }
            else
            {
                idleSeconds += Time.unscaledDeltaTime;
            }
        }

        private void EvaluateTriggers()
        {
            if (cameraMoving)
            {
                TryBeginClip(ClipDangle);
            }

            if (rightDropHolding)
            {
                TryBeginClip(ClipPickPlace);
            }

            if (!cameraMoving &&
                !rightDropHolding &&
                idleSeconds >= dozeThresholdSeconds &&
                !IsPlaying(ClipDozeEnter) &&
                !IsPlaying(ClipDozeLoop) &&
                (activeClip == null || activeClip.priority <= 2))
            {
                TryBeginClip(ClipDozeEnter);
            }
        }

        private void TryBeginClip(string clipId)
        {
            ClipDefinition clip = FindClip(clipId);
            if (clip == null || clip.frames == null || clip.frames.Length == 0)
            {
                return;
            }

            if (activeClip != null)
            {
                if (clip.priority < activeClip.priority)
                {
                    return;
                }

                if (clip.priority == activeClip.priority &&
                    string.Equals(activeClipId, clipId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            PlayClip(clipId, force: true);
        }

        private void PlayClip(string clipId, bool force)
        {
            ClipDefinition clip = FindClip(clipId);
            if (clip == null)
            {
                clip = FindClip(ClipBase);
                clipId = ClipBase;
            }

            if (clip == null)
            {
                activeClip = null;
                activeClipId = string.Empty;
                return;
            }

            if (!force &&
                string.Equals(activeClipId, clipId, StringComparison.Ordinal))
            {
                return;
            }

            SyncClipDurations(clip);
            activeClip = clip;
            activeClipId = clipId;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            cyclesCompleted = 0;
            ApplyFrame(0);
        }

        private void EndActiveClip()
        {
            PlayClip(ClipBase, force: true);
        }

        private void TickAnimation()
        {
            if (activeClip == null ||
                activeClip.frames == null ||
                activeClip.frames.Length == 0 ||
                spriteRenderer == null)
            {
                return;
            }

            frameTimer += Time.unscaledDeltaTime;
            float step = CurrentFrameSeconds();
            while (frameTimer >= step)
            {
                frameTimer -= step;
                if (!AdvanceFrame())
                {
                    break;
                }

                step = CurrentFrameSeconds();
                if (step <= 0f)
                {
                    break;
                }
            }

            ApplyFrame(frameIndex);
        }

        private bool AdvanceFrame()
        {
            Sprite[] frames = activeClip.frames;
            if (frames.Length <= 1)
            {
                frameIndex = 0;
                return CompleteCycleIfNeeded();
            }

            if (!activeClip.pingPong)
            {
                frameIndex++;
                if (frameIndex >= frames.Length)
                {
                    frameIndex = 0;
                    return CompleteCycleIfNeeded();
                }

                return true;
            }

            frameIndex += frameDirection;
            if (frameIndex >= frames.Length - 1)
            {
                frameIndex = frames.Length - 1;
                frameDirection = -1;
                return true;
            }

            if (frameIndex <= 0)
            {
                frameIndex = 0;
                frameDirection = 1;
                // 往復で戻ってきたときを1サイクルとする。
                return CompleteCycleIfNeeded();
            }

            return true;
        }

        private bool CompleteCycleIfNeeded()
        {
            // 採集長押し中は間隔同期でループし続ける。
            if (gatherHoldActive && IsPlaying(ClipGatherSword))
            {
                return true;
            }

            if (activeClip.loop)
            {
                return true;
            }

            cyclesCompleted++;
            if (cyclesCompleted < Mathf.Max(1, activeClip.playCount))
            {
                return true;
            }

            string next = activeClip.nextClipId;
            if (string.IsNullOrWhiteSpace(next))
            {
                PlayClip(ClipBase, force: true);
            }
            else
            {
                PlayClip(next, force: true);
            }

            return false;
        }

        private float CurrentFrameSeconds()
        {
            if (gatherHoldActive &&
                IsPlaying(ClipGatherSword) &&
                activeClip != null &&
                activeClip.frames != null &&
                activeClip.frames.Length > 0)
            {
                return Mathf.Max(
                    0.02f,
                    gatherHoldIntervalSeconds / activeClip.frames.Length);
            }

            if (activeClip == null ||
                activeClip.secondsPerFrame == null ||
                activeClip.secondsPerFrame.Length == 0)
            {
                return 0.1f;
            }

            int index = Mathf.Clamp(frameIndex, 0, activeClip.secondsPerFrame.Length - 1);
            return Mathf.Max(0.02f, activeClip.secondsPerFrame[index]);
        }

        private void FollowCursor()
        {
            if (targetCamera == null || Mouse.current == null)
            {
                return;
            }

            Vector2 screen = Mouse.current.position.ReadValue();
            if (!targetCamera.pixelRect.Contains(screen))
            {
                return;
            }

            Vector3 target = targetCamera.ScreenToWorldPoint(new Vector3(
                screen.x,
                screen.y,
                -targetCamera.transform.position.z));
            target.z = 0f;
            target += (Vector3)cursorOffset;

            if (!hasFollowPosition)
            {
                transform.position = target;
                followVelocity = Vector3.zero;
                hasFollowPosition = true;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                target,
                ref followVelocity,
                1f / Mathf.Max(0.01f, followSpeed),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        private void ApplyScreenFixedPosition()
        {
            if (targetCamera == null)
            {
                return;
            }

            Vector3 screen = new(
                screenViewportPosition.x * targetCamera.pixelWidth,
                screenViewportPosition.y * targetCamera.pixelHeight,
                -targetCamera.transform.position.z);
            Vector3 world = targetCamera.ScreenToWorldPoint(screen);
            world.z = 0f;
            transform.position = world;
        }

        private bool IsScreenPointOverFox(Vector2 screen)
        {
            if (spriteRenderer == null || !spriteRenderer.enabled)
            {
                return false;
            }

            Bounds bounds = spriteRenderer.bounds;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(
                screen.x,
                screen.y,
                -targetCamera.transform.position.z));
            world.z = bounds.center.z;
            return bounds.Contains(world);
        }

        private Vector2 WorldToScreen(Vector3 world)
        {
            Vector3 screen = targetCamera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null ||
                activeClip == null ||
                activeClip.frames == null ||
                activeClip.frames.Length == 0)
            {
                return;
            }

            spriteRenderer.sprite =
                activeClip.frames[Mathf.Clamp(index, 0, activeClip.frames.Length - 1)];
            ApplyDisplaySize();
        }

        private void ApplyDisplaySize()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null || displayHeight <= 0f)
            {
                return;
            }

            spriteRenderer.color = Color.white;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.sortingLayerName = "WorldMouse";
            spriteRenderer.sortingOrder = -10;
            Vector2 native = spriteRenderer.sprite.bounds.size;
            float scale = displayHeight / Mathf.Max(0.0001f, native.y);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ApplyVisibility()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            bool show = displayMode != KCFoxDisplayMode.Off;
            spriteRenderer.enabled = show;
        }

        private bool IsPlaying(string clipId) =>
            string.Equals(activeClipId, clipId, StringComparison.Ordinal);

        private ClipDefinition FindClip(string clipId)
        {
            if (clips == null || string.IsNullOrEmpty(clipId))
            {
                return null;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                ClipDefinition clip = clips[i];
                if (clip != null &&
                    string.Equals(clip.id, clipId, StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private void EnsureBaseClipExists()
        {
            if (FindClip(ClipBase) != null)
            {
                return;
            }

            Debug.LogWarning(
                "[KCMouseFoxFollower] 優先度1の base クリップがありません。Inspector で設定してください。");
        }

        private void ResetDozeThreshold()
        {
            dozeThresholdSeconds =
                dozeIdleBaseSeconds +
                UnityEngine.Random.Range(0f, Mathf.Max(0f, dozeIdleRandomSeconds));
        }

        private static void SyncClipDurations(ClipDefinition clip)
        {
            if (clip == null)
            {
                return;
            }

            int count = clip.frames != null ? clip.frames.Length : 0;
            if (count <= 0)
            {
                return;
            }

            if (clip.secondsPerFrame == null || clip.secondsPerFrame.Length == 0)
            {
                clip.secondsPerFrame = new float[count];
                for (int i = 0; i < count; i++)
                {
                    clip.secondsPerFrame[i] = 0.1f;
                }

                return;
            }

            if (clip.secondsPerFrame.Length == count)
            {
                return;
            }

            float fallback = clip.secondsPerFrame[clip.secondsPerFrame.Length - 1];
            if (fallback < 0.02f)
            {
                fallback = 0.1f;
            }

            var resized = new float[count];
            for (int i = 0; i < count; i++)
            {
                resized[i] = i < clip.secondsPerFrame.Length
                    ? Mathf.Max(0.02f, clip.secondsPerFrame[i])
                    : fallback;
            }

            clip.secondsPerFrame = resized;
        }
    }
}
