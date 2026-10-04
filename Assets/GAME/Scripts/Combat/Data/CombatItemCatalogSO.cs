using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Data
{
    [CreateAssetMenu(menuName = "Game/Combat/Item Catalog")]
    public sealed class CombatItemCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CombatItemDefinitionSO> items = new();

        public IReadOnlyList<CombatItemDefinitionSO> Items => items;

        public bool TryGet(string itemId, out CombatItemDefinitionSO definition)
        {
            string normalized = NormalizeItemId(itemId);
            for (int i = 0; i < items.Count; i++)
            {
                CombatItemDefinitionSO candidate = items[i];
                if (candidate != null && string.Equals(candidate.ItemId, normalized, StringComparison.Ordinal))
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
            if (issues == null)
                return;

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                CombatItemDefinitionSO item = items[i];
                string itemId = item?.ItemId;
                if (string.IsNullOrEmpty(itemId))
                    issues.Add($"Combat item entry {i} has no stable inventory item ID.");
                else if (!ids.Add(itemId))
                    issues.Add($"Duplicate combat item ID '{itemId}'.");
                else if (!item.HasConfiguredEffect)
                    issues.Add($"Combat item '{itemId}' has no configured combat effect.");
            }
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        }
    }
}
