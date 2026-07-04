namespace Game.Reward
{
    public readonly struct RewardResult
    {
        public static readonly RewardResult Empty = new RewardResult(0, 0);

        public readonly int Gold;
        public readonly int Exp;
        public readonly string ItemId;
        public readonly int ItemCount;

        public RewardResult(int gold, int exp)
            : this(gold, exp, null, 0)
        {
        }

        public RewardResult(int gold, int exp, string itemId, int itemCount)
        {
            Gold = gold;
            Exp = exp;
            ItemId = itemId;
            ItemCount = itemCount;
        }
    }
}
