using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatAttackAuthorityPolicy
    {
        public sealed class Result
        {
            public ICombatant Winner { get; }
            public Side WinnerSide { get; }
            public Side NextAttackSide { get; }

            internal Result(ICombatant winner)
            {
                Winner = winner;
                WinnerSide = winner.Side;
                NextAttackSide = winner.Side;
            }
        }

        public static bool TryResolve(CombatClashResult clash, out Result result)
        {
            result = null;
            if (clash?.AttackDeclaration?.Attacker == null)
                return false;

            ICombatant winner;
            switch (clash.Outcome)
            {
                case CombatClashOutcome.AttackerWin:
                case CombatClashOutcome.Unopposed:
                    winner = clash.AttackDeclaration.Attacker;
                    break;
                case CombatClashOutcome.ResponderWin:
                    winner = clash.ResponseDeclaration?.Responder;
                    break;
                default:
                    return false;
            }

            if (winner == null || !ReferenceEquals(winner, clash.Winner))
                return false;

            result = new Result(winner);
            return true;
        }
    }
}
