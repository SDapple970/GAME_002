using System;
using System.Collections.Generic;
using Game.NonCombat.Progress;

namespace Game.Combat.Integration
{
    public enum EnemySkillAcquisitionStatus
    {
        Processed,
        InvalidRequest,
        NotVictory,
        InvalidRecipient,
        RuntimeUnavailable
    }

    public sealed class EnemySkillAcquisitionResult
    {
        public EnemySkillAcquisitionStatus Status { get; }
        public IReadOnlyList<CharacterSkillAcquireResult> Skills { get; }
        public IReadOnlyList<string> SkippedSkillPersistentKeys { get; }
        public int AcquiredCount { get; }

        internal EnemySkillAcquisitionResult(
            EnemySkillAcquisitionStatus status,
            List<CharacterSkillAcquireResult> skills = null,
            List<string> skippedKeys = null)
        {
            Status = status;
            Skills = Array.AsReadOnly(skills?.ToArray() ?? Array.Empty<CharacterSkillAcquireResult>());
            SkippedSkillPersistentKeys = Array.AsReadOnly(skippedKeys?.ToArray() ?? Array.Empty<string>());
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].Changed) AcquiredCount++;
        }
    }
}
