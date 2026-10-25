namespace Game.Combat.Model
{
    public sealed class CombatResponseDeclaration
    {
        public ICombatant Responder { get; }
        public ISkill Skill { get; }
        public long? CardRuntimeId { get; }
        public int BpInvestment { get; }

        public CombatResponseDeclaration(ICombatant responder, ISkill skill)
            : this(responder, skill, null)
        {
        }

        public CombatResponseDeclaration(ICombatant responder, ISkill skill,
            long? cardRuntimeId)
            : this(responder, skill, cardRuntimeId, 0)
        {
        }

        public CombatResponseDeclaration(ICombatant responder, ISkill skill,
            long? cardRuntimeId, int bpInvestment)
        {
            Responder = responder;
            Skill = skill;
            CardRuntimeId = cardRuntimeId;
            BpInvestment = bpInvestment;
        }
    }
}
