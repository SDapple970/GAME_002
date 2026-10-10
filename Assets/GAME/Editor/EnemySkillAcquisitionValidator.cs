using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Data;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class EnemySkillAcquisitionValidator
    {
        [MenuItem("GAME/Validation/Enemy Skill Acquisition")]
        public static void ValidateAllAssets()
        {
            EnemyDefinitionSO[] enemies = AssetDatabase.FindAssets("t:EnemyDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>).ToArray();
            SkillDefinitionSO[] skills = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).ToArray();
            IReadOnlyList<string> issues = CollectIssues(enemies, skills);
            if (issues.Count == 0)
                Debug.Log("[EnemySkillAcquisitionValidator] Enemy identities and explicit skill mappings are valid.");
            for (int i = 0; i < issues.Count; i++)
                Debug.LogError($"[EnemySkillAcquisitionValidator] {issues[i]}");
        }

        /// <summary>Pass the production registry to validate mappings in that specific combat registration set.</summary>
        public static IReadOnlyList<string> CollectIssues(
            IEnumerable<EnemyDefinitionSO> enemies,
            IEnumerable<SkillDefinitionSO> skills)
        {
            List<string> issues = new();
            Dictionary<string, int> skillKeyCounts = new(StringComparer.Ordinal);
            HashSet<string> uniqueSkillKeys = new(StringComparer.Ordinal);
            if (skills != null)
                foreach (SkillDefinitionSO skill in skills)
                {
                    string key = Normalize(skill != null ? skill.PersistentKey : null);
                    if (key == null) continue;
                    skillKeyCounts.TryGetValue(key, out int count);
                    skillKeyCounts[key] = count + 1;
                    if (skill.OwnershipCategory == SkillOwnershipCategory.Unique) uniqueSkillKeys.Add(key);
                }

            HashSet<string> sources = new(StringComparer.Ordinal);
            if (enemies == null)
            {
                issues.Add("EnemyDefinitionSO collection is null.");
                return issues;
            }

            foreach (EnemyDefinitionSO enemy in enemies)
            {
                if (enemy == null)
                {
                    issues.Add("EnemyDefinitionSO is null.");
                    continue;
                }

                string sourceKey = Normalize(enemy.PersistentKey);
                if (sourceKey == null)
                    issues.Add($"{enemy.name} has an empty persistentKey.");
                else if (!sources.Add(sourceKey))
                    issues.Add($"{enemy.name} has duplicate enemy persistentKey '{sourceKey}'.");

                HashSet<string> mapped = new(StringComparer.Ordinal);
                foreach (string rawKey in enemy.AcquirableSkillPersistentKeys)
                {
                    string key = Normalize(rawKey);
                    if (key == null)
                        issues.Add($"{enemy.name} has an empty acquirable skill persistent key.");
                    else if (!mapped.Add(key))
                        issues.Add($"{enemy.name} has duplicate acquirable skill persistent key '{key}'.");
                    else if (!skillKeyCounts.TryGetValue(key, out int count) || count != 1)
                        issues.Add($"{enemy.name} has unresolved or ambiguous acquirable skill persistent key '{key}'.");
                    else if (uniqueSkillKeys.Contains(key))
                        issues.Add($"{enemy.name} cannot map Unique skill '{key}' as an enemy acquisition.");
                }
            }

            return issues.AsReadOnly();
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
