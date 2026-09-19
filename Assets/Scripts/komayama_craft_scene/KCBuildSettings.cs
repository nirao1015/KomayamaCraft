using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 建設共通設定（ブロック寸法・ブループリント色など）。全施設で共有。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KCBuildSettings : MonoBehaviour
    {
        [SerializeField, Min(0.01f), InspectorName("ブロック辺長")]
        [Tooltip("建設の吸い付き単位。建設不可マス1マスと同じ。後から変えられる。")]
        private float blockSize = 0.5f;

        [SerializeField, InspectorName("仮組・配置可の色")]
        private Color blueprintValidColor = new Color(0.35f, 0.85f, 1f, 0.65f);

        [SerializeField, InspectorName("仮組・配置不可の色")]
        private Color blueprintInvalidColor = new Color(1f, 0.25f, 0.25f, 0.65f);

        [SerializeField, InspectorName("建設禁止カーソル")]
        private Texture2D forbidCursorTexture;

        [SerializeField]
        private Vector2 forbidCursorHotspot = new Vector2(16f, 16f);

        [SerializeField, Min(0f), InspectorName("施設ドロップ起点の下オフセット")]
        [Tooltip("占有矩形の下辺中央から、さらに下へずらす距離（全施設共通）。")]
        private float facilityDropOffsetBelowFootprint = 0.35f;

        public float BlockSize => Mathf.Max(0.01f, blockSize);

        public Color BlueprintValidColor => blueprintValidColor;

        public Color BlueprintInvalidColor => blueprintInvalidColor;

        public Texture2D ForbidCursorTexture => forbidCursorTexture;

        public Vector2 ForbidCursorHotspot => forbidCursorHotspot;

        public float FacilityDropOffsetBelowFootprint =>
            Mathf.Max(0f, facilityDropOffsetBelowFootprint);
    }
}
