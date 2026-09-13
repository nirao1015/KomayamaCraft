using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private KCWorldSettings worldSettings;
        [SerializeField] private KCMouseFoxFollower foxFollower;
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField] private Vector2 minimumPosition = new(-6f, -3f);
        [SerializeField] private Vector2 maximumPosition = new(6f, 3f);
        [SerializeField] private bool enableEdgeScroll;
        [SerializeField, Min(0f)] private float edgeThresholdPixels = 16f;
        [SerializeField, Min(0.01f)] private float zoomStep = 0.6f;
        [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 2.5f;
        [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 8f;

        private bool movedThisFrame;

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
        }

        private void Update()
        {
            movedThisFrame = false;
            ApplyZoom();
            ApplyMove();
            foxFollower?.NotifyCameraMoving(movedThisFrame);
        }

        private void ApplyZoom()
        {
            if (targetCamera == null || !targetCamera.orthographic)
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

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(
                position.x + worldDelta.x,
                minimumPosition.x,
                maximumPosition.x);
            position.y = Mathf.Clamp(
                position.y + worldDelta.y,
                minimumPosition.y,
                maximumPosition.y);
            transform.position = position;
        }

        public void PanByMapDrag(Vector2 grabbedWorldDelta)
        {
            float strength = worldSettings != null ? worldSettings.MapDragPullStrength : 1f;
            PanByWorldDelta(-grabbedWorldDelta * strength);
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
