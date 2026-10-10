namespace Game.NonCombat.Progress
{
    public enum UniqueSkillAvailabilityStatus
    {
        Unconfigured,
        Disabled,
        Locked,
        Unlocked,
        InvalidDefinition
    }

    public sealed class UniqueSkillAvailability
    {
        public string PersistentSkillKey { get; }
        public string DisplayName { get; }
        public int? RequiredLevel { get; }
        public UniqueSkillAvailabilityStatus Status { get; }
        public bool IsUnlocked => Status == UniqueSkillAvailabilityStatus.Unlocked;

        internal UniqueSkillAvailability(string key, int? requiredLevel, UniqueSkillAvailabilityStatus status, string displayName = null)
        {
            PersistentSkillKey = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "이름 없는 고유 스킬" : displayName;
            RequiredLevel = requiredLevel;
            Status = status;
        }
    }
}
