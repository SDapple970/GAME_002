using System;
using System.Collections.Generic;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Inventory
{
    public sealed class InventoryService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static InventoryService Instance { get; private set; }

        [SerializeField] private ItemCatalogSO itemCatalog;
        private readonly Dictionary<string, int> _items = new(StringComparer.Ordinal);

        public event Action<InventoryMutationResult> Changed;
        public event Action Refreshed;

        public ItemCatalogSO ItemCatalog => itemCatalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddItem(string itemId, int count) => TryAddItem(itemId, count);

        public InventoryMutationResult TryAddItem(string itemId, int count)
        {
            string id = NormalizeItemId(itemId);
            if (id == null) return Result(id, count, 0, InventoryMutationStatus.InvalidItemId);
            if (count <= 0) return Result(id, count, 0, InventoryMutationStatus.InvalidAmount);
            if (!TryResolveStackLimit(id, out int limit)) return Result(id, count, 0, InventoryMutationStatus.UnknownDefinition);

            int current = GetCount(id);
            int capacity = limit > 0 ? limit - current : int.MaxValue - current;
            if (capacity <= 0)
                return Result(id, count, 0, limit > 0 ? InventoryMutationStatus.StackLimitReached : InventoryMutationStatus.OverflowPrevented);

            int applied = Math.Min(count, capacity);
            int next = current + applied;
            _items[id] = next;
            InventoryMutationStatus status = applied == count ? InventoryMutationStatus.Success : InventoryMutationStatus.Partial;
            InventoryMutationResult result = new(id, count, applied, next, status);
            Changed?.Invoke(result);
            return result;
        }

        public bool CanAdd(string itemId, int count) => TryEvaluateAdd(itemId, count).AppliedAmount == count;

        private InventoryMutationResult TryEvaluateAdd(string itemId, int count)
        {
            string id = NormalizeItemId(itemId);
            if (id == null) return Result(id, count, 0, InventoryMutationStatus.InvalidItemId);
            if (count <= 0) return Result(id, count, 0, InventoryMutationStatus.InvalidAmount);
            if (!TryResolveStackLimit(id, out int limit)) return Result(id, count, 0, InventoryMutationStatus.UnknownDefinition);
            int current = GetCount(id);
            int capacity = limit > 0 ? limit - current : int.MaxValue - current;
            int applied = Math.Max(0, Math.Min(count, capacity));
            return new InventoryMutationResult(id, count, applied, current, applied == count ? InventoryMutationStatus.Success : applied > 0 ? InventoryMutationStatus.Partial : limit > 0 ? InventoryMutationStatus.StackLimitReached : InventoryMutationStatus.OverflowPrevented);
        }

        public bool TryRemoveItem(string itemId, int count) => TryRemoveItemDetailed(itemId, count).Status == InventoryMutationStatus.Success;

        public InventoryMutationResult TryRemoveItemDetailed(string itemId, int count)
        {
            string id = NormalizeItemId(itemId);
            if (id == null) return Result(id, count, 0, InventoryMutationStatus.InvalidItemId);
            if (count <= 0) return Result(id, count, 0, InventoryMutationStatus.InvalidAmount);
            int current = GetCount(id);
            if (current < count) return new InventoryMutationResult(id, count, 0, current, InventoryMutationStatus.InsufficientQuantity);
            int next = current - count;
            if (next == 0) _items.Remove(id); else _items[id] = next;
            InventoryMutationResult result = new(id, count, count, next, InventoryMutationStatus.Success);
            Changed?.Invoke(result);
            return result;
        }

        public int GetCount(string itemId)
        {
            string id = NormalizeItemId(itemId);
            return id != null && _items.TryGetValue(id, out int count) ? count : 0;
        }

        public IReadOnlyDictionary<string, int> GetSnapshot() => new Dictionary<string, int>(_items, StringComparer.Ordinal);
        public Dictionary<string, int> ExportItems() => new(_items, StringComparer.Ordinal);

        public void ImportItems(Dictionary<string, int> items)
        {
            _items.Clear();
            if (items != null)
                foreach (KeyValuePair<string, int> pair in items)
                {
                    string id = NormalizeItemId(pair.Key);
                    if (id != null && pair.Value > 0) _items[id] = pair.Value;
                }
            Refreshed?.Invoke();
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.inventory ??= new InventorySaveData();
            saveData.inventory.items.Clear();
            foreach (KeyValuePair<string, int> pair in _items)
                if (pair.Value > 0) saveData.inventory.items.Add(new SaveIntEntry { id = pair.Key, value = pair.Value });
            saveData.inventory.items.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            Dictionary<string, int> restored = new(StringComparer.Ordinal);
            if (saveData?.inventory?.items != null)
                foreach (SaveIntEntry entry in saveData.inventory.items)
                {
                    string id = NormalizeItemId(entry?.id);
                    if (id == null || entry.value <= 0) continue;
                    restored.TryGetValue(id, out int current);
                    restored[id] = current > int.MaxValue - entry.value ? int.MaxValue : current + entry.value;
                }
            ImportItems(restored);
        }

        internal static string NormalizeItemId(string itemId) => string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();

        private bool TryResolveStackLimit(string itemId, out int maximum)
        {
            maximum = 0;
            if (itemCatalog == null) return true;
            if (!itemCatalog.TryGet(itemId, out ItemDefinitionSO definition)) return false;
            maximum = definition.MaximumStackCount;
            return true;
        }

        private InventoryMutationResult Result(string id, int requested, int applied, InventoryMutationStatus status) =>
            new(id, requested, applied, GetCount(id), status);
    }
}
