using System;
using System.Collections.Generic;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.World.Exploration
{
    public enum PersistentConditionMutationStatus
    {
        Success = 0,
        InvalidOwnerId = 10,
        InvalidConditionId = 20,
        AlreadyAcquired = 30,
        NotAcquired = 40
    }

    public readonly struct PersistentConditionChange
    {
        public PersistentConditionChange(string ownerId, string conditionId, PersistentConditionCategory category, bool acquired)
        {
            OwnerId = ownerId; ConditionId = conditionId; Category = category; Acquired = acquired;
        }
        public string OwnerId { get; }
        public string ConditionId { get; }
        public PersistentConditionCategory Category { get; }
        public bool Acquired { get; }
    }

    public sealed class PersistentConditionRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static PersistentConditionRuntime Instance { get; private set; }
        private readonly Dictionary<string, HashSet<string>> _diseases = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _quirks = new(StringComparer.Ordinal);

        public event Action<PersistentConditionChange> Changed;
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

        public PersistentConditionMutationStatus TryAcquire(string ownerId, PersistentConditionDefinitionSO definition)
        {
            if (definition == null) return PersistentConditionMutationStatus.InvalidConditionId;
            return TryAcquire(ownerId, definition.ConditionId, definition.Category);
        }

        public PersistentConditionMutationStatus TryAcquire(string ownerId, string conditionId, PersistentConditionCategory category)
        {
            string owner = PersistentConditionIdentity.Normalize(ownerId);
            string condition = PersistentConditionIdentity.Normalize(conditionId);
            if (owner == null) return PersistentConditionMutationStatus.InvalidOwnerId;
            if (condition == null || !Enum.IsDefined(typeof(PersistentConditionCategory), category)) return PersistentConditionMutationStatus.InvalidConditionId;
            HashSet<string> values = GetOrCreate(owner, category);
            if (!values.Add(condition)) return PersistentConditionMutationStatus.AlreadyAcquired;
            Changed?.Invoke(new PersistentConditionChange(owner, condition, category, true));
            return PersistentConditionMutationStatus.Success;
        }

        public PersistentConditionMutationStatus TryRemove(string ownerId, string conditionId, PersistentConditionCategory category)
        {
            string owner = PersistentConditionIdentity.Normalize(ownerId);
            string condition = PersistentConditionIdentity.Normalize(conditionId);
            if (owner == null) return PersistentConditionMutationStatus.InvalidOwnerId;
            if (condition == null || !Enum.IsDefined(typeof(PersistentConditionCategory), category)) return PersistentConditionMutationStatus.InvalidConditionId;
            Dictionary<string, HashSet<string>> source = GetSource(category);
            if (!source.TryGetValue(owner, out HashSet<string> values) || !values.Remove(condition))
                return PersistentConditionMutationStatus.NotAcquired;
            if (values.Count == 0) source.Remove(owner);
            Changed?.Invoke(new PersistentConditionChange(owner, condition, category, false));
            return PersistentConditionMutationStatus.Success;
        }

        public bool HasCondition(string ownerId, string conditionId, PersistentConditionCategory category)
        {
            string owner = PersistentConditionIdentity.Normalize(ownerId);
            string condition = PersistentConditionIdentity.Normalize(conditionId);
            return owner != null && condition != null && Enum.IsDefined(typeof(PersistentConditionCategory), category) && GetSource(category).TryGetValue(owner, out HashSet<string> values) && values.Contains(condition);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.exploration ??= new ExplorationSaveData();
            saveData.exploration.conditions.Clear();
            Capture(_diseases, PersistentConditionCategory.Disease, saveData.exploration.conditions);
            Capture(_quirks, PersistentConditionCategory.Quirk, saveData.exploration.conditions);
            saveData.exploration.conditions.Sort((a, b) =>
            {
                int owner = string.CompareOrdinal(a.ownerId, b.ownerId);
                int category = owner == 0 ? a.category.CompareTo(b.category) : owner;
                return category == 0 ? string.CompareOrdinal(a.conditionId, b.conditionId) : category;
            });
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _diseases.Clear(); _quirks.Clear();
            if (saveData?.exploration?.conditions != null)
                foreach (PersistentConditionSaveData entry in saveData.exploration.conditions)
                {
                    string owner = PersistentConditionIdentity.Normalize(entry?.ownerId);
                    string condition = PersistentConditionIdentity.Normalize(entry?.conditionId);
                    if (owner == null || condition == null || !Enum.IsDefined(typeof(PersistentConditionCategory), entry.category)) continue;
                    GetOrCreate(owner, (PersistentConditionCategory)entry.category).Add(condition);
                }
            Refreshed?.Invoke();
        }

        private Dictionary<string, HashSet<string>> GetSource(PersistentConditionCategory category) =>
            category == PersistentConditionCategory.Quirk ? _quirks : _diseases;

        private HashSet<string> GetOrCreate(string ownerId, PersistentConditionCategory category)
        {
            Dictionary<string, HashSet<string>> source = GetSource(category);
            if (!source.TryGetValue(ownerId, out HashSet<string> values)) source.Add(ownerId, values = new HashSet<string>(StringComparer.Ordinal));
            return values;
        }

        private static void Capture(Dictionary<string, HashSet<string>> source, PersistentConditionCategory category, List<PersistentConditionSaveData> target)
        {
            foreach (KeyValuePair<string, HashSet<string>> owner in source)
                foreach (string condition in owner.Value)
                    target.Add(new PersistentConditionSaveData { ownerId = owner.Key, conditionId = condition, category = (int)category });
        }
    }
}
