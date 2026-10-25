namespace Game.Combat.Model
{
    public enum CombatBpEnhancementKind
    {
        None = 0,
        ClashPower = 1,
        ExtraHits = 2,
        Healing = 3,
        StatusApplications = 4
    }

    public interface ICombatBpEnhancementProvider
    {
        CombatBpEnhancementKind BpEnhancementKind { get; }
        int BpEnhancementPerPoint { get; }
    }

    public readonly struct CombatBpEnhancementEntry
    {
        public ISkill Skill { get; }
        public CombatBpEnhancementKind Kind { get; }
        public int PerPoint { get; }

        public CombatBpEnhancementEntry(ISkill skill, CombatBpEnhancementKind kind, int perPoint)
        {
            Skill = skill;
            Kind = kind;
            PerPoint = perPoint;
        }
    }
}
