using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaDropArea : MonoBehaviour
    {
        [SerializeField] private Collider2D dropAreaCollider;
        [SerializeField] private KomayamaDroppedItem droppedItemPrefab;
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField] private Transform droppedItemRoot;
        [SerializeField] private Tilemap noDropPaint;

        [SerializeField, Min(0f)]
        [Tooltip("ドロップ同士を離すときの余白（ワールド単位）。")]
        private float separationPadding = 0.02f;

        [SerializeField, Min(0.01f)]
        private float dropAnimationSeconds = 0.4f;

        [SerializeField, Min(0f)]
        private float dropAnimationArcHeight = 0f;

        [SerializeField, Min(0.01f)]
        private float pushAnimationSeconds = 0.45f;

        [Header("ねじ込み押しのけ")]
        [SerializeField, Range(0.15f, 0.9f), InspectorName("連鎖の減衰")]
        [Tooltip("1ホップごとに押し量を何倍にするか。小さいほど遠くはほとんど動きません。")]
        private float pushDecay = 0.55f;

        [SerializeField, Min(1), InspectorName("連鎖の最大ホップ")]
        [Tooltip("新しい1個から何個先まで力を伝えるか。")]
        private int pushMaxHops = 12;

        [SerializeField, Min(0), InspectorName("落ち着き反復")]
        [Tooltip("連鎖のあと、近傍の残り重なりを隙間へ落とす回数。")]
        private int settleIterations = 3;

        private readonly List<KomayamaDroppedItem> items = new();
        private readonly Dictionary<int, ScatterCursor> scatterCursors = new();
        private readonly SpatialHash hash = new();
        private readonly List<int> queryBuffer = new(32);
        private readonly Queue<PushWork> pushQueue = new();
        private readonly List<int> neighborhood = new(64);
        private int fallbackDirectionSeed;

        private struct PushWork
        {
            public int index;
            public int from;
            public int hop;
        }

        private struct ScatterCursor
        {
            public bool initialized;
            public float nextAngle;
        }

        public int ItemCount
        {
            get
            {
                PruneDestroyedItems();
                return items.Count;
            }
        }

        private int MaxGroundItems =>
            itemSettings != null
                ? itemSettings.MaxGroundItems
                : 1000;

        private void Awake()
        {
            if (dropAreaCollider == null)
            {
                dropAreaCollider = GetComponent<Collider2D>();
            }

            if (droppedItemRoot == null)
            {
                droppedItemRoot = transform;
            }
        }

        public bool Contains(Vector2 worldPosition)
        {
            return dropAreaCollider != null &&
                   dropAreaCollider.OverlapPoint(worldPosition);
        }

        public bool TrySpawnNear(
            ItemDefinition item,
            int amount,
            Vector2 sourcePosition,
            out KomayamaDroppedItem spawned)
        {
            return TrySpawn(
                item,
                amount,
                sourcePosition,
                true,
                null,
                out spawned);
        }

        public bool TrySpawnNear(
            ItemDefinition item,
            int amount,
            Transform source,
            out KomayamaDroppedItem spawned)
        {
            Vector2 origin = source != null ? (Vector2)source.position : Vector2.zero;
            return TrySpawn(
                item,
                amount,
                origin,
                true,
                source,
                out spawned);
        }

        public bool TrySpawnAt(
            ItemDefinition item,
            int amount,
            Vector2 worldPosition,
            out KomayamaDroppedItem spawned)
        {
            return TrySpawn(
                item,
                amount,
                worldPosition,
                false,
                null,
                out spawned);
        }

        /// <summary>
        /// セーブ復旧用。押しのけ・ドロップ演出なしで、指定座標にそのまま置く。
        /// </summary>
        public bool TrySpawnForLoad(
            ItemDefinition item,
            int amount,
            Vector2 worldPosition,
            string instanceId,
            out KomayamaDroppedItem spawned)
        {
            spawned = null;
            if (item == null || amount <= 0 || droppedItemPrefab == null)
            {
                return false;
            }

            PruneDestroyedItems();
            spawned = Instantiate(
                droppedItemPrefab,
                worldPosition,
                Quaternion.identity,
                droppedItemRoot != null ? droppedItemRoot : transform);
            spawned.Initialize(item, amount, this);
            spawned.SetItemSettings(itemSettings);
            spawned.RestoreIdentity(instanceId, worldPosition);
            items.Add(spawned);
            return true;
        }

        public void Unregister(KomayamaDroppedItem item)
        {
            if (item != null)
            {
                items.Remove(item);
            }
        }

        private bool TrySpawn(
            ItemDefinition item,
            int amount,
            Vector2 origin,
            bool randomize,
            Transform source,
            out KomayamaDroppedItem spawned)
        {
            spawned = null;
            if (item == null || amount <= 0 || droppedItemPrefab == null ||
                dropAreaCollider == null)
            {
                return false;
            }

            PruneDestroyedItems();
            if (items.Count >= MaxGroundItems)
            {
                return false;
            }

            if (randomize)
            {
                return TrySpawnNearSourceClockwise(
                    item,
                    amount,
                    origin,
                    source,
                    out spawned);
            }

            if (!IsPlacementAllowed(origin, null))
            {
                return false;
            }

            if (!TryInsertAt(origin, PushRadius, null, out Vector2[] resolved))
            {
                return false;
            }

            return FinishSpawn(item, amount, origin, resolved, out spawned);
        }

        private bool TrySpawnNearSourceClockwise(
            ItemDefinition item,
            int amount,
            Vector2 origin,
            Transform source,
            out KomayamaDroppedItem spawned)
        {
            spawned = null;
            float itemSize = itemSettings != null
                ? itemSettings.ItemFootprint
                : 1f;
            float firstRadius = itemSettings != null
                ? itemSettings.FirstDropRadius
                : 2.5f;
            int sourceKey = source != null
                ? source.GetInstanceID()
                : origin.GetHashCode();
            if (!scatterCursors.TryGetValue(sourceKey, out ScatterCursor cursor) ||
                !cursor.initialized)
            {
                cursor = new ScatterCursor
                {
                    initialized = true,
                    nextAngle = Mathf.PI * 0.5f
                };
            }

            float itemAngle = itemSize / Mathf.Max(0.01f, firstRadius);
            const int MaxNudges = 64;
            bool placed = false;
            Vector2 candidate = default;
            for (int nudge = 0; nudge < MaxNudges; nudge++)
            {
                candidate = origin + new Vector2(
                    Mathf.Cos(cursor.nextAngle),
                    Mathf.Sin(cursor.nextAngle)) * firstRadius;
                if (IsPlacementAllowed(candidate, source))
                {
                    placed = true;
                    break;
                }

                float blockedStep = Mathf.Max(0.05f, itemAngle * 0.2f);
                cursor.nextAngle -= blockedStep;
            }

            if (!placed)
            {
                scatterCursors[sourceKey] = cursor;
                return false;
            }

            if (!TryInsertAt(candidate, PushRadius, source, out Vector2[] resolved))
            {
                scatterCursors[sourceKey] = cursor;
                return false;
            }

            float advance = Random.Range(itemAngle * 0.25f, itemAngle * 2f);
            cursor.nextAngle -= advance;
            scatterCursors[sourceKey] = cursor;
            return FinishSpawn(item, amount, origin, resolved, out spawned);
        }

        private bool FinishSpawn(
            ItemDefinition item,
            int amount,
            Vector2 travelFrom,
            Vector2[] resolved,
            out KomayamaDroppedItem spawned)
        {
            int newIndex = resolved.Length - 1;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].MoveDropCenter(resolved[i], pushAnimationSeconds);
            }

            spawned = SpawnDropped(item, amount, travelFrom, resolved[newIndex]);
            return spawned != null;
        }

        private KomayamaDroppedItem SpawnDropped(
            ItemDefinition item,
            int amount,
            Vector2 travelFrom,
            Vector2 landAt)
        {
            KomayamaDroppedItem spawned = Object.Instantiate(
                droppedItemPrefab,
                travelFrom,
                Quaternion.identity,
                droppedItemRoot);
            spawned.Initialize(item, Mathf.Max(1, amount), this);
            spawned.SetItemSettings(itemSettings);
            spawned.SetDropCenter(landAt);
            spawned.PlayDropAnimation(
                travelFrom,
                dropAnimationSeconds,
                dropAnimationArcHeight);
            items.Add(spawned);
            return spawned;
        }

        private float PushRadius =>
            itemSettings != null
                ? itemSettings.PushRadius
                : droppedItemPrefab.OverlapRadius;

        private bool TryInsertAt(
            Vector2 seed,
            float newRadius,
            Transform source,
            out Vector2[] positions)
        {
            positions = null;
            if (!IsPlacementAllowed(seed, source))
            {
                return false;
            }

            int existingCount = items.Count;
            int newIndex = existingCount;
            positions = new Vector2[existingCount + 1];
            var radii = new float[positions.Length];
            float maxRadius = newRadius;
            for (int i = 0; i < existingCount; i++)
            {
                positions[i] = items[i].DropCenter;
                radii[i] = items[i].OverlapRadius;
                maxRadius = Mathf.Max(maxRadius, radii[i]);
            }

            positions[newIndex] = seed;
            radii[newIndex] = newRadius;

            float cellSize = Mathf.Max(0.25f, (maxRadius * 2f) + separationPadding);
            int hops = Mathf.Max(1, pushMaxHops);
            float decay = Mathf.Clamp(pushDecay, 0.15f, 0.9f);
            var visits = new int[positions.Length];
            pushQueue.Clear();

            RebuildHash(positions, cellSize);
            EnqueueOverlaps(
                positions,
                radii,
                newIndex,
                newIndex,
                0,
                hops,
                maxRadius);
            while (pushQueue.Count > 0)
            {
                PushWork work = pushQueue.Dequeue();
                if (work.index == newIndex ||
                    work.hop > hops ||
                    visits[work.index] >= 4)
                {
                    continue;
                }

                visits[work.index]++;
                float scale = work.hop <= 0
                    ? 1f
                    : Mathf.Pow(decay, work.hop);
                if (scale < 0.02f)
                {
                    continue;
                }

                Vector2 before = positions[work.index];
                if (!SeparateFrom(
                        ref positions[work.index],
                        radii[work.index],
                        positions[work.from],
                        radii[work.from],
                        scale,
                        work.index,
                        work.from,
                        source))
                {
                    continue;
                }

                if ((positions[work.index] - before).sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                RebuildHash(positions, cellSize);
                EnqueueOverlaps(
                    positions,
                    radii,
                    work.index,
                    newIndex,
                    work.hop + 1,
                    hops,
                    maxRadius);
            }

            SettleNeighborhood(positions, radii, seed, newIndex, maxRadius, hops, cellSize, source);
            positions[newIndex] = seed;
            return IsPlacementAllowed(positions[newIndex], source);
        }

        private void EnqueueOverlaps(
            Vector2[] positions,
            float[] radii,
            int fromIndex,
            int newIndex,
            int nextHop,
            int maxHops,
            float maxRadius)
        {
            if (nextHop > maxHops)
            {
                return;
            }

            float queryRadius = radii[fromIndex] + maxRadius + separationPadding;
            hash.Query(positions[fromIndex], queryRadius, queryBuffer);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                int other = queryBuffer[i];
                if (other == fromIndex || other == newIndex)
                {
                    continue;
                }

                float required = radii[fromIndex] + radii[other] + separationPadding;
                if ((positions[other] - positions[fromIndex]).sqrMagnitude >= required * required)
                {
                    continue;
                }

                pushQueue.Enqueue(new PushWork
                {
                    index = other,
                    from = fromIndex,
                    hop = nextHop
                });
            }
        }

        private bool SeparateFrom(
            ref Vector2 moving,
            float movingRadius,
            Vector2 origin,
            float originRadius,
            float scale,
            int movingIndex,
            int originIndex,
            Transform source)
        {
            Vector2 delta = moving - origin;
            float required = movingRadius + originRadius + separationPadding;
            float distance = delta.magnitude;
            if (distance >= required)
            {
                return false;
            }

            Vector2 direction = distance > 0.0001f
                ? delta / distance
                : FallbackDirection(movingIndex, originIndex);
            Vector2 before = moving;
            moving += direction * ((required - distance) * Mathf.Max(0f, scale));
            ClampMovedPosition(ref moving, before, source);
            return true;
        }

        private void SettleNeighborhood(
            Vector2[] positions,
            float[] radii,
            Vector2 seed,
            int newIndex,
            float maxRadius,
            int hops,
            float cellSize,
            Transform source)
        {
            int iterations = Mathf.Max(0, settleIterations);
            if (iterations == 0)
            {
                return;
            }

            float reach = maxRadius * 2f * (hops + 1) + 1f;
            const float SettleEpsilon = 0.0004f;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                RebuildHash(positions, cellSize);
                CollectNeighborhood(positions, seed, reach, newIndex);
                float traveled = 0f;
                for (int n = 0; n < neighborhood.Count; n++)
                {
                    int a = neighborhood[n];
                    hash.Query(
                        positions[a],
                        radii[a] + maxRadius + separationPadding,
                        queryBuffer);
                    for (int q = 0; q < queryBuffer.Count; q++)
                    {
                        int b = queryBuffer[q];
                        if (b <= a)
                        {
                            continue;
                        }

                        traveled += ResolveOverlap(
                            positions,
                            radii,
                            a,
                            b,
                            newIndex,
                            source);
                    }
                }

                if (traveled < SettleEpsilon)
                {
                    break;
                }
            }
        }

        private void CollectNeighborhood(
            Vector2[] positions,
            Vector2 seed,
            float reach,
            int newIndex)
        {
            neighborhood.Clear();
            float reachSqr = reach * reach;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == newIndex)
                {
                    continue;
                }

                if ((positions[i] - seed).sqrMagnitude <= reachSqr)
                {
                    neighborhood.Add(i);
                }
            }
        }

        private float ResolveOverlap(
            Vector2[] positions,
            float[] radii,
            int indexA,
            int indexB,
            int newIndex,
            Transform source)
        {
            Vector2 delta = positions[indexB] - positions[indexA];
            float required = radii[indexA] + radii[indexB] + separationPadding;
            float distance = delta.magnitude;
            if (distance >= required)
            {
                return 0f;
            }

            Vector2 direction = distance > 0.0001f
                ? delta / distance
                : FallbackDirection(indexA, indexB);
            float overlap = required - distance;
            if (indexA == newIndex)
            {
                return PushBy(positions, indexB, direction * overlap, source);
            }

            if (indexB == newIndex)
            {
                return PushBy(positions, indexA, -direction * overlap, source);
            }

            Vector2 half = direction * (overlap * 0.5f);
            return PushBy(positions, indexA, -half, source) +
                   PushBy(positions, indexB, half, source);
        }

        private float PushBy(
            Vector2[] positions,
            int index,
            Vector2 offset,
            Transform source)
        {
            Vector2 before = positions[index];
            positions[index] += offset;
            ClampMovedPosition(ref positions[index], before, source);
            return (positions[index] - before).magnitude;
        }

        private Vector2 FallbackDirection(int indexA, int indexB)
        {
            fallbackDirectionSeed++;
            float angle = ((indexA + 1) * 73856093 ^
                           (indexB + 1) * 19349663 ^
                           fallbackDirectionSeed * 83492791) *
                          0.000001f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void RebuildHash(Vector2[] positions, float cellSize)
        {
            hash.Rebuild(positions, cellSize);
        }

        private bool IsPlacementAllowed(Vector2 position, Transform source)
        {
            return Contains(position) && !IsBlockedByNoDropPaint(position);
        }

        private void ClampMovedPosition(
            ref Vector2 position,
            Vector2 from,
            Transform source)
        {
            if (IsPlacementAllowed(position, source))
            {
                return;
            }

            Vector2 best = from;
            const int Steps = 12;
            for (int step = 1; step <= Steps; step++)
            {
                Vector2 candidate = Vector2.Lerp(from, position, step / (float)Steps);
                if (IsPlacementAllowed(candidate, source))
                {
                    best = candidate;
                }
                else
                {
                    break;
                }
            }

            position = best;
        }

        private bool IsBlockedByNoDropPaint(Vector2 position)
        {
            return noDropPaint != null &&
                   noDropPaint.GetTile(noDropPaint.WorldToCell(position)) != null;
        }

        private void PruneDestroyedItems()
        {
            items.RemoveAll(item => item == null);
        }

        private sealed class SpatialHash
        {
            private readonly Dictionary<long, List<int>> cells = new();
            private readonly Stack<List<int>> pool = new();
            private float cellSize = 1f;

            public void Rebuild(Vector2[] positions, float size)
            {
                foreach (KeyValuePair<long, List<int>> pair in cells)
                {
                    pair.Value.Clear();
                    pool.Push(pair.Value);
                }

                cells.Clear();
                cellSize = Mathf.Max(0.1f, size);
                if (positions == null)
                {
                    return;
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    long key = Key(positions[i]);
                    if (!cells.TryGetValue(key, out List<int> list))
                    {
                        list = pool.Count > 0 ? pool.Pop() : new List<int>(8);
                        cells[key] = list;
                    }

                    list.Add(i);
                }
            }

            public void Query(Vector2 position, float radius, List<int> results)
            {
                results.Clear();
                int minX = Mathf.FloorToInt((position.x - radius) / cellSize);
                int maxX = Mathf.FloorToInt((position.x + radius) / cellSize);
                int minY = Mathf.FloorToInt((position.y - radius) / cellSize);
                int maxY = Mathf.FloorToInt((position.y + radius) / cellSize);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!cells.TryGetValue(Pack(x, y), out List<int> list))
                        {
                            continue;
                        }

                        for (int i = 0; i < list.Count; i++)
                        {
                            results.Add(list[i]);
                        }
                    }
                }
            }

            private long Key(Vector2 position)
            {
                int x = Mathf.FloorToInt(position.x / cellSize);
                int y = Mathf.FloorToInt(position.y / cellSize);
                return Pack(x, y);
            }

            private static long Pack(int x, int y)
            {
                return ((long)x << 32) ^ (uint)y;
            }
        }
    }
}
