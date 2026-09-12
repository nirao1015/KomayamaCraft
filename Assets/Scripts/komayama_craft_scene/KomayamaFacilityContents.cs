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

            while (remaining > 0 && dropArea != null)
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
