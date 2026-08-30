namespace Game.Combat.Model
{
    public sealed class CombatOutcomeAction
    {
        public ICombatant Actor { get; }
        public ISkill Skill { get; }
        public ICombatant Opponent { get; }
        public CombatClashOutcome SourceOutcome { get; }

        internal CombatOutcomeAction(
            ICombatant actor,
            ISkill skill,
            ICombatant opponent,
            CombatClashOutcome sourceOutcome)
        {
            Actor = actor;
            Skill = skill;
            Opponent = opponent;
            SourceOutcome = sourceOutcome;
        }
    }
}
