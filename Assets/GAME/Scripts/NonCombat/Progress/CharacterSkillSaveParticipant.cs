using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    /// <summary>Unity lifecycle and save bridge for the pure CharacterSkillRuntime domain model.</summary>
    public sealed class CharacterSkillSaveParticipant : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer, INewGameRuntimeReset
    {
        public static CharacterSkillSaveParticipant Instance { get; private set; }

        public CharacterSkillRuntime Runtime { get; } = new CharacterSkillRuntime();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.characterSkills ??= new CharacterSkillCollectionSaveData();
            Runtime.Capture(saveData.characterSkills);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            Runtime.Restore(saveData?.characterSkills);
        }

        public void ResetForNewGame()
        {
            Runtime.Reset();
        }
    }
}
