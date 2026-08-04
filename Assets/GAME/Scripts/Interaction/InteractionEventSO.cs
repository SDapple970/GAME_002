using Game.Reward;
using UnityEngine;

namespace Game.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, InteractableObject target, InteractionController controller)
        {
            Interactor = interactor;
            Target = target;
            Controller = controller;
        }

        public GameObject Interactor { get; }
        public InteractableObject Target { get; }
        public InteractionController Controller { get; }
    }

    public abstract class InteractionEventSO : ScriptableObject
    {
        [SerializeField] private string actionId;

        public string ActionId => InteractionIdentity.Normalize(actionId);
        public virtual bool SupportsProductionExecution => false;

        public abstract void Execute(InteractionContext context);

        public virtual InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
        {
            if (context.Request.UsePolicy != InteractionUsePolicy.LegacyCompatibility)
            {
                Debug.LogWarning(
                    $"[InteractionEventSO] Legacy event '{name}' cannot execute for {context.Request.UsePolicy}. Migrate it to the Production result API.",
                    context.Request.Source);
                return InteractionEventResult.Failed("interaction.event.unsupported");
            }

            Execute(context.LegacyContext);
            return InteractionEventResult.AcceptedResult(stateChanged: true, irreversible: false);
        }
    }

    public readonly struct InteractionExecutionContext
    {
        public InteractionExecutionContext(
            InteractionRequest request,
            InteractionRuntime runtime,
            InteractionContext legacyContext,
            string actionId,
            int eventIndex)
        {
            Request = request;
            Runtime = runtime;
            LegacyContext = legacyContext;
            ActionId = actionId;
            EventIndex = eventIndex;
        }

        public InteractionRequest Request { get; }
        public InteractionRuntime Runtime { get; }
        public InteractionContext LegacyContext { get; }
        public string ActionId { get; }
        public int EventIndex { get; }
    }

    public readonly struct InteractionEventResult
    {
        private InteractionEventResult(
            bool accepted,
            bool stateChanged,
            bool irreversible,
            bool failed,
            string message,
            RewardGrantResult reward,
            bool storyAccepted,
            bool questAccepted)
        {
            Accepted = accepted;
            StateChanged = stateChanged;
            Irreversible = irreversible;
            HasFailure = failed;
            Message = message;
            Reward = reward;
            StoryAccepted = storyAccepted;
            QuestAccepted = questAccepted;
        }

        public bool Accepted { get; }
        public bool StateChanged { get; }
        public bool Irreversible { get; }
        public bool HasFailure { get; }
        public string Message { get; }
        public RewardGrantResult Reward { get; }
        public bool StoryAccepted { get; }
        public bool QuestAccepted { get; }

        public static InteractionEventResult AcceptedResult(
            bool stateChanged,
            bool irreversible,
            string message = null,
            RewardGrantResult reward = default,
            bool storyAccepted = false,
            bool questAccepted = false)
        {
            return new InteractionEventResult(true, stateChanged, irreversible, false, message, reward, storyAccepted, questAccepted);
        }

        public static InteractionEventResult NoEffect(string message = null)
        {
            return new InteractionEventResult(false, false, false, false, message, default, false, false);
        }

        public static InteractionEventResult Failed(string message = null)
        {
            return new InteractionEventResult(false, false, false, true, message, default, false, false);
        }
    }
}
