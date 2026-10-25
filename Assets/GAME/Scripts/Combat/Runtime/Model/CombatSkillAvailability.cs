namespace Game.Combat.Model
{
    public enum CombatSkillUnavailableReason
    {
        None = 0,
        InsufficientMp = 1,
        InvalidPhase = 2,
        InvalidTarget = 3,
        RestrictedByStatus = 4,
        NotInHand = 5
    }

    public readonly struct CombatSkillAvailability
    {
        public ISkill Skill { get; }
        public CombatSkillUnavailableReason Reason { get; }
        public bool IsAvailable => Reason == CombatSkillUnavailableReason.None;

        public CombatSkillAvailability(ISkill skill, CombatSkillUnavailableReason reason)
        {
            Skill = skill;
            Reason = reason;
        }
    }
}
