using System;
using System.Collections.Generic;
using Game.Combat.Model;
using UnityEngine;

namespace Game.Combat.Core
{
    public sealed class CombatStateMachine
    {
        public Phase Phase { get; private set; } = Phase.EnterCombat;
        public CombatEndReason EndReason { get; private set; } = CombatEndReason.None;

        private readonly CombatSession _session;
        private CombatTurn _presentingTurn;
        private int _presentingTurnIndex = -1;
        private bool _presentationRequested;
        private bool _approachPresentationRequested;
        private bool _endTurnProcessed;
        private bool _exited;
        private bool _isExecutingAllOut;
        private bool _isReturningToStandoff;
        private bool _isExecutingSkill;
        private bool _isExecutingItem;

        internal bool IsExecutingEffect => _isExecutingAllOut || _isExecutingSkill;

        internal bool TryBeginItemExecution(int expectedExchangeVersion)
        {
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) || Phase != Phase.Standoff)
                return false;
            _isExecutingItem = true;
            return true;
        }

        internal void EndItemExecution() => _isExecutingItem = false;

        public event Action<CombatSession, Action> OnRequireResolutionPlay;
        public event Action<CombatAttackDeclaration, Action> OnRequireApproachPlay;
        public event Action<Phase, Phase> OnPhaseChanged;
        public event Action<CombatSession> OnEnemyActionRequired;

        public CombatStateMachine(CombatSession session)
        {
            _session = session;
            InitializeRuntimeState();
        }

        public CombatStateMachine(CombatSession session, object legacyArg1, object legacyArg2)
        {
            _session = session;
            InitializeRuntimeState();
        }

        internal bool OwnsSession(CombatSession session)
        {
            return ReferenceEquals(_session, session);
        }

        private void InitializeRuntimeState()
        {
            // The bootstrapper finalizes the roster before constructing the state machine.
            // Keep runtime-state availability independent from the first Planning/Turn transition.
            _session?.InitializeCombatStates(_session.RuntimeConfig);
        }

        public bool ConfirmPlanning()
        {
            if (_exited || Phase != Phase.Planning || _session?.CurrentTurn == null)
                return false;

            if (_session.CurrentTurn.Lifecycle != CombatTurnLifecycle.Resolved)
            {
                Debug.LogWarning(
                    $"[CombatStateMachine] Resolution entry rejected. Turn={_session.TurnIndex}, " +
                    $"Lifecycle={_session.CurrentTurn.Lifecycle}.");
                return false;
            }

            _presentationRequested = false;
            _endTurnProcessed = false;
            _presentingTurn = null;
            _presentingTurnIndex = -1;
            SetPhase(Phase.Resolution);
            return true;
        }

        public bool EnterStandoff()
        {
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain)
                return false;

            if (Phase == Phase.Standoff)
                return true;

            if (Phase != Phase.EnterCombat || CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
                return false;

            _session.InitializeCombatStates(_session.RuntimeConfig);
            if (_session.ExchangeState == null || _session.CombatStateCount != CountRosterCombatants())
                return false;

            _session.ExchangeState.ResetForStandoff();
            _approachPresentationRequested = false;
            _session.StandoffState.Reset();
            SetPhase(Phase.Standoff);
            return true;
        }

        public bool TryDeclareAttack(ICombatant attacker, ICombatant target, ISkill skill)
        {
            return TryDeclareAttack(new CombatAttackDeclaration(attacker, target, skill));
        }

        public bool TryDeclareAttack(CombatAttackDeclaration declaration)
        {
            if (!CanAcceptDeclaration(declaration))
                return false;

            _session.ExchangeState.CommitDeclaration(declaration);
            _session.StandoffState.Reset();
            SetPhase(Phase.AttackDeclaration);
            return true;
        }

        public bool TryDeclareAttack(
            CombatAttackDeclaration declaration,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            bool isInitialDeclaration = Phase == Phase.Standoff;
            bool isChainDeclaration = Phase == Phase.AttackDeclaration &&
                                      exchange?.IsChainActive == true &&
                                      exchange.CurrentDeclaration == null;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                (!isInitialDeclaration && !isChainDeclaration) ||
                declaration?.Attacker == null || declaration.Target == null || declaration.Skill == null ||
                declaration.Attacker.Side != exchange.CurrentAttackSide ||
                (isChainDeclaration && !ReferenceEquals(declaration.Attacker, exchange.CurrentAttackActor)) ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, declaration))
            {
                return false;
            }

            CombatantCombatState attackerState = _session.GetCombatState(declaration.Attacker);
            if (!attackerState.CanSpendMp(CombatMpCostResolver.Resolve(declaration.Skill)))
                return false;

            exchange.CommitDeclaration(declaration);
            if (isInitialDeclaration)
                _session.StandoffState.Reset();
            SetPhase(Phase.AttackDeclaration);
            return true;
        }

        public bool TrySubmitEnemyDecision(IEnemyCombatDecisionPolicy decisionPolicy)
        {
            if (decisionPolicy == null || _exited || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.Standoff || !_session.StandoffState.IsPressureReady)
            {
                return false;
            }

            CombatAttackDeclaration declaration;
            try
            {
                if (!decisionPolicy.TryCreateDeclaration(
                        new EnemyCombatDecisionRequest(_session),
                        out declaration))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

            if (declaration?.Attacker == null || declaration.Attacker.Side != Side.Enemies)
                return false;

            return TryDeclareAttack(declaration);
        }

        /// <summary>
        /// Replaces only an initial enemy declaration whose response is still pending.
        /// All legality checks precede mutation; the existing commit remains the sole MP payer.
        /// </summary>
        public bool TryReplaceEnemyDeclaration(CombatDeclarationToken token, ISkill replacementSkill)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration original = exchange?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(token.ExchangeVersion) ||
                token.SessionId != _session.CompletionId ||
                token.DeclarationId != exchange.DeclarationId || token.Revision != exchange.DeclarationRevision ||
                Phase != Phase.AttackDeclaration || exchange.IsCommitted || exchange.IsChainActive ||
                exchange.HasReplacedDeclaration || exchange.ResponseState != CombatResponseState.Pending ||
                original?.Attacker?.Side != Side.Enemies || original.Skill == null ||
                original.Skill.Tag != SkillTag.Attack || replacementSkill == null ||
                replacementSkill.Tag != SkillTag.Attack || original.Skill.Id.Value == replacementSkill.Id.Value ||
                !_session.TryGetEnemyBehavior(original.Attacker, out EnemyCombatBehaviorSnapshot behavior) ||
                !behavior.DeclarationFeintEnabled ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, original))
            {
                return false;
            }

            CombatAttackDeclaration replacement = new(original.Attacker, original.Target, replacementSkill);
            if (!CombatDeclarationPolicy.CanDeclareAttack(_session, replacement) ||
                (_session.TryGetHand(original.Attacker, out CombatHandRuntime hand) &&
                 !hand.TryFindInHand(replacementSkill, CombatCardKind.Normal, null, out _)) ||
                !_session.GetCombatState(original.Attacker).CanSpendMp(CombatMpCostResolver.Resolve(replacementSkill)))
            {
                return false;
            }

            exchange.ReplaceDeclaration(replacement);
            return true;
        }

        public bool TryDeclareResponse(ICombatant responder, ISkill skill)
        {
            return TryDeclareResponse(new CombatResponseDeclaration(responder, skill));
        }

        public bool TryDeclareResponse(CombatResponseDeclaration response)
        {
            if (!CanAcceptResponse(response))
                return false;

            _session.ExchangeState.CommitResponse(response);
            return true;
        }

        public bool TryDeclareResponse(
            CombatResponseDeclaration response,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration attack = exchange?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                !CanEditResponse() ||
                !CombatDeclarationPolicy.CanDeclareResponse(_session, attack, response))
            {
                return false;
            }

            exchange.CommitResponse(response);
            return true;
        }

        public bool ConfirmNoResponse()
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!CanEditResponse() || !CanUseDeclarationParticipants(declaration))
                return false;

            _session.ExchangeState.ConfirmNoResponse();
            return true;
        }

        public bool ConfirmNoResponse(int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                !CanEditResponse() ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, declaration))
            {
                return false;
            }

            exchange.ConfirmNoResponse();
            return true;
        }

        public bool TryCancelAttackDeclaration()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.AttackDeclaration || exchange?.CurrentDeclaration == null ||
                exchange.IsCommitted || CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            exchange.ClearDeclaration();
            _session.StandoffState.Reset();
            SetPhase(Phase.Standoff);
            return true;
        }

        public bool TryCommitExchange()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.AttackDeclaration || exchange == null || exchange.IsCommitted ||
                exchange.ResponseState == CombatResponseState.Pending ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None ||
                !CanUseDeclaration(declaration))
            {
                return false;
            }

            if (!CombatOutcomeTargetResolver.CanResolve(
                    declaration.Attacker,
                    declaration.Skill,
                    declaration.Target,
                    _session))
            {
                return false;
            }

            CombatResponseDeclaration response = null;

            if (exchange.ResponseState == CombatResponseState.CounterDeclared)
            {
                response = exchange.CurrentResponse;
                if (!CanUseResponse(declaration, response) ||
                    !CombatOutcomeTargetResolver.CanResolve(
                        response.Responder,
                        response.Skill,
                        declaration.Attacker,
                        _session))
                {
                    return false;
                }
            }
            else if (exchange.ResponseState != CombatResponseState.NoResponse)
            {
                return false;
            }

            CombatantCombatState attackerState = _session.GetCombatState(declaration.Attacker);
            CombatantCombatState responderState = response != null
                ? _session.GetCombatState(response.Responder)
                : null;
            int attackMpCost = CombatMpCostResolver.Resolve(declaration.Skill);
            int responseMpCost = response != null
                ? CombatMpCostResolver.Resolve(response.Skill)
                : 0;

            if (!attackerState.CanSpendMp(attackMpCost) ||
                (responderState != null && !responderState.CanSpendMp(responseMpCost)))
            {
                return false;
            }

            // Affordability for every payer is established before either state is mutated.
            if (!attackerState.TrySpendMp(attackMpCost))
                return false;

            if (responderState != null && !responderState.TrySpendMp(responseMpCost))
            {
                attackerState.RestoreMp(attackMpCost);
                return false;
            }

            exchange.MarkCommitted(attackMpCost, responseMpCost);
            return true;
        }

        public bool TryCommitExchange(int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.AttackDeclaration || exchange.IsCommitted ||
                exchange.ResponseState == CombatResponseState.Pending ||
                declaration == null)
            {
                return false;
            }

            CombatResponseDeclaration requestedResponse =
                exchange.ResponseState == CombatResponseState.CounterDeclared
                    ? exchange.CurrentResponse
                    : null;
            if (!CombatExchangeCommitPolicy.TryCommit(
                    _session,
                    declaration,
                    requestedResponse,
                    out CombatExchangeCommitResult result))
            {
                return false;
            }

            exchange.StoreCommitResult(result);
            return true;
        }

        public bool TryBeginApproach()
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!CanExecuteDeclaration(declaration))
                return false;

            _approachPresentationRequested = false;
            SetPhase(Phase.Approach);
            return true;
        }

        public bool TryBeginApproach(int expectedExchangeVersion)
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                !CanExecuteDeclaration(declaration) ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, declaration))
            {
                return false;
            }

            _approachPresentationRequested = false;
            SetPhase(Phase.Approach);
            return true;
        }

        public bool CompleteApproach()
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (_exited || Phase != Phase.Approach || !CanUseDeclarationParticipants(declaration) ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            SetPhase(Phase.Clash);
            return true;
        }

        public bool CompleteApproach(int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.Approach || exchange == null || !exchange.IsCommitted ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, declaration))
            {
                return false;
            }

            if (exchange.ResponseState == CombatResponseState.NoResponse)
            {
                CombatClashRequest request = new CombatClashRequest(
                    declaration,
                    CombatResponseState.NoResponse,
                    null);
                if (!CombatClashResolver.TryResolve(request, null, out CombatClashResult result))
                    return false;

                exchange.StoreClashResult(result);
                SetPhase(Phase.ApplyOutcome);
                return true;
            }

            if (exchange.ResponseState != CombatResponseState.CounterDeclared ||
                !CombatDeclarationPolicy.CanDeclareResponse(
                    _session,
                    declaration,
                    exchange.CurrentResponse))
            {
                return false;
            }

            SetPhase(Phase.Clash);
            return true;
        }

        public bool TryResolveClash(ICombatClashRule clashRule = null)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.Clash || exchange == null || !exchange.IsCommitted ||
                exchange.ClashSeries != null ||
                exchange.CurrentClashResult != null || exchange.ResponseState == CombatResponseState.Pending ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None ||
                !CanUseDeclaration(declaration))
            {
                return false;
            }

            if (exchange.ResponseState == CombatResponseState.CounterDeclared &&
                !CanUseResponse(declaration, exchange.CurrentResponse))
            {
                return false;
            }

            CombatClashRequest request = new CombatClashRequest(
                declaration,
                exchange.ResponseState,
                exchange.CurrentResponse);
            if (!CombatClashResolver.TryResolve(request, clashRule, out CombatClashResult result))
                return false;

            exchange.StoreClashResult(result);
            SetPhase(Phase.ApplyOutcome);
            return true;
        }

        public bool TryResolveClash(
            FinalCombatClashRule clashRule,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            CombatResponseDeclaration response = exchange?.CurrentResponse;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.Clash || exchange == null || !exchange.IsCommitted ||
                exchange.ClashSeries != null ||
                exchange.CurrentClashResult != null || clashRule == null ||
                exchange.ResponseState != CombatResponseState.CounterDeclared ||
                !CombatDeclarationPolicy.CanDeclareAttack(_session, declaration) ||
                !CombatDeclarationPolicy.CanDeclareResponse(_session, declaration, response))
            {
                return false;
            }

            CombatClashRequest request = new CombatClashRequest(
                declaration,
                CombatResponseState.CounterDeclared,
                response);
            if (!CombatClashResolver.TryResolve(request, clashRule, out CombatClashResult result))
                return false;

            exchange.StoreClashResult(result);
            SetPhase(Phase.ApplyOutcome);
            return true;
        }

        public bool TryCompleteClashSeries(
            CombatClashSeriesState series,
            long expectedToken,
            ISkill finalSkill,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) || Phase != Phase.Clash ||
                exchange == null || !ReferenceEquals(exchange.ClashSeries, series) ||
                !series.CanClaimFinalAttack || CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None ||
                !series.TryClaimFinalOutcome(expectedToken, finalSkill, out CombatClashResult result) ||
                !exchange.StoreClashSeriesResult(series, result))
                return false;

            SetPhase(Phase.ApplyOutcome);
            return true;
        }

        public bool TryEndClashSeriesDraw(
            CombatClashSeriesState series,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) || Phase != Phase.Clash ||
                exchange == null || !ReferenceEquals(exchange.ClashSeries, series) ||
                !series.IsComplete || series.FinalOutcome != CombatClashOutcome.Tie)
                return false;

            exchange.ResetForStandoff();
            _session.StandoffState.Reset();
            SetPhase(Phase.Standoff);
            return true;
        }

        public bool TryPrepareOutcome()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatClashResult result = exchange?.CurrentClashResult;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || result == null ||
                exchange.IsOutcomePrepared || CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None ||
                !IsCurrentClashResultConsistent(exchange, result))
            {
                return false;
            }

            if (!CombatOutcomeSelector.TrySelect(result, out CombatOutcomeAction action))
                return false;

            exchange.StorePreparedOutcome(action);
            return true;
        }

        public bool TryPrepareOutcome(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryPrepareOutcome();
        }

        public bool TryPrepareSkillExecution()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _isExecutingSkill || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || !exchange.IsOutcomePrepared ||
                exchange.IsExecutionPrepared || CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            CombatOutcomeAction action = exchange.CurrentOutcomeAction;
            if (action == null)
            {
                if (exchange.CurrentClashResult?.Outcome != CombatClashOutcome.Tie)
                    return false;

                exchange.StorePreparedExecution(null);
                return true;
            }

            if (!CombatOutcomeTargetResolver.TryResolve(
                    action,
                    _session,
                    out CombatSkillExecutionRequest request))
            {
                return false;
            }

            exchange.StorePreparedExecution(request);
            return true;
        }

        public bool TryPrepareSkillExecution(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryPrepareSkillExecution();
        }

        public bool TryExecutePreparedSkill()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _isExecutingSkill || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || !exchange.IsOutcomePrepared ||
                !exchange.IsExecutionPrepared || exchange.IsExecutionCompleted ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            CombatSkillExecutionRequest request = exchange.CurrentExecutionRequest;
            if (request == null)
            {
                if (exchange.CurrentClashResult?.Outcome != CombatClashOutcome.Tie)
                    return false;

                exchange.StoreCompletedExecution(null);
                return true;
            }

            _isExecutingSkill = true;
            try
            {
                if (!Game.Combat.Actions.SkillRunner.TryExecute(_session, request, out CombatSkillExecutionResult result))
                    return false;
                if (!_exited) exchange.StoreCompletedExecution(result);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EnterExit(CombatEndReason.Abort);
                return false;
            }
            finally { _isExecutingSkill = false; }
        }

        public bool TryExecutePreparedSkill(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryExecutePreparedSkill();
        }

        public bool TryResolvePosture(ICombatPostureRule postureRule = null)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatClashResult clash = exchange?.CurrentClashResult;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || clash == null ||
                !exchange.IsExecutionCompleted ||
                exchange.PostureResolutionState != CombatPostureResolutionState.Pending)
            {
                return false;
            }

            if (clash.Outcome == CombatClashOutcome.Unopposed)
            {
                exchange.StorePostureResolution(CombatPostureResolutionState.NotApplicable, null);
                return true;
            }

            if (clash.Outcome == CombatClashOutcome.Tie)
            {
                exchange.StorePostureResolution(CombatPostureResolutionState.PolicyRequired, null);
                return true;
            }

            if (!TryGetPostureParticipants(
                    clash,
                    out ICombatant winner,
                    out ICombatant loser,
                    out ISkill winningSkill) ||
                !_session.TryGetCombatState(loser, out CombatantCombatState loserState))
            {
                return false;
            }

            if (loser.HP <= 0)
            {
                exchange.StorePostureResolution(CombatPostureResolutionState.NotApplicable, null);
                return true;
            }

            if (postureRule == null || exchange.CurrentExecutionResult == null)
                return false;

            int postureBefore = loserState.CurrentPosture;
            CombatPostureRequest request = new CombatPostureRequest(
                winner,
                loser,
                winningSkill,
                clash.Outcome,
                exchange.CurrentExecutionResult,
                postureBefore,
                loserState.MaxPosture);

            int postureDelta;
            try
            {
                postureDelta = postureRule is FinalCombatPostureRule finalPostureRule
                    ? Math.Max(0, finalPostureRule.ResolvePostureDelta(request, _session))
                    : Math.Max(0, postureRule.ResolvePostureDelta(request));
            }
            catch (Exception)
            {
                return false;
            }

            loserState.AddPosture(postureDelta);
            CombatPostureResult result = new CombatPostureResult(
                loser,
                postureBefore,
                loserState.CurrentPosture,
                loserState.IsPostureMax);
            exchange.StorePostureResolution(CombatPostureResolutionState.Applied, result);
            return true;
        }

        public bool TryResolvePosture(
            ICombatPostureRule postureRule,
            int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) &&
                   TryResolvePosture(postureRule);
        }

        public bool TryResolveStun()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null ||
                exchange.PostureResolutionState == CombatPostureResolutionState.Pending ||
                exchange.StunResolutionState != CombatStunResolutionState.Pending)
            {
                return false;
            }

            if (exchange.PostureResolutionState == CombatPostureResolutionState.PolicyRequired)
            {
                exchange.StoreStunResolution(CombatStunResolutionState.PolicyRequired, null);
                return true;
            }

            if (exchange.PostureResolutionState == CombatPostureResolutionState.NotApplicable)
            {
                exchange.StoreStunResolution(CombatStunResolutionState.NotApplicable, null);
                return true;
            }

            CombatPostureResult postureResult = exchange.CurrentPostureResult;
            if (postureResult == null)
                return false;

            if (!postureResult.ReachedMaximum)
            {
                exchange.StoreStunResolution(CombatStunResolutionState.NotApplicable, null);
                return true;
            }

            ICombatant target = postureResult.Target;
            if (target == null || target.HP <= 0 ||
                !_session.TryGetCombatState(target, out _) || !IsCurrentRosterMember(target))
            {
                exchange.StoreStunResolution(CombatStunResolutionState.NotApplicable, null);
                return true;
            }

            bool wasStunnedBefore = target.IsStunned;
            if (!wasStunnedBefore)
                target.SetStunned(true);

            bool isStunnedAfter = target.IsStunned;
            if (!isStunnedAfter)
                return false;

            exchange.StoreStunResolution(
                CombatStunResolutionState.Applied,
                new CombatStunResult(target, wasStunnedBefore, isStunnedAfter));
            return true;
        }

        public bool TryResolveStun(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryResolveStun();
        }

        public bool TryPrepareAftermath()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || exchange.CurrentClashResult == null ||
                !exchange.IsExecutionCompleted ||
                exchange.PostureResolutionState == CombatPostureResolutionState.Pending ||
                exchange.StunResolutionState == CombatStunResolutionState.Pending ||
                exchange.IsAftermathPrepared)
            {
                return false;
            }

            if (!exchange.IsMentalResolved && !TryResolveMental())
                return false;

            exchange.StoreAftermath(new CombatAftermathSnapshot(_session, exchange));
            return exchange.IsAftermathPrepared;
        }

        internal bool TryResolveMental()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || exchange.CurrentClashResult == null ||
                !exchange.IsExecutionCompleted ||
                exchange.PostureResolutionState == CombatPostureResolutionState.Pending ||
                exchange.StunResolutionState == CombatStunResolutionState.Pending ||
                exchange.IsMentalResolved)
            {
                return false;
            }

            if (!TryApplyMentalConsequences(
                    exchange.CurrentClashResult,
                    exchange.CurrentExecutionResult,
                    exchange.CurrentStunResult,
                    out System.Collections.Generic.IReadOnlyList<CombatMentalMutationResult> results))
            {
                return false;
            }

            exchange.StoreMentalResults(results);
            return true;
        }

        internal bool TryResolveMental(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryResolveMental();
        }

        private bool TryApplyMentalConsequences(
            CombatClashResult clash,
            CombatSkillExecutionResult execution,
            CombatStunResult stun,
            out System.Collections.Generic.IReadOnlyList<CombatMentalMutationResult> results)
        {
            results = System.Array.Empty<CombatMentalMutationResult>();
            if (_session == null)
                return false;

            System.Collections.Generic.IReadOnlyList<FinalCombatMentalRule.CombatMentalDelta> deltas =
                new FinalCombatMentalRule(_session.RuntimeConfig).Calculate(clash, execution, stun);
            System.Collections.Generic.List<CombatantCombatState> states =
                new System.Collections.Generic.List<CombatantCombatState>(deltas.Count);
            for (int i = 0; i < deltas.Count; i++)
            {
                if (deltas[i].Target == null ||
                    !_session.TryGetCombatState(deltas[i].Target, out CombatantCombatState state))
                {
                    return false;
                }

                states.Add(state);
            }

            System.Collections.Generic.List<CombatMentalMutationResult> applied =
                new System.Collections.Generic.List<CombatMentalMutationResult>(deltas.Count);
            for (int i = 0; i < deltas.Count; i++)
                applied.Add(states[i].ApplyMentalDelta(deltas[i].RequestedDelta));

            results = applied.AsReadOnly();
            return true;
        }

        public bool TryPrepareAftermath(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) && TryPrepareAftermath();
        }

        public bool TryPrepareAftermathDecision()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || EndReason != CombatEndReason.None || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null ||
                !exchange.IsAftermathPrepared || exchange.CurrentAftermathSnapshot == null ||
                exchange.IsAftermathDecisionPrepared)
            {
                return false;
            }

            CombatAftermathDecision decision =
                CombatAftermathDecisionResolver.Resolve(exchange.CurrentAftermathSnapshot);
            exchange.StoreAftermathDecision(decision);
            return exchange.IsAftermathDecisionPrepared;
        }

        public bool TryPrepareAftermathDecision(int expectedExchangeVersion)
        {
            return HasExpectedExchangeVersion(expectedExchangeVersion) &&
                   TryPrepareAftermathDecision();
        }

        public bool TryEnterChainDecision()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || EndReason != CombatEndReason.None || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null ||
                !exchange.IsAftermathDecisionPrepared ||
                exchange.CurrentAftermathDecision?.Kind !=
                    CombatAftermathDecisionKind.ChainDecisionRequired)
            {
                return false;
            }

            SetPhase(Phase.ChainDecision);
            return true;
        }

        public bool TryPrepareTerminalDecision(ICombatTerminalPolicy policy)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAftermathDecision aftermathDecision = exchange?.CurrentAftermathDecision;
            CombatAftermathSnapshot snapshot = exchange?.CurrentAftermathSnapshot;
            if (_exited || EndReason != CombatEndReason.None || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null || policy == null ||
                !exchange.IsAftermathDecisionPrepared ||
                aftermathDecision?.Kind != CombatAftermathDecisionKind.TerminalCandidate ||
                snapshot == null || exchange.IsTerminalDecisionPrepared)
            {
                return false;
            }

            CombatTerminalDecision terminalDecision;
            try
            {
                if (!policy.TryResolve(
                        aftermathDecision.TerminalCandidate,
                        snapshot,
                        out terminalDecision))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (!IsValidTerminalDecision(aftermathDecision.TerminalCandidate, terminalDecision))
                return false;

            exchange.StoreTerminalDecision(terminalDecision);
            return exchange.IsTerminalDecisionPrepared;
        }

        public bool TryExecutePreparedTerminalDecision()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAftermathDecision aftermathDecision = exchange?.CurrentAftermathDecision;
            CombatTerminalDecision terminalDecision = exchange?.CurrentTerminalDecision;
            if (_exited || EndReason != CombatEndReason.None || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ApplyOutcome || exchange == null ||
                !exchange.IsAftermathDecisionPrepared ||
                aftermathDecision?.Kind != CombatAftermathDecisionKind.TerminalCandidate ||
                !exchange.IsTerminalDecisionPrepared ||
                !IsValidTerminalDecision(aftermathDecision.TerminalCandidate, terminalDecision))
            {
                return false;
            }

            AdvanceStatusDurationsForCompletedExchange(exchange);
            EnterExit(terminalDecision.EndReason);
            return Phase == Phase.ExitCombat && EndReason == terminalDecision.EndReason;
        }

        public bool TryFinalizeApplyOutcome(
            FinalCombatTerminalPolicy terminalPolicy,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAftermathDecision aftermathDecision = exchange?.CurrentAftermathDecision;
            CombatAftermathSnapshot snapshot = exchange?.CurrentAftermathSnapshot;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.ApplyOutcome || exchange.IsApplyOutcomeFinalized ||
                !exchange.IsMentalResolved || !exchange.IsAftermathDecisionPrepared || aftermathDecision == null || snapshot == null ||
                aftermathDecision.Kind == CombatAftermathDecisionKind.TiePolicyRequired ||
                !CombatAttackAuthorityPolicy.TryResolve(
                    exchange.CurrentClashResult,
                    out CombatAttackAuthorityPolicy.Result authority))
            {
                return false;
            }

            // Presentation may have held this exchange while the roster or HP changed.
            snapshot = new CombatAftermathSnapshot(_session, exchange);
            aftermathDecision = CombatAftermathDecisionResolver.Resolve(snapshot);
            if (aftermathDecision.Kind == CombatAftermathDecisionKind.TiePolicyRequired)
                return false;

            CombatTerminalDecision terminalDecision = null;
            bool isTerminal = aftermathDecision.Kind == CombatAftermathDecisionKind.TerminalCandidate;
            if (isTerminal)
            {
                if (terminalPolicy == null)
                    return false;

                try
                {
                    if (!terminalPolicy.TryResolve(
                            aftermathDecision.TerminalCandidate,
                            snapshot,
                            out terminalDecision))
                    {
                        return false;
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                if (!IsValidTerminalDecision(aftermathDecision.TerminalCandidate, terminalDecision))
                    return false;
            }
            else if (aftermathDecision.Kind != CombatAftermathDecisionKind.ChainDecisionRequired &&
                     aftermathDecision.Kind != CombatAftermathDecisionKind.AllOutCandidate)
            {
                return false;
            }

            bool isAllOutCandidate = aftermathDecision.Kind == CombatAftermathDecisionKind.AllOutCandidate &&
                                     CombatAllOutPolicy.IsCandidate(_session);
            exchange.StoreAttackAuthority(authority.Winner);
            AdvanceStatusDurationsForCompletedExchange(exchange);
            exchange.FinalizeApplyOutcome(isAllOutCandidate);

            if (isTerminal)
            {
                exchange.StoreTerminalDecision(terminalDecision);
                EnterExit(terminalDecision.EndReason);
                return Phase == Phase.ExitCombat && EndReason == terminalDecision.EndReason;
            }

            exchange.BeginChainDecision(authority.Winner);
            SetPhase(Phase.ChainDecision);
            return true;
        }

        private void AdvanceStatusDurationsForCompletedExchange(CombatExchangeState exchange)
        {
            if (exchange == null || exchange.AreStatusDurationsAdvanced)
                return;

            CombatAttackDeclaration declaration = exchange.CurrentDeclaration;
            if (declaration?.Attacker == null || declaration.Target == null)
                return;

            Dictionary<ICombatant, HashSet<string>> newlyChangedStatuses =
                new Dictionary<ICombatant, HashSet<string>>();
            IReadOnlyList<CombatStatusApplicationResult> appliedStatuses =
                exchange.CurrentExecutionResult?.AppliedStatusResults;
            if (appliedStatuses != null)
            {
                for (int i = 0; i < appliedStatuses.Count; i++)
                {
                    CombatStatusApplicationResult applied = appliedStatuses[i];
                    if (applied?.Changed != true || applied.Target == null ||
                        applied.Status == null || string.IsNullOrEmpty(applied.Status.StatusId))
                    {
                        continue;
                    }

                    if (!newlyChangedStatuses.TryGetValue(applied.Target, out HashSet<string> ids))
                    {
                        ids = new HashSet<string>(StringComparer.Ordinal);
                        newlyChangedStatuses.Add(applied.Target, ids);
                    }

                    ids.Add(applied.Status.StatusId);
                }
            }

            HashSet<ICombatant> participants = new HashSet<ICombatant>();
            participants.Add(declaration.Attacker);
            participants.Add(declaration.Target);
            if (exchange.CurrentResponse?.Responder != null)
                participants.Add(exchange.CurrentResponse.Responder);

            foreach (ICombatant participant in participants)
            {
                if (_session.TryGetCombatState(participant, out CombatantCombatState state))
                {
                    newlyChangedStatuses.TryGetValue(participant, out HashSet<string> exclusions);
                    state.AdvanceStatusDurations(exclusions);
                }
            }

            exchange.MarkStatusDurationsAdvanced();
        }

        /// <summary>
        /// Executes a validated All-Out through SkillRunner, then returns to the canonical
        /// terminal policy or the next FinalExchange standoff. It is intentionally only valid
        /// for the current finalized All-Out candidate version.
        /// </summary>
        public bool TryExecuteAllOut(
            int expectedExchangeVersion,
            out CombatSkillExecutionResult executionResult)
        {
            executionResult = null;
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) || _exited ||
                EndReason != CombatEndReason.None || _session == null ||
                _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.ChainDecision || exchange == null ||
                !exchange.IsApplyOutcomeFinalized || !exchange.IsAllOutCandidate ||
                !exchange.IsChainActive || !CombatAllOutExecutionPolicy.TryCreateExecutionRequest(
                    _session,
                    out CombatSkillExecutionRequest request))
            {
                return false;
            }

            // Reserve before SkillRunner: combatant callbacks can synchronously submit commands.
            _isExecutingAllOut = true;
            try
            {
                if (!Game.Combat.Actions.SkillRunner.TryExecute(_session, request, out executionResult))
                    return false;

                if (_exited) return true;

                if (!TryApplyMentalConsequences(null, executionResult, null, out _))
                {
                    EnterExit(CombatEndReason.Abort);
                    return true;
                }

                if (!ResolveCurrentTerminal())
                    ReturnToStandoff();
                return true;
            }
            catch (Exception exception)
            {
                // Partial effects cannot be retried safely. Completion still uses the normal owner.
                Debug.LogException(exception);
                EnterExit(CombatEndReason.Abort);
                return false;
            }
            finally { _isExecutingAllOut = false; }
        }

        internal bool TryRefreshDecisionBoundary(int expectedExchangeVersion)
        {
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                (Phase != Phase.Standoff && Phase != Phase.ChainDecision))
                return false;

            if (ResolveCurrentTerminal())
                return true;

            CombatExchangeState exchange = _session.ExchangeState;
            if (Phase == Phase.ChainDecision)
            {
                if (exchange.IsAllOutCandidate && !CombatAllOutPolicy.IsCandidate(_session))
                    return TryEndChain(expectedExchangeVersion);
                return false;
            }

            // Evaluate before recovery, including encounters that enter with every actor stunned.
            if (CombatAllOutPolicy.IsCandidate(_session))
            {
                ICombatant owner = _session.Allies.Find(ally =>
                    CombatDeclarationPolicy.IsLivingMember(_session, ally));
                exchange.FinalizeApplyOutcome(true);
                exchange.BeginChainDecision(owner);
                SetPhase(Phase.ChainDecision);
                return true;
            }

            IReadOnlyList<ICombatant> actingSide = _session.GetSide(exchange.CurrentAttackSide);
            bool hasLiving = false;
            foreach (ICombatant actor in actingSide)
            {
                if (!CombatDeclarationPolicy.IsLivingMember(_session, actor)) continue;
                hasLiving = true;
                if (!actor.IsStunned) return false;
            }
            if (!hasLiving) return false;
            ReturnToStandoff();
            return true;
        }

        private bool ResolveCurrentTerminal()
        {
            CombatEndReason reason = FinalCombatTerminalPolicy.Evaluate(_session);
            if (reason == CombatEndReason.None) return false;

            bool enemiesLiving = _session.Enemies.Exists(enemy =>
                CombatDeclarationPolicy.IsLivingMember(_session, enemy));
            CombatTerminalCandidate candidate = reason == CombatEndReason.Victory
                ? CombatTerminalCandidate.EnemiesWiped
                : enemiesLiving ? CombatTerminalCandidate.AlliesWiped : CombatTerminalCandidate.BothWiped;
            bool returning = _isReturningToStandoff;
            _isReturningToStandoff = true;
            try
            {
                _session.ExchangeState.StoreTerminalDecision(new CombatTerminalDecision(candidate, reason));
                EnterExit(reason);
            }
            finally { _isReturningToStandoff = returning; }
            return true;
        }

        private void ReturnToStandoff()
        {
            _isReturningToStandoff = true;
            try
            {
                RecoverStunsAtStandoff();
                if (_exited) return;
                _session.ExchangeState.ResetForStandoff();
                _session.StandoffState.Reset();
                _approachPresentationRequested = false;
                SetPhase(Phase.Standoff);
            }
            finally { _isReturningToStandoff = false; }
        }

        private void RecoverStunsAtStandoff()
        {
            foreach (ICombatant actor in _session.Allies)
                RecoverStun(actor);
            foreach (ICombatant actor in _session.Enemies)
                RecoverStun(actor);
        }

        private void RecoverStun(ICombatant actor)
        {
            if (_exited || !CombatDeclarationPolicy.IsLivingMember(_session, actor) || !actor.IsStunned)
                return;
            StaggerSystem.ClearStunAtTurnEnd(actor);
            _session.GetCombatState(actor).SetPosture(0);
        }

        public bool CanExecuteUltimate(ICombatant actor, ICombatant target,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) || _exited ||
                EndReason != CombatEndReason.None ||
                _session?.FlowMode != CombatFlowMode.StandoffClashChain ||
                (Phase != Phase.Standoff && Phase != Phase.ChainDecision) ||
                exchange == null || actor == null ||
                exchange.CurrentAttackSide != actor.Side ||
                Phase == Phase.ChainDecision &&
                (!exchange.IsApplyOutcomeFinalized || !exchange.IsChainActive || exchange.IsAllOutCandidate ||
                 !ReferenceEquals(exchange.ChainOwner, actor)) ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None ||
                !_session.TryGetCombatState(actor, out CombatantCombatState actorState) ||
                !actorState.IsAlive || actor.IsStunned || actorState.IsPanicked ||
                _session.UltimateRuntime?.TryGetStage(actor, out int stage) != true ||
                stage != CombatUltimateRuntime.MaximumStage ||
                !_session.UltimateRuntime.TryGetSkill(actor, out ISkill skill) ||
                !actorState.CanSpendMp(CombatMpCostResolver.Resolve(skill)))
                return false;

            CombatOutcomeAction action = new CombatOutcomeAction(actor, skill, target,
                CombatClashOutcome.AttackerWin);
            return CombatOutcomeTargetResolver.TryResolve(action, _session, out _);
        }

        public bool TryExecuteUltimate(ICombatant actor, ICombatant target,
            int expectedExchangeVersion, out CombatSkillExecutionResult executionResult)
        {
            executionResult = null;
            if (!CanExecuteUltimate(actor, target, expectedExchangeVersion) ||
                !_session.UltimateRuntime.TryGetSkill(actor, out ISkill skill))
                return false;

            CombatOutcomeAction action = new CombatOutcomeAction(actor, skill, target,
                CombatClashOutcome.AttackerWin);
            if (!CombatOutcomeTargetResolver.TryResolve(action, _session,
                    out CombatSkillExecutionRequest request))
                return false;

            CombatantCombatState actorState = _session.GetCombatState(actor);
            int mpCost = CombatMpCostResolver.Resolve(skill);
            _isExecutingSkill = true;
            try
            {
                if (!actorState.TrySpendMp(mpCost)) return false;
                if (!Game.Combat.Actions.SkillRunner.TryExecute(_session, request, out executionResult))
                {
                    actorState.RestoreMp(mpCost);
                    executionResult = null;
                    return false;
                }
                if (_exited) return true;

                _session.UltimateRuntime.TryConsumeReadyStage(actor);
                TryApplyMentalConsequences(null, executionResult, null, out _);
                if (!ResolveCurrentTerminal())
                {
                    CombatExchangeState exchange = _session.ExchangeState;
                    exchange.ResetForStandoff();
                    exchange.SetAttackSide(actor.Side == Side.Allies ? Side.Enemies : Side.Allies);
                    _approachPresentationRequested = false;
                    SetPhase(Phase.Standoff);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EnterExit(CombatEndReason.Abort);
                return false;
            }
            finally { _isExecutingSkill = false; }
        }

        public bool TryContinueChain(ISkill skill, int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            ICombatant owner = exchange?.CurrentAttackActor;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.ChainDecision || !exchange.IsApplyOutcomeFinalized ||
                !exchange.IsChainActive || exchange.IsAllOutCandidate || !ReferenceEquals(exchange.ChainOwner, owner) ||
                !CombatChainPolicy.ShouldContinue(_session, owner, skill, true))
            {
                return false;
            }

            exchange.PrepareNextChainExchange(owner, exchange.HandoffCount);
            SetPhase(Phase.AttackDeclaration);
            return true;
        }

        public bool TryContinueChain(int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            ICombatant owner = exchange?.CurrentAttackActor;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.ChainDecision || !exchange.IsApplyOutcomeFinalized ||
                !exchange.IsChainActive || exchange.IsAllOutCandidate || !ReferenceEquals(exchange.ChainOwner, owner) ||
                !CombatChainPolicy.CanContinueWithAnySkill(_session, owner))
            {
                return false;
            }

            exchange.PrepareNextChainExchange(owner, exchange.HandoffCount);
            SetPhase(Phase.AttackDeclaration);
            return true;
        }

        public bool TryHandoffChain(
            ICombatant receivingActor,
            ISkill receivingSkill,
            bool isHandoffEligible,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            ICombatant owner = exchange?.CurrentAttackActor;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.ChainDecision || !exchange.IsApplyOutcomeFinalized ||
                !exchange.IsChainActive || exchange.IsAllOutCandidate || !ReferenceEquals(exchange.ChainOwner, owner) ||
                !CombatHandoffPolicy.TryAuthorize(
                    _session,
                    owner,
                    receivingActor,
                    receivingSkill,
                    exchange.HandoffCount,
                    isHandoffEligible,
                    out CombatHandoffPolicy.Result handoff))
            {
                return false;
            }

            exchange.PrepareNextChainExchange(handoff.ReceivingActor, handoff.HandoffCount);
            SetPhase(Phase.AttackDeclaration);
            return true;
        }

        public bool TryEndChain(int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.ChainDecision || !exchange.IsApplyOutcomeFinalized ||
                !exchange.IsChainActive)
            {
                return false;
            }

            if (!ResolveCurrentTerminal())
                ReturnToStandoff();
            return true;
        }

        public bool TryGrantStandoffAttackAuthority(
            Side side,
            int expectedExchangeVersion)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                Phase != Phase.Standoff || exchange == null ||
                !Enum.IsDefined(typeof(Side), side) ||
                exchange.CurrentAttackSide == side ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            exchange.SetAttackSide(side);
            return true;
        }

        internal bool TryExecuteOvercome(
            ICombatant actor,
            int expectedExchangeVersion,
            out CombatOvercomeResult result)
        {
            result = CreateRejectedOvercomeResult(actor, CombatOvercomeFailureReason.InvalidSession);
            CombatOvercomeFailureReason failureReason = CombatOvercomeFailureReason.InvalidSession;
            if (_exited || _isExecutingSkill || _isExecutingAllOut || _isExecutingItem ||
                _isReturningToStandoff || _session == null ||
                !CombatOvercomePolicy.TryAuthorize(
                    _session,
                    Phase,
                    actor,
                    expectedExchangeVersion,
                    out failureReason))
            {
                result = CreateRejectedOvercomeResult(actor, failureReason);
                return false;
            }

            CombatantCombatState state = _session.GetCombatState(actor);
            CombatMentalMutationResult mutation = state.SetMental(
                _session.RuntimeConfig.OvercomeRecoveryMental);
            Side nextAuthority = actor.Side == Side.Allies ? Side.Enemies : Side.Allies;
            if (!TryGrantStandoffAttackAuthority(nextAuthority, expectedExchangeVersion))
            {
                result = CreateRejectedOvercomeResult(actor, CombatOvercomeFailureReason.NotActionAuthority);
                return false;
            }

            result = new CombatOvercomeResult(
                actor,
                true,
                mutation.MentalBefore,
                mutation.MentalAfter,
                mutation.PanicBefore,
                mutation.PanicAfter,
                _session.ExchangeState.Version,
                CombatOvercomeFailureReason.None);
            return true;
        }

        public bool TryTerminateExplicit(
            CombatEndReason requestedReason,
            int expectedExchangeVersion)
        {
            if (!HasExpectedExchangeVersion(expectedExchangeVersion) ||
                _exited || _session.FlowMode != CombatFlowMode.StandoffClashChain)
            {
                return false;
            }

            FinalCombatTerminalPolicy policy = new FinalCombatTerminalPolicy();
            if (!policy.TryResolveExplicit(requestedReason, out CombatTerminalDecision decision))
                return false;

            _session.ExchangeState.StoreTerminalDecision(decision);
            EnterExit(decision.EndReason);
            return true;
        }

        private static bool IsValidTerminalDecision(
            CombatTerminalCandidate expectedCandidate,
            CombatTerminalDecision decision)
        {
            return decision != null &&
                   expectedCandidate != CombatTerminalCandidate.None &&
                   Enum.IsDefined(typeof(CombatTerminalCandidate), expectedCandidate) &&
                   decision.TerminalCandidate == expectedCandidate &&
                   decision.EndReason != CombatEndReason.None &&
                   Enum.IsDefined(typeof(CombatEndReason), decision.EndReason);
        }

        private bool HasExpectedExchangeVersion(int expectedExchangeVersion)
        {
            return !_exited && !_isExecutingAllOut && !_isExecutingSkill && !_isExecutingItem &&
                   !_isReturningToStandoff && _session != null &&
                   _session.FlowMode == CombatFlowMode.StandoffClashChain &&
                   _session.ExchangeState != null &&
                   expectedExchangeVersion >= 0 &&
                   _session.ExchangeState.Version == expectedExchangeVersion;
        }

        private CombatOvercomeResult CreateRejectedOvercomeResult(
            ICombatant actor,
            CombatOvercomeFailureReason failureReason)
        {
            if (_session != null && actor != null &&
                _session.TryGetCombatState(actor, out CombatantCombatState state))
            {
                return new CombatOvercomeResult(
                    actor,
                    false,
                    state.CurrentMental,
                    state.CurrentMental,
                    state.IsPanicked,
                    state.IsPanicked,
                    _session?.ExchangeState?.Version ?? -1,
                    failureReason);
            }

            return new CombatOvercomeResult(
                actor,
                false,
                0,
                0,
                false,
                false,
                _session?.ExchangeState?.Version ?? -1,
                failureReason);
        }

        private static bool TryGetPostureParticipants(
            CombatClashResult clash,
            out ICombatant winner,
            out ICombatant loser,
            out ISkill winningSkill)
        {
            winner = null;
            loser = null;
            winningSkill = null;
            CombatAttackDeclaration attack = clash?.AttackDeclaration;
            CombatResponseDeclaration response = clash?.ResponseDeclaration;

            if (clash?.Outcome == CombatClashOutcome.AttackerWin &&
                attack?.Attacker != null && attack.Skill != null && response?.Responder != null)
            {
                winner = attack.Attacker;
                loser = response.Responder;
                winningSkill = attack.Skill;
                return ReferenceEquals(clash.Winner, winner);
            }

            if (clash?.Outcome == CombatClashOutcome.ResponderWin &&
                response?.Responder != null && response.Skill != null && attack?.Attacker != null)
            {
                winner = response.Responder;
                loser = attack.Attacker;
                winningSkill = response.Skill;
                return ReferenceEquals(clash.Winner, winner);
            }

            return false;
        }

        private bool IsCurrentClashResultConsistent(
            CombatExchangeState exchange,
            CombatClashResult result)
        {
            CombatAttackDeclaration declaration = result.AttackDeclaration;
            if (!ReferenceEquals(declaration, exchange.CurrentDeclaration) ||
                declaration?.Attacker == null || declaration.Target == null || declaration.Skill == null ||
                !_session.TryGetCombatState(declaration.Attacker, out _) ||
                !_session.TryGetCombatState(declaration.Target, out _))
            {
                return false;
            }

            if (result.Outcome == CombatClashOutcome.Unopposed)
            {
                return exchange.ResponseState == CombatResponseState.NoResponse &&
                       result.ResponseDeclaration == null && exchange.CurrentResponse == null;
            }

            CombatResponseDeclaration response = result.ResponseDeclaration;
            return exchange.ResponseState == CombatResponseState.CounterDeclared &&
                   ReferenceEquals(response, exchange.CurrentResponse) &&
                   response?.Responder != null && response.Skill != null &&
                   ReferenceEquals(response.Responder, declaration.Target) &&
                   _session.TryGetCombatState(response.Responder, out _);
        }

        private bool CanExecuteDeclaration(CombatAttackDeclaration declaration)
        {
            return !_exited && _session != null &&
                   _session.FlowMode == CombatFlowMode.StandoffClashChain &&
                   Phase == Phase.AttackDeclaration &&
                   _session.ExchangeState.ResponseState != CombatResponseState.Pending &&
                   _session.ExchangeState.IsCommitted &&
                   CombatEndEvaluator.Evaluate(_session) == CombatEndReason.None &&
                   CanUseDeclarationParticipants(declaration);
        }

        private bool IsCurrentRosterMember(ICombatant combatant)
        {
            if (_session == null || combatant == null)
                return false;

            System.Collections.Generic.IReadOnlyList<ICombatant> roster = _session.GetSide(combatant.Side);
            for (int i = 0; i < roster.Count; i++)
            {
                if (ReferenceEquals(roster[i], combatant))
                    return true;
            }

            return false;
        }

        private bool CanAcceptResponse(CombatResponseDeclaration response)
        {
            if (!CanEditResponse() || response?.Responder == null || response.Skill == null)
                return false;

            CombatAttackDeclaration declaration = _session.ExchangeState.CurrentDeclaration;
            ICombatant responder = response.Responder;
            if (!ReferenceEquals(responder, declaration.Target) ||
                ReferenceEquals(responder, declaration.Attacker) ||
                responder.Side == declaration.Attacker.Side || responder.HP <= 0 || responder.IsStunned ||
                !_session.TryGetCombatState(responder, out CombatantCombatState responderState) ||
                responderState.IsPanicked || responder.Skills == null)
            {
                return false;
            }

            for (int i = 0; i < responder.Skills.Count; i++)
            {
                if (ReferenceEquals(responder.Skills[i], response.Skill))
                    return true;
            }

            return false;
        }

        private bool CanEditResponse()
        {
            return !_exited && _session != null &&
                   _session.FlowMode == CombatFlowMode.StandoffClashChain &&
                   Phase == Phase.AttackDeclaration &&
                   _session.ExchangeState?.CurrentDeclaration != null &&
                   !_session.ExchangeState.IsCommitted;
        }

        private bool CanUseDeclaration(CombatAttackDeclaration declaration)
        {
            if (!CanUseDeclarationParticipants(declaration) ||
                declaration.Attacker.Side == declaration.Target.Side)
            {
                return false;
            }

            return OwnsSkill(declaration.Attacker, declaration.Skill);
        }

        private bool CanUseResponse(
            CombatAttackDeclaration declaration,
            CombatResponseDeclaration response)
        {
            return response?.Responder != null && response.Skill != null &&
                   ReferenceEquals(response.Responder, declaration.Target) &&
                   !ReferenceEquals(response.Responder, declaration.Attacker) &&
                   response.Responder.Side != declaration.Attacker.Side &&
                   response.Responder.HP > 0 && !response.Responder.IsStunned &&
                   _session.TryGetCombatState(response.Responder, out CombatantCombatState responderState) &&
                   !responderState.IsPanicked &&
                   OwnsSkill(response.Responder, response.Skill);
        }

        private static bool OwnsSkill(ICombatant combatant, ISkill skill)
        {
            if (combatant?.Skills == null || skill == null)
                return false;

            for (int i = 0; i < combatant.Skills.Count; i++)
            {
                if (ReferenceEquals(combatant.Skills[i], skill))
                    return true;
            }

            return false;
        }

        private bool CanUseDeclarationParticipants(CombatAttackDeclaration declaration)
        {
            if (_session == null || declaration?.Attacker == null || declaration.Target == null ||
                declaration.Skill == null)
            {
                return false;
            }

            return _session.TryGetCombatState(declaration.Attacker, out _) &&
                   _session.TryGetCombatState(declaration.Target, out _) &&
                   declaration.Attacker.HP > 0 && declaration.Target.HP > 0;
        }

        private bool CanAcceptDeclaration(CombatAttackDeclaration declaration)
        {
            if (_exited || _isExecutingSkill || _isExecutingItem || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.Standoff || declaration == null || declaration.Attacker == null ||
                declaration.Target == null || declaration.Skill == null ||
                CombatEndEvaluator.Evaluate(_session) != CombatEndReason.None)
            {
                return false;
            }

            ICombatant attacker = declaration.Attacker;
            ICombatant target = declaration.Target;
            if (!_session.TryGetCombatState(attacker, out _) ||
                !_session.TryGetCombatState(target, out _) ||
                attacker.HP <= 0 || target.HP <= 0 || attacker.Side == target.Side)
            {
                return false;
            }

            if (attacker.IsStunned || CombatDeclarationPolicy.IsPanicked(_session, attacker))
                return false;

            if (attacker.Skills == null)
                return false;

            for (int i = 0; i < attacker.Skills.Count; i++)
            {
                if (ReferenceEquals(attacker.Skills[i], declaration.Skill))
                    return true;
            }

            return false;
        }

        public void ForceExit(CombatEndReason reason)
        {
            if (_exited)
                return;

            EnterExit(reason == CombatEndReason.None ? CombatEndReason.Abort : reason);
        }

        public void Tick()
        {
            if (_exited)
                return;

            switch (Phase)
            {
                case Phase.EnterCombat:
                    CombatEndReason initialEnd = _session?.FlowMode == CombatFlowMode.StandoffClashChain
                        ? FinalCombatTerminalPolicy.Evaluate(_session) : CombatEndEvaluator.Evaluate(_session);
                    if (initialEnd != CombatEndReason.None)
                    {
                        EnterExit(initialEnd);
                        break;
                    }

                    if (_session.FlowMode == CombatFlowMode.StandoffClashChain)
                    {
                        if (!EnterStandoff())
                            EnterExit(CombatEndReason.Abort);
                        break;
                    }

                    if (!_session.TryBeginNewTurn())
                    {
                        EnterExit(CombatEndReason.Abort);
                        break;
                    }

                    SetPhase(Phase.Planning);
                    Debug.Log($"[CombatStateMachine] EnterCombat -> Planning. Turn={_session.TurnIndex}");
                    break;

                case Phase.Resolution:
                    BeginPresentationOnce();
                    break;

                case Phase.Approach:
                    BeginApproachPresentationOnce();
                    break;

                case Phase.EndTurn:
                    EndTurnOnce();
                    break;
            }
        }

        public void Tick(float deltaSeconds)
        {
            Tick();
            AdvanceStandoff(deltaSeconds);
        }

        private void AdvanceStandoff(float deltaSeconds)
        {
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.Standoff || deltaSeconds <= 0f ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            CombatEndReason endReason = CombatEndEvaluator.Evaluate(_session);
            if (endReason != CombatEndReason.None)
            {
                EnterExit(endReason);
                return;
            }

            RecoverMp(_session.Allies, deltaSeconds);
            RecoverMp(_session.Enemies, deltaSeconds);

            float pressureAdvance = _session.RuntimeConfig.PressurePerSecond * deltaSeconds;
            if (_session.StandoffState.Advance(pressureAdvance))
                RaiseEnemyActionRequired();
        }

        private void RecoverMp(System.Collections.Generic.IReadOnlyList<ICombatant> combatants, float deltaSeconds)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant == null || combatant.HP <= 0)
                    continue;

                if (_session.TryGetCombatState(combatant, out CombatantCombatState state))
                    state.RecoverMp(_session.RuntimeConfig.MpRecoveryPerSecond, deltaSeconds);
            }
        }

        private void RaiseEnemyActionRequired()
        {
            Action<CombatSession> handlers = OnEnemyActionRequired;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<CombatSession>)invocationList[i]).Invoke(_session);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void BeginPresentationOnce()
        {
            if (_presentationRequested || _session?.CurrentTurn == null)
                return;

            CombatTurn turn = _session.CurrentTurn;
            if (!turn.TryBeginPresentation())
            {
                Debug.LogError(
                    $"[CombatStateMachine] Presentation rejected. Turn={_session.TurnIndex}, Lifecycle={turn.Lifecycle}.");
                return;
            }

            _presentationRequested = true;
            _presentingTurn = turn;
            _presentingTurnIndex = _session.TurnIndex;
            int turnIndex = _presentingTurnIndex;
            Action completion = () => OnResolutionFinished(_session, turn, turnIndex);

            Action<CombatSession, Action> handler = GetSinglePresentationHandler();
            if (handler == null)
            {
                Debug.LogWarning("[CombatStateMachine] No CombatDirector bound. Completing resolution immediately.");
                completion();
                return;
            }

            try
            {
                handler.Invoke(_session, completion);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                completion();
            }
        }

        private void BeginApproachPresentationOnce()
        {
            if (_approachPresentationRequested)
                return;

            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!CanUseDeclarationParticipants(declaration))
                return;

            Action<CombatAttackDeclaration, Action> handler = GetSingleApproachPresentationHandler();
            if (handler == null)
                return;

            _approachPresentationRequested = true;
            Action completion = () => CompleteApproach();
            try
            {
                handler.Invoke(declaration, completion);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                completion();
            }
        }

        private Action<CombatAttackDeclaration, Action> GetSingleApproachPresentationHandler()
        {
            if (OnRequireApproachPlay == null)
                return null;

            Delegate[] handlers = OnRequireApproachPlay.GetInvocationList();
            if (handlers.Length > 1)
                Debug.LogWarning("[CombatStateMachine] Multiple approach presenters are bound. Only the first will run.");

            return handlers[0] as Action<CombatAttackDeclaration, Action>;
        }

        private Action<CombatSession, Action> GetSinglePresentationHandler()
        {
            if (OnRequireResolutionPlay == null)
                return null;

            Delegate[] handlers = OnRequireResolutionPlay.GetInvocationList();
            if (handlers.Length > 1)
                Debug.LogWarning("[CombatStateMachine] Multiple resolution presenters are bound. Only the first will run.");

            return handlers[0] as Action<CombatSession, Action>;
        }

        private void OnResolutionFinished(CombatSession expectedSession, CombatTurn expectedTurn, int expectedTurnIndex)
        {
            if (_exited || Phase != Phase.Resolution)
                return;

            if (!ReferenceEquals(_session, expectedSession) ||
                !ReferenceEquals(_session.CurrentTurn, expectedTurn) ||
                !ReferenceEquals(_presentingTurn, expectedTurn) ||
                _session.TurnIndex != expectedTurnIndex ||
                _presentingTurnIndex != expectedTurnIndex)
            {
                return;
            }

            if (!expectedTurn.TryMarkPresented())
                return;

            SetPhase(Phase.EndTurn);
        }

        private void EndTurnOnce()
        {
            if (_endTurnProcessed || _session?.CurrentTurn == null)
                return;

            CombatTurn completedTurn = _session.CurrentTurn;
            if (!completedTurn.TryComplete())
                return;

            _endTurnProcessed = true;

            CombatEndReason evaluated = CombatEndEvaluator.Evaluate(_session);
            if (evaluated != CombatEndReason.None)
            {
                EnterExit(evaluated);
                return;
            }

            ClearStunsAtTurnEnd();
            if (!_session.TryBeginNewTurn())
            {
                EnterExit(CombatEndReason.Abort);
                return;
            }

            _presentationRequested = false;
            _endTurnProcessed = false;
            _presentingTurn = null;
            _presentingTurnIndex = -1;
            SetPhase(Phase.Planning);
            Debug.Log($"[CombatStateMachine] EndTurn -> Planning. Turn={_session.TurnIndex}");
        }

        private void ClearStunsAtTurnEnd()
        {
            ClearStuns(_session.Allies);
            ClearStuns(_session.Enemies);
        }

        private int CountRosterCombatants()
        {
            return CountCombatants(_session.Allies) + CountCombatants(_session.Enemies);
        }

        private static int CountCombatants(System.Collections.Generic.IReadOnlyList<ICombatant> combatants)
        {
            int count = 0;
            for (int i = 0; i < combatants.Count; i++)
            {
                if (combatants[i] != null)
                    count++;
            }

            return count;
        }

        private static void ClearStuns(System.Collections.Generic.IReadOnlyList<ICombatant> combatants)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                ICombatant combatant = combatants[i];
                if (combatant != null && combatant.IsStunned)
                    StaggerSystem.ClearStunAtTurnEnd(combatant);
            }
        }

        private void EnterExit(CombatEndReason reason)
        {
            if (_exited)
                return;

            _exited = true;
            EndReason = reason;
            _session?.MarkCombatEnded();
            _session?.DisposeUltimateRuntime();
            _session?.CurrentTurn?.CompleteForExit();
            SetPhase(Phase.ExitCombat);
        }

        private void SetPhase(Phase next)
        {
            if (Phase == next)
                return;

            Phase previous = Phase;
            Phase = next;
            if (_session?.FlowMode == CombatFlowMode.StandoffClashChain)
                _session.ExchangeState?.AdvanceVersion();
            if (OnPhaseChanged == null)
                return;

            Delegate[] handlers = OnPhaseChanged.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<Phase, Phase>)handlers[i]).Invoke(previous, next);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
