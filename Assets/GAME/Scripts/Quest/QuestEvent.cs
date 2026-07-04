using UnityEngine;

namespace Game.Quest
{
    public readonly struct QuestEvent
    {
        public readonly QuestEventType Type;
        public readonly string QuestId;
        public readonly string ObjectiveId;
        public readonly int Amount;
        public readonly GameObject Source;

        public QuestEvent(
            QuestEventType type,
            string questId,
            string objectiveId,
            int amount = 1,
            GameObject source = null)
        {
            Type = type;
            QuestId = questId;
            ObjectiveId = objectiveId;
            Amount = amount;
            Source = source;
        }
    }
}
