using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.Editor
{
    public static class SkillDefinitionPersistentIdentityValidator
    {
        [MenuItem("GAME/Validation/Combat Skill Persistent Identities")]
        public static void ValidateAllAssets()
        {
            SkillDefinitionSO[] definitions = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(path))
                .ToArray();
            IReadOnlyList<string> issues = CollectPersistentKeyIssues(definitions);

            if (issues.Count == 0)
            {
                Debug.Log("[SkillDefinitionPersistentIdentityValidator] Persistent skill identities are valid.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
                Debug.LogError($"[SkillDefinitionPersistentIdentityValidator] {issues[i]}");
        }

        public static IReadOnlyList<string> CollectPersistentKeyIssues(IEnumerable<SkillDefinitionSO> definitions)
        {
            List<string> issues = new();
            HashSet<string> keys = new(StringComparer.Ordinal);
            if (definitions == null)
            {
                issues.Add("SkillDefinitionSO collection is null.");
                return issues;
            }

            foreach (SkillDefinitionSO definition in definitions)
            {
                if (definition == null)
                {
                    issues.Add("SkillDefinitionSO is null.");
                    continue;
                }

                string key = definition.PersistentKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    issues.Add($"{definition.name} has an empty persistentKey.");
                    continue;
                }

                if (!keys.Add(key))
                    issues.Add($"{definition.name} has duplicate persistentKey '{key}'.");
            }

            return issues;
        }

        public static IReadOnlyList<string> CollectRegistrationIssues(
            IEnumerable<SkillDefinitionSO> definitions,
            string registrationName)
        {
            List<string> issues = new(CollectPersistentKeyIssues(definitions));
            if (definitions == null)
                return issues;

            HashSet<int> skillIds = new();
            foreach (SkillDefinitionSO definition in definitions)
            {
                if (definition == null)
                    continue;

                if (definition.skillId <= 0)
                    issues.Add($"{registrationName}: {definition.name} has invalid skillId {definition.skillId}.");
                else if (!skillIds.Add(definition.skillId))
                    issues.Add($"{registrationName}: duplicate skillId {definition.skillId} in one SkillBook registration set.");
            }

            return issues;
        }
    }
}
