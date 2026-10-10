using System;
using System.Collections.Generic;
using Game.Combat.Data;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;

namespace Game.UI
{
    /// <summary>Content-only presenter; it never activates UI roots or writes GameState.</summary>
    public sealed class CharacterSkillPresenter : IDisposable
    {
        private readonly ICharacterSkillView _view;
        private readonly FilmEquipApplication _application;
        private readonly CharacterSkillRuntime _runtime;
        private readonly PartyRuntime _party;
        private readonly CharacterProgressionService _progression;
        private readonly GameStateMachine _state;
        private readonly List<SkillDefinitionSO> _definitions;
        private string _selectedCharacterId;
        private bool _refreshing;
        private bool _disposed;

        public CharacterSkillPresenter(ICharacterSkillView view, FilmEquipApplication application,
            CharacterSkillRuntime runtime, PartyRuntime party, CharacterProgressionService progression,
            GameStateMachine state, IEnumerable<SkillDefinitionSO> definitions)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _party = party ?? throw new ArgumentNullException(nameof(party));
            _progression = progression;
            _state = state;
            _definitions = new List<SkillDefinitionSO>(definitions ?? Array.Empty<SkillDefinitionSO>());
            _view.CharacterSelected += SelectCharacter;
            _view.EquipRequested += Equip;
            _view.UnequipRequested += Unequip;
            _runtime.Changed += Refresh;
            _party.Changed += HandlePartyChanged;
            _party.Refreshed += Refresh;
            if (_progression != null)
            {
                _progression.ProgressionChanged += HandleProgressionChanged;
                _progression.Refreshed += Refresh;
            }
            if (_state != null) _state.OnStateChanged += HandleStateChanged;
            Refresh();
        }

        public void Refresh()
        {
            if (_disposed || _refreshing) return;
            _refreshing = true;
            try
            {
                IReadOnlyList<string> characters = _party.Members;
                if (!_party.Contains(_selectedCharacterId))
                    _selectedCharacterId = _party.LeaderCharacterId ?? (characters.Count > 0 ? characters[0] : null);
                IReadOnlyList<string> available = _application.GetAvailableFilms();
                HashSet<string> filmKeys = new(available, StringComparer.Ordinal);
                List<string> equipped = new();
                foreach (string key in _runtime.GetEquippedSkills(_selectedCharacterId))
                    if (filmKeys.Contains(key)) equipped.Add(key);
                List<FilmSkillViewItem> films = new();
                foreach (string key in available)
                {
                    SkillDefinitionSO definition = _definitions.Find(item => item != null && item.PersistentKey?.Trim() == key);
                    string name = string.IsNullOrWhiteSpace(definition?.displayName) ? "이름 없는 필름" : definition.displayName;
                    films.Add(new FilmSkillViewItem(key, name, equipped.Contains(key)));
                }
                IReadOnlyList<UniqueSkillAvailability> unique = UniqueSkillUnlockEvaluator.GetAvailability(
                    _selectedCharacterId, _progression, _definitions);
                _view.Render(new CharacterSkillViewModel(characters, _selectedCharacterId, films, equipped, unique,
                    _application.CanEdit && _party.Contains(_selectedCharacterId)));
            }
            finally { _refreshing = false; }
        }

        public void SelectCharacter(string characterId)
        {
            if (_disposed || !_party.Contains(characterId)) return;
            _selectedCharacterId = CharacterIdentity.Normalize(characterId);
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.CharacterSelected -= SelectCharacter;
            _view.EquipRequested -= Equip;
            _view.UnequipRequested -= Unequip;
            _runtime.Changed -= Refresh;
            _party.Changed -= HandlePartyChanged;
            _party.Refreshed -= Refresh;
            if (_progression != null)
            {
                _progression.ProgressionChanged -= HandleProgressionChanged;
                _progression.Refreshed -= Refresh;
            }
            if (_state != null) _state.OnStateChanged -= HandleStateChanged;
        }

        private void Equip(string key) => _view.ShowEquipResult(_application.TryEquipFilm(_selectedCharacterId, key));
        private void Unequip(string key) => _view.ShowEquipResult(_application.TryUnequipFilm(_selectedCharacterId, key));
        private void HandlePartyChanged(PartyMutationResult _) => Refresh();
        private void HandleProgressionChanged(ExperienceApplyResult _) => Refresh();
        private void HandleStateChanged(GameState _, GameState __) => Refresh();
    }
}
