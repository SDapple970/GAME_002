using System.Collections.Generic;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    public sealed class StoryFlagDatabase : MonoBehaviour
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
    }
}
