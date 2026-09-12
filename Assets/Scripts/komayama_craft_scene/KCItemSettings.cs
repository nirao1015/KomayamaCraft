using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KCItemSettings : MonoBehaviour
    {
        [Header("アイテム")]
        [SerializeField]
        [Tooltip("地面に落ちたアイテムの見た目の幅と高さ（ワールド単位）。絵を差し替えても、この枠の中に縦横比を保って収まります。全ドロップ品で共通です。")]
        private Vector2 visualSize = new(1f, 1f);

        [SerializeField, Min(0.01f)]
        [Tooltip("左クリックで拾える円の半径（ワールド単位）。見た目や押しのけとは別に変えられます。全ドロップ品で共通です。")]
        private float pickupRadius = 0.6f;

        [SerializeField, Min(0.01f)]
        [Tooltip("ドロップ品同士が重ならないように押しのける円の半径（ワールド単位）。見た目や取得判定とは別に変えられます。全ドロップ品で共通です。")]
        private float pushRadius = 0.4f;

        [Header("ドロップ散らばり")]
        [SerializeField, Min(0.01f), InspectorName("採集ドロップ円半径")]
        [Tooltip("素材生産オブジェクトの中心から、時計回りに置く円の半径（ワールド単位）。円上で重なる場合は、新しい1個をそこに固定して周りを押しのけます。半径は増やしません。")]
        private float firstDropRadius = 2.5f;

        [Header("ドロップ操作")]
        [SerializeField, Min(0f), InspectorName("長押し判定までの時間")]
        [Tooltip("右クリックを押してから、連続ドロップが始まるまでの秒数。0に近いほどすぐ連打扱いになります。")]
        private float dropHoldStartDelay = 0.35f;

        [SerializeField, Min(0f), InspectorName("連続ドロップ間隔")]
        [Tooltip("右クリック長押し中に、次の1個を置くまでの秒数。0なら毎フレーム1個です。")]
        private float dropHoldRepeatSeconds = 0.12f;

        [Header("手持ち")]
        [SerializeField, Min(1)]
        [Tooltip("今ピックアップできる個数。テスト用は10。アップグレード後の上限は下の最大値です。")]
        private int handCapacity = 10;

        [SerializeField, Min(1)]
        [Tooltip("アップグレード等をすべて終えたときの手持ち上限。")]
        private int handCapacityMax = 100;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("カーソル右横に出すアイコンの大きさ。ドロップ品の見た目サイズに対する比率です。小さくするとドロップ品より小さく見えます。")]
        private float cursorIconScale = 0.45f;

        public Vector2 VisualSize => new(
            Mathf.Max(0.01f, visualSize.x),
            Mathf.Max(0.01f, visualSize.y));

        public float PickupRadius => Mathf.Max(0.01f, pickupRadius);

        public float PushRadius => Mathf.Max(0.01f, pushRadius);

        public float FirstDropRadius => Mathf.Max(0.01f, firstDropRadius);

        public float DropHoldStartDelay => Mathf.Max(0f, dropHoldStartDelay);

        public float DropHoldRepeatSeconds => Mathf.Max(0f, dropHoldRepeatSeconds);

        public float ItemFootprint => Mathf.Max(VisualSize.x, VisualSize.y);

        public int HandCapacity => Mathf.Max(1, handCapacity);

        public int HandCapacityMax => Mathf.Max(HandCapacity, handCapacityMax);

        public float CursorIconScale => Mathf.Clamp(cursorIconScale, 0.1f, 1f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            KomayamaDroppedItem[] dropped = FindObjectsByType<KomayamaDroppedItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < dropped.Length; i++)
            {
                dropped[i]?.ApplyPresentation();
            }
        }
#endif
    }
}
