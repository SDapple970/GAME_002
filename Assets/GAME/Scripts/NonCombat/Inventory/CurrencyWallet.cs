using UnityEngine;
using Game.NonCombat.Save;

namespace Game.NonCombat.Inventory
{
    public sealed class CurrencyWallet : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static CurrencyWallet Instance { get; private set; }

        [SerializeField] private int gold;

        public int Gold => gold;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            gold += amount;
            Debug.Log($"[Currency] Gold +{amount} => {gold}", this);
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (gold < amount) return false;

            gold -= amount;
            Debug.Log($"[Currency] Gold -{amount} => {gold}", this);
            return true;
        }

        public void SetGold(int value) => gold = Mathf.Max(0, value);

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.currency ??= new CurrencySaveData();
            saveData.currency.gold = gold;
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            if (saveData?.currency == null)
                return;

            SetGold(saveData.currency.gold);
        }
    }
}
