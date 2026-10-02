using System;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Integration
{
    public sealed class UnityCombatRuleRandomSource : ICombatRuleRandomSource
    {
        public int NextInclusive(int minInclusive, int maxInclusive)
        {
            if (minInclusive > maxInclusive)
                throw new ArgumentOutOfRangeException(nameof(minInclusive));

            if (maxInclusive == int.MaxValue)
            {
                long range = (long)maxInclusive - minInclusive + 1L;
                long offset = Math.Min(
                    range - 1L,
                    (long)Math.Floor(UnityEngine.Random.value * range));
                return (int)((long)minInclusive + offset);
            }

            return UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
        }
    }
}
