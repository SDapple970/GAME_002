using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatOutcomeTargetResolver
    {
        public static bool TryResolve(
            CombatOutcomeAction action,
            CombatSession session,
            out CombatSkillExecutionRequest request)
        {
            request = null;
            if (action == null ||
                !TryCollectTargets(action.Actor, action.Skill, action.Opponent, session, out List<ICombatant> targets))
                return false;

            request = new CombatSkillExecutionRequest(
                action.Actor,
                action.Skill,
                targets,
                action.Opponent,
                action.SourceOutcome);
            return true;
        }

        public static bool CanResolve(
            ICombatant actor,
            ISkill skill,
            ICombatant opponent,
            CombatSession session)
        {
            return TryCollectTargets(actor, skill, opponent, session, out _);
        }

        private static bool TryCollectTargets(
            ICombatant actor,
            ISkill skill,
            ICombatant opponent,
            CombatSession session,
            out List<ICombatant> targets)
        {
            targets = null;
            if (!CanUseContext(actor, skill, opponent, session))
                return false;

            targets = new List<ICombatant>();
            switch (skill.Targeting)
            {
                case TargetingRule.None:
                    break;

                case TargetingRule.Self:
                    targets.Add(actor);
                    break;

                case TargetingRule.SingleEnemy:
                    if (!TryAddHostileOpponent(actor, opponent, session, targets))
                        return false;
                    break;

                case TargetingRule.SingleAlly:
                case TargetingRule.AnySingle:
                    return false;

                case TargetingRule.AllEnemies:
                    if (!AddLivingSide(session.GetSide(Opposite(actor.Side)), targets))
                        return false;
                    break;

                case TargetingRule.AllAllies:
                    if (!AddLivingSide(session.GetSide(actor.Side), targets))
                        return false;
                    break;

                case TargetingRule.Environment:
                default:
                    return false;
            }

            return true;
        }

        private static bool CanUseContext(
            ICombatant actor,
            ISkill skill,
            ICombatant opponent,
            CombatSession session)
        {
            if (actor == null || skill == null || opponent == null || session == null ||
                !session.TryGetCombatState(actor, out _) ||
                !session.TryGetCombatState(opponent, out _) ||
                actor.Skills == null)
            {
                return false;
            }

            for (int i = 0; i < actor.Skills.Count; i++)
            {
                if (ReferenceEquals(actor.Skills[i], skill))
                    return true;
            }

            return false;
        }

        private static bool TryAddHostileOpponent(
            ICombatant actor,
            ICombatant opponent,
            CombatSession session,
            List<ICombatant> targets)
        {
            if (opponent.HP <= 0 || !session.TryGetCombatState(opponent, out _) ||
                opponent.Side == actor.Side)
            {
                return false;
            }

            targets.Add(opponent);
            return true;
        }

        private static bool AddLivingSide(
            IReadOnlyList<ICombatant> roster,
            List<ICombatant> targets)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                ICombatant target = roster[i];
                if (target != null && target.HP > 0)
                    targets.Add(target);
            }

            return targets.Count > 0;
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }
    }
}
