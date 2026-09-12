using TMPro;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// TimeUIBackground 配下の TimeUI に、ゲーム内経過時間を 1 秒ごとに表示する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game02ElapsedTimeUiController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text timeUiText;
        [SerializeField] private string timeUiChildName = "TimeUI";

        private const int MaxDisplaySeconds = (999 * 3600) + (59 * 60) + 59;
        private int lastDisplayedWholeSeconds = -1;

        public static Game02ElapsedTimeUiController EnsureSceneController()
        {
            GameObject root = GameObject.Find("PanelCanvas/PanelObject/TimeUIBackground");
            if (root == null)
            {
                root = FindInactiveObjectByName("TimeUIBackground");
            }

            if (root == null)
            {
                return null;
            }

            Game02ElapsedTimeUiController controller = root.GetComponent<Game02ElapsedTimeUiController>();
            if (controller == null)
            {
                controller = root.AddComponent<Game02ElapsedTimeUiController>();
            }

            return controller;
        }

        private static GameObject FindInactiveObjectByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == objectName)
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private void Awake()
        {
            ResolveTimeUiTextIfNeeded();
            RefreshTimeText(force: true);
        }

        private void Update()
        {
            RefreshTimeText(force: false);
        }

        private void ResolveTimeUiTextIfNeeded()
        {
            if (timeUiText != null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(timeUiChildName))
            {
                Transform byName = transform.Find(timeUiChildName);
                if (byName != null)
                {
                    timeUiText = byName.GetComponent<TMP_Text>();
                    if (timeUiText != null)
                    {
                        return;
                    }
                }
            }

            TMP_Text[] allText = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < allText.Length; i++)
            {
                TMP_Text candidate = allText[i];
                if (candidate != null && candidate.name == "TimeUI")
                {
                    timeUiText = candidate;
                    return;
                }
            }
        }

        private void RefreshTimeText(bool force)
        {
            ResolveTimeUiTextIfNeeded();
            if (timeUiText == null)
            {
                return;
            }

            float rawElapsedSeconds = 0f;
            if (GameManager.Instance != null)
            {
                rawElapsedSeconds = Mathf.Max(0f, GameManager.Instance.GameplayElapsedSeconds);
            }

            int wholeSeconds = Mathf.Clamp(Mathf.FloorToInt(rawElapsedSeconds), 0, MaxDisplaySeconds);
            if (!force && wholeSeconds == lastDisplayedWholeSeconds)
            {
                return;
            }

            lastDisplayedWholeSeconds = wholeSeconds;
            timeUiText.text = FormatElapsed(wholeSeconds);
        }

        private static string FormatElapsed(int seconds)
        {
            int clamped = Mathf.Clamp(seconds, 0, MaxDisplaySeconds);
            int hours = clamped / 3600;
            int minutes = (clamped % 3600) / 60;
            int secs = clamped % 60;

            if (hours > 0)
            {
                return $"{hours}h:{minutes:00}m:{secs:00}s";
            }

            return $"{minutes:00}m:{secs:00}s";
        }
    }
}
