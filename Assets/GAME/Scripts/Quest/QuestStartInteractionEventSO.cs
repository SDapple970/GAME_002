using Game.Interaction;
using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "GAME/Quest/Start Quest Event", fileName = "QuestStartInteractionEvent")]
    public sealed class QuestStartInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private QuestDataSO quest;

        public override void Execute(InteractionContext context)
        {
            QuestManager manager = ResolveManager();
            if (manager == null || quest == null)
            {
                Debug.LogWarning("[QuestStartInteractionEventSO] QuestManager or quest is missing.", context.Target);
                return;
            }

            manager.StartQuest(quest);
        }

        private static QuestManager ResolveManager()
        {
            return QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
        }
    }
}
