using Game.Interaction;
using Game.Quest;
using UnityEngine;

namespace Game.Demo
{
    [CreateAssetMenu(menuName = "GAME/Demo/Rescue NPC Objective Event", fileName = "RescueNpcObjectiveEvent")]
    public sealed class RescueNpcObjectiveEventSO : InteractionEventSO
    {
        [SerializeField] private DungeonObjectiveManager objectiveManager;
        [SerializeField] private bool completeQuestOnRescue;
        [SerializeField] private QuestId questId = QuestId.BIC_Zone01;

        public override void Execute(InteractionContext context)
        {
            DungeonObjectiveManager manager = objectiveManager;
            if (manager == null)
                manager = Object.FindFirstObjectByType<DungeonObjectiveManager>();

            if (manager != null)
                manager.MarkNpcRescued();
            else
                Debug.LogWarning("[RescueNpcObjectiveEventSO] DungeonObjectiveManager is missing.", context.Target);

            if (completeQuestOnRescue)
                CompleteQuest(context);
        }

        private void CompleteQuest(InteractionContext context)
        {
            if (questId == QuestId.None)
                return;

            QuestManager manager = QuestManager.Instance != null
                ? QuestManager.Instance
                : Object.FindFirstObjectByType<QuestManager>();

            if (manager != null)
                manager.CompleteQuest(questId);
            else
                Debug.LogWarning("[RescueNpcObjectiveEventSO] QuestManager is missing.", context.Target);
        }
    }
}
