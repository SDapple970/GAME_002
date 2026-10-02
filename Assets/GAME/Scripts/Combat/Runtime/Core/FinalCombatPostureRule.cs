using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public sealed class FinalCombatPostureRule : ICombatPostureRule
    {
        public const int BasePostureDamage = 1;

        public int PostureModifier { get; }

        public FinalCombatPostureRule(int postureModifier = 0)
        {
            PostureModifier = postureModifier;
        }

        public int ResolvePostureDelta(CombatPostureRequest request)
        {
            if (request?.Loser == null)
                throw new ArgumentException("A clash loser is required for posture resolution.");

            return CalculatePostureDamage();
        }

        public bool TryApplyLoss(
            CombatantCombatState loserState,
            ICombatant loser,
            out CombatPostureResult postureResult,
            out CombatStunResult stunResult)
        {
            postureResult = null;
            stunResult = null;
            if (loserState == null || loser == null ||
                !ReferenceEquals(loserState.Combatant, loser) || !loserState.IsAlive)
            {
                return false;
            }

            int postureBefore = loserState.CurrentPosture;
            loserState.AddPosture(CalculatePostureDamage());
            bool reachedMaximum = loserState.IsPostureMax;
            postureResult = new CombatPostureResult(
                loser,
                postureBefore,
                loserState.CurrentPosture,
                reachedMaximum);

            bool wasStunned = loser.IsStunned;
            if (reachedMaximum && !wasStunned)
                loser.SetStunned(true);

            stunResult = new CombatStunResult(loser, wasStunned, loser.IsStunned);
            return !reachedMaximum || loser.IsStunned;
        }

        private int CalculatePostureDamage()
        {
            long damage = (long)BasePostureDamage + PostureModifier;
            if (damage <= 0L)
                return 0;

            return damage >= int.MaxValue ? int.MaxValue : (int)damage;
        }
    }
}
