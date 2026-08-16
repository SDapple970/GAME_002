using System;

namespace Game.Combat.Model
{
    public sealed class CombatantCombatState
    {
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
        }

        public void SetMp(int value)
        {
            CurrentMp = Clamp(value, MaxMp);
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
