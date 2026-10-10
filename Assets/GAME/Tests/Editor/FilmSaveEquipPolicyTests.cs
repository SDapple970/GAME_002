using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;

namespace Game.Tests.Integration
{
    public sealed class FilmSaveEquipPolicyTests : FilmUniqueTestFixture
    {
        [Test]
        public void SharedAndCharacterEquip_RoundTripPreservesOrderUnknownKeysAndUnrelatedSections()
        {
            SkillDefinitionSO z = Skill("film.z", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO a = Skill("film.a", 12, SkillOwnershipCategory.Film);
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { z, a }, State, Entry);
            Participant.Runtime.TryAcquireSharedFilm(z.PersistentKey);
            Participant.Runtime.TryAcquireSharedFilm(a.PersistentKey);
            Participant.Runtime.TryAcquireSharedFilm("unknown.shared");
            Participant.Runtime.TryAcquire("hero.a", "unknown.legacy");
            Assert.That(app.TryEquipFilm("hero.a", z.PersistentKey).Changed, Is.True);
            Assert.That(app.TryEquipFilm("hero.a", a.PersistentKey).Changed, Is.True);
            Assert.That(app.TryEquipFilm("hero.b", a.PersistentKey).Changed, Is.True);
            GameSaveData save = new();
            save.currency.gold = 123;
            save.inventory.items.Add(new SaveIntEntry { id = "item.test", value = 2 });
            save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "hero.a", level = 20, experience = 17 });
            Participant.CaptureSaveData(save);
            GameSaveData migrated = Migrate(SaveSerializer.ToJson(save));
            Participant.ResetForNewGame();
            Participant.RestoreSaveData(migrated);
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.EqualTo(new[] { "film.a", "film.z", "unknown.shared" }));
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.a"), Is.EqualTo(new[] { "film.z", "film.a" }));
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.b"), Is.EqualTo(new[] { "film.a" }));
            Assert.That(Participant.Runtime.HasAcquired("hero.a", "unknown.legacy"), Is.True);
            Assert.That(migrated.currency.gold, Is.EqualTo(123));
            Assert.That(migrated.inventory.items.Single().value, Is.EqualTo(2));
            Assert.That(migrated.progression.characters.Single().level, Is.EqualTo(20));
            Assert.That(migrated.progression.characters.Single().experience, Is.EqualTo(17));
            Assert.That(app.TryUnequipFilm("hero.a", "film.z").Changed, Is.True);
            Assert.That(Participant.Runtime.HasSharedFilm("film.z"), Is.True);
        }

        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void OldSchemas_PreserveLegacyDataAndPromoteOnlyKnownFilmsAtRuntime(int schema)
        {
            GameSaveData old = new();
            old.header.schemaVersion = schema;
            old.currency.gold = 19;
            old.party.memberIds.Add("hero.a");
            old.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = "hero.a",
                acquiredSkillKeys = new List<string> { "film.known", "unique.known", "unknown", "legacy" },
                equippedSkillKeys = new List<string> { "legacy", "film.known" }
            });
            GameSaveData migrated = Migrate(SaveSerializer.ToJson(old));
            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.characterSkills.sharedFilmSkillKeys, Is.Empty);
            Assert.That(migrated.characterSkills.characters.Single().equippedSkillKeys, Is.EqualTo(new[] { "legacy", "film.known" }));
            SkillDefinitionSO film = Skill("film.known", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO unique = Skill("unique.known", 12, SkillOwnershipCategory.Unique, "hero.a");
            new FilmEquipApplication(Participant.Runtime, Party, new[] { film, unique }, State, Entry);
            Participant.RestoreSaveData(migrated);
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.EqualTo(new[] { "film.known" }));
            Assert.That(Participant.Runtime.GetAcquiredSkills("hero.a"), Is.EqualTo(new[] { "film.known", "legacy", "unique.known", "unknown" }));
            GameSaveData captured = new();
            Participant.CaptureSaveData(captured);
            string first = SaveSerializer.ToJson(captured);
            Participant.RestoreSaveData(Migrate(SaveSerializer.ToJson(captured)));
            Participant.CaptureSaveData(captured);
            Assert.That(SaveSerializer.ToJson(captured), Is.EqualTo(first));
            Assert.That(migrated.currency.gold, Is.EqualTo(19));
            Assert.That(migrated.party.memberIds, Is.EqualTo(new[] { "hero.a" }));
        }

        [Test]
        public void EquipPolicy_RejectsUnknownUniqueAndUnownedCharactersAndResetClearsAllState()
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO unique = Skill("unique.test", 12, SkillOwnershipCategory.Unique, "hero.a");
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { film, unique }, State, Entry);
            Assert.That(app.TryEquipFilm("hero.a", film.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.NotAcquired));
            Assert.That(app.TryEquipFilm("hero.a", "unknown").Status, Is.EqualTo(CharacterSkillEquipStatus.UnknownSkill));
            Assert.That(app.TryEquipFilm("hero.a", unique.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.NotFilm));
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            Assert.That(app.TryEquipFilm("outsider", film.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.NotPartyMember));
            app.TryEquipFilm("hero.a", film.PersistentKey);
            Participant.ResetForNewGame();
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.Empty);
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.a"), Is.Empty);
        }

        [TestCase(GameState.CombatTransition)]
        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        [TestCase(GameState.Dialogue)]
        [TestCase(GameState.Choice)]
        [TestCase(GameState.Reward)]
        [TestCase(GameState.Cutscene)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.Title)]
        [TestCase(GameState.Boot)]
        public void EquipPolicy_RejectsBlockedGlobalStates(GameState blocked)
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { film }, State, Entry);
            app.TryEquipFilm("hero.a", film.PersistentKey);
            typeof(GameStateMachine).GetField("<Current>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(State, blocked);
            Assert.That(app.TryEquipFilm("hero.b", film.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.EquipNotAllowed));
            Assert.That(app.TryUnequipFilm("hero.a", film.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.EquipNotAllowed));
            Assert.That(Participant.Runtime.IsEquipped("hero.a", film.PersistentKey), Is.True);
        }

        [Test]
        public void ActiveCombat_BlocksEquipAndKeepsSnapshotEvenAfterPureRuntimeChanges()
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            Register(film);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { film }, State, Entry);
            app.TryEquipFilm("hero.a", film.PersistentKey);
            Assert.That(Entry.StartCombat(Request("hero.a")), Is.True);
            Assert.That(app.TryUnequipFilm("hero.a", film.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.EquipNotAllowed));
            Participant.Runtime.TryUnequip("hero.a", film.PersistentKey); // Compatibility/domain API has no global-state responsibility.
            Assert.That(Entry.ActiveSession.Allies.Single().Skills.Single().Id.Value, Is.EqualTo(11));
        }

        private static GameSaveData Migrate(string json)
        {
            Type type = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            object[] args = { json, null, false, null };
            Assert.That((bool)type.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }
    }
}
