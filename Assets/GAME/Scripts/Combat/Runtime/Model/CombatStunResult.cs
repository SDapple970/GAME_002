namespace Game.Combat.Model
{
    public sealed class CombatStunResult
    {
        public ICombatant Target { get; }
        public bool WasStunnedBefore { get; }
        public bool IsStunnedAfter { get; }
        public bool StunApplied { get; }

        internal CombatStunResult(
            ICombatant target,
            bool wasStunnedBefore,
            bool isStunnedAfter)
        {
            Target = target;
            WasStunnedBefore = wasStunnedBefore;
            IsStunnedAfter = isStunnedAfter;
            StunApplied = !wasStunnedBefore && isStunnedAfter;
        }
    }
}
