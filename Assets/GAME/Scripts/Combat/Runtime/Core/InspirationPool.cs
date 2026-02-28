using System;

namespace Game.Combat.Core
{
    public sealed class InspirationPool
    {
        public int Current { get; private set; }
        public int Max { get; }

        public event Action<int, int> OnChanged;

        public InspirationPool(int max, int startValue = 0)
        {
            Max = Math.Max(1, max);
            Current = Clamp(startValue);
        }

        public void GainPerTurn(int amount) => Gain(amount);

        public void Gain(int amount)
        {
            if (amount <= 0) return;
            Current = Clamp(Current + amount);
            OnChanged?.Invoke(Current, Max);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Current < amount) return false;

            Current -= amount;
            OnChanged?.Invoke(Current, Max);
            return true;
        }

        private int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > Max) return Max;
            return v;
        }
    }
}
