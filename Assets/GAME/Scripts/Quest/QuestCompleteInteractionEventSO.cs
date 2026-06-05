using Game.Interaction;
using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "GAME/Quest/Complete Quest Event", fileName = "QuestCompleteInteractionEvent")]
    public sealed class QuestCompleteInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private QuestId questId = QuestId.TutorialPermit;

        public override void Execute(InteractionContext context)
        {
            QuestManager manager = QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
            if (manager == null)
            {
                Debug.LogWarning("[QuestCompleteInteractionEventSO] QuestManager is missing.", context.Target);
                return;
            }

            if (!manager.IsActiveQuest(questId))
            {
                Debug.LogWarning($"[QuestCompleteInteractionEventSO] Active quest mismatch or missing. questId={questId}", context.Target);
                return;
            }

            manager.CompleteQuest(questId);
        }
    }
}
