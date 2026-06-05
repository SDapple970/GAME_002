using System;

namespace Game.Quest
{
    [Serializable]
    public sealed class QuestProgress
    {
        public QuestDataSO quest;
        public int currentStep;
        public bool completed;

        public QuestProgress()
        {
        }

        public QuestProgress(QuestDataSO quest)
        {
            this.quest = quest;
            currentStep = quest != null ? quest.GetFirstStepIndex() : 0;
            completed = false;
        }

        public QuestId QuestId => quest != null ? quest.QuestId : global::Game.Quest.QuestId.None;
        public QuestStepData CurrentStepData => quest != null ? quest.GetStep(currentStep) : null;
    }
}
