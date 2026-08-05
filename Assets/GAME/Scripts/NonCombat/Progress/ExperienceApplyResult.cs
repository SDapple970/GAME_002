namespace Game.NonCombat.Progress
{
    public enum ExperienceApplyStatus { Settled, Pending, InvalidAmount, InvalidCharacterId, UnresolvedCharacter, InvalidDefinition, OverflowPrevented }

    public readonly struct ExperienceApplyResult
    {
        public readonly string CharacterId;
        public readonly int RequestedExperience;
        public readonly int AppliedExperience;
        public readonly int PreviousLevel;
        public readonly int ResultingLevel;
        public readonly int PreviousExperience;
        public readonly int ResultingExperience;
        public readonly int LevelsGained;
        public readonly ExperienceApplyStatus Status;

        public ExperienceApplyResult(string characterId, int requestedExperience, int appliedExperience, int previousLevel, int resultingLevel, int previousExperience, int resultingExperience, int levelsGained, ExperienceApplyStatus status)
        { CharacterId = characterId; RequestedExperience = requestedExperience; AppliedExperience = appliedExperience; PreviousLevel = previousLevel; ResultingLevel = resultingLevel; PreviousExperience = previousExperience; ResultingExperience = resultingExperience; LevelsGained = levelsGained; Status = status; }

        public bool Settled => Status == ExperienceApplyStatus.Settled;
        public bool Changed => AppliedExperience > 0;
    }
}
