using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// タイトル SlotPanel の1スロット分。
    /// Slot1Button の Rect / 色はシーン側の設定を尊重する。
    /// プレイ中は α=0、ホバー時のみ設定色（α含む）へ戻す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaTitleSlotCard : MonoBehaviour
    {
        [SerializeField] private int slotNumber = 1;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectHighlightImage;
        [SerializeField, Tooltip("ホバー時に戻す色。Image に α>0 の色が付いているときは Awake でそれを記憶する。")]
        private Color selectHoverColor = new Color(1f, 0.323f, 0.565f, 0.341f);
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image screenshotImage;
        [SerializeField] private TMP_Text saveTimeText;
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private Sprite emptyScreenshotSprite;
        [SerializeField] private Button deleteButton;

        private bool hoverColorCached;
        private bool pointerInside;
        private bool hoverHandlersWired;

        public int SlotNumber => slotNumber;
        public Button SelectButton => selectButton;
        public Button DeleteButton => deleteButton;

        public event Action<int> DeleteRequested;

        private void Awake()
        {
            CacheHoverColorIfNeeded();
            EnsureMaskPanelsPassTransparentHits();
            WireSelectHoverHandlers();
            ApplyHighlightVisible(false);
        }

        /// <summary>
        /// SlotPanel3（および同系の透過マスク）は不透明だけヒットし、透過は下の Slot1Button へ通す。
        /// Rect / 色は変更しない。
        /// </summary>
        private static void EnsureMaskPanelsPassTransparentHits()
        {
            Transform slotPanel = FindSlotPanelTransform();
            if (slotPanel == null)
            {
                return;
            }

            ConfigureAlphaHit(slotPanel.Find("SlotPanel3"));
        }

        private static Transform FindSlotPanelTransform()
        {
            // SlotCanvas 配下へ移設後も拾う（非アクティブ含む）
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "SlotPanel")
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void ConfigureAlphaHit(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            Image img = panel.GetComponent<Image>();
            if (img == null)
            {
                return;
            }

            img.raycastTarget = true;
            if (img.alphaHitTestMinimumThreshold <= 0f)
            {
                img.alphaHitTestMinimumThreshold = 0.1f;
            }
        }

        private void OnDisable()
        {
            pointerInside = false;
            ApplyHighlightVisible(false);
        }

        public void Apply(KomayamaSaveSlotInfo info, bool isLastPlayed, bool selectable)
        {
            CacheHoverColorIfNeeded();
            WireSelectHoverHandlers();

            string lastMark = isLastPlayed && info.HasData ? " 前回のプレイ" : string.Empty;
            if (nameText != null)
            {
                nameText.text = $"スロット{info.Slot}{lastMark}";
            }

            if (saveTimeText != null)
            {
                if (!info.HasData)
                {
                    saveTimeText.text = "空き";
                }
                else if (info.LastSavedAtLocal.HasValue)
                {
                    saveTimeText.text = info.LastSavedAtLocal.Value.ToString(
                        "yyyy/MM/dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    saveTimeText.text = "セーブ時刻 —";
                }
            }

            if (playTimeText != null)
            {
                if (!info.HasData)
                {
                    playTimeText.text = string.Empty;
                }
                else if (info.PlaySeconds.HasValue)
                {
                    playTimeText.text = "プレイ " + FormatPlayTime(info.PlaySeconds.Value);
                }
                else
                {
                    playTimeText.text = "プレイ --:--";
                }
            }

            ApplyScreenshot(info);

            if (selectButton != null)
            {
                selectButton.interactable = selectable;
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(info.HasData);
                deleteButton.interactable = info.HasData;
            }

            if (!pointerInside)
            {
                ApplyHighlightVisible(false);
            }
        }

        public void WireDeleteClick()
        {
            if (deleteButton == null)
            {
                return;
            }

            deleteButton.onClick.RemoveListener(OnDeleteClicked);
            deleteButton.onClick.AddListener(OnDeleteClicked);
        }

        private void OnDeleteClicked()
        {
            DeleteRequested?.Invoke(slotNumber > 0 ? slotNumber : 1);
        }

        private void WireSelectHoverHandlers()
        {
            if (hoverHandlersWired || selectButton == null)
            {
                return;
            }

            EventTrigger trigger = selectButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = selectButton.gameObject.AddComponent<EventTrigger>();
            }

            EnsureTrigger(trigger, EventTriggerType.PointerEnter, _ =>
            {
                pointerInside = true;
                ApplyHighlightVisible(true);
            });
            EnsureTrigger(trigger, EventTriggerType.PointerExit, _ =>
            {
                pointerInside = false;
                ApplyHighlightVisible(false);
            });
            hoverHandlersWired = true;
        }

        private static void EnsureTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            for (int i = 0; i < trigger.triggers.Count; i++)
            {
                if (trigger.triggers[i].eventID == type)
                {
                    trigger.triggers[i].callback.RemoveAllListeners();
                    trigger.triggers[i].callback.AddListener(action);
                    return;
                }
            }

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        private void CacheHoverColorIfNeeded()
        {
            if (hoverColorCached)
            {
                return;
            }

            Image highlight = ResolveHighlightImage();
            // Image にユーザーが付けた α>0 の色があれば、それをホバー色として記憶するだけ。
            // α=0 のときはシリアライズ済み selectHoverColor を壊さない。
            if (highlight != null && highlight.color.a > 0.001f)
            {
                selectHoverColor = highlight.color;
            }

            hoverColorCached = true;
        }

        private Image ResolveHighlightImage()
        {
            if (selectHighlightImage != null)
            {
                return selectHighlightImage;
            }

            if (selectButton != null)
            {
                return selectButton.targetGraphic as Image ?? selectButton.GetComponent<Image>();
            }

            return null;
        }

        private void ApplyHighlightVisible(bool visible)
        {
            Image highlight = ResolveHighlightImage();
            if (highlight == null)
            {
                return;
            }

            if (visible)
            {
                highlight.color = selectHoverColor;
                return;
            }

            Color idle = selectHoverColor;
            idle.a = 0f;
            highlight.color = idle;
        }

        private void ApplyScreenshot(KomayamaSaveSlotInfo info)
        {
            if (screenshotImage == null)
            {
                return;
            }

            Texture2D loaded = null;
            if (info.HasData && !string.IsNullOrEmpty(info.ThumbnailPath) &&
                System.IO.File.Exists(info.ThumbnailPath))
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(info.ThumbnailPath);
                    loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!loaded.LoadImage(bytes))
                    {
                        Destroy(loaded);
                        loaded = null;
                    }
                }
                catch
                {
                    if (loaded != null)
                    {
                        Destroy(loaded);
                        loaded = null;
                    }
                }
            }

            if (screenshotImage.sprite != null &&
                screenshotImage.sprite != emptyScreenshotSprite &&
                screenshotImage.sprite.texture != null)
            {
                Texture2D previous = screenshotImage.sprite.texture;
                Destroy(screenshotImage.sprite);
                if (previous != null)
                {
                    Destroy(previous);
                }
            }

            if (loaded != null)
            {
                screenshotImage.sprite = Sprite.Create(
                    loaded,
                    new Rect(0f, 0f, loaded.width, loaded.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                screenshotImage.color = Color.white;
                screenshotImage.preserveAspect = true;
            }
            else
            {
                screenshotImage.sprite = emptyScreenshotSprite;
                screenshotImage.color = emptyScreenshotSprite != null
                    ? Color.white
                    : new Color(0f, 0f, 0f, 0f);
            }
        }

        private static string FormatPlayTime(float seconds)
        {
            if (seconds < 0f)
            {
                seconds = 0f;
            }

            int total = Mathf.FloorToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0)
            {
                return $"{h}:{m:00}:{s:00}";
            }

            return $"{m:00}:{s:00}";
        }
    }
}
