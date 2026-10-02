using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Model;

namespace Game.Combat.UI
{
    /// <summary>
    /// Adapts FinalExchange player decision requests into local selection state and versioned
    /// orchestrator commands. It intentionally owns neither input actions nor global UI routing.
    /// </summary>
    public sealed class FinalCombatPlayerCommandController
    {
        private readonly ICombatHandoffEligibilityProvider _handoffEligibilityProvider;

        private CombatFlowOrchestrator _orchestrator;
        private CombatSession _session;
        private CombatExchangeDecisionRequest _request;
        private ICombatant _selectedActor;
        private ISkill _selectedSkill;
        private ICombatant _selectedTarget;
        private ICombatant _selectedHandoffTarget;

        public event Action<PlayerCombatDecisionViewState> ViewStateChanged;

        public PlayerCombatDecisionViewState ViewState { get; private set; } = PlayerCombatDecisionViewState.Empty;

        public FinalCombatPlayerCommandController(
            ICombatHandoffEligibilityProvider handoffEligibilityProvider = null)
        {
            _handoffEligibilityProvider = handoffEligibilityProvider;
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
            _orchestrator.AttackDecisionRequested += HandleAttackDecisionRequested;
            _orchestrator.ResponseDecisionRequested += HandleResponseDecisionRequested;
            _orchestrator.ChainDecisionRequested += HandleChainDecisionRequested;

            ConsumeRequest(_orchestrator.PendingDecisionRequest);
            return true;
        }

        public void Unbind()
        {
            if (_orchestrator != null)
            {
                _orchestrator.AttackDecisionRequested -= HandleAttackDecisionRequested;
                _orchestrator.ResponseDecisionRequested -= HandleResponseDecisionRequested;
                _orchestrator.ChainDecisionRequested -= HandleChainDecisionRequested;
            }

            _orchestrator = null;
            _session = null;
            ClearRequestAndSelection(true);
        }

        public bool SelectActor(ICombatant actor)
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Attack) ||
                !Contains(BuildSelectableActors(), actor))
            {
                return false;
            }

            _selectedActor = actor;
            _selectedSkill = null;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectSkill(ISkill skill)
        {
            if (_request == null || !HasCurrentPlayerRequest(_request.Kind) || skill == null ||
                (_request.Kind != CombatExchangeDecisionKind.Attack &&
                 _request.Kind != CombatExchangeDecisionKind.Response))
            {
                return false;
            }

            if (_request.Kind == CombatExchangeDecisionKind.Attack)
            {
                ICombatant actor = GetRequiredAttackActor();
                if (actor == null || !Contains(BuildSelectableSkills(actor), skill))
                    return false;
            }
            else if (_request.Kind == CombatExchangeDecisionKind.Response)
            {
                if (!Contains(BuildResponseSkills(), skill))
                    return false;
            }
            _selectedSkill = skill;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectTarget(ICombatant target)
        {
            if (_request == null || !HasCurrentPlayerRequest(_request.Kind) ||
                target == null || _selectedSkill == null)
                return false;

            IReadOnlyList<ICombatant> targets = BuildSelectableTargets();
            if (!Contains(targets, target))
                return false;

            _selectedTarget = target;
            PublishViewState();
            return true;
        }

        public bool SelectHandoffTarget(ICombatant target)
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                !Contains(BuildHandoffCandidates(), target))
            {
                return false;
            }

            _selectedHandoffTarget = target;
            _selectedSkill = null;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectHandoffSkill(ISkill skill)
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                _selectedHandoffTarget == null ||
                !Contains(BuildHandoffSkills(_selectedHandoffTarget), skill))
            {
                return false;
            }

            _selectedSkill = skill;
            PublishViewState();
            return true;
        }

        public bool Confirm()
        {
            if (_request == null || _orchestrator == null ||
                !HasCurrentPlayerRequest(_request.Kind))
                return false;

            switch (_request.Kind)
            {
                case CombatExchangeDecisionKind.Attack:
                    return ConfirmAttack();
                case CombatExchangeDecisionKind.Response:
                    return ConfirmResponse();
                default:
                    return false;
            }
        }

        public bool ConfirmNoResponse()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Response))
                return false;

            int version = _request.ExchangeVersion;
            ClearSelectionOnly(true);
            return _orchestrator.SubmitNoResponse(version);
        }

        public bool ConfirmContinue()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                _selectedHandoffTarget != null ||
                !CombatChainPolicy.CanContinueWithAnySkill(_session, _request.ActingActor))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            ClearSelectionOnly(true);
            return _orchestrator.ContinueChain(version);
        }

        public bool ConfirmHandoff()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                _selectedHandoffTarget == null || _selectedSkill == null ||
                !Contains(BuildHandoffSkills(_selectedHandoffTarget), _selectedSkill))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            ICombatant target = _selectedHandoffTarget;
            ISkill skill = _selectedSkill;
            ClearSelectionOnly(true);
            return _orchestrator.Handoff(target, skill, true, version);
        }

        public bool EndChain()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain))
                return false;

            int version = _request.ExchangeVersion;
            ClearSelectionOnly(true);
            return _orchestrator.EndChain(version);
        }

        public bool CancelSelection()
        {
            if (_request == null)
                return false;

            ClearSelectionOnly(true);
            return true;
        }

        private bool ConfirmAttack()
        {
            ICombatant actor = GetRequiredAttackActor();
            if (actor == null || _selectedSkill == null || _selectedTarget == null ||
                !CanSubmitAttack(actor, _selectedSkill, _selectedTarget))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            CombatAttackDeclaration declaration = new CombatAttackDeclaration(actor, _selectedTarget, _selectedSkill);
            ClearSelectionOnly(true);
            return _orchestrator.SubmitAttackDeclaration(declaration, version);
        }

        private bool ConfirmResponse()
        {
            CombatAttackDeclaration attack = _session?.ExchangeState?.CurrentDeclaration;
            ICombatant responder = _request.ActingActor;
            if (attack == null || responder == null || _selectedSkill == null ||
                _selectedTarget == null || !ReferenceEquals(_selectedTarget, attack.Attacker) ||
                !CanSubmitResponse(attack, responder, _selectedSkill))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            CombatResponseDeclaration response = new CombatResponseDeclaration(responder, _selectedSkill);
            ClearSelectionOnly(true);
            return _orchestrator.SubmitResponse(response, version);
        }

        private void HandleAttackDecisionRequested(CombatExchangeDecisionRequest request)
        {
            ConsumeRequest(request);
        }

        private void HandleResponseDecisionRequested(CombatExchangeDecisionRequest request)
        {
            ConsumeRequest(request);
        }

        private void HandleChainDecisionRequested(CombatExchangeDecisionRequest request)
        {
            ConsumeRequest(request);
        }

        private void ConsumeRequest(CombatExchangeDecisionRequest request)
        {
            ClearRequestAndSelection(false);
            if (request == null || request.ActingSide != Side.Allies ||
                (request.Kind != CombatExchangeDecisionKind.Attack &&
                 request.Kind != CombatExchangeDecisionKind.Response &&
                 request.Kind != CombatExchangeDecisionKind.Chain))
            {
                PublishViewState();
                return;
            }

            _request = request;
            if (request.Kind == CombatExchangeDecisionKind.Attack && request.ActingActor != null)
                _selectedActor = request.ActingActor;

            PublishViewState();
        }

        private bool HasCurrentPlayerRequest(CombatExchangeDecisionKind kind)
        {
            if (_request == null || _request.ActingSide != Side.Allies ||
                _orchestrator == null || _session == null ||
                !ReferenceEquals(_orchestrator.PendingDecisionRequest, _request))
            {
                ClearRequestAndSelection(true);
                return false;
            }

            return _request.Kind == kind;
        }

        private ICombatant GetRequiredAttackActor()
        {
            if (_request?.Kind != CombatExchangeDecisionKind.Attack)
                return null;

            return _request.ActingActor ?? _selectedActor;
        }

        private IReadOnlyList<ICombatant> BuildSelectableActors()
        {
            if (!HasRawRequest(CombatExchangeDecisionKind.Attack))
                return Array.Empty<ICombatant>();

            if (_request.ActingActor != null)
                return HasSelectableSkill(_request.ActingActor)
                    ? new[] { _request.ActingActor }
                    : Array.Empty<ICombatant>();

            return Filter(_session.GetSide(Side.Allies), HasSelectableSkill);
        }

        private IReadOnlyList<ISkill> BuildSelectableSkills(ICombatant actor)
        {
            if (actor?.Skills == null)
                return Array.Empty<ISkill>();

            List<ISkill> skills = new List<ISkill>();
            for (int i = 0; i < actor.Skills.Count; i++)
            {
                ISkill skill = actor.Skills[i];
                if (HasSelectableTarget(actor, skill))
                    skills.Add(skill);
            }

            return skills;
        }

        private IReadOnlyList<ICombatant> BuildSelectableTargets()
        {
            if (_request == null || _selectedSkill == null)
                return Array.Empty<ICombatant>();

            if (_request.Kind == CombatExchangeDecisionKind.Attack)
            {
                ICombatant actor = GetRequiredAttackActor();
                return actor == null ? Array.Empty<ICombatant>() : BuildAttackTargets(actor, _selectedSkill);
            }

            if (_request.Kind == CombatExchangeDecisionKind.Response)
            {
                CombatAttackDeclaration attack = _session?.ExchangeState?.CurrentDeclaration;
                return attack != null && CanSubmitResponse(attack, _request.ActingActor, _selectedSkill)
                    ? new[] { attack.Attacker }
                    : Array.Empty<ICombatant>();
            }

            return Array.Empty<ICombatant>();
        }

        private IReadOnlyList<ISkill> BuildResponseSkills()
        {
            CombatAttackDeclaration attack = _session?.ExchangeState?.CurrentDeclaration;
            ICombatant responder = _request?.ActingActor;
            if (!HasRawRequest(CombatExchangeDecisionKind.Response) ||
                attack == null || responder?.Skills == null)
            {
                return Array.Empty<ISkill>();
            }

            List<ISkill> skills = new List<ISkill>();
            for (int i = 0; i < responder.Skills.Count; i++)
            {
                ISkill skill = responder.Skills[i];
                if (CanSubmitResponse(attack, responder, skill))
                    skills.Add(skill);
            }

            return skills;
        }

        private IReadOnlyList<ISkill> BuildChainSkills()
        {
            ICombatant actor = _request?.ActingActor;
            if (!HasRawRequest(CombatExchangeDecisionKind.Chain) || actor?.Skills == null)
                return Array.Empty<ISkill>();

            List<ISkill> skills = new List<ISkill>();
            for (int i = 0; i < actor.Skills.Count; i++)
            {
                ISkill skill = actor.Skills[i];
                if (CombatChainPolicy.CanContinue(_session, actor, skill))
                    skills.Add(skill);
            }

            return skills;
        }

        private IReadOnlyList<ICombatant> BuildHandoffCandidates()
        {
            ICombatant owner = _request?.ActingActor;
            if (!HasRawRequest(CombatExchangeDecisionKind.Chain) || owner == null ||
                _handoffEligibilityProvider == null)
            {
                return Array.Empty<ICombatant>();
            }

            return Filter(_session.GetSide(owner.Side), candidate =>
                !ReferenceEquals(candidate, owner) &&
                _handoffEligibilityProvider.IsEligible(_session, owner, candidate) &&
                BuildHandoffSkills(candidate).Count > 0);
        }

        private IReadOnlyList<ISkill> BuildHandoffSkills(ICombatant candidate)
        {
            ICombatant owner = _request?.ActingActor;
            if (!HasRawRequest(CombatExchangeDecisionKind.Chain) || owner == null || candidate?.Skills == null ||
                _handoffEligibilityProvider == null ||
                !_handoffEligibilityProvider.IsEligible(_session, owner, candidate))
            {
                return Array.Empty<ISkill>();
            }

            List<ISkill> skills = new List<ISkill>();
            int handoffCount = _session.ExchangeState.HandoffCount;
            for (int i = 0; i < candidate.Skills.Count; i++)
            {
                ISkill skill = candidate.Skills[i];
                if (CombatHandoffPolicy.TryAuthorize(
                        _session,
                        owner,
                        candidate,
                        skill,
                        handoffCount,
                        true,
                        out _))
                {
                    skills.Add(skill);
                }
            }

            return skills;
        }

        private IReadOnlyList<ICombatant> BuildAttackTargets(ICombatant actor, ISkill skill)
        {
            if (actor == null || skill == null)
                return Array.Empty<ICombatant>();

            return Filter(_session.GetSide(Opposite(actor.Side)), target => CanSubmitAttack(actor, skill, target));
        }

        private bool HasSelectableSkill(ICombatant actor)
        {
            return BuildSelectableSkills(actor).Count > 0;
        }

        private bool HasSelectableTarget(ICombatant actor, ISkill skill)
        {
            return BuildAttackTargets(actor, skill).Count > 0;
        }

        private bool CanSubmitAttack(ICombatant actor, ISkill skill, ICombatant target)
        {
            if (_session == null || actor == null || skill == null || target == null ||
                !_session.TryGetCombatState(actor, out CombatantCombatState actorState) ||
                !actorState.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
            {
                return false;
            }

            return CombatDeclarationPolicy.CanDeclareAttack(
                _session,
                new CombatAttackDeclaration(actor, target, skill));
        }

        private bool CanSubmitResponse(
            CombatAttackDeclaration attack,
            ICombatant responder,
            ISkill skill)
        {
            if (_session == null || attack == null || responder == null || skill == null ||
                !_session.TryGetCombatState(responder, out CombatantCombatState responderState) ||
                !responderState.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
            {
                return false;
            }

            return CombatDeclarationPolicy.CanDeclareResponse(
                _session,
                attack,
                new CombatResponseDeclaration(responder, skill));
        }

        private void ClearRequestAndSelection(bool publish)
        {
            _request = null;
            _selectedActor = null;
            _selectedSkill = null;
            _selectedTarget = null;
            _selectedHandoffTarget = null;
            if (publish)
                PublishViewState();
        }

        private void ClearSelectionOnly(bool publish)
        {
            _selectedSkill = null;
            _selectedTarget = null;
            _selectedHandoffTarget = null;
            if (_request?.Kind == CombatExchangeDecisionKind.Attack && _request.ActingActor == null)
                _selectedActor = null;
            if (publish)
                PublishViewState();
        }

        private void PublishViewState()
        {
            if (_request == null)
            {
                ViewState = PlayerCombatDecisionViewState.Empty;
            }
            else
            {
                IReadOnlyList<ICombatant> actors = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? BuildSelectableActors()
                    : Array.Empty<ICombatant>();
                ICombatant activeActor = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? GetRequiredAttackActor()
                    : _request.ActingActor;
                IReadOnlyList<ISkill> skills = _request.Kind == CombatExchangeDecisionKind.Chain && _selectedHandoffTarget != null
                    ? BuildHandoffSkills(_selectedHandoffTarget)
                    : _request.Kind == CombatExchangeDecisionKind.Chain
                        ? Array.Empty<ISkill>()
                        : _request.Kind == CombatExchangeDecisionKind.Response
                            ? BuildResponseSkills()
                            : BuildSelectableSkills(activeActor);
                IReadOnlyList<ICombatant> targets = BuildSelectableTargets();
                IReadOnlyList<ICombatant> handoffCandidates = _request.Kind == CombatExchangeDecisionKind.Chain
                    ? BuildHandoffCandidates()
                    : Array.Empty<ICombatant>();
                bool canConfirm = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? activeActor != null && _selectedSkill != null && _selectedTarget != null &&
                      CanSubmitAttack(activeActor, _selectedSkill, _selectedTarget)
                    : _request.Kind == CombatExchangeDecisionKind.Response
                        ? _selectedSkill != null && _selectedTarget != null &&
                          CanSubmitResponse(_session.ExchangeState.CurrentDeclaration, _request.ActingActor, _selectedSkill)
                        : false;

                ViewState = new PlayerCombatDecisionViewState(
                    _request.Kind,
                    _request.ExchangeVersion,
                    _request.ActingActor,
                    actors,
                    skills,
                    targets,
                    handoffCandidates,
                    _selectedActor,
                    _selectedSkill,
                    _selectedTarget,
                    _selectedHandoffTarget,
                    canConfirm,
                    _request.Kind == CombatExchangeDecisionKind.Response,
                    _request.Kind == CombatExchangeDecisionKind.Chain &&
                    _selectedHandoffTarget == null &&
                    CombatChainPolicy.CanContinueWithAnySkill(_session, _request.ActingActor),
                    _request.Kind == CombatExchangeDecisionKind.Chain &&
                    _selectedHandoffTarget != null && _selectedSkill != null &&
                    Contains(BuildHandoffSkills(_selectedHandoffTarget), _selectedSkill),
                    _request.Kind == CombatExchangeDecisionKind.Chain);
            }

            ViewStateChanged?.Invoke(ViewState);
        }

        private bool HasRawRequest(CombatExchangeDecisionKind kind)
        {
            return _request != null && _request.Kind == kind && _request.ActingSide == Side.Allies &&
                   _session != null;
        }

        private static IReadOnlyList<T> Filter<T>(IReadOnlyList<T> values, Func<T, bool> predicate) where T : class
        {
            if (values == null)
                return Array.Empty<T>();

            List<T> result = new List<T>();
            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];
                if (value != null && predicate(value))
                    result.Add(value);
            }

            return result;
        }

        private static bool Contains<T>(IReadOnlyList<T> values, T expected) where T : class
        {
            if (values == null || expected == null)
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], expected))
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
