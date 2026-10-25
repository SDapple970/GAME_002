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
        private long? _selectedCardRuntimeId;
        private int _selectedBpInvestment;
        private CombatItemOption _selectedItem;
        private ICombatant _selectedTarget;
        private ICombatant _selectedHandoffTarget;

        public event Action<PlayerCombatDecisionViewState> ViewStateChanged;

        public PlayerCombatDecisionViewState ViewState { get; private set; } = PlayerCombatDecisionViewState.Empty;
        public int SelectedBpInvestment => _selectedBpInvestment;

        public bool SelectBpInvestment(int amount)
        {
            if (_request == null || _selectedSkill == null ||
                !HasCurrentPlayerRequest(_request.Kind) ||
                _request.Kind != CombatExchangeDecisionKind.Attack &&
                _request.Kind != CombatExchangeDecisionKind.Response &&
                _request.Kind != CombatExchangeDecisionKind.ClashAttack &&
                _request.Kind != CombatExchangeDecisionKind.ClashResponse)
                return false;

            ICombatant actor = _request.Kind == CombatExchangeDecisionKind.Attack
                ? GetRequiredAttackActor() : _request.ActingActor;
            if (!_orchestrator.CanInvestBp(actor, _selectedSkill, amount)) return false;
            _selectedBpInvestment = amount;
            PublishViewState();
            return true;
        }

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
            _orchestrator.ClashSeriesDecisionRequested += HandleClashSeriesDecisionRequested;
            _orchestrator.ClashSeriesValidationEnabled += RefreshAvailability;

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
                _orchestrator.ClashSeriesDecisionRequested -= HandleClashSeriesDecisionRequested;
                _orchestrator.ClashSeriesValidationEnabled -= RefreshAvailability;
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
            _selectedCardRuntimeId = null;
            _selectedBpInvestment = 0;
            _selectedItem = null;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectSkill(ISkill skill)
        {
            if (_request == null || !HasCurrentPlayerRequest(_request.Kind) || skill == null ||
                (_request.Kind != CombatExchangeDecisionKind.Attack &&
                 _request.Kind != CombatExchangeDecisionKind.Response &&
                 _request.Kind != CombatExchangeDecisionKind.ClashAttack &&
                 _request.Kind != CombatExchangeDecisionKind.ClashResponse))
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
            else if (!Contains(_orchestrator.GetAvailableClashSeriesSkills(_request), skill))
                return false;
            ICombatant selectedActor = _request.Kind == CombatExchangeDecisionKind.Attack
                ? GetRequiredAttackActor() : _request.ActingActor;
            long? cardRuntimeId = null;
            if (_session.TryGetHand(selectedActor, out CombatHandRuntime hand))
            {
                CombatCardKind kind = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? CombatCardKind.Normal : CombatCardKind.Clash;
                if (!hand.TryFindInHand(skill, kind, null, out CombatHandCard card))
                    return false;
                cardRuntimeId = card.RuntimeId;
            }
            _selectedSkill = skill;
            _selectedCardRuntimeId = cardRuntimeId;
            _selectedBpInvestment = 0;
            _selectedItem = null;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectCard(long runtimeId)
        {
            if (_request == null || !HasCurrentPlayerRequest(_request.Kind) ||
                _request.Kind == CombatExchangeDecisionKind.Chain)
                return false;

            ICombatant actor = _request.Kind == CombatExchangeDecisionKind.Attack
                ? GetRequiredAttackActor() : _request.ActingActor;
            if (!_session.TryGetHand(actor, out CombatHandRuntime hand))
                return false;

            IReadOnlyList<CombatHandCard> cards = hand.Hand;
            for (int i = 0; i < cards.Count; i++)
            {
                CombatHandCard card = cards[i];
                if (card.RuntimeId != runtimeId ||
                    !_orchestrator.GetSkillAvailability(_request, actor, card.Skill,
                        runtimeId).IsAvailable)
                    continue;

                _selectedSkill = card.Skill;
                _selectedCardRuntimeId = runtimeId;
                _selectedBpInvestment = 0;
                _selectedItem = null;
                _selectedTarget = null;
                PublishViewState();
                return true;
            }

            return false;
        }

        public bool SelectCombatItem(CombatItemOption item)
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Attack) || item == null ||
                !ContainsCombatItem(BuildSelectableItems(GetRequiredAttackActor()), item))
            {
                return false;
            }

            _selectedItem = item;
            _selectedSkill = null;
            _selectedCardRuntimeId = null;
            _selectedBpInvestment = 0;
            _selectedTarget = null;
            PublishViewState();
            return true;
        }

        public bool SelectTarget(ICombatant target)
        {
            if (_request == null || !HasCurrentPlayerRequest(_request.Kind) ||
                target == null || (_selectedSkill == null && _selectedItem == null))
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
            _selectedCardRuntimeId = null;
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
                case CombatExchangeDecisionKind.ClashAttack:
                case CombatExchangeDecisionKind.ClashResponse:
                    return ConfirmClashSeriesSkill();
                default:
                    return false;
            }
        }

        public bool ConfirmNoResponse()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Response) &&
                !HasCurrentPlayerRequest(CombatExchangeDecisionKind.ClashResponse))
                return false;

            CombatExchangeDecisionRequest request = _request;
            if (request.Kind == CombatExchangeDecisionKind.ClashResponse)
            {
                ClearRequestAndSelection(true);
                bool accepted = _orchestrator.SubmitClashSeriesNoResponse(request);
                if (!accepted && ReferenceEquals(_orchestrator.PendingDecisionRequest, request))
                    ConsumeRequest(request);
                return accepted;
            }

            ClearSelectionOnly(true);
            return _orchestrator.SubmitNoResponse(request);
        }

        public bool ConfirmContinue()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                _selectedHandoffTarget != null ||
                !CanContinueWithCurrentHand(_request.ActingActor))
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

        public bool ConfirmAllOut()
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Chain) ||
                !CanExecuteAllOut())
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            ClearSelectionOnly(true);
            return _orchestrator.ExecuteAllOut(version);
        }

        public bool ConfirmOvercome()
        {
            return TryOvercome(GetRequiredAttackActor());
        }

        public bool TryOvercome(ICombatant actor)
        {
            if (!HasCurrentPlayerRequest(CombatExchangeDecisionKind.Attack) || actor == null ||
                !CanOvercome(actor))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            ClearSelectionOnly(true);
            return _orchestrator.TryOvercome(actor, version, out _);
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
            if (_selectedItem != null)
                return ConfirmCombatItem(actor);

            if (actor == null || _selectedSkill == null || _selectedTarget == null ||
                !CanSubmitAttack(actor, _selectedSkill, _selectedTarget, _selectedCardRuntimeId) ||
                !_orchestrator.CanInvestBp(actor, _selectedSkill, _selectedBpInvestment))
            {
                return false;
            }

            int version = _request.ExchangeVersion;
            CombatAttackDeclaration declaration = new CombatAttackDeclaration(
                actor, _selectedTarget, _selectedSkill, _selectedCardRuntimeId, _selectedBpInvestment);
            ClearSelectionOnly(true);
            return _orchestrator.SubmitAttackDeclaration(declaration, version);
        }

        private bool ConfirmCombatItem(ICombatant user)
        {
            if (user == null || _selectedItem == null || _selectedTarget == null ||
                !ContainsCombatItem(BuildSelectableItems(user), _selectedItem) ||
                !Contains(BuildCombatItemTargets(user, _selectedItem), _selectedTarget))
            {
                return false;
            }

            CombatItemUseRequest request = new CombatItemUseRequest(
                _selectedItem.ItemId,
                user,
                _selectedTarget,
                _request.ExchangeVersion);
            ClearSelectionOnly(true);
            return _orchestrator.TryUseCombatItem(request, out _);
        }

        private bool ConfirmResponse()
        {
            CombatAttackDeclaration attack = _session?.ExchangeState?.CurrentDeclaration;
            ICombatant responder = _request.ActingActor;
            if (attack == null || responder == null || _selectedSkill == null ||
                _selectedTarget == null || !ReferenceEquals(_selectedTarget, attack.Attacker) ||
                !CanSubmitResponse(attack, responder, _selectedSkill, _selectedCardRuntimeId) ||
                !_orchestrator.CanInvestBp(responder, _selectedSkill, _selectedBpInvestment))
            {
                return false;
            }

            CombatExchangeDecisionRequest request = _request;
            CombatResponseDeclaration response = new CombatResponseDeclaration(
                responder, _selectedSkill, _selectedCardRuntimeId, _selectedBpInvestment);
            ClearSelectionOnly(true);
            return _orchestrator.SubmitResponse(response, request);
        }

        private bool ConfirmClashSeriesSkill()
        {
            CombatExchangeDecisionRequest request = _request;
            ISkill skill = _selectedSkill;
            long? cardRuntimeId = _selectedCardRuntimeId;
            int bpInvestment = _selectedBpInvestment;
            if (skill == null || !_orchestrator.CanUseClashSeriesSkill(request, skill) ||
                !_orchestrator.CanInvestBp(request.ActingActor, skill, bpInvestment))
                return false;

            ClearRequestAndSelection(true);
            bool accepted = _orchestrator.SubmitClashSeriesSkill(request, skill,
                cardRuntimeId, bpInvestment);
            if (!accepted && ReferenceEquals(_orchestrator.PendingDecisionRequest, request))
                ConsumeRequest(request);
            return accepted;
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

        private void HandleClashSeriesDecisionRequested(CombatExchangeDecisionRequest request)
        {
            ConsumeRequest(request);
        }

        private void ConsumeRequest(CombatExchangeDecisionRequest request)
        {
            ClearRequestAndSelection(false);
            if (request == null || request.ActingSide != Side.Allies ||
                (request.Kind != CombatExchangeDecisionKind.Attack &&
                 request.Kind != CombatExchangeDecisionKind.Response &&
                 request.Kind != CombatExchangeDecisionKind.Chain &&
                 request.Kind != CombatExchangeDecisionKind.ClashAttack &&
                 request.Kind != CombatExchangeDecisionKind.ClashResponse))
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

        public void RefreshAvailability()
        {
            if (_request != null && _orchestrator != null &&
                ReferenceEquals(_orchestrator.PendingDecisionRequest, _request))
                PublishViewState();
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
                return HasDisplayableSkills(_request.ActingActor) ||
                       HasSelectableAction(_request.ActingActor) || CanOvercome(_request.ActingActor)
                    ? new[] { _request.ActingActor }
                    : Array.Empty<ICombatant>();

            return Filter(_session.GetSide(Side.Allies), actor =>
                HasDisplayableSkills(actor) || HasSelectableAction(actor) || CanOvercome(actor));
        }

        private bool HasDisplayableSkills(ICombatant actor)
        {
            return (_orchestrator?.IsClashSeriesValidationEnabled == true ||
                    _session?.HasHandValidation == true) &&
                   actor?.Skills?.Count > 0 && !actor.IsStunned &&
                   _session.TryGetCombatState(actor, out CombatantCombatState state) &&
                   state.IsAlive && !state.IsPanicked;
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
            if (_request == null || (_selectedSkill == null && _selectedItem == null))
                return Array.Empty<ICombatant>();

            if (_request.Kind == CombatExchangeDecisionKind.Attack)
            {
                ICombatant actor = GetRequiredAttackActor();
                if (actor == null)
                    return Array.Empty<ICombatant>();

                return _selectedItem != null
                    ? BuildCombatItemTargets(actor, _selectedItem)
                    : BuildAttackTargets(actor, _selectedSkill);
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

        private IReadOnlyList<CombatItemOption> BuildSelectableItems(ICombatant user)
        {
            if (!HasRawRequest(CombatExchangeDecisionKind.Attack) || user == null)
                return Array.Empty<CombatItemOption>();

            return _orchestrator?.GetUsableCombatItems(user) ?? Array.Empty<CombatItemOption>();
        }

        private IReadOnlyList<ICombatant> BuildCombatItemTargets(
            ICombatant user,
            CombatItemOption item)
        {
            if (user == null || item == null)
                return Array.Empty<ICombatant>();

            return _orchestrator?.GetUsableCombatItemTargets(user, item) ??
                   Array.Empty<ICombatant>();
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
                        out _) &&
                    (!_session.TryGetHand(candidate, out CombatHandRuntime hand) ||
                     hand.TryFindInHand(skill, CombatCardKind.Normal, null, out _)))
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

        private bool HasSelectableAction(ICombatant actor)
        {
            return BuildSelectableSkills(actor).Count > 0 || BuildSelectableItems(actor).Count > 0;
        }

        private bool HasSelectableTarget(ICombatant actor, ISkill skill)
        {
            return BuildAttackTargets(actor, skill).Count > 0;
        }

        private bool CanContinueWithCurrentHand(ICombatant actor)
        {
            if (!CombatChainPolicy.CanContinueWithAnySkill(_session, actor))
                return false;
            if (!_session.TryGetHand(actor, out CombatHandRuntime hand))
                return true;

            IReadOnlyList<CombatHandCard> cards = hand.Hand;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].Kind == CombatCardKind.Normal &&
                    CombatChainPolicy.CanContinue(_session, actor, cards[i].Skill))
                    return true;
            return false;
        }

        private bool CanSubmitAttack(ICombatant actor, ISkill skill, ICombatant target,
            long? cardRuntimeId = null)
        {
            if (_session == null || actor == null || skill == null || target == null ||
                !_session.TryGetCombatState(actor, out CombatantCombatState actorState) ||
                !actorState.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
            {
                return false;
            }

            if (_session.TryGetHand(actor, out CombatHandRuntime hand) &&
                !hand.TryFindInHand(skill, CombatCardKind.Normal, cardRuntimeId, out _))
                return false;

            return CombatDeclarationPolicy.CanDeclareAttack(
                _session,
                new CombatAttackDeclaration(actor, target, skill));
        }

        private bool CanSubmitResponse(
            CombatAttackDeclaration attack,
            ICombatant responder,
            ISkill skill,
            long? cardRuntimeId = null)
        {
            if (_session == null || attack == null || responder == null || skill == null ||
                !_session.TryGetCombatState(responder, out CombatantCombatState responderState) ||
                !responderState.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
            {
                return false;
            }

            if (_session.TryGetHand(responder, out CombatHandRuntime hand) &&
                !hand.TryFindInHand(skill, CombatCardKind.Clash, cardRuntimeId, out _))
                return false;

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
            _selectedCardRuntimeId = null;
            _selectedBpInvestment = 0;
            _selectedItem = null;
            _selectedTarget = null;
            _selectedHandoffTarget = null;
            if (publish)
                PublishViewState();
        }

        private void ClearSelectionOnly(bool publish)
        {
            _selectedSkill = null;
            _selectedCardRuntimeId = null;
            _selectedBpInvestment = 0;
            _selectedItem = null;
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
                        : _request.Kind == CombatExchangeDecisionKind.ClashAttack ||
                          _request.Kind == CombatExchangeDecisionKind.ClashResponse
                            ? _orchestrator.GetAvailableClashSeriesSkills(_request)
                        : _request.Kind == CombatExchangeDecisionKind.Response
                            ? BuildResponseSkills()
                            : BuildSelectableSkills(activeActor);
                IReadOnlyList<CombatSkillAvailability> skillAvailability =
                    BuildSkillAvailability(activeActor);
                IReadOnlyList<CombatItemOption> items = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? BuildSelectableItems(activeActor)
                    : Array.Empty<CombatItemOption>();
                IReadOnlyList<ICombatant> targets = BuildSelectableTargets();
                IReadOnlyList<ICombatant> handoffCandidates = _request.Kind == CombatExchangeDecisionKind.Chain
                    ? BuildHandoffCandidates()
                    : Array.Empty<ICombatant>();
                bool canConfirm = _request.Kind == CombatExchangeDecisionKind.Attack
                    ? activeActor != null && _selectedTarget != null &&
                      ((_selectedSkill != null && CanSubmitAttack(activeActor, _selectedSkill, _selectedTarget,
                          _selectedCardRuntimeId) &&
                        _orchestrator.CanInvestBp(activeActor, _selectedSkill, _selectedBpInvestment)) ||
                       (_selectedItem != null && ContainsCombatItem(items, _selectedItem) &&
                        Contains(BuildCombatItemTargets(activeActor, _selectedItem), _selectedTarget)))
                    : _request.Kind == CombatExchangeDecisionKind.Response
                        ? _selectedSkill != null && _selectedTarget != null &&
                          CanSubmitResponse(_session.ExchangeState.CurrentDeclaration, _request.ActingActor,
                              _selectedSkill, _selectedCardRuntimeId) &&
                          _orchestrator.CanInvestBp(_request.ActingActor, _selectedSkill, _selectedBpInvestment)
                        : _request.Kind == CombatExchangeDecisionKind.ClashAttack ||
                          _request.Kind == CombatExchangeDecisionKind.ClashResponse
                            ? _selectedSkill != null &&
                              _orchestrator.CanUseClashSeriesSkill(_request, _selectedSkill) &&
                              _orchestrator.CanInvestBp(activeActor, _selectedSkill, _selectedBpInvestment)
                        : false;

                CombatantCombatState activeState = null;
                if (activeActor != null)
                    _session.TryGetCombatState(activeActor, out activeState);
                CombatHandRuntime activeHand = null;
                bool hasHand = activeActor != null &&
                    _session.TryGetHand(activeActor, out activeHand);

                ViewState = new PlayerCombatDecisionViewState(
                    _request.Kind,
                    _request.ExchangeVersion,
                    _request.ActingActor,
                    actors,
                    skills,
                    items,
                    targets,
                    handoffCandidates,
                    _selectedActor,
                    _selectedSkill,
                    _selectedItem,
                    _selectedTarget,
                    _selectedHandoffTarget,
                    canConfirm,
                    _request.Kind == CombatExchangeDecisionKind.Response ||
                    _request.Kind == CombatExchangeDecisionKind.ClashResponse,
                    _request.Kind == CombatExchangeDecisionKind.Chain &&
                    _selectedHandoffTarget == null &&
                    CanContinueWithCurrentHand(_request.ActingActor),
                    _request.Kind == CombatExchangeDecisionKind.Chain &&
                    _selectedHandoffTarget != null && _selectedSkill != null &&
                    Contains(BuildHandoffSkills(_selectedHandoffTarget), _selectedSkill),
                    _request.Kind == CombatExchangeDecisionKind.Chain,
                    _request.Kind == CombatExchangeDecisionKind.Chain && CanExecuteAllOut(),
                    _request.Kind == CombatExchangeDecisionKind.Attack && skills.Count > 0,
                    _request.Kind == CombatExchangeDecisionKind.Attack && items.Count > 0,
                    activeState?.CurrentMental ?? 0,
                    activeState?.MaxMental ?? 0,
                    activeState?.IsPanicked == true,
                    CanOvercome(activeActor),
                    _request.DeclarationToken,
                    _request.DeclaredAttack,
                    skillAvailability,
                    _request.ClashSeriesToken,
                    hasHand,
                    hasHand ? activeHand.Hand : null,
                    _selectedCardRuntimeId,
                    hasHand ? BuildCardAvailability(activeActor, activeHand.Hand) : null);
            }

            ViewStateChanged?.Invoke(ViewState);
        }

        private IReadOnlyList<CombatSkillAvailability> BuildSkillAvailability(ICombatant actor)
        {
            if (actor?.Skills == null || _request == null || _orchestrator == null ||
                _request.Kind == CombatExchangeDecisionKind.Chain)
                return Array.Empty<CombatSkillAvailability>();

            List<CombatSkillAvailability> options = new List<CombatSkillAvailability>();
            for (int i = 0; i < actor.Skills.Count; i++)
            {
                ISkill skill = actor.Skills[i];
                if (skill != null)
                    options.Add(_orchestrator.GetSkillAvailability(_request, actor, skill));
            }
            return options;
        }

        private IReadOnlyList<CombatSkillAvailability> BuildCardAvailability(
            ICombatant actor, IReadOnlyList<CombatHandCard> cards)
        {
            List<CombatSkillAvailability> options = new List<CombatSkillAvailability>();
            for (int i = 0; i < cards.Count; i++)
                options.Add(_orchestrator.GetSkillAvailability(_request, actor,
                    cards[i].Skill, cards[i].RuntimeId));
            return options;
        }

        private bool HasRawRequest(CombatExchangeDecisionKind kind)
        {
            return _request != null && _request.Kind == kind && _request.ActingSide == Side.Allies &&
                   _session != null;
        }

        private bool CanExecuteAllOut()
        {
            return _session?.ExchangeState?.IsAllOutCandidate == true &&
                   CombatAllOutExecutionPolicy.TryCreateExecutionRequest(_session, out _);
        }

        private bool CanOvercome(ICombatant actor)
        {
            return _request != null && _session != null && actor != null &&
                   CombatOvercomePolicy.TryAuthorize(
                       _session,
                       _request.Phase,
                       actor,
                       _request.ExchangeVersion,
                       out _);
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

        private static bool ContainsCombatItem(
            IReadOnlyList<CombatItemOption> values,
            CombatItemOption expected)
        {
            if (values == null || expected == null || string.IsNullOrEmpty(expected.ItemId))
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i]?.ItemId, expected.ItemId, StringComparison.Ordinal))
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
