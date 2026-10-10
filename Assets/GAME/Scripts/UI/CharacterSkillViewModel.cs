using System;
using System.Collections.Generic;
using Game.NonCombat.Progress;

namespace Game.UI
{
    public sealed class FilmSkillViewItem
    {
        public string PersistentSkillKey { get; }
        public string DisplayName { get; }
        public bool IsEquipped { get; }

        internal FilmSkillViewItem(string key, string displayName, bool equipped)
        {
            PersistentSkillKey = key;
            DisplayName = displayName;
            IsEquipped = equipped;
        }
    }

    public sealed class CharacterSkillViewModel
    {
        public IReadOnlyList<string> CharacterIds { get; }
        public string SelectedCharacterId { get; }
        public IReadOnlyList<FilmSkillViewItem> SharedFilms { get; }
        public IReadOnlyList<string> EquippedFilmKeys { get; }
        public IReadOnlyList<UniqueSkillAvailability> UniqueSkills { get; }
        public int? NextUniqueUnlockLevel { get; }
        public bool CanEdit { get; }
        public bool CanShow => CanEdit;

        internal CharacterSkillViewModel(IReadOnlyList<string> characters, string selected,
            List<FilmSkillViewItem> films, List<string> equipped, IReadOnlyList<UniqueSkillAvailability> unique, bool canEdit)
        {
            CharacterIds = new List<string>(characters).AsReadOnly();
            SelectedCharacterId = selected;
            SharedFilms = films.AsReadOnly();
            EquippedFilmKeys = equipped.AsReadOnly();
            UniqueSkills = new List<UniqueSkillAvailability>(unique).AsReadOnly();
            NextUniqueUnlockLevel = UniqueSkillUnlockEvaluator.GetNextUnlockLevel(UniqueSkills);
            CanEdit = canEdit;
        }
    }
}
