using System;
using System.Collections.Generic;
using Game.Combat.Data;
using Game.Combat.Integration;

namespace Game.NonCombat.Progress
{
    /// <summary>Read-only level evaluation. Progression owns levels; no unlocked state is stored here.</summary>
    public static class UniqueSkillUnlockEvaluator
    {
        public static IReadOnlyList<UniqueSkillAvailability> GetAvailability(string characterId,
            CharacterProgressionService progression, IEnumerable<SkillDefinitionSO> skills)
        {
            return Evaluate(characterId, progression,
                PersistentSkillCombatLoadoutBridge.BuildDefinitionMap(skills ?? Array.Empty<SkillDefinitionSO>(), null));
        }

        public static int? GetNextUnlockLevel(IReadOnlyList<UniqueSkillAvailability> skills)
        {
            int? next = null;
            if (skills != null)
                for (int i = 0; i < skills.Count; i++)
                    if (skills[i].Status == UniqueSkillAvailabilityStatus.Locked && skills[i].RequiredLevel.HasValue &&
                        (!next.HasValue || skills[i].RequiredLevel.Value < next.Value))
                        next = skills[i].RequiredLevel.Value;
            return next;
        }

        internal static IReadOnlyList<UniqueSkillAvailability> Evaluate(string characterId,
            CharacterProgressionService progression, IReadOnlyDictionary<string, SkillDefinitionSO> skills)
        {
            string id = CharacterIdentity.Normalize(characterId);
            List<UniqueSkillAvailability> result = new();
            if (id == null || progression == null || !progression.TryGetState(id, out int level, out _) ||
                !progression.TryGetDefinition(id, out CharacterProgressionDefinitionSO definition) ||
                definition.UniqueSkillUnlocks.Count > 6)
                return result.AsReadOnly();

            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (CharacterUniqueSkillUnlockDefinition entry in definition.UniqueSkillUnlocks)
            {
                string key = string.IsNullOrWhiteSpace(entry?.PersistentSkillKey) ? null : entry.PersistentSkillKey.Trim();
                UniqueSkillAvailabilityStatus status;
                int? required = null;
                if (entry == null || key == null || !seen.Add(key) || !skills.TryGetValue(key, out SkillDefinitionSO skill) ||
                    skill.OwnershipCategory != SkillOwnershipCategory.Unique || CharacterIdentity.Normalize(skill.UniqueOwnerCharacterId) != id)
                    status = UniqueSkillAvailabilityStatus.InvalidDefinition;
                else if (!entry.Enabled)
                    status = UniqueSkillAvailabilityStatus.Disabled;
                else if (entry.Condition == UniqueSkillUnlockCondition.Unconfigured)
                    status = UniqueSkillAvailabilityStatus.Unconfigured;
                else if (entry.Condition != UniqueSkillUnlockCondition.Level || !CharacterUniqueSkillUnlockDefinition.IsSupportedLevel(entry.UnlockLevel))
                    status = UniqueSkillAvailabilityStatus.InvalidDefinition;
                else
                {
                    required = entry.UnlockLevel;
                    status = level >= required.Value ? UniqueSkillAvailabilityStatus.Unlocked : UniqueSkillAvailabilityStatus.Locked;
                }
                string displayName = key != null && skills.TryGetValue(key, out SkillDefinitionSO namedSkill) ? namedSkill.displayName : null;
                result.Add(new UniqueSkillAvailability(key, required, status, displayName));
            }
            return result.AsReadOnly();
        }
    }
}
