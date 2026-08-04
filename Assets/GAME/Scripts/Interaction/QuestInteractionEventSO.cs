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

            QuestRuntime runtime = questRuntime != null
                ? questRuntime
                : FindFirstObjectByType<QuestRuntime>();
            if (runtime == null)
                return InteractionEventResult.Failed("interaction.quest.runtime-missing");

            bool applied = runtime.ApplyEvent(new QuestEvent(
                eventType,
                questId,
                objectiveId,
                identity,
                Mathf.Max(1, amount),
                context.Request.Interactor));
            return applied
                ? InteractionEventResult.AcceptedResult(true, true, questAccepted: true)
                : InteractionEventResult.NoEffect("interaction.quest.not-applied");
        }
    }
}
