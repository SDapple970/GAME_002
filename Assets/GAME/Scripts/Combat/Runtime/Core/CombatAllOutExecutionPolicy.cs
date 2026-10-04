using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// V1 technical rule: execute the first eligible ally's authored AllEnemies skill through
    /// the normal skill pipeline. Content owns the actual damage value; this policy owns no
    /// independent All-Out formula or mutable combat state.
    /// </summary>
    public static class CombatAllOutExecutionPolicy
    {
        public static bool TryCreateExecutionRequest(
            CombatSession session,
            out CombatSkillExecutionRequest request)
        {
            request = null;
            if (!CombatAllOutPolicy.IsCandidate(session))
                return false;

            for (int actorIndex = 0; actorIndex < session.Allies.Count; actorIndex++)
            {
                ICombatant actor = session.Allies[actorIndex];
                if (!CombatDeclarationPolicy.IsActiveMember(session, actor) ||
                    CombatDeclarationPolicy.IsPanicked(session, actor) || actor.Skills == null)
                    continue;

                for (int skillIndex = 0; skillIndex < actor.Skills.Count; skillIndex++)
                {
                    ISkill skill = actor.Skills[skillIndex];
                    if (skill == null || skill.Targeting != TargetingRule.AllEnemies ||
                        skill.BaseDamage <= 0)
                    {
                        continue;
                    }

                    CombatOutcomeAction action = new CombatOutcomeAction(
                        actor,
                        skill,
                        null,
                        CombatClashOutcome.AttackerWin);
                    return CombatOutcomeTargetResolver.TryResolve(action, session, out request);
                }
            }

            return false;
        }
    }
}
