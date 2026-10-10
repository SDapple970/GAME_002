using System.Collections.Generic;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.NonCombat.Progress;
using UnityEngine;

namespace Game.Combat.Integration
{
    /// <summary>Called once by the canonical combat completion boundary before Reward/Quest observers.</summary>
    public static class EnemySkillAcquisitionIntegration
    {
        public static EnemySkillAcquisitionResult ProcessCompletion(
            CombatResult result,
            IEnumerable<SkillDefinitionSO> productionDefinitions,
            Object diagnosticContext)
        {
            CharacterSkillSaveParticipant participant = CharacterSkillSaveParticipant.Instance;
            // Reuse the existing production key resolver; no global asset scan or second registry.
            Dictionary<string, SkillDefinitionSO> definitions = result?.EndReason == CombatEndReason.Victory &&
                result.DefeatedEnemySources.Count > 0 && productionDefinitions != null
                ? PersistentSkillCombatLoadoutBridge.BuildDefinitionMap(productionDefinitions, diagnosticContext)
                : new Dictionary<string, SkillDefinitionSO>();
            EnemySkillAcquisitionResult acquisition = EnemySkillAcquisitionProcessor.ProcessWithDefinitions(
                EnemySkillAcquisitionRequest.FromCombatResult(result),
                participant != null ? participant.Runtime : null,
                definitions,
                message => Debug.LogWarning($"[EnemySkillAcquisitionIntegration] {message}", diagnosticContext));

            for (int i = 0; i < acquisition.Skills.Count; i++)
                if (acquisition.Skills[i].Changed)
                    Debug.Log($"[EnemySkillAcquisitionIntegration] Acquired '{acquisition.Skills[i].PersistentSkillKey}' " +
                        $"for '{acquisition.Skills[i].CharacterId ?? "shared party Film collection"}'. completion={result.CompletionId}.", diagnosticContext);
            return acquisition;
        }
    }
}
