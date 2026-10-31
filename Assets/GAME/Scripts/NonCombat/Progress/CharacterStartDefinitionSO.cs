using System;
using System.Collections.Generic;
using Game.NonCombat.Party;
using Game.Combat.Data;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    /// <summary>Authored startup configuration only; Party and Progression retain all session state.</summary>
    [CreateAssetMenu(menuName = "GAME/NonCombat/Character Start Definition", fileName = "CharacterStartDefinition")]
    public sealed class CharacterStartDefinitionSO : ScriptableObject
    {
        [SerializeField] private CharacterProgressionDefinitionSO[] definitions = Array.Empty<CharacterProgressionDefinitionSO>();
        [SerializeField] private string[] initialMemberIds = Array.Empty<string>();
        [SerializeField] private string initialLeaderId;
        [SerializeField] private string[] initialCombatMemberIds = Array.Empty<string>();
        [SerializeField] private string defaultRewardTargetId;
        [SerializeField] private InitialSkillGrant[] initialSkillGrants = Array.Empty<InitialSkillGrant>();

        [Serializable]
        internal sealed class InitialSkillGrant
        {
            [SerializeField] private string characterId;
            [SerializeField] private SkillDefinitionSO[] skills = Array.Empty<SkillDefinitionSO>();
            internal string CharacterId => CharacterIdentity.Normalize(characterId);
            internal IReadOnlyList<SkillDefinitionSO> Skills => skills ?? Array.Empty<SkillDefinitionSO>();
        }

        internal IReadOnlyList<InitialSkillGrant> InitialSkillGrants => initialSkillGrants ?? Array.Empty<InitialSkillGrant>();

        public IReadOnlyList<CharacterProgressionDefinitionSO> Definitions => Array.AsReadOnly(definitions ?? Array.Empty<CharacterProgressionDefinitionSO>());
        public IReadOnlyList<string> InitialMemberIds => Array.AsReadOnly(initialMemberIds ?? Array.Empty<string>());
        public string InitialLeaderId => CharacterIdentity.Normalize(initialLeaderId);
        public IReadOnlyList<string> InitialCombatMemberIds => Array.AsReadOnly(initialCombatMemberIds ?? Array.Empty<string>());
        public string DefaultRewardTargetId => CharacterIdentity.Normalize(defaultRewardTargetId);

        public bool TryValidate(out string message)
        {
            HashSet<string> known = new(StringComparer.Ordinal);
            foreach (CharacterProgressionDefinitionSO definition in Definitions)
            {
                string id = CharacterIdentity.Normalize(definition != null ? definition.CharacterId : null);
                if (id == null || !known.Add(id)) return Failed("Definitions require unique, non-empty Character IDs.", out message);
                for (int level = definition.StartingLevel; level < definition.MaximumLevel; level++)
                    if (!definition.TryGetRequiredExperience(level, out _)) return Failed($"Character '{id}' has an invalid EXP curve at level {level}.", out message);
            }
            if (known.Count == 0) return Failed("Character definitions are not configured.", out message);

            HashSet<string> members = new(StringComparer.Ordinal);
            foreach (string value in InitialMemberIds)
            {
                string id = CharacterIdentity.Normalize(value);
                if (id == null || !known.Contains(id) || !members.Add(id)) return Failed("Initial members must be unique authored Character IDs.", out message);
            }
            if (members.Count == 0 || InitialLeaderId == null || !members.Contains(InitialLeaderId))
                return Failed("Initial Party requires an owned leader and at least one member.", out message);

            HashSet<string> lineup = new(StringComparer.Ordinal);
            foreach (string value in InitialCombatMemberIds)
            {
                string id = CharacterIdentity.Normalize(value);
                if (id == null || !members.Contains(id) || !lineup.Add(id)) return Failed("Initial combat members must be unique owned Character IDs.", out message);
            }
            if (!PartyRuntime.IsValidCombatLineupCount(lineup.Count))
                return Failed($"Initial combat lineup must contain 1 to {PartyRuntime.MaxCombatLineupSize} members.", out message);
            if (DefaultRewardTargetId == null || !known.Contains(DefaultRewardTargetId))
                return Failed("Default Reward EXP target must reference an authored Character ID.", out message);

            HashSet<string> grantCharacters = new(StringComparer.Ordinal);
            foreach (InitialSkillGrant grant in InitialSkillGrants)
            {
                if (grant == null || grant.CharacterId == null || !members.Contains(grant.CharacterId) ||
                    !grantCharacters.Add(grant.CharacterId) || grant.Skills.Count == 0)
                    return Failed("Initial skill grants require unique initial Party members and non-empty skills.", out message);
                HashSet<string> skillKeys = new(StringComparer.Ordinal);
                HashSet<int> skillIds = new();
                foreach (SkillDefinitionSO skill in grant.Skills)
                    if (skill == null || string.IsNullOrWhiteSpace(skill.PersistentKey) ||
                        skill.OwnershipCategory != SkillOwnershipCategory.Compatibility ||
                        !skillKeys.Add(skill.PersistentKey.Trim()) || !skillIds.Add(skill.skillId))
                        return Failed("Initial skills require unique Compatibility skills with persistent keys and runtime IDs.", out message);
            }
            message = null;
            return true;
        }

        private static bool Failed(string reason, out string message) { message = reason; return false; }
    }
}
