using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Data;
using Game.NonCombat.Progress;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class CharacterUniqueSkillValidator
    {
        [MenuItem("GAME/Validation/Character Unique Skills")]
        public static void ValidateAllAssets()
        {
            var characters = AssetDatabase.FindAssets("t:CharacterProgressionDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>);
            var skills = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>);
            IReadOnlyList<string> issues = CollectIssues(characters, skills);
            if (issues.Count == 0) Debug.Log("[CharacterUniqueSkillValidator] Unique skill authoring is valid.");
            foreach (string issue in issues) Debug.LogError($"[CharacterUniqueSkillValidator] {issue}");
        }

        public static IReadOnlyList<string> CollectIssues(IEnumerable<CharacterProgressionDefinitionSO> characters, IEnumerable<SkillDefinitionSO> skills)
        {
            List<string> issues = new();
            Dictionary<string, SkillDefinitionSO> registry = new(StringComparer.Ordinal);
            HashSet<string> ambiguous = new(StringComparer.Ordinal);
            foreach (SkillDefinitionSO skill in skills ?? Array.Empty<SkillDefinitionSO>())
            {
                string key = Normalize(skill?.PersistentKey);
                if (key == null) continue;
                if (!registry.TryAdd(key, skill)) ambiguous.Add(key);
                if (skill.OwnershipCategory == SkillOwnershipCategory.Unique && Normalize(skill.UniqueOwnerCharacterId) == null)
                    issues.Add($"Unique skill '{key}' has no owner character ID.");
            }
            HashSet<string> owners = new(StringComparer.Ordinal);
            foreach (CharacterProgressionDefinitionSO character in characters ?? Array.Empty<CharacterProgressionDefinitionSO>())
            {
                string id = Normalize(character?.CharacterId);
                if (id == null) { issues.Add("Character Unique authoring has no owner character ID."); continue; }
                if (!owners.Add(id)) issues.Add($"Duplicate character definition '{id}'.");
                if (character.UniqueSkillUnlocks.Count > 6) issues.Add($"Character '{id}' exceeds six Unique skills.");
                HashSet<string> seen = new(StringComparer.Ordinal);
                foreach (CharacterUniqueSkillUnlockDefinition entry in character.UniqueSkillUnlocks)
                {
                    string key = Normalize(entry?.PersistentSkillKey);
                    if (key == null || !seen.Add(key)) { issues.Add($"Character '{id}' has empty or duplicate Unique skill key."); continue; }
                    if (ambiguous.Contains(key) || !registry.TryGetValue(key, out SkillDefinitionSO skill) ||
                        skill.OwnershipCategory != SkillOwnershipCategory.Unique || Normalize(skill.UniqueOwnerCharacterId) != id)
                        issues.Add($"Character '{id}' has unresolved, non-Unique or wrong-owner key '{key}'.");
                    if (entry.Condition != UniqueSkillUnlockCondition.Unconfigured &&
                        (entry.Condition != UniqueSkillUnlockCondition.Level || !CharacterUniqueSkillUnlockDefinition.IsSupportedLevel(entry.UnlockLevel)))
                        issues.Add($"Character '{id}' has unsupported Unique unlock condition for '{key}'.");
                }
            }
            return issues.AsReadOnly();
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
