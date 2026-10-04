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

        internal CombatSkillExecutionResult(
            ICombatant actor,
            ISkill skill,
            CombatClashOutcome sourceOutcome,
            IReadOnlyList<CombatSkillTargetResult> targetResults,
            IReadOnlyList<CombatStatusApplicationResult> appliedStatusResults)
        {
            Actor = actor;
            Skill = skill;
            SourceOutcome = sourceOutcome;
            WasExecuted = true;

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
    }
}
