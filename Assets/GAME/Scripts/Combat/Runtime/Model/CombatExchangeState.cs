using System;

namespace Game.Combat.Model
{
    public sealed class CombatExchangeState
    {
        public Side InitialInitiative { get; }
        public Side CurrentAttackSide { get; private set; }
        public ICombatant CurrentAttackActor { get; private set; }
        public int Version { get; private set; }
        public int ChainSequenceId { get; private set; }
        public int HandoffCount { get; private set; }
        public bool IsChainActive { get; private set; }
        public ICombatant ChainOwner { get; private set; }
        public bool IsApplyOutcomeFinalized { get; private set; }
        public bool IsAllOutCandidate { get; private set; }
        public CombatAttackDeclaration CurrentDeclaration { get; private set; }
        public CombatResponseState ResponseState { get; private set; }
        public CombatResponseDeclaration CurrentResponse { get; private set; }
        public bool IsCommitted { get; private set; }
        public int CommittedAttackMpCost { get; private set; }
        public int CommittedResponseMpCost { get; private set; }
        public CombatClashResult CurrentClashResult { get; private set; }
        public bool IsOutcomePrepared { get; private set; }
        public CombatOutcomeAction CurrentOutcomeAction { get; private set; }
        public bool IsExecutionPrepared { get; private set; }
        public CombatSkillExecutionRequest CurrentExecutionRequest { get; private set; }
        public bool IsExecutionCompleted { get; private set; }
        public CombatSkillExecutionResult CurrentExecutionResult { get; private set; }
        public CombatPostureResolutionState PostureResolutionState { get; private set; }
        public CombatPostureResult CurrentPostureResult { get; private set; }
        public CombatStunResolutionState StunResolutionState { get; private set; }
        public CombatStunResult CurrentStunResult { get; private set; }
        public bool IsAftermathPrepared { get; private set; }
        public CombatAftermathSnapshot CurrentAftermathSnapshot { get; private set; }
        public bool IsAftermathDecisionPrepared { get; private set; }
        public CombatAftermathDecision CurrentAftermathDecision { get; private set; }
        public bool IsTerminalDecisionPrepared { get; private set; }
        public CombatTerminalDecision CurrentTerminalDecision { get; private set; }

        public CombatExchangeState(Side initialInitiative)
        {
            if (!Enum.IsDefined(typeof(Side), initialInitiative))
                initialInitiative = Side.Allies;

            InitialInitiative = initialInitiative;
            CurrentAttackSide = initialInitiative;
        }

        public void SetAttackSide(Side side)
        {
            if (!Enum.IsDefined(typeof(Side), side))
                return;

            CurrentAttackSide = side;
            AdvanceVersion();
        }

        public void SetChainState(bool isActive, ICombatant owner)
        {
            IsChainActive = isActive && owner != null;
            ChainOwner = IsChainActive ? owner : null;
            CurrentAttackActor = ChainOwner;
            if (CurrentAttackActor != null)
                CurrentAttackSide = CurrentAttackActor.Side;
            AdvanceVersion();
        }

        internal void CommitDeclaration(CombatAttackDeclaration declaration)
        {
            CurrentDeclaration = declaration;
            CurrentAttackSide = declaration.DeclaringSide;
            CurrentAttackActor = declaration.Attacker;
            ResponseState = CombatResponseState.Pending;
            CurrentResponse = null;
            CurrentClashResult = null;
            ClearPreparedOutcome();
            ClearCommit();
            AdvanceVersion();
        }

        internal void CommitResponse(CombatResponseDeclaration response)
        {
            CurrentResponse = response;
            ResponseState = CombatResponseState.CounterDeclared;
            AdvanceVersion();
        }

        internal void ConfirmNoResponse()
        {
            CurrentResponse = null;
            ResponseState = CombatResponseState.NoResponse;
            AdvanceVersion();
        }

        internal void MarkCommitted(int attackMpCost, int responseMpCost)
        {
            IsCommitted = true;
            CommittedAttackMpCost = Math.Max(0, attackMpCost);
            CommittedResponseMpCost = Math.Max(0, responseMpCost);
            AdvanceVersion();
        }

        internal void StoreCommitResult(CombatExchangeCommitResult result)
        {
            if (result == null || !result.Succeeded)
                return;

            CurrentResponse = result.CommittedResponse;
            ResponseState = result.ResponseState;
            IsCommitted = true;
            CommittedAttackMpCost = Math.Max(0, result.AttackMpSpent);
            CommittedResponseMpCost = Math.Max(0, result.ResponseMpSpent);
            AdvanceVersion();
        }

        internal void StoreClashResult(CombatClashResult result)
        {
            CurrentClashResult = result;
            ClearPreparedOutcome();
            ClearPostureResolution();
            AdvanceVersion();
        }

        internal void StorePreparedOutcome(CombatOutcomeAction action)
        {
            CurrentOutcomeAction = action;
            IsOutcomePrepared = true;
            ClearPreparedExecution();
            AdvanceVersion();
        }

        internal void StorePreparedExecution(CombatSkillExecutionRequest request)
        {
            CurrentExecutionRequest = request;
            IsExecutionPrepared = true;
            ClearCompletedExecution();
            AdvanceVersion();
        }

        internal void StoreCompletedExecution(CombatSkillExecutionResult result)
        {
            CurrentExecutionResult = result;
            IsExecutionCompleted = true;
            ClearPostureResolution();
            AdvanceVersion();
        }

        internal void StorePostureResolution(
            CombatPostureResolutionState state,
            CombatPostureResult result)
        {
            if (state == CombatPostureResolutionState.Pending)
                return;

            PostureResolutionState = state;
            CurrentPostureResult = state == CombatPostureResolutionState.Applied ? result : null;
            ClearStunResolution();
            AdvanceVersion();
        }

        internal void StoreStunResolution(
            CombatStunResolutionState state,
            CombatStunResult result)
        {
            if (state == CombatStunResolutionState.Pending)
                return;

            StunResolutionState = state;
            CurrentStunResult = state == CombatStunResolutionState.Applied ? result : null;
            ClearAftermath();
            AdvanceVersion();
        }

        internal void StoreAftermath(CombatAftermathSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            CurrentAftermathSnapshot = snapshot;
            IsAftermathPrepared = true;
            ClearAftermathDecision();
            AdvanceVersion();
        }

        internal void StoreAftermathDecision(CombatAftermathDecision decision)
        {
            if (decision == null)
                return;

            CurrentAftermathDecision = decision;
            IsAftermathDecisionPrepared = true;
            ClearTerminalDecision();
            AdvanceVersion();
        }

        internal void StoreTerminalDecision(CombatTerminalDecision decision)
        {
            if (decision == null)
                return;

            CurrentTerminalDecision = decision;
            IsTerminalDecisionPrepared = true;
            AdvanceVersion();
        }

        internal void ClearDeclaration()
        {
            CurrentDeclaration = null;
            CurrentResponse = null;
            ResponseState = CombatResponseState.Pending;
            CurrentClashResult = null;
            ClearPreparedOutcome();
            ClearCommit();
            AdvanceVersion();
        }

        internal void StoreAttackAuthority(ICombatant owner)
        {
            if (owner == null)
                return;

            CurrentAttackActor = owner;
            CurrentAttackSide = owner.Side;
            AdvanceVersion();
        }

        internal void FinalizeApplyOutcome(bool isAllOutCandidate)
        {
            IsApplyOutcomeFinalized = true;
            IsAllOutCandidate = isAllOutCandidate;
            AdvanceVersion();
        }

        internal void BeginChainDecision(ICombatant owner)
        {
            if (owner == null)
                return;

            if (!IsChainActive)
                ChainSequenceId++;

            IsChainActive = true;
            ChainOwner = owner;
            CurrentAttackActor = owner;
            CurrentAttackSide = owner.Side;
            AdvanceVersion();
        }

        internal void PrepareNextChainExchange(ICombatant owner, int handoffCount)
        {
            if (!IsChainActive || owner == null || handoffCount < 0)
                return;

            ClearDeclarationCore();
            ChainOwner = owner;
            CurrentAttackActor = owner;
            CurrentAttackSide = owner.Side;
            HandoffCount = handoffCount;
            AdvanceVersion();
        }

        internal void ResetForStandoff()
        {
            ClearDeclarationCore();
            IsChainActive = false;
            ChainOwner = null;
            CurrentAttackActor = null;
            HandoffCount = 0;
            IsApplyOutcomeFinalized = false;
            IsAllOutCandidate = false;
            AdvanceVersion();
        }

        internal void AdvanceVersion()
        {
            if (Version < int.MaxValue)
                Version++;
        }

        private void ClearPreparedOutcome()
        {
            IsOutcomePrepared = false;
            CurrentOutcomeAction = null;
            IsApplyOutcomeFinalized = false;
            IsAllOutCandidate = false;
            ClearPreparedExecution();
        }

        private void ClearPreparedExecution()
        {
            IsExecutionPrepared = false;
            CurrentExecutionRequest = null;
            ClearCompletedExecution();
        }

        private void ClearCompletedExecution()
        {
            IsExecutionCompleted = false;
            CurrentExecutionResult = null;
            ClearPostureResolution();
        }

        private void ClearPostureResolution()
        {
            PostureResolutionState = CombatPostureResolutionState.Pending;
            CurrentPostureResult = null;
            ClearStunResolution();
        }

        private void ClearStunResolution()
        {
            StunResolutionState = CombatStunResolutionState.Pending;
            CurrentStunResult = null;
            ClearAftermath();
        }

        private void ClearAftermath()
        {
            IsAftermathPrepared = false;
            CurrentAftermathSnapshot = null;
            ClearAftermathDecision();
        }

        private void ClearAftermathDecision()
        {
            IsAftermathDecisionPrepared = false;
            CurrentAftermathDecision = null;
            ClearTerminalDecision();
        }

        private void ClearTerminalDecision()
        {
            IsTerminalDecisionPrepared = false;
            CurrentTerminalDecision = null;
        }

        private void ClearCommit()
        {
            IsCommitted = false;
            CommittedAttackMpCost = 0;
            CommittedResponseMpCost = 0;
        }

        private void ClearDeclarationCore()
        {
            CurrentDeclaration = null;
            CurrentResponse = null;
            ResponseState = CombatResponseState.Pending;
            CurrentClashResult = null;
            ClearPreparedOutcome();
            ClearCommit();
        }
    }
}
