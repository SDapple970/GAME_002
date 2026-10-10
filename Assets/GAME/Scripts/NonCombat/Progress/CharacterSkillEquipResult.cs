namespace Game.NonCombat.Progress
{
    public enum CharacterSkillEquipStatus
    {
        Equipped,
        AlreadyEquipped,
        NotAcquired,
        InvalidCharacterId,
        InvalidPersistentSkillKey,
        Unequipped,
        NotEquipped,
        EquipNotAllowed,
        NotPartyMember,
        UnknownSkill,
        NotFilm
    }

    public readonly struct CharacterSkillEquipResult
    {
        public readonly string CharacterId;
        public readonly string PersistentSkillKey;
        public readonly CharacterSkillEquipStatus Status;

        public bool Changed => Status == CharacterSkillEquipStatus.Equipped ||
                               Status == CharacterSkillEquipStatus.Unequipped;

        public CharacterSkillEquipResult(
            string characterId,
            string persistentSkillKey,
            CharacterSkillEquipStatus status)
        {
            CharacterId = characterId;
            PersistentSkillKey = persistentSkillKey;
            Status = status;
        }
    }
}
