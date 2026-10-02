using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class FinalCombatTerminalPolicy : ICombatTerminalPolicy
    {
        public bool TryResolve(
            CombatTerminalCandidate candidate,
            CombatAftermathSnapshot snapshot,
            out CombatTerminalDecision decision)
        {
            decision = null;
            if (snapshot == null)
                return false;

            CombatEndReason reason;
            switch (candidate)
            {
                case CombatTerminalCandidate.EnemiesWiped:
                    if (snapshot.LivingAlliesCount <= 0 || snapshot.LivingEnemiesCount != 0)
                        return false;
                    reason = CombatEndReason.Victory;
                    break;
                case CombatTerminalCandidate.AlliesWiped:
                    if (snapshot.LivingAlliesCount != 0 || snapshot.LivingEnemiesCount <= 0)
                        return false;
                    reason = CombatEndReason.Defeat;
                    break;
                case CombatTerminalCandidate.BothWiped:
                    if (snapshot.LivingAlliesCount != 0 || snapshot.LivingEnemiesCount != 0)
                        return false;
                    reason = CombatEndReason.Defeat;
                    break;
                default:
                    return false;
            }

            decision = new CombatTerminalDecision(candidate, reason);
            return true;
        }

        public bool TryResolveExplicit(
            CombatEndReason requestedReason,
            out CombatTerminalDecision decision)
        {
            decision = null;
            if (requestedReason != CombatEndReason.Escape &&
                requestedReason != CombatEndReason.Abort &&
                requestedReason != CombatEndReason.Scripted)
            {
                return false;
            }

            decision = new CombatTerminalDecision(CombatTerminalCandidate.None, requestedReason);
            return true;
        }

        public static CombatEndReason Evaluate(CombatSession session)
        {
            if (session == null)
                return CombatEndReason.None;

            int livingAllies = CountLiving(session, session.Allies);
            int livingEnemies = CountLiving(session, session.Enemies);
            if (livingAllies == 0)
                return CombatEndReason.Defeat;

            return livingEnemies == 0 ? CombatEndReason.Victory : CombatEndReason.None;
        }

        private static int CountLiving(
            CombatSession session,
            System.Collections.Generic.IReadOnlyList<ICombatant> combatants)
        {
            int living = 0;
            for (int i = 0; i < combatants.Count; i++)
            {
                if (CombatDeclarationPolicy.IsLivingMember(session, combatants[i]))
                    living++;
            }

            return living;
        }
    }
}
