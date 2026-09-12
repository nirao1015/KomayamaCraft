using System;
using System.Collections.Generic;
using UnityEngine;

namespace KomayamaCraft
{
    [DisallowMultipleComponent]
    public sealed class KomayamaHandInventory : MonoBehaviour
    {
        [SerializeField] private KCItemSettings itemSettings;
        [SerializeField] private List<ItemDefinition> held = new();

        public ItemDefinition Item => GetSortedItem(0);
        public int Amount => CountOf(Item);
        public int Capacity => itemSettings != null
            ? Mathf.Clamp(itemSettings.HandCapacity, 1, itemSettings.HandCapacityMax)
            : 10;
        public int MaxCapacity => itemSettings != null ? itemSettings.HandCapacityMax : 100;
        public int TotalCount => held.Count;
        public bool IsEmpty => held.Count <= 0;
        public event Action Changed;

        public bool CanAccept(ItemDefinition candidate)
        {
            return candidate != null && held.Count < Capacity;
        }

        public string GetAddFailureReason(ItemDefinition candidate)
        {
            if (candidate == null)
            {
                return "対象がありません";
            }

            if (held.Count >= Capacity)
            {
                return "手持ちが満杯です";
            }

            return "回収できません";
        }

        public bool TryAdd(ItemDefinition candidate, int quantity = 1)
        {
            if (candidate == null || quantity <= 0)
            {
                return false;
            }

            int accepted = Mathf.Min(quantity, Capacity - held.Count);
            if (accepted <= 0)
            {
                return false;
            }

            for (int i = 0; i < accepted; i++)
            {
                held.Add(candidate);
            }

            Changed?.Invoke();
            return accepted == quantity;
        }

        public bool TryRemoveOne(out ItemDefinition removedItem)
        {
            return TryRemoveOne(Item, out removedItem);
        }

        public bool TryRemoveOne(ItemDefinition required, out ItemDefinition removedItem)
        {
            removedItem = null;
            if (required == null)
            {
                return false;
            }

            for (int i = held.Count - 1; i >= 0; i--)
            {
                if (held[i] != required)
                {
                    continue;
                }

                removedItem = held[i];
                held.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public int CountOf(ItemDefinition required)
        {
            if (required == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (held[i] == required)
                {
                    count++;
                }
            }

            return count;
        }

        public bool CanConsume(ItemDefinition required, int quantity)
        {
            return required != null && quantity > 0 && CountOf(required) >= quantity;
        }

        public bool TryConsume(ItemDefinition required, int quantity)
        {
            if (!CanConsume(required, quantity))
            {
                return false;
            }

            int remaining = quantity;
            for (int i = held.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (held[i] != required)
                {
                    continue;
                }

                held.RemoveAt(i);
                remaining--;
            }

            Changed?.Invoke();
            return true;
        }

        public ItemDefinition FindFirst(Predicate<ItemDefinition> match)
        {
            if (match == null)
            {
                return null;
            }

            List<ItemDefinition> sorted = GetSortedItems();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (match(sorted[i]))
                {
                    return sorted[i];
                }
            }

            return null;
        }

        public List<ItemDefinition> GetSortedItems()
        {
            var sorted = new List<ItemDefinition>(held);
            var firstIndex = new Dictionary<ItemDefinition, int>();
            for (int i = 0; i < held.Count; i++)
            {
                ItemDefinition item = held[i];
                if (item == null || firstIndex.ContainsKey(item))
                {
                    continue;
                }

                firstIndex.Add(item, i);
            }

            sorted.Sort((left, right) => CompareByFirstSeen(left, right, firstIndex));
            return sorted;
        }

        public void Clear()
        {
            held.Clear();
            Changed?.Invoke();
        }

        public void Restore(ItemDefinition restoredItem, int restoredAmount)
        {
            Clear();
            if (restoredItem == null || restoredAmount <= 0)
            {
                return;
            }

            TryAdd(restoredItem, restoredAmount);
        }

        public void Restore(IReadOnlyList<ItemStackSaveDto> stacks)
        {
            held.Clear();
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    ItemStackSaveDto stack = stacks[i];
                    if (stack == null ||
                        !GameDataCatalogs.KomayamaItems.TryGet(stack.itemDefinitionId, out ItemDefinition item))
                    {
                        continue;
                    }

                    int amount = Mathf.Max(0, stack.amount);
                    for (int n = 0; n < amount && held.Count < Capacity; n++)
                    {
                        held.Add(item);
                    }
                }
            }

            Changed?.Invoke();
        }

        public void CaptureStacks(List<ItemStackSaveDto> destination)
        {
            destination.Clear();
            List<ItemDefinition> sorted = GetSortedItems();
            ItemDefinition current = null;
            int amount = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                ItemDefinition item = sorted[i];
                if (item == current)
                {
                    amount++;
                    continue;
                }

                if (current != null && amount > 0)
                {
                    destination.Add(new ItemStackSaveDto
                    {
                        itemDefinitionId = current.DefinitionId,
                        amount = amount
                    });
                }

                current = item;
                amount = 1;
            }

            if (current != null && amount > 0)
            {
                destination.Add(new ItemStackSaveDto
                {
                    itemDefinitionId = current.DefinitionId,
                    amount = amount
                });
            }
        }

        private ItemDefinition GetSortedItem(int index)
        {
            List<ItemDefinition> sorted = GetSortedItems();
            return index >= 0 && index < sorted.Count ? sorted[index] : null;
        }

        private static int CompareByFirstSeen(
            ItemDefinition left,
            ItemDefinition right,
            Dictionary<ItemDefinition, int> firstIndex)
        {
            int leftIndex = left != null && firstIndex.ContainsKey(left)
                ? firstIndex[left]
                : int.MaxValue;
            int rightIndex = right != null && firstIndex.ContainsKey(right)
                ? firstIndex[right]
                : int.MaxValue;
            return leftIndex.CompareTo(rightIndex);
        }
    }
}
