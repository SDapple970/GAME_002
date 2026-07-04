namespace Game.Reward
{
    public readonly struct RewardGrantResult
    {
        public static readonly RewardGrantResult Empty = new RewardGrantResult(
            RewardSourceType.Unknown,
            null,
            0,
            0,
            null,
            0,
            false);

        public readonly RewardSourceType SourceType;
        public readonly string SourceId;
        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;
        public readonly bool DuplicateBlocked;

        public RewardGrantResult(
            RewardSourceType sourceType,
            string sourceId,
            int gold,
            int exp,
            string itemId,
            int itemCount,
            bool duplicateBlocked)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            Gold = gold;
            Exp = exp;
            ItemId = itemId;
            ItemCount = itemCount;
            DuplicateBlocked = duplicateBlocked;
        }

        public bool HasAnyReward => Gold > 0 || Exp > 0 || ItemCount > 0;
    }
}
