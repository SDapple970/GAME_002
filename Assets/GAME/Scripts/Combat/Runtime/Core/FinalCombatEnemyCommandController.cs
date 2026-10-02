using System;
using Game.Combat.Model;

namespace Game.Combat.Core
{
    /// <summary>
    /// Binds deterministic FinalExchange enemy decisions to the orchestrator command surface.
    /// This controller owns no combat state, presentation, input, or scene references.
    /// </summary>
    public sealed class FinalCombatEnemyCommandController
    {
        private readonly IFinalCombatEnemyDecisionPolicy _policy;

        private CombatFlowOrchestrator _orchestrator;
        private CombatSession _session;
        private CombatExchangeDecisionRequest _request;

        public FinalCombatEnemyCommandController(IFinalCombatEnemyDecisionPolicy policy = null)
        {
            _policy = policy ?? new FirstValidFinalCombatEnemyDecisionPolicy();
        }

        public bool Bind(CombatFlowOrchestrator orchestrator, CombatSession session)
        {
            Unbind();
            if (orchestrator == null || session == null ||
                session.FlowMode != CombatFlowMode.StandoffClashChain)
            {
                return false;
            }

            _orchestrator = orchestrator;
            _session = session;
            _orchestrator.AttackDecisionRequested += HandleDecisionRequest;
            _orchestrator.ResponseDecisionRequested += HandleDecisionRequest;
            _orchestrator.ChainDecisionRequested += HandleDecisionRequest;
            ConsumeRequest(_orchestrator.PendingDecisionRequest);
            return true;
        }

        public void Unbind()
        {
            if (_orchestrator != null)
            {
                _orchestrator.AttackDecisionRequested -= HandleDecisionRequest;
                _orchestrator.ResponseDecisionRequested -= HandleDecisionRequest;
                _orchestrator.ChainDecisionRequested -= HandleDecisionRequest;
            }

            _orchestrator = null;
            _session = null;
            _request = null;
        }

        private void HandleDecisionRequest(CombatExchangeDecisionRequest request)
        {
            ConsumeRequest(request);
        }

        private void ConsumeRequest(CombatExchangeDecisionRequest request)
        {
            _request = null;
            if (request == null || request.ActingSide != Side.Enemies ||
                _orchestrator == null || _session == null)
            {
                return;
            }

            _request = request;
            switch (request.Kind)
            {
                case CombatExchangeDecisionKind.Attack:
                    SubmitAttackOrYield(request);
                    break;
                case CombatExchangeDecisionKind.Response:
                    SubmitResponseOrDecline(request);
                    break;
                case CombatExchangeDecisionKind.Chain:
                    ContinueOrEnd(request);
                    break;
            }
        }

        private void SubmitAttackOrYield(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            if (_policy.TryCreateAttack(_session, request.ActingActor, out CombatAttackDeclaration declaration) &&
                _orchestrator.SubmitAttackDeclaration(declaration, request.ExchangeVersion))
            {
                return;
            }

            if (IsCurrent(request))
                _orchestrator.YieldStandoffAttackAuthority(Side.Enemies, request.ExchangeVersion);
        }

        private void SubmitResponseOrDecline(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            if (_policy.TryCreateResponse(_session, request.ActingActor, out CombatResponseDeclaration response) &&
                _orchestrator.SubmitResponse(response, request.ExchangeVersion))
            {
                return;
            }

            if (IsCurrent(request))
                _orchestrator.SubmitNoResponse(request.ExchangeVersion);
        }

        private void ContinueOrEnd(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            if (_policy.CanContinue(_session, request.ActingActor) &&
                _orchestrator.ContinueChain(request.ExchangeVersion))
            {
                return;
            }

            if (IsCurrent(request))
                _orchestrator.EndChain(request.ExchangeVersion);
        }

        private bool IsCurrent(CombatExchangeDecisionRequest request)
        {
            return request != null && ReferenceEquals(_request, request) &&
                   _orchestrator != null && _session != null &&
                   ReferenceEquals(_orchestrator.PendingDecisionRequest, request);
        }
    }
}
