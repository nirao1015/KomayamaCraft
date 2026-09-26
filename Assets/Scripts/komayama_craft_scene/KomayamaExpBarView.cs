using UnityEngine;
using UnityEngine.UI;

namespace KomayamaCraft
{
    /// <summary>
    /// 経験値バー見た目。Fill は 0〜1（レベル内割合）。
    /// 現状はプレビュー用に 1（100%）固定。進行データ接続は後続。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaExpBarView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField, Range(0f, 1f), Tooltip("仮表示。本番接続までは 1＝100% 見た目確認用。")]
        private float previewFillAmount = 1f;

        private void Awake()
        {
            ApplyFill(previewFillAmount);
        }

        private void OnValidate()
        {
            ApplyFill(previewFillAmount);
        }

        /// <summary>0〜1。後続で ProgressService のレベル内割合を渡す。</summary>
        public void SetFillAmount(float normalized01)
        {
            ApplyFill(Mathf.Clamp01(normalized01));
        }

        private void ApplyFill(float amount)
        {
            if (fillImage == null)
            {
                return;
            }

            if (fillImage.type != Image.Type.Filled)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            fillImage.fillAmount = Mathf.Clamp01(amount);
        }
    }
}
