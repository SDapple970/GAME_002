using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatHandoffPolicy
    {
        public const int MaximumHandoffsPerChain = 1;

        public sealed class Result
        {
            public ICombatant ReceivingActor { get; }
            public Side NextAttackSide { get; }
            public int HandoffCount { get; }

            internal Result(ICombatant receivingActor, int handoffCount)
            {
                ReceivingActor = receivingActor;
                NextAttackSide = receivingActor.Side;
                HandoffCount = handoffCount;
            }
        }

        public static bool TryAuthorize(
            CombatSession session,
            ICombatant currentAttackOwner,
            ICombatant receivingActor,
            ISkill receivingSkill,
            int completedHandoffs,
            bool isHandoffEligible,
            out Result result)
        {
            result = null;
            if (completedHandoffs < 0 || completedHandoffs >= MaximumHandoffsPerChain ||
                !isHandoffEligible || currentAttackOwner == null || receivingActor == null ||
                ReferenceEquals(currentAttackOwner, receivingActor) ||
                currentAttackOwner.Side != receivingActor.Side ||
                !CombatDeclarationPolicy.IsActiveMember(session, currentAttackOwner) ||
                !CombatChainPolicy.CanContinue(session, receivingActor, receivingSkill))
            {
                return false;
            }

            result = new Result(receivingActor, completedHandoffs + 1);
            return true;
        }
    }
}
