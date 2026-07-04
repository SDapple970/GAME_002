using System;

namespace Game.Quest
{
    public static class QuestEventChannel
    {
        public static event Action<QuestEvent> OnEventRaised;

        public static void Publish(QuestEvent questEvent)
        {
            OnEventRaised?.Invoke(questEvent);
        }
    }
}
