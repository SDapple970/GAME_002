using Game.Combat.Data;

namespace Game.Combat.Model
{
    public sealed class CombatStatusRuntime
    {
        internal CombatStatusRuntime(CombatStatusDefinitionSO definition)
        {
            Definition = definition;
            StatusId = definition.StatusId;
            StackCount = 1;
            RemainingDuration = definition.ExplicitDuration;
        }

        public CombatStatusDefinitionSO Definition { get; private set; }
        public string StatusId { get; }
        public int StackCount { get; private set; }
        public int RemainingDuration { get; private set; }
        public bool IsExpired { get; private set; }
        public bool HasFiniteDuration => DurationKind == CombatStatusDurationKind.Explicit &&
                                         RemainingDuration > 0;
        public CombatStatusDurationKind DurationKind => Definition.DurationKind;
        public CombatStatusPolarity Polarity => Definition.Polarity;

        internal bool Apply(CombatStatusDefinitionSO definition)
        {
            int previousStacks = StackCount;
            int previousDuration = RemainingDuration;
            Definition = definition;
            IsExpired = false;

            switch (definition.StackPolicy)
            {
                case CombatStatusStackPolicy.Stack:
                    StackCount = System.Math.Min(definition.MaximumStacks, StackCount + 1);
                    break;
                case CombatStatusStackPolicy.Refresh:
                    RemainingDuration = definition.ExplicitDuration;
                    break;
                default:
                    StackCount = 1;
                    RemainingDuration = definition.ExplicitDuration;
                    break;
            }

            return previousStacks != StackCount || previousDuration != RemainingDuration;
        }

        internal bool TryAdvanceCompletedExchange(out bool expired)
        {
            expired = false;
            if (!HasFiniteDuration || IsExpired)
                return false;

            RemainingDuration--;
            if (RemainingDuration > 0)
                return true;

            RemainingDuration = 0;
            IsExpired = true;
            expired = true;
            return true;
        }
    }

    public sealed class CombatStatusApplicationResult
    {
        internal CombatStatusApplicationResult(
            ICombatant target,
            CombatStatusRuntime status,
            int previousStacks,
            bool wasAdded,
            bool changed)
        {
            Target = target;
            Status = status;
            PreviousStacks = previousStacks;
            CurrentStacks = status.StackCount;
            WasAdded = wasAdded;
            Changed = changed;
        }

        public ICombatant Target { get; }
        public CombatStatusRuntime Status { get; }
        public int PreviousStacks { get; }
        public int CurrentStacks { get; }
        public bool WasAdded { get; }
        public bool Changed { get; }
    }

    public sealed class CombatStatusDurationResult
    {
        internal CombatStatusDurationResult(
            ICombatant target,
            CombatStatusSnapshot snapshot,
            int remainingDurationBefore,
            bool expired)
        {
            Target = target;
            Snapshot = snapshot;
            RemainingDurationBefore = remainingDurationBefore;
            Expired = expired;
        }

        public ICombatant Target { get; }
        public CombatStatusSnapshot Snapshot { get; }
        public int RemainingDurationBefore { get; }
        public int RemainingDuration => Snapshot?.RemainingDuration ?? 0;
        public bool Expired { get; }
    }
}
