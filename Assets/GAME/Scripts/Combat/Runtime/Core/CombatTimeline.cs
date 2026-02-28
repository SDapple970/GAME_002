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
            if (action.IsNone) return;

            _items.Add(new TimelineItem(
                actor,
                action,
                action.PlannedSpeed
            ));
        }

        public void ResolveAll(CombatSession session, SkillBook skillBook)
        {
            // 정렬: Speed desc → 선공측 우선 → 안정 정렬
            _items.Sort((a, b) =>
            {
                int c = b.Speed.CompareTo(a.Speed);
                if (c != 0) return c;

                // 선공 측 우선
                int aPri = (a.Actor.Side == session.InitiativeSide) ? 0 : 1;
                int bPri = (b.Actor.Side == session.InitiativeSide) ? 0 : 1;
                c = aPri.CompareTo(bPri);
                if (c != 0) return c;

                return a.OrderIndex.CompareTo(b.OrderIndex);
            });

            // MVP: Clash는 다음 단계에서 붙인다(지금은 순차 해결)
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var skill = skillBook.Get(item.Action.SkillId);

                if (skill == null)
                {
                    session.CurrentTurn.Events.Add(new ResolvedEvent($"[{item.Actor.Id}] missing skill id={item.Action.SkillId.Value}."));
                    continue;
                }

                ICombatant target = ResolveTarget(session, item.Actor, item.Action);
                SkillRunner.Resolve(session, item.Actor, skill, target);
            }
        }

        private static ICombatant ResolveTarget(CombatSession session, ICombatant actor, PlannedAction action)
        {
            if (action.Targeting == TargetingRule.Self) return actor;
            if (action.Targeting == TargetingRule.SingleEnemy || action.Targeting == TargetingRule.SingleAlly || action.Targeting == TargetingRule.AnySingle)
            {
                // 단일 타겟은 Id로 찾기
                foreach (var c in session.Allies) if (c.Id.Value == action.TargetCombatantId.Value) return c;
                foreach (var c in session.Enemies) if (c.Id.Value == action.TargetCombatantId.Value) return c;
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
