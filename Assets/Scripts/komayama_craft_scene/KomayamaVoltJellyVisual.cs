using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 蓄電クラゲ専用の見た目枠。鉄鱗獣などの <see cref="KCSpriteFrame"/> とは別系統。
    /// Inspector の tint／alpha・Preserve 材を維持したまま、枠へ contain する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KomayamaVoltJellyVisual : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        private float width = 2f;

        [SerializeField, Min(0.01f)]
        private float height = 2f;

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private Sprite lastSprite;
        private Color lastColor = new(1f, 1f, 1f, 0.7f);

        public Vector2 FrameSize => new(width, height);

        private void OnEnable()
        {
            CacheColor();
            Apply();
        }

        private void OnValidate()
        {
            CacheColor();
            Apply();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            // Inspector / 実行時に変えた tint・alpha を記憶（枠再適用で消さない）
            if (spriteRenderer.color != lastColor)
            {
                lastColor = spriteRenderer.color;
            }

            if (spriteRenderer.sprite != lastSprite)
            {
                Apply();
            }
        }

        public void SetFrameSize(float frameWidth, float frameHeight)
        {
            if (frameWidth > 0f)
            {
                width = frameWidth;
            }

            if (frameHeight > 0f)
            {
                height = frameHeight;
            }

            Apply();
        }

        public void Apply()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = lastColor;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            lastSprite = spriteRenderer.sprite;
            transform.localScale = Vector3.one;
            if (spriteRenderer.sprite == null || width <= 0f || height <= 0f)
            {
                SyncParentCollider();
                return;
            }

            Vector2 native = spriteRenderer.sprite.bounds.size;
            float scale = Mathf.Min(
                width / Mathf.Max(0.0001f, native.x),
                height / Mathf.Max(0.0001f, native.y));
            transform.localScale = new Vector3(scale, scale, 1f);
            SyncParentCollider();
        }

        private void CacheColor()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                lastColor = spriteRenderer.color;
            }
        }

        private void SyncParentCollider()
        {
            Transform parent = transform.parent;
            if (parent == null || !parent.TryGetComponent(out BoxCollider2D box))
            {
                return;
            }

            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds world = spriteRenderer.bounds;
                Vector3 localCenter = parent.InverseTransformPoint(world.center);
                Vector3 localSize = parent.InverseTransformVector(world.size);
                box.size = new Vector2(
                    Mathf.Max(0.01f, Mathf.Abs(localSize.x)),
                    Mathf.Max(0.01f, Mathf.Abs(localSize.y)));
                box.offset = new Vector2(localCenter.x, localCenter.y);
                return;
            }

            box.size = new Vector2(width, height);
            box.offset = Vector2.zero;
        }
    }
}
