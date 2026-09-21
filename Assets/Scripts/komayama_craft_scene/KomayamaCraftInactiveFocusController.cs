using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// ウィンドウ非アクティブ時、設定に応じてクラフトのゲーム時間をポーズする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftInactiveFocusController : MonoBehaviour
    {
        [SerializeField] private KomayamaGameClock gameClock;

        private bool inactivePauseHeld;

        private void Awake()
        {
            if (gameClock == null)
            {
                gameClock = GetComponent<KomayamaGameClock>();
            }

            if (gameClock == null)
            {
                gameClock = KomayamaGameClock.Instance;
            }
        }

        private void OnEnable()
        {
            SoundSettingsManager.SettingsChanged += RefreshInactivePause;
            RefreshInactivePause();
        }

        private void OnDisable()
        {
            SoundSettingsManager.SettingsChanged -= RefreshInactivePause;
            ReleaseInactivePauseIfHeld();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            RefreshInactivePause();
        }

        private void OnApplicationPause(bool pause)
        {
            RefreshInactivePause();
        }

        private void RefreshInactivePause()
        {
            SoundSettingsManager settings = SoundSettingsManager.Instance;
            bool shouldHold =
                settings != null
                && settings.GetPauseWhenInactive()
                && !settings.IsApplicationActive;

            if (shouldHold)
            {
                HoldInactivePause();
            }
            else
            {
                ReleaseInactivePauseIfHeld();
            }
        }

        private void HoldInactivePause()
        {
            if (inactivePauseHeld)
            {
                return;
            }

            if (gameClock == null)
            {
                gameClock = KomayamaGameClock.Instance;
            }

            if (gameClock == null)
            {
                return;
            }

            gameClock.PushPause();
            inactivePauseHeld = true;
        }

        private void ReleaseInactivePauseIfHeld()
        {
            if (!inactivePauseHeld)
            {
                return;
            }

            if (gameClock != null)
            {
                gameClock.PopPause();
            }

            inactivePauseHeld = false;
        }
    }
}
