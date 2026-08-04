using Game.Reward;

namespace Game.Interaction
{
    public enum InteractionResultStatus
    {
        Success = 0,
        NoEffect = 10,
        AlreadyConsumed = 20,
        BlockedState = 30,
        BlockedCondition = 40,
        InvalidIdentity = 50,
        PartialFailure = 60,
        Failed = 70
    }

    public readonly struct InteractionResult
    {
        public InteractionResult(
            InteractionResultStatus status,
            string sourceId,
            string message,
            bool stateChanged,
            bool consumed,
            bool promptRefreshRequired,
            RewardGrantResult reward = default,
            bool storyAccepted = false,
            bool questAccepted = false)
        {
            Status = status;
            SourceId = sourceId;
            Message = message;
            StateChanged = stateChanged;
            Consumed = consumed;
            PromptRefreshRequired = promptRefreshRequired;
            Reward = reward;
            StoryAccepted = storyAccepted;
            QuestAccepted = questAccepted;
        }

        public InteractionResultStatus Status { get; }
        public string SourceId { get; }
        public string Message { get; }
        public bool StateChanged { get; }
        public bool Consumed { get; }
        public bool PromptRefreshRequired { get; }
        public RewardGrantResult Reward { get; }
        public bool StoryAccepted { get; }
        public bool QuestAccepted { get; }
        public bool Succeeded => Status == InteractionResultStatus.Success || Status == InteractionResultStatus.PartialFailure;
    }
}
