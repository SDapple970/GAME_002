namespace Game.Combat.Model
{
    public sealed class CombatClashRoundResult
    {
        public int ComparisonNumber { get; }
        public int AttackerPower { get; }
        public int ResponderPower { get; }
        public int AttackerDurabilityBefore { get; }
        public int AttackerDurabilityAfter { get; }
        public int ResponderDurabilityBefore { get; }
        public int ResponderDurabilityAfter { get; }
        public bool IsComplete { get; }
        public CombatClashOutcome? FinalOutcome { get; }

        internal CombatClashRoundResult(
            int comparisonNumber,
            int attackerPower,
            int responderPower,
            int attackerDurabilityBefore,
            int attackerDurabilityAfter,
            int responderDurabilityBefore,
            int responderDurabilityAfter,
            bool isComplete,
            CombatClashOutcome? finalOutcome)
        {
            ComparisonNumber = comparisonNumber;
            AttackerPower = attackerPower;
            ResponderPower = responderPower;
            AttackerDurabilityBefore = attackerDurabilityBefore;
            AttackerDurabilityAfter = attackerDurabilityAfter;
            ResponderDurabilityBefore = responderDurabilityBefore;
            ResponderDurabilityAfter = responderDurabilityAfter;
            IsComplete = isComplete;
            FinalOutcome = finalOutcome;
        }
    }
}
