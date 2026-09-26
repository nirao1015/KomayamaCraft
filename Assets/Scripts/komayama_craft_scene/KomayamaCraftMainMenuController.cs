using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// menu設定 から開くメインメニュー（設定／セーブしてタイトル／セーブしてデスクトップ）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftMainMenuController : MonoBehaviour
    {
        private enum Layer
        {
            Closed = 0,
            Main = 1,
            Config = 2
        }

        [Header("入口")]
        [SerializeField] private Button openButton;

        [Header("メインメニュー UI")]
        [SerializeField] private GameObject mainMenuRoot;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveTitleButton;
        [SerializeField] private Button saveQuitButton;
        [SerializeField] private TMP_Text statusMessageText;

        [Header("設定パネル（タイトル同等）")]
        [SerializeField] private GameObject craftConfigRoot;
        [SerializeField] private Button craftConfigExitButton;

        [Header("右端メニュー操作ロック")]
        [SerializeField] private CanvasGroup menuObjectCanvasGroup;
        [SerializeField, Tooltip("MenuObject 配下の Selectable。未設定時は menuObjectCanvasGroup のみ。")]
        private Selectable[] menuBarSelectables;

        [Header("依存")]
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField] private KomayamaSaveService saveService;
        [SerializeField] private KomayamaCraftSeManager seManager;
        [SerializeField] private KomayamaBuildMenuSlide buildMenuSlide;

        [Header("タイトル遷移")]
        [SerializeField] private string titleSceneName = "title_scene";
        [SerializeField] private float transitionWaitSeconds = 0.2f;
        [SerializeField] private float fadeOutDurationSeconds = 1.5f;
        [SerializeField] private GameObject fadeCanvasPrefab;

        private Layer layer = Layer.Closed;
        private bool pauseHeld;
        private bool busy;
        private Fade fadeController;
        private FadeImage fadeImage;
        private readonly List<Selectable> cachedMenuSelectables = new List<Selectable>(16);

        public static KomayamaCraftMainMenuController Instance { get; private set; }

        public bool IsOpen => layer != Layer.Closed;

        private void Awake()
        {
            Instance = this;

            if (openButton != null)
            {
                openButton.onClick.AddListener(OnClickOpenToggle);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseFully);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenConfig);
            }

            if (saveTitleButton != null)
            {
                saveTitleButton.onClick.AddListener(OnClickSaveTitle);
            }

            if (saveQuitButton != null)
            {
                saveQuitButton.onClick.AddListener(OnClickSaveQuit);
            }

            if (craftConfigExitButton != null)
            {
                craftConfigExitButton.onClick.AddListener(ReturnToMainFromConfig);
            }

            CacheMenuSelectablesIfNeeded();
            ForceClosedVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OnClickOpenToggle);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseFully);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OpenConfig);
            }

            if (saveTitleButton != null)
            {
                saveTitleButton.onClick.RemoveListener(OnClickSaveTitle);
            }

            if (saveQuitButton != null)
            {
                saveQuitButton.onClick.RemoveListener(OnClickSaveQuit);
            }

            if (craftConfigExitButton != null)
            {
                craftConfigExitButton.onClick.RemoveListener(ReturnToMainFromConfig);
            }

            ReleasePauseIfHeld();
        }

        private void Update()
        {
            if (busy || layer == Layer.Closed)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (layer == Layer.Config)
            {
                ReturnToMainFromConfig();
            }
            else
            {
                CloseFully();
            }
        }

        private void OnClickOpenToggle()
        {
            if (busy)
            {
                return;
            }

            KomayamaQuestController quest = KomayamaQuestController.Instance;
            if (quest != null && !quest.AreBuildAndSettingsUnlocked)
            {
                return;
            }

            if (layer == Layer.Closed)
            {
                OpenMain();
            }
            else if (layer == Layer.Main)
            {
                CloseFully();
            }
            else
            {
                // 設定表示中の再押下はメインへ戻す
                ReturnToMainFromConfig();
            }
        }

        public void OpenMain()
        {
            if (busy || layer == Layer.Main)
            {
                return;
            }

            if (buildMenuSlide != null && buildMenuSlide.IsOpen)
            {
                buildMenuSlide.Close();
            }

            if (KomayamaCraftPlaceholderMenuController.Instance != null &&
                KomayamaCraftPlaceholderMenuController.Instance.IsOpen)
            {
                KomayamaCraftPlaceholderMenuController.Instance.CloseAll();
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);
            HoldPause();
            SetMenuBarInteractable(false);
            SetStatus(string.Empty);

            if (craftConfigRoot != null)
            {
                craftConfigRoot.SetActive(false);
            }

            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(true);
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }

            layer = Layer.Main;
        }

        public void CloseFully()
        {
            if (busy || layer == Layer.Closed)
            {
                return;
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);

            if (craftConfigRoot != null)
            {
                craftConfigRoot.SetActive(false);
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(false);
            }

            SetMenuBarInteractable(true);
            ReleasePauseIfHeld();
            SetStatus(string.Empty);
            layer = Layer.Closed;
        }

        private void OpenConfig()
        {
            if (busy || layer != Layer.Main)
            {
                return;
            }

            if (craftConfigRoot == null)
            {
                SetStatus("設定パネルが未配置です");
                return;
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuSettings);

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(false);
            }

            craftConfigRoot.SetActive(true);
            layer = Layer.Config;
        }

        private void ReturnToMainFromConfig()
        {
            if (busy || layer != Layer.Config)
            {
                return;
            }

            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.MainMenuToggle);

            if (craftConfigRoot != null)
            {
                craftConfigRoot.SetActive(false);
            }

            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(true);
            }

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }

            layer = Layer.Main;
        }

        private void OnClickSaveTitle()
        {
            if (busy || layer != Layer.Main)
            {
                return;
            }

            StartCoroutine(CoSaveAndGoTitle());
        }

        private void OnClickSaveQuit()
        {
            if (busy || layer != Layer.Main)
            {
                return;
            }

            StartCoroutine(CoSaveAndQuit());
        }

        private IEnumerator CoSaveAndGoTitle()
        {
            busy = true;

            if (!TrySaveNow())
            {
                SetStatus("セーブに失敗しました");
                busy = false;
                yield break;
            }

            float wait = Mathf.Max(0f, transitionWaitSeconds);
            if (wait > 0f)
            {
                yield return new WaitForSecondsRealtime(wait);
            }

            // 押下 SE は鳴らさない（遷移 SE＋フェードのみ）
            KomayamaCraftSeManager.TryPlay(seManager, KomayamaCraftSeCue.TransitionToTitle);
            yield return CoRunFadeOut();

            string next = string.IsNullOrWhiteSpace(titleSceneName) ? "title_scene" : titleSceneName.Trim();
            SceneManager.LoadScene(next);
        }

        private IEnumerator CoSaveAndQuit()
        {
            busy = true;

            if (!TrySaveNow())
            {
                SetStatus("セーブに失敗しました");
                busy = false;
                yield break;
            }

            // 押下 SE は鳴らさない（終了待ち用も無し）
            QuitApplication();
            yield break;
        }

        private bool TrySaveNow()
        {
            if (saveService == null)
            {
                saveService = FindFirstObjectByType<KomayamaSaveService>(FindObjectsInactive.Include);
            }

            if (saveService == null)
            {
                return false;
            }

            return saveService.TrySave();
        }

        private void HoldPause()
        {
            if (pauseHeld)
            {
                return;
            }

            if (gameClock == null)
            {
                gameClock = KomayamaGameClock.Instance;
            }

            if (gameClock != null)
            {
                gameClock.PushPause();
                pauseHeld = true;
            }
        }

        private void ReleasePauseIfHeld()
        {
            if (!pauseHeld)
            {
                return;
            }

            if (gameClock != null)
            {
                gameClock.PopPause();
            }

            pauseHeld = false;
        }

        private void SetMenuBarInteractable(bool interactable)
        {
            if (menuObjectCanvasGroup != null)
            {
                menuObjectCanvasGroup.interactable = interactable;
                menuObjectCanvasGroup.blocksRaycasts = interactable;
            }

            CacheMenuSelectablesIfNeeded();
            for (int i = 0; i < cachedMenuSelectables.Count; i++)
            {
                if (cachedMenuSelectables[i] != null)
                {
                    cachedMenuSelectables[i].interactable = interactable;
                }
            }
        }

        private void CacheMenuSelectablesIfNeeded()
        {
            if (cachedMenuSelectables.Count > 0)
            {
                return;
            }

            if (menuBarSelectables != null && menuBarSelectables.Length > 0)
            {
                for (int i = 0; i < menuBarSelectables.Length; i++)
                {
                    if (menuBarSelectables[i] != null)
                    {
                        cachedMenuSelectables.Add(menuBarSelectables[i]);
                    }
                }

                return;
            }

            if (menuObjectCanvasGroup != null)
            {
                Selectable[] found = menuObjectCanvasGroup.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    cachedMenuSelectables.Add(found[i]);
                }
            }
        }

        private void ForceClosedVisuals()
        {
            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(false);
            }

            if (craftConfigRoot != null)
            {
                craftConfigRoot.SetActive(false);
            }

            SetMenuBarInteractable(true);
            layer = Layer.Closed;
        }

        private void SetStatus(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message ?? string.Empty;
            }
        }

        private IEnumerator CoRunFadeOut()
        {
            EnsureFadeController();
            if (fadeController == null)
            {
                yield break;
            }

            yield return null;
            bool done = false;
            fadeController.FadeIn(Mathf.Max(0.01f, fadeOutDurationSeconds), () => done = true);
            while (!done)
            {
                yield return null;
            }
        }

        private void EnsureFadeController()
        {
            if (fadeController != null)
            {
                return;
            }

            if (fadeCanvasPrefab == null)
            {
#if UNITY_EDITOR
                fadeCanvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Imports/Fade/FadeCanvas.prefab");
#endif
            }

            if (fadeCanvasPrefab == null)
            {
                return;
            }

            GameObject fadeObject = Instantiate(fadeCanvasPrefab);
            fadeObject.name = "FadeCanvas";
            DontDestroyOnLoad(fadeObject);
            Canvas[] canvases = fadeObject.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].overrideSorting = true;
                canvases[i].sortingOrder = 2000;
            }

            fadeController = fadeObject.GetComponent<Fade>();
            fadeImage = fadeObject.GetComponent<FadeImage>();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
