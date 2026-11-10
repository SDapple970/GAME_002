using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatAllOutPolicy
    {
        public static bool IsCandidate(CombatSession session)
        {
            if (session == null || session.IsCombatEnded ||
                FinalCombatTerminalPolicy.Evaluate(session) != CombatEndReason.None)
                return false;

            int livingEnemies = 0;
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                ICombatant enemy = session.Enemies[i];
                if (!CombatDeclarationPolicy.IsLivingMember(session, enemy))
                    continue;

                livingEnemies++;
                if (!enemy.IsStunned)
                    return false;
            }

            if (livingEnemies == 0)
                return false;

            foreach (ICombatant ally in session.Allies)
                if (CombatAllOutExecutionPolicy.CanParticipate(session, ally))
                    return true;

            return false;
        }
    }
}
