using System.Collections.Generic;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatFlowOrchestrator : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;

        private CombatSession _session;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        public void BindSession(CombatSession session)
        {
            _session = session;
        }

        public bool SubmitPlayerDraftAndAdvance(
            CombatPlanDraft draft,
            ICombatant playerActor,
            out string errorMessage)
        {
            errorMessage = null;

            if (_session == null || entryPoint == null)
                return Fail("Combat flow is not bound to an entry point and session.", out errorMessage);

            if (!ReferenceEquals(_session, entryPoint.ActiveSession))
                return Fail("The bound combat session is stale.", out errorMessage);

            if (entryPoint.ActiveStateMachine == null || entryPoint.ActiveStateMachine.Phase != Phase.Planning)
                return Fail("Combat is not accepting plans outside Planning.", out errorMessage);

            CombatTurn turn = _session.CurrentTurn;
            if (turn == null || turn.Lifecycle != CombatTurnLifecycle.Planning)
                return Fail("The active turn has already been submitted.", out errorMessage);

            if (!CombatPlanValidator.TryNormalizePlayerDraft(
                    _session,
                    draft,
                    playerActor,
                    out ActionPlan playerPlan,
                    out errorMessage))
            {
                return false;
            }

            foreach (KeyValuePair<CombatantId, ActionPlan> pair in turn.Plans)
            {
                if (CombatPlanValidator.FindCombatant(_session, pair.Key) == null)
                    return Fail($"Existing plan actor {pair.Key.Value} is not in the active session.", out errorMessage);
            }

            Dictionary<CombatantId, ActionPlan> plans = new();
            plans.Add(playerActor.Id, playerPlan);

            if (!BuildAdditionalAllyPlans(turn, plans, out errorMessage) ||
                !BuildEnemyPlans(turn, plans, out errorMessage))
            {
                return false;
            }

            if (!turn.TryReplacePlans(plans))
                return Fail("The active turn stopped accepting plans before commitment.", out errorMessage);

            if (!entryPoint.SubmitCurrentTurn())
                return Fail("CombatEntryPoint rejected the committed turn.", out errorMessage);

            return true;
        }

        private bool BuildAdditionalAllyPlans(
            CombatTurn turn,
            Dictionary<CombatantId, ActionPlan> plans,
            out string errorMessage)
        {
            errorMessage = null;

            for (int i = 1; i < _session.Allies.Count; i++)
            {
                ICombatant ally = _session.Allies[i];
                if (ally == null)
                    return Fail("An additional ally is null.", out errorMessage);

                ActionPlan source = turn.TryGetPlan(ally.Id, out ActionPlan existing)
                    ? existing
                    : new ActionPlan(PlannedAction.None, PlannedAction.None);

                if (!CombatPlanValidator.TryNormalizePlan(_session, ally, source, out ActionPlan normalized, out errorMessage))
                    return false;

                plans.Add(ally.Id, normalized);
            }

            return true;
        }

        private bool BuildEnemyPlans(
            CombatTurn turn,
            Dictionary<CombatantId, ActionPlan> plans,
            out string errorMessage)
        {
            errorMessage = null;

            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy == null)
                    return Fail("An enemy combatant is null.", out errorMessage);

                ActionPlan source;
                if (turn.TryGetPlan(enemy.Id, out ActionPlan existing))
                {
                    source = existing;
                }
                else
                {
                    source = BuildDeterministicEnemyPlan(enemy);
                }

                if (!CombatPlanValidator.TryNormalizePlan(_session, enemy, source, out ActionPlan normalized, out errorMessage))
                    return false;

                plans.Add(enemy.Id, normalized);
            }

            return true;
        }

        private ActionPlan BuildDeterministicEnemyPlan(ICombatant enemy)
        {
            if (enemy.HP <= 0 || enemy.IsStunned || enemy.Skills == null || enemy.Skills.Count == 0)
                return NonePlan();

            int startIndex = _session.TurnIndex % enemy.Skills.Count;
            for (int offset = 0; offset < enemy.Skills.Count; offset++)
            {
                ISkill skill = enemy.Skills[(startIndex + offset) % enemy.Skills.Count];
                if (skill == null || !TryChooseEnemyTarget(enemy, skill, out CombatantId targetId))
                    continue;

                PlannedAction action = new PlannedAction(
                    skill.Id,
                    skill.Tag,
                    skill.Targeting,
                    targetId,
                    skill.Speed,
                    skill.ConsumesTurn);
                return new ActionPlan(action, PlannedAction.None);
            }

            return NonePlan();
        }

        private bool TryChooseEnemyTarget(ICombatant enemy, ISkill skill, out CombatantId targetId)
        {
            targetId = default;

            switch (skill.Targeting)
            {
                case TargetingRule.None:
                case TargetingRule.Environment:
                case TargetingRule.AllEnemies:
                case TargetingRule.AllAllies:
                    return true;

                case TargetingRule.Self:
                    targetId = enemy.Id;
                    return true;

                case TargetingRule.SingleAlly:
                    return TryFindFirstLiving(_session.Allies, out targetId);

                case TargetingRule.SingleEnemy:
                    return TryFindFirstLiving(_session.Enemies, out targetId);

                case TargetingRule.AnySingle:
                    return TryFindFirstLiving(_session.Allies, out targetId) ||
                           TryFindFirstLiving(_session.Enemies, out targetId);

                default:
                    return false;
            }
        }

        private static bool TryFindFirstLiving(IReadOnlyList<ICombatant> actors, out CombatantId id)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ICombatant actor = actors[i];
                if (actor != null && actor.HP > 0)
                {
                    id = actor.Id;
                    return true;
                }
            }

            id = default;
            return false;
        }

        private static ActionPlan NonePlan()
        {
            return new ActionPlan(PlannedAction.None, PlannedAction.None);
        }

        private static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }
}
