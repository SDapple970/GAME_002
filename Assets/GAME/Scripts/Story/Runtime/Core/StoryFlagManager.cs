// Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagManager.cs
using System.Collections.Generic;
using System;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Story.Core
{
    public sealed class StoryFlagManager : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer, INewGameRuntimeReset
    {
        public static StoryFlagManager Instance { get; private set; }

        private readonly Dictionary<string, bool> _boolFlags = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _intFlags = new(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        internal static StoryFlagManager EnsureInstalled()
        {
            if (Instance != null) return Instance;
            StoryFlagManager existing = FindFirstObjectByType<StoryFlagManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Awake();
                return Instance;
            }

            StoryFlagManager created = new GameObject("StoryFlagManager").AddComponent<StoryFlagManager>();
            // EditMode compatibility callers also need an owner without waiting for Awake.
            if (Instance == null) created.Awake();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Do not destroy other services on an authored shared root.
                enabled = false;
                if (Application.isPlaying) Destroy(this);
                return;
            }

            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Dictionary<string, bool> ExportBoolFlags() => new(_boolFlags, StringComparer.Ordinal);
        public Dictionary<string, int> ExportIntFlags() => new(_intFlags, StringComparer.Ordinal);

        // Legacy bool-only imports replace only that domain, leaving int flags intact.
        public void ImportBoolFlags(Dictionary<string, bool> flags)
        {
            _boolFlags.Clear();
            if (flags == null) return;
            foreach (KeyValuePair<string, bool> pair in flags)
                if (!string.IsNullOrEmpty(pair.Key)) _boolFlags[pair.Key] = pair.Value;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.story ??= new StorySaveData();
            saveData.story.flags ??= new List<SaveBoolEntry>();
            saveData.story.intFlags ??= new List<SaveIntEntry>();
            saveData.story.flags.Clear();
            saveData.story.intFlags.Clear();
            foreach (KeyValuePair<string, bool> pair in _boolFlags)
                saveData.story.flags.Add(new SaveBoolEntry { id = pair.Key, value = pair.Value });
            foreach (KeyValuePair<string, int> pair in _intFlags)
                saveData.story.intFlags.Add(new SaveIntEntry { id = pair.Key, value = pair.Value });
            saveData.story.flags.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            saveData.story.intFlags.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            ClearAll();
            // Last entry wins within each type, matching the old Database restore.
            // Exact keys and separate bool/int domains preserve existing caller semantics.
            if (saveData?.story?.flags != null)
                foreach (SaveBoolEntry entry in saveData.story.flags)
                    if (entry != null && !string.IsNullOrEmpty(entry.id)) _boolFlags[entry.id] = entry.value;
            if (saveData?.story?.intFlags != null)
                foreach (SaveIntEntry entry in saveData.story.intFlags)
                    if (entry != null && !string.IsNullOrEmpty(entry.id)) _intFlags[entry.id] = entry.value;
        }

        public void ResetForNewGame() => ClearAll();

        public bool GetBool(string key)
        {
            if (!IsValidKey(key)) return false;
            return _boolFlags.TryGetValue(key, out bool value) && value;
        }

        public void SetBool(string key, bool value)
        {
            if (!IsValidKey(key)) return;
            _boolFlags[key] = value;
            Debug.Log($"[StoryFlag] Bool '{key}' = {value}");
        }

        public int GetInt(string key)
        {
            if (!IsValidKey(key)) return 0;
            return _intFlags.TryGetValue(key, out int value) ? value : 0;
        }

        public void SetInt(string key, int value)
        {
            if (!IsValidKey(key)) return;
            _intFlags[key] = value;
            Debug.Log($"[StoryFlag] Int '{key}' = {value}");
        }

        public void AddInt(string key, int amount)
        {
            if (!IsValidKey(key)) return;
            SetInt(key, GetInt(key) + amount);
        }

        public bool HasBool(string key)
        {
            if (!IsValidKey(key)) return false;
            return _boolFlags.ContainsKey(key);
        }

        public bool HasInt(string key)
        {
            if (!IsValidKey(key)) return false;
            return _intFlags.ContainsKey(key);
        }

        public bool HasFlag(string key)
        {
            if (!IsValidKey(key)) return false;
            return _boolFlags.ContainsKey(key) || _intFlags.ContainsKey(key);
        }

        public void ClearBool(string key)
        {
            if (!IsValidKey(key)) return;
            _boolFlags.Remove(key);
        }

        public void ClearInt(string key)
        {
            if (!IsValidKey(key)) return;
            _intFlags.Remove(key);
        }

        public void ClearFlag(string key)
        {
            if (!IsValidKey(key)) return;
            _boolFlags.Remove(key);
            _intFlags.Remove(key);
        }

        public void ClearAll()
        {
            _boolFlags.Clear();
            _intFlags.Clear();
        }

        private static bool IsValidKey(string key)
        {
            if (!string.IsNullOrEmpty(key)) return true;

            Debug.LogWarning("[StoryFlag] Flag key is null or empty.");
            return false;
        }
    }
}
