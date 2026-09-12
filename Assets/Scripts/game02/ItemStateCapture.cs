using System.Collections.Generic;
using UnityEngine;

namespace Game02
{
    internal static class ItemStateCapture
    {
        public static List<SpawnedItemState> CaptureLooseItems()
        {
            var result = new List<SpawnedItemState>();
            ItemSpawnController spawner = Object.FindObjectOfType<ItemSpawnController>(true);
            RectTransform itemCanvas = spawner != null ? spawner.ItemCanvas : null;
            if (itemCanvas == null)
            {
                return result;
            }

            DraggableItemController[] items = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
            for (int i = 0; i < items.Length; i++)
            {
                DraggableItemController item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.transform.parent != itemCanvas)
                {
                    continue;
                }

                RectTransform rect = item.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                var state = new SpawnedItemState
                {
                    itemType = item.ItemType,
                    itemName = item.name ?? string.Empty,
                    parentPath = itemCanvas.name,
                    anchoredX = rect.anchoredPosition.x,
                    anchoredY = rect.anchoredPosition.y
                };

                ItemStreamSpawnPopularity stream = item.GetComponent<ItemStreamSpawnPopularity>();
                if (stream != null)
                {
                    state.streamSpawnPopularity = stream.GetSpawnPopularity();
                    state.streamGenre = stream.GetStreamGenre();
                }

                ItemMoviePower movie = item.GetComponent<ItemMoviePower>();
                if (movie != null)
                {
                    state.moviePower = movie.GetFinalPopularity();
                    state.movieBuzzGain = movie.GetBuzzGainValue();
                    state.movieName = movie.GetMovieName();
                }

                result.Add(state);
            }

            return result;
        }

        public static void ApplyLooseItems(List<SpawnedItemState> items)
        {
            ItemSpawnController spawner = Object.FindObjectOfType<ItemSpawnController>(true);
            if (spawner == null || spawner.ItemCanvas == null)
            {
                return;
            }

            RectTransform itemCanvas = spawner.ItemCanvas;
            DraggableItemController[] existing = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                DraggableItemController item = existing[i];
                if (item != null && item.transform.parent == itemCanvas)
                {
                    Object.Destroy(item.gameObject);
                }
            }

            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                SpawnedItemState state = items[i];
                if (state == null)
                {
                    continue;
                }

                Vector2 pos = new Vector2(state.anchoredX, state.anchoredY);
                long? streamPopularity = state.itemType == ItemType.ItemStream ? state.streamSpawnPopularity : null;
                if (!spawner.TrySpawnItem(state.itemType, pos, false, out DraggableItemController spawned, streamPopularity, true))
                {
                    continue;
                }

                if (spawned == null)
                {
                    continue;
                }

                spawned.name = string.IsNullOrEmpty(state.itemName) ? spawned.name : state.itemName;
                ItemStreamSpawnPopularity stream = spawned.GetComponent<ItemStreamSpawnPopularity>();
                if (stream != null)
                {
                    stream.ApplySpawnPopularity(state.streamSpawnPopularity);
                    stream.ApplyStreamGenre(state.streamGenre);
                }

                ItemMoviePower movie = spawned.GetComponent<ItemMoviePower>();
                if (movie != null)
                {
                    movie.InitializeMovieStats(state.moviePower, state.movieBuzzGain, state.movieName);
                }

                RectTransform rect = spawned.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = pos;
                }

                spawned.RefreshBaseStateFromCurrentTransform();
            }

            EnsureSingleItemMailChara(itemCanvas);
            spawner.NotifyPlacementLayoutNeedsRefresh();
        }

        private static void EnsureSingleItemMailChara(RectTransform itemCanvas)
        {
            if (itemCanvas == null)
            {
                return;
            }

            DraggableItemController[] all = itemCanvas.GetComponentsInChildren<DraggableItemController>(true);
            DraggableItemController keep = null;
            for (int i = 0; i < all.Length; i++)
            {
                DraggableItemController item = all[i];
                if (item == null || item.ItemType != ItemType.ItemMailChara)
                {
                    continue;
                }

                if (keep == null)
                {
                    keep = item;
                    continue;
                }

                Object.Destroy(item.gameObject);
            }
        }
    }
}
