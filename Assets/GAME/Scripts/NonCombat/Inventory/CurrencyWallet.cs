using System;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Inventory
{
    public sealed class CurrencyWallet : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static CurrencyWallet Instance { get; private set; }
        [SerializeField] private int gold;

        public int Gold => gold;
        public event Action<CurrencyMutationResult> Changed;
        public event Action Refreshed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddGold(int amount) => TryAddGold(amount);

        public CurrencyMutationResult TryAddGold(int amount)
        {
            if (amount <= 0) return new CurrencyMutationResult(amount, 0, gold, CurrencyMutationStatus.InvalidAmount);
            if (gold > int.MaxValue - amount) return new CurrencyMutationResult(amount, 0, gold, CurrencyMutationStatus.OverflowPrevented);
            gold += amount;
            CurrencyMutationResult result = new(amount, amount, gold, CurrencyMutationStatus.Success);
            Changed?.Invoke(result);
            return result;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (gold < amount) return false;
            gold -= amount;
            Changed?.Invoke(new CurrencyMutationResult(-amount, -amount, gold, CurrencyMutationStatus.Success));
            return true;
        }

        public void SetGold(int value)
        {
            int next = Mathf.Max(0, value);
            if (next == gold) return;
            int previous = gold;
            gold = next;
            Changed?.Invoke(new CurrencyMutationResult(next - previous, next - previous, gold, CurrencyMutationStatus.Success));
        }

        public void CaptureSaveData(GameSaveData saveData) { if (saveData == null) return; saveData.currency ??= new CurrencySaveData(); saveData.currency.gold = gold; }
        public void RestoreSaveData(GameSaveData saveData) { gold = Mathf.Max(0, saveData?.currency?.gold ?? 0); Refreshed?.Invoke(); }
    }
}
