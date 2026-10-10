using System;
using System.Collections.Generic;
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
            if (lineup.Count == 0) return Failed("Initial combat lineup is not configured.", out message);
            if (DefaultRewardTargetId == null || !known.Contains(DefaultRewardTargetId))
                return Failed("Default Reward EXP target must reference an authored Character ID.", out message);
            message = null;
            return true;
        }

        private static bool Failed(string reason, out string message) { message = reason; return false; }
    }
}
