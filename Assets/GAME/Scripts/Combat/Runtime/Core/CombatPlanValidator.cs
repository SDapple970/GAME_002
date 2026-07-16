using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatPlanValidator
    {
        public static bool ValidatePlayerDraft(
            CombatSession session,
            CombatPlanDraft draft,
            ICombatant actor,
            out string errorMessage)
        {
            return TryNormalizePlayerDraft(session, draft, actor, out _, out errorMessage);
        }

        public static bool TryNormalizePlayerDraft(
            CombatSession session,
            CombatPlanDraft draft,
            ICombatant actor,
            out ActionPlan normalizedPlan,
            out string errorMessage)
        {
            normalizedPlan = new ActionPlan(PlannedAction.None, PlannedAction.None);
            errorMessage = null;

            if (session == null)
                return Fail("Combat session is missing.", out errorMessage);

            if (draft == null)
                return Fail("Player plan draft is missing.", out errorMessage);

            if (actor == null)
                return Fail("Player-controlled actor is missing.", out errorMessage);

            if (session.Allies.Count == 0 || actor.Side != Side.Allies || !ReferenceEquals(session.Allies[0], actor))
                return Fail("The submitted actor is not the canonical player-controlled ally.", out errorMessage);

            if (draft.Plans.Count != 1 || !draft.TryGetPlan(actor.Id, out ActionPlan plan))
                return Fail("The draft must contain exactly one plan for the player-controlled ally.", out errorMessage);

            if (!TryNormalizePlan(session, actor, plan, out normalizedPlan, out errorMessage))
                return false;

            if (actor.HP > 0 && !actor.IsStunned && normalizedPlan.Slot1.IsNone && normalizedPlan.Slot2.IsNone)
                return Fail("At least one player action is required.", out errorMessage);

            return true;
        }

        public static bool TryNormalizePlan(
            CombatSession session,
            ICombatant actor,
            ActionPlan plan,
            out ActionPlan normalizedPlan,
            out string errorMessage)
        {
            normalizedPlan = new ActionPlan(PlannedAction.None, PlannedAction.None);
            errorMessage = null;

            if (!TryValidateActor(session, actor, out errorMessage))
                return false;

            if (actor.HP <= 0 || actor.IsStunned)
            {
                normalizedPlan = new ActionPlan(PlannedAction.None, PlannedAction.None);
                return true;
            }

            if (!TryNormalizeAction(session, actor, plan.Slot1, 1, out PlannedAction slot1, out errorMessage) ||
                !TryNormalizeAction(session, actor, plan.Slot2, 2, out PlannedAction slot2, out errorMessage))
            {
                return false;
            }

            normalizedPlan = new ActionPlan(slot1, slot2);
            return true;
        }

        public static bool TryNormalizeCommittedPlans(
            CombatSession session,
            IReadOnlyDictionary<CombatantId, ActionPlan> committedPlans,
            out Dictionary<CombatantId, ActionPlan> normalizedPlans,
            out string errorMessage)
        {
            normalizedPlans = new Dictionary<CombatantId, ActionPlan>();
            errorMessage = null;

            if (session == null || session.CurrentTurn == null)
                return Fail("The active turn is missing.", out errorMessage);

            if (committedPlans == null)
                return Fail("Committed plans are missing.", out errorMessage);

            foreach (KeyValuePair<CombatantId, ActionPlan> pair in committedPlans)
            {
                if (FindCombatant(session, pair.Key) == null)
                    return Fail($"Plan actor {pair.Key.Value} does not belong to the active session.", out errorMessage);
            }

            if (!NormalizeSide(session, session.Allies, Side.Allies, committedPlans, normalizedPlans, out errorMessage) ||
                !NormalizeSide(session, session.Enemies, Side.Enemies, committedPlans, normalizedPlans, out errorMessage))
            {
                return false;
            }

            return true;
        }

        private static bool NormalizeSide(
            CombatSession session,
            IReadOnlyList<ICombatant> actors,
            Side expectedSide,
            IReadOnlyDictionary<CombatantId, ActionPlan> committedPlans,
            Dictionary<CombatantId, ActionPlan> normalizedPlans,
            out string errorMessage)
        {
            errorMessage = null;

            for (int i = 0; i < actors.Count; i++)
            {
                ICombatant actor = actors[i];
                if (!TryValidateActor(session, actor, out errorMessage))
                    return false;

                if (actor.Side != expectedSide)
                    return Fail($"Actor {actor.Id.Value} is on {actor.Side}, expected {expectedSide}.", out errorMessage);

                if (!committedPlans.TryGetValue(actor.Id, out ActionPlan plan))
                    return Fail($"Living/dead actor {actor.Id.Value} has no explicit plan.", out errorMessage);

                if (!TryNormalizePlan(session, actor, plan, out ActionPlan normalized, out errorMessage))
                    return false;

                normalizedPlans.Add(actor.Id, normalized);
            }

            return true;
        }

        private static bool TryNormalizeAction(
            CombatSession session,
            ICombatant actor,
            PlannedAction action,
            int slot,
            out PlannedAction normalized,
            out string errorMessage)
        {
            normalized = PlannedAction.None;
            errorMessage = null;

            if (action.IsNone)
                return true;

            ISkill skill = FindSkill(actor, action.SkillId);
            if (skill == null)
                return Fail($"Actor {actor.Id.Value} slot {slot} references unknown skill {action.SkillId.Value}.", out errorMessage);

            CombatantId targetId = default;
            switch (skill.Targeting)
            {
                case TargetingRule.None:
                case TargetingRule.Environment:
                    break;

                case TargetingRule.Self:
                    targetId = actor.Id;
                    break;

                case TargetingRule.SingleEnemy:
                    if (!TryResolveLivingTarget(session, action.TargetCombatantId, Side.Enemies, out ICombatant enemy, out errorMessage))
                        return false;
                    targetId = enemy.Id;
                    break;

                case TargetingRule.SingleAlly:
                    if (!TryResolveLivingTarget(session, action.TargetCombatantId, Side.Allies, out ICombatant ally, out errorMessage))
                        return false;
                    targetId = ally.Id;
                    break;

                case TargetingRule.AnySingle:
                    ICombatant target = FindCombatant(session, action.TargetCombatantId);
                    if (target == null || target.HP <= 0)
                        return Fail($"Actor {actor.Id.Value} slot {slot} has an invalid or dead AnySingle target.", out errorMessage);
                    targetId = target.Id;
                    break;

                case TargetingRule.AllEnemies:
                    if (!HasLivingTarget(session.Enemies))
                        return Fail($"Actor {actor.Id.Value} slot {slot} has no living enemy targets.", out errorMessage);
                    break;

                case TargetingRule.AllAllies:
                    if (!HasLivingTarget(session.Allies))
                        return Fail($"Actor {actor.Id.Value} slot {slot} has no living ally targets.", out errorMessage);
                    break;

                default:
                    return Fail($"Actor {actor.Id.Value} slot {slot} uses unsupported targeting rule {skill.Targeting}.", out errorMessage);
            }

            normalized = new PlannedAction(
                skill.Id,
                skill.Tag,
                skill.Targeting,
                targetId,
                skill.Speed,
                skill.ConsumesTurn);
            return true;
        }

        private static bool TryResolveLivingTarget(
            CombatSession session,
            CombatantId targetId,
            Side expectedSide,
            out ICombatant target,
            out string errorMessage)
        {
            target = FindCombatant(session, targetId);
            if (target == null)
                return Fail($"Target {targetId.Value} does not belong to the active session.", out errorMessage);

            if (target.Side != expectedSide)
                return Fail($"Target {targetId.Value} is on {target.Side}, expected {expectedSide}.", out errorMessage);

            if (target.HP <= 0)
                return Fail($"Target {targetId.Value} is dead.", out errorMessage);

            errorMessage = null;
            return true;
        }

        private static bool TryValidateActor(CombatSession session, ICombatant actor, out string errorMessage)
        {
            if (session == null)
                return Fail("Combat session is missing.", out errorMessage);

            if (actor == null)
                return Fail("A required combat actor is null.", out errorMessage);

            ICombatant sessionActor = FindCombatant(session, actor.Id);
            if (!ReferenceEquals(sessionActor, actor))
                return Fail($"Actor {actor.Id.Value} does not belong to the active session.", out errorMessage);

            errorMessage = null;
            return true;
        }

        public static ICombatant FindCombatant(CombatSession session, CombatantId id)
        {
            if (session == null)
                return null;

            for (int i = 0; i < session.Allies.Count; i++)
            {
                ICombatant actor = session.Allies[i];
                if (actor != null && actor.Id.Value == id.Value)
                    return actor;
            }

            for (int i = 0; i < session.Enemies.Count; i++)
            {
                ICombatant actor = session.Enemies[i];
                if (actor != null && actor.Id.Value == id.Value)
                    return actor;
            }

            return null;
        }

        private static ISkill FindSkill(ICombatant actor, SkillId skillId)
        {
            if (actor?.Skills == null)
                return null;

            for (int i = 0; i < actor.Skills.Count; i++)
            {
                ISkill skill = actor.Skills[i];
                if (skill != null && skill.Id.Value == skillId.Value)
                    return skill;
            }

            return null;
        }

        private static bool HasLivingTarget(IReadOnlyList<ICombatant> actors)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].HP > 0)
                    return true;
            }

            return false;
        }

        private static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }
}
