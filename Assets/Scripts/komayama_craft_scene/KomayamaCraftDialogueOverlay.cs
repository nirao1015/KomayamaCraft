using System;
using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// Craft ゲーム内会話の窓口。Pause・入力ブロック・カメラ復元を担当する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftDialogueOverlay : MonoBehaviour
    {
        public static KomayamaCraftDialogueOverlay Instance { get; private set; }

        [Header("参照")]
        [SerializeField] private KomayamaCraftDialogueRunner runner;
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField] private KomayamaCraftCameraController cameraController;
        [SerializeField] private GameObject overlayRoot;

        [Header("暗幕")]
        [SerializeField, Range(0f, 1f)] private float defaultDimAlpha = 0f;

        private bool sessionActive;
        private bool pausePushed;
        private bool restoreCameraOnEnd = true;
        private Action onCompleted;

        /// <summary>会話セッション中か（入力ブロック判定用）。</summary>
        public bool IsDialogueActive => sessionActive;

        public float DefaultDimAlpha => defaultDimAlpha;

        private void Awake()
        {
            Instance = this;
            // 表示切替は Play / Opening / シーン初期状態に任せる。
            // overlayRoot が自分自身のとき、ここで SetActive(false) すると
            // Opening が起こした直後にまた落ちる。
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 台本を再生する。stageKey 例: <c>craft_intro_01</c>
        /// → <c>komayama_craft_scene-craft_intro_01.csv</c>
        /// </summary>
        public void Play(string stageKey, Action completed = null)
        {
            Play(stageKey, managePause: true, restoreCamera: true, dimAlpha: defaultDimAlpha, completed);
        }

        /// <param name="managePause">false のとき Pause は呼び出し側が管理する（OP 用）。</param>
        /// <param name="restoreCamera">false のとき終了時にカメラスナップショットへ戻さない。</param>
        /// <param name="dimAlpha">暗幕 α。OP のワールド全景は 0。</param>
        public void Play(
            string stageKey,
            bool managePause,
            bool restoreCamera,
            float dimAlpha,
            Action completed = null)
        {
            if (sessionActive)
            {
                Debug.LogWarning("[CraftDialogue] Already playing; ignored.", this);
                return;
            }

            if (runner == null)
            {
                Debug.LogError("[CraftDialogue] Runner is missing.", this);
                completed?.Invoke();
                return;
            }

            onCompleted = completed;
            sessionActive = true;
            restoreCameraOnEnd = restoreCamera;

            if (managePause && gameClock != null)
            {
                gameClock.PushPause();
                pausePushed = true;
            }
            else
            {
                pausePushed = false;
            }

            if (cameraController != null)
            {
                cameraController.BeginDialogueFramingSnapshot();
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            runner.BeginPlay(
                "komayama_craft_scene",
                stageKey,
                Mathf.Clamp01(dimAlpha),
                OnRunnerFinished);
        }

        private void OnRunnerFinished()
        {
            if (cameraController != null)
            {
                if (restoreCameraOnEnd)
                {
                    cameraController.EndDialogueFramingAndRestore();
                }
                else
                {
                    cameraController.EndDialogueFramingKeepPosition();
                }
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            if (pausePushed && gameClock != null)
            {
                gameClock.PopPause();
                pausePushed = false;
            }

            sessionActive = false;
            Action done = onCompleted;
            onCompleted = null;
            done?.Invoke();
        }
    }
}
