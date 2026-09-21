using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// クエストログ1行（仮実装）。タイトル・本文・± 切替。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaQuestLogRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button expandButton;
        [SerializeField] private TMP_Text expandButtonLabel;
        [SerializeField] private GameObject bodyRoot;
        [SerializeField] private TMP_SpriteAsset keyIconSpriteAsset;

        private string questId = string.Empty;
        private Action<string> onToggleRequested;

        public string QuestId => questId;

        public void Bind(Action<string> toggleRequested)
        {
            onToggleRequested = toggleRequested;
            EnsureBodySpriteAsset();
            if (expandButton != null)
            {
                expandButton.onClick.RemoveListener(HandleExpandClicked);
                expandButton.onClick.AddListener(HandleExpandClicked);
            }
        }

        public void SetQuest(string id, string title, string body)
        {
            questId = id ?? string.Empty;
            EnsureBodySpriteAsset();
            if (titleText != null)
            {
                titleText.richText = true;
                titleText.enableWordWrapping = true;
                titleText.text = title ?? string.Empty;
                titleText.ForceMeshUpdate();
            }

            if (bodyText != null)
            {
                bodyText.richText = true;
                bodyText.enableWordWrapping = true;
                bodyText.text = body ?? string.Empty;
                bodyText.ForceMeshUpdate();
            }

            // 折返し後の preferredHeight を LayoutGroup に反映する
            var self = transform as RectTransform;
            if (self != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(self);
            }
        }

        private void EnsureBodySpriteAsset()
        {
            if (bodyText == null)
            {
                return;
            }

            if (keyIconSpriteAsset != null)
            {
                bodyText.spriteAsset = keyIconSpriteAsset;
            }
        }

        public void SetExpandControlsVisible(bool visible)
        {
            if (expandButton != null)
            {
                expandButton.gameObject.SetActive(visible);
            }
        }

        public void SetExpanded(bool expanded)
        {
            if (bodyRoot != null)
            {
                bodyRoot.SetActive(expanded);
            }
            else if (bodyText != null)
            {
                bodyText.gameObject.SetActive(expanded);
            }

            if (expandButtonLabel != null)
            {
                expandButtonLabel.text = expanded ? "−" : "＋";
            }
        }

        private void HandleExpandClicked()
        {
            onToggleRequested?.Invoke(questId);
        }

        private void OnDestroy()
        {
            if (expandButton != null)
            {
                expandButton.onClick.RemoveListener(HandleExpandClicked);
            }
        }
    }
}
