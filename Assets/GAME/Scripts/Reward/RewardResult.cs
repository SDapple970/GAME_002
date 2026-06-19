namespace Game.Reward
{
    public readonly struct RewardResult
    {
        public static readonly RewardResult Empty = new RewardResult(0, 0);

        public readonly int Gold;
        public readonly int Exp;

        public RewardResult(int gold, int exp)
        {
            Gold = gold;
            Exp = exp;
        }
    }
}
