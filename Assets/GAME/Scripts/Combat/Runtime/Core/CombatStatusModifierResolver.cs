using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Reads combat-local status runtime state for FinalExchange calculations.
    /// It owns no state and keeps modifier aggregation out of presentation.
    /// </summary>
    internal static class CombatStatusModifierResolver
    {
        public static int ResolveDamageModifier(CombatSession session, ICombatant actor)
        {
            return ResolveModifier(session, actor, status => status.Definition.DamageModifier);
        }

        public static int ResolveClashModifier(CombatSession session, ICombatant actor)
        {
            return ResolveModifier(session, actor, status => status.Definition.ClashModifier);
        }

        public static int ResolvePostureModifier(CombatSession session, ICombatant actor)
        {
            return ResolveModifier(session, actor, status => status.Definition.PostureModifier);
        }

        public static int ApplyDamageModifier(CombatSession session, ICombatant actor, int baseDamage)
        {
            return AddAndClampNonNegative(baseDamage, ResolveDamageModifier(session, actor));
        }

        private static int ResolveModifier(
            CombatSession session,
            ICombatant actor,
            Func<CombatStatusRuntime, int> selector)
        {
            if (session == null || actor == null || selector == null ||
                !session.TryGetCombatState(actor, out CombatantCombatState state))
            {
                return 0;
            }

            long total = 0;
            IReadOnlyList<CombatStatusRuntime> statuses = state.ActiveStatuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                CombatStatusRuntime status = statuses[i];
                if (status == null || status.IsExpired || status.Definition == null)
                    continue;

                total += (long)selector(status) * Math.Max(1, status.StackCount);
                if (total >= int.MaxValue)
                    return int.MaxValue;
                if (total <= int.MinValue)
                    return int.MinValue;
            }

            return (int)total;
        }

        private static int AddAndClampNonNegative(int value, int modifier)
        {
            long adjusted = (long)Math.Max(0, value) + modifier;
            if (adjusted <= 0L)
                return 0;

            return adjusted >= int.MaxValue ? int.MaxValue : (int)adjusted;
        }
    }
}
