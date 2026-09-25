using System.Collections.Generic;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatFlowOrchestrator : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;

        private readonly DeterministicCycleEnemyCombatPolicy _enemyPolicy = new DeterministicCycleEnemyCombatPolicy();
        private CombatSession _session;
        private CombatStateMachine _boundStateMachine;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        public void BindSession(CombatSession session)
        {
            UnsubscribeStandoffDecision();
            _session = session;

            if (_session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                entryPoint == null || !ReferenceEquals(_session, entryPoint.ActiveSession) ||
                entryPoint.ActiveStateMachine == null)
            {
                return;
            }

            _boundStateMachine = entryPoint.ActiveStateMachine;
            _boundStateMachine.OnEnemyActionRequired += HandleEnemyActionRequired;
        }

        private void OnDisable()
        {
            UnsubscribeStandoffDecision();
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
                else if (!_enemyPolicy.TryCreatePlan(
                             new EnemyCombatPlanRequest(_session, enemy),
                             out source))
                {
                    return Fail($"Enemy policy could not create a plan for {enemy.Id.Value}.", out errorMessage);
                }

                if (!CombatPlanValidator.TryNormalizePlan(_session, enemy, source, out ActionPlan normalized, out errorMessage))
                    return false;

                plans.Add(enemy.Id, normalized);
            }

            return true;
        }

        private void HandleEnemyActionRequired(CombatSession session)
        {
            if (!ReferenceEquals(session, _session) || _boundStateMachine == null ||
                entryPoint == null || !ReferenceEquals(_boundStateMachine, entryPoint.ActiveStateMachine))
            {
                return;
            }

            _boundStateMachine.TrySubmitEnemyDecision(_enemyPolicy);
        }

        private void UnsubscribeStandoffDecision()
        {
            if (_boundStateMachine == null)
                return;

            _boundStateMachine.OnEnemyActionRequired -= HandleEnemyActionRequired;
            _boundStateMachine = null;
        }

        private static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }
}
