using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class CombatTimeline
    {
        private readonly List<TimelineItem> _items = new();

        public void Clear() => _items.Clear();

        public void Add(ICombatant actor, PlannedAction action)
        {
            if (action.IsNone)
                return;

            _items.Add(new TimelineItem(actor, action, action.PlannedSpeed));
        }

        public void ResolveAll(CombatSession session, SkillBook skillBook)
        {
            // Compatibility ordering only. Production ordering belongs to CombatTurnResolver.
            _items.Sort((left, right) =>
            {
                int comparison = right.Speed.CompareTo(left.Speed);
                if (comparison != 0)
                    return comparison;

                int leftPriority = left.Actor.Side == session.InitiativeSide ? 0 : 1;
                int rightPriority = right.Actor.Side == session.InitiativeSide ? 0 : 1;
                comparison = leftPriority.CompareTo(rightPriority);
                return comparison != 0
                    ? comparison
                    : left.OrderIndex.CompareTo(right.OrderIndex);
            });

            for (int i = 0; i < _items.Count; i++)
            {
                TimelineItem item = _items[i];
                ISkill skill = skillBook.Get(item.Action.SkillId);
                if (skill == null)
                {
                    session.CurrentTurn.AddResolvedEvent(new ResolvedEvent(
                        $"[{item.Actor.Id}] missing skill id={item.Action.SkillId.Value}."));
                    continue;
                }

                ICombatant target = ResolveTarget(session, item.Actor, item.Action);
                SkillRunner.Resolve(session, item.Actor, skill, target);
            }
        }

        private static ICombatant ResolveTarget(CombatSession session, ICombatant actor, PlannedAction action)
        {
            if (action.Targeting == TargetingRule.Self)
                return actor;

            if (action.Targeting != TargetingRule.SingleEnemy &&
                action.Targeting != TargetingRule.SingleAlly &&
                action.Targeting != TargetingRule.AnySingle)
            {
                return null;
            }

            foreach (ICombatant ally in session.Allies)
            {
                if (ally.Id.Value == action.TargetCombatantId.Value)
                    return ally;
            }

            foreach (ICombatant enemy in session.Enemies)
            {
                if (enemy.Id.Value == action.TargetCombatantId.Value)
                    return enemy;
            }

            return null;
        }

        private readonly struct TimelineItem
        {
            private static int _counter;

            public readonly int OrderIndex;
            public readonly ICombatant Actor;
            public readonly PlannedAction Action;
            public readonly int Speed;

            public TimelineItem(ICombatant actor, PlannedAction action, int speed)
            {
                Actor = actor;
                Action = action;
                Speed = speed;
                OrderIndex = _counter++;
            }
        }
    }
}
