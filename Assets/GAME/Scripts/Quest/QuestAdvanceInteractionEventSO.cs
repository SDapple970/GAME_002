using Game.Interaction;
using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "GAME/Quest/Advance Quest Event", fileName = "QuestAdvanceInteractionEvent")]
    public sealed class QuestAdvanceInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private QuestId questId = QuestId.TutorialPermit;
        [SerializeField] private bool advanceByOne = true;
        [SerializeField] private int targetStep;

        public override void Execute(InteractionContext context)
        {
            QuestManager manager = QuestManager.Instance != null ? QuestManager.Instance : Object.FindFirstObjectByType<QuestManager>();
            if (manager == null)
            {
                Debug.LogWarning("[QuestAdvanceInteractionEventSO] QuestManager is missing.", context.Target);
                return;
            }

            if (!manager.IsActiveQuest(questId))
            {
                Debug.LogWarning($"[QuestAdvanceInteractionEventSO] Active quest mismatch or missing. questId={questId}", context.Target);
                return;
            }

            if (advanceByOne)
                manager.AdvanceStep(questId);
            else
                manager.SetStep(questId, targetStep);
        }
    }
}
