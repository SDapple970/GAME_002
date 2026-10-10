using System;
using System.Collections.Generic;
using Game.NonCombat.Save;

namespace Game.NonCombat.Progress
{
    /// <summary>Persistent skill ownership and equip state. Application boundaries own gameplay/state policy.</summary>
    public sealed class CharacterSkillRuntime
    {
        private readonly Dictionary<string, HashSet<string>> _acquiredByCharacter = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _equippedByCharacter = new(StringComparer.Ordinal);
        private readonly HashSet<string> _sharedFilms = new(StringComparer.Ordinal);
        private readonly HashSet<string> _knownFilmKeys = new(StringComparer.Ordinal);

        // Classification is supplied by the existing production registry at an application/integration boundary.
        internal void ConfigureFilmKeys(IEnumerable<string> knownFilmKeys)
        {
            _knownFilmKeys.Clear();
            if (knownFilmKeys != null)
                foreach (string rawKey in knownFilmKeys)
                {
                    string key = NormalizePersistentSkillKey(rawKey);
                    if (key != null) _knownFilmKeys.Add(key);
                }
            if (PromoteKnownLegacyFilms()) Changed?.Invoke();
        }

        private bool PromoteKnownLegacyFilms()
        {
            bool changed = false;
            foreach (HashSet<string> acquired in _acquiredByCharacter.Values)
                foreach (string key in acquired)
                    if (_knownFilmKeys.Contains(key)) changed |= _sharedFilms.Add(key);
            return changed;
        }

        public event Action Changed;

        public CharacterSkillAcquireResult TryAcquireSharedFilm(string persistentSkillKey)
        {
            string key = NormalizePersistentSkillKey(persistentSkillKey);
            if (key == null)
                return new CharacterSkillAcquireResult(null, null, CharacterSkillAcquireStatus.InvalidPersistentSkillKey);
            bool added = _sharedFilms.Add(key);
            if (added) Changed?.Invoke();
            return new CharacterSkillAcquireResult(null, key,
                added ? CharacterSkillAcquireStatus.Acquired : CharacterSkillAcquireStatus.AlreadyOwned);
        }

        public bool HasSharedFilm(string persistentSkillKey)
        {
            string key = NormalizePersistentSkillKey(persistentSkillKey);
            return key != null && _sharedFilms.Contains(key);
        }

        public IReadOnlyList<string> GetSharedFilms()
        {
            string[] keys = new List<string>(_sharedFilms).ToArray();
            Array.Sort(keys, StringComparer.Ordinal);
            return Array.AsReadOnly(keys);
        }

        public CharacterSkillAcquireResult TryAcquire(string characterId, string persistentSkillKey)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            if (normalizedCharacterId == null)
            {
                return new CharacterSkillAcquireResult(
                    null,
                    NormalizePersistentSkillKey(persistentSkillKey),
                    CharacterSkillAcquireStatus.InvalidCharacterId);
            }

            string normalizedSkillKey = NormalizePersistentSkillKey(persistentSkillKey);
            if (normalizedSkillKey == null)
            {
                return new CharacterSkillAcquireResult(
                    normalizedCharacterId,
                    null,
                    CharacterSkillAcquireStatus.InvalidPersistentSkillKey);
            }

            if (!_acquiredByCharacter.TryGetValue(normalizedCharacterId, out HashSet<string> acquired))
            {
                acquired = new HashSet<string>(StringComparer.Ordinal);
                _acquiredByCharacter.Add(normalizedCharacterId, acquired);
            }

            CharacterSkillAcquireStatus status = acquired.Add(normalizedSkillKey)
                ? CharacterSkillAcquireStatus.Acquired
                : CharacterSkillAcquireStatus.AlreadyOwned;
            if (_knownFilmKeys.Contains(normalizedSkillKey)) _sharedFilms.Add(normalizedSkillKey);
            if (status == CharacterSkillAcquireStatus.Acquired) Changed?.Invoke();
            return new CharacterSkillAcquireResult(normalizedCharacterId, normalizedSkillKey, status);
        }

        public bool HasAcquired(string characterId, string persistentSkillKey)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            string normalizedSkillKey = NormalizePersistentSkillKey(persistentSkillKey);
            return normalizedCharacterId != null &&
                   normalizedSkillKey != null &&
                   _acquiredByCharacter.TryGetValue(normalizedCharacterId, out HashSet<string> acquired) &&
                   acquired.Contains(normalizedSkillKey);
        }

        public IReadOnlyList<string> GetAcquiredSkills(string characterId)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            if (normalizedCharacterId == null ||
                !_acquiredByCharacter.TryGetValue(normalizedCharacterId, out HashSet<string> acquired))
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            string[] snapshot = new List<string>(acquired).ToArray();
            Array.Sort(snapshot, StringComparer.Ordinal);
            return Array.AsReadOnly(snapshot);
        }

        public CharacterSkillEquipResult TryEquip(string characterId, string persistentSkillKey)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            if (normalizedCharacterId == null)
                return new CharacterSkillEquipResult(null, NormalizePersistentSkillKey(persistentSkillKey), CharacterSkillEquipStatus.InvalidCharacterId);

            string normalizedSkillKey = NormalizePersistentSkillKey(persistentSkillKey);
            if (normalizedSkillKey == null)
                return new CharacterSkillEquipResult(normalizedCharacterId, null, CharacterSkillEquipStatus.InvalidPersistentSkillKey);

            if (!HasAcquired(normalizedCharacterId, normalizedSkillKey) && !HasSharedFilm(normalizedSkillKey))
                return new CharacterSkillEquipResult(normalizedCharacterId, normalizedSkillKey, CharacterSkillEquipStatus.NotAcquired);

            if (!_equippedByCharacter.TryGetValue(normalizedCharacterId, out List<string> equipped))
            {
                equipped = new List<string>();
                _equippedByCharacter.Add(normalizedCharacterId, equipped);
            }

            if (equipped.Contains(normalizedSkillKey))
                return new CharacterSkillEquipResult(normalizedCharacterId, normalizedSkillKey, CharacterSkillEquipStatus.AlreadyEquipped);

            equipped.Add(normalizedSkillKey);
            Changed?.Invoke();
            return new CharacterSkillEquipResult(normalizedCharacterId, normalizedSkillKey, CharacterSkillEquipStatus.Equipped);
        }

        public CharacterSkillEquipResult TryUnequip(string characterId, string persistentSkillKey)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            if (normalizedCharacterId == null)
                return new CharacterSkillEquipResult(null, NormalizePersistentSkillKey(persistentSkillKey), CharacterSkillEquipStatus.InvalidCharacterId);

            string normalizedSkillKey = NormalizePersistentSkillKey(persistentSkillKey);
            if (normalizedSkillKey == null)
                return new CharacterSkillEquipResult(normalizedCharacterId, null, CharacterSkillEquipStatus.InvalidPersistentSkillKey);

            if (!_equippedByCharacter.TryGetValue(normalizedCharacterId, out List<string> equipped) ||
                !equipped.Remove(normalizedSkillKey))
            {
                return new CharacterSkillEquipResult(normalizedCharacterId, normalizedSkillKey, CharacterSkillEquipStatus.NotEquipped);
            }

            if (equipped.Count == 0)
                _equippedByCharacter.Remove(normalizedCharacterId);

            Changed?.Invoke();
            return new CharacterSkillEquipResult(normalizedCharacterId, normalizedSkillKey, CharacterSkillEquipStatus.Unequipped);
        }

        public bool IsEquipped(string characterId, string persistentSkillKey)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            string normalizedSkillKey = NormalizePersistentSkillKey(persistentSkillKey);
            return normalizedCharacterId != null &&
                   normalizedSkillKey != null &&
                   _equippedByCharacter.TryGetValue(normalizedCharacterId, out List<string> equipped) &&
                   equipped.Contains(normalizedSkillKey);
        }

        public IReadOnlyList<string> GetEquippedSkills(string characterId)
        {
            string normalizedCharacterId = CharacterIdentity.Normalize(characterId);
            if (normalizedCharacterId == null ||
                !_equippedByCharacter.TryGetValue(normalizedCharacterId, out List<string> equipped))
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            return Array.AsReadOnly(equipped.ToArray());
        }

        public void Restore(CharacterSkillCollectionSaveData saveData)
        {
            _acquiredByCharacter.Clear();
            _equippedByCharacter.Clear();
            _sharedFilms.Clear();
            if (saveData?.sharedFilmSkillKeys != null)
                foreach (string rawKey in saveData.sharedFilmSkillKeys)
                {
                    string key = NormalizePersistentSkillKey(rawKey);
                    if (key != null) _sharedFilms.Add(key);
                }
            if (saveData?.characters == null)
            {
                Changed?.Invoke();
                return;
            }

            for (int i = 0; i < saveData.characters.Count; i++)
            {
                CharacterAcquiredSkillSaveData entry = saveData.characters[i];
                string characterId = CharacterIdentity.Normalize(entry?.characterId);
                if (characterId == null)
                    continue;

                if (!_acquiredByCharacter.TryGetValue(characterId, out HashSet<string> acquired))
                {
                    acquired = new HashSet<string>(StringComparer.Ordinal);
                    _acquiredByCharacter.Add(characterId, acquired);
                }

                if (entry.acquiredSkillKeys == null)
                    continue;

                for (int skillIndex = 0; skillIndex < entry.acquiredSkillKeys.Count; skillIndex++)
                {
                    string skillKey = NormalizePersistentSkillKey(entry.acquiredSkillKeys[skillIndex]);
                    if (skillKey != null)
                        acquired.Add(skillKey);
                }
            }

            PromoteKnownLegacyFilms();
            for (int i = 0; i < saveData.characters.Count; i++)
            {
                CharacterAcquiredSkillSaveData entry = saveData.characters[i];
                string characterId = CharacterIdentity.Normalize(entry?.characterId);
                if (characterId == null || entry.equippedSkillKeys == null)
                {
                    continue;
                }

                _acquiredByCharacter.TryGetValue(characterId, out HashSet<string> acquired);

                List<string> equipped = null;
                for (int skillIndex = 0; skillIndex < entry.equippedSkillKeys.Count; skillIndex++)
                {
                    string skillKey = NormalizePersistentSkillKey(entry.equippedSkillKeys[skillIndex]);
                    if (skillKey == null || !(acquired?.Contains(skillKey) ?? false) && !_sharedFilms.Contains(skillKey))
                        continue;

                    equipped ??= new List<string>();
                    if (!equipped.Contains(skillKey))
                        equipped.Add(skillKey);
                }

                if (equipped == null || equipped.Count == 0)
                    continue;

                if (_equippedByCharacter.TryGetValue(characterId, out List<string> existing))
                {
                    for (int skillIndex = 0; skillIndex < equipped.Count; skillIndex++)
                        if (!existing.Contains(equipped[skillIndex])) existing.Add(equipped[skillIndex]);
                }
                else
                {
                    _equippedByCharacter.Add(characterId, equipped);
                }
            }
            Changed?.Invoke();
        }

        public void Capture(CharacterSkillCollectionSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.characters ??= new List<CharacterAcquiredSkillSaveData>();
            saveData.characters.Clear();
            saveData.sharedFilmSkillKeys = new List<string>(_sharedFilms);
            saveData.sharedFilmSkillKeys.Sort(StringComparer.Ordinal);
            HashSet<string> charactersWithState = new(_acquiredByCharacter.Keys, StringComparer.Ordinal);
            charactersWithState.UnionWith(_equippedByCharacter.Keys);
            List<string> characterIds = new(charactersWithState);
            characterIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < characterIds.Count; i++)
            {
                string characterId = characterIds[i];
                _acquiredByCharacter.TryGetValue(characterId, out HashSet<string> acquired);
                List<string> keys = acquired != null ? new List<string>(acquired) : new List<string>();
                keys.Sort(StringComparer.Ordinal);
                List<string> equipped = _equippedByCharacter.TryGetValue(characterId, out List<string> equippedValues)
                    ? new List<string>(equippedValues)
                    : new List<string>();
                saveData.characters.Add(new CharacterAcquiredSkillSaveData
                {
                    characterId = characterId,
                    acquiredSkillKeys = keys,
                    equippedSkillKeys = equipped
                });
            }
        }

        public void Reset()
        {
            _acquiredByCharacter.Clear();
            _equippedByCharacter.Clear();
            _sharedFilms.Clear();
            Changed?.Invoke();
        }

        private static string NormalizePersistentSkillKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
