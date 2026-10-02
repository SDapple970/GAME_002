using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatAllOutPolicy
    {
        public static bool IsCandidate(CombatSession session)
        {
            if (session == null)
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

            return livingEnemies > 0;
        }
    }
}
