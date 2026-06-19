using Game.Mission;
using Game.Mission.Data;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestRuntime : MonoBehaviour
    {
        [SerializeField] private MissionManager missionManager;

        private void Awake()
        {
            ResolveMissionManager();
        }

        public void StartQuest(MissionDefinitionSO definition)
        {
            ResolveMissionManager();
            missionManager?.StartMission(definition);
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            ResolveMissionManager();
            missionManager?.CompleteObjective(questId, objectiveId);
        }

        public void CompleteQuest(string questId)
        {
            ResolveMissionManager();
            missionManager?.CompleteMission(questId);
        }

        private void ResolveMissionManager()
        {
            if (missionManager == null)
                missionManager = MissionManager.Instance != null
                    ? MissionManager.Instance
                    : FindFirstObjectByType<MissionManager>();
        }
    }
}
