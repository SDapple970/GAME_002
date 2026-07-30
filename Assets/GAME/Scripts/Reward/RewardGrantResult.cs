namespace Game.Reward
{
    public readonly struct RewardGrantResult
    {
        public static readonly RewardGrantResult Empty = new RewardGrantResult(
            RewardSourceType.Unknown,
            null,
            0, 0,
            0, 0,
            null,
            0,
            null,
            0,
            false,
            false,
            false);

        public readonly RewardSourceType SourceType;
        public readonly string SourceId;
        public readonly string ActionId;
        public readonly int RequestedGold;
        public readonly int RequestedExp;
        public readonly string RequestedItemId;
        public readonly int RequestedItemCount;
        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;
        public readonly bool DuplicateBlocked;
        public readonly bool PartialFailure;
        public readonly bool InvalidRequest;

        public RewardGrantResult(
            RewardSourceType sourceType,
            string sourceId,
            int gold,
            int exp,
            string itemId,
            int itemCount,
            bool duplicateBlocked)
            : this(
                sourceType,
                sourceId,
                gold,
                exp,
                gold,
                exp,
                itemId,
                itemCount,
                itemId,
                itemCount,
                duplicateBlocked,
                false,
                false)
        {
        }

        public RewardGrantResult(
            RewardSourceType sourceType,
            string sourceId,
            int requestedGold,
            int requestedExp,
            int appliedGold,
            int appliedExp,
            string requestedItemId,
            int requestedItemCount,
            string appliedItemId,
            int appliedItemCount,
            bool duplicateBlocked,
            bool partialFailure,
            bool invalidRequest,
            string actionId = null)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            ActionId = actionId;
            RequestedGold = requestedGold;
            RequestedExp = requestedExp;
            RequestedItemId = requestedItemId;
            RequestedItemCount = requestedItemCount;
            Gold = appliedGold;
            Exp = appliedExp;
            ItemId = appliedItemId;
            ItemCount = appliedItemCount;
            DuplicateBlocked = duplicateBlocked;
            PartialFailure = partialFailure;
            InvalidRequest = invalidRequest;
        }

        public bool HasAnyReward => Gold > 0 || Exp > 0 || ItemCount > 0;
    }
}
