namespace Game.NonCombat.Progress
{
    public enum CharacterSkillAcquireStatus
    {
        Acquired,
        AlreadyOwned,
        InvalidCharacterId,
        InvalidPersistentSkillKey
    }

    public readonly struct CharacterSkillAcquireResult
    {
        public readonly string CharacterId;
        public readonly string PersistentSkillKey;
        public readonly CharacterSkillAcquireStatus Status;

        public bool Changed => Status == CharacterSkillAcquireStatus.Acquired;

        public CharacterSkillAcquireResult(
            string characterId,
            string persistentSkillKey,
            CharacterSkillAcquireStatus status)
        {
            CharacterId = characterId;
            PersistentSkillKey = persistentSkillKey;
            Status = status;
        }
    }
}
