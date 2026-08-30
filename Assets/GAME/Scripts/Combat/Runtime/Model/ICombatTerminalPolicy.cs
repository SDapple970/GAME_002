namespace Game.Combat.Model
{
    public interface ICombatTerminalPolicy
    {
        bool TryResolve(
            CombatTerminalCandidate candidate,
            CombatAftermathSnapshot snapshot,
            out CombatTerminalDecision decision);
    }
}
