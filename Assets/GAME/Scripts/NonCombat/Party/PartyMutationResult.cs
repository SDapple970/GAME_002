namespace Game.NonCombat.Party
{
    public enum PartyMutationStatus { Success, InvalidCharacterId, AlreadyMember, NotOwned, InvalidLeader, NoChange }

    public readonly struct PartyMutationResult
    {
        public readonly string CharacterId;
        public readonly PartyMutationStatus Status;
        public bool Changed => Status == PartyMutationStatus.Success;

        public PartyMutationResult(string characterId, PartyMutationStatus status)
        { CharacterId = characterId; Status = status; }
    }
}
