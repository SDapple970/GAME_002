namespace Game.Combat.Model
{
    public enum CombatItemUseStatus
    {
        Success,
        InvalidRequest,
        InvalidPhase,
        StaleRequest,
        MissingExecutor,
        MissingDefinition,
        InsufficientInventory,
        InvalidTarget,
        EffectNotApplicable,
        InventoryMutationFailed,
        EffectFailed,
        FlowTransitionFailed
    }

    public sealed class CombatItemUseResult
    {
        internal CombatItemUseResult(
            CombatItemUseStatus status,
            string itemId,
            bool itemConsumed,
            bool effectApplied,
            int hpRecovered,
            CombatStatusApplicationResult statusApplication)
        {
            Status = status;
            ItemId = itemId;
            ItemConsumed = itemConsumed;
            EffectApplied = effectApplied;
            HpRecovered = hpRecovered;
            StatusApplication = statusApplication;
        }

        public CombatItemUseStatus Status { get; }
        public string ItemId { get; }
        public bool ItemConsumed { get; }
        public bool EffectApplied { get; }
        public int HpRecovered { get; }
        public CombatStatusApplicationResult StatusApplication { get; }
        public bool Succeeded => Status == CombatItemUseStatus.Success;

        internal static CombatItemUseResult Failure(CombatItemUseStatus status, string itemId = null) =>
            new CombatItemUseResult(status, itemId, false, false, 0, null);
    }
}
