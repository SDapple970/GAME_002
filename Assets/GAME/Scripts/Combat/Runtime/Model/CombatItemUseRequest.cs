namespace Game.Combat.Model
{
    public sealed class CombatItemUseRequest
    {
        public CombatItemUseRequest(
            string itemId,
            ICombatant user,
            ICombatant target,
            int exchangeVersion)
        {
            ItemId = itemId;
            User = user;
            Target = target;
            ExchangeVersion = exchangeVersion;
        }

        public string ItemId { get; }
        public ICombatant User { get; }
        public ICombatant Target { get; }
        public int ExchangeVersion { get; }
    }
}
