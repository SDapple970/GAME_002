using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "GAME/Quest/Quest Definition", fileName = "QuestDefinition")]
    public sealed class QuestDefinitionSO : ScriptableObject
    {
        [SerializeField] private string questId;
        [SerializeField] private string questTitle;
        [TextArea(3, 8)]
        [SerializeField] private string description;
        [SerializeField] private QuestObjectiveDefinition[] objectives;

        public string QuestId => questId;
        public string QuestTitle => questTitle;
        public string Description => description;
        public QuestObjectiveDefinition[] Objectives => objectives;

        public QuestObjectiveDefinition FindObjective(QuestEvent questEvent)
        {
            if (objectives == null)
                return null;

            for (int i = 0; i < objectives.Length; i++)
            {
                QuestObjectiveDefinition objective = objectives[i];
                if (objective != null && objective.Matches(questEvent))
                    return objective;
            }

            return null;
        }
    }
}
