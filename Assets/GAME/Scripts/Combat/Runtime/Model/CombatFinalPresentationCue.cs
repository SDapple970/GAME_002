namespace Game.Combat.Model
{
    public sealed class CombatFinalPresentationCue
    {
        public CombatFinalPresentationCueKind Kind { get; }
        public ICombatant Actor { get; }
        public ICombatant Target { get; }
        public ISkill Skill { get; }

        internal CombatFinalPresentationCue(
            CombatFinalPresentationCueKind kind,
            ICombatant actor,
            ICombatant target,
            ISkill skill)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Skill = skill;
        }
    }
}
