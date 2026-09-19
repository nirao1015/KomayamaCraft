using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// DebugOverlay/ButtonPause 用。ゲーム時間の一時停止をトグルする。
    /// イベント用 API は <see cref="KomayamaGameClock.PushPause"/> / <see cref="KomayamaGameClock.PopPause"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class KomayamaCraftPauseButton : MonoBehaviour
    {
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text debugText;

        private void Reset()
        {
            CacheRefs();
        }

        private void Awake()
        {
            CacheRefs();
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickTogglePause);
                button.onClick.AddListener(OnClickTogglePause);
            }
        }

        private void OnValidate()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            if (debugText != null)
            {
                debugText.raycastTarget = false;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickTogglePause);
            }
        }

        private void Update()
        {
            if (button == null)
            {
                return;
            }

            bool allow = debugManager != null && !debugManager.ProductionReleaseBuild;
            if (button.interactable != allow)
            {
                button.interactable = allow;
            }
        }

        private void LateUpdate()
        {
            SyncLabel();
        }

        private void CacheRefs()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (debugText == null)
            {
                debugText = GetComponentInChildren<TMP_Text>(true);
            }

            if (debugManager == null)
            {
                debugManager = FindFirstObjectByType<KomayamaCraftDebugManager>();
            }

            if (gameClock == null)
            {
                gameClock = FindFirstObjectByType<KomayamaGameClock>();
            }
        }

        private void OnClickTogglePause()
        {
            if (debugManager == null || debugManager.ProductionReleaseBuild)
            {
                return;
            }

            debugManager.ToggleGamePause();
            Refresh();
        }

        private void Refresh()
        {
            SyncLabel();
        }

        private void SyncLabel()
        {
            if (debugText == null)
            {
                return;
            }

            bool paused = gameClock != null && gameClock.IsPaused;
            string label = paused ? "▶" : "❚❚";
            if (debugText.text != label)
            {
                debugText.text = label;
            }
        }
    }
}
