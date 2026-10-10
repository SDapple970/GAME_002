using System.Collections.Generic;
using UnityEngine;
using Game.NonCombat.Save;
using Game.Story.Core;

namespace Game.NonCombat.Progress
{
    public sealed class StoryFlagDatabase : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer, INewGameRuntimeReset
    {
        public static StoryFlagDatabase Instance { get; private set; }

        // Compatibility facade; StoryFlagManager is the only flag state owner.
        private StoryFlagManager Owner => StoryFlagManager.EnsureInstalled();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            StoryFlagManager.EnsureInstalled();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool HasFlag(string flagId)
        {
            return !string.IsNullOrEmpty(flagId) && Owner.GetBool(flagId);
        }

        public void SetFlag(string flagId, bool value)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            Owner.SetBool(flagId, value);
        }

        public void ClearFlag(string flagId) => SetFlag(flagId, false);

        public Dictionary<string, bool> ExportFlags() => Owner.ExportBoolFlags();

        public void ImportFlags(Dictionary<string, bool> flags)
        {
            Owner.ImportBoolFlags(flags);
        }

        public void ResetForNewGame() => Owner.ResetForNewGame();

        public void CaptureSaveData(GameSaveData saveData)
        {
            Owner.CaptureSaveData(saveData);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            Owner.RestoreSaveData(saveData);
        }
    }
}
