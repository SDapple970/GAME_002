// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatEndEvaluator.cs
using System.Linq;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatEndEvaluator
    {
        public static CombatEndReason Evaluate(CombatSession session)
        {
            if (session == null)
                return CombatEndReason.Abort;

            bool alliesDead = session.Allies == null || session.Allies.Count == 0 || session.Allies.All(a => a == null || a.HP <= 0);
            bool enemiesDead = session.Enemies == null || session.Enemies.Count == 0 || session.Enemies.All(e => e == null || e.HP <= 0);

            if (alliesDead && enemiesDead)
                return CombatEndReason.Abort;

            if (enemiesDead)
                return CombatEndReason.Victory;

            if (alliesDead)
                return CombatEndReason.Defeat;

            return CombatEndReason.None;
        }
    }
}