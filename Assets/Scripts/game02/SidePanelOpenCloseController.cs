using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game02
{
    /// <summary>
    /// SidePanelObject 用。HitImg で SidePanelView を開閉する（仕様: side_panel_open_close_spec.md）。
    /// 入力ブロックは <see cref="SidePanelPointerInputBlocker"/> を付けた背景子（SidePanelBackdrop）のみに行う。
    /// </summary>
    [DisallowMultipleComponent]
    public class SidePanelOpenCloseController : MonoBehaviour
    {
        private const string DefaultBackdropChildName = "SidePanelBackdrop";

        [SerializeField] private RectTransform sidePanelView;
        [SerializeField] private Image hitImage;
        [Tooltip("省略時は SidePanelView 直下の SidePanelBackdrop を使用。")]
        [SerializeField] private RectTransform sidePanelBackdrop;
        [SerializeField] private float openCloseStartX = 1550f;
        [SerializeField] private float openCloseDuration = 0.35f;
        [SerializeField] private bool initializeClosedOnStart = true;
        [SerializeField] private bool allowSpaceKeyToggle = true;

        private float _openedAnchoredX;
        private bool _isOpen;
        private bool _isAnimating;
        private Button _hitButton;
        private Coroutine _running;
        private Graphic _backdropBlockGraphic;

        /// <summary>サイドパネルの閉じアニメーションが完了した後に発火（開く側では発火しない）。</summary>
        public static event Action SidePanelClosed;

        public bool IsOpen => _isOpen;
        public int DragForegroundSortingOrder
        {
            get
            {
                Canvas panelCanvas = sidePanelView != null ? sidePanelView.GetComponentInParent<Canvas>() : null;
                return panelCanvas != null ? panelCanvas.sortingOrder + 1 : 1000;
            }
        }

        private void Awake()
        {
            if (sidePanelView == null || hitImage == null)
            {
                Debug.LogWarning($"{nameof(SidePanelOpenCloseController)}: assign sidePanelView and hitImage.", this);
                enabled = false;
                return;
            }

            ResolveBackdrop();
            if (sidePanelBackdrop == null)
            {
                Debug.LogWarning(
                    $"{nameof(SidePanelOpenCloseController)}: child '{DefaultBackdropChildName}' under sidePanelView is required.",
                    this);
                enabled = false;
                return;
            }

            _openedAnchoredX = sidePanelView.anchoredPosition.x;
            RemoveLegacyRootBlocker();
            EnsureBackdropBlockerAndGraphic();
            ApplyInitialOpenCloseState();
            ConfigureHitButton();
        }

        private void ApplyInitialOpenCloseState()
        {
            if (initializeClosedOnStart)
            {
                if (!sidePanelView.gameObject.activeSelf)
                {
                    sidePanelView.gameObject.SetActive(true);
                }

                SetAnchoredX(openCloseStartX);
                _isOpen = false;
                SetBackdropBlocksRaycasts(false);
            }
            else
            {
                float currentX = sidePanelView.anchoredPosition.x;
                _isOpen = !Mathf.Approximately(currentX, openCloseStartX);
                SetBackdropBlocksRaycasts(_isOpen);
            }
        }

        private void ResolveBackdrop()
        {
            if (sidePanelBackdrop != null)
            {
                return;
            }

            Transform t = sidePanelView.Find(DefaultBackdropChildName);
            if (t != null)
            {
                sidePanelBackdrop = t as RectTransform;
            }
        }

        private void RemoveLegacyRootBlocker()
        {
            SidePanelPointerInputBlocker legacy = sidePanelView.GetComponent<SidePanelPointerInputBlocker>();
            if (legacy != null)
            {
                Destroy(legacy);
            }
        }

        private void EnsureBackdropBlockerAndGraphic()
        {
            if (sidePanelBackdrop.GetComponent<SidePanelPointerInputBlocker>() == null)
            {
                sidePanelBackdrop.gameObject.AddComponent<SidePanelPointerInputBlocker>();
            }

            _backdropBlockGraphic = sidePanelBackdrop.GetComponent<Graphic>();
            if (_backdropBlockGraphic == null)
            {
                _backdropBlockGraphic = sidePanelBackdrop.gameObject.AddComponent<Image>();
                Color c = ((Image)_backdropBlockGraphic).color;
                c.a = 0f;
                ((Image)_backdropBlockGraphic).color = c;
            }

            _backdropBlockGraphic.raycastTarget = false;
        }

        private void SetBackdropBlocksRaycasts(bool block)
        {
            if (sidePanelBackdrop == null)
            {
                return;
            }

            if (_backdropBlockGraphic == null)
            {
                _backdropBlockGraphic = sidePanelBackdrop.GetComponent<Graphic>();
            }

            if (_backdropBlockGraphic != null)
            {
                _backdropBlockGraphic.raycastTarget = block;
            }
        }

        private void OnDisable()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _isAnimating = false;
            if (hitImage != null)
            {
                SetHitRaycast(true);
            }

            SetBackdropBlocksRaycasts(false);
        }

        private void Update()
        {
            if (!allowSpaceKeyToggle)
            {
                return;
            }

            if (!IsSpacePressedThisFrame())
            {
                return;
            }

            OnHitImgClicked();
        }

        private static bool IsSpacePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            if (_hitButton != null)
            {
                _hitButton.onClick.RemoveListener(OnHitImgClicked);
            }
        }

        private void ConfigureHitButton()
        {
            _hitButton = hitImage.GetComponent<Button>();
            if (_hitButton == null)
            {
                _hitButton = hitImage.gameObject.AddComponent<Button>();
            }

            _hitButton.targetGraphic = hitImage;
            _hitButton.transition = Selectable.Transition.None;
            _hitButton.navigation = new Navigation { mode = Navigation.Mode.None };
            _hitButton.onClick.RemoveListener(OnHitImgClicked);
            _hitButton.onClick.AddListener(OnHitImgClicked);
        }

        private void OnHitImgClicked()
        {
            if (GameManager.Instance != null &&
                (GameManager.Instance.IsPreGameSequenceActive || GameManager.Instance.IsGameCleared))
            {
                return;
            }

            if (_isAnimating)
            {
                return;
            }

            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (!_isOpen)
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.SidePanelOpen);
                _running = StartCoroutine(OpenRoutine());
            }
            else
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.SidePanelClose);
                _running = StartCoroutine(CloseRoutine());
            }
        }

        private IEnumerator OpenRoutine()
        {
            _isAnimating = true;
            SetHitRaycast(false);
            SetBackdropBlocksRaycasts(true);

            float duration = Mathf.Max(0f, openCloseDuration);
            float fromX = sidePanelView.anchoredPosition.x;
            float toX = _openedAnchoredX;

            if (duration <= 0f)
            {
                SetAnchoredX(toX);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // パネル開閉は一時停止中でも必ず進行させる。
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetAnchoredX(Mathf.Lerp(fromX, toX, t));
                    yield return null;
                }

                SetAnchoredX(toX);
            }

            _isOpen = true;
            _isAnimating = false;
            SetHitRaycast(true);
            _running = null;
            Game02AlienProgressTracker.EnsureExists()?.NotifySidePanelOpened();
        }

        private IEnumerator CloseRoutine()
        {
            _isAnimating = true;
            SetHitRaycast(false);

            float duration = Mathf.Max(0f, openCloseDuration);
            float fromX = sidePanelView.anchoredPosition.x;
            float toX = openCloseStartX;

            if (duration <= 0f)
            {
                SetAnchoredX(toX);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // パネル開閉は一時停止中でも必ず進行させる。
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetAnchoredX(Mathf.Lerp(fromX, toX, t));
                    yield return null;
                }

                SetAnchoredX(toX);
            }

            SetBackdropBlocksRaycasts(false);
            _isOpen = false;
            _isAnimating = false;
            SetHitRaycast(true);
            _running = null;
            SidePanelClosed?.Invoke();
        }

        private void SetAnchoredX(float x)
        {
            Vector2 pos = sidePanelView.anchoredPosition;
            pos.x = x;
            sidePanelView.anchoredPosition = pos;
        }
        private void SetHitRaycast(bool enabled)
        {
            hitImage.raycastTarget = enabled;
        }
    }
}
