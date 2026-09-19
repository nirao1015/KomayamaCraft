using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KomayamaCraft
{
    /// <summary>
    /// 新規ゲーム時の OP：主観目覚め → 会話 CSV → 通常操作へ。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class KomayamaCraftOpeningController : MonoBehaviour
    {
        public static KomayamaCraftOpeningController Instance { get; private set; }

        [Header("参照")]
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private KomayamaCraftDialogueOverlay dialogueOverlay;
        [SerializeField] private Transform shipFocusTarget;
        [SerializeField] private KomayamaSaveService saveService;
        [SerializeField] private GameObject dialogueCanvasRoot;
        [SerializeField] private KomayamaQuestController questController;

        [Header("目覚め UI")]
        [SerializeField] private GameObject wakeRoot;
        [SerializeField] private Image whiteVeil;
        [SerializeField] private RectTransform eyelidTop;
        [SerializeField] private RectTransform eyelidBottom;
        [SerializeField] private TextMeshProUGUI callSpeakerText;
        [SerializeField] private TextMeshProUGUI callBodyText;
        [SerializeField] private GameObject callWindowRoot;

        [Header("台本")]
        [SerializeField] private string openingDialogueStageKey = "craft_op_01";

        [Header("Audio（差し込み用・今回は再生しない）")]
        [SerializeField] private AudioSource openingSeSource;
        [SerializeField] private AudioClip callVoiceSeClip;
        [SerializeField] private AudioClip blinkSeClip;
        [SerializeField] private AudioClip eyeOpenSeClip;

        [Header("目覚めタイミング")]
        [SerializeField, Min(1)] private int blinkCount = 3;
        [SerializeField, Min(0.01f)] private float blinkSeconds = 0.15f;
        [SerializeField, Min(0.01f)] private float finalOpenSeconds = 1f;
        [SerializeField] private string callSpeaker = "？？？";
        [SerializeField] private string callBody = "…くん　駒山くん";

        private bool sessionIsNewGame;
        private bool openingActive;
        private bool allowCallAdvance;
        private bool pausePushed;
        private Vector2 eyelidTopClosed;
        private Vector2 eyelidTopOpen;
        private Vector2 eyelidBottomClosed;
        private Vector2 eyelidBottomOpen;

        /// <summary>OP 実行中（入力ブロック用）。</summary>
        public bool IsOpeningActive => openingActive;

        private void Awake()
        {
            Instance = this;
            // ドメインリロード後もタイトルからの新規指定を拾う（SaveService の Consume より前）
            sessionIsNewGame = KomayamaBootRequest.StartNewGame;
            if (KomayamaSaveSlots.TryPeekBootRequest(out bool bootNew, out _))
            {
                sessionIsNewGame = bootNew;
            }

            if (wakeRoot != null)
            {
                wakeRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (!ShouldPlayOpening())
            {
                questController?.NotifyOpeningPhaseEnded(
                    playedOpening: false,
                    isNewGameSession: sessionIsNewGame);
                return;
            }

            StartCoroutine(RunOpening());
        }

        private bool ShouldPlayOpening()
        {
            if (debugManager != null &&
                !debugManager.ProductionReleaseBuild &&
                debugManager.StartTutorialAutoPlay)
            {
                // オートプレイ時は OP を必ず通す（スキップチェックより優先）
                return sessionIsNewGame;
            }

            if (debugManager != null &&
                !debugManager.ProductionReleaseBuild &&
                debugManager.QuestDebugScenario !=
                    KomayamaQuestDebugController.Scenario.None)
            {
                return false;
            }

            if (debugManager != null && debugManager.SkipOpeningPresentation)
            {
                return false;
            }

            if (!sessionIsNewGame)
            {
                return false;
            }

            return true;
        }

        private IEnumerator RunOpening()
        {
            openingActive = true;

            if (gameClock != null)
            {
                gameClock.PushPause();
                pausePushed = true;
            }

            if (cameraController != null && shipFocusTarget != null)
            {
                cameraController.SnapToWorldCenter(shipFocusTarget.position);
            }

            // Canvas は常時非表示スタートなので起こす（Opening はこのホスト上で動く）
            if (dialogueCanvasRoot != null)
            {
                dialogueCanvasRoot.SetActive(true);
            }

            if (wakeRoot != null)
            {
                wakeRoot.SetActive(true);
            }

            Canvas.ForceUpdateCanvases();
            CacheEyelidPositions();

            SetEyelidsClosed();
            SetWhiteAlpha(1f);
            ShowCallLine();

            allowCallAdvance = true;
            while (allowCallAdvance)
            {
                if (GetAdvancePressed())
                {
                    allowCallAdvance = false;
                    break;
                }

                yield return null;
            }

            HideCallLine();

            // 演出中は操作停止（allowCallAdvance=false）
            for (int i = 0; i < blinkCount; i++)
            {
                yield return AnimateEyelids(closedAmount: 0.35f, duration: blinkSeconds);
                yield return AnimateEyelids(closedAmount: 1f, duration: blinkSeconds);
            }

            yield return AnimateFinalOpen(finalOpenSeconds);

            if (wakeRoot != null)
            {
                wakeRoot.SetActive(false);
            }

            bool dialogueDone = false;
            if (dialogueOverlay != null)
            {
                dialogueOverlay.Play(
                    openingDialogueStageKey,
                    managePause: false,
                    restoreCamera: false,
                    dimAlpha: 0f,
                    () => dialogueDone = true);

                while (!dialogueDone)
                {
                    yield return null;
                }
            }

            if (pausePushed && gameClock != null)
            {
                gameClock.PopPause();
                pausePushed = false;
            }

            openingActive = false;
            questController?.NotifyOpeningPhaseEnded(
                playedOpening: true,
                isNewGameSession: sessionIsNewGame);
        }

        private void ShowCallLine()
        {
            if (callWindowRoot != null)
            {
                callWindowRoot.SetActive(true);
            }

            if (callSpeakerText != null)
            {
                callSpeakerText.text = callSpeaker ?? string.Empty;
            }

            if (callBodyText != null)
            {
                callBodyText.text = callBody ?? string.Empty;
            }
        }

        private void HideCallLine()
        {
            if (callWindowRoot != null)
            {
                callWindowRoot.SetActive(false);
            }
        }

        private void CacheEyelidPositions()
        {
            // 閉じ: 画面内。開き: 上まぶたは上へ、下まぶたは下へ退避
            const float fallbackTravel = 640f;
            if (eyelidTop != null)
            {
                float h = Mathf.Abs(eyelidTop.sizeDelta.y);
                if (h < 1f)
                {
                    h = Mathf.Abs(eyelidTop.rect.height);
                }

                float travel = Mathf.Max(fallbackTravel, h + 40f);
                eyelidTopClosed = Vector2.zero;
                eyelidTopOpen = new Vector2(0f, travel);
            }

            if (eyelidBottom != null)
            {
                float h = Mathf.Abs(eyelidBottom.sizeDelta.y);
                if (h < 1f)
                {
                    h = Mathf.Abs(eyelidBottom.rect.height);
                }

                // ConfigureEyelid: 閉じは (0, h)、開きは画面下外へ (0, -40)
                eyelidBottomClosed = new Vector2(0f, h);
                eyelidBottomOpen = new Vector2(0f, -40f);
            }
        }

        private void SetEyelidsClosed()
        {
            if (eyelidTop != null)
            {
                eyelidTop.anchoredPosition = eyelidTopClosed;
            }

            if (eyelidBottom != null)
            {
                eyelidBottom.anchoredPosition = eyelidBottomClosed;
            }
        }

        private IEnumerator AnimateEyelids(float closedAmount, float duration)
        {
            Vector2 topFrom = eyelidTop != null ? eyelidTop.anchoredPosition : Vector2.zero;
            Vector2 bottomFrom = eyelidBottom != null ? eyelidBottom.anchoredPosition : Vector2.zero;
            Vector2 topTo = Vector2.Lerp(eyelidTopOpen, eyelidTopClosed, Mathf.Clamp01(closedAmount));
            Vector2 bottomTo = Vector2.Lerp(eyelidBottomOpen, eyelidBottomClosed, Mathf.Clamp01(closedAmount));
            float elapsed = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                if (eyelidTop != null)
                {
                    eyelidTop.anchoredPosition = Vector2.Lerp(topFrom, topTo, t);
                }

                if (eyelidBottom != null)
                {
                    eyelidBottom.anchoredPosition = Vector2.Lerp(bottomFrom, bottomTo, t);
                }

                yield return null;
            }
        }

        private IEnumerator AnimateFinalOpen(float duration)
        {
            Vector2 topFrom = eyelidTop != null ? eyelidTop.anchoredPosition : Vector2.zero;
            Vector2 bottomFrom = eyelidBottom != null ? eyelidBottom.anchoredPosition : Vector2.zero;
            float whiteFrom = whiteVeil != null ? whiteVeil.color.a : 1f;
            float elapsed = 0f;
            float dur = Mathf.Max(0.01f, duration);
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                t = t * t * (3f - 2f * t);
                if (eyelidTop != null)
                {
                    eyelidTop.anchoredPosition = Vector2.Lerp(topFrom, eyelidTopOpen, t);
                }

                if (eyelidBottom != null)
                {
                    eyelidBottom.anchoredPosition = Vector2.Lerp(bottomFrom, eyelidBottomOpen, t);
                }

                SetWhiteAlpha(Mathf.Lerp(whiteFrom, 0f, t));
                yield return null;
            }

            SetEyelidsOpen();
            SetWhiteAlpha(0f);
        }

        private void SetEyelidsOpen()
        {
            if (eyelidTop != null)
            {
                eyelidTop.anchoredPosition = eyelidTopOpen;
            }

            if (eyelidBottom != null)
            {
                eyelidBottom.anchoredPosition = eyelidBottomOpen;
            }
        }

        private void SetWhiteAlpha(float alpha)
        {
            if (whiteVeil == null)
            {
                return;
            }

            Color c = whiteVeil.color;
            c.a = Mathf.Clamp01(alpha);
            whiteVeil.color = c;
            whiteVeil.enabled = c.a > 0.001f;
        }

        private static bool GetAdvancePressed()
        {
            if (KomayamaCraftAutoPlayInput.IsActive &&
                KomayamaCraftAutoPlayInput.ConsumeDialogueAdvance())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Play Opening Now")]
        private void DebugPlayOpeningNow()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StopAllCoroutines();
            StartCoroutine(RunOpening());
        }
#endif
    }
}
