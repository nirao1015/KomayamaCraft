using System.Collections.Generic;
using UnityEngine;

namespace Game02
{
    /// <summary>
    /// ItemStream の予約排他を管理する。予約対象が無効化/破棄された場合は自動解放する。
    /// </summary>
    public sealed class ItemStreamReservationRegistry : MonoBehaviour
    {
        private static readonly Dictionary<int, int> ReservedByItemInstanceId = new Dictionary<int, int>();

        [SerializeField] private int itemInstanceId;
        [SerializeField] private int ownerRequesterId = -1;

        public static bool TryReserve(DraggableItemController item, int requesterId)
        {
            if (item == null || item.ItemType != ItemType.ItemStream || requesterId == 0)
            {
                return false;
            }

            int streamItemInstanceId = item.gameObject.GetInstanceID();
            if (ReservedByItemInstanceId.TryGetValue(streamItemInstanceId, out int existingOwner))
            {
                if (existingOwner != requesterId)
                {
                    return false;
                }
            }
            else
            {
                ReservedByItemInstanceId[streamItemInstanceId] = requesterId;
            }

            ItemStreamReservationRegistry registry = item.GetComponent<ItemStreamReservationRegistry>();
            if (registry == null)
            {
                registry = item.gameObject.AddComponent<ItemStreamReservationRegistry>();
            }

            if (registry != null)
            {
                registry.itemInstanceId = streamItemInstanceId;
                registry.ownerRequesterId = requesterId;
            }

            return true;
        }

        public static bool IsReservedByOther(DraggableItemController item, int requesterId)
        {
            if (item == null)
            {
                return false;
            }

            int streamItemInstanceId = item.gameObject.GetInstanceID();
            if (!ReservedByItemInstanceId.TryGetValue(streamItemInstanceId, out int owner))
            {
                return false;
            }

            return owner != requesterId;
        }

        public static void Release(DraggableItemController item, int requesterId)
        {
            if (item == null)
            {
                return;
            }

            ReleaseByInstanceId(item.gameObject.GetInstanceID(), requesterId);
        }

        private static void ReleaseByInstanceId(int streamItemInstanceId, int requesterId)
        {
            if (!ReservedByItemInstanceId.TryGetValue(streamItemInstanceId, out int owner))
            {
                return;
            }

            if (requesterId != 0 && owner != requesterId)
            {
                return;
            }

            ReservedByItemInstanceId.Remove(streamItemInstanceId);
        }

        private void OnDisable()
        {
            ReleaseByInstanceId(itemInstanceId, ownerRequesterId);
        }

        private void OnDestroy()
        {
            ReleaseByInstanceId(itemInstanceId, ownerRequesterId);
        }
    }
}
