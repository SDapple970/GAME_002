using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Inventory
{
    [CreateAssetMenu(menuName = "GAME/NonCombat/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ItemDefinitionSO> items = new();

        public IReadOnlyList<ItemDefinitionSO> Items => items;

        public bool TryGet(string itemId, out ItemDefinitionSO definition)
        {
            string normalized = InventoryService.NormalizeItemId(itemId);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinitionSO candidate = items[i];
                if (candidate != null && string.Equals(InventoryService.NormalizeItemId(candidate.ItemId), normalized, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public void CollectValidationIssues(List<string> issues)
        {
            if (issues == null) return;
            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinitionSO item = items[i];
                string id = InventoryService.NormalizeItemId(item != null ? item.ItemId : null);
                if (id == null) issues.Add($"Item catalog entry {i} has no stable item ID.");
                else if (!ids.Add(id)) issues.Add($"Duplicate item ID '{id}' in item catalog.");
            }
        }
    }
}
