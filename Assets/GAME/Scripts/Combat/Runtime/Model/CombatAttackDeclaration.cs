namespace Game.Combat.Model
{
    public sealed class CombatAttackDeclaration
    {
        public ICombatant Attacker { get; }
        public ICombatant Target { get; }
        public ISkill Skill { get; }
        public Side DeclaringSide { get; }

        public CombatAttackDeclaration(ICombatant attacker, ICombatant target, ISkill skill)
        {
            Attacker = attacker;
            Target = target;
            Skill = skill;
            DeclaringSide = attacker != null ? attacker.Side : default;
        }
    }
}
