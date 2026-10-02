using System;
using System.Collections.Generic;
using Game.Combat.Integration;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatFlowOrchestrator : MonoBehaviour
    {
        private const int MaxAutomaticStepsPerPump = 32;

        [SerializeField] private CombatEntryPoint entryPoint;

        private readonly DeterministicCycleEnemyCombatPolicy _enemyPolicy =
            new DeterministicCycleEnemyCombatPolicy();
        private readonly FinalCombatPostureRule _postureRule = new FinalCombatPostureRule();
        private readonly FinalCombatTerminalPolicy _terminalPolicy = new FinalCombatTerminalPolicy();

        private CombatSession _session;
        private CombatStateMachine _boundStateMachine;
        private ICombatRuleRandomSource _randomSource;
        private ICombatClashSideInputProvider _clashInputProvider;
        private CombatExchangeDecisionRequest _pendingDecisionRequest;
        private CombatApproachPresentationRequest _pendingApproachRequest;
        private CombatOutcomePresentationRequest _pendingOutcomePresentationRequest;
        private int _completedOutcomePresentationVersion = -1;
        private int _bindingId;
        private bool _isPumping;
        private bool _isTicking;
        private bool _pumpRequested;
        private bool _terminalCompletionRaised;

        public event Action<CombatExchangeDecisionRequest> AttackDecisionRequested;
        public event Action<CombatExchangeDecisionRequest> ResponseDecisionRequested;
        public event Action<CombatExchangeDecisionRequest> ChainDecisionRequested;
        public event Action<CombatApproachPresentationRequest> ApproachPresentationRequested;
        public event Action<CombatOutcomePresentationRequest> OutcomePresentationRequested;
        public event Action<CombatSession, CombatEndReason, int> TerminalCompleted;

        public CombatExchangeDecisionRequest PendingDecisionRequest => _pendingDecisionRequest;
        public CombatApproachPresentationRequest PendingApproachRequest => _pendingApproachRequest;
        public CombatOutcomePresentationRequest PendingOutcomePresentationRequest =>
            _pendingOutcomePresentationRequest;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = FindFirstObjectByType<CombatEntryPoint>();
        }

        public void BindSession(CombatSession session)
        {
            ResetFinalExchangeBinding();
            _session = session;

            if (_session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                entryPoint == null || !ReferenceEquals(_session, entryPoint.ActiveSession) ||
                entryPoint.ActiveStateMachine == null)
            {
                return;
            }

            BindFinalExchangeCore(
                entryPoint.ActiveStateMachine,
                new UnityCombatRuleRandomSource(),
                new AuthoredCombatClashSideInputProvider());
        }

        public bool BindFinalExchange(
            CombatSession session,
            CombatStateMachine stateMachine,
            ICombatRuleRandomSource randomSource,
            ICombatClashSideInputProvider clashInputProvider = null)
        {
            ResetFinalExchangeBinding();
            _session = session;
            if (_session == null || stateMachine == null || randomSource == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                !stateMachine.OwnsSession(_session))
            {
                return false;
            }

            BindFinalExchangeCore(
                stateMachine,
                randomSource,
                clashInputProvider ?? new AuthoredCombatClashSideInputProvider());
            return true;
        }

        public bool IsBoundTo(CombatSession session, CombatStateMachine stateMachine)
        {
            return ReferenceEquals(_session, session) &&
                   ReferenceEquals(_boundStateMachine, stateMachine) &&
                   IsFinalExchangeBound();
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsFinalExchangeBound())
                return;

            _isTicking = true;
            try
            {
                if (_boundStateMachine.Phase == Phase.Standoff)
                    _boundStateMachine.Tick(deltaSeconds);
            }
            finally
            {
                _isTicking = false;
            }

            AdvanceUntilBlocked();
        }

        public void AdvanceUntilBlocked()
        {
            if (!IsFinalExchangeBound())
                return;

            if (_isPumping || _isTicking)
            {
                _pumpRequested = true;
                return;
            }

            _isPumping = true;
            try
            {
                do
                {
                    _pumpRequested = false;
                    int automaticSteps = 0;
                    while (IsFinalExchangeBound())
                    {
                        Phase phaseBefore = _boundStateMachine.Phase;
                        int versionBefore = _session.ExchangeState.Version;
                        if (!AdvanceOneAutomaticStep())
                            break;

                        automaticSteps++;
                        if (automaticSteps >= MaxAutomaticStepsPerPump)
                        {
                            Debug.LogError("[CombatFlowOrchestrator] FinalExchange pump exceeded its automatic step limit.", this);
                            break;
                        }

                        if (phaseBefore == _boundStateMachine.Phase &&
                            versionBefore == _session.ExchangeState.Version)
                        {
                            Debug.LogError("[CombatFlowOrchestrator] FinalExchange pump reported progress without a state change.", this);
                            break;
                        }
                    }
                }
                while (_pumpRequested && IsFinalExchangeBound());
            }
            finally
            {
                _isPumping = false;
            }
        }

        public bool SubmitAttackDeclaration(
            CombatAttackDeclaration declaration,
            int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryDeclareAttack(declaration, expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool SubmitResponse(
            CombatResponseDeclaration response,
            int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryDeclareResponse(response, expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool SubmitNoResponse(int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.ConfirmNoResponse(expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool ContinueChain(ISkill skill, int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryContinueChain(skill, expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool ContinueChain(int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryContinueChain(expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool Handoff(
            ICombatant receivingActor,
            ISkill receivingSkill,
            bool isHandoffEligible,
            int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryHandoffChain(
                    receivingActor,
                    receivingSkill,
                    isHandoffEligible,
                    expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool EndChain(int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryEndChain(expectedExchangeVersion))
            {
                return false;
            }

            _session.StandoffState.Reset();
            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool YieldStandoffAttackAuthority(Side yieldingSide, int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() || _session.ExchangeState.CurrentAttackSide != yieldingSide ||
                !_boundStateMachine.TryGrantStandoffAttackAuthority(
                    Opposite(yieldingSide), expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool TerminateExplicit(
            CombatEndReason reason,
            int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryTerminateExplicit(reason, expectedExchangeVersion))
            {
                return false;
            }

            RequestPump();
            return true;
        }

        public bool SubmitPlayerDraftAndAdvance(
            CombatPlanDraft draft,
            ICombatant playerActor,
            out string errorMessage)
        {
            errorMessage = null;

            if (_session == null || entryPoint == null)
                return Fail("Combat flow is not bound to an entry point and session.", out errorMessage);

            if (!ReferenceEquals(_session, entryPoint.ActiveSession))
                return Fail("The bound combat session is stale.", out errorMessage);

            if (entryPoint.ActiveStateMachine == null || entryPoint.ActiveStateMachine.Phase != Phase.Planning)
                return Fail("Combat is not accepting plans outside Planning.", out errorMessage);

            CombatTurn turn = _session.CurrentTurn;
            if (turn == null || turn.Lifecycle != CombatTurnLifecycle.Planning)
                return Fail("The active turn has already been submitted.", out errorMessage);

            if (!CombatPlanValidator.TryNormalizePlayerDraft(
                    _session,
                    draft,
                    playerActor,
                    out ActionPlan playerPlan,
                    out errorMessage))
            {
                return false;
            }

            foreach (KeyValuePair<CombatantId, ActionPlan> pair in turn.Plans)
            {
                if (CombatPlanValidator.FindCombatant(_session, pair.Key) == null)
                    return Fail($"Existing plan actor {pair.Key.Value} is not in the active session.", out errorMessage);
            }

            Dictionary<CombatantId, ActionPlan> plans = new Dictionary<CombatantId, ActionPlan>();
            plans.Add(playerActor.Id, playerPlan);

            if (!BuildAdditionalAllyPlans(turn, plans, out errorMessage) ||
                !BuildEnemyPlans(turn, plans, out errorMessage))
            {
                return false;
            }

            if (!turn.TryReplacePlans(plans))
                return Fail("The active turn stopped accepting plans before commitment.", out errorMessage);

            if (!entryPoint.SubmitCurrentTurn())
                return Fail("CombatEntryPoint rejected the committed turn.", out errorMessage);

            return true;
        }

        private void OnDisable()
        {
            ResetFinalExchangeBinding();
            _session = null;
        }

        private void BindFinalExchangeCore(
            CombatStateMachine stateMachine,
            ICombatRuleRandomSource randomSource,
            ICombatClashSideInputProvider clashInputProvider)
        {
            _boundStateMachine = stateMachine;
            _randomSource = randomSource;
            _clashInputProvider = clashInputProvider;
            _boundStateMachine.OnEnemyActionRequired += HandleEnemyActionRequired;
            AdvanceUntilBlocked();
        }

        private bool AdvanceOneAutomaticStep()
        {
            CombatExchangeState exchange = _session.ExchangeState;
            switch (_boundStateMachine.Phase)
            {
                case Phase.EnterCombat:
                    Phase previous = _boundStateMachine.Phase;
                    int previousVersion = exchange.Version;
                    _boundStateMachine.Tick();
                    return previous != _boundStateMachine.Phase || previousVersion != exchange.Version;

                case Phase.Standoff:
                    PublishDecisionRequest(
                        CombatExchangeDecisionKind.Attack,
                        exchange.CurrentAttackSide,
                        exchange.CurrentAttackActor,
                        AttackDecisionRequested);
                    return false;

                case Phase.AttackDeclaration:
                    if (exchange.CurrentDeclaration == null)
                    {
                        PublishDecisionRequest(
                            CombatExchangeDecisionKind.Attack,
                            exchange.CurrentAttackSide,
                            exchange.CurrentAttackActor,
                            AttackDecisionRequested);
                        return false;
                    }

                    if (exchange.ResponseState == CombatResponseState.Pending)
                    {
                        ICombatant responder = exchange.CurrentDeclaration.Target;
                        PublishDecisionRequest(
                            CombatExchangeDecisionKind.Response,
                            responder.Side,
                            responder,
                            ResponseDecisionRequested);
                        return false;
                    }

                    if (!exchange.IsCommitted)
                        return _boundStateMachine.TryCommitExchange(exchange.Version);

                    return _boundStateMachine.TryBeginApproach(exchange.Version);

                case Phase.Approach:
                    PublishApproachRequest();
                    return false;

                case Phase.Clash:
                    return TryResolveClash(exchange);

                case Phase.ApplyOutcome:
                    return TryAdvanceApplyOutcome(exchange);

                case Phase.ChainDecision:
                    PublishDecisionRequest(
                        CombatExchangeDecisionKind.Chain,
                        exchange.CurrentAttackSide,
                        exchange.CurrentAttackActor,
                        ChainDecisionRequested);
                    return false;

                case Phase.ExitCombat:
                    RaiseTerminalCompletionOnce(exchange.Version);
                    return false;

                default:
                    return false;
            }
        }

        private bool TryResolveClash(CombatExchangeState exchange)
        {
            CombatAttackDeclaration attack = exchange.CurrentDeclaration;
            CombatResponseDeclaration response = exchange.CurrentResponse;
            if (attack?.Attacker == null || attack.Skill == null ||
                response?.Responder == null || response.Skill == null ||
                !_clashInputProvider.TryCreate(
                    attack.Attacker,
                    attack.Skill,
                    true,
                    out CombatClashSideInput attackerInput) ||
                !_clashInputProvider.TryCreate(
                    response.Responder,
                    response.Skill,
                    false,
                    out CombatClashSideInput responderInput))
            {
                return false;
            }

            FinalCombatClashRule rule = new FinalCombatClashRule(
                attackerInput,
                responderInput,
                _randomSource);
            return _boundStateMachine.TryResolveClash(rule, exchange.Version);
        }

        private bool TryAdvanceApplyOutcome(CombatExchangeState exchange)
        {
            int version = exchange.Version;
            if (!exchange.IsOutcomePrepared)
                return _boundStateMachine.TryPrepareOutcome(version);

            if (!exchange.IsExecutionPrepared)
                return _boundStateMachine.TryPrepareSkillExecution(version);

            if (!exchange.IsExecutionCompleted)
                return _boundStateMachine.TryExecutePreparedSkill(version);

            if (exchange.PostureResolutionState == CombatPostureResolutionState.Pending)
                return _boundStateMachine.TryResolvePosture(_postureRule, version);

            if (exchange.StunResolutionState == CombatStunResolutionState.Pending)
                return _boundStateMachine.TryResolveStun(version);

            if (!exchange.IsAftermathPrepared)
                return _boundStateMachine.TryPrepareAftermath(version);

            if (!exchange.IsAftermathDecisionPrepared)
                return _boundStateMachine.TryPrepareAftermathDecision(version);

            if (_completedOutcomePresentationVersion != version)
            {
                PublishOutcomePresentationRequest(exchange, version);
                return false;
            }

            return _boundStateMachine.TryFinalizeApplyOutcome(_terminalPolicy, version);
        }

        private void PublishOutcomePresentationRequest(
            CombatExchangeState exchange,
            int version)
        {
            if (_pendingOutcomePresentationRequest != null &&
                _pendingOutcomePresentationRequest.ExchangeVersion == version)
            {
                return;
            }

            int bindingId = _bindingId;
            CombatOutcomePresentationRequest request = new CombatOutcomePresentationRequest(
                version,
                exchange.ChainSequenceId,
                exchange.CurrentDeclaration,
                exchange.ResponseState,
                exchange.CurrentResponse,
                exchange.CurrentClashResult,
                exchange.CurrentOutcomeAction,
                exchange.CurrentExecutionResult,
                exchange.CurrentPostureResult,
                exchange.CurrentStunResult,
                exchange.CurrentAftermathSnapshot,
                exchange.CurrentAftermathDecision,
                () => CompleteOutcomePresentation(bindingId, version));
            _pendingOutcomePresentationRequest = request;

            Action<CombatOutcomePresentationRequest> handlers = OutcomePresentationRequested;
            if (handlers == null)
            {
                request.TryComplete();
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            if (invocationList.Length > 1)
            {
                Debug.LogWarning(
                    "[CombatFlowOrchestrator] Multiple outcome presenters are bound. Only the first will run.",
                    this);
            }

            try
            {
                ((Action<CombatOutcomePresentationRequest>)invocationList[0]).Invoke(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                request.TryComplete();
            }
        }

        private bool CompleteOutcomePresentation(
            int bindingId,
            int expectedExchangeVersion)
        {
            if (bindingId != _bindingId || !IsFinalExchangeBound() ||
                _boundStateMachine.Phase != Phase.ApplyOutcome ||
                _session.ExchangeState.Version != expectedExchangeVersion ||
                _pendingOutcomePresentationRequest == null ||
                _pendingOutcomePresentationRequest.ExchangeVersion != expectedExchangeVersion)
            {
                return false;
            }

            _completedOutcomePresentationVersion = expectedExchangeVersion;
            _pendingOutcomePresentationRequest = null;
            RequestPump();
            return true;
        }

        private void PublishDecisionRequest(
            CombatExchangeDecisionKind kind,
            Side actingSide,
            ICombatant actingActor,
            Action<CombatExchangeDecisionRequest> handlers)
        {
            int version = _session.ExchangeState.Version;
            if (_pendingDecisionRequest != null &&
                _pendingDecisionRequest.Kind == kind &&
                _pendingDecisionRequest.ExchangeVersion == version &&
                _pendingDecisionRequest.Phase == _boundStateMachine.Phase)
            {
                return;
            }

            CombatExchangeDecisionRequest request = new CombatExchangeDecisionRequest(
                kind,
                version,
                actingSide,
                actingActor,
                _boundStateMachine.Phase);
            _pendingDecisionRequest = request;
            InvokeDecisionHandlers(handlers, request);
        }

        private void PublishApproachRequest()
        {
            CombatExchangeState exchange = _session.ExchangeState;
            int version = exchange.Version;
            if (_pendingApproachRequest != null &&
                _pendingApproachRequest.ExchangeVersion == version)
            {
                return;
            }

            int bindingId = _bindingId;
            CombatApproachPresentationRequest request = new CombatApproachPresentationRequest(
                version,
                exchange.CurrentDeclaration,
                () => CompleteApproach(bindingId, version));
            _pendingApproachRequest = request;

            Action<CombatApproachPresentationRequest> handlers = ApproachPresentationRequested;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            if (invocationList.Length > 1)
                Debug.LogWarning("[CombatFlowOrchestrator] Multiple approach presenters are bound. Only the first will run.", this);

            try
            {
                ((Action<CombatApproachPresentationRequest>)invocationList[0]).Invoke(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private bool CompleteApproach(int bindingId, int expectedExchangeVersion)
        {
            if (bindingId != _bindingId || !IsFinalExchangeBound() ||
                !_boundStateMachine.CompleteApproach(expectedExchangeVersion))
            {
                return false;
            }

            _pendingApproachRequest = null;
            RequestPump();
            return true;
        }

        private void HandleEnemyActionRequired(CombatSession session)
        {
            if (!ReferenceEquals(session, _session) || !IsFinalExchangeBound() ||
                _boundStateMachine.Phase != Phase.Standoff)
            {
                return;
            }

            CombatExchangeState exchange = _session.ExchangeState;
            if (exchange.CurrentAttackSide != Side.Enemies &&
                !_boundStateMachine.TryGrantStandoffAttackAuthority(
                    Side.Enemies,
                    exchange.Version))
            {
                return;
            }

            PublishDecisionRequest(
                CombatExchangeDecisionKind.Attack,
                Side.Enemies,
                null,
                AttackDecisionRequested);
            _pumpRequested = true;
        }

        private void RaiseTerminalCompletionOnce(int exchangeVersion)
        {
            if (_terminalCompletionRaised)
                return;

            _terminalCompletionRaised = true;
            Action<CombatSession, CombatEndReason, int> handlers = TerminalCompleted;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatSession, CombatEndReason, int>)invocationList[i]).Invoke(
                        _session,
                        _boundStateMachine.EndReason,
                        exchangeVersion);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void RequestPump()
        {
            if (_isPumping || _isTicking)
            {
                _pumpRequested = true;
                return;
            }

            AdvanceUntilBlocked();
        }

        private void ResetFinalExchangeBinding()
        {
            if (_boundStateMachine != null)
                _boundStateMachine.OnEnemyActionRequired -= HandleEnemyActionRequired;

            _bindingId++;
            _boundStateMachine = null;
            _randomSource = null;
            _clashInputProvider = null;
            _pendingDecisionRequest = null;
            _pendingApproachRequest = null;
            _pendingOutcomePresentationRequest = null;
            _completedOutcomePresentationVersion = -1;
            _isPumping = false;
            _isTicking = false;
            _pumpRequested = false;
            _terminalCompletionRaised = false;
        }

        private bool IsFinalExchangeBound()
        {
            return _session != null &&
                   _session.FlowMode == CombatFlowMode.StandoffClashChain &&
                   _boundStateMachine != null &&
                   _randomSource != null &&
                   _clashInputProvider != null;
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }

        private static void InvokeDecisionHandlers(
            Action<CombatExchangeDecisionRequest> handlers,
            CombatExchangeDecisionRequest request)
        {
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatExchangeDecisionRequest>)invocationList[i]).Invoke(request);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private bool BuildAdditionalAllyPlans(
            CombatTurn turn,
            Dictionary<CombatantId, ActionPlan> plans,
            out string errorMessage)
        {
            errorMessage = null;

            for (int i = 1; i < _session.Allies.Count; i++)
            {
                ICombatant ally = _session.Allies[i];
                if (ally == null)
                    return Fail("An additional ally is null.", out errorMessage);

                ActionPlan source = turn.TryGetPlan(ally.Id, out ActionPlan existing)
                    ? existing
                    : new ActionPlan(PlannedAction.None, PlannedAction.None);

                if (!CombatPlanValidator.TryNormalizePlan(
                        _session,
                        ally,
                        source,
                        out ActionPlan normalized,
                        out errorMessage))
                {
                    return false;
                }

                plans.Add(ally.Id, normalized);
            }

            return true;
        }

        private bool BuildEnemyPlans(
            CombatTurn turn,
            Dictionary<CombatantId, ActionPlan> plans,
            out string errorMessage)
        {
            errorMessage = null;

            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy == null)
                    return Fail("An enemy combatant is null.", out errorMessage);

                ActionPlan source;
                if (turn.TryGetPlan(enemy.Id, out ActionPlan existing))
                {
                    source = existing;
                }
                else if (!_enemyPolicy.TryCreatePlan(
                             new EnemyCombatPlanRequest(_session, enemy),
                             out source))
                {
                    return Fail($"Enemy policy could not create a plan for {enemy.Id.Value}.", out errorMessage);
                }

                if (!CombatPlanValidator.TryNormalizePlan(
                        _session,
                        enemy,
                        source,
                        out ActionPlan normalized,
                        out errorMessage))
                {
                    return false;
                }

                plans.Add(enemy.Id, normalized);
            }

            return true;
        }

        private static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }
}
