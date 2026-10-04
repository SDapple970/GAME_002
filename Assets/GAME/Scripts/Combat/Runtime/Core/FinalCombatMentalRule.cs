using System;
using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Computes one FinalExchange outcome's Mental deltas without mutating combat state.
    /// </summary>
    internal sealed class FinalCombatMentalRule
    {
        private readonly CombatRuntimeConfig _config;

        public FinalCombatMentalRule(CombatRuntimeConfig config)
        {
            _config = config;
        }

        public IReadOnlyList<CombatMentalDelta> Calculate(
            CombatClashResult clash,
            CombatSkillExecutionResult execution,
            CombatStunResult stun)
        {
            List<CombatMentalDelta> deltas = new List<CombatMentalDelta>();
            Dictionary<ICombatant, int> indexes =
                new Dictionary<ICombatant, int>(CombatantReferenceComparer.Instance);
            HashSet<ICombatant> damagedTargets =
                new HashSet<ICombatant>(CombatantReferenceComparer.Instance);

            if (TryResolveClashLoser(clash, out ICombatant loser))
                Add(deltas, indexes, loser, -_config.ClashMentalLoss);

            IReadOnlyList<CombatSkillTargetResult> targetResults = execution?.TargetResults;
            if (targetResults != null)
            {
                for (int i = 0; i < targetResults.Count; i++)
                {
                    CombatSkillTargetResult result = targetResults[i];
                    if (result?.Target != null && result.DamageApplied > 0 &&
                        damagedTargets.Add(result.Target))
                    {
                        Add(deltas, indexes, result.Target, -_config.DamageMentalLoss);
                    }
                }
            }

            if (stun?.StunApplied == true)
                Add(deltas, indexes, stun.Target, -_config.StunMentalLoss);

            return deltas.AsReadOnly();
        }

        private static bool TryResolveClashLoser(CombatClashResult clash, out ICombatant loser)
        {
            loser = null;
            if (clash?.Winner == null ||
                (clash.Outcome != CombatClashOutcome.AttackerWin &&
                 clash.Outcome != CombatClashOutcome.ResponderWin))
            {
                return false;
            }

            CombatAttackDeclaration attack = clash.AttackDeclaration;
            if (ReferenceEquals(clash.Winner, attack?.Attacker))
                loser = clash.ResponseDeclaration?.Responder ?? attack.Target;
            else
                loser = attack?.Attacker;

            return loser != null;
        }

        private static void Add(
            List<CombatMentalDelta> deltas,
            Dictionary<ICombatant, int> indexes,
            ICombatant target,
            int requestedDelta)
        {
            if (target == null || requestedDelta == 0)
                return;

            if (indexes.TryGetValue(target, out int index))
            {
                CombatMentalDelta existing = deltas[index];
                deltas[index] = new CombatMentalDelta(target, existing.RequestedDelta + requestedDelta);
                return;
            }

            indexes.Add(target, deltas.Count);
            deltas.Add(new CombatMentalDelta(target, requestedDelta));
        }

        public readonly struct CombatMentalDelta
        {
            internal CombatMentalDelta(ICombatant target, int requestedDelta)
            {
                Target = target;
                RequestedDelta = requestedDelta;
            }

            public ICombatant Target { get; }
            public int RequestedDelta { get; }
        }

        private sealed class CombatantReferenceComparer : IEqualityComparer<ICombatant>
        {
            public static CombatantReferenceComparer Instance { get; } = new CombatantReferenceComparer();

            public bool Equals(ICombatant left, ICombatant right) => ReferenceEquals(left, right);

            public int GetHashCode(ICombatant value) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
