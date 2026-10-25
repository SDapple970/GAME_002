using System;

namespace Game.Combat.Model
{
    public readonly struct CombatClashSeriesConfig
    {
        public int AttackerDurability { get; }
        public int ResponderDurability { get; }
        public int MaxComparisons { get; }

        public CombatClashSeriesConfig(
            int attackerDurability,
            int responderDurability,
            int maxComparisons)
        {
            if (attackerDurability <= 0)
                throw new ArgumentOutOfRangeException(nameof(attackerDurability));
            if (responderDurability <= 0)
                throw new ArgumentOutOfRangeException(nameof(responderDurability));
            if (maxComparisons <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxComparisons));

            AttackerDurability = attackerDurability;
            ResponderDurability = responderDurability;
            MaxComparisons = maxComparisons;
        }
    }
}
