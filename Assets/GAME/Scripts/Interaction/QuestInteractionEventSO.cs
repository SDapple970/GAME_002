using Game.Common.Identity;
using Game.Quest;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Quest Event", fileName = "QuestInteractionEvent")]
    public sealed class QuestInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private string questId;
        [SerializeField] private string objectiveId;
        [SerializeField] private QuestEventType eventType = QuestEventType.Interact;
        [SerializeField] private int amount = 1;
        // Retained for serialized compatibility with prior authored assets. Production
        // delivery now goes through QuestEventChannel and QuestObjectiveTracker.
        [SerializeField] private QuestRuntime questRuntime;

        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
            Debug.LogWarning(
                "[QuestInteractionEventSO] Production Quest events require InteractionRunner identity context.",
                context.Target);
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            if (!GameplayOutcomeIdentity.TryCreate(
                    GameplayOutcomeSourceType.Interaction,
                    context.Request.InteractionId,
                    context.ActionId,
                    out GameplayOutcomeIdentity identity))
            {
                return InteractionEventResult.Failed("interaction.quest.invalid-identity");
            }

            QuestEventChannel.Publish(new QuestEvent(
                eventType,
                questId,
                objectiveId,
                identity,
                Mathf.Max(1, amount),
                context.Request.Interactor,
                context.Request.InteractionId));
            return InteractionEventResult.AcceptedResult(true, true, questAccepted: true);
        }
    }
}
