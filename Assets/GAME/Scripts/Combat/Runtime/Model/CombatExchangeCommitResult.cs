namespace Game.Combat.Model
{
    public enum CombatExchangeCommitFailure
    {
        None,
        InvalidAttack,
        InsufficientAttackMp,
        InvalidResponse
    }

    public sealed class CombatExchangeCommitResult
    {
        public bool Succeeded { get; }
        public CombatExchangeCommitFailure Failure { get; }
        public CombatResponseState ResponseState { get; }
        public CombatResponseDeclaration CommittedResponse { get; }
        public int AttackMpSpent { get; }
        public int ResponseMpSpent { get; }
        public bool ResponseDowngradedForInsufficientMp { get; }

        internal CombatExchangeCommitResult(
            bool succeeded,
            CombatExchangeCommitFailure failure,
            CombatResponseState responseState,
            CombatResponseDeclaration committedResponse,
            int attackMpSpent,
            int responseMpSpent,
            bool responseDowngradedForInsufficientMp)
        {
            Succeeded = succeeded;
            Failure = failure;
            ResponseState = responseState;
            CommittedResponse = committedResponse;
            AttackMpSpent = attackMpSpent;
            ResponseMpSpent = responseMpSpent;
            ResponseDowngradedForInsufficientMp = responseDowngradedForInsufficientMp;
        }
    }
}
