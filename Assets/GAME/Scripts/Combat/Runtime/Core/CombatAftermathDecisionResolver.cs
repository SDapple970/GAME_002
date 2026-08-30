using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatAftermathDecisionResolver
    {
        public static CombatAftermathDecision Resolve(CombatAftermathSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            CombatTerminalCandidate terminalCandidate = ResolveTerminalCandidate(snapshot);
            if (terminalCandidate != CombatTerminalCandidate.None)
            {
                return new CombatAftermathDecision(
                    CombatAftermathDecisionKind.TerminalCandidate,
                    terminalCandidate);
            }

            if (snapshot.RequiresTiePolicy)
                return new CombatAftermathDecision(CombatAftermathDecisionKind.TiePolicyRequired);

            if (snapshot.LivingEnemiesCount > 0 &&
                snapshot.StunnedLivingEnemiesCount == snapshot.LivingEnemiesCount)
            {
                return new CombatAftermathDecision(CombatAftermathDecisionKind.AllOutCandidate);
            }

            return new CombatAftermathDecision(CombatAftermathDecisionKind.ChainDecisionRequired);
        }

        private static CombatTerminalCandidate ResolveTerminalCandidate(CombatAftermathSnapshot snapshot)
        {
            bool alliesWiped = snapshot.LivingAlliesCount == 0;
            bool enemiesWiped = snapshot.LivingEnemiesCount == 0;

            if (alliesWiped && enemiesWiped)
                return CombatTerminalCandidate.BothWiped;
            if (alliesWiped)
                return CombatTerminalCandidate.AlliesWiped;
            if (enemiesWiped)
                return CombatTerminalCandidate.EnemiesWiped;

            return CombatTerminalCandidate.None;
        }
    }
}
