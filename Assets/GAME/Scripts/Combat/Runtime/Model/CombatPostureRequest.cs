namespace Game.Combat.Model
{
    public sealed class CombatPostureRequest
    {
        public ICombatant Winner { get; }
        public ICombatant Loser { get; }
        public ISkill WinningSkill { get; }
        public CombatClashOutcome ClashOutcome { get; }
        public CombatSkillExecutionResult SkillExecutionResult { get; }
        public int LoserPostureBefore { get; }
        public int LoserPostureMax { get; }

        internal CombatPostureRequest(
            ICombatant winner,
            ICombatant loser,
            ISkill winningSkill,
            CombatClashOutcome clashOutcome,
            CombatSkillExecutionResult skillExecutionResult,
            int loserPostureBefore,
            int loserPostureMax)
        {
            Winner = winner;
            Loser = loser;
            WinningSkill = winningSkill;
            ClashOutcome = clashOutcome;
            SkillExecutionResult = skillExecutionResult;
            LoserPostureBefore = loserPostureBefore;
            LoserPostureMax = loserPostureMax;
        }
    }
}
