using System;
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
        [SerializeField] private QuestCategory category = QuestCategory.Unspecified;
        [Min(0)]
        [SerializeField] private int missionDayCost;
        [SerializeField] private QuestObjectiveDefinition[] objectives;
        [SerializeField] private int rewardGold;
        [SerializeField] private int rewardExp;
        [SerializeField] private QuestRetryPolicy retryPolicy = QuestRetryPolicy.NotRetryable;

        public string QuestId => questId;
        public string QuestTitle => questTitle;
        public string Description => description;
        public QuestCategory Category => category;
        public int MissionDayCost => Mathf.Max(0, missionDayCost);
        public QuestObjectiveDefinition[] Objectives => objectives;
        public int RewardGold => rewardGold;
        public int RewardExp => rewardExp;
        public QuestRetryPolicy RetryPolicy => retryPolicy;

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

        public QuestObjectiveDefinition FindObjective(string objectiveId)
        {
            if (objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < objectives.Length; i++)
            {
                QuestObjectiveDefinition objective = objectives[i];
                if (objective != null && objective.ObjectiveId == objectiveId)
                    return objective;
            }

            return null;
        }
    }
}
