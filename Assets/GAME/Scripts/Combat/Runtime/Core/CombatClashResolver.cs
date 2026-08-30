using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    public static class CombatClashResolver
    {
        public static bool TryResolve(
            CombatClashRequest request,
            ICombatClashRule rule,
            out CombatClashResult result)
        {
            result = null;
            if (!IsValidRequest(request))
                return false;

            if (request.ResponseState == CombatResponseState.NoResponse)
            {
                result = new CombatClashResult(
                    CombatClashOutcome.Unopposed,
                    request.AttackDeclaration,
                    null,
                    request.Attacker);
                return true;
            }

            if (request.ResponseState != CombatResponseState.CounterDeclared || rule == null)
                return false;

            CombatClashOutcome outcome;
            try
            {
                outcome = rule.Resolve(request);
            }
            catch (Exception)
            {
                return false;
            }

            ICombatant winner;
            switch (outcome)
            {
                case CombatClashOutcome.AttackerWin:
                    winner = request.Attacker;
                    break;
                case CombatClashOutcome.ResponderWin:
                    winner = request.ResponseDeclaration.Responder;
                    break;
                case CombatClashOutcome.Tie:
                    winner = null;
                    break;
                default:
                    return false;
            }

            result = new CombatClashResult(
                outcome,
                request.AttackDeclaration,
                request.ResponseDeclaration,
                winner);
            return true;
        }

        private static bool IsValidRequest(CombatClashRequest request)
        {
            if (request?.AttackDeclaration?.Attacker == null ||
                request.AttackDeclaration.Target == null || request.AttackDeclaration.Skill == null)
            {
                return false;
            }

            if (request.ResponseState == CombatResponseState.NoResponse)
                return request.ResponseDeclaration == null;

            CombatResponseDeclaration response = request.ResponseDeclaration;
            return request.ResponseState == CombatResponseState.CounterDeclared &&
                   response?.Responder != null && response.Skill != null &&
                   ReferenceEquals(response.Responder, request.Target);
        }
    }
}
