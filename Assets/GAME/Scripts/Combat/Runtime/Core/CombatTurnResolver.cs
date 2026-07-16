using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Combat.Data;
using Game.Combat.Model;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace Game.Combat.Core
{
    internal interface ICombatRandomSource
    {
        int NextInclusive(int minimum, int maximum);
    }

    internal sealed class SeededCombatRandomSource : ICombatRandomSource
    {
        private readonly System.Random _random;

        public SeededCombatRandomSource(int seed)
        {
            _random = new System.Random(seed);
        }

        public int NextInclusive(int minimum, int maximum)
        {
            return _random.Next(minimum, maximum + 1);
        }
    }

    internal sealed class FixedCombatRandomSource : ICombatRandomSource
    {
        private readonly int[] _values;
        private int _index;

        public FixedCombatRandomSource(params int[] values)
        {
            _values = values ?? Array.Empty<int>();
        }

        public int NextInclusive(int minimum, int maximum)
        {
            int value = _values.Length == 0 ? minimum : _values[Math.Min(_index++, _values.Length - 1)];
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public static class CombatTurnResolver
    {
        private sealed class UnityCombatRandomSource : ICombatRandomSource
        {
            public int NextInclusive(int minimum, int maximum)
            {
                return UnityEngine.Random.Range(minimum, maximum + 1);
            }
        }

        private sealed class PendingAction
        {
            public ICombatant Actor;
            public PlannedAction Plan;
            public ISkill Skill;
            public ICombatant Target;
            public int RosterOrder;
        }

        public static bool ResolveTurn(CombatSession session)
        {
            return ResolveTurn(session, new UnityCombatRandomSource());
        }

        internal static bool ResolveTurn(CombatSession session, ICombatRandomSource randomSource)
        {
            if (session == null)
            {
                Debug.LogError("[CombatTurnResolver] Cannot resolve a null session.");
                return false;
            }

            CombatTurn turn = session.CurrentTurn;
            if (turn == null)
            {
                Debug.LogError($"[CombatTurnResolver] Session turn {session.TurnIndex} has no CombatTurn.");
                return false;
            }

            if (turn.Lifecycle != CombatTurnLifecycle.Submitted)
            {
                Debug.LogWarning(
                    $"[CombatTurnResolver] Turn {session.TurnIndex} resolution rejected at lifecycle {turn.Lifecycle}.");
                return false;
            }

            if (randomSource == null)
            {
                Debug.LogError($"[CombatTurnResolver] Turn {session.TurnIndex} has no random source.");
                return false;
            }

            if (!CombatPlanValidator.TryNormalizeCommittedPlans(
                    session,
                    turn.Plans,
                    out Dictionary<CombatantId, ActionPlan> normalized,
                    out string validationError))
            {
                Debug.LogError($"[CombatTurnResolver] Turn {session.TurnIndex} prevalidation failed: {validationError}");
                return false;
            }

            if (!PlansMatch(turn.Plans, normalized))
            {
                Debug.LogError($"[CombatTurnResolver] Turn {session.TurnIndex} contains non-authoritative action metadata.");
                return false;
            }

            string actionContext = "preparation";
            if (!turn.ClearResolutionResults())
                return false;

            try
            {
                ResolveSlotPhase(session, 1, randomSource, ref actionContext);
                ResolveSlotPhase(session, 2, randomSource, ref actionContext);

                if (!turn.TryMarkResolved())
                {
                    Debug.LogError($"[CombatTurnResolver] Turn {session.TurnIndex} could not mark resolution complete.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                turn.TryMarkResolutionFailed();
                Debug.LogError(
                    $"[CombatTurnResolver] Turn {session.TurnIndex} failed during {actionContext}. " +
                    "Runtime mutations already applied before this exception are not rolled back.");
                Debug.LogException(exception);
                return false;
            }
        }

        private static void ResolveSlotPhase(
            CombatSession session,
            int slotIndex,
            ICombatRandomSource randomSource,
            ref string actionContext)
        {
            List<PendingAction> actions = CollectActions(session, slotIndex);
            actions.Sort((left, right) => CompareActions(session, left, right));

            HashSet<PendingAction> processed = new();
            for (int i = 0; i < actions.Count; i++)
            {
                PendingAction action = actions[i];
                if (processed.Contains(action))
                    continue;

                PendingAction mutual = FindMutualAction(actions, action, processed);
                if (mutual != null)
                {
                    processed.Add(action);
                    processed.Add(mutual);
                    actionContext = $"slot {slotIndex} clash {action.Actor.Id.Value}<->{mutual.Actor.Id.Value}";
                    ExecuteClash(session, action, mutual, randomSource);
                    continue;
                }

                processed.Add(action);
                actionContext = $"slot {slotIndex} actor {action.Actor.Id.Value} skill {action.Skill.Id.Value}";

                if (action.Plan.Targeting == TargetingRule.AllEnemies ||
                    action.Plan.Targeting == TargetingRule.AllAllies)
                {
                    ExecuteArea(session, action);
                }
                else
                {
                    ExecuteUnopposed(session, action);
                }
            }
        }

        private static List<PendingAction> CollectActions(CombatSession session, int slotIndex)
        {
            List<PendingAction> actions = new();
            int rosterOrder = 0;
            CollectSide(session, session.Allies, slotIndex, actions, ref rosterOrder);
            CollectSide(session, session.Enemies, slotIndex, actions, ref rosterOrder);
            return actions;
        }

        private static void CollectSide(
            CombatSession session,
            IReadOnlyList<ICombatant> actors,
            int slotIndex,
            List<PendingAction> actions,
            ref int rosterOrder)
        {
            for (int i = 0; i < actors.Count; i++, rosterOrder++)
            {
                ICombatant actor = actors[i];
                if (actor == null)
                    continue;

                if (!session.CurrentTurn.TryGetPlan(actor.Id, out ActionPlan plan))
                    continue;

                PlannedAction action = slotIndex == 1 ? plan.Slot1 : plan.Slot2;
                if (action.IsNone)
                    continue;

                ISkill skill = FindSkill(actor, action.SkillId);
                if (skill == null)
                    continue;

                ICombatant target = IsSingleTarget(action.Targeting)
                    ? CombatPlanValidator.FindCombatant(session, action.TargetCombatantId)
                    : action.Targeting == TargetingRule.Self ? actor : null;

                actions.Add(new PendingAction
                {
                    Actor = actor,
                    Plan = action,
                    Skill = skill,
                    Target = target,
                    RosterOrder = rosterOrder
                });
            }
        }

        private static int CompareActions(CombatSession session, PendingAction left, PendingAction right)
        {
            int comparison = right.Plan.PlannedSpeed.CompareTo(left.Plan.PlannedSpeed);
            if (comparison != 0)
                return comparison;

            int leftInitiative = left.Actor.Side == session.InitiativeSide ? 0 : 1;
            int rightInitiative = right.Actor.Side == session.InitiativeSide ? 0 : 1;
            comparison = leftInitiative.CompareTo(rightInitiative);
            if (comparison != 0)
                return comparison;

            comparison = left.RosterOrder.CompareTo(right.RosterOrder);
            if (comparison != 0)
                return comparison;

            return left.Actor.Id.Value.CompareTo(right.Actor.Id.Value);
        }

        private static PendingAction FindMutualAction(
            IReadOnlyList<PendingAction> actions,
            PendingAction action,
            HashSet<PendingAction> processed)
        {
            if (!IsSingleTarget(action.Plan.Targeting) || action.Target == null)
                return null;

            for (int i = 0; i < actions.Count; i++)
            {
                PendingAction candidate = actions[i];
                if (ReferenceEquals(candidate, action) || processed.Contains(candidate))
                    continue;

                if (IsSingleTarget(candidate.Plan.Targeting) &&
                    ReferenceEquals(candidate.Actor, action.Target) &&
                    ReferenceEquals(candidate.Target, action.Actor))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ExecuteClash(
            CombatSession session,
            PendingAction a,
            PendingAction b,
            ICombatRandomSource randomSource)
        {
            Event_Clash playbookEvent = new Event_Clash
            {
                ActorA = a.Actor,
                SkillA = a.Skill,
                ActorB = b.Actor,
                SkillB = b.Skill
            };

            if (!CanExecute(a) || !CanExecute(b) ||
                !ReferenceEquals(a.Target, b.Actor) || !ReferenceEquals(b.Target, a.Actor))
            {
                CancelClash(session, playbookEvent, "[Clash cancelled] A participant or mutual target is no longer valid.", false);
                return;
            }

            long totalCostLong = (long)Math.Max(0, a.Skill.InspirationCost) + Math.Max(0, b.Skill.InspirationCost);
            if (totalCostLong > int.MaxValue || !session.Inspiration.CanSpend((int)totalCostLong))
            {
                CancelClash(session, playbookEvent, "[Clash cancelled] Not enough Inspiration for both actions.", true);
                return;
            }

            int totalCost = (int)totalCostLong;
            if (!session.Inspiration.TrySpend(totalCost))
            {
                CancelClash(session, playbookEvent, "[Clash cancelled] Atomic Inspiration spend failed.", true);
                return;
            }

            playbookEvent.PowerA = a.Skill.BaseDamage + randomSource.NextInclusive(1, 3);
            playbookEvent.PowerB = b.Skill.BaseDamage + randomSource.NextInclusive(1, 3);

            PendingAction winner = null;
            PendingAction loser = null;
            if (playbookEvent.PowerA > playbookEvent.PowerB)
            {
                winner = a;
                loser = b;
            }
            else if (playbookEvent.PowerB > playbookEvent.PowerA)
            {
                winner = b;
                loser = a;
            }

            if (winner != null)
            {
                playbookEvent.Winner = winner.Actor;
                playbookEvent.Loser = loser.Actor;
                ApplyDamageAndStagger(
                    session,
                    winner.Actor,
                    winner.Skill,
                    loser.Actor,
                    out int damage,
                    out int stagger,
                    out bool weakness);
                playbookEvent.DamageDealtToLoser = damage;
                playbookEvent.StaggerDealtToLoser = stagger;
                playbookEvent.HitWeakness = weakness;
                playbookEvent.LogMessage =
                    $"[Clash] {winner.Actor.Id.Value} defeated {loser.Actor.Id.Value} " +
                    $"({playbookEvent.PowerA} vs {playbookEvent.PowerB}).";
            }
            else
            {
                playbookEvent.LogMessage =
                    $"[Clash] Draw ({playbookEvent.PowerA} vs {playbookEvent.PowerB}); no damage dealt.";
            }

            AddResult(session, playbookEvent);
        }

        private static void CancelClash(
            CombatSession session,
            Event_Clash playbookEvent,
            string message,
            bool lackOfInspiration)
        {
            playbookEvent.IsCancelled = true;
            playbookEvent.LackOfInspiration = lackOfInspiration;
            playbookEvent.LogMessage = message;
            AddResult(session, playbookEvent);
        }

        private static void ExecuteUnopposed(CombatSession session, PendingAction action)
        {
            if (action.Target == null)
            {
                ExecuteUtility(session, action);
                return;
            }

            Event_Unopposed playbookEvent = new Event_Unopposed
            {
                Actor = action.Actor,
                Target = action.Target,
                Skill = action.Skill
            };

            if (!CanExecute(action))
            {
                CancelUnopposed(session, playbookEvent, "Action cancelled because the actor cannot act.", false);
                return;
            }

            if (action.Target.HP <= 0)
            {
                CancelUnopposed(session, playbookEvent, "Action cancelled because the target is dead.", false);
                return;
            }

            if (!session.Inspiration.TrySpend(action.Skill.InspirationCost))
            {
                CancelUnopposed(session, playbookEvent, "Action cancelled because Inspiration is insufficient.", true);
                return;
            }

            ApplyDamageAndStagger(
                session,
                action.Actor,
                action.Skill,
                action.Target,
                out int damage,
                out int stagger,
                out bool weakness);
            playbookEvent.DamageDealt = damage;
            playbookEvent.StaggerDealt = stagger;
            playbookEvent.HitWeakness = weakness;
            playbookEvent.LogMessage =
                $"[Action] {action.Actor.Id.Value} hit {action.Target.Id.Value} for {damage} damage and {stagger} stagger.";
            AddResult(session, playbookEvent);
        }

        private static void ExecuteUtility(CombatSession session, PendingAction action)
        {
            Event_Utility playbookEvent = new Event_Utility
            {
                Actor = action.Actor,
                Skill = action.Skill
            };

            if (!CanExecute(action) || !session.Inspiration.TrySpend(action.Skill.InspirationCost))
            {
                playbookEvent.IsCancelled = true;
                playbookEvent.LogMessage = $"[Utility] {action.Actor.Id.Value} action was cancelled.";
                AddResult(session, playbookEvent);
                return;
            }

            if (action.Skill.Tag == SkillTag.Inspect && action.Target != null)
                session.Knowledge.RevealWeakness(action.Target.Id);

            playbookEvent.LogMessage = $"[Utility] {action.Actor.Id.Value} used {action.Skill.Name}.";
            AddResult(session, playbookEvent);
        }

        private static void ExecuteArea(CombatSession session, PendingAction action)
        {
            Event_Area playbookEvent = new Event_Area
            {
                Actor = action.Actor,
                Skill = action.Skill
            };

            IReadOnlyList<ICombatant> targetSide = action.Plan.Targeting == TargetingRule.AllEnemies
                ? session.Enemies
                : session.Allies;

            if (!CanExecute(action))
            {
                playbookEvent.IsCancelled = true;
                playbookEvent.LogMessage = "Area action cancelled because the actor cannot act.";
                AddResult(session, playbookEvent);
                return;
            }

            for (int i = 0; i < targetSide.Count; i++)
            {
                ICombatant target = targetSide[i];
                if (target != null && target.HP > 0)
                    playbookEvent.Targets.Add(target);
            }

            if (playbookEvent.Targets.Count == 0)
            {
                playbookEvent.IsCancelled = true;
                playbookEvent.LogMessage = "Area action cancelled because no living targets remain.";
                AddResult(session, playbookEvent);
                return;
            }

            if (!session.Inspiration.TrySpend(action.Skill.InspirationCost))
            {
                playbookEvent.IsCancelled = true;
                playbookEvent.LackOfInspiration = true;
                playbookEvent.LogMessage = "Area action cancelled because Inspiration is insufficient.";
                AddResult(session, playbookEvent);
                return;
            }

            for (int i = 0; i < playbookEvent.Targets.Count; i++)
            {
                ICombatant target = playbookEvent.Targets[i];
                ApplyDamageAndStagger(
                    session,
                    action.Actor,
                    action.Skill,
                    target,
                    out int damage,
                    out int stagger,
                    out bool weakness);
                playbookEvent.DamageDealt.Add(damage);
                playbookEvent.StaggerDealt.Add(stagger);
                playbookEvent.HitWeakness.Add(weakness);
            }

            playbookEvent.LogMessage =
                $"[Area] {action.Actor.Id.Value} used {action.Skill.Name} on {playbookEvent.Targets.Count} targets.";
            AddResult(session, playbookEvent);
        }

        private static void CancelUnopposed(
            CombatSession session,
            Event_Unopposed playbookEvent,
            string message,
            bool lackOfInspiration)
        {
            playbookEvent.IsCancelled = true;
            playbookEvent.LackOfInspiration = lackOfInspiration;
            playbookEvent.LogMessage = message;
            AddResult(session, playbookEvent);
        }

        private static bool CanExecute(PendingAction action)
        {
            return action.Actor != null && action.Actor.HP > 0 && !action.Actor.IsStunned;
        }

        private static void ApplyDamageAndStagger(
            CombatSession session,
            ICombatant attacker,
            ISkill skill,
            ICombatant target,
            out int damage,
            out int stagger,
            out bool hitWeakness)
        {
            damage = skill.BaseDamage;
            hitWeakness = (target.Weakness & skill.Keywords) != 0;
            stagger = skill.BaseStagger + (hitWeakness ? skill.WeaknessStaggerBonus : 0);

            if (damage > 0)
                target.ApplyDamage(damage);

            StaggerSystem.AddStagger(target, stagger);

            if (skill.Tag == SkillTag.Inspect)
                session.Knowledge.RevealWeakness(target.Id);
        }

        private static void AddResult(CombatSession session, PlaybookEvent playbookEvent)
        {
            session.CurrentTurn.AddPlaybookEvent(playbookEvent);
            session.CurrentTurn.AddResolvedEvent(new ResolvedEvent(playbookEvent.LogMessage));
        }

        private static bool PlansMatch(
            IReadOnlyDictionary<CombatantId, ActionPlan> committed,
            IReadOnlyDictionary<CombatantId, ActionPlan> normalized)
        {
            if (committed.Count != normalized.Count)
                return false;

            foreach (KeyValuePair<CombatantId, ActionPlan> pair in committed)
            {
                if (!normalized.TryGetValue(pair.Key, out ActionPlan other) ||
                    !ActionsMatch(pair.Value.Slot1, other.Slot1) ||
                    !ActionsMatch(pair.Value.Slot2, other.Slot2))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ActionsMatch(PlannedAction left, PlannedAction right)
        {
            return left.IsNone == right.IsNone &&
                   left.SkillId.Value == right.SkillId.Value &&
                   left.Tag == right.Tag &&
                   left.Targeting == right.Targeting &&
                   left.TargetCombatantId.Value == right.TargetCombatantId.Value &&
                   left.PlannedSpeed == right.PlannedSpeed &&
                   left.ConsumesTurn == right.ConsumesTurn;
        }

        private static bool IsSingleTarget(TargetingRule targeting)
        {
            return targeting == TargetingRule.SingleEnemy ||
                   targeting == TargetingRule.SingleAlly ||
                   targeting == TargetingRule.AnySingle;
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
    }
}
