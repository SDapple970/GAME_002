namespace Game.Combat.Model
{
    public sealed class CombatClashResult
    {
        public CombatClashOutcome Outcome { get; }
        public CombatAttackDeclaration AttackDeclaration { get; }
        public CombatResponseDeclaration ResponseDeclaration { get; }
        public ICombatant Winner { get; }

        internal CombatClashResult(
            CombatClashOutcome outcome,
            CombatAttackDeclaration attackDeclaration,
            CombatResponseDeclaration responseDeclaration,
            ICombatant winner)
        {
            Outcome = outcome;
            AttackDeclaration = attackDeclaration;
            ResponseDeclaration = responseDeclaration;
            Winner = winner;
        }
    }
}
