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
            return TryCommit(session, attack, requestedResponse, CombatCardKind.Normal, out result);
        }

        public static bool TryCommit(
            CombatSession session,
            CombatAttackDeclaration attack,
            CombatResponseDeclaration requestedResponse,
            CombatCardKind attackKind,
            out CombatExchangeCommitResult result)
        {
            if (!CombatDeclarationPolicy.CanDeclareAttack(session, attack))
            {
                result = Failure(CombatExchangeCommitFailure.InvalidAttack);
                return false;
            }

            CombatantCombatState attackerState = session.GetCombatState(attack.Attacker);
            CombatBpRuntime bp = session.BpRuntime;
            if (attack.BpInvestment < 0 || !CombatBpEnhancementPolicy.CanInvest(session, attack.Skill, attack.BpInvestment) ||
                (bp == null ? attack.BpInvestment != 0 : !bp.CanInvest(attack.Attacker, attack.BpInvestment)))
            {
                result = Failure(CombatExchangeCommitFailure.InsufficientAttackBp);
                return false;
            }
            CombatHandRuntime attackHand = null;
            CombatHandCard attackCard = null;
            if (attack.CardRuntimeId.HasValue && !session.TryGetHand(attack.Attacker, out _))
            {
                result = Failure(CombatExchangeCommitFailure.InvalidAttack);
                return false;
            }
            if (session.TryGetHand(attack.Attacker, out attackHand) &&
                !attackHand.TryFindInHand(attack.Skill, attackKind,
                    attack.CardRuntimeId, out attackCard))
            {
                result = Failure(CombatExchangeCommitFailure.InvalidAttack);
                return false;
            }
            int attackCost = CombatMpCostResolver.Resolve(attack.Skill);
            if (!attackerState.CanSpendMp(attackCost))
            {
                result = Failure(CombatExchangeCommitFailure.InsufficientAttackMp);
                return false;
            }

            CombatResponseDeclaration committedResponse = requestedResponse;
            CombatantCombatState responderState = null;
            CombatHandRuntime responseHand = null;
            CombatHandCard responseCard = null;
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
                if (requestedResponse.BpInvestment < 0 ||
                    !CombatBpEnhancementPolicy.CanInvest(session, requestedResponse.Skill, requestedResponse.BpInvestment) ||
                    (bp == null ? requestedResponse.BpInvestment != 0 :
                        !bp.CanInvest(requestedResponse.Responder, requestedResponse.BpInvestment)))
                {
                    result = Failure(CombatExchangeCommitFailure.InsufficientResponseBp);
                    return false;
                }
                if (requestedResponse.CardRuntimeId.HasValue &&
                    !session.TryGetHand(requestedResponse.Responder, out _))
                {
                    result = Failure(CombatExchangeCommitFailure.InvalidResponse);
                    return false;
                }
                if (session.TryGetHand(requestedResponse.Responder, out responseHand) &&
                    !responseHand.TryFindInHand(requestedResponse.Skill, CombatCardKind.Clash,
                        requestedResponse.CardRuntimeId, out responseCard))
                {
                    result = Failure(CombatExchangeCommitFailure.InvalidResponse);
                    return false;
                }
                responseCost = CombatMpCostResolver.Resolve(requestedResponse.Skill);
                if (!responderState.CanSpendMp(responseCost))
                {
                    committedResponse = null;
                    responderState = null;
                    responseCost = 0;
                    responseCard = null;
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

            // Card locations cannot change between validation and synchronous MP commit.
            if (attackCard != null) attackHand.Commit(attackCard);
            if (responseCard != null) responseHand.Commit(responseCard);
            bp?.CommitInvestment(attack.Attacker, attack.BpInvestment);
            if (committedResponse != null)
                bp?.CommitInvestment(committedResponse.Responder, committedResponse.BpInvestment);

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
                downgraded,
                attack.BpInvestment,
                committedResponse?.BpInvestment ?? 0);
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
                false,
                0,
                0);
        }
    }
}
