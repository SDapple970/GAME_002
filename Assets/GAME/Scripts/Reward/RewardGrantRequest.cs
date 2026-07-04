namespace Game.Reward
{
    public readonly struct RewardGrantRequest
    {
        public readonly RewardSourceType SourceType;
        public readonly string SourceId;
        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;

        public RewardGrantRequest(
            RewardSourceType sourceType,
            string sourceId,
            int gold = 0,
            int exp = 0,
            string itemId = null,
            int itemCount = 0)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            Gold = gold;
            Exp = exp;
            ItemId = itemId;
            ItemCount = itemCount;
        }
    }
}
