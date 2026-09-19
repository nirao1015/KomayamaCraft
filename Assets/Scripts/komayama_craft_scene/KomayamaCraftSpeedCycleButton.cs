using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// DebugOverlay/ButtonSpeed 用。クリックでデバッグ倍速を循環する。
    /// 倍率は <see cref="KomayamaGameClock"/>（Time.timeScale）経由で生産・演出全体に効く。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class KomayamaCraftSpeedCycleButton : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private KomayamaCraftDebugManager debugManager;
        [SerializeField] private KomayamaGameClock gameClock;
        [SerializeField, Tooltip("未設定時はこのオブジェクトの Button")]
        private Button button;
        [SerializeField, Tooltip("ButtonSpeed/DebugText (TMP)。未設定時は子から検索。")]
        private TMP_Text debugText;

        private void Reset()
        {
            CacheRefs();
        }

        private void Awake()
        {
            CacheRefs();
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickCycleSpeed);
                button.onClick.AddListener(OnClickCycleSpeed);
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

            RefreshLabelAndInteractable();
        }

        private void Start()
        {
            RefreshLabelAndInteractable();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickCycleSpeed);
            }
        }

        private void Update()
        {
            RefreshInteractableOnly();
        }

        private void LateUpdate()
        {
            SyncDebugTextLabel();
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

        private void OnClickCycleSpeed()
        {
            if (debugManager == null || debugManager.ProductionReleaseBuild)
            {
                return;
            }

            debugManager.CycleGameSpeed();
            RefreshLabelAndInteractable();
        }

        private void RefreshLabelAndInteractable()
        {
            SyncDebugTextLabel();
            RefreshInteractableOnly();
        }

        private void SyncDebugTextLabel()
        {
            if (debugText == null)
            {
                return;
            }

            string label = "—";
            if (debugManager != null)
            {
                if (debugManager.ProductionReleaseBuild)
                {
                    label = "x1";
                }
                else if (gameClock != null && gameClock.IsPaused)
                {
                    label = "PAUSE/" + KomayamaCraftDebugManager.FormatGameSpeedLabel(
                        debugManager.GameSpeedStepIndex);
                }
                else
                {
                    label = KomayamaCraftDebugManager.FormatGameSpeedLabel(
                        debugManager.GameSpeedStepIndex);
                }
            }

            if (debugText.text != label)
            {
                debugText.text = label;
            }
        }

        private void RefreshInteractableOnly()
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
    }
}
