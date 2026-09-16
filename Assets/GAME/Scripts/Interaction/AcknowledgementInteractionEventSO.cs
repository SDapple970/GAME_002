using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// Authored production interaction that proves the canonical runner path without
    /// taking ownership of dialogue, quests, rewards, or global state.
    /// </summary>
    [CreateAssetMenu(menuName = "GAME/Interaction/Acknowledgement Event", fileName = "AcknowledgementInteractionEvent")]
    public sealed class AcknowledgementInteractionEventSO : InteractionEventSO
    {
        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            return InteractionEventResult.AcceptedResult(
                stateChanged: false,
                irreversible: false,
                message: "상호작용 완료");
        }
    }
}
