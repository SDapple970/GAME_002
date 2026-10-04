using System;
using System.Collections.Generic;
using Game.Combat.Data;

namespace Game.Combat.Model
{
    public sealed class CombatantCombatState
    {
        private double _mpRecoveryRemainder;
        private readonly int _panicThreshold;
        private readonly Dictionary<string, CombatStatusRuntime> _statuses =
            new Dictionary<string, CombatStatusRuntime>(StringComparer.Ordinal);

        public ICombatant Combatant { get; }
        public int CurrentHp { get; private set; }
        public int MaxHp { get; }
        public bool IsAlive => CurrentHp > 0;
        public int CurrentMp { get; private set; }
        public int MaxMp { get; private set; }
        public int CurrentPosture { get; private set; }
        public int MaxPosture { get; private set; }
        public bool IsPostureMax => MaxPosture > 0 && CurrentPosture >= MaxPosture;
        public int CurrentMental { get; private set; }
        public int MaxMental { get; }
        public CombatMentalState MentalState { get; private set; }
        public bool IsPanicked => MentalState == CombatMentalState.Panicked;
        public IReadOnlyList<CombatStatusRuntime> ActiveStatuses => GetActiveStatuses();

        public CombatantCombatState(ICombatant combatant, CombatRuntimeConfig config)
        {
            Combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            MaxHp = Math.Max(0, combatant.MaxHP);
            CurrentHp = Clamp(combatant.HP, MaxHp);
            MaxMp = config.MaxMp;
            CurrentMp = config.InitialMp;
            MaxPosture = config.MaxPosture;
            CurrentPosture = config.InitialPosture;
            MaxMental = config.MaxMental;
            CurrentMental = Clamp(config.InitialMental, MaxMental);
            _panicThreshold = config.PanicThreshold;
            MentalState = CurrentMental <= _panicThreshold
                ? CombatMentalState.Panicked
                : CombatMentalState.Stable;
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0)
                return;

            CurrentHp = Math.Max(0, CurrentHp - amount);
        }

        public void RestoreHp(int amount)
        {
            if (amount <= 0)
                return;

            CurrentHp = AddClamped(CurrentHp, amount, MaxHp);
        }

        public bool CanRestoreHp(int amount)
        {
            return amount > 0 && IsAlive && CurrentHp < MaxHp;
        }

        public void SetMaxMp(int value)
        {
            MaxMp = Math.Max(0, value);
            CurrentMp = Clamp(CurrentMp, MaxMp);
            _mpRecoveryRemainder = 0d;
        }

        public void SetMp(int value)
        {
            CurrentMp = Clamp(value, MaxMp);
            _mpRecoveryRemainder = 0d;
        }

        public bool CanSpendMp(int amount)
        {
            return amount >= 0 && CurrentMp >= amount;
        }

        public bool TrySpendMp(int amount)
        {
            if (!CanSpendMp(amount))
                return false;

            CurrentMp -= amount;
            return true;
        }

        public void RestoreMp(int amount)
        {
            if (amount <= 0)
                return;

            CurrentMp = AddClamped(CurrentMp, amount, MaxMp);
        }

        public bool RecoverMp(float recoveryPerSecond, float deltaSeconds)
        {
            if (recoveryPerSecond <= 0f || deltaSeconds <= 0f ||
                float.IsNaN(recoveryPerSecond) || float.IsInfinity(recoveryPerSecond) ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
                CurrentMp >= MaxMp)
            {
                if (CurrentMp >= MaxMp)
                    _mpRecoveryRemainder = 0d;
                return false;
            }

            double accumulated = _mpRecoveryRemainder + (double)recoveryPerSecond * deltaSeconds;
            int recovered = accumulated >= int.MaxValue ? int.MaxValue : (int)Math.Floor(accumulated);
            if (recovered <= 0)
            {
                _mpRecoveryRemainder = accumulated;
                return false;
            }

            int previous = CurrentMp;
            CurrentMp = AddClamped(CurrentMp, recovered, MaxMp);
            _mpRecoveryRemainder = CurrentMp >= MaxMp ? 0d : accumulated - recovered;
            return CurrentMp != previous;
        }

        public void SetMaxPosture(int value)
        {
            MaxPosture = Math.Max(0, value);
            CurrentPosture = Clamp(CurrentPosture, MaxPosture);
        }

        public void SetPosture(int value)
        {
            CurrentPosture = Clamp(value, MaxPosture);
        }

        public void AddPosture(int amount)
        {
            if (amount <= 0)
                return;

            CurrentPosture = AddClamped(CurrentPosture, amount, MaxPosture);
        }

        public void ReducePosture(int amount)
        {
            if (amount <= 0)
                return;

            CurrentPosture = Math.Max(0, CurrentPosture - amount);
        }

        public CombatMentalMutationResult ApplyMentalDelta(int requestedDelta)
        {
            long requested = (long)CurrentMental + requestedDelta;
            int targetMental = requested <= 0L
                ? 0
                : requested >= MaxMental ? MaxMental : (int)requested;
            return SetMental(targetMental, requestedDelta);
        }

        internal CombatMentalMutationResult SetMental(int mental)
        {
            return SetMental(mental, mental - CurrentMental);
        }

        private CombatMentalMutationResult SetMental(int mental, int requestedDelta)
        {
            int mentalBefore = CurrentMental;
            bool panicBefore = IsPanicked;
            CurrentMental = Clamp(mental, MaxMental);
            MentalState = CurrentMental <= _panicThreshold
                ? CombatMentalState.Panicked
                : CombatMentalState.Stable;
            return new CombatMentalMutationResult(
                Combatant,
                mentalBefore,
                CurrentMental,
                requestedDelta,
                CurrentMental - mentalBefore,
                panicBefore,
                IsPanicked);
        }

        public CombatStatusApplicationResult ApplyStatus(CombatStatusDefinitionSO definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.StatusId))
                return null;

            if (!_statuses.TryGetValue(definition.StatusId, out CombatStatusRuntime status))
            {
                status = new CombatStatusRuntime(definition);
                _statuses.Add(status.StatusId, status);
                return new CombatStatusApplicationResult(Combatant, status, 0, true, true);
            }

            int previousStacks = status.StackCount;
            bool changed = status.Apply(definition);
            return new CombatStatusApplicationResult(Combatant, status, previousStacks, false, changed);
        }

        public bool CanApplyStatus(CombatStatusDefinitionSO definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.StatusId))
                return false;

            if (!_statuses.TryGetValue(definition.StatusId, out CombatStatusRuntime status))
                return true;

            if (definition.StackPolicy == CombatStatusStackPolicy.Stack)
                return status.StackCount < definition.MaximumStacks;

            return definition.StackPolicy == CombatStatusStackPolicy.Refresh
                ? status.RemainingDuration != definition.ExplicitDuration
                : status.StackCount != 1 || status.RemainingDuration != definition.ExplicitDuration;
        }

        public bool RemoveStatus(string statusId)
        {
            return !string.IsNullOrWhiteSpace(statusId) && _statuses.Remove(statusId.Trim());
        }

        public bool HasStatus(string statusId)
        {
            return !string.IsNullOrWhiteSpace(statusId) && _statuses.ContainsKey(statusId.Trim());
        }

        public bool TryGetStatus(string statusId, out CombatStatusRuntime status)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                status = null;
                return false;
            }

            return _statuses.TryGetValue(statusId.Trim(), out status);
        }

        private IReadOnlyList<CombatStatusRuntime> GetActiveStatuses()
        {
            List<CombatStatusRuntime> values = new List<CombatStatusRuntime>(_statuses.Values);
            values.Sort((left, right) => string.CompareOrdinal(left.StatusId, right.StatusId));
            return values.AsReadOnly();
        }

        private static int AddClamped(int current, int amount, int max)
        {
            long result = (long)current + amount;
            return result >= max ? max : (int)result;
        }

        private static int Clamp(int value, int max)
        {
            return Math.Min(Math.Max(0, value), max);
        }
    }
}
