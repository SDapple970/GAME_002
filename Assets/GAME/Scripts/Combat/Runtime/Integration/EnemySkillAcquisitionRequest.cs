using System.Collections.Generic;
using Game.Combat.Model;

namespace Game.Combat.Integration
{
    public sealed class EnemySkillAcquisitionRequest
    {
        public string CompletionId { get; }
        public string RecipientCharacterId { get; }
        public CombatEndReason EndReason { get; }
        public bool IsPartyEligible { get; }
        public IReadOnlyList<CombatDefeatedEnemyRecord> DefeatedEnemySources { get; }

        private EnemySkillAcquisitionRequest(CombatResult result)
        {
            CompletionId = result.CompletionId;
            RecipientCharacterId = result.SkillAcquisitionRecipientCharacterId;
            EndReason = result.EndReason;
            IsPartyEligible = result.IsPartySkillAcquisitionEligible;
            List<CombatDefeatedEnemyRecord> records = new();
            if (result.DefeatedEnemySources != null)
                for (int i = 0; i < result.DefeatedEnemySources.Count; i++)
                    if (result.DefeatedEnemySources[i] != null) records.Add(result.DefeatedEnemySources[i]);
            DefeatedEnemySources = records.AsReadOnly();
        }

        public static EnemySkillAcquisitionRequest FromCombatResult(CombatResult result)
        {
            return result != null ? new EnemySkillAcquisitionRequest(result) : null;
        }
    }
}
