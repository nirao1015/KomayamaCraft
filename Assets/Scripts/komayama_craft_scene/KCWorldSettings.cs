using UnityEngine;
using UnityEngine.Serialization;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KCWorldSettings : MonoBehaviour
    {
        [Header("カメラ")]
        [SerializeField, Min(0f), InspectorName("WASD 横速度")]
        [FormerlySerializedAs("wasdMoveSpeed")]
        [Tooltip("A/D・左右矢印（および斜めの横成分）。ワールド単位／秒。")]
        private float wasdMoveSpeedX = 24f;

        [SerializeField, Min(0f), InspectorName("WASD 縦速度")]
        [Tooltip("W/S・上下矢印（および斜めの縦成分）。ワールド単位／秒。")]
        private float wasdMoveSpeedY = 24f;

        [SerializeField, Min(0f)]
        [Tooltip("何もない場所を左ドラッグして、下の土地を引っ張る強さ。1でマウスと同じ距離。大きくすると同じドラッグでより遠くまで動きます。")]
        private float mapDragPullStrength = 1f;

        [SerializeField, Min(0.1f), InspectorName("ズーム下限"), Tooltip(
            "マウスホイールで最も寄ったときの Orthographic Size。カメラ演出もこの値を上限寄りとして使う。正本は本コンポーネント（KCConfigValues）。")]
        private float zoomMinimumOrthographicSize = 2.5f;

        [SerializeField, Min(0.1f), InspectorName("ズーム上限"), Tooltip(
            "マウスホイールで最も引いたときの Orthographic Size。正本は本コンポーネント（KCConfigValues）。")]
        private float zoomMaximumOrthographicSize = 8f;

        public float WasdMoveSpeedX => wasdMoveSpeedX;
        public float WasdMoveSpeedY => wasdMoveSpeedY;

        /// <summary>互換。横速度を返す。</summary>
        public float WasdMoveSpeed => wasdMoveSpeedX;

        public float MapDragPullStrength => Mathf.Max(0f, mapDragPullStrength);

        public float ZoomMinimumOrthographicSize =>
            Mathf.Max(0.1f, zoomMinimumOrthographicSize);

        public float ZoomMaximumOrthographicSize =>
            Mathf.Max(0.1f, zoomMaximumOrthographicSize);
    }
}
