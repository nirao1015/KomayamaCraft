using UnityEngine;

namespace KomayamaCraft
{
    /// <summary>
    /// 浜辺の蓄電クラゲ専用。自動ドロップ源＋クリックは現状 no-op。
    /// <see cref="KomayamaResourceNode"/>（鉄鱗獣などのクリック採集）とは別系統で、
    /// 採集入力・発生点定義・KCSpriteFrame を共有しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KomayamaVoltJellySpawner : MonoBehaviour
    {
        [SerializeField] private ItemDefinition dropItem;
        [SerializeField] private KomayamaDropArea dropArea;
        [SerializeField] private BoxCollider2D dropZone;

        [SerializeField, Min(0.1f)]
        private float dropIntervalSeconds = 3f;

        [SerializeField, Min(0)]
        private int maxItemsInZone = 2;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("ドロップ帯の縦方向割合（上下は均等余白）。")]
        private float lineHeightFraction = 0.7f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("線からの横ずれ σ（矩形半幅に対する割合）。")]
        private float lateralSigmaFraction = 0.18f;

        private float nextDropAt;

        private void Awake()
        {
            if (dropZone == null)
            {
                Transform zone = transform.Find("DropZone");
                if (zone != null)
                {
                    dropZone = zone.GetComponent<BoxCollider2D>();
                }
            }

            nextDropAt = Time.time + dropIntervalSeconds;
        }

        private void Update()
        {
            if (dropItem == null || dropArea == null || dropZone == null)
            {
                return;
            }

            if (Time.time < nextDropAt)
            {
                return;
            }

            nextDropAt = Time.time + dropIntervalSeconds;

            if (CountItemsInZone() >= maxItemsInZone)
            {
                return;
            }

            Vector2 worldPosition = SampleRugbyBallPosition();
            dropArea.TrySpawnAt(dropItem, 1, worldPosition, out _);
        }

        private int CountItemsInZone()
        {
            Bounds bounds = dropZone.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                bounds.center,
                bounds.size,
                dropZone.transform.eulerAngles.z);

            int count = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                KomayamaDroppedItem dropped =
                    hits[i].GetComponentInParent<KomayamaDroppedItem>();
                if (dropped == null || dropped.Item == null)
                {
                    continue;
                }

                if (dropped.Item == dropItem)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector2 SampleRugbyBallPosition()
        {
            Vector2 size = dropZone.size;
            Vector2 offset = dropZone.offset;
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;

            float lineHalfHeight = halfH * Mathf.Clamp01(lineHeightFraction);
            float localY = Random.Range(-lineHalfHeight, lineHalfHeight);

            float sigma = Mathf.Max(0.001f, halfW * lateralSigmaFraction);
            float localX = NextGaussian() * sigma;
            localX = Mathf.Clamp(localX, -halfW, halfW);

            Vector3 local = new(
                offset.x + localX,
                offset.y + localY,
                0f);
            return dropZone.transform.TransformPoint(local);
        }

        private static float NextGaussian()
        {
            // Box-Muller
            float u1 = Mathf.Max(1e-6f, Random.value);
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) *
                   Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
