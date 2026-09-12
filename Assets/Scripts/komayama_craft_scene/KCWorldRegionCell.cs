using UnityEngine;

namespace KomayamaCraft
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KCWorldRegionCell : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("この地域マスの表示サイズ（ワールド単位）。差し替える画像の解像度やPixels Per Unitが変わっても、この大きさのまま表示します。")]
        private Vector2 cellSize = new(19.2f, 10.8f);

        [SerializeField]
        [Tooltip("地域マスの見た目。未設定なら同じオブジェクトのSpriteRendererを使います。")]
        private SpriteRenderer spriteRenderer;

        private Sprite lastSprite;

        public Vector2 CellSize => cellSize;

        private void OnEnable()
        {
            ApplyDisplay();
        }

        private void OnValidate()
        {
            ApplyDisplay();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && spriteRenderer.sprite != lastSprite)
            {
                ApplyDisplay();
            }
        }

        public void SetCellSize(Vector2 size)
        {
            if (size.x > 0f && size.y > 0f)
            {
                cellSize = size;
            }

            ApplyDisplay();
        }

        public void ApplyDisplay()
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
            if (spriteRenderer.sprite == null || cellSize.x <= 0f || cellSize.y <= 0f)
            {
                return;
            }

            Vector2 native = spriteRenderer.sprite.bounds.size;
            float scaleX = cellSize.x / Mathf.Max(0.0001f, native.x);
            float scaleY = cellSize.y / Mathf.Max(0.0001f, native.y);
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }
}
