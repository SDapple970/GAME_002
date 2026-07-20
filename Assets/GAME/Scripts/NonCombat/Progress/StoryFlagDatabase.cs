using System.Collections.Generic;
using UnityEngine;
using Game.NonCombat.Save;

namespace Game.NonCombat.Progress
{
    public sealed class StoryFlagDatabase : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static StoryFlagDatabase Instance { get; private set; }

        private readonly Dictionary<string, bool> _flags = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public bool HasFlag(string flagId)
        {
            return !string.IsNullOrEmpty(flagId) && _flags.TryGetValue(flagId, out bool value) && value;
        }

        public void SetFlag(string flagId, bool value)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            _flags[flagId] = value;
            Debug.Log($"[StoryFlag] {flagId} = {value}", this);
        }

        public void ClearFlag(string flagId) => SetFlag(flagId, false);

        public Dictionary<string, bool> ExportFlags() => new(_flags);

        public void ImportFlags(Dictionary<string, bool> flags)
        {
            _flags.Clear();
            if (flags == null) return;

            foreach (KeyValuePair<string, bool> pair in flags)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                    _flags[pair.Key] = pair.Value;
            }
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.story ??= new StorySaveData();
            saveData.story.flags.Clear();
            foreach (KeyValuePair<string, bool> pair in _flags)
                if (!string.IsNullOrWhiteSpace(pair.Key)) saveData.story.flags.Add(new SaveBoolEntry { id = pair.Key, value = pair.Value });
            saveData.story.flags.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            Dictionary<string, bool> flags = new();
            if (saveData?.story?.flags != null)
                foreach (SaveBoolEntry entry in saveData.story.flags)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.id)) flags[entry.id] = entry.value;
            ImportFlags(flags);
        }
    }
}
