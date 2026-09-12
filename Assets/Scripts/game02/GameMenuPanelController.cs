using UnityEngine;
using UnityEngine.UI;

namespace Game02
{
    [DisallowMultipleComponent]
    public sealed class GameMenuPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject gameMenuPanel;
        [SerializeField] private Button buttonMenu;
        [SerializeField] private Button gameMenuButton;
        [SerializeField] private Button gameReturnButton;

        private bool wasPausedBeforeMenuOpen;
        private bool isMenuOpen;
        private CanvasGroup menuCanvasGroup;
        private Game02TransitionManager cachedTransitionManager;

        /// <summary>ゲーム内メニュー（オーバーレイ）が開いているか。他 UI との表示制御に使う。</summary>
        public bool IsMenuOpen => isMenuOpen;

        public static GameMenuPanelController EnsureSceneController()
        {
            GameObject panel = GameObject.Find("PanelCanvas/GameMenuPanel");
            if (panel == null)
            {
                panel = FindPanelUnderPanelCanvas("GameMenuPanel");
                if (panel == null)
                {
                    return null;
                }
            }

            GameMenuPanelController controller = panel.GetComponent<GameMenuPanelController>();
            if (controller == null)
            {
                controller = panel.AddComponent<GameMenuPanelController>();
            }

            controller.gameMenuPanel = panel;
            controller.EnsureBindings();
            controller.EnsureInitialHiddenState();
            return controller;
        }

        private void Awake()
        {
            if (gameMenuPanel == null)
            {
                gameMenuPanel = gameObject;
            }

            EnsureBindings();
            EnsureInitialHiddenState();
        }

        private void OnEnable()
        {
            EnsureBindings();
            RefreshButtonMenuInteractable();
        }

        private void Update()
        {
            RefreshButtonMenuInteractable();
        }

        private void EnsureBindings()
        {
            if (gameMenuPanel == null)
            {
                gameMenuPanel = gameObject;
            }

            if (buttonMenu == null)
            {
                buttonMenu = FindButtonUnderPanelCanvas("ButtonMenu");
            }

            if (gameReturnButton == null)
            {
                gameReturnButton = FindButtonInChildren(gameMenuPanel != null ? gameMenuPanel.transform : transform, "GameReturnButton");
            }

            if (gameMenuButton == null)
            {
                gameMenuButton = FindButtonInChildren(gameMenuPanel != null ? gameMenuPanel.transform : transform, "GameMenuButton");
            }

            if (gameMenuButton == null)
            {
                gameMenuButton = FindButtonInChildren(gameMenuPanel != null ? gameMenuPanel.transform : transform, "Game02MenuReturnButton");
            }

            if (gameMenuButton == null)
            {
                gameMenuButton = FindButtonInChildren(gameMenuPanel != null ? gameMenuPanel.transform : transform, "GameClearButton");
            }
        }

        private void EnsureInitialHiddenState()
        {
            if (gameMenuPanel == null)
            {
                return;
            }

            isMenuOpen = false;
            ApplyMenuVisibility(visible: false);
        }

        private void RefreshButtonMenuInteractable()
        {
            if (buttonMenu == null)
            {
                return;
            }

            GameManager manager = GameManager.Instance;
            bool interactable = manager != null &&
                               !manager.HasFatalError &&
                               !manager.IsPreGameSequenceActive &&
                               !manager.IsGameCleared &&
                               !(CachedTransitionManager != null &&
                                 CachedTransitionManager.IsSceneTransitionInProgress);
            if (buttonMenu.interactable != interactable)
            {
                buttonMenu.interactable = interactable;
            }

            if (gameMenuButton != null && gameMenuButton.interactable != interactable)
            {
                gameMenuButton.interactable = interactable;
            }

            if (gameReturnButton != null && gameReturnButton.interactable != interactable)
            {
                gameReturnButton.interactable = interactable;
            }
        }

        /// <summary>ButtonMenu の OnClick から呼ぶ（Inspector 配線）。</summary>
        public void OnClickButtonMenu()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null ||
                manager.HasFatalError ||
                manager.IsPreGameSequenceActive ||
                manager.IsGameCleared)
            {
                return;
            }

            if (isMenuOpen)
            {
                return;
            }

            wasPausedBeforeMenuOpen = manager.IsPaused;
            SetMenuVisible(true);
            manager.SetPaused(true, showPausePanelWhenPaused: false);
            if (!wasPausedBeforeMenuOpen)
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.MenuPanelOpen);
            }
        }

        /// <summary>メニューを閉じるボタンの OnClick から呼ぶ（Inspector 配線）。</summary>
        public void OnClickGameReturnButton()
        {
            if (!isMenuOpen)
            {
                return;
            }

            SetMenuVisible(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPaused(wasPausedBeforeMenuOpen);
            }

            if (!wasPausedBeforeMenuOpen)
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.MenuPanelClose);
            }
        }

        private Game02TransitionManager CachedTransitionManager
        {
            get
            {
                if (cachedTransitionManager == null)
                {
                    cachedTransitionManager = FindAnyObjectByType<Game02TransitionManager>(FindObjectsInactive.Include);
                }

                return cachedTransitionManager;
            }
        }

        private void SetMenuVisible(bool visible)
        {
            if (gameMenuPanel == null)
            {
                return;
            }

            ApplyMenuVisibility(visible);

            if (visible)
            {
                EnsureMenuVisiblePresentation();
            }

            isMenuOpen = visible;
        }

        private void ApplyMenuVisibility(bool visible)
        {
            if (gameMenuPanel == null)
            {
                return;
            }

            if (!gameMenuPanel.activeSelf)
            {
                gameMenuPanel.SetActive(true);
            }

            EnsureMenuCanvasGroup();
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = visible ? 1f : 0f;
                menuCanvasGroup.interactable = visible;
                menuCanvasGroup.blocksRaycasts = visible;
            }
        }

        private void EnsureMenuCanvasGroup()
        {
            if (gameMenuPanel == null || menuCanvasGroup != null)
            {
                return;
            }

            menuCanvasGroup = gameMenuPanel.GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
            {
                menuCanvasGroup = gameMenuPanel.AddComponent<CanvasGroup>();
            }
        }

        private void EnsureMenuVisiblePresentation()
        {
            if (gameMenuPanel == null)
            {
                return;
            }

            // メニューを最前面に寄せる（見えているのに背面に潜る事故を防ぐ）。
            gameMenuPanel.transform.SetAsLastSibling();

            CanvasGroup[] groups = gameMenuPanel.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup g = groups[i];
                if (g == null)
                {
                    continue;
                }

                g.alpha = 1f;
                g.interactable = true;
                g.blocksRaycasts = true;
            }

            // 半透明オーバーレイが透明化しているケースを補正する。
            Transform overlay = gameMenuPanel.transform.Find("GrayOverlay");
            if (overlay != null)
            {
                Image overlayImage = overlay.GetComponent<Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = new Color(0f, 0f, 0f, 0.5f);
                    overlayImage.raycastTarget = true;
                }
            }
        }

        private static Button FindButtonUnderPanelCanvas(string buttonName)
        {
            GameObject panelCanvas = GameObject.Find("PanelCanvas");
            if (panelCanvas == null)
            {
                return null;
            }

            return FindButtonInChildren(panelCanvas.transform, buttonName);
        }

        private static Button FindButtonInChildren(Transform root, string buttonName)
        {
            if (root == null || string.IsNullOrEmpty(buttonName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != buttonName)
                {
                    continue;
                }

                Button b = t.GetComponent<Button>();
                if (b != null)
                {
                    return b;
                }
            }

            return null;
        }

        private static GameObject FindPanelUnderPanelCanvas(string panelName)
        {
            GameObject panelCanvas = GameObject.Find("PanelCanvas");
            if (panelCanvas == null || string.IsNullOrEmpty(panelName))
            {
                return null;
            }

            Transform[] all = panelCanvas.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != panelName)
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }

    }
}
