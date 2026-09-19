using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaEscapeController : MonoBehaviour
    {
        [SerializeField] private KomayamaShip ship;
        [SerializeField] private KomayamaProgressService progress;
        [SerializeField] private KomayamaCraftHud hud;
        [SerializeField] private GameObject endingRoot;
        [SerializeField] private TMPro.TMP_Text endingText;

        private bool sequenceRunning;
        private float sequenceStartedAt;

        public bool IsSequenceRunning => sequenceRunning;

        private void Update()
        {
            if ((KomayamaCraftDialogueOverlay.Instance != null &&
                 KomayamaCraftDialogueOverlay.Instance.IsDialogueActive) ||
                (KomayamaCraftOpeningController.Instance != null &&
                 KomayamaCraftOpeningController.Instance.IsOpeningActive))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || sequenceRunning)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                TryBeginEscape();
            }
        }

        public bool TryBeginEscape()
        {
            if (sequenceRunning)
            {
                return false;
            }

            if (ship == null || !ship.CanEscape)
            {
                hud?.ShowMessage("恒星間羅針を取り付けるまで脱出できない");
                return false;
            }

            sequenceRunning = true;
            sequenceStartedAt = Time.unscaledTime;
            progress?.MarkEndingCleared();
            if (endingRoot != null)
            {
                endingRoot.SetActive(true);
            }

            if (endingText != null)
            {
                endingText.text = progress != null && progress.HasClearedEnding
                    ? "惑星圏を離脱した。Enterでフィールドへ戻る。"
                    : "惑星圏を離脱した。";
            }

            hud?.ShowMessage("脱出成功。フィールドへ戻れます");
            return true;
        }

        public void ReturnToField()
        {
            sequenceRunning = false;
            if (endingRoot != null)
            {
                endingRoot.SetActive(false);
            }

            hud?.ShowMessage("クリア済み。再脱出できます");
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (sequenceRunning &&
                keyboard != null &&
                keyboard.enterKey.wasPressedThisFrame &&
                Time.unscaledTime >= sequenceStartedAt + 0.4f &&
                endingRoot != null &&
                endingRoot.activeSelf)
            {
                ReturnToField();
            }
        }
    }
}
