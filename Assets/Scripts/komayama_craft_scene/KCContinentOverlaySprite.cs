using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// Layer_Continent 配下のバイオーム／TMP フォルダ用オーバーレイ画像。
    /// 地域マス（<see cref="KCWorldRegionCell"/>）とは別。位置・回転は自動調整しない。
    /// 既定は自由変形（Scale／回転は Scene／Inspector のまま）。
    /// 枠フィットが必要なときだけ autoFitToDisplaySize か ContextMenu で適用する。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class KCContinentOverlaySprite : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("枠フィット時の表示枠サイズ（ワールド単位）。自由変形中は無視。")]
        private Vector2 displaySize = new(19.2f, 10.8f);

        [SerializeField]
        [Tooltip("枠フィット時: ON=アスペクト比維持（contain）／OFF=枠へ引き伸ばし。")]
        private bool preserveAspect = true;

        [SerializeField]
        [Tooltip("ON: Display Size に合わせて Scale を自動更新。OFF: Scale／回転を自由に編集（推奨）。")]
        private bool autoFitToDisplaySize;

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private Sprite lastSprite;
        private bool lastPreserveAspect;
        private bool lastAutoFit;
        private Vector2 lastDisplaySize;

        public Vector2 DisplaySize => displaySize;
        public bool PreserveAspect => preserveAspect;
        public bool AutoFitToDisplaySize => autoFitToDisplaySize;

        private void OnEnable()
        {
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
            else
            {
                CacheSpriteOnly();
            }
        }

        private void OnValidate()
        {
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
            else
            {
                CacheSpriteOnly();
            }
        }

        private void LateUpdate()
        {
            if (!autoFitToDisplaySize)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.sprite != lastSprite ||
                preserveAspect != lastPreserveAspect ||
                autoFitToDisplaySize != lastAutoFit ||
                displaySize != lastDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        public void SetDisplaySize(Vector2 size)
        {
            if (size.x > 0f && size.y > 0f)
            {
                displaySize = size;
            }

            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        public void SetPreserveAspect(bool value)
        {
            preserveAspect = value;
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        public void SetAutoFitToDisplaySize(bool value)
        {
            autoFitToDisplaySize = value;
            if (autoFitToDisplaySize)
            {
                ApplyDisplaySizeFit();
            }
        }

        [ContextMenu("Fit To Display Size Now")]
        public void ApplyDisplaySizeFit()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            // 位置・回転・色は触らない。Scale のみ枠に合わせる。
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            lastSprite = spriteRenderer.sprite;
            lastPreserveAspect = preserveAspect;
            lastAutoFit = autoFitToDisplaySize;
            lastDisplaySize = displaySize;

            if (spriteRenderer.sprite == null ||
                displaySize.x <= 0f ||
                displaySize.y <= 0f)
            {
                return;
            }

            Vector2 native = spriteRenderer.sprite.bounds.size;
            float nx = Mathf.Max(0.0001f, native.x);
            float ny = Mathf.Max(0.0001f, native.y);

            float scaleX;
            float scaleY;
            if (preserveAspect)
            {
                float uniform = Mathf.Min(displaySize.x / nx, displaySize.y / ny);
                scaleX = uniform;
                scaleY = uniform;
            }
            else
            {
                scaleX = displaySize.x / nx;
                scaleY = displaySize.y / ny;
            }

            Vector3 localScale = transform.localScale;
            localScale.x = scaleX;
            localScale.y = scaleY;
            transform.localScale = localScale;
        }

        private void CacheSpriteOnly()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                lastSprite = spriteRenderer.sprite;
            }

            lastPreserveAspect = preserveAspect;
            lastAutoFit = autoFitToDisplaySize;
            lastDisplaySize = displaySize;
        }
    }
}
