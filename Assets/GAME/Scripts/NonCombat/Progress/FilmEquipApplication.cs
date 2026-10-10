using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Core;
using Game.NonCombat.Party;

namespace Game.NonCombat.Progress
{
    /// <summary>Non-visual application boundary for validated, non-combat Film equip requests.</summary>
    public sealed class FilmEquipApplication
    {
        private readonly CharacterSkillRuntime _runtime;
        private readonly PartyRuntime _party;
        private readonly GameStateMachine _state;
        private readonly CombatEntryPoint _entry;
        private readonly IReadOnlyDictionary<string, SkillDefinitionSO> _definitions;

        public bool CanEdit => _state != null &&
            (_state.Current == GameState.Exploration || _state.Current == GameState.UIOnly) &&
            (_entry == null || _entry.ActiveSession == null && _entry.ActiveStateMachine == null);

        public FilmEquipApplication(CharacterSkillRuntime runtime, PartyRuntime party,
            IEnumerable<SkillDefinitionSO> definitions, GameStateMachine state, CombatEntryPoint entry)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _party = party;
            _state = state;
            _entry = entry;
            _definitions = PersistentSkillCombatLoadoutBridge.BuildDefinitionMap(definitions ?? Array.Empty<SkillDefinitionSO>(), null);
            ConfigureOwnership();
        }

        public IReadOnlyList<string> GetAvailableFilms()
        {
            ConfigureOwnership();
            List<string> keys = new();
            foreach (string key in _runtime.GetSharedFilms())
                if (_definitions.TryGetValue(key, out SkillDefinitionSO skill) && skill.OwnershipCategory == SkillOwnershipCategory.Film)
                    keys.Add(key);
            return keys.AsReadOnly();
        }

        public CharacterSkillEquipResult TryEquipFilm(string characterId, string persistentKey) => Change(characterId, persistentKey, true);
        public CharacterSkillEquipResult TryUnequipFilm(string characterId, string persistentKey) => Change(characterId, persistentKey, false);

        private CharacterSkillEquipResult Change(string characterId, string persistentKey, bool equip)
        {
            ConfigureOwnership();
            string id = CharacterIdentity.Normalize(characterId);
            string key = string.IsNullOrWhiteSpace(persistentKey) ? null : persistentKey.Trim();
            CharacterSkillEquipStatus? rejected = id == null ? CharacterSkillEquipStatus.InvalidCharacterId :
                key == null ? CharacterSkillEquipStatus.InvalidPersistentSkillKey :
                !CanEdit ? CharacterSkillEquipStatus.EquipNotAllowed :
                _party == null || !_party.Contains(id) ? CharacterSkillEquipStatus.NotPartyMember :
                !_definitions.ContainsKey(key) ? CharacterSkillEquipStatus.UnknownSkill :
                _definitions[key].OwnershipCategory != SkillOwnershipCategory.Film ? CharacterSkillEquipStatus.NotFilm :
                !_runtime.HasSharedFilm(key) ? CharacterSkillEquipStatus.NotAcquired : null;
            if (rejected.HasValue) return new CharacterSkillEquipResult(id, key, rejected.Value);
            return equip ? _runtime.TryEquip(id, key) : _runtime.TryUnequip(id, key);
        }

        private void ConfigureOwnership()
        {
            List<string> keys = new();
            foreach (KeyValuePair<string, SkillDefinitionSO> pair in _definitions)
                if (pair.Value.OwnershipCategory == SkillOwnershipCategory.Film) keys.Add(pair.Key);
            _runtime.ConfigureFilmKeys(keys);
        }
    }
}
