using System;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.World.Exploration
{
    public enum ExplorationResourceType
    {
        Shining = 0,
        Hunger = 10
    }

    public readonly struct ExplorationResourceChange
    {
        public ExplorationResourceChange(ExplorationResourceType resource, int previousValue, int currentValue)
        {
            Resource = resource;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }

        public ExplorationResourceType Resource { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
    }

    public sealed class ExplorationResourceRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static ExplorationResourceRuntime Instance { get; private set; }

        private int _shining;
        private int _hunger;

        public int Shining => _shining;
        public int Hunger => _hunger;
        public event Action<ExplorationResourceChange> Changed;
        public event Action Refreshed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool TryAddShining(int amount) => TryChange(ExplorationResourceType.Shining, amount);
        public bool TrySpendShining(int amount) => amount > 0 && _shining >= amount && TryChange(ExplorationResourceType.Shining, -amount);
        public bool TryChangeHunger(int amount) => TryChange(ExplorationResourceType.Hunger, amount);

        public bool CanChangeHunger(int amount) => CanApply(_hunger, amount);

        public bool TrySetHunger(int value)
        {
            if (value < 0) return false;
            return SetValue(ExplorationResourceType.Hunger, value);
        }

        public bool TrySetShining(int value)
        {
            if (value < 0) return false;
            return SetValue(ExplorationResourceType.Shining, value);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.exploration ??= new ExplorationSaveData();
            saveData.exploration.shining = _shining;
            saveData.exploration.hunger = _hunger;
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _shining = Mathf.Max(0, saveData?.exploration?.shining ?? 0);
            _hunger = Mathf.Max(0, saveData?.exploration?.hunger ?? 0);
            Refreshed?.Invoke();
        }

        private bool TryChange(ExplorationResourceType resource, int amount)
        {
            if (amount == 0) return false;
            int current = resource == ExplorationResourceType.Shining ? _shining : _hunger;
            if (!CanApply(current, amount)) return false;
            return SetValue(resource, current + amount);
        }

        private static bool CanApply(int current, int amount)
        {
            long next = (long)current + amount;
            return next >= 0 && next <= int.MaxValue;
        }

        private bool SetValue(ExplorationResourceType resource, int value)
        {
            int previous = resource == ExplorationResourceType.Shining ? _shining : _hunger;
            if (previous == value) return false;
            if (resource == ExplorationResourceType.Shining) _shining = value; else _hunger = value;
            Changed?.Invoke(new ExplorationResourceChange(resource, previous, value));
            return true;
        }
    }
}
