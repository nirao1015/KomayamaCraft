using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KCMouseHandView : MonoBehaviour
    {
        [SerializeField] private KomayamaHandInventory hand;
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform itemsRoot;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private SpriteRenderer[] iconRenderers = System.Array.Empty<SpriteRenderer>();
        [SerializeField] private Vector2 worldOffset = new(0.35f, 0f);
        [SerializeField] private Vector2 countLocalPosition = Vector2.zero;
        [SerializeField, Min(1)] private int wrapCount = 10;
        [SerializeField, Min(1)] private int extraGapEvery = 5;
        [SerializeField, Min(0f)] private float extraGapItemCount = 0.2f;
        [SerializeField, Min(0f)] private float iconSpacing = 0.04f;

        private readonly List<ItemDefinition> sorted = new();

        private void Awake()
        {
            if (hand != null)
            {
                hand.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (hand != null)
            {
                hand.Changed -= Refresh;
            }
        }

        private void LateUpdate()
        {
            FollowCursor();
            Refresh();
        }

        private void FollowCursor()
        {
            if (targetCamera == null || Mouse.current == null)
            {
                return;
            }

            Vector2 screen = Mouse.current.position.ReadValue();
            if (!targetCamera.pixelRect.Contains(screen))
            {
                return;
            }

            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(
                screen.x,
                screen.y,
                -targetCamera.transform.position.z));
            world.z = 0f;
            transform.position = world + (Vector3)worldOffset;
        }

        private void Refresh()
        {
            sorted.Clear();
            if (hand != null && !hand.IsEmpty)
            {
                sorted.AddRange(hand.GetSortedItems());
            }

            float iconSize = ResolveIconSize();
            int columns = Mathf.Max(1, wrapCount);
            int visible = Mathf.Min(sorted.Count, iconRenderers.Length);
            float rowPitch = iconSize + iconSpacing;
            for (int i = 0; i < iconRenderers.Length; i++)
            {
                SpriteRenderer renderer = iconRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                bool show = i < visible && sorted[i] != null && sorted[i].Icon != null;
                renderer.enabled = show;
                renderer.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                renderer.sprite = sorted[i].Icon;
                renderer.color = Color.white;
                renderer.sortingLayerName = "WorldMouse";
                renderer.sortingOrder = 10;
                ApplyContainScale(renderer, iconSize);
                int column = i % columns;
                int row = i / columns;
                renderer.transform.localPosition = new Vector3(
                    ResolveColumnX(column, iconSize),
                    -rowPitch - row * rowPitch,
                    0f);
            }

            if (countText == null)
            {
                return;
            }

            countText.transform.localPosition = countLocalPosition;
            if (hand == null || hand.IsEmpty)
            {
                countText.enabled = false;
                countText.text = string.Empty;
                return;
            }

            countText.enabled = true;
            countText.text = $"{hand.TotalCount}/{hand.Capacity}";
        }

        private float ResolveColumnX(int column, float iconSize)
        {
            float x = column * (iconSize + iconSpacing);
            int extraSlots = extraGapEvery > 0 ? column / extraGapEvery : 0;
            return x + extraSlots * extraGapItemCount * iconSize;
        }

        private float ResolveIconSize()
        {
            if (itemSettings == null)
            {
                return 0.45f;
            }

            return itemSettings.ItemFootprint * itemSettings.CursorIconScale;
        }

        private static void ApplyContainScale(SpriteRenderer renderer, float frame)
        {
            if (renderer.sprite == null || frame <= 0f)
            {
                renderer.transform.localScale = Vector3.one;
                return;
            }

            Vector2 native = renderer.sprite.bounds.size;
            float scale = Mathf.Min(
                frame / Mathf.Max(0.0001f, native.x),
                frame / Mathf.Max(0.0001f, native.y));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
