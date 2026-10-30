using System;
using System.Collections.Generic;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatFlowOrchestrator : MonoBehaviour
    {
        private const int MaxAutomaticStepsPerPump = 32;

        [SerializeField] private CombatEntryPoint entryPoint;
        [SerializeField, Min(0.1f)] private float allOutDecisionDurationSeconds = 10f;

        private readonly DeterministicCycleEnemyCombatPolicy _enemyPolicy =
            new DeterministicCycleEnemyCombatPolicy();
        private readonly FinalCombatPostureRule _postureRule = new FinalCombatPostureRule();
        private readonly FinalCombatTerminalPolicy _terminalPolicy = new FinalCombatTerminalPolicy();
        private readonly HashSet<CombatFeintCueKey> _raisedFeintCues = new();

        private CombatSession _session;
        private CombatStateMachine _boundStateMachine;
        private ICombatRuleRandomSource _randomSource;
        private ICombatClashSideInputProvider _clashInputProvider;
        private ICombatItemUseExecutor _combatItemUseExecutor;
        private CombatExchangeDecisionRequest _pendingDecisionRequest;
        private CombatApproachPresentationRequest _pendingApproachRequest;
        private CombatOutcomePresentationRequest _pendingOutcomePresentationRequest;
        private int _completedOutcomePresentationVersion = -1;
        private int _bindingId;
        private bool _isPumping;
        private bool _isTicking;
        private bool _pumpRequested;
        private bool _terminalCompletionRaised;
        private bool _isReplacingDeclaration;
        private bool _isExecutingAllOut;
        private bool _isExecutingItem;
        private float _allOutDecisionRemaining;
        private CombatClashSeriesConfig? _seriesValidationConfig;
        private Func<CombatClashSeriesState, ISkill> _seriesFinalSkillSelector;
        private Func<CombatExchangeDecisionRequest, ISkill> _seriesTimeoutAttackSelector;
        private float _seriesDecisionDuration;
        private float _seriesDecisionRemaining;
        private ISkill _seriesAttackSkill;
        private long? _seriesAttackCardRuntimeId;
        private int _seriesAttackBpInvestment;
        private bool _bpCurrentAttackCycleHadSuccessfulExchange;
        private CombatClashRoundPresentationRequest _pendingRoundPresentationRequest;
        private int _lastPresentedRound;
#if UNITY_EDITOR
        private string _lastClashBlockedDiagnostic;
#endif

        public event Action<CombatExchangeDecisionRequest> AttackDecisionRequested;
        public event Action<CombatExchangeDecisionRequest> ResponseDecisionRequested;
        public event Action<CombatExchangeDecisionRequest> ChainDecisionRequested;
        public event Action<CombatExchangeDecisionRequest> ClashSeriesDecisionRequested;
        public event Action<CombatClashRoundPresentationRequest> ClashRoundPresentationRequested;
        public event Action ClashSeriesValidationEnabled;
        public event Action CombatItemOptionsChanged;
        public event Action<CombatApproachPresentationRequest> ApproachPresentationRequested;
        public event Action<CombatOutcomePresentationRequest> OutcomePresentationRequested;
        public event Action<CombatSkillExecutionResult> AllOutPresentationRequested;
        public event Action<CombatOvercomeResult> OvercomePresentationRequested;
        public event Action<CombatFeintPresentationRequest> FeintPresentationRequested;
        public event Action ChainDecisionAccepted;
        public event Action<CombatSession> FinalExchangeUnbound;
        public event Action<CombatSession, CombatEndReason, int> TerminalCompleted;

        public CombatExchangeDecisionRequest PendingDecisionRequest => _pendingDecisionRequest;
        public CombatApproachPresentationRequest PendingApproachRequest => _pendingApproachRequest;
        public CombatOutcomePresentationRequest PendingOutcomePresentationRequest =>
            _pendingOutcomePresentationRequest;
        public CombatClashRoundPresentationRequest PendingClashRoundPresentationRequest =>
            _pendingRoundPresentationRequest;
        public float ClashSeriesDecisionTimeRemaining => _seriesDecisionRemaining;
        public bool IsClashSeriesValidationEnabled => _seriesValidationConfig.HasValue;
        public float AllOutDecisionTimeRemaining => _allOutDecisionRemaining;
        public bool IsAllOutSelection => IsFinalExchangeBound() &&
            _boundStateMachine.Phase == Phase.ChainDecision &&
            _session.ExchangeState.IsAllOutCandidate;

        public string AllOutUnavailableReason => !IsAllOutSelection ? "총공격 후보 없음" :
            GameStateMachine.Instance?.Is(GameState.Paused) == true || Time.timeScale <= 0f ? "일시 정지" :
            !CombatAllOutPolicy.IsCandidate(_session) ? "대상 상태 변경" :
            !CombatAllOutExecutionPolicy.TryCreateExecutionRequest(_session, out _) ?
                "행동 가능한 아군의 제작된 AllEnemies 공격 스킬 없음" : null;

        public CombatDeclarationToken? CurrentDeclarationToken =>
            IsFinalExchangeBound() && _boundStateMachine.Phase == Phase.AttackDeclaration &&
            _session.ExchangeState.CurrentDeclaration != null
                ? new CombatDeclarationToken(_session.CompletionId, _session.ExchangeState.DeclarationId,
                    _session.ExchangeState.DeclarationRevision, _session.ExchangeState.Version)
                : null;

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
                new AuthoredCombatClashSideInputProvider(_session));
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
                clashInputProvider ?? new AuthoredCombatClashSideInputProvider(_session));
            return true;
        }

        public bool IsBoundTo(CombatSession session, CombatStateMachine stateMachine)
        {
            return ReferenceEquals(_session, session) &&
                   ReferenceEquals(_boundStateMachine, stateMachine) &&
                   IsFinalExchangeBound();
        }

        /// <summary>
        /// Explicit per-session validation opt-in. No serialized or Production default is supplied.
        /// Both skill policies and all numbers must come from the validation caller.
        /// </summary>
        public bool TryEnableClashSeriesValidation(
            CombatClashSeriesConfig config,
            float decisionDurationSeconds,
            Func<CombatClashSeriesState, ISkill> finalSkillSelector,
            Func<CombatExchangeDecisionRequest, ISkill> timeoutAttackSelector)
        {
            if (_session?.HasProductionHandBpInitialization == true)
                return false;
            CombatExchangeState exchange = _session?.ExchangeState;
            bool beforeFirstCommit = _boundStateMachine?.Phase == Phase.Standoff ||
                (_boundStateMachine?.Phase == Phase.AttackDeclaration &&
                 exchange?.CurrentDeclaration != null && !exchange.IsCommitted &&
                 exchange.ResponseState == CombatResponseState.Pending);
            if (!IsFinalExchangeBound() || !beforeFirstCommit ||
                _session.Allies.Count < 1 || _session.Allies.Count > 3 || _session.Enemies.Count < 1 ||
                exchange.ClashSeries != null)
                return false;

            if (!TryConfigureClashSeries(config, decisionDurationSeconds, finalSkillSelector, timeoutAttackSelector))
                return false;
            ClashSeriesValidationEnabled?.Invoke();
            return true;
        }

        private bool TryConfigureClashSeries(
            CombatClashSeriesConfig config,
            float decisionDurationSeconds,
            Func<CombatClashSeriesState, ISkill> finalSkillSelector,
            Func<CombatExchangeDecisionRequest, ISkill> timeoutAttackSelector)
        {
            if (config.AttackerDurability <= 0 || config.ResponderDurability <= 0 || config.MaxComparisons <= 0 ||
                decisionDurationSeconds <= 0f || float.IsNaN(decisionDurationSeconds) ||
                float.IsInfinity(decisionDurationSeconds) || finalSkillSelector == null || timeoutAttackSelector == null)
                return false;
            _seriesValidationConfig = config;
            _seriesDecisionDuration = decisionDurationSeconds;
            _seriesFinalSkillSelector = finalSkillSelector;
            _seriesTimeoutAttackSelector = timeoutAttackSelector;
            return true;
        }

        public IReadOnlyList<ISkill> GetAvailableClashSeriesSkills(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrentSeriesDecision(request) || request.ActingActor?.Skills == null)
                return Array.Empty<ISkill>();

            List<ISkill> skills = new List<ISkill>();
            foreach (ISkill skill in request.ActingActor.Skills)
            {
                if (CanUseClashSeriesSkill(request, skill))
                    skills.Add(skill);
            }
            return skills;
        }

        public CombatSkillAvailability GetSkillAvailability(
            CombatExchangeDecisionRequest request,
            ICombatant actor,
            ISkill skill)
        {
            return GetSkillAvailability(request, actor, skill, null);
        }

        public CombatSkillAvailability GetSkillAvailability(
            CombatExchangeDecisionRequest request,
            ICombatant actor,
            ISkill skill,
            long? cardRuntimeId)
        {
            if (request == null || !IsFinalExchangeBound() || !ReferenceEquals(_pendingDecisionRequest, request) ||
                request.ExchangeVersion != _session.ExchangeState.Version ||
                request.Phase != _boundStateMachine.Phase ||
                ((request.Kind == CombatExchangeDecisionKind.ClashAttack ||
                  request.Kind == CombatExchangeDecisionKind.ClashResponse) &&
                 !IsCurrentSeriesDecision(request)))
                return new CombatSkillAvailability(skill, CombatSkillUnavailableReason.InvalidPhase);

            return CombatSkillAvailabilityPolicy.Evaluate(
                _session, request, actor, skill, _seriesAttackSkill, cardRuntimeId);
        }

        public bool CanUseClashSeriesSkill(CombatExchangeDecisionRequest request, ISkill skill)
        {
            return IsCurrentSeriesDecision(request) &&
                   GetSkillAvailability(request, request.ActingActor, skill).IsAvailable;
        }

        public bool SubmitClashSeriesSkill(CombatExchangeDecisionRequest request, ISkill skill)
        {
            return SubmitClashSeriesSkill(request, skill, null);
        }

        public bool SubmitClashSeriesSkill(CombatExchangeDecisionRequest request, ISkill skill,
            long? cardRuntimeId)
        {
            return SubmitClashSeriesSkill(request, skill, cardRuntimeId, 0);
        }

        public bool SubmitClashSeriesSkill(CombatExchangeDecisionRequest request, ISkill skill,
            long? cardRuntimeId, int bpInvestment)
        {
            if (!IsCurrentSeriesDecision(request) ||
                !GetSkillAvailability(request, request.ActingActor, skill, cardRuntimeId).IsAvailable ||
                !CanInvestBp(request.ActingActor, skill, bpInvestment))
                return false;

            if (request.Kind == CombatExchangeDecisionKind.ClashAttack)
            {
                _seriesAttackSkill = skill;
                _seriesAttackCardRuntimeId = cardRuntimeId;
                _seriesAttackBpInvestment = bpInvestment;
                _pendingDecisionRequest = null;
                RequestPump();
                return true;
            }

            CombatClashSeriesState series = _session.ExchangeState.ClashSeries;
            if (!series.TryCommitNextRound(request.ClashSeriesToken.Value,
                    _seriesAttackSkill, skill, _seriesAttackCardRuntimeId,
                    cardRuntimeId, _seriesAttackBpInvestment, bpInvestment, out _))
                return false;

            _seriesAttackSkill = null;
            _seriesAttackCardRuntimeId = null;
            _seriesAttackBpInvestment = 0;
            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool SubmitClashSeriesNoResponse(CombatExchangeDecisionRequest request)
        {
            if (!IsCurrentSeriesDecision(request) ||
                request.Kind != CombatExchangeDecisionKind.ClashResponse ||
                _seriesAttackSkill == null ||
                !_session.ExchangeState.ClashSeries.TryCommitNextRound(
                    request.ClashSeriesToken.Value, _seriesAttackSkill, null,
                    _seriesAttackCardRuntimeId, null, _seriesAttackBpInvestment, 0, out _))
                return false;

            _seriesAttackSkill = null;
            _seriesAttackCardRuntimeId = null;
            _seriesAttackBpInvestment = 0;
            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public void RegisterCombatItemUseExecutor(ICombatItemUseExecutor executor)
        {
            _combatItemUseExecutor = executor;
            NotifyCombatItemOptionsChanged();
        }

        public void UnregisterCombatItemUseExecutor(ICombatItemUseExecutor executor)
        {
            if (ReferenceEquals(_combatItemUseExecutor, executor))
            {
                _combatItemUseExecutor = null;
                NotifyCombatItemOptionsChanged();
            }
        }

        internal void NotifyCombatItemOptionsChanged()
        {
            if (_isExecutingItem || CombatItemOptionsChanged == null)
                return;
            foreach (Action handler in CombatItemOptionsChanged.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        public IReadOnlyList<CombatItemOption> GetUsableCombatItems(ICombatant user)
        {
            if (!CanAcceptCombatItemCommand(_session?.ExchangeState?.Version ?? -1) ||
                _combatItemUseExecutor == null)
            {
                return Array.Empty<CombatItemOption>();
            }

            return _combatItemUseExecutor.GetUsableItems(_session, user) ??
                   Array.Empty<CombatItemOption>();
        }

        public IReadOnlyList<ICombatant> GetUsableCombatItemTargets(
            ICombatant user,
            CombatItemOption item)
        {
            if (!CanAcceptCombatItemCommand(_session?.ExchangeState?.Version ?? -1) ||
                _combatItemUseExecutor == null)
            {
                return Array.Empty<ICombatant>();
            }

            return _combatItemUseExecutor.GetUsableTargets(_session, user, item) ??
                   Array.Empty<ICombatant>();
        }

        public bool TryUseCombatItem(
            CombatItemUseRequest request,
            out CombatItemUseResult result)
        {
            result = CombatItemUseResult.Failure(CombatItemUseStatus.InvalidRequest, request?.ItemId);
            if (request == null)
                return false;

            if (!CanAcceptCombatItemCommand(request.ExchangeVersion))
            {
                result = CombatItemUseResult.Failure(CombatItemUseStatus.StaleRequest, request.ItemId);
                return false;
            }

            if (_combatItemUseExecutor == null)
            {
                result = CombatItemUseResult.Failure(CombatItemUseStatus.MissingExecutor, request.ItemId);
                return false;
            }

            if (!CanUseCombatItemUser(request.User))
            {
                result = CombatItemUseResult.Failure(CombatItemUseStatus.InvalidRequest, request.ItemId);
                return false;
            }

            int bindingId = _bindingId;
            CombatStateMachine itemMachine = _boundStateMachine;
            if (!itemMachine.TryBeginItemExecution(request.ExchangeVersion))
                return false;
            _isExecutingItem = true;
            try
            {
                result = _combatItemUseExecutor.TryUse(_session, request);
                if (result != null && !result.Succeeded && result.ItemConsumed &&
                    bindingId == _bindingId)
                    itemMachine.ForceExit(CombatEndReason.Abort);
                if (bindingId != _bindingId || result == null || !result.Succeeded)
                    return false;

                itemMachine.EndItemExecution();
                if (!itemMachine.TryGrantStandoffAttackAuthority(Side.Enemies, request.ExchangeVersion))
                {
                    result = new CombatItemUseResult(
                        CombatItemUseStatus.FlowTransitionFailed, request.ItemId, result.ItemConsumed,
                        result.EffectApplied, result.HpRecovered, result.StatusApplication);
                    return false;
                }
            }
            finally
            {
                itemMachine.EndItemExecution();
                _isExecutingItem = false;
                if (bindingId == _bindingId && _boundStateMachine?.Phase != Phase.ExitCombat)
                    NotifyCombatItemOptionsChanged();
                if (bindingId != _bindingId || _boundStateMachine?.Phase == Phase.ExitCombat)
                    RequestPump();
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            Tick(deltaSeconds, Time.unscaledDeltaTime);
        }

        public void Tick(float deltaSeconds, float unscaledDeltaSeconds)
        {
            if (!IsFinalExchangeBound() || _isExecutingAllOut || _isExecutingItem)
                return;

            if (GameStateMachine.Instance?.Is(GameState.Paused) == true || Time.timeScale <= 0f)
                return;

            _isTicking = true;
            try
            {
                if (_boundStateMachine.Phase == Phase.Standoff)
                    _boundStateMachine.Tick(deltaSeconds);
                else if (_boundStateMachine.Phase == Phase.Clash)
                    TickClashSeriesDecision(unscaledDeltaSeconds);
                else if (IsAllOutSelection && _pendingDecisionRequest != null &&
                         unscaledDeltaSeconds > 0f && !float.IsNaN(unscaledDeltaSeconds) &&
                         !float.IsInfinity(unscaledDeltaSeconds))
                {
                    _allOutDecisionRemaining = Mathf.Max(0f, _allOutDecisionRemaining - unscaledDeltaSeconds);
                    if (_allOutDecisionRemaining <= 0f)
                        EndChain(_pendingDecisionRequest.ExchangeVersion);
                }
            }
            finally
            {
                _isTicking = false;
            }

            AdvanceUntilBlocked();
        }

        /// <summary>
        /// Emits one non-blocking visual cue for an explicitly opted-in enemy. This method never
        /// changes combat state, authority, input, pressure, or combatant runtime values.
        /// Automatic feint scheduling remains disabled until its gameplay policy is specified.
        /// </summary>
        public bool TryRequestFeintCue(CombatantId enemyCombatantId, int expectedExchangeVersion)
        {
            if (!IsFinalExchangeBound() || _boundStateMachine.Phase != Phase.Standoff ||
                _session.ExchangeState.Version != expectedExchangeVersion)
            {
                return false;
            }

            ICombatant enemy = FindEnemy(enemyCombatantId);
            if (enemy == null || enemy.IsStunned ||
                !_session.TryGetCombatState(enemy, out CombatantCombatState state) ||
                !state.IsAlive || state.IsPanicked ||
                !_session.TryGetEnemyBehavior(enemy, out EnemyCombatBehaviorSnapshot behavior) ||
                !behavior.FeintCueEnabled)
            {
                return false;
            }

            CombatFeintCueKey key = new(_session.CompletionId, expectedExchangeVersion, enemyCombatantId);
            if (!_raisedFeintCues.Add(key))
                return false;

            RaiseFeintPresentation(new CombatFeintPresentationRequest(
                _session.CompletionId,
                expectedExchangeVersion,
                enemyCombatantId));
            return true;
        }

        public void AdvanceUntilBlocked()
        {
            if (!IsFinalExchangeBound())
                return;

            if (_isPumping || _isTicking || _isReplacingDeclaration || _isExecutingAllOut || _isExecutingItem)
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
            if (_isExecutingItem || !IsFinalExchangeBound() ||
                !HasRequiredCard(declaration?.Attacker, declaration?.Skill,
                    CombatCardKind.Normal, declaration?.CardRuntimeId) ||
                !CanInvestBp(declaration?.Attacker, declaration?.Skill,
                    declaration?.BpInvestment ?? 0) ||
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
            if (_isReplacingDeclaration || !IsFinalExchangeBound() ||
                !HasRequiredCard(response?.Responder, response?.Skill,
                    CombatCardKind.Clash, response?.CardRuntimeId) ||
                !CanInvestBp(response?.Responder, response?.Skill,
                    response?.BpInvestment ?? 0) ||
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
            if (_isReplacingDeclaration || !IsFinalExchangeBound() ||
                !_boundStateMachine.ConfirmNoResponse(expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool SubmitResponse(CombatResponseDeclaration response, CombatExchangeDecisionRequest request)
        {
            return IsCurrentResponseRequest(request) && SubmitResponse(response, request.ExchangeVersion);
        }

        public bool SubmitNoResponse(CombatExchangeDecisionRequest request)
        {
            return IsCurrentResponseRequest(request) && SubmitNoResponse(request.ExchangeVersion);
        }

        /// <summary>Explicit opt-in request; no timer, automatic AI choice, or presentation callback schedules it.</summary>
        public bool TryReplaceEnemyDeclaration(CombatDeclarationToken token, ISkill replacementSkill)
        {
            if (_isReplacingDeclaration || !IsFinalExchangeBound())
                return false;

            _isReplacingDeclaration = true;
            bool accepted;
            try
            {
                accepted = _boundStateMachine.TryReplaceEnemyDeclaration(token, replacementSkill);
                if (accepted)
                    _pendingDecisionRequest = null;
            }
            finally
            {
                _isReplacingDeclaration = false;
            }

            if (accepted)
                RequestPump();
            return accepted;
        }

        private bool IsCurrentResponseRequest(CombatExchangeDecisionRequest request)
        {
            CombatDeclarationToken? current = CurrentDeclarationToken;
            CombatDeclarationToken? requested = request?.DeclarationToken;
            return ReferenceEquals(request, _pendingDecisionRequest) &&
                   request?.Kind == CombatExchangeDecisionKind.Response &&
                   current.HasValue && requested.HasValue &&
                   requested.Value.SessionId == current.Value.SessionId &&
                   requested.Value.DeclarationId == current.Value.DeclarationId &&
                   requested.Value.Revision == current.Value.Revision &&
                   requested.Value.ExchangeVersion == current.Value.ExchangeVersion;
        }

        public bool ContinueChain(ISkill skill, int expectedExchangeVersion)
        {
            ICombatant owner = _session?.ExchangeState?.CurrentAttackActor;
            if (!HasRequiredCard(owner, skill, CombatCardKind.Normal, null))
                return false;
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryContinueChain(skill, expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RaiseChainDecisionAccepted();
            RequestPump();
            return true;
        }

        public bool ContinueChain(int expectedExchangeVersion)
        {
            if (!HasAnyNormalCard(_session?.ExchangeState?.CurrentAttackActor))
                return false;
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryContinueChain(expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RaiseChainDecisionAccepted();
            RequestPump();
            return true;
        }

        public bool ExecuteAllOut(int expectedExchangeVersion)
        {
            CombatExchangeDecisionRequest request = _pendingDecisionRequest;
            return request?.ExchangeVersion == expectedExchangeVersion && ExecuteAllOut(request);
        }

        public bool ExecuteAllOut(CombatExchangeDecisionRequest request)
        {
            if (_isExecutingAllOut || !IsCurrentAllOutRequest(request) || AllOutUnavailableReason != null)
                return false;

            CombatExchangeState exchange = _session.ExchangeState;
            int bindingId = _bindingId;
            Side cycleSide = exchange.CurrentAttackSide;
            int cycleId = exchange.DeclarationId;
            bool executed;
            CombatSkillExecutionResult result;
            _isExecutingAllOut = true;
            try
            {
                executed = _boundStateMachine.TryExecuteAllOut(request.ExchangeVersion, out result);
            }
            finally { _isExecutingAllOut = false; }
            if (bindingId != _bindingId)
            {
                RequestPump();
                return executed;
            }
            if (!executed)
            {
                if (_boundStateMachine.Phase == Phase.ExitCombat) RequestPump();
                return false;
            }

            if (_boundStateMachine.Phase != Phase.ExitCombat && cycleId > 0)
            {
                _session.TryRecordCompletedAttackCycle(cycleSide, cycleId, true);
                _session.TryRecordCompletedBpAttackCycle(cycleSide, cycleId, true);
            }
            _bpCurrentAttackCycleHadSuccessfulExchange = false;

            _pendingDecisionRequest = null;
            _allOutDecisionRemaining = 0f;
            RaiseChainDecisionAccepted();
            RaiseAllOutPresentation(result);
            RequestPump();
            return true;
        }

        public bool DeclineAllOut(CombatExchangeDecisionRequest request)
        {
            return IsCurrentAllOutRequest(request) && EndChain(request.ExchangeVersion);
        }

        private bool IsCurrentAllOutRequest(CombatExchangeDecisionRequest request)
        {
            return IsAllOutSelection && request != null &&
                ReferenceEquals(_pendingDecisionRequest, request) &&
                request.Kind == CombatExchangeDecisionKind.Chain && request.ActingSide == Side.Allies &&
                request.Phase == _boundStateMachine.Phase &&
                request.ExchangeVersion == _session.ExchangeState.Version &&
                GameStateMachine.Instance?.Is(GameState.Paused) != true && Time.timeScale > 0f;
        }

        public bool CanExecuteUltimate(CombatExchangeDecisionRequest request,
            ICombatant actor, ICombatant target)
        {
            return !_isExecutingItem && IsFinalExchangeBound() && request != null &&
                ReferenceEquals(_pendingDecisionRequest, request) &&
                (request.Kind == CombatExchangeDecisionKind.Attack ||
                 request.Kind == CombatExchangeDecisionKind.Chain) &&
                request.ActingSide == actor?.Side &&
                (request.ActingActor == null || ReferenceEquals(request.ActingActor, actor)) &&
                request.Phase == _boundStateMachine.Phase &&
                GameStateMachine.Instance?.Is(GameState.Paused) != true &&
                _boundStateMachine.CanExecuteUltimate(actor, target, request.ExchangeVersion);
        }

        public bool ExecuteUltimate(CombatExchangeDecisionRequest request,
            ICombatant actor, ICombatant target)
        {
            if (!CanExecuteUltimate(request, actor, target)) return false;

            CombatExchangeState exchange = _session.ExchangeState;
            int bindingId = _bindingId;
            CombatStateMachine machine = _boundStateMachine;
            bool endedChain = _boundStateMachine.Phase == Phase.ChainDecision;
            Side cycleSide = exchange.CurrentAttackSide;
            int cycleId = exchange.DeclarationId;
            if (!machine.TryExecuteUltimate(actor, target,
                    request.ExchangeVersion, out CombatSkillExecutionResult result))
            {
                if (bindingId != _bindingId || machine.Phase == Phase.ExitCombat) RequestPump();
                return false;
            }
            if (bindingId != _bindingId)
            {
                RequestPump();
                return true;
            }

            if (endedChain && _boundStateMachine.Phase != Phase.ExitCombat)
            {
                _session.TryRecordCompletedAttackCycle(cycleSide, cycleId, true);
                _session.TryRecordCompletedBpAttackCycle(cycleSide, cycleId, true);
                _bpCurrentAttackCycleHadSuccessfulExchange = false;
                _session.StandoffState.Reset();
                RaiseChainDecisionAccepted();
            }

            _pendingDecisionRequest = null;
            RaiseAllOutPresentation(result);
            RequestPump();
            return true;
        }

        public bool Handoff(
            ICombatant receivingActor,
            ISkill receivingSkill,
            bool isHandoffEligible,
            int expectedExchangeVersion)
        {
            if (!HasRequiredCard(receivingActor, receivingSkill, CombatCardKind.Normal, null) ||
                !IsFinalExchangeBound() ||
                !_boundStateMachine.TryHandoffChain(
                    receivingActor,
                    receivingSkill,
                    isHandoffEligible,
                    expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RaiseChainDecisionAccepted();
            RequestPump();
            return true;
        }

        private bool HasRequiredCard(ICombatant actor, ISkill skill,
            CombatCardKind kind, long? runtimeId)
        {
            return actor != null && skill != null && _session != null &&
                (!_session.TryGetHand(actor, out CombatHandRuntime hand) ||
                 hand.TryFindInHand(skill, kind, runtimeId, out _));
        }

        public bool CanInvestBp(ICombatant actor, ISkill skill, int amount)
        {
            if (amount == 0) return true;
            return IsFinalExchangeBound() && _session.BpRuntime != null &&
                CombatBpEnhancementPolicy.CanInvest(_session, skill, amount) &&
                _session.BpRuntime.CanInvest(actor, amount);
        }

        private bool HasAnyNormalCard(ICombatant actor)
        {
            if (actor == null || _session == null || !_session.TryGetHand(actor, out CombatHandRuntime hand))
                return actor != null;
            IReadOnlyList<CombatHandCard> cards = hand.Hand;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].Kind == CombatCardKind.Normal &&
                    CombatChainPolicy.CanContinue(_session, actor, cards[i].Skill))
                    return true;
            return false;
        }

        public bool EndChain(int expectedExchangeVersion)
        {
            if (IsAllOutSelection && (GameStateMachine.Instance?.Is(GameState.Paused) == true ||
                Time.timeScale <= 0f || _isExecutingAllOut)) return false;
            CombatExchangeState exchange = _session?.ExchangeState;
            int bindingId = _bindingId;
            Side cycleSide = exchange?.CurrentAttackSide ?? Side.Allies;
            int cycleId = exchange?.DeclarationId ?? 0;
            bool latestExchangeSucceeded = exchange?.IsApplyOutcomeFinalized == true;
            bool bpCycleSucceeded = _bpCurrentAttackCycleHadSuccessfulExchange ||
                                    latestExchangeSucceeded;
            if (!IsFinalExchangeBound() ||
                !_boundStateMachine.TryEndChain(expectedExchangeVersion))
            {
                return false;
            }

            if (bindingId != _bindingId)
            {
                RequestPump();
                return true;
            }

            _session.TryRecordCompletedAttackCycle(cycleSide, cycleId, latestExchangeSucceeded);
            _session.TryRecordCompletedBpAttackCycle(cycleSide, cycleId, bpCycleSucceeded);
            _bpCurrentAttackCycleHadSuccessfulExchange = false;
            _session.StandoffState.Reset();
            _pendingDecisionRequest = null;
            _allOutDecisionRemaining = 0f;
            RaiseChainDecisionAccepted();
            RequestPump();
            return true;
        }

        public bool YieldStandoffAttackAuthority(Side yieldingSide, int expectedExchangeVersion)
        {
            if (_isExecutingItem || !IsFinalExchangeBound() || _session.ExchangeState.CurrentAttackSide != yieldingSide ||
                !_boundStateMachine.TryGrantStandoffAttackAuthority(
                    Opposite(yieldingSide), expectedExchangeVersion))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RequestPump();
            return true;
        }

        public bool TryOvercome(
            ICombatant actor,
            int expectedExchangeVersion,
            out CombatOvercomeResult result)
        {
            result = null;
            if (_isExecutingItem || !IsFinalExchangeBound() ||
                !_boundStateMachine.TryExecuteOvercome(actor, expectedExchangeVersion, out result))
            {
                return false;
            }

            _pendingDecisionRequest = null;
            RaiseOvercomePresentation(result);
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
            // Startup validates this immutable session snapshot before OpeningEffect. Install it
            // before the first pump, without firing Debug opt-in events or publishing commands.
            if (_session.HasProductionHandBpInitialization &&
                (!_session.ProductionClashConfig.HasValue ||
                 !TryConfigureClashSeries(_session.ProductionClashConfig.Value,
                     _session.ProductionClashDecisionDuration, SelectProductionFinalSkill, SelectProductionTimeoutAttack)))
                throw new InvalidOperationException("Production Hand/BP session is missing its required Clash configuration.");
            _boundStateMachine = stateMachine;
            _randomSource = randomSource;
            _clashInputProvider = clashInputProvider;
            _boundStateMachine.OnEnemyActionRequired += HandleEnemyActionRequired;
            AdvanceUntilBlocked();
        }

        private static ISkill SelectProductionFinalSkill(CombatClashSeriesState series)
        {
            if (series == null || !series.FinalOutcome.HasValue) return null;
            bool attackerWon = series.FinalOutcome == CombatClashOutcome.AttackerWin ||
                               series.FinalOutcome == CombatClashOutcome.Unopposed;
            var skills = attackerWon ? series.CommittedAttackSkills : series.CommittedResponseSkills;
            return skills.Count > 0 ? skills[skills.Count - 1] : null;
        }

        private ISkill SelectProductionTimeoutAttack(CombatExchangeDecisionRequest request)
        {
            var skills = GetAvailableClashSeriesSkills(request);
            return skills.Count > 0 ? skills[0] : null;
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
                    if (_boundStateMachine.TryRefreshDecisionBoundary(exchange.Version))
                        return true;
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
                    return _seriesValidationConfig.HasValue
                        ? TryAdvanceClashSeries(exchange)
                        : TryResolveClash(exchange);

                case Phase.ApplyOutcome:
                    return TryAdvanceApplyOutcome(exchange);

                case Phase.ChainDecision:
                    if (_boundStateMachine.TryRefreshDecisionBoundary(exchange.Version))
                    {
                        _pendingDecisionRequest = null;
                        _allOutDecisionRemaining = 0f;
                        RaiseChainDecisionAccepted();
                        return true;
                    }
                    PublishDecisionRequest(
                        CombatExchangeDecisionKind.Chain,
                        exchange.IsAllOutCandidate ? Side.Allies : exchange.CurrentAttackSide,
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

        private bool TryAdvanceClashSeries(CombatExchangeState exchange)
        {
            CombatClashSeriesState series = exchange.ClashSeries;
            if (series == null)
            {
                if (exchange.ResponseState != CombatResponseState.CounterDeclared)
                    return TryResolveClash(exchange);

                try
                {
                    _lastPresentedRound = 0;
                    _seriesAttackSkill = null;
                    _seriesAttackCardRuntimeId = null;
                    _seriesAttackBpInvestment = 0;
                    new CombatClashSeriesState(_session, _boundStateMachine,
                        _seriesValidationConfig.Value);
                    TraceClash("SeriesAttached");
                    return true;
                }
                catch (InvalidOperationException exception)
                {
                    Debug.LogError($"[CombatFlowOrchestrator] Clash series could not start: {exception.Message}", this);
                    return false;
                }
            }

            if (_pendingRoundPresentationRequest != null ||
                series.ComparisonCount > _lastPresentedRound)
            {
                TraceClashBlocked("AwaitPresentation", series);
                return false;
            }

            if (series.IsComplete)
            {
                _pendingDecisionRequest = null;
                _seriesDecisionRemaining = 0f;
                if (series.FinalOutcome == CombatClashOutcome.Tie)
                {
                    bool drawCompleted = _boundStateMachine.TryEndClashSeriesDraw(series, exchange.Version);
                    TraceClash(drawCompleted ? "SeriesDrawToStandoff" : "SeriesDrawRejected");
                    return drawCompleted;
                }

                ISkill finalSkill = _seriesFinalSkillSelector?.Invoke(series);
                bool finalCompleted = finalSkill != null && _boundStateMachine.TryCompleteClashSeries(
                    series, series.CommandToken, finalSkill, exchange.Version);
                TraceClash(finalCompleted ? "SeriesFinalToOutcome" : "SeriesFinalRejected");
                return finalCompleted;
            }

            if (series.IsRoundCommitted)
            {
                CombatAttackDeclaration attack = exchange.CurrentDeclaration;
                CombatResponseDeclaration response = exchange.CurrentResponse;
                if (attack == null || response == null ||
                    !_clashInputProvider.TryCreate(attack.Attacker, series.CurrentAttackSkill,
                        true, out CombatClashSideInput attackerInput) ||
                    !_clashInputProvider.TryCreate(response.Responder, series.CurrentResponseSkill,
                        false, out CombatClashSideInput responderInput) ||
                    !series.TryResolveRound(series.CommandToken, attackerInput,
                        responderInput, out CombatClashRoundResult result))
                {
                    TraceClashBlocked("RoundInputOrResolveRejected", series);
                    return false;
                }

                TraceClash("RoundCompared");
                PublishClashRoundPresentation(series, result, attack.Attacker, response.Responder);
                return false;
            }

            CombatExchangeDecisionKind kind = _seriesAttackSkill == null
                ? CombatExchangeDecisionKind.ClashAttack
                : CombatExchangeDecisionKind.ClashResponse;
            ICombatant actor = kind == CombatExchangeDecisionKind.ClashAttack
                ? exchange.CurrentDeclaration?.Attacker : exchange.CurrentResponse?.Responder;
            PublishClashSeriesDecision(kind, actor, series.CommandToken);
            TraceClashBlocked(actor == null ? "DecisionActorMissing" : "AwaitSeriesDecision", series);
            return false;
        }

        private void PublishClashRoundPresentation(
            CombatClashSeriesState series,
            CombatClashRoundResult result,
            ICombatant attacker,
            ICombatant responder)
        {
            int bindingId = _bindingId;
            int version = _session.ExchangeState.Version;
            CombatClashRoundPresentationRequest request = new CombatClashRoundPresentationRequest(
                version, series.CommandToken, result, attacker, responder,
                series.CurrentAttackSkill, series.CurrentResponseSkill,
                () => CompleteClashRoundPresentation(bindingId, series, result.ComparisonNumber, version));
            _pendingRoundPresentationRequest = request;
            TraceClash($"PresentationRequested request={request.GetHashCode()}");
            Action<CombatClashRoundPresentationRequest> handlers = ClashRoundPresentationRequested;
            if (handlers == null)
            {
                request.TryComplete();
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            if (invocationList.Length > 1)
                Debug.LogWarning(
                    "[CombatFlowOrchestrator] Multiple clash round presenters are bound. Only the first will run.",
                    this);

            try
            {
                ((Action<CombatClashRoundPresentationRequest>)invocationList[0]).Invoke(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                request.TryComplete();
            }
        }

        private bool CompleteClashRoundPresentation(
            int bindingId,
            CombatClashSeriesState series,
            int comparisonNumber,
            int version)
        {
            string rejection = bindingId != _bindingId ? "BindingChanged" :
                !IsFinalExchangeBound() ? "SessionUnbound" :
                _boundStateMachine.Phase != Phase.Clash ? "PhaseChanged" :
                _session.ExchangeState.Version != version ? "ExchangeVersionChanged" :
                !ReferenceEquals(_session.ExchangeState.ClashSeries, series) ? "SeriesChanged" :
                _pendingRoundPresentationRequest?.Result.ComparisonNumber != comparisonNumber
                    ? "PendingRoundChanged" : null;
            if (rejection != null)
            {
                TraceClash($"PresentationCompletionRejected reason={rejection} expectedVersion={version} expectedRound={comparisonNumber}");
                return false;
            }

            TraceClash($"PresentationCompletionAccepted request={_pendingRoundPresentationRequest.GetHashCode()}");
            _lastPresentedRound = comparisonNumber;
            _pendingRoundPresentationRequest = null;
            RequestPump();
            TraceClash("PresentationCompletionPumpRequested");
            return true;
        }

        private void PublishClashSeriesDecision(
            CombatExchangeDecisionKind kind,
            ICombatant actor,
            long token)
        {
            if (actor == null || (_pendingDecisionRequest != null &&
                _pendingDecisionRequest.Kind == kind &&
                _pendingDecisionRequest.ClashSeriesToken == token))
                return;

            CombatExchangeDecisionRequest request = new CombatExchangeDecisionRequest(
                kind, _session.ExchangeState.Version, actor.Side, actor, Phase.Clash,
                clashSeriesToken: token);
            _seriesDecisionRemaining = _seriesDecisionDuration;
            _pendingDecisionRequest = request;
            InvokeDecisionHandlers(ClashSeriesDecisionRequested, request);
        }

        private bool IsCurrentSeriesDecision(CombatExchangeDecisionRequest request)
        {
            return IsFinalExchangeBound() && ReferenceEquals(_pendingDecisionRequest, request) &&
                   request != null && request.Phase == Phase.Clash &&
                   (request.Kind == CombatExchangeDecisionKind.ClashAttack ||
                    request.Kind == CombatExchangeDecisionKind.ClashResponse) &&
                   request.ExchangeVersion == _session.ExchangeState.Version &&
                   request.ClashSeriesToken == _session.ExchangeState.ClashSeries?.CommandToken &&
                   _boundStateMachine.Phase == Phase.Clash &&
                   GameStateMachine.Instance?.Is(GameState.Paused) != true && Time.timeScale > 0f;
        }

        private void TickClashSeriesDecision(float unscaledSeconds)
        {
            CombatExchangeDecisionRequest request = _pendingDecisionRequest;
            if (!IsCurrentSeriesDecision(request) || _seriesDecisionRemaining <= 0f ||
                unscaledSeconds <= 0f)
                return;

            _seriesDecisionRemaining = Mathf.Max(0f, _seriesDecisionRemaining - unscaledSeconds);
            if (_seriesDecisionRemaining > 0f)
                return;

            TraceClash($"SeriesDecisionTimeout kind={request.Kind} actor={request.ActingActor?.Id}");
            if (request.Kind == CombatExchangeDecisionKind.ClashResponse)
                SubmitClashSeriesNoResponse(request);
            else
            {
                ISkill timeoutSkill = _seriesTimeoutAttackSelector?.Invoke(request);
                TraceClash($"SeriesTimeoutAttackSelected skill={timeoutSkill?.Name ?? "<none>"}");
                bool submitted = timeoutSkill != null && SubmitClashSeriesSkill(request, timeoutSkill);
                if (!submitted && GetAvailableClashSeriesSkills(request).Count == 0 &&
                    _session.ExchangeState.ClashSeries.TryConcludeWithoutNextAttack(
                        request.ClashSeriesToken.Value))
                {
                    _pendingDecisionRequest = null;
                    TraceClash("SeriesNoAttackToDraw");
                    RequestPump();
                }
                else if (!submitted && GetAvailableClashSeriesSkills(request).Count > 0)
                    SubmitClashSeriesSkill(request, GetAvailableClashSeriesSkills(request)[0]);
            }
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

            Side declaringSide = exchange.CurrentDeclaration?.DeclaringSide ?? Side.Allies;
            int cycleId = exchange.DeclarationId;
            if (!_boundStateMachine.TryFinalizeApplyOutcome(_terminalPolicy, version))
                return false;
            _bpCurrentAttackCycleHadSuccessfulExchange = true;
            if (_boundStateMachine.Phase == Phase.ChainDecision &&
                exchange.CurrentAttackSide != declaringSide)
                _session.TryRecordCompletedAttackCycle(declaringSide, cycleId, true);
            if (_boundStateMachine.Phase == Phase.ChainDecision &&
                exchange.CurrentAttackSide != declaringSide)
            {
                _session.TryRecordCompletedBpAttackCycle(declaringSide, cycleId, true);
                _bpCurrentAttackCycleHadSuccessfulExchange = false;
            }
            return true;
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
                exchange.CurrentMentalResults,
                exchange.CurrentAftermathSnapshot,
                exchange.CurrentAftermathDecision,
                () => CompleteOutcomePresentation(bindingId, version),
                exchange.ClashSeries != null);
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
                _boundStateMachine.Phase,
                kind == CombatExchangeDecisionKind.Response ? CurrentDeclarationToken : null,
                kind == CombatExchangeDecisionKind.Response ? _session.ExchangeState.CurrentDeclaration : null);
            _pendingDecisionRequest = request;
            if (IsAllOutSelection)
            {
                _allOutDecisionRemaining = allOutDecisionDurationSeconds > 0f &&
                    !float.IsNaN(allOutDecisionDurationSeconds) && !float.IsInfinity(allOutDecisionDurationSeconds)
                    ? allOutDecisionDurationSeconds : 10f;
                if (AllOutUnavailableReason != null)
                    Debug.LogWarning($"[CombatFlowOrchestrator] All-Out unavailable: {AllOutUnavailableReason}", this);
            }
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
            _pendingDecisionRequest = null;
            _allOutDecisionRemaining = 0f;
            RaiseChainDecisionAccepted();
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

        private void RaiseAllOutPresentation(CombatSkillExecutionResult result)
        {
            Action<CombatSkillExecutionResult> handlers = AllOutPresentationRequested;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatSkillExecutionResult>)invocationList[i]).Invoke(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void RaiseOvercomePresentation(CombatOvercomeResult result)
        {
            Action<CombatOvercomeResult> handlers = OvercomePresentationRequested;
            if (result == null || !result.WasAccepted || handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatOvercomeResult>)invocationList[i]).Invoke(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void RaiseFeintPresentation(CombatFeintPresentationRequest request)
        {
            Action<CombatFeintPresentationRequest> handlers = FeintPresentationRequested;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatFeintPresentationRequest>)invocationList[i]).Invoke(request);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void RaiseChainDecisionAccepted()
        {
            Action handlers = ChainDecisionAccepted;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action)invocationList[i]).Invoke();
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

        private void TraceClashBlocked(string boundary, CombatClashSeriesState series)
        {
#if UNITY_EDITOR
            string key = $"{boundary}:{_session?.ExchangeState?.Version}:{series?.CommandToken}:{series?.ComparisonCount}";
            if (_lastClashBlockedDiagnostic == key)
                return;

            _lastClashBlockedDiagnostic = key;
            TraceClash(boundary);
#endif
        }

        private void TraceClash(string boundary)
        {
#if UNITY_EDITOR
            if (!_seriesValidationConfig.HasValue || _session == null)
                return;

            CombatExchangeState exchange = _session.ExchangeState;
            CombatClashSeriesState series = exchange?.ClashSeries;
            Debug.Log($"[ClashTrace/Flow] {boundary} session={_session.CompletionId} " +
                $"exchange={exchange?.DeclarationId}/{exchange?.Version} round={series?.ComparisonCount} " +
                $"token={series?.CommandToken} phase={_boundStateMachine?.Phase} " +
                $"gameState={GameStateMachine.Instance?.Current.ToString() ?? "<none>"} " +
                $"timeScale={Time.timeScale:0.###} pending={_pendingRoundPresentationRequest?.GetHashCode()} " +
                $"lastPresented={_lastPresentedRound} decision={_pendingDecisionRequest?.Kind}", this);
#endif
        }

        private void ResetFinalExchangeBinding()
        {
            CombatSession previousSession = _session;
            if (_boundStateMachine != null && (_isExecutingItem || _boundStateMachine.IsExecutingEffect))
                _boundStateMachine.ForceExit(CombatEndReason.Abort);
            if (_boundStateMachine != null)
                _boundStateMachine.OnEnemyActionRequired -= HandleEnemyActionRequired;

            if (previousSession != null)
            {
                Action<CombatSession> handlers = FinalExchangeUnbound;
                if (handlers != null)
                {
                    Delegate[] invocationList = handlers.GetInvocationList();
                    for (int i = 0; i < invocationList.Length; i++)
                    {
                        try
                        {
                            ((Action<CombatSession>)invocationList[i]).Invoke(previousSession);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, this);
                        }
                    }
                }
            }

            _bindingId++;
            _boundStateMachine = null;
            _randomSource = null;
            _clashInputProvider = null;
            _pendingDecisionRequest = null;
            _pendingApproachRequest = null;
            _pendingOutcomePresentationRequest = null;
            _pendingRoundPresentationRequest = null;
            _seriesValidationConfig = null;
            _seriesFinalSkillSelector = null;
            _seriesTimeoutAttackSelector = null;
            _seriesDecisionDuration = 0f;
            _seriesDecisionRemaining = 0f;
            _allOutDecisionRemaining = 0f;
            _seriesAttackSkill = null;
            _seriesAttackCardRuntimeId = null;
            _seriesAttackBpInvestment = 0;
            _bpCurrentAttackCycleHadSuccessfulExchange = false;
            _lastPresentedRound = 0;
#if UNITY_EDITOR
            _lastClashBlockedDiagnostic = null;
#endif
            _raisedFeintCues.Clear();
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

        private ICombatant FindEnemy(CombatantId enemyCombatantId)
        {
            for (int i = 0; i < _session.Enemies.Count; i++)
            {
                ICombatant enemy = _session.Enemies[i];
                if (enemy != null && enemy.Id.Equals(enemyCombatantId))
                    return enemy;
            }

            return null;
        }

        private readonly struct CombatFeintCueKey : IEquatable<CombatFeintCueKey>
        {
            private readonly string _sessionId;
            private readonly int _exchangeVersion;
            private readonly CombatantId _enemyCombatantId;

            public CombatFeintCueKey(string sessionId, int exchangeVersion, CombatantId enemyCombatantId)
            {
                _sessionId = sessionId;
                _exchangeVersion = exchangeVersion;
                _enemyCombatantId = enemyCombatantId;
            }

            public bool Equals(CombatFeintCueKey other)
            {
                return _sessionId == other._sessionId &&
                       _exchangeVersion == other._exchangeVersion &&
                       _enemyCombatantId.Equals(other._enemyCombatantId);
            }

            public override bool Equals(object obj)
            {
                return obj is CombatFeintCueKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_sessionId, _exchangeVersion, _enemyCombatantId);
            }
        }

        private bool CanAcceptCombatItemCommand(int expectedExchangeVersion)
        {
            return !_isExecutingItem && IsFinalExchangeBound() &&
                   GameStateMachine.Instance?.Is(GameState.Paused) != true && Time.timeScale > 0f &&
                   _pendingDecisionRequest != null &&
                   _pendingDecisionRequest.Kind == CombatExchangeDecisionKind.Attack &&
                   _pendingDecisionRequest.ActingSide == Side.Allies &&
                   _pendingDecisionRequest.ExchangeVersion == expectedExchangeVersion &&
                   _boundStateMachine.Phase == Phase.Standoff &&
                   _session.ExchangeState.CurrentAttackSide == Side.Allies &&
                   _session.ExchangeState.Version == expectedExchangeVersion;
        }

        private bool CanUseCombatItemUser(ICombatant user)
        {
            return user != null && _session != null &&
                   _session.TryGetCombatState(user, out CombatantCombatState state) &&
                   state.IsAlive && !user.IsStunned && !state.IsPanicked;
        }

        private static Side Opposite(Side side)
        {
            return side == Side.Allies ? Side.Enemies : Side.Allies;
        }

        private void InvokeDecisionHandlers(
            Action<CombatExchangeDecisionRequest> handlers,
            CombatExchangeDecisionRequest request)
        {
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                // A synchronous response/feint may supersede this request before later UI subscribers run.
                if (!ReferenceEquals(_pendingDecisionRequest, request))
                    break;
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
