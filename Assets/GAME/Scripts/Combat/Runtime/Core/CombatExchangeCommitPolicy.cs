using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatExchangeCommitPolicy
    {
        public static bool TryCommit(
            CombatSession session,
            CombatAttackDeclaration attack,
            CombatResponseDeclaration requestedResponse,
            out CombatExchangeCommitResult result)
        {
            if (!CombatDeclarationPolicy.CanDeclareAttack(session, attack))
            {
                result = Failure(CombatExchangeCommitFailure.InvalidAttack);
                return false;
            }

            CombatantCombatState attackerState = session.GetCombatState(attack.Attacker);
            int attackCost = CombatMpCostResolver.Resolve(attack.Skill);
            if (!attackerState.CanSpendMp(attackCost))
            {
                result = Failure(CombatExchangeCommitFailure.InsufficientAttackMp);
                return false;
            }

            CombatResponseDeclaration committedResponse = requestedResponse;
            CombatantCombatState responderState = null;
            int responseCost = 0;
            bool downgraded = false;

            if (requestedResponse != null)
            {
                if (!CombatDeclarationPolicy.CanDeclareResponse(session, attack, requestedResponse))
                {
                    result = Failure(CombatExchangeCommitFailure.InvalidResponse);
                    return false;
                }

                responderState = session.GetCombatState(requestedResponse.Responder);
                responseCost = CombatMpCostResolver.Resolve(requestedResponse.Skill);
                if (!responderState.CanSpendMp(responseCost))
                {
                    committedResponse = null;
                    responderState = null;
                    responseCost = 0;
                    downgraded = true;
                }
            }

            if (!attackerState.TrySpendMp(attackCost))
            {
                result = Failure(CombatExchangeCommitFailure.InsufficientAttackMp);
                return false;
            }

            if (committedResponse != null && !responderState.TrySpendMp(responseCost))
            {
                throw new System.InvalidOperationException(
                    "Response MP changed after commit validation in a synchronous pure policy.");
            }

            CombatResponseState responseState = committedResponse == null
                ? CombatResponseState.NoResponse
                : CombatResponseState.CounterDeclared;
            result = new CombatExchangeCommitResult(
                true,
                CombatExchangeCommitFailure.None,
                responseState,
                committedResponse,
                attackCost,
                responseCost,
                downgraded);
            return true;
        }

        private static CombatExchangeCommitResult Failure(CombatExchangeCommitFailure failure)
        {
            return new CombatExchangeCommitResult(
                false,
                failure,
                CombatResponseState.Pending,
                null,
                0,
                0,
                false);
        }
    }
}
