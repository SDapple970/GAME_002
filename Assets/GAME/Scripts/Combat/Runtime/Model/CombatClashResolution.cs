namespace Game.Combat.Model
{
    public enum CombatClashDecisionStage
    {
        FirstScore,
        ReClashScore,
        Speed,
        CurrentAttackOwner
    }

    public sealed class CombatClashResolution
    {
        public CombatClashOutcome Outcome { get; }
        public int AttackerFirstVariance { get; }
        public int ResponderFirstVariance { get; }
        public int AttackerFirstScore { get; }
        public int ResponderFirstScore { get; }
        public bool ReClashOccurred { get; }
        public int? AttackerReClashVariance { get; }
        public int? ResponderReClashVariance { get; }
        public int? AttackerReClashScore { get; }
        public int? ResponderReClashScore { get; }
        public CombatClashDecisionStage DecisionStage { get; }
        public Side WinnerSide { get; }
        public Side NextAttackSide { get; }
        public int RandomSampleCount { get; }

        internal CombatClashResolution(
            CombatClashOutcome outcome,
            int attackerFirstVariance,
            int responderFirstVariance,
            int attackerFirstScore,
            int responderFirstScore,
            bool reClashOccurred,
            int? attackerReClashVariance,
            int? responderReClashVariance,
            int? attackerReClashScore,
            int? responderReClashScore,
            CombatClashDecisionStage decisionStage,
            Side winnerSide,
            int randomSampleCount)
        {
            Outcome = outcome;
            AttackerFirstVariance = attackerFirstVariance;
            ResponderFirstVariance = responderFirstVariance;
            AttackerFirstScore = attackerFirstScore;
            ResponderFirstScore = responderFirstScore;
            ReClashOccurred = reClashOccurred;
            AttackerReClashVariance = attackerReClashVariance;
            ResponderReClashVariance = responderReClashVariance;
            AttackerReClashScore = attackerReClashScore;
            ResponderReClashScore = responderReClashScore;
            DecisionStage = decisionStage;
            WinnerSide = winnerSide;
            NextAttackSide = winnerSide;
            RandomSampleCount = randomSampleCount;
        }
    }
}
