using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// Layer_Objects/宇宙船 の見た目段階（ship_1 → ship_2 …）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaShipVisual : MonoBehaviour
    {
        public const int StageInitial = 0;
        public const int StageAfterRainLeak = 1;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] stageSprites = new Sprite[2];
        [SerializeField, Min(0)] private int stageIndex;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public int StageIndex => stageIndex;
        public Sprite CurrentSprite =>
            spriteRenderer != null ? spriteRenderer.sprite : null;

        public Vector3 WorldCenter =>
            spriteRenderer != null
                ? spriteRenderer.bounds.center
                : transform.position;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            ApplyStage(stageIndex, force: true);
        }

        public void SetStage(int stage, bool force = false)
        {
            ApplyStage(stage, force);
        }

        public void ApplyStage(int stage, bool force)
        {
            int clamped = Mathf.Max(0, stage);
            if (!force && clamped == stageIndex &&
                spriteRenderer != null &&
                spriteRenderer.sprite != null)
            {
                return;
            }

            stageIndex = clamped;
            if (spriteRenderer == null ||
                stageSprites == null ||
                stageSprites.Length == 0)
            {
                return;
            }

            int i = Mathf.Min(stageIndex, stageSprites.Length - 1);
            if (stageSprites[i] != null)
            {
                spriteRenderer.sprite = stageSprites[i];
            }
        }

        public void SetSpriteColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        public Color GetSpriteColor()
        {
            return spriteRenderer != null ? spriteRenderer.color : Color.white;
        }
    }
}
