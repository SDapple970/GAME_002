using System.Collections.Generic;
using Game.Combat.Data;

namespace Game.Combat.Model
{
    public enum CombatTurnLifecycle
    {
        Planning,
        Submitted,
        ResolutionFailed,
        Resolved,
        Presenting,
        Presented,
        Completed
    }

    public sealed class CombatTurn
    {
        private readonly Dictionary<CombatantId, ActionPlan> _plans = new();
        private readonly List<ResolvedEvent> _events = new();
        private readonly List<PlaybookEvent> _playbook = new();

        public IReadOnlyDictionary<CombatantId, ActionPlan> Plans => _plans;
        public IReadOnlyList<ResolvedEvent> Events => _events;
        public IReadOnlyList<PlaybookEvent> Playbook => _playbook;
        public CombatTurnLifecycle Lifecycle { get; private set; } = CombatTurnLifecycle.Planning;

        public void SetPlan(CombatantId id, ActionPlan plan)
        {
            TrySetPlan(id, plan);
        }

        public bool TrySetPlan(CombatantId id, ActionPlan plan)
        {
            if (Lifecycle != CombatTurnLifecycle.Planning)
                return false;

            _plans[id] = plan;
            return true;
        }

        public bool TryReplacePlans(IReadOnlyDictionary<CombatantId, ActionPlan> plans)
        {
            if (Lifecycle != CombatTurnLifecycle.Planning || plans == null)
                return false;

            _plans.Clear();
            foreach (KeyValuePair<CombatantId, ActionPlan> pair in plans)
                _plans.Add(pair.Key, pair.Value);
            return true;
        }

        public bool TryGetPlan(CombatantId id, out ActionPlan plan)
        {
            return _plans.TryGetValue(id, out plan);
        }

        internal bool ClearResolutionResults()
        {
            if (Lifecycle != CombatTurnLifecycle.Submitted)
                return false;

            _events.Clear();
            _playbook.Clear();
            return true;
        }

        internal bool AddResolvedEvent(ResolvedEvent resolvedEvent)
        {
            if (Lifecycle == CombatTurnLifecycle.ResolutionFailed ||
                Lifecycle == CombatTurnLifecycle.Resolved ||
                Lifecycle == CombatTurnLifecycle.Presenting ||
                Lifecycle == CombatTurnLifecycle.Presented ||
                Lifecycle == CombatTurnLifecycle.Completed)
            {
                return false;
            }

            _events.Add(resolvedEvent);
            return true;
        }

        internal bool AddPlaybookEvent(PlaybookEvent playbookEvent)
        {
            if (Lifecycle != CombatTurnLifecycle.Submitted || playbookEvent == null)
                return false;

            _playbook.Add(playbookEvent);
            return true;
        }

        public void ClearDebugEvents()
        {
            if (Lifecycle == CombatTurnLifecycle.Planning)
                _events.Clear();
        }

        public bool TrySubmit() => TryAdvance(CombatTurnLifecycle.Planning, CombatTurnLifecycle.Submitted);
        public bool TryMarkResolved() => TryAdvance(CombatTurnLifecycle.Submitted, CombatTurnLifecycle.Resolved);
        public bool TryMarkResolutionFailed() => TryAdvance(CombatTurnLifecycle.Submitted, CombatTurnLifecycle.ResolutionFailed);
        public bool TryBeginPresentation() => TryAdvance(CombatTurnLifecycle.Resolved, CombatTurnLifecycle.Presenting);
        public bool TryMarkPresented() => TryAdvance(CombatTurnLifecycle.Presenting, CombatTurnLifecycle.Presented);
        public bool TryComplete() => TryAdvance(CombatTurnLifecycle.Presented, CombatTurnLifecycle.Completed);

        public void CompleteForExit()
        {
            Lifecycle = CombatTurnLifecycle.Completed;
        }

        private bool TryAdvance(CombatTurnLifecycle expected, CombatTurnLifecycle next)
        {
            if (Lifecycle != expected)
                return false;

            Lifecycle = next;
            return true;
        }
    }

    public readonly struct ResolvedEvent
    {
        public readonly string Message;

        public ResolvedEvent(string message)
        {
            Message = message;
        }

        public override string ToString() => Message;
    }
}
