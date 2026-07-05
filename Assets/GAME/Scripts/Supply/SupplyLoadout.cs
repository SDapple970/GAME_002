using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Supply
{
    [Serializable]
    public sealed class SupplyLoadout
    {
        [SerializeField] private List<string> itemIds = new();
        [SerializeField] private List<int> itemCounts = new();

        public IReadOnlyList<string> ItemIds => itemIds;
        public IReadOnlyList<int> ItemCounts => itemCounts;

        public void SetItem(string itemId, int count)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            int existingIndex = itemIds.IndexOf(itemId);
            int safeCount = Mathf.Max(0, count);
            if (safeCount <= 0)
            {
                if (existingIndex >= 0)
                {
                    itemIds.RemoveAt(existingIndex);
                    itemCounts.RemoveAt(existingIndex);
                }

                return;
            }

            if (existingIndex >= 0)
            {
                itemCounts[existingIndex] = safeCount;
                return;
            }

            itemIds.Add(itemId);
            itemCounts.Add(safeCount);
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            int index = itemIds.IndexOf(itemId);
            return index >= 0 && index < itemCounts.Count ? Mathf.Max(0, itemCounts[index]) : 0;
        }

        public void Clear()
        {
            itemIds.Clear();
            itemCounts.Clear();
        }

        public SupplyLoadout Clone()
        {
            SupplyLoadout clone = new();
            for (int i = 0; i < itemIds.Count; i++)
            {
                string itemId = itemIds[i];
                int count = i < itemCounts.Count ? itemCounts[i] : 0;
                clone.SetItem(itemId, count);
            }

            return clone;
        }

        public void ReplaceWith(IReadOnlyList<string> ids, IReadOnlyList<int> counts)
        {
            Clear();
            if (ids == null || counts == null)
                return;

            int count = Mathf.Min(ids.Count, counts.Count);
            for (int i = 0; i < count; i++)
                SetItem(ids[i], counts[i]);
        }
    }
}
