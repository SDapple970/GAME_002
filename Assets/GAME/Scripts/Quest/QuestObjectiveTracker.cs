using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;

        private void Awake()
        {
            ResolveRuntime();
        }

        private void OnEnable()
        {
            ResolveRuntime();
            QuestEventChannel.OnEventRaised += HandleQuestEvent;
        }

        private void OnDisable()
        {
            QuestEventChannel.OnEventRaised -= HandleQuestEvent;
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteObjective(questId, objectiveId);
        }

        private void HandleQuestEvent(QuestEvent questEvent)
        {
            ResolveRuntime();
            questRuntime?.ApplyEvent(questEvent);
        }

        private void ResolveRuntime()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }
    }
}
