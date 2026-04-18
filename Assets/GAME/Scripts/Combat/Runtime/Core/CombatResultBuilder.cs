// GAME_002/Assets/GAME/Scripts/Combat/Core/CombatResultBuilder.cs
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatResultBuilder
    {
        public static CombatResult Build(CombatSession session, CombatEndReason endReason)
        {
            var result = new CombatResult
            {
                EndReason = endReason,
                IsWin = endReason == CombatEndReason.Victory,
                EscapeSucceeded = endReason == CombatEndReason.Escape,
                TotalExp = 0,
                TotalGold = 0
            };

            if (session == null)
                return result;

            // 임시 보상 규칙: 승리 시에만 지급
            if (endReason == CombatEndReason.Victory)
            {
                result.TotalExp = 150;
                result.TotalGold = 50;
            }

            if (session.Enemies != null)
            {
                for (int i = 0; i < session.Enemies.Count; i++)
                {
                    var enemy = session.Enemies[i];
                    if (enemy == null) continue;

                    if (enemy.HP <= 0)
                        result.DefeatedEnemyIds.Add(enemy.Id.Value);
                }
            }

            if (session.Allies != null)
            {
                for (int i = 0; i < session.Allies.Count; i++)
                {
                    var ally = session.Allies[i];
                    if (ally == null) continue;

                    if (ally.HP > 0)
                        result.SurvivedAllyIds.Add(ally.Id.Value);
                }
            }

            return result;
        }
    }
}