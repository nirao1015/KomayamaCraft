using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game02
{
    /// <summary>
    /// game02_scene 内の Canvas 開閉（単純な SetActive）とシーン遷移（フェード付き）の窓口。
    /// メニュー退出・クリア退出ボタンは Inspector の Button.OnClick から本コンポーネントの public メソッドを呼び出す。
    /// メニュー退出時のフェードは menu02 の StartButton と同様に FadeCanvas プレハブ（Imports/Fade）を優先する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game02TransitionManager : MonoBehaviour
    {
        private const int FadeCanvasSortingOrder = 2000;

        [Header("開閉対象: 設定など（SetActive）")]
        [SerializeField, Tooltip("設定 UI などのルート。未設定のときは開閉メソッドは何もしません。")]
        private GameObject configPanelRoot;

        [Header("遷移先: インゲームメニューからの退出")]
        [SerializeField, Tooltip("メニューへ戻る系ボタン押下後に読み込むシーン名。")]
        private string leaveFromGameMenuPanelSceneName = "menu02_scene";

        [Header("遷移演出: インゲームメニューからの退出")]
        [SerializeField, Tooltip("メニューへ戻るときのフェードアウト秒数。")]
        private float leaveFromGameMenuFadeOutSeconds = 1f;
        [SerializeField, Tooltip("FadeCanvas 未設定時のみ使用。FadeManager によるフェードイン秒数。")]
        private float leaveFromGameMenuFadeInSeconds = 0.2f;

        [Header("遷移先: ゲームクリアパネルからの退出")]
        [SerializeField, Tooltip("GameClearButton 押下後に読み込むシーン名。")]
        private string leaveAfterGameClearSceneName = "menu02_scene";

        [Header("遷移演出: ゲームクリアパネルからの退出")]
        [SerializeField, Tooltip("クリア後のメニューへ戻るときのフェードアウト秒数。")]
        private float leaveAfterGameClearFadeOutSeconds = 1f;
        [SerializeField, Tooltip("FadeCanvas 未設定時のみ使用。FadeManager によるフェードイン秒数。")]
        private float leaveAfterGameClearFadeInSeconds = 0.2f;
        [SerializeField, Tooltip(
            "遷移先シーン名が dialogue_scene のとき、読み込み直前に SceneTransitionContext.DestinationSceneName に設定する（dialogue 終了後に開くシーン）。空ならコンテキストは変更しない。")]
        private string dialogueSceneDestinationSceneName;

        [Header("フェード（menu02 / Menu02TransitionManager と同等）")]
        [SerializeField, Tooltip("遷移 SE 再生後、フェード開始までの待機秒数（Time.timeScale に依存しない）。")]
        private float transitionWaitSeconds = 0.5f;
        [SerializeField, Tooltip("menu02 と同じ FadeCanvas プレハブを指定。未設定時は従来どおり FadeManager を使用。")]
        private GameObject fadeCanvasPrefab;
        [SerializeField, Tooltip("フェードアウト時に使うマスク画像。未設定ならプレハブ既定（menu02 と同様）。")]
        private Sprite fadeOutSprite;

        [Header("参照: メニューへ戻るボタン（インタラクト制御・押下演出）")]
        [SerializeField, Tooltip("GameMenuPanel 内の Game02MenuReturnButton。")]
        private Button leaveFromGameMenuPanelButton;
        [SerializeField, Tooltip("PausePanel 内の Game02MenuReturnButton。")]
        private Button leaveFromPausePanelButton;

        [Header("参照: ゲームクリア退出ボタン（インタラクト制御・押下演出）")]
        [SerializeField, Tooltip("GameClearedPanel の GameClearButton。")]
        private Button gameClearLeaveButton;

        [Header("遷移ボタン押下演出（menu02 と同様）")]
        [SerializeField, Tooltip("有効時、ボタンに付いた MenuImageButtonPressFeedback を同期します。")]
        private bool enableLeaveButtonPressFeedback = true;
        [SerializeField, Tooltip("押下演出のスケール倍率（MenuImageButtonPressFeedback.Configure に渡す値）。")]
        private float leaveButtonPressedScale = 0.94f;
        [SerializeField, Tooltip("押下演出の明るさ倍率。")]
        private float leaveButtonPressedBrightness = 0.84f;

        private bool isSceneTransitioning;
        private Fade fadeController;
        private FadeImage fadeImage;

        /// <summary>シーン遷移処理中なら true（メニューボタンのインタラクト制御などに使用）。</summary>
        public bool IsSceneTransitionInProgress => isSceneTransitioning;

        /// <summary>
        /// シーンに配置済みの Game02TransitionManager を返す。見つからなければ null。
        /// </summary>
        public static Game02TransitionManager EnsureSceneController()
        {
            return FindAnyObjectByType<Game02TransitionManager>(FindObjectsInactive.Include);
        }

        private void Awake()
        {
            ConfigureLeaveButtonPressFeedbackIfNeeded();
        }

        private void OnEnable()
        {
            ConfigureLeaveButtonPressFeedbackIfNeeded();
        }

        /// <summary>設定パネルなどを表示するときに呼ぶ（ルート未設定なら何もしない）。</summary>
        public void SetConfigPanelActive(bool active)
        {
            if (configPanelRoot != null)
            {
                configPanelRoot.SetActive(active);
            }
        }

        /// <summary>
        /// GameMenuPanel の「メニューへ戻る」ボタン OnClick から呼ぶ。PausePanel は閉じたままにする。
        /// </summary>
        public void RequestLeaveToMenuFromPlayingUi()
        {
            RequestLeaveToMenuFromPlayingUiInternal(keepPausePanelVisibleWhenPaused: false);
        }

        /// <summary>
        /// PausePanel の Game02MenuReturnButton OnClick から呼ぶ。PausePanel は閉じない（シーン遷移まで表示を維持）。
        /// </summary>
        public void RequestLeaveToMenuFromPausePanel()
        {
            RequestLeaveToMenuFromPlayingUiInternal(keepPausePanelVisibleWhenPaused: true);
        }

        private void RequestLeaveToMenuFromPlayingUiInternal(bool keepPausePanelVisibleWhenPaused)
        {
            if (isSceneTransitioning)
            {
                return;
            }

            GameManager manager = GameManager.Instance;
            if (manager == null ||
                manager.HasFatalError ||
                manager.IsPreGameSequenceActive ||
                manager.IsGameCleared)
            {
                return;
            }

            if (!manager.IsPaused)
            {
                Game02SeManager.TryGet()?.PlayByCue(Game02SeCue.PauseOn);
            }

            manager.SetPaused(true, showPausePanelWhenPaused: keepPausePanelVisibleWhenPaused);
            TransitionLeaveFromGameMenuPanel();
        }

        /// <summary>インゲームメニューからメニューシーン等へ退場するときに呼ぶ。</summary>
        public void TransitionLeaveFromGameMenuPanel()
        {
            BeginTransitionToScene(
                leaveFromGameMenuPanelSceneName,
                leaveFromGameMenuFadeOutSeconds,
                leaveFromGameMenuFadeInSeconds);
        }

        /// <summary>ゲームクリア後の GameClearButton.OnClick から呼ぶ（Inspector 設定の遷移先・フェード秒数を使用）。</summary>
        public void TransitionLeaveAfterGameClear()
        {
            GameClearedCeremonyController.TryNotifyEndingCleanupStatic();
            BeginTransitionToScene(
                leaveAfterGameClearSceneName,
                leaveAfterGameClearFadeOutSeconds,
                leaveAfterGameClearFadeInSeconds);
        }

        /// <summary>
        /// 任意シーンへフェード付きで遷移する。dialogue_scene のときは <see cref="dialogueSceneDestinationSceneName"/> が空でなければ
        /// <see cref="SceneTransitionContext.DestinationSceneName"/> を設定してから読み込む。
        /// </summary>
        /// <param name="sceneName">読み込むシーン名。</param>
        /// <param name="fadeOutSeconds">フェードアウト秒数（FadeCanvas 使用時）。</param>
        /// <param name="fadeInSeconds">FadeManager フォールバック時のフェードイン秒数。</param>
        public void TransitionToScene(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            BeginTransitionToScene(sceneName, fadeOutSeconds, fadeInSeconds);
        }

        private void BeginTransitionToScene(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            if (!TryBeginSceneTransition())
            {
                return;
            }

            Game02SeManager.TryGet()?.PlayTransitionStartSe();

            StartCoroutine(CoLoadSceneWithFade(sceneName, fadeOutSeconds, fadeInSeconds));
        }

        private bool TryBeginSceneTransition()
        {
            if (isSceneTransitioning)
            {
                return false;
            }

            isSceneTransitioning = true;
            SetLeaveRouteButtonsInteractable(false);
            return true;
        }

        private void AbortSceneTransitionAndUnlockButtons()
        {
            isSceneTransitioning = false;
            SetLeaveRouteButtonsInteractable(true);
        }

        private IEnumerator CoLoadSceneWithFade(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            string trimmed = string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                AbortSceneTransitionAndUnlockButtons();
                yield break;
            }

            ApplySceneTransitionContextIfNeeded(trimmed);

            float transitionWait = Mathf.Max(0f, transitionWaitSeconds);
            if (transitionWait > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionWait);
            }

            if (fadeCanvasPrefab != null)
            {
                yield return StartCoroutine(CoRunMenuStyleFadeOut(Mathf.Max(0.01f, fadeOutSeconds)));
                SceneManager.LoadScene(trimmed);
                yield break;
            }

            LoadSceneWithFadeManager(trimmed, fadeOutSeconds, fadeInSeconds);
        }

        private void ApplySceneTransitionContextIfNeeded(string trimmedSceneName)
        {
            if (!IsDialogueScene(trimmedSceneName))
            {
                return;
            }

            string dest = string.IsNullOrWhiteSpace(dialogueSceneDestinationSceneName)
                ? null
                : dialogueSceneDestinationSceneName.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                return;
            }

            SceneTransitionContext.DestinationSceneName = dest;
        }

        private static bool IsDialogueScene(string trimmedSceneName)
        {
            return string.Equals(trimmedSceneName, "dialogue_scene", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerator CoRunMenuStyleFadeOut(float fadeOutDurationSeconds)
        {
            EnsureFadeController();
            bool fadeCompleted = fadeController == null;
            if (fadeController == null)
            {
                yield break;
            }

            yield return null;
            if (fadeImage != null && fadeOutSprite != null && fadeOutSprite.texture != null)
            {
                fadeImage.UpdateMaskTexture(fadeOutSprite.texture);
            }

            fadeController.FadeIn(fadeOutDurationSeconds, () => fadeCompleted = true);
            while (!fadeCompleted)
            {
                yield return null;
            }
        }

        private void EnsureFadeController()
        {
            if (fadeCanvasPrefab == null)
            {
                return;
            }

            if (fadeController != null)
            {
                return;
            }

            Fade existing = ResolveExistingFadeInstance();
            if (existing != null)
            {
                fadeController = existing;
                fadeImage = existing.GetComponent<FadeImage>();
                EnsureFadeCanvasFrontMost(existing.gameObject);
                return;
            }

            GameObject fadeObject = Instantiate(fadeCanvasPrefab);
            fadeObject.name = "FadeCanvas";
            UnityEngine.Object.DontDestroyOnLoad(fadeObject);
            EnsureFadeCanvasFrontMost(fadeObject);

            fadeController = fadeObject.GetComponent<Fade>();
            fadeImage = fadeObject.GetComponent<FadeImage>();
        }

        private static Fade ResolveExistingFadeInstance()
        {
            Fade[] fades = FindObjectsByType<Fade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < fades.Length; i++)
            {
                Fade candidate = fades[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(true);
                }

                if (!candidate.enabled)
                {
                    candidate.enabled = true;
                }

                return candidate;
            }

            return null;
        }

        private static void EnsureFadeCanvasFrontMost(GameObject fadeObject)
        {
            if (fadeObject == null)
            {
                return;
            }

            Canvas[] canvases = fadeObject.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.overrideSorting = true;
                canvas.sortingOrder = FadeCanvasSortingOrder;
            }
        }

        private void SetLeaveRouteButtonsInteractable(bool interactable)
        {
            if (leaveFromGameMenuPanelButton != null)
            {
                leaveFromGameMenuPanelButton.interactable = interactable;
            }

            if (leaveFromPausePanelButton != null)
            {
                leaveFromPausePanelButton.interactable = interactable;
            }

            if (gameClearLeaveButton != null)
            {
                gameClearLeaveButton.interactable = interactable;
            }
        }

        private void ConfigureLeaveButtonPressFeedbackIfNeeded()
        {
            if (!enableLeaveButtonPressFeedback)
            {
                return;
            }

            AttachMenuPressFeedback(leaveFromGameMenuPanelButton);
            AttachMenuPressFeedback(leaveFromPausePanelButton);
            AttachMenuPressFeedback(gameClearLeaveButton);
        }

        private void AttachMenuPressFeedback(Button button)
        {
            MenuImageButtonPressFeedback.AttachTo(button, leaveButtonPressedScale, leaveButtonPressedBrightness);
        }

        private static void LoadSceneWithFadeManager(string trimmedSceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            FadeManager fadeManager = EnsureFadeManager();
            if (fadeManager != null)
            {
                fadeManager.LoadScene(trimmedSceneName, Mathf.Max(0f, fadeOutSeconds), Mathf.Max(0f, fadeInSeconds));
                return;
            }

            SceneManager.LoadScene(trimmedSceneName);
        }

        private static FadeManager EnsureFadeManager()
        {
            FadeManager fadeManager = FindAnyObjectByType<FadeManager>();
            if (fadeManager == null)
            {
                GameObject fadeManagerObject = new GameObject("FadeManager");
                fadeManager = fadeManagerObject.AddComponent<FadeManager>();
                fadeManager.DebugMode = false;
            }
            else
            {
                fadeManager.DebugMode = false;
            }

            return fadeManager;
        }
    }
}
