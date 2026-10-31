using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.Combat.Integration
{
    /// <summary>
    /// Integration-only conversion from persistent equipped keys to isolated combat loadouts.
    /// CombatSession and combat core receive only immutable ISkill snapshots.
    /// </summary>
    public static class PersistentSkillCombatLoadoutBridge
    {
        // The public compatibility resolver remains available to LegacyPlanning/test callers.
        // Production FinalExchange must not silently drop an equipped key and start a partial loadout.
        internal static bool TryApplyForProduction(CombatStartRequest request,
            IEnumerable<SkillDefinitionSO> definitions, UnityEngine.Object context, out string error)
        {
            error = null;
            if (request == null)
            { error = "Production skill request is missing."; return false; }
            // Field-only requests still resolve their authored SkillBook without persistent
            // bindings. A bound equipped key must resolve even when the registry is absent.
            var registry = definitions != null ? new List<SkillDefinitionSO>(definitions) : new List<SkillDefinitionSO>();
            var byKey = BuildDefinitionMap(registry, context);
            CharacterSkillSaveParticipant owner = CharacterSkillSaveParticipant.Instance;
            foreach (GameObject field in request.AllyFieldObjects)
                request.ClearAllyLoadoutSnapshot(field);
            foreach (GameObject field in request.AllyFieldObjects)
            {
                if (!request.TryGetAllyCharacterId(field, out string id)) continue;
                if (owner != null && owner.HadDiscardedEquipment(id))
                { error = $"Original equipped keys for '{id}' were discarded by save normalization; a partial combat snapshot is not allowed."; return false; }
                var equipped = owner != null ? owner.Runtime.GetEquippedSkills(id) : Array.Empty<string>();
                if (id == "hero.test" && equipped.Count == 0)
                {
                    EquipmentReadOrigin origin = owner != null ? owner.GetEquipmentReadOrigin(id) : EquipmentReadOrigin.Unknown;
                    error = origin == EquipmentReadOrigin.MissingInLegacySource
                        ? $"Legacy equipment is missing in original schema {owner.EquipmentSourceSchemaVersion}; no approved Legacy Hand policy exists; automatic fallback is disabled."
                        : $"Equipment for '{id}' has source {origin}; automatic fallback is disabled.";
                    return false;
                }
                var ids = new HashSet<int>();
                foreach (string key in equipped)
                {
                    if (!byKey.TryGetValue(key, out SkillDefinitionSO definition))
                    { error = $"Equipped key '{key}' for '{id}' is unresolved or ambiguous; stored equipment was preserved."; return false; }
                    if (!Enum.IsDefined(typeof(SkillOwnershipCategory), definition.OwnershipCategory))
                    { error = $"Equipped key '{key}' has an invalid ownership category."; return false; }
                    if (definition.OwnershipCategory == SkillOwnershipCategory.Film &&
                        (owner == null || !owner.Runtime.HasSharedFilm(key) && !owner.Runtime.HasAcquired(id, key)))
                    { error = $"Equipped Film '{key}' for '{id}' is not owned."; return false; }
                    if (definition.OwnershipCategory == SkillOwnershipCategory.Unique)
                    {
                        bool unlocked = false;
                        foreach (var unique in UniqueSkillUnlockEvaluator.Evaluate(id, CharacterProgressionService.Instance, byKey))
                            if (unique.PersistentSkillKey == key && unique.IsUnlocked) { unlocked = true; break; }
                        if (!unlocked)
                        { error = $"Equipped Unique '{key}' for '{id}' is not unlocked for its owner."; return false; }
                    }
                    if (!ids.Add(definition.skillId))
                    { error = $"Equipped keys for '{id}' contain a duplicate runtime SkillId {definition.skillId}."; return false; }
                }
            }
            // Resolve only after every actor's equipped keys pass the preflight. No acquisition,
            // reset, alias substitution, fallback snapshot, or Hand rule is manufactured here.
            ApplyToRequest(request, registry, context);
            return true;
        }

        public static void ApplyToRequest(
            CombatStartRequest request,
            IEnumerable<SkillDefinitionSO> definitions,
            UnityEngine.Object diagnosticContext)
        {
            if (request == null || definitions == null)
                return;

            Dictionary<string, SkillDefinitionSO> definitionsByKey = BuildDefinitionMap(definitions, diagnosticContext);
            CharacterSkillRuntime runtime = CharacterSkillSaveParticipant.Instance != null ? CharacterSkillSaveParticipant.Instance.Runtime : null;
            if (runtime != null) ConfigureFilmOwnership(runtime, definitionsByKey);
            for (int i = 0; i < request.AllyFieldObjects.Count; i++)
            {
                GameObject fieldObject = request.AllyFieldObjects[i];
                if (!request.TryGetAllyCharacterId(fieldObject, out string characterId))
                    continue;

                request.ClearAllyLoadoutSnapshot(fieldObject);
                IReadOnlyList<string> equippedKeys = runtime != null ? runtime.GetEquippedSkills(characterId) : Array.Empty<string>();

                List<SoSkill> resolvedSkills = new();
                HashSet<int> runtimeIds = new();
                for (int keyIndex = 0; keyIndex < equippedKeys.Count; keyIndex++)
                {
                    string persistentKey = equippedKeys[keyIndex];
                    if (!definitionsByKey.TryGetValue(persistentKey, out SkillDefinitionSO definition))
                    {
                        Debug.LogWarning(
                            $"[PersistentSkillCombatLoadoutBridge] Equipped key '{persistentKey}' for character '{characterId}' " +
                            "is not resolvable by the production combat registry; it was omitted from this combat snapshot.",
                            diagnosticContext);
                        continue;
                    }

                    if (definition.OwnershipCategory == SkillOwnershipCategory.Unique ||
                        !Enum.IsDefined(typeof(SkillOwnershipCategory), definition.OwnershipCategory) ||
                        definition.OwnershipCategory == SkillOwnershipCategory.Film && !runtime.HasSharedFilm(persistentKey))
                        continue;

                    if (!runtimeIds.Add(definition.skillId))
                    {
                        Debug.LogWarning(
                            $"[PersistentSkillCombatLoadoutBridge] Equipped key '{persistentKey}' for character '{characterId}' " +
                            $"shares runtime SkillId {definition.skillId} with an earlier resolved skill and was omitted.",
                            diagnosticContext);
                        continue;
                    }

                    resolvedSkills.Add(new SoSkill(definition));
                }

                bool includeCompatibility = resolvedSkills.Count == 0;
                IReadOnlyList<UniqueSkillAvailability> uniqueSkills = UniqueSkillUnlockEvaluator.Evaluate(
                    characterId, CharacterProgressionService.Instance, definitionsByKey);
                for (int uniqueIndex = 0; uniqueIndex < uniqueSkills.Count; uniqueIndex++)
                {
                    if (!uniqueSkills[uniqueIndex].IsUnlocked) continue;
                    SkillDefinitionSO definition = definitionsByKey[uniqueSkills[uniqueIndex].PersistentSkillKey];
                    if (runtimeIds.Add(definition.skillId)) resolvedSkills.Add(new SoSkill(definition));
                }

                if (resolvedSkills.Count > 0)
                    request.SetAllyLoadoutSnapshot(fieldObject, new CombatSkillLoadoutSnapshot(resolvedSkills, includeCompatibility));
            }
        }

        internal static Dictionary<string, SkillDefinitionSO> BuildDefinitionMap(
            IEnumerable<SkillDefinitionSO> definitions,
            UnityEngine.Object diagnosticContext)
        {
            Dictionary<string, SkillDefinitionSO> result = new(StringComparer.Ordinal);
            HashSet<string> ambiguousKeys = new(StringComparer.Ordinal);
            foreach (SkillDefinitionSO definition in definitions)
            {
                string key = NormalizePersistentKey(definition != null ? definition.PersistentKey : null);
                if (key == null)
                    continue;

                if (result.ContainsKey(key))
                {
                    result.Remove(key);
                    if (ambiguousKeys.Add(key))
                    {
                        Debug.LogWarning(
                            $"[PersistentSkillCombatLoadoutBridge] Production combat registry has duplicate persistent key '{key}'. " +
                            "The ambiguous key will not be resolved into a combat snapshot.",
                            diagnosticContext);
                    }
                    continue;
                }

                if (!ambiguousKeys.Contains(key))
                    result.Add(key, definition);
            }

            return result;
        }

        internal static void ConfigureFilmOwnership(CharacterSkillRuntime runtime, IReadOnlyDictionary<string, SkillDefinitionSO> definitions)
        {
            List<string> keys = new();
            foreach (KeyValuePair<string, SkillDefinitionSO> pair in definitions)
                if (pair.Value.OwnershipCategory == SkillOwnershipCategory.Film) keys.Add(pair.Key);
            runtime.ConfigureFilmKeys(keys);
        }

        private static string NormalizePersistentKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
