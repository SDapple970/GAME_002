namespace Game.Combat.Model
{
    public sealed class CombatMentalMutationResult
    {
        internal CombatMentalMutationResult(
            ICombatant target,
            int mentalBefore,
            int mentalAfter,
            int requestedDelta,
            int appliedDelta,
            bool panicBefore,
            bool panicAfter)
        {
            Target = target;
            MentalBefore = mentalBefore;
            MentalAfter = mentalAfter;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            PanicBefore = panicBefore;
            PanicAfter = panicAfter;
            PanicApplied = !panicBefore && panicAfter;
        }

        public ICombatant Target { get; }
        public int MentalBefore { get; }
        public int MentalAfter { get; }
        public int RequestedDelta { get; }
        public int AppliedDelta { get; }
        public bool PanicBefore { get; }
        public bool PanicAfter { get; }
        public bool PanicApplied { get; }
        public bool ThresholdCrossed => PanicApplied;
    }
}
