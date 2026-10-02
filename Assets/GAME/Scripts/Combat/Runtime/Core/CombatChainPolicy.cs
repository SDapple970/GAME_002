using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatChainPolicy
    {
        public static bool CanContinue(
            CombatSession session,
            ICombatant currentAttackOwner,
            ISkill skill)
        {
            if (session == null || currentAttackOwner == null || skill == null ||
                FinalCombatTerminalPolicy.Evaluate(session) != CombatEndReason.None ||
                !CombatDeclarationPolicy.IsActiveMember(session, currentAttackOwner) ||
                !CombatDeclarationPolicy.OwnsSkill(currentAttackOwner, skill))
            {
                return false;
            }

            CombatantCombatState state = session.GetCombatState(currentAttackOwner);
            return state.CanSpendMp(CombatMpCostResolver.Resolve(skill)) &&
                   HasValidTarget(session, currentAttackOwner, skill);
        }

        public static bool CanContinueWithAnySkill(
            CombatSession session,
            ICombatant currentAttackOwner)
        {
            if (currentAttackOwner?.Skills == null)
                return false;

            for (int i = 0; i < currentAttackOwner.Skills.Count; i++)
            {
                if (CanContinue(session, currentAttackOwner, currentAttackOwner.Skills[i]))
                    return true;
            }

            return false;
        }

        public static bool ShouldContinue(
            CombatSession session,
            ICombatant currentAttackOwner,
            ISkill skill,
            bool wantsToContinue)
        {
            return wantsToContinue && CanContinue(session, currentAttackOwner, skill);
        }

        private static bool HasValidTarget(
            CombatSession session,
            ICombatant actor,
            ISkill skill)
        {
            if (skill.Targeting == TargetingRule.SingleEnemy)
                return CanResolveAgainstSide(session, actor, skill, Opposite(actor.Side));

            if (skill.Targeting == TargetingRule.SingleAlly)
                return CanResolveAgainstSide(session, actor, skill, actor.Side);

            return CombatOutcomeTargetResolver.CanResolve(actor, skill, null, session);
        }

        private static bool CanResolveAgainstSide(
            CombatSession session,
            ICombatant actor,
            ISkill skill,
            Side targetSide)
        {
            System.Collections.Generic.IReadOnlyList<ICombatant> targets = session.GetSide(targetSide);
            for (int i = 0; i < targets.Count; i++)
            {
                if (CombatOutcomeTargetResolver.CanResolve(actor, skill, targets[i], session))
                    return true;
            }

            return false;
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }
    }
}
