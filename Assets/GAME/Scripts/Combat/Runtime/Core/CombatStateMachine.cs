using System;
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

            _session.ExchangeState.ClearDeclaration();
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

        public bool ConfirmNoResponse()
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!CanEditResponse() || !CanUseDeclarationParticipants(declaration))
                return false;

            _session.ExchangeState.ConfirmNoResponse();
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

        public bool TryBeginApproach()
        {
            CombatAttackDeclaration declaration = _session?.ExchangeState?.CurrentDeclaration;
            if (!CanExecuteDeclaration(declaration))
                return false;

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

        public bool TryResolveClash(ICombatClashRule clashRule = null)
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            CombatAttackDeclaration declaration = exchange?.CurrentDeclaration;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
                Phase != Phase.Clash || exchange == null || !exchange.IsCommitted ||
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

        public bool TryPrepareSkillExecution()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
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

        public bool TryExecutePreparedSkill()
        {
            CombatExchangeState exchange = _session?.ExchangeState;
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
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

            if (!Game.Combat.Actions.SkillRunner.TryExecute(
                    _session,
                    request,
                    out CombatSkillExecutionResult result))
            {
                return false;
            }

            exchange.StoreCompletedExecution(result);
            return true;
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
                postureDelta = Math.Max(0, postureRule.ResolvePostureDelta(request));
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

            exchange.StoreAftermath(new CombatAftermathSnapshot(_session, exchange));
            return exchange.IsAftermathPrepared;
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

            EnterExit(terminalDecision.EndReason);
            return Phase == Phase.ExitCombat && EndReason == terminalDecision.EndReason;
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
                responder.Side == declaration.Attacker.Side || responder.HP <= 0 ||
                !_session.TryGetCombatState(responder, out _) || responder.Skills == null)
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
                   response.Responder.HP > 0 &&
                   _session.TryGetCombatState(response.Responder, out _) &&
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
            if (_exited || _session == null || _session.FlowMode != CombatFlowMode.StandoffClashChain ||
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
                    CombatEndReason initialEnd = CombatEndEvaluator.Evaluate(_session);
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
            _session?.CurrentTurn?.CompleteForExit();
            SetPhase(Phase.ExitCombat);
        }

        private void SetPhase(Phase next)
        {
            if (Phase == next)
                return;

            Phase previous = Phase;
            Phase = next;
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
