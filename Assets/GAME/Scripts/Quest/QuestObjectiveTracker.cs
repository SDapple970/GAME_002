using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;

        private void Awake()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }

        public void CompleteObjective(string questId, string objectiveId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteObjective(questId, objectiveId);
        }
    }
}
