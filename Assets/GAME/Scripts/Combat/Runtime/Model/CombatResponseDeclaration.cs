namespace Game.Combat.Model
{
    public sealed class CombatResponseDeclaration
    {
        public ICombatant Responder { get; }
        public ISkill Skill { get; }

        public CombatResponseDeclaration(ICombatant responder, ISkill skill)
        {
            Responder = responder;
            Skill = skill;
        }
    }
}
