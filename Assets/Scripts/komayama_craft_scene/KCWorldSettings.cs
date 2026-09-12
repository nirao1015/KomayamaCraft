using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KCWorldSettings : MonoBehaviour
    {
        [Header("カメラ")]
        [SerializeField, Min(0f)]
        [Tooltip("WASDまたは矢印キーでカメラを動かしたときの速度。値を上げると同じ時間でより遠くまで移動します。")]
        private float wasdMoveSpeed = 6f;

        [SerializeField, Min(0f)]
        [Tooltip("何もない場所を左ドラッグして、下の土地を引っ張る強さ。1でマウスと同じ距離。大きくすると同じドラッグでより遠くまで動きます。")]
        private float mapDragPullStrength = 1f;

        [SerializeField, Min(0.1f), InspectorName("ズーム下限")]
        [Tooltip("マウスホイールでズームインしたときの直交サイズの下限。小さいほど寄れる。今の初期値は 2.5。")]
        private float zoomMinimumOrthographicSize = 2.5f;

        [SerializeField, Min(0.1f), InspectorName("ズーム上限")]
        [Tooltip("マウスホイールでズームアウトしたときの直交サイズの上限。大きいほど引ける。今の初期値は 8。")]
        private float zoomMaximumOrthographicSize = 8f;

        public float WasdMoveSpeed => wasdMoveSpeed;

        public float MapDragPullStrength => Mathf.Max(0f, mapDragPullStrength);

        public float ZoomMinimumOrthographicSize =>
            Mathf.Max(0.1f, zoomMinimumOrthographicSize);

        public float ZoomMaximumOrthographicSize =>
            Mathf.Max(0.1f, zoomMaximumOrthographicSize);
    }
}
