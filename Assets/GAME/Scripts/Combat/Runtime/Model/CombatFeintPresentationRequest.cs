namespace Game.Combat.Model
{
    /// <summary>
    /// Immutable, presentation-only signal for an enemy feint. It contains identifiers only;
    /// display code resolves the actor from its currently bound combat session.
    /// </summary>
    public sealed class CombatFeintPresentationRequest
    {
        public string SessionId { get; }
        public int ExchangeVersion { get; }
        public CombatantId EnemyCombatantId { get; }
        public CombatFinalPresentationCueKind Kind => CombatFinalPresentationCueKind.Feint;

        internal CombatFeintPresentationRequest(
            string sessionId,
            int exchangeVersion,
            CombatantId enemyCombatantId)
        {
            SessionId = sessionId;
            ExchangeVersion = exchangeVersion;
            EnemyCombatantId = enemyCombatantId;
        }
    }
}
