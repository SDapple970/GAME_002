namespace Game.NonCombat.Inventory
{
    public enum CurrencyMutationStatus
    {
        Success,
        InvalidAmount,
        InsufficientFunds,
        OverflowPrevented,
        NoChange
    }

    public readonly struct CurrencyMutationResult
    {
        public readonly int RequestedAmount;
        public readonly int AppliedAmount;
        public readonly int ResultingGold;
        public readonly CurrencyMutationStatus Status;

        public CurrencyMutationResult(int requestedAmount, int appliedAmount, int resultingGold, CurrencyMutationStatus status)
        {
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            ResultingGold = resultingGold;
            Status = status;
        }

        public bool Changed => AppliedAmount != 0;
        public bool IsSuccess => Status == CurrencyMutationStatus.Success;
    }
}
