using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Supply
{
    public sealed class SupplyLoadoutService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        [SerializeField] private SupplyLoadout currentLoadout = new();

        public SupplyLoadout GetSnapshot()
        {
            return currentLoadout != null ? currentLoadout.Clone() : new SupplyLoadout();
        }

        public void AddItem(string itemId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
                return;

            currentLoadout ??= new SupplyLoadout();
            int nextCount = currentLoadout.GetCount(itemId) + count;
            currentLoadout.SetItem(itemId, nextCount);
        }

        public void RemoveItem(string itemId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0 || currentLoadout == null)
                return;

            int nextCount = currentLoadout.GetCount(itemId) - count;
            currentLoadout.SetItem(itemId, nextCount);
        }

        public void ClearLoadout()
        {
            currentLoadout ??= new SupplyLoadout();
            currentLoadout.Clear();
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.futureDaily ??= new FutureDailySaveData();
            saveData.futureDaily.selectedSupplyItemIds.Clear();
            saveData.futureDaily.selectedSupplyItemCounts.Clear();

            SupplyLoadout snapshot = GetSnapshot();
            for (int i = 0; i < snapshot.ItemIds.Count; i++)
            {
                string itemId = snapshot.ItemIds[i];
                int count = i < snapshot.ItemCounts.Count ? snapshot.ItemCounts[i] : 0;
                if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
                    continue;

                saveData.futureDaily.selectedSupplyItemIds.Add(itemId);
                saveData.futureDaily.selectedSupplyItemCounts.Add(count);
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            currentLoadout ??= new SupplyLoadout();
            currentLoadout.ReplaceWith(
                saveData?.futureDaily?.selectedSupplyItemIds,
                saveData?.futureDaily?.selectedSupplyItemCounts);
        }
    }
}
