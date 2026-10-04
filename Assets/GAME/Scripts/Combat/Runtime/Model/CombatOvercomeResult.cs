namespace Game.Combat.Model
{
    public sealed class CombatOvercomeResult
    {
        internal CombatOvercomeResult(
            ICombatant actor,
            bool wasAccepted,
            int mentalBefore,
            int mentalAfter,
            bool panicBefore,
            bool panicAfter,
            int exchangeVersion,
            CombatOvercomeFailureReason failureReason)
        {
            Actor = actor;
            WasAccepted = wasAccepted;
            MentalBefore = mentalBefore;
            MentalAfter = mentalAfter;
            PanicBefore = panicBefore;
            PanicAfter = panicAfter;
            ExchangeVersion = exchangeVersion;
            FailureReason = failureReason;
        }

        public ICombatant Actor { get; }
        public bool WasAccepted { get; }
        public int MentalBefore { get; }
        public int MentalAfter { get; }
        public bool PanicBefore { get; }
        public bool PanicAfter { get; }
        public int ExchangeVersion { get; }
        public CombatOvercomeFailureReason FailureReason { get; }
    }
}
