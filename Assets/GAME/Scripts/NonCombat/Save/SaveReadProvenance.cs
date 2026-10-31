using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.NonCombat.Save
{
    internal enum EquipmentReadOrigin
    {
        Unknown,
        MissingInLegacySource,
        ExplicitEmpty,
        ExplicitSkills,
        ExplicitNull
    }

    /// <summary>Evidence from the original JSON, independent of migration's normalized empty lists.</summary>
    internal sealed class SaveReadProvenance
    {
        private readonly JToken _skills;
        private readonly bool _skillsAbsent;
        private readonly string _normalizedSkills;
        private readonly HashSet<string> _sourceCharacters = new(StringComparer.Ordinal);
        private readonly HashSet<string> _discardedEquipment = new(StringComparer.Ordinal);
        internal int? SourceSchemaVersion { get; }

        private SaveReadProvenance(JObject root, GameSaveData normalized)
        {
            JToken version = (root["header"] as JObject)?["schemaVersion"];
            if (version?.Type == JTokenType.Integer && long.TryParse(version.ToString(), out long number) && number >= 1 && number <= int.MaxValue)
                SourceSchemaVersion = (int)number;
            _skillsAbsent = root.Property("characterSkills", StringComparison.Ordinal) == null;
            _skills = root["characterSkills"]?.DeepClone();
            if ((root["party"] as JObject)?["memberIds"] is JArray members)
                foreach (var member in members)
                    if (member.Type == JTokenType.String && !string.IsNullOrWhiteSpace(member.Value<string>())) _sourceCharacters.Add(member.Value<string>().Trim());
            AddSourceCharacters((root["progression"] as JObject)?["characters"] as JArray);
            AddSourceCharacters((_skills as JObject)?["characters"] as JArray);
            _normalizedSkills = JsonUtility.ToJson(normalized.characterSkills);
            if ((_skills as JObject)?["characters"] is JArray rows)
                foreach (var token in rows)
                {
                    if (token is not JObject row || row["characterId"]?.Type != JTokenType.String ||
                        row["equippedSkillKeys"] is not JArray keys) continue;
                    string id = row["characterId"].Value<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var retained = new HashSet<string>(StringComparer.Ordinal);
                    if (normalized.characterSkills?.characters != null)
                        foreach (var character in normalized.characterSkills.characters)
                            if (character.characterId == id && character.equippedSkillKeys != null)
                                retained.UnionWith(character.equippedSkillKeys);
                    foreach (var key in keys)
                        if (key.Type == JTokenType.String && !string.IsNullOrWhiteSpace(key.Value<string>()) &&
                            !retained.Contains(key.Value<string>().Trim())) _discardedEquipment.Add(id);
                }
        }

        private void AddSourceCharacters(JArray rows)
        {
            if (rows == null) return;
            foreach (var row in rows)
                if (row is JObject character && character["characterId"]?.Type == JTokenType.String &&
                    !string.IsNullOrWhiteSpace(character["characterId"].Value<string>()))
                    _sourceCharacters.Add(character["characterId"].Value<string>().Trim());
        }

        internal static SaveReadProvenance FromOriginal(string json, GameSaveData normalized)
        {
            try
            {
                var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                return new SaveReadProvenance(root, normalized);
            }
            catch (JsonException)
            {
                // Ambiguous/unrecognized source evidence must never authorize a fallback.
                return null;
            }
        }

        internal bool Matches(GameSaveData data) => data != null &&
            string.Equals(_normalizedSkills, JsonUtility.ToJson(data.characterSkills), StringComparison.Ordinal);

        internal bool HadDiscardedEquipment(string characterId) =>
            characterId != null && _discardedEquipment.Contains(characterId.Trim());

        internal EquipmentReadOrigin ForCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return EquipmentReadOrigin.Unknown;
            if (!_sourceCharacters.Contains(characterId.Trim())) return EquipmentReadOrigin.Unknown;
            // Equipped keys were introduced by the version-8 migration. A current-format
            // omission or a versionless object does not prove missing historical equipment.
            bool historical = SourceSchemaVersion.HasValue && SourceSchemaVersion.Value <= 8;
            if (_skillsAbsent) return historical ? EquipmentReadOrigin.MissingInLegacySource : EquipmentReadOrigin.Unknown;
            if (_skills is not JObject skills) return EquipmentReadOrigin.Unknown;
            var charactersProperty = skills.Property("characters", StringComparison.Ordinal);
            if (charactersProperty == null) return historical ? EquipmentReadOrigin.MissingInLegacySource : EquipmentReadOrigin.Unknown;
            if (charactersProperty.Value is not JArray characters) return EquipmentReadOrigin.Unknown;
            JObject found = null;
            foreach (JToken token in characters)
            {
                if (token is not JObject row || row["characterId"]?.Type != JTokenType.String ||
                    !string.Equals(row["characterId"].Value<string>()?.Trim(), characterId.Trim(), StringComparison.Ordinal)) continue;
                if (found != null) return EquipmentReadOrigin.Unknown;
                found = row;
            }
            if (found == null) return historical ? EquipmentReadOrigin.MissingInLegacySource : EquipmentReadOrigin.Unknown;
            var equipped = found.Property("equippedSkillKeys", StringComparison.Ordinal);
            if (equipped == null) return historical ? EquipmentReadOrigin.MissingInLegacySource : EquipmentReadOrigin.Unknown;
            if (equipped.Value.Type == JTokenType.Null) return EquipmentReadOrigin.ExplicitNull;
            if (equipped.Value is not JArray keys) return EquipmentReadOrigin.Unknown;
            foreach (var key in keys) if (key.Type != JTokenType.String) return EquipmentReadOrigin.Unknown;
            return keys.Count == 0 ? EquipmentReadOrigin.ExplicitEmpty : EquipmentReadOrigin.ExplicitSkills;
        }
    }
}
