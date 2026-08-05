namespace Game.NonCombat.Inventory
{
    public enum InventoryMutationStatus
    {
        Success,
        Partial,
        InvalidItemId,
        UnknownDefinition,
        InvalidAmount,
        StackLimitReached,
        InsufficientQuantity,
        OverflowPrevented,
        NoChange
    }

    public readonly struct InventoryMutationResult
    {
        public readonly string ItemId;
        public readonly int RequestedAmount;
        public readonly int AppliedAmount;
        public readonly int ResultingCount;
        public readonly InventoryMutationStatus Status;

        public InventoryMutationResult(string itemId, int requestedAmount, int appliedAmount, int resultingCount, InventoryMutationStatus status)
        {
            ItemId = itemId;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            ResultingCount = resultingCount;
            Status = status;
        }

        public bool Changed => AppliedAmount > 0;
        public bool IsSuccess => Status == InventoryMutationStatus.Success || Status == InventoryMutationStatus.Partial;
    }
}
