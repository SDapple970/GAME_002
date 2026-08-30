using System;

namespace Game.Combat.Model
{
    public sealed class CombatantCombatState
    {
        private double _mpRecoveryRemainder;

        public ICombatant Combatant { get; }
        public int CurrentMp { get; private set; }
        public int MaxMp { get; private set; }
        public int CurrentPosture { get; private set; }
        public int MaxPosture { get; private set; }
        public bool IsPostureMax => MaxPosture > 0 && CurrentPosture >= MaxPosture;

        public CombatantCombatState(ICombatant combatant, CombatRuntimeConfig config)
        {
            Combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            MaxMp = config.MaxMp;
            CurrentMp = config.InitialMp;
            MaxPosture = config.MaxPosture;
            CurrentPosture = config.InitialPosture;
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
