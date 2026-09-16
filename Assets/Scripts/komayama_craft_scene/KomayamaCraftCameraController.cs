using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftCameraController : MonoBehaviour
    {
        public const int UnlockStageCount = 4;

        [Serializable]
        public struct BoundsRect
        {
            [Tooltip("カメラ中心の下限（ワールド）")]
            public Vector2 minimum;

            [Tooltip("カメラ中心の上限（ワールド）")]
            public Vector2 maximum;

            public Vector2 Clamp(Vector2 position)
            {
                return new Vector2(
                    Mathf.Clamp(position.x, minimum.x, maximum.x),
                    Mathf.Clamp(position.y, minimum.y, maximum.y));
            }
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private KCWorldSettings worldSettings;
        [SerializeField] private KCMouseFoxFollower foxFollower;
        [SerializeField] private KomayamaQuestLogView questLogView;
        [SerializeField, Min(0f)] private float moveSpeed = 6f;

        [Header("カメラ移動範囲（段階開放・中心Clamp）")]
        [SerializeField, InspectorName("海・全域（デバッグ0）")]
        [Tooltip("デバッグで 0 を選んだときの範囲。大陸全体に相当。")]
        private BoundsRect seaOrFullBounds = new BoundsRect
        {
            minimum = new Vector2(-55.5f, -34.6f),
            maximum = new Vector2(40.5f, 41f)
        };

        [SerializeField, InspectorName("段階範囲（0=初期〜3=3段階開放）")]
        [Tooltip("要素0=初期、1=1段階開放後、2=2段階、3=3段階。広くなる一方。クエスト連動は後続。")]
        private BoundsRect[] unlockStageBounds =
        {
            new BoundsRect
            {
                minimum = new Vector2(-18f, -8f),
                maximum = new Vector2(8f, 18f)
            },
            new BoundsRect
            {
                minimum = new Vector2(-30f, -18f),
                maximum = new Vector2(18f, 26f)
            },
            new BoundsRect
            {
                minimum = new Vector2(-42f, -26f),
                maximum = new Vector2(28f, 34f)
            },
            new BoundsRect
            {
                minimum = new Vector2(-55.5f, -34.6f),
                maximum = new Vector2(40.5f, 41f)
            }
        };

        [SerializeField, Range(0, UnlockStageCount - 1), InspectorName("進行上の開放段階")]
        [Tooltip("0=初期。クエスト連動までは通常 0 のまま。デバッグ上書きが優先される場合あり。")]
        private int unlockStage;

        [SerializeField] private bool enableEdgeScroll;
        [SerializeField, Min(0f)] private float edgeThresholdPixels = 16f;
        [SerializeField, Min(0.01f)] private float zoomStep = 0.6f;
        [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 2.5f;
        [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 8f;

        private bool movedThisFrame;
        private int? debugAreaOverride;

        public int UnlockStage => Mathf.Clamp(unlockStage, 0, UnlockStageCount - 1);

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (foxFollower == null)
            {
                foxFollower = FindFirstObjectByType<KCMouseFoxFollower>();
            }

            if (questLogView == null)
            {
                questLogView = FindFirstObjectByType<KomayamaQuestLogView>();
            }

            EnsureStageArray();
            ClampToActiveBounds();
        }

        private void Update()
        {
            movedThisFrame = false;
            ApplyZoom();
            ApplyMove();
            foxFollower?.NotifyCameraMoving(movedThisFrame);
        }

        /// <summary>
        /// デバッグ用。null で解除。0=海・全域、1=初期、2=1段階、3=2段階、4=3段階。
        /// </summary>
        public void SetDebugAreaOverride(int? areaCode)
        {
            if (areaCode == null)
            {
                debugAreaOverride = null;
            }
            else
            {
                debugAreaOverride = Mathf.Clamp(areaCode.Value, 0, UnlockStageCount);
            }

            ClampToActiveBounds();
        }

        /// <summary>
        /// 進行用。一方通行で段階を上げる（クエスト連動は後続）。
        /// </summary>
        public void UnlockCameraStageAtLeast(int stage)
        {
            int clamped = Mathf.Clamp(stage, 0, UnlockStageCount - 1);
            if (clamped > unlockStage)
            {
                unlockStage = clamped;
            }

            if (debugAreaOverride == null)
            {
                ClampToActiveBounds();
            }
        }

        public BoundsRect GetActiveBounds()
        {
            EnsureStageArray();
            if (debugAreaOverride.HasValue)
            {
                int code = debugAreaOverride.Value;
                if (code <= 0)
                {
                    return seaOrFullBounds;
                }

                int stageIndex = Mathf.Clamp(code - 1, 0, UnlockStageCount - 1);
                return unlockStageBounds[stageIndex];
            }

            return unlockStageBounds[UnlockStage];
        }

        private void ApplyZoom()
        {
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            if (questLogView != null && questLogView.ShouldBlockWorldZoom())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f)
            {
                return;
            }

            const float WheelNotch = 120f;
            float notches = Mathf.Abs(scrollY) >= WheelNotch * 0.5f
                ? scrollY / WheelNotch
                : Mathf.Sign(scrollY);
            float configuredMin = worldSettings != null
                ? worldSettings.ZoomMinimumOrthographicSize
                : minimumOrthographicSize;
            float configuredMax = worldSettings != null
                ? worldSettings.ZoomMaximumOrthographicSize
                : maximumOrthographicSize;
            float minSize = Mathf.Min(configuredMin, configuredMax);
            float maxSize = Mathf.Max(configuredMin, configuredMax);
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - notches * zoomStep,
                minSize,
                maxSize);
        }

        private void ApplyMove()
        {
            Vector2 direction = ReadKeyboardDirection();
            if (enableEdgeScroll)
            {
                direction += ReadEdgeDirection();
            }

            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            float speed = worldSettings != null ? worldSettings.WasdMoveSpeed : moveSpeed;
            PanByWorldDelta(direction.normalized * speed * Time.unscaledDeltaTime);
        }

        public void PanByWorldDelta(Vector2 worldDelta)
        {
            if (worldDelta.sqrMagnitude > 0.0000001f)
            {
                movedThisFrame = true;
            }

            BoundsRect bounds = GetActiveBounds();
            Vector3 position = transform.position;
            Vector2 clamped = bounds.Clamp(
                new Vector2(position.x + worldDelta.x, position.y + worldDelta.y));
            position.x = clamped.x;
            position.y = clamped.y;
            transform.position = position;
        }

        public void PanByMapDrag(Vector2 grabbedWorldDelta)
        {
            float strength = worldSettings != null ? worldSettings.MapDragPullStrength : 1f;
            PanByWorldDelta(-grabbedWorldDelta * strength);
        }

        public void CaptureSave(CameraViewSaveDto dto)
        {
            if (dto == null)
            {
                return;
            }

            Vector3 position = transform.position;
            dto.position = new Float2SaveDto(position.x, position.y);
            Camera cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
            dto.orthographicSize = cam != null && cam.orthographic
                ? cam.orthographicSize
                : 0f;
        }

        public void ApplySave(CameraViewSaveDto dto)
        {
            if (dto == null || dto.orthographicSize <= 0.01f)
            {
                return;
            }

            BoundsRect bounds = GetActiveBounds();
            Vector3 position = transform.position;
            Vector2 clamped = bounds.Clamp(new Vector2(dto.position.x, dto.position.y));
            position.x = clamped.x;
            position.y = clamped.y;
            transform.position = position;

            Camera cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
            if (cam == null || !cam.orthographic)
            {
                return;
            }

            float configuredMin = worldSettings != null
                ? worldSettings.ZoomMinimumOrthographicSize
                : minimumOrthographicSize;
            float configuredMax = worldSettings != null
                ? worldSettings.ZoomMaximumOrthographicSize
                : maximumOrthographicSize;
            float minSize = Mathf.Min(configuredMin, configuredMax);
            float maxSize = Mathf.Max(configuredMin, configuredMax);
            cam.orthographicSize = Mathf.Clamp(dto.orthographicSize, minSize, maxSize);
        }

        private void ClampToActiveBounds()
        {
            BoundsRect bounds = GetActiveBounds();
            Vector3 position = transform.position;
            Vector2 clamped = bounds.Clamp(new Vector2(position.x, position.y));
            position.x = clamped.x;
            position.y = clamped.y;
            transform.position = position;
        }

        private void EnsureStageArray()
        {
            if (unlockStageBounds != null && unlockStageBounds.Length == UnlockStageCount)
            {
                return;
            }

            var next = new BoundsRect[UnlockStageCount];
            for (int i = 0; i < UnlockStageCount; i++)
            {
                if (unlockStageBounds != null && i < unlockStageBounds.Length)
                {
                    next[i] = unlockStageBounds[i];
                }
                else
                {
                    next[i] = seaOrFullBounds;
                }
            }

            unlockStageBounds = next;
        }

        private void OnValidate()
        {
            EnsureStageArray();
            unlockStage = Mathf.Clamp(unlockStage, 0, UnlockStageCount - 1);
            if (Application.isPlaying)
            {
                ClampToActiveBounds();
            }
        }

        private static Vector2 ReadKeyboardDirection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }

        private Vector2 ReadEdgeDirection()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return Vector2.zero;
            }

            Vector2 position = mouse.position.ReadValue();
            float x = position.x <= edgeThresholdPixels
                ? -1f
                : position.x >= Screen.width - edgeThresholdPixels ? 1f : 0f;
            float y = position.y <= edgeThresholdPixels
                ? -1f
                : position.y >= Screen.height - edgeThresholdPixels ? 1f : 0f;
            return new Vector2(x, y);
        }
    }
}
