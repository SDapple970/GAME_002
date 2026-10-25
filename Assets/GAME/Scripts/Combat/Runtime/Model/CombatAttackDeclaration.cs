namespace Game.Combat.Model
{
    public sealed class CombatAttackDeclaration
    {
        public ICombatant Attacker { get; }
        public ICombatant Target { get; }
        public ISkill Skill { get; }
        public Side DeclaringSide { get; }
        public long? CardRuntimeId { get; }
        public int BpInvestment { get; }

        public CombatAttackDeclaration(ICombatant attacker, ICombatant target, ISkill skill)
            : this(attacker, target, skill, null)
        {
        }

        public CombatAttackDeclaration(ICombatant attacker, ICombatant target, ISkill skill,
            long? cardRuntimeId)
            : this(attacker, target, skill, cardRuntimeId, 0)
        {
        }

        public CombatAttackDeclaration(ICombatant attacker, ICombatant target, ISkill skill,
            long? cardRuntimeId, int bpInvestment)
        {
            Attacker = attacker;
            Target = target;
            Skill = skill;
            DeclaringSide = attacker != null ? attacker.Side : default;
            CardRuntimeId = cardRuntimeId;
            BpInvestment = bpInvestment;
        }
    }
}
