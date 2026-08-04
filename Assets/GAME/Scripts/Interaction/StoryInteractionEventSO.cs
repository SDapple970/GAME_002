using Game.Story;
using Game.Story.Data;
using UnityEngine;

namespace Game.Interaction
{
    [CreateAssetMenu(menuName = "GAME/Interaction/Story Event", fileName = "StoryInteractionEvent")]
    public sealed class StoryInteractionEventSO : InteractionEventSO
    {
        [SerializeField] private StoryEventDefinitionSO eventDefinition;
        [SerializeField] private StoryEventRunner runner;

        public override bool SupportsProductionExecution => true;

        public override void Execute(InteractionContext context)
        {
            TryStart(context.Target);
        }

        public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            return TryStart(context.Request.Source)
                ? InteractionEventResult.AcceptedResult(true, true, storyAccepted: true)
                : InteractionEventResult.Failed("interaction.story.start-rejected");
        }

        private bool TryStart(InteractableObject source)
        {
            StoryEventRunner resolved = runner != null ? runner : FindFirstObjectByType<StoryEventRunner>();
            if (resolved == null || eventDefinition == null)
            {
                Debug.LogWarning("[StoryInteractionEventSO] StoryEventRunner or event definition is missing.", source);
                return false;
            }

            StorySpeakerAnchor speakerAnchor = source != null
                ? source.GetComponentInChildren<StorySpeakerAnchor>()
                : null;
            return resolved.TryStartEvent(eventDefinition, speakerAnchor);
        }
    }
}
