using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Core
{
    public sealed class SaveLoadService : MonoBehaviour
    {
        public static SaveLoadService Instance { get; private set; }

        [SerializeField] private SaveManager saveManager;

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
