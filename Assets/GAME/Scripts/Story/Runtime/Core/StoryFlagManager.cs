// Assets/GAME/Scripts/Story/Runtime/Core/StoryFlagManager.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story.Core
{
    public sealed class StoryFlagManager : MonoBehaviour
    {
        public static StoryFlagManager Instance { get; private set; }

        private readonly Dictionary<string, bool> _boolFlags = new();
        private readonly Dictionary<string, int> _intFlags = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
