using System;
using System.Collections.Generic;

namespace Game.Combat.Model
{
    public sealed class CombatSkillExecutionRequest
    {
        public ICombatant Actor { get; }
        public ISkill Skill { get; }
        public IReadOnlyList<ICombatant> Targets { get; }
        public ICombatant Opponent { get; }
        public CombatClashOutcome SourceOutcome { get; }

        internal CombatSkillExecutionRequest(
            ICombatant actor,
            ISkill skill,
            IReadOnlyList<ICombatant> targets,
            ICombatant opponent,
            CombatClashOutcome sourceOutcome)
        {
            Actor = actor;
            Skill = skill;
            Opponent = opponent;
            SourceOutcome = sourceOutcome;

            ICombatant[] snapshot = new ICombatant[targets?.Count ?? 0];
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = targets[i];

            Targets = Array.AsReadOnly(snapshot);
        }
    }
}
