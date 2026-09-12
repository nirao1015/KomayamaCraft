using UnityEngine;

namespace KomayamaCraft
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KCSpriteFrame : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("見た目の枠の幅（ワールド単位）。画像はこの幅を超えないように、縦横比を保って収まります。")]
        private float width = 2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("見た目の枠の高さ（ワールド単位）。画像はこの高さを超えないように、縦横比を保って収まります。")]
        private float height = 2f;

        [SerializeField]
        [Tooltip("表示するSpriteRenderer。未設定なら同じオブジェクトのものを使います。")]
        private SpriteRenderer spriteRenderer;

        private Sprite lastSprite;

        public Vector2 FrameSize => new(width, height);

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && spriteRenderer.sprite != lastSprite)
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

            spriteRenderer.color = Color.white;
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

        private void OnDrawGizmosSelected()
        {
            Transform space = transform.parent != null ? transform.parent : transform;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.matrix = space.localToWorldMatrix;
            Gizmos.DrawWireCube(transform.localPosition, new Vector3(width, height, 0f));
        }
    }
}
