using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// menuスキル／図鑑／ステータス用の画面ひな形（背景＋戻る）。中身は後続。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaCraftPlaceholderMenuController : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = string.Empty;
            public Button openButton;
            public GameObject root;
            public Button backButton;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private KomayamaBuildMenuSlide buildMenuSlide;
        [SerializeField] private KomayamaGameClock gameClock;

        private int openIndex = -1;
        private bool pauseHeld;

        public static KomayamaCraftPlaceholderMenuController Instance { get; private set; }

        public bool IsOpen => openIndex >= 0;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                int captured = i;
                if (entry.openButton != null)
                {
                    entry.openButton.onClick.AddListener(() => Open(captured));
                }

                if (entry.backButton != null)
                {
                    entry.backButton.onClick.AddListener(CloseAll);
                }

                if (entry.root != null)
                {
                    entry.root.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.openButton != null)
                {
                    entry.openButton.onClick.RemoveAllListeners();
                }

                if (entry.backButton != null)
                {
                    entry.backButton.onClick.RemoveAllListeners();
                }
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseAll();
            }
        }

        public void Open(int index)
        {
            if (index < 0 || index >= entries.Length || entries[index] == null || entries[index].root == null)
            {
                return;
            }

            KomayamaQuestController quest = KomayamaQuestController.Instance;
            if (quest != null && !quest.AreBuildAndSettingsUnlocked)
            {
                return;
            }

            if (KomayamaCraftMainMenuController.Instance != null &&
                KomayamaCraftMainMenuController.Instance.IsOpen)
            {
                KomayamaCraftMainMenuController.Instance.CloseFully();
            }

            if (buildMenuSlide != null && buildMenuSlide.IsOpen)
            {
                buildMenuSlide.Close();
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry?.root == null)
                {
                    continue;
                }

                entry.root.SetActive(i == index);
            }

            openIndex = index;
            HoldPause();
        }

        public void CloseAll()
        {
            if (!IsOpen)
            {
                ReleasePause();
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry?.root != null)
                {
                    entry.root.SetActive(false);
                }
            }

            openIndex = -1;
            ReleasePause();
        }

        private void HoldPause()
        {
            if (pauseHeld || gameClock == null)
            {
                return;
            }

            gameClock.PushPause();
            pauseHeld = true;
        }

        private void ReleasePause()
        {
            if (!pauseHeld || gameClock == null)
            {
                return;
            }

            gameClock.PopPause();
            pauseHeld = false;
        }
    }
}
