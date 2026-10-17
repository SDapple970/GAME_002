namespace Game.Combat.Model
{
    /// <summary>Value identity for one revision of a declaration in one combat session.</summary>
    public readonly struct CombatDeclarationToken
    {
        public string SessionId { get; }
        public int DeclarationId { get; }
        public int Revision { get; }
        public int ExchangeVersion { get; }

        public CombatDeclarationToken(string sessionId, int declarationId, int revision, int exchangeVersion)
        {
            SessionId = sessionId;
            DeclarationId = declarationId;
            Revision = revision;
            ExchangeVersion = exchangeVersion;
        }
    }
}
