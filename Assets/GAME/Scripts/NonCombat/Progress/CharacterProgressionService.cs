using System;
using System.Collections.Generic;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    public sealed class CharacterProgressionService : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        private sealed class State { public int Level; public int Experience; public CharacterProgressionDefinitionSO Definition; }

        public static CharacterProgressionService Instance { get; private set; }
        [SerializeField] private string defaultRewardTargetId;
        [SerializeField] private List<CharacterProgressionDefinitionSO> definitions = new();
        private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);

        public string DefaultRewardTargetId => NormalizeId(defaultRewardTargetId);
        public event Action<ExperienceApplyResult> ProgressionChanged;
        public event Action Refreshed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; BuildAuthoredStates(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool TryGetState(string characterId, out int level, out int experience)
        {
            if (_states.TryGetValue(NormalizeId(characterId) ?? string.Empty, out State state)) { level = state.Level; experience = state.Experience; return true; }
            level = 0; experience = 0; return false;
        }

        public ExperienceApplyResult ApplyExperience(string characterId, int amount)
        {
            string id = NormalizeId(characterId);
            if (id == null) return Empty(id, amount, ExperienceApplyStatus.InvalidCharacterId);
            if (amount <= 0) return Empty(id, amount, ExperienceApplyStatus.InvalidAmount);
            if (!_states.TryGetValue(id, out State state)) return Empty(id, amount, ExperienceApplyStatus.UnresolvedCharacter);
            int previousLevel = state.Level; int previousExperience = state.Experience;
            if (state.Definition == null) return Result(id, amount, 0, state, previousLevel, previousExperience, ExperienceApplyStatus.Pending);
            if (state.Level >= state.Definition.MaximumLevel) return Result(id, amount, 0, state, previousLevel, previousExperience, ExperienceApplyStatus.Settled);
            if (state.Experience > int.MaxValue - amount) return Result(id, amount, 0, state, previousLevel, previousExperience, ExperienceApplyStatus.OverflowPrevented);

            int remaining = amount;
            while (remaining > 0 && state.Level < state.Definition.MaximumLevel)
            {
                if (!state.Definition.TryGetRequiredExperience(state.Level, out int required))
                    return Result(id, amount, amount - remaining, state, previousLevel, previousExperience, ExperienceApplyStatus.InvalidDefinition);
                int needed = required - state.Experience;
                if (remaining < needed) { state.Experience += remaining; remaining = 0; break; }
                remaining -= needed; state.Level++; state.Experience = 0;
            }

            int applied = amount - remaining;
            // EXP that cannot change a valid max-level target is terminally settled.
            ExperienceApplyResult result = Result(id, amount, applied, state, previousLevel, previousExperience, ExperienceApplyStatus.Settled);
            if (applied > 0) ProgressionChanged?.Invoke(result);
            return result;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.progression ??= new ProgressionSaveData();
            saveData.progression.characters.Clear();
            foreach (KeyValuePair<string, State> pair in _states)
                saveData.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = pair.Key, level = pair.Value.Level, experience = pair.Value.Experience });
            saveData.progression.characters.Sort((a, b) => string.CompareOrdinal(a.characterId, b.characterId));
            saveData.party ??= new PartySaveData();
            for (int i = 0; i < saveData.party.memberLevels.Count; i++)
            {
                SaveIntEntry mirror = saveData.party.memberLevels[i];
                string id = NormalizeId(mirror?.id);
                if (id != null && _states.TryGetValue(id, out State state)) mirror.value = state.Level;
            }
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            BuildAuthoredStates();
            Dictionary<string, CharacterProgressionStateSaveData> normalized = new(StringComparer.Ordinal);
            if (saveData?.progression?.characters != null)
                foreach (CharacterProgressionStateSaveData entry in saveData.progression.characters)
                {
                    string id = NormalizeId(entry?.characterId);
                    if (id == null) continue;
                    if (!normalized.TryGetValue(id, out CharacterProgressionStateSaveData current) || entry.level > current.level || entry.level == current.level && entry.experience > current.experience)
                        normalized[id] = entry;
                }
            foreach (KeyValuePair<string, CharacterProgressionStateSaveData> pair in normalized)
            {
                CharacterProgressionDefinitionSO definition = FindDefinition(pair.Key);
                int maximum = definition != null ? definition.MaximumLevel : int.MaxValue;
                _states[pair.Key] = new State { Definition = definition, Level = Mathf.Clamp(pair.Value.level, 1, maximum), Experience = Mathf.Max(0, pair.Value.experience) };
            }
            Refreshed?.Invoke();
        }

        private void BuildAuthoredStates()
        {
            _states.Clear();
            foreach (CharacterProgressionDefinitionSO definition in definitions)
            {
                string id = NormalizeId(definition != null ? definition.CharacterId : null);
                if (id != null && !_states.ContainsKey(id)) _states.Add(id, new State { Definition = definition, Level = definition.StartingLevel, Experience = 0 });
            }
        }

        private CharacterProgressionDefinitionSO FindDefinition(string id)
        {
            foreach (CharacterProgressionDefinitionSO definition in definitions)
                if (definition != null && string.Equals(NormalizeId(definition.CharacterId), id, StringComparison.Ordinal)) return definition;
            return null;
        }

        internal void ConfigureForTests(string defaultTargetId, params CharacterProgressionDefinitionSO[] authoredDefinitions)
        { defaultRewardTargetId = defaultTargetId; definitions = authoredDefinitions != null ? new List<CharacterProgressionDefinitionSO>(authoredDefinitions) : new List<CharacterProgressionDefinitionSO>(); BuildAuthoredStates(); }

        public static string NormalizeId(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static ExperienceApplyResult Empty(string id, int amount, ExperienceApplyStatus status) => new(id, amount, 0, 0, 0, 0, 0, 0, status);
        private static ExperienceApplyResult Result(string id, int amount, int applied, State state, int previousLevel, int previousExperience, ExperienceApplyStatus status) => new(id, amount, applied, previousLevel, state.Level, previousExperience, state.Experience, state.Level - previousLevel, status);
    }
}
