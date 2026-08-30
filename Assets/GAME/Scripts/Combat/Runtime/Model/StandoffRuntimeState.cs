namespace Game.Combat.Model
{
    public sealed class StandoffRuntimeState
    {
        public float CurrentPressure { get; private set; }
        public float MaxPressure { get; private set; }
        public bool IsPressureReady => MaxPressure > 0f && CurrentPressure >= MaxPressure;

        public StandoffRuntimeState(float maxPressure)
        {
            Reset(maxPressure);
        }

        public void Reset()
        {
            CurrentPressure = 0f;
        }

        public void Reset(float maxPressure)
        {
            MaxPressure = NormalizeNonNegative(maxPressure);
            CurrentPressure = 0f;
        }

        public bool Advance(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || MaxPressure <= 0f || IsPressureReady)
                return false;

            if (float.IsInfinity(amount) || amount >= MaxPressure - CurrentPressure)
            {
                CurrentPressure = MaxPressure;
                return true;
            }

            CurrentPressure += amount;
            return false;
        }

        private static float NormalizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }
    }
}
