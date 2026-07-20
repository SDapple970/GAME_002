using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.Debugging
{
    [DisallowMultipleComponent]
    public sealed class CombatAutoPlanner : MonoBehaviour
    {
        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField] private bool autoSubmit = true;
        [SerializeField] private bool fillSecondSlot = true;
        [SerializeField] private float submitDelay = 0.25f;
        [SerializeField] private bool logPlans = true;

        private int _lastSubmittedTurnIndex = -1;
        private Coroutine _submitRoutine;

        private void Reset()
        {
            AutoBindReferences();
        }

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnDisable()
        {
            if (_submitRoutine != null)
            {
                StopCoroutine(_submitRoutine);
                _submitRoutine = null;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!CanPrepareSubmit())
                return;

            if (_submitRoutine != null)
                return;

            _submitRoutine = StartCoroutine(Co_SubmitAfterDelay(entryPoint.ActiveSession.TurnIndex));
#endif
        }

        private void AutoBindReferences()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        private bool CanPrepareSubmit()
        {
            if (!autoSubmit)
                return false;

            if (entryPoint == null)
                return false;

            if (entryPoint.ActiveSession == null)
                return false;

            if (entryPoint.ActiveStateMachine == null)
                return false;

            if (entryPoint.ActiveStateMachine.Phase != Phase.Planning)
                return false;

            return entryPoint.ActiveSession.TurnIndex != _lastSubmittedTurnIndex;
        }

        private IEnumerator Co_SubmitAfterDelay(int turnIndex)
        {
            if (submitDelay > 0f)
                yield return new WaitForSeconds(submitDelay);

            _submitRoutine = null;

            if (!CanPrepareSubmit())
                yield break;

            if (entryPoint.ActiveSession.TurnIndex != turnIndex)
                yield break;

            BuildPlans(entryPoint.ActiveSession);

            bool submitted = entryPoint.SubmitCurrentTurn();
            if (submitted)
                _lastSubmittedTurnIndex = turnIndex;
        }

        private void BuildPlans(CombatSession session)
        {
            if (session == null || session.CurrentTurn == null)
                return;

            ICombatant firstAliveAlly = FindFirstAlive(session.Allies);
            ICombatant firstAliveEnemy = FindFirstAlive(session.Enemies);

            BuildPlansForSide(session.Allies, firstAliveEnemy);
            BuildPlansForSide(session.Enemies, firstAliveAlly);
        }

        private void BuildPlansForSide(IReadOnlyList<ICombatant> actors, ICombatant target)
        {
            if (actors == null)
                return;

            for (int i = 0; i < actors.Count; i++)
            {
                ICombatant actor = actors[i];
                if (actor == null || actor.HP <= 0 || actor.IsStunned)
                    continue;

                ActionPlan plan = BuildPlan(actor, target);
                entryPoint.ActiveSession.CurrentTurn.SetPlan(actor.Id, plan);

                if (logPlans)
                {
                    Debug.Log(
                        $"[CombatAutoPlanner] Turn={entryPoint.ActiveSession.TurnIndex} Actor={actor.Id} " +
                        $"Slot1={PlanLabel(plan.Slot1)} Slot2={PlanLabel(plan.Slot2)}",
                        this
                    );
                }
            }
        }

        private ActionPlan BuildPlan(ICombatant actor, ICombatant target)
        {
            if (actor == null || actor.Skills == null || actor.Skills.Count == 0)
                return new ActionPlan(PlannedAction.None, PlannedAction.None);

            ISkill skill = ChooseSkill(actor.Skills);
            if (skill == null)
                return new ActionPlan(PlannedAction.None, PlannedAction.None);

            if (RequiresTarget(skill) && target == null)
                return new ActionPlan(PlannedAction.None, PlannedAction.None);

            CombatantId targetId = RequiresTarget(skill) ? target.Id : default;
            PlannedAction slot1 = new PlannedAction(
                skill.Id,
                skill.Tag,
                skill.Targeting,
                targetId,
                skill.Speed,
                skill.ConsumesTurn
            );

            PlannedAction slot2 = fillSecondSlot ? slot1 : PlannedAction.None;
            return new ActionPlan(slot1, slot2);
        }

        private static ISkill ChooseSkill(IReadOnlyList<ISkill> skills)
        {
            if (skills == null || skills.Count == 0)
                return null;

            ISkill fallback = null;
            ISkill nonInspectSingleTarget = null;
            ISkill utility = null;

            for (int i = 0; i < skills.Count; i++)
            {
                ISkill skill = skills[i];
                if (skill == null)
                    continue;

                if (fallback == null)
                    fallback = skill;

                if (skill.Tag == SkillTag.Attack &&
                    (skill.Targeting == TargetingRule.SingleEnemy || skill.Targeting == TargetingRule.AnySingle))
                    return skill;

                if (nonInspectSingleTarget == null &&
                    skill.Tag != SkillTag.Inspect &&
                    (skill.Targeting == TargetingRule.SingleEnemy ||
                     skill.Targeting == TargetingRule.AnySingle ||
                     skill.Targeting == TargetingRule.SingleAlly))
                {
                    nonInspectSingleTarget = skill;
                }

                if (utility == null &&
                    (skill.Targeting == TargetingRule.Self || skill.Targeting == TargetingRule.None))
                {
                    utility = skill;
                }
            }

            return nonInspectSingleTarget ?? utility ?? fallback;
        }

        private static ICombatant FindFirstAlive(IReadOnlyList<ICombatant> combatants)
        {
            if (combatants == null)
                return null;

            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant != null && combatant.HP > 0)
                    return combatant;
            }

            return null;
        }

        private static bool RequiresTarget(ISkill skill)
        {
            if (skill == null)
                return false;

            return skill.Targeting == TargetingRule.SingleEnemy ||
                   skill.Targeting == TargetingRule.SingleAlly ||
                   skill.Targeting == TargetingRule.AnySingle;
        }

        private static string PlanLabel(PlannedAction action)
        {
            return action.IsNone ? "None" : $"{action.SkillId}->{action.TargetCombatantId}";
        }
    }
}
