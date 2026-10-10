using System;
using System.Collections.Generic;
using Game.Combat.Model;
using Game.Combat.Data;
using Game.NonCombat.Progress;

namespace Game.Combat.Integration
{
    /// <summary>Stateless acquisition translation; CharacterSkillRuntime remains the only persistent owner.</summary>
    public static class EnemySkillAcquisitionProcessor
    {
        internal static EnemySkillAcquisitionResult ProcessWithDefinitions(
            EnemySkillAcquisitionRequest request,
            CharacterSkillRuntime runtime,
            IReadOnlyDictionary<string, SkillDefinitionSO> definitions,
            Action<string> diagnostic)
        {
            if (request == null) return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.InvalidRequest);
            if (request.EndReason != CombatEndReason.Victory) return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.NotVictory);
            if (runtime == null) return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.RuntimeUnavailable);
            string recipient = CharacterIdentity.Normalize(request.RecipientCharacterId);
            if (!request.IsPartyEligible && recipient == null)
                return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.InvalidRecipient);

            List<CharacterSkillAcquireResult> acquired = new();
            List<string> skipped = new();
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (CombatDefeatedEnemyRecord record in request.DefeatedEnemySources)
            {
                EnemySourceSnapshot source = record?.Source;
                if (Normalize(source?.SourceKey) == null) continue;
                foreach (string key in source.AcquirableSkillPersistentKeys)
                {
                    if (!seen.Add(key)) continue;
                    if (definitions == null || !definitions.TryGetValue(key, out SkillDefinitionSO skill) || skill == null ||
                        skill.OwnershipCategory == SkillOwnershipCategory.Unique ||
                        !Enum.IsDefined(typeof(SkillOwnershipCategory), skill.OwnershipCategory))
                    {
                        skipped.Add(key);
                        diagnostic?.Invoke($"Enemy source '{source.SourceKey}' maps unresolved, invalid-role or Unique skill '{key}'; acquisition skipped.");
                        continue;
                    }

                    if (skill.OwnershipCategory == SkillOwnershipCategory.Film)
                    {
                        if (request.IsPartyEligible) acquired.Add(runtime.TryAcquireSharedFilm(key));
                        else
                        {
                            skipped.Add(key);
                            diagnostic?.Invoke($"Film '{key}' acquisition requires participating party identities; acquisition skipped.");
                        }
                    }
                    else if (recipient != null)
                    {
                        acquired.Add(runtime.TryAcquire(recipient, key));
                    }
                    else
                    {
                        skipped.Add(key);
                    }
                }
            }

            return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.Processed, acquired, skipped);
        }

        public static EnemySkillAcquisitionResult Process(
            EnemySkillAcquisitionRequest request,
            CharacterSkillRuntime runtime,
            IEnumerable<string> registeredPersistentSkillKeys,
            Action<string> diagnostic = null)
        {
            if (request == null)
                return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.InvalidRequest);
            if (request.EndReason != CombatEndReason.Victory)
                return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.NotVictory);
            string recipient = CharacterIdentity.Normalize(request.RecipientCharacterId);
            if (recipient == null)
                return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.InvalidRecipient);
            if (runtime == null)
                return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.RuntimeUnavailable);

            HashSet<string> known = new(StringComparer.Ordinal);
            if (registeredPersistentSkillKeys != null)
                foreach (string rawKey in registeredPersistentSkillKeys)
                {
                    string key = Normalize(rawKey);
                    if (key != null) known.Add(key);
                }

            List<CharacterSkillAcquireResult> acquired = new();
            List<string> skipped = new();
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int i = 0; i < request.DefeatedEnemySources.Count; i++)
            {
                EnemySourceSnapshot source = request.DefeatedEnemySources[i]?.Source;
                if (Normalize(source?.SourceKey) == null) continue;
                foreach (string key in source.AcquirableSkillPersistentKeys)
                {
                    if (!seen.Add(key)) continue;
                    if (!known.Contains(key))
                    {
                        skipped.Add(key);
                        diagnostic?.Invoke($"Enemy source '{source.SourceKey}' maps unresolved skill key '{key}'; acquisition skipped. completion={request.CompletionId}, recipient={recipient}.");
                        continue;
                    }

                    acquired.Add(runtime.TryAcquire(recipient, key));
                }
            }

            return new EnemySkillAcquisitionResult(EnemySkillAcquisitionStatus.Processed, acquired, skipped);
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
