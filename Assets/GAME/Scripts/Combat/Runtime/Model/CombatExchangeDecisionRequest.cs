namespace Game.Combat.Model
{
    public enum CombatExchangeDecisionKind
    {
        Attack = 0,
        Response = 1,
        Chain = 2
    }

    public sealed class CombatExchangeDecisionRequest
    {
        public CombatExchangeDecisionKind Kind { get; }
        public int ExchangeVersion { get; }
        public Side ActingSide { get; }
        public ICombatant ActingActor { get; }
        public Phase Phase { get; }

        internal CombatExchangeDecisionRequest(
            CombatExchangeDecisionKind kind,
            int exchangeVersion,
            Side actingSide,
            ICombatant actingActor,
            Phase phase)
        {
            Kind = kind;
            ExchangeVersion = exchangeVersion;
            ActingSide = actingSide;
            ActingActor = actingActor;
            Phase = phase;
        }
    }
}
