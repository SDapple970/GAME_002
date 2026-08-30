using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatMpCostResolver
    {
        public static int Resolve(ISkill skill)
        {
            if (!(skill is ICombatMpCostProvider provider))
                return 0;

            return Math.Max(0, provider.MpCost);
        }
    }
}
