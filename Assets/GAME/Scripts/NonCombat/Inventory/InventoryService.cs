using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Inventory
{
    public sealed class InventoryService : MonoBehaviour
    {
        public static InventoryService Instance { get; private set; }

        private readonly Dictionary<string, int> _items = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void AddItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;

            _items.TryGetValue(itemId, out int current);
            _items[itemId] = current + count;
            Debug.Log($"[Inventory] {itemId} +{count} => {_items[itemId]}", this);
        }

        public bool TryRemoveItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!_items.TryGetValue(itemId, out int current) || current < count) return false;

            int next = current - count;
            if (next <= 0)
                _items.Remove(itemId);
            else
                _items[itemId] = next;

            Debug.Log($"[Inventory] {itemId} -{count} => {next}", this);
            return true;
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _items.TryGetValue(itemId, out int count) ? count : 0;
        }

        public Dictionary<string, int> ExportItems() => new(_items);

        public void ImportItems(Dictionary<string, int> items)
        {
            _items.Clear();
            if (items == null) return;

            foreach (KeyValuePair<string, int> pair in items)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
                    _items[pair.Key] = pair.Value;
            }
        }
    }
}
