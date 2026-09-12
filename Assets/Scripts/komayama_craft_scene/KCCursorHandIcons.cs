using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KCCursorHandIcons : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform iconRoot;
        [SerializeField] private Vector2 cursorOffset = new(18f, 0f);
        [SerializeField, Min(1)] private int wrapCount = 10;

        private readonly List<Image> icons = new();
        private readonly List<ItemDefinition> sorted = new();

        private void Awake()
        {
            if (iconRoot == null)
            {
                iconRoot = transform as RectTransform;
            }

            if (hand != null)
            {
                hand.Changed += RefreshIcons;
            }

            TMPro.TMP_Text label = GetComponent<TMPro.TMP_Text>();
            if (label != null)
            {
                label.enabled = false;
                label.raycastTarget = false;
            }

            CollectExistingIcons();
        }

        private void CollectExistingIcons()
        {
            icons.Clear();
            if (iconRoot == null)
            {
                return;
            }

            Image[] found = iconRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || found[i].transform == iconRoot)
                {
                    continue;
                }

                found[i].raycastTarget = false;
                icons.Add(found[i]);
            }
        }

        private void OnDestroy()
        {
            if (hand != null)
            {
                hand.Changed -= RefreshIcons;
            }
        }

        private void LateUpdate()
        {
            FollowCursor();
            if (hand == null || hand.IsEmpty)
            {
                SetVisibleCount(0);
                return;
            }

            RefreshIcons();
        }

        private void FollowCursor()
        {
            if (iconRoot == null || UnityEngine.InputSystem.Mouse.current == null)
            {
                return;
            }

            iconRoot.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue() +
                cursorOffset;
        }

        private void RefreshIcons()
        {
            if (iconRoot == null || hand == null)
            {
                return;
            }

            sorted.Clear();
            sorted.AddRange(hand.GetSortedItems());
            SetVisibleCount(sorted.Count);
            float iconSize = ResolveIconSize();
            int columns = Mathf.Max(1, wrapCount);
            for (int i = 0; i < sorted.Count; i++)
            {
                Image icon = icons[i];
                icon.sprite = sorted[i] != null ? sorted[i].Icon : null;
                icon.enabled = icon.sprite != null;
                icon.color = Color.white;
                icon.raycastTarget = false;
                int column = i % columns;
                int row = i / columns;
                RectTransform rect = icon.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(iconSize, iconSize);
                rect.anchoredPosition = new Vector2(
                    column * (iconSize + 2f),
                    -row * (iconSize + 2f));
            }
        }

        private float ResolveIconSize()
        {
            float world = 0.45f;
            if (itemSettings != null)
            {
                world = itemSettings.ItemFootprint * itemSettings.CursorIconScale;
            }

            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null || !camera.orthographic)
            {
                return 28f;
            }

            return Mathf.Max(
                12f,
                world * camera.pixelHeight / (camera.orthographicSize * 2f));
        }

        private void SetVisibleCount(int count)
        {
            while (icons.Count < count)
            {
                var iconObject = new GameObject("HandIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(iconRoot, false);
                Image image = iconObject.GetComponent<Image>();
                image.raycastTarget = false;
                icons.Add(image);
            }

            for (int i = 0; i < icons.Count; i++)
            {
                icons[i].gameObject.SetActive(i < count);
            }
        }
    }
}
