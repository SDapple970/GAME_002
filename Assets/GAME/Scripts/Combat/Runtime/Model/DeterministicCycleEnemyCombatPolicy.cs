using System.Collections.Generic;

namespace Game.Combat.Model
{
    /// <summary>
    /// The current production baseline: choose skills in deterministic turn order
    /// and use the first valid runtime-living target for the skill's targeting rule.
    /// </summary>
    internal sealed class DeterministicCycleEnemyCombatPolicy :
        IEnemyCombatPlanPolicy,
        IEnemyCombatDecisionPolicy
    {
        public bool TryCreatePlan(EnemyCombatPlanRequest request, out ActionPlan plan)
        {
            plan = NonePlan();
            if (request == null)
                return false;

            if (!CanAct(request.Enemy, request.RuntimeState))
                return true;

            if (!TryCreateAction(request.Session, request.Enemy, out PlannedAction action, out _))
                return true;

            plan = new ActionPlan(action, PlannedAction.None);
            return true;
        }

        public bool TryCreateDeclaration(
            EnemyCombatDecisionRequest request,
            out CombatAttackDeclaration declaration)
        {
            declaration = null;
            CombatSession session = request?.Session;
            if (session == null)
                return false;

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                ICombatant enemy = session.Enemies[i];
                if (enemy == null ||
                    !session.TryGetCombatState(enemy, out CombatantCombatState runtimeState) ||
                    !CanAct(enemy, runtimeState) ||
                    !TryCreateAction(session, enemy, out PlannedAction action, out ICombatant target) ||
                    target == null || target.Side == enemy.Side)
                {
                    continue;
                }

                ISkill skill = FindSkill(enemy.Skills, action.SkillId);
                if (skill == null)
                    continue;

                declaration = new CombatAttackDeclaration(enemy, target, skill);
                return true;
            }

            return false;
        }

        private static bool TryCreateAction(
            CombatSession session,
            ICombatant enemy,
            out PlannedAction action,
            out ICombatant target)
        {
            action = PlannedAction.None;
            target = null;
            IReadOnlyList<ISkill> skills = enemy.Skills;
            if (skills == null || skills.Count == 0)
                return false;

            int startIndex = session.TurnIndex % skills.Count;
            for (int offset = 0; offset < skills.Count; offset++)
            {
                ISkill skill = skills[(startIndex + offset) % skills.Count];
                if (skill == null || !TryChooseTarget(session, enemy, skill, out ICombatant chosenTarget))
                    continue;

                target = chosenTarget;
                action = new PlannedAction(
                    skill.Id,
                    skill.Tag,
                    skill.Targeting,
                    chosenTarget != null ? chosenTarget.Id : default,
                    skill.Speed,
                    skill.ConsumesTurn);
                return true;
            }

            return false;
        }

        private static bool TryChooseTarget(
            CombatSession session,
            ICombatant enemy,
            ISkill skill,
            out ICombatant target)
        {
            target = null;

            switch (skill.Targeting)
            {
                case TargetingRule.None:
                case TargetingRule.Environment:
                case TargetingRule.AllEnemies:
                case TargetingRule.AllAllies:
                    return true;

                case TargetingRule.Self:
                    target = enemy;
                    return true;

                case TargetingRule.SingleAlly:
                    return TryFindFirstLiving(session, session.GetSide(enemy.Side), out target);

                case TargetingRule.SingleEnemy:
                    return TryFindFirstLiving(session, session.GetSide(Opposite(enemy.Side)), out target);

                case TargetingRule.AnySingle:
                    return TryFindFirstLiving(session, session.GetSide(Opposite(enemy.Side)), out target) ||
                           TryFindFirstLiving(session, session.GetSide(enemy.Side), out target);

                default:
                    return false;
            }
        }

        private static bool TryFindFirstLiving(
            CombatSession session,
            IReadOnlyList<ICombatant> actors,
            out ICombatant target)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ICombatant actor = actors[i];
                if (actor != null &&
                    session.TryGetCombatState(actor, out CombatantCombatState state) &&
                    state.IsAlive)
                {
                    target = actor;
                    return true;
                }
            }

            target = null;
            return false;
        }

        private static bool CanAct(ICombatant enemy, CombatantCombatState runtimeState)
        {
            return runtimeState.IsAlive && !enemy.IsStunned &&
                   enemy.Skills != null && enemy.Skills.Count > 0;
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }

        private static ISkill FindSkill(IReadOnlyList<ISkill> skills, SkillId skillId)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                ISkill skill = skills[i];
                if (skill != null && skill.Id.Equals(skillId))
                    return skill;
            }

            return null;
        }

        private static ActionPlan NonePlan()
        {
            return new ActionPlan(PlannedAction.None, PlannedAction.None);
        }
    }
}
