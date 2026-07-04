using Game.NonCombat.Save;
using System;
using UnityEngine;

namespace Game.Core
{
    public sealed class SaveLoadService : MonoBehaviour
    {
        public static SaveLoadService Instance { get; private set; }

        [SerializeField] private SaveManager saveManager;

        private bool _missingProviderWarned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveSaveManager();
        }

        public void Save()
        {
            if (!CanSaveNow())
            {
                Debug.LogWarning($"[SaveLoadService] Save blocked in state {GameStateMachine.Instance?.Current}.", this);
                return;
            }

            ResolveSaveManager();
            saveManager?.Save();
        }

        public void Load()
        {
            ResolveSaveManager();
            saveManager?.Load();
        }

        public GameSaveData CaptureGameSaveDataSnapshot()
        {
            GameSaveData saveData = new();
            saveData.header.savedAtUtc = DateTime.UtcNow.ToString("O");

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int providerCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISaveDataProvider provider)
                {
                    provider.CaptureSaveData(saveData);
                    providerCount++;
                }
            }

            if (providerCount == 0 && !_missingProviderWarned)
            {
                _missingProviderWarned = true;
                Debug.LogWarning("[SaveLoadService] No save data providers were found for GameSaveData snapshot capture.", this);
            }

            return saveData;
        }

        public void RestoreGameSaveDataSnapshot(GameSaveData saveData)
        {
            if (saveData == null)
            {
                Debug.LogWarning("[SaveLoadService] Restore ignored. GameSaveData is null.", this);
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISaveDataConsumer consumer)
                    consumer.RestoreSaveData(saveData);
            }
        }

        private static bool CanSaveNow()
        {
            if (GameStateMachine.Instance == null)
                return true;

            GameState state = GameStateMachine.Instance.Current;
            return state == GameState.Exploration ||
                   state == GameState.UIOnly ||
                   state == GameState.Paused;
        }

        private void ResolveSaveManager()
        {
            if (saveManager == null)
                saveManager = FindFirstObjectByType<SaveManager>();
        }
    }
}
