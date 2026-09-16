using UnityEngine;

namespace KomayamaCraft
{
    public static class KomayamaFacilityContents
    {
        public static void GiveOrDrop(
            ItemDefinition item,
            int amount,
            KomayamaHandInventory hand,
            KomayamaDropArea dropArea,
            Vector2 dropPosition)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            int remaining = amount;
            while (remaining > 0 && hand != null && hand.TryAdd(item))
            {
                remaining--;
            }

            DropOnly(item, remaining, dropArea, dropPosition);
        }

        /// <summary>手持ちへ入れず、常に地面へドロップする。</summary>
        public static void DropOnly(
            ItemDefinition item,
            int amount,
            KomayamaDropArea dropArea,
            Vector2 dropPosition)
        {
            if (item == null || amount <= 0 || dropArea == null)
            {
                return;
            }

            int remaining = amount;
            while (remaining > 0)
            {
                if (!dropArea.TrySpawnNear(item, 1, dropPosition, out _))
                {
                    break;
                }

                remaining--;
            }
        }
    }
}
