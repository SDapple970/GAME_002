using System;

namespace Game.Combat.Model
{
    /// <summary>Explicit validation values; no Production balance defaults.</summary>
    public readonly struct CombatBpValidationConfig
    {
        public int MaximumBp { get; }
        public int RoundCyclesPerGain { get; }
        public int MaximumInvestment { get; }

        public CombatBpValidationConfig(int maximumBp, int roundCyclesPerGain, int maximumInvestment)
        {
            if (maximumBp <= 0 || roundCyclesPerGain <= 0 || maximumInvestment <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBp));
            MaximumBp = maximumBp;
            RoundCyclesPerGain = roundCyclesPerGain;
            MaximumInvestment = maximumInvestment;
        }
    }
}
