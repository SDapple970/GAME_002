namespace Game.Reward
{
    public readonly struct RewardResult
    {
        public static readonly RewardResult Empty = new RewardResult(0, 0);

        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;
        public readonly bool DuplicateBlocked;
        public readonly bool PartialFailure;

        public RewardResult(int gold, int exp)
            : this(gold, exp, null, 0, false, false)
        {
        }

        public RewardResult(
            int gold,
            int exp,
            string itemId,
            int itemCount,
            bool duplicateBlocked = false,
            bool partialFailure = false)
        {
            Gold = gold;
            Exp = exp;
            ItemId = itemId;
            ItemCount = itemCount;
            DuplicateBlocked = duplicateBlocked;
            PartialFailure = partialFailure;
        }

        public RewardResult(RewardGrantResult result)
            : this(
                result.Gold,
                result.Exp,
                result.ItemId,
                result.ItemCount,
                result.DuplicateBlocked,
                result.PartialFailure)
        {
        }
    }
}
