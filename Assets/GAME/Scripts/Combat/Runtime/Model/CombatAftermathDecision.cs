namespace Game.Combat.Model
{
    public enum CombatAftermathDecisionKind
    {
        TerminalCandidate,
        TiePolicyRequired,
        AllOutCandidate,
        ChainDecisionRequired
    }

    public enum CombatTerminalCandidate
    {
        None,
        AlliesWiped,
        EnemiesWiped,
        BothWiped
    }

    public sealed class CombatAftermathDecision
    {
        public CombatAftermathDecisionKind Kind { get; }
        public CombatTerminalCandidate TerminalCandidate { get; }

        internal CombatAftermathDecision(
            CombatAftermathDecisionKind kind,
            CombatTerminalCandidate terminalCandidate = CombatTerminalCandidate.None)
        {
            Kind = kind;
            TerminalCandidate = kind == CombatAftermathDecisionKind.TerminalCandidate
                ? terminalCandidate
                : CombatTerminalCandidate.None;
        }
    }
}
