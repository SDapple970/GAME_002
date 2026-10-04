using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    /// <summary>
    /// Immutable FinalExchange result snapshot exposed to presentation code.
    /// The callback advances pacing only; all combat mutation is already complete.
    /// </summary>
    public sealed class CombatOutcomePresentationRequest
    {
        private readonly Func<bool> _completion;
        private bool _completionRequested;

        public int ExchangeVersion { get; }
        public int ChainSequenceId { get; }
        public CombatAttackDeclaration AttackDeclaration { get; }
        public CombatResponseState ResponseState { get; }
        public CombatResponseDeclaration ResponseDeclaration { get; }
        public CombatClashResult ClashResult { get; }
        public CombatOutcomeAction WinningAction { get; }
        public CombatSkillExecutionResult ExecutionResult { get; }
        public CombatPostureResult PostureResult { get; }
        public CombatStunResult StunResult { get; }
        public IReadOnlyList<CombatMentalMutationResult> MentalMutationResults { get; }
        public CombatAftermathSnapshot Aftermath { get; }
        public CombatAftermathDecision AftermathDecision { get; }
        public bool HasResponse => ResponseState == CombatResponseState.CounterDeclared;
        public bool HasClash => HasResponse && ClashResult?.Outcome != CombatClashOutcome.Unopposed;
        public ICombatant Winner => ClashResult?.Winner;
        public ICombatant Loser { get; }
        public Side NextAttackSide { get; }
        public bool IsTerminal =>
            AftermathDecision?.Kind == CombatAftermathDecisionKind.TerminalCandidate;
        public bool IsAllOutCandidate =>
            AftermathDecision?.Kind == CombatAftermathDecisionKind.AllOutCandidate;

        internal CombatOutcomePresentationRequest(
            int exchangeVersion,
            int chainSequenceId,
            CombatAttackDeclaration attackDeclaration,
            CombatResponseState responseState,
            CombatResponseDeclaration responseDeclaration,
            CombatClashResult clashResult,
            CombatOutcomeAction winningAction,
            CombatSkillExecutionResult executionResult,
            CombatPostureResult postureResult,
            CombatStunResult stunResult,
            IReadOnlyList<CombatMentalMutationResult> mentalMutationResults,
            CombatAftermathSnapshot aftermath,
            CombatAftermathDecision aftermathDecision,
            Func<bool> completion)
        {
            ExchangeVersion = exchangeVersion;
            ChainSequenceId = chainSequenceId;
            AttackDeclaration = attackDeclaration;
            ResponseState = responseState;
            ResponseDeclaration = responseDeclaration;
            ClashResult = clashResult;
            WinningAction = winningAction;
            ExecutionResult = executionResult;
            PostureResult = postureResult;
            StunResult = stunResult;
            CombatMentalMutationResult[] mentalSnapshot =
                new CombatMentalMutationResult[mentalMutationResults?.Count ?? 0];
            for (int i = 0; i < mentalSnapshot.Length; i++)
                mentalSnapshot[i] = mentalMutationResults[i];
            MentalMutationResults = Array.AsReadOnly(mentalSnapshot);
            Aftermath = aftermath;
            AftermathDecision = aftermathDecision;
            Loser = ResolveLoser(attackDeclaration, responseDeclaration, Winner);
            NextAttackSide = Winner != null
                ? Winner.Side
                : attackDeclaration != null ? attackDeclaration.DeclaringSide : Side.Allies;
            _completion = completion;
        }

        public bool TryComplete()
        {
            if (_completionRequested)
                return false;

            _completionRequested = true;
            return _completion != null && _completion.Invoke();
        }

        private static ICombatant ResolveLoser(
            CombatAttackDeclaration attack,
            CombatResponseDeclaration response,
            ICombatant winner)
        {
            if (winner == null)
                return null;

            if (ReferenceEquals(winner, attack?.Attacker))
                return response?.Responder ?? attack?.Target;

            return attack?.Attacker;
        }
    }
}
