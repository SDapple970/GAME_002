using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "GAME/Quest/Quest Data", fileName = "QuestData")]
    public sealed class QuestDataSO : ScriptableObject
    {
        [SerializeField] private QuestId questId = QuestId.TutorialPermit;
        [SerializeField] private string questTitle;
        [TextArea(3, 8)]
        [SerializeField] private string description;
        [SerializeField] private QuestStepData[] steps;
        [SerializeField] private int rewardGold;
        [SerializeField] private int rewardExp;

        public QuestId QuestId => questId;
        public string QuestTitle => questTitle;
        public string Description => description;
        public QuestStepData[] Steps => steps;
        public int RewardGold => rewardGold;
        public int RewardExp => rewardExp;

        public QuestStepData GetStep(int stepIndex)
        {
            if (steps == null)
                return null;

            for (int i = 0; i < steps.Length; i++)
            {
                QuestStepData step = steps[i];
                if (step != null && step.stepIndex == stepIndex)
                    return step;
            }

            return null;
        }

        public int GetFirstStepIndex()
        {
            if (steps == null || steps.Length == 0 || steps[0] == null)
                return 0;

            return steps[0].stepIndex;
        }

        public int GetNextStepIndex(int currentStep)
        {
            if (steps == null || steps.Length == 0)
                return currentStep + 1;

            int best = int.MaxValue;
            for (int i = 0; i < steps.Length; i++)
            {
                QuestStepData step = steps[i];
                if (step != null && step.stepIndex > currentStep && step.stepIndex < best)
                    best = step.stepIndex;
            }

            return best == int.MaxValue ? currentStep + 1 : best;
        }
    }
}
