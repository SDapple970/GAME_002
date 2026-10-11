using Game.Combat.Data;

namespace Game.Combat.Model
{
    /// <summary>
    /// Immutable status data for presentation. It never grants mutation access to combat state.
    /// </summary>
    public sealed class CombatStatusSnapshot
    {
        internal CombatStatusSnapshot(CombatStatusRuntime status)
        {
            StatusId = status?.StatusId ?? string.Empty;
            DisplayName = status?.Definition?.DisplayName ?? StatusId;
            Polarity = status?.Polarity ?? CombatStatusPolarity.Debuff;
            StackCount = status?.StackCount ?? 0;
            RemainingDuration = status?.RemainingDuration ?? 0;
            DurationKind = status?.DurationKind ?? CombatStatusDurationKind.UntilRemoved;
            IsExpired = status?.IsExpired ?? true;
        }

        public string StatusId { get; }
        public string DisplayName { get; }
        public CombatStatusPolarity Polarity { get; }
        public int StackCount { get; }
        public int RemainingDuration { get; }
        public CombatStatusDurationKind DurationKind { get; }
        public bool IsExpired { get; }
    }
}
