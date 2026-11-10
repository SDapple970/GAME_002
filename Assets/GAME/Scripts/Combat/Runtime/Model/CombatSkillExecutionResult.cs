using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    public sealed class CombatSkillExecutionResult
    {
        public ICombatant Actor { get; }
        public ISkill Skill { get; }
        public CombatClashOutcome SourceOutcome { get; }
        public IReadOnlyList<CombatSkillTargetResult> TargetResults { get; }
        public IReadOnlyList<CombatStatusApplicationResult> AppliedStatusResults { get; }
        public bool WasExecuted { get; }
        /// <summary>Allies eligible when the All-Out choice opened, in party order.</summary>
        public IReadOnlyList<ICombatant> AllOutParticipants { get; }
        /// <summary>Actual All-Out contributors and their individual receipts, in execution order.
        /// For an All-Out, TargetResults and AppliedStatusResults contain the combined receipts.</summary>
        public IReadOnlyList<CombatSkillExecutionResult> AllOutExecutions { get; }

        internal CombatSkillExecutionResult(
            ICombatant actor,
            ISkill skill,
            CombatClashOutcome sourceOutcome,
            IReadOnlyList<CombatSkillTargetResult> targetResults,
            IReadOnlyList<CombatStatusApplicationResult> appliedStatusResults,
            IReadOnlyList<ICombatant> allOutParticipants = null,
            IReadOnlyList<CombatSkillExecutionResult> allOutExecutions = null)
        {
            Actor = actor;
            Skill = skill;
            SourceOutcome = sourceOutcome;
            WasExecuted = true;
            AllOutParticipants = allOutParticipants ?? Array.Empty<ICombatant>();
            AllOutExecutions = allOutExecutions ?? Array.Empty<CombatSkillExecutionResult>();

            CombatSkillTargetResult[] snapshot =
                new CombatSkillTargetResult[targetResults?.Count ?? 0];
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = targetResults[i];

            TargetResults = Array.AsReadOnly(snapshot);

            CombatStatusApplicationResult[] statusSnapshot =
                new CombatStatusApplicationResult[appliedStatusResults?.Count ?? 0];
            for (int i = 0; i < statusSnapshot.Length; i++)
                statusSnapshot[i] = appliedStatusResults[i];

            AppliedStatusResults = Array.AsReadOnly(statusSnapshot);
        }

        internal static CombatSkillExecutionResult CombineAllOut(
            IReadOnlyList<ICombatant> participants, List<CombatSkillExecutionResult> executions)
        {
            if (executions.Count == 0) return null;

            var targets = new List<CombatSkillTargetResult>();
            var statuses = new List<CombatStatusApplicationResult>();
            foreach (CombatSkillExecutionResult execution in executions)
            {
                foreach (CombatSkillTargetResult target in execution.TargetResults)
                {
                    int index = targets.FindIndex(existing => ReferenceEquals(existing.Target, target.Target));
                    if (index >= 0)
                        targets[index] = targets[index].Combine(target);
                    else
                        targets.Add(target);
                }
                statuses.AddRange(execution.AppliedStatusResults);
            }

            CombatSkillExecutionResult lead = executions[0];
            return new CombatSkillExecutionResult(lead.Actor, lead.Skill, lead.SourceOutcome, targets, statuses,
                new List<ICombatant>(participants).AsReadOnly(), executions.AsReadOnly());
        }
    }
}
