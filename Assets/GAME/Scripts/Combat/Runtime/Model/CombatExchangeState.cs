using System;

namespace Game.Combat.Model
{
    public sealed class CombatExchangeState
    {
        public Side InitialInitiative { get; }
        public Side CurrentAttackSide { get; private set; }
        public bool IsChainActive { get; private set; }
        public ICombatant ChainOwner { get; private set; }
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
        }

        public void SetChainState(bool isActive, ICombatant owner)
        {
            IsChainActive = isActive && owner != null;
            ChainOwner = IsChainActive ? owner : null;
        }

        internal void CommitDeclaration(CombatAttackDeclaration declaration)
        {
            CurrentDeclaration = declaration;
            CurrentAttackSide = declaration.DeclaringSide;
            ResponseState = CombatResponseState.Pending;
            CurrentResponse = null;
            CurrentClashResult = null;
            ClearPreparedOutcome();
            ClearCommit();
        }

        internal void CommitResponse(CombatResponseDeclaration response)
        {
            CurrentResponse = response;
            ResponseState = CombatResponseState.CounterDeclared;
        }

        internal void ConfirmNoResponse()
        {
            CurrentResponse = null;
            ResponseState = CombatResponseState.NoResponse;
        }

        internal void MarkCommitted(int attackMpCost, int responseMpCost)
        {
            IsCommitted = true;
            CommittedAttackMpCost = Math.Max(0, attackMpCost);
            CommittedResponseMpCost = Math.Max(0, responseMpCost);
        }

        internal void StoreClashResult(CombatClashResult result)
        {
            CurrentClashResult = result;
            ClearPreparedOutcome();
            ClearPostureResolution();
        }

        internal void StorePreparedOutcome(CombatOutcomeAction action)
        {
            CurrentOutcomeAction = action;
            IsOutcomePrepared = true;
            ClearPreparedExecution();
        }

        internal void StorePreparedExecution(CombatSkillExecutionRequest request)
        {
            CurrentExecutionRequest = request;
            IsExecutionPrepared = true;
            ClearCompletedExecution();
        }

        internal void StoreCompletedExecution(CombatSkillExecutionResult result)
        {
            CurrentExecutionResult = result;
            IsExecutionCompleted = true;
            ClearPostureResolution();
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
        }

        internal void StoreAftermath(CombatAftermathSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            CurrentAftermathSnapshot = snapshot;
            IsAftermathPrepared = true;
            ClearAftermathDecision();
        }

        internal void StoreAftermathDecision(CombatAftermathDecision decision)
        {
            if (decision == null)
                return;

            CurrentAftermathDecision = decision;
            IsAftermathDecisionPrepared = true;
            ClearTerminalDecision();
        }

        internal void StoreTerminalDecision(CombatTerminalDecision decision)
        {
            if (decision == null)
                return;

            CurrentTerminalDecision = decision;
            IsTerminalDecisionPrepared = true;
        }

        internal void ClearDeclaration()
        {
            CurrentDeclaration = null;
            CurrentResponse = null;
            ResponseState = CombatResponseState.Pending;
            CurrentClashResult = null;
            ClearPreparedOutcome();
            ClearCommit();
        }

        private void ClearPreparedOutcome()
        {
            IsOutcomePrepared = false;
            CurrentOutcomeAction = null;
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
    }
}
