using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Pure authorization for the FinalExchange Overcome command. It never mutates combat state.
    /// </summary>
    internal static class CombatOvercomePolicy
    {
        public static bool TryAuthorize(
            CombatSession session,
            Phase phase,
            ICombatant actor,
            int expectedExchangeVersion,
            out CombatOvercomeFailureReason failureReason)
        {
            failureReason = CombatOvercomeFailureReason.None;
            if (session == null || session.FlowMode != CombatFlowMode.StandoffClashChain ||
                session.ExchangeState == null)
            {
                failureReason = CombatOvercomeFailureReason.InvalidSession;
                return false;
            }

            if (expectedExchangeVersion < 0 || session.ExchangeState.Version != expectedExchangeVersion)
            {
                failureReason = CombatOvercomeFailureReason.StaleRequest;
                return false;
            }

            if (phase != Phase.Standoff)
            {
                failureReason = CombatOvercomeFailureReason.InvalidPhase;
                return false;
            }

            if (actor == null || !session.TryGetCombatState(actor, out CombatantCombatState state) ||
                !state.IsAlive)
            {
                failureReason = CombatOvercomeFailureReason.InvalidActor;
                return false;
            }

            if (FinalCombatTerminalPolicy.Evaluate(session) != CombatEndReason.None)
            {
                failureReason = CombatOvercomeFailureReason.CombatEnded;
                return false;
            }

            CombatExchangeState exchange = session.ExchangeState;
            if (exchange.CurrentAttackSide != actor.Side ||
                (exchange.CurrentAttackActor != null &&
                 !object.ReferenceEquals(exchange.CurrentAttackActor, actor)))
            {
                failureReason = CombatOvercomeFailureReason.NotActionAuthority;
                return false;
            }

            if (actor.IsStunned)
            {
                failureReason = CombatOvercomeFailureReason.Stunned;
                return false;
            }

            if (!state.IsPanicked)
            {
                failureReason = CombatOvercomeFailureReason.NotPanicked;
                return false;
            }

            return true;
        }
    }
}
