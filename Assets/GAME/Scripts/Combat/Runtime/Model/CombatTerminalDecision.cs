namespace Game.Combat.Model
{
    public sealed class CombatTerminalDecision
    {
        public CombatTerminalCandidate TerminalCandidate { get; }
        public CombatEndReason EndReason { get; }

        public CombatTerminalDecision(
            CombatTerminalCandidate terminalCandidate,
            CombatEndReason endReason)
        {
            TerminalCandidate = terminalCandidate;
            EndReason = endReason;
        }
    }
}
