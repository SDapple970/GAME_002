using System;

namespace Game.Quest
{
    [Serializable]
    public sealed class QuestStepData
    {
        public int stepIndex;
        public string objectiveText;
        public bool showInTracker = true;
    }
}
