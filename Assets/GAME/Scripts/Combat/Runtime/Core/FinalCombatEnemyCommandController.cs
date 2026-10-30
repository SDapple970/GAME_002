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
        private bool _acceptsRequests;

        public FinalCombatEnemyCommandController(IFinalCombatEnemyDecisionPolicy policy = null)
        {
            _policy = policy ?? new ConditionalFinalCombatEnemyDecisionPolicy();
        }

        public bool Bind(CombatFlowOrchestrator orchestrator, CombatSession session)
        {
            return BindInternal(orchestrator, session, consumePendingRequest: true);
        }

        /// <summary>
        /// Establishes the normal enemy-command subscriptions without consuming the initial
        /// declaration request. CombatEntryPoint uses this only while its per-session opt-ins
        /// are being configured.
        /// </summary>
        internal bool BindForStartup(CombatFlowOrchestrator orchestrator, CombatSession session)
        {
            return BindInternal(orchestrator, session, consumePendingRequest: false);
        }

        /// <summary>Opens an already bound startup controller to its pending request.</summary>
        internal void ActivateStartupRequests()
        {
            if (_orchestrator == null || _session == null || _acceptsRequests)
                return;

            _acceptsRequests = true;
            ConsumeRequest(_orchestrator.PendingDecisionRequest);
        }

        private bool BindInternal(
            CombatFlowOrchestrator orchestrator,
            CombatSession session,
            bool consumePendingRequest)
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
            _orchestrator.ClashSeriesDecisionRequested += HandleDecisionRequest;
            _acceptsRequests = consumePendingRequest;
            if (_acceptsRequests)
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
                _orchestrator.ClashSeriesDecisionRequested -= HandleDecisionRequest;
            }

            _orchestrator = null;
            _session = null;
            _request = null;
            _acceptsRequests = false;
        }

        private void HandleDecisionRequest(CombatExchangeDecisionRequest request)
        {
            if (!_acceptsRequests)
                return;

            ConsumeRequest(request);
        }

        public bool TryReplaceDeclaration(CombatDeclarationToken token, ISkill replacementSkill)
        {
            return _orchestrator != null && _session != null &&
                   token.SessionId == _session.CompletionId &&
                   _orchestrator.TryReplaceEnemyDeclaration(token, replacementSkill);
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
                case CombatExchangeDecisionKind.ClashAttack:
                case CombatExchangeDecisionKind.ClashResponse:
                    SubmitClashSeriesDecision(request);
                    break;
            }
        }

        private void SubmitClashSeriesDecision(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            System.Collections.Generic.IReadOnlyList<ISkill> available =
                _orchestrator.GetAvailableClashSeriesSkills(request);
#if UNITY_EDITOR
            var reasons = new System.Text.StringBuilder();
            if (request.ActingActor?.Skills != null)
            {
                foreach (ISkill skill in request.ActingActor.Skills)
                {
                    CombatSkillAvailability status = _orchestrator.GetSkillAvailability(
                        request, request.ActingActor, skill);
                    if (reasons.Length > 0)
                        reasons.Append(", ");
                    reasons.Append(skill?.Name).Append(':').Append(status.Reason);
                }
            }
            UnityEngine.Debug.Log($"[ClashTrace/Enemy] Decision session={_session.CompletionId} " +
                $"exchange={request.ExchangeVersion} round={_session.ExchangeState.ClashSeries?.ComparisonCount} " +
                $"kind={request.Kind} actor={request.ActingActor?.Id} available={available.Count} " +
                $"skills=[{reasons}]", null);
#endif
            if (available.Count == 0)
            {
                if (request.Kind == CombatExchangeDecisionKind.ClashResponse)
                    _orchestrator.SubmitClashSeriesNoResponse(request);
                return;
            }

            ISkill preferred = null;
            if (request.Kind == CombatExchangeDecisionKind.ClashAttack &&
                _policy.TryCreateAttack(_session, request.ActingActor, out CombatAttackDeclaration attack))
                preferred = attack?.Skill;
            else if (request.Kind == CombatExchangeDecisionKind.ClashResponse &&
                     _policy.TryCreateResponse(_session, request.ActingActor, out CombatResponseDeclaration response))
                preferred = response?.Skill;

            ISkill selected = available[0];
            for (int i = 0; i < available.Count; i++)
            {
                if (ReferenceEquals(available[i], preferred))
                {
                    selected = preferred;
                    break;
                }
            }

            _orchestrator.SubmitClashSeriesSkill(request, selected);
        }

        private void SubmitAttackOrYield(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            if (_policy.TryCreateAttack(_session, request.ActingActor, out CombatAttackDeclaration declaration))
            {
                if (declaration != null && !_orchestrator.GetSkillAvailability(
                        request, declaration.Attacker, declaration.Skill).IsAvailable &&
                    _session.TryGetHand(declaration.Attacker, out _))
                {
                    declaration = FindHandAttack(request, declaration);
                }

                if (CombatOvercomePolicy.TryAuthorize(
                        _session,
                        request.Phase,
                        declaration?.Attacker,
                        request.ExchangeVersion,
                        out _) &&
                    _orchestrator.TryOvercome(declaration.Attacker, request.ExchangeVersion, out _))
                {
                    return;
                }

                if (_orchestrator.SubmitAttackDeclaration(declaration, request.ExchangeVersion))
                    return;
            }

            ICombatant panickedActor = FindEligiblePanickedActor(request);
            if (panickedActor != null &&
                _orchestrator.TryOvercome(panickedActor, request.ExchangeVersion, out _))
            {
                return;
            }

            if (IsCurrent(request))
                _orchestrator.YieldStandoffAttackAuthority(Side.Enemies, request.ExchangeVersion);
        }

        private ICombatant FindEligiblePanickedActor(CombatExchangeDecisionRequest request)
        {
            if (request == null || _session == null)
                return null;

            System.Collections.Generic.IReadOnlyList<ICombatant> enemies = _session.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                ICombatant actor = enemies[i];
                if (CombatOvercomePolicy.TryAuthorize(
                        _session,
                        request.Phase,
                        actor,
                        request.ExchangeVersion,
                        out _))
                {
                    return actor;
                }
            }

            return null;
        }

        private void SubmitResponseOrDecline(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrent(request))
                return;

            if (_policy.TryCreateResponse(_session, request.ActingActor, out CombatResponseDeclaration response))
            {
                if (response != null && _session.TryGetHand(response.Responder, out _) &&
                    !_orchestrator.GetSkillAvailability(request, response.Responder,
                        response.Skill).IsAvailable)
                    response = FindHandResponse(request);
            }
            else
            {
                response = null;
            }

            if (response != null &&
                _orchestrator.SubmitResponse(response, request.ExchangeVersion))
            {
                return;
            }

            if (IsCurrent(request))
                _orchestrator.SubmitNoResponse(request.ExchangeVersion);
        }

        private CombatResponseDeclaration FindHandResponse(CombatExchangeDecisionRequest request)
        {
            ICombatant actor = request.ActingActor;
            CombatAttackDeclaration attack = _session.ExchangeState.CurrentDeclaration;
            if (actor?.Skills == null || attack == null) return null;
            for (int i = 0; i < actor.Skills.Count; i++)
            {
                ISkill skill = actor.Skills[i];
                if (_orchestrator.GetSkillAvailability(request, actor, skill).IsAvailable &&
                    CombatDeclarationPolicy.CanDeclareResponse(_session, attack,
                        new CombatResponseDeclaration(actor, skill)))
                    return new CombatResponseDeclaration(actor, skill);
            }
            return null;
        }

        private CombatAttackDeclaration FindHandAttack(
            CombatExchangeDecisionRequest request, CombatAttackDeclaration preferred)
        {
            if (preferred?.Attacker?.Skills == null) return null;
            for (int i = 0; i < preferred.Attacker.Skills.Count; i++)
            {
                ISkill skill = preferred.Attacker.Skills[i];
                if (_orchestrator.GetSkillAvailability(request, preferred.Attacker, skill).IsAvailable &&
                    CombatDeclarationPolicy.CanDeclareAttack(_session,
                        new CombatAttackDeclaration(preferred.Attacker, preferred.Target, skill)))
                    return new CombatAttackDeclaration(preferred.Attacker, preferred.Target, skill);
            }
            return null;
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
