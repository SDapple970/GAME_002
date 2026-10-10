using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.EditorTools;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Integration
{
    public sealed class UniqueSkillUnlockTests : FilmUniqueTestFixture
    {
        [TestCase(4, 0)]
        [TestCase(5, 1)]
        [TestCase(10, 2)]
        [TestCase(20, 3)]
        [TestCase(40, 4)]
        [TestCase(99, 4)]
        public void AuthoredLevelsUnlockInOrderAndTwoPendingConditionsNeverUnlock(int level, int count)
        {
            SkillDefinitionSO[] skills = Enumerable.Range(1, 6).Select(i => Skill($"unique.{i}", 20 + i, SkillOwnershipCategory.Unique, "hero.a")).ToArray();
            CharacterProgressionDefinitionSO definition = Character("hero.a", level, skills, new[] { 5, 10, 20, 40, 0, 0 });
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, definition);
            var available = UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills);
            Assert.That(available.Count(skill => skill.IsUnlocked), Is.EqualTo(count));
            Assert.That(available.Select(skill => skill.PersistentSkillKey), Is.EqualTo(skills.Select(skill => skill.PersistentKey)));
            Assert.That(available.Skip(4).All(skill => skill.Status == UniqueSkillAvailabilityStatus.Unconfigured), Is.True);
            Assert.That(available.Skip(4).All(skill => skill.RequiredLevel == null), Is.True);
            Assert.That(Participant.Runtime.GetSharedFilms(), Is.Empty);
            Assert.That(CharacterUniqueSkillValidator.CollectIssues(new[] { definition }, skills), Is.Empty);
        }

        [Test]
        public void ProgressionJumpLoadAndNewGameReevaluateWithoutSavingUnlockState()
        {
            SkillDefinitionSO[] skills = Enumerable.Range(1, 4).Select(i => Skill($"unique.{i}", 20 + i, SkillOwnershipCategory.Unique, "hero.a")).ToArray();
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, Character("hero.a", 4, skills, new[] { 5, 10, 20, 40 }));
            progression.ApplyExperience("hero.a", 10);
            Assert.That(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills).Count(skill => skill.IsUnlocked), Is.EqualTo(1));
            GameSaveData save = new();
            progression.CaptureSaveData(save);
            progression.ApplyExperience("hero.a", 350);
            Assert.That(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills).Count(skill => skill.IsUnlocked), Is.EqualTo(4));
            progression.RestoreSaveData(SaveSerializer.FromGameSaveJson(SaveSerializer.ToJson(save)));
            Assert.That(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills).Count(skill => skill.IsUnlocked), Is.EqualTo(1));
            Assert.That(UniqueSkillUnlockEvaluator.GetNextUnlockLevel(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills)), Is.EqualTo(10));
            progression.ResetForNewGame();
            Assert.That(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills).All(skill => !skill.IsUnlocked), Is.True);
            Assert.That(Participant.Runtime.GetAcquiredSkills("hero.a"), Is.Empty);
        }

        [Test]
        public void WrongOwnerNonUniqueAndUnsupportedConditionsAreRejected()
        {
            SkillDefinitionSO wrongOwner = Skill("unique.other", 21, SkillOwnershipCategory.Unique, "hero.b");
            SkillDefinitionSO film = Skill("film.a", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO invalidLevel = Skill("unique.invalid", 22, SkillOwnershipCategory.Unique, "hero.a");
            CharacterProgressionDefinitionSO definition = Character("hero.a", 99, new[] { wrongOwner, film, invalidLevel }, new[] { 5, 5, 1 });
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, definition);
            var available = UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, new[] { wrongOwner, film, invalidLevel });
            Assert.That(available.All(skill => !skill.IsUnlocked), Is.True);
            Assert.That(CharacterUniqueSkillValidator.CollectIssues(new[] { definition }, new[] { wrongOwner, film, invalidLevel }), Has.Some.Contains("wrong-owner"));
            Assert.That(CharacterUniqueSkillValidator.CollectIssues(new[] { definition }, new[] { wrongOwner, film, invalidLevel }), Has.Some.Contains("unsupported"));
        }

        [Test]
        public void FilmAndOwnUniqueMergeIntoIndependentImmutableSnapshotsWithoutSlotPolicy()
        {
            SkillDefinitionSO filmA = Skill("film.a", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO filmB = Skill("film.b", 12, SkillOwnershipCategory.Film);
            SkillDefinitionSO uniqueA = Skill("unique.a", 21, SkillOwnershipCategory.Unique, "hero.a");
            SkillDefinitionSO uniqueB = Skill("unique.b", 22, SkillOwnershipCategory.Unique, "hero.b");
            Register(filmA, filmB, uniqueA, uniqueB);
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, Character("hero.a", 5, new[] { uniqueA }, new[] { 5 }),
                Character("hero.b", 4, new[] { uniqueB }, new[] { 5 }));
            Participant.Runtime.TryAcquireSharedFilm(filmA.PersistentKey);
            Participant.Runtime.TryAcquireSharedFilm(filmB.PersistentKey);
            Participant.Runtime.TryEquip("hero.a", filmA.PersistentKey);
            Participant.Runtime.TryEquip("hero.b", filmB.PersistentKey);
            Participant.Runtime.TryAcquire("hero.b", uniqueA.PersistentKey);
            Participant.Runtime.TryEquip("hero.b", uniqueA.PersistentKey); // Old raw state cannot bypass Unique ownership.
            Assert.That(Entry.StartCombat(Request("hero.a", "hero.b")), Is.True);
            Assert.That(Entry.ActiveSession.Allies[0].Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 11, 21 }));
            Assert.That(Entry.ActiveSession.Allies[1].Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 12 }));
            Participant.Runtime.TryUnequip("hero.a", filmA.PersistentKey);
            progression.ResetForNewGame();
            Assert.That(Entry.ActiveSession.Allies[0].Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 11, 21 }));
        }

        [Test]
        public void UniqueWithoutFilmRetainsCompatibilityAndSerializedIdsCannotBypassUnlock()
        {
            SkillDefinitionSO basic = Skill("legacy.basic", 1);
            SkillDefinitionSO unique = Skill("unique.a", 21, SkillOwnershipCategory.Unique, "hero.a");
            Register(basic, unique);
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, Character("hero.a", 4, new[] { unique }, new[] { 5 }));
            var request = Request("hero.a");
            CombatSkillLoadoutComponent loadout = request.AllyFieldObjects[0].AddComponent<CombatSkillLoadoutComponent>();
            SerializedObject data = new(loadout);
            data.FindProperty("skillIds").arraySize = 1;
            data.FindProperty("skillIds").GetArrayElementAtIndex(0).intValue = unique.skillId;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Entry.StartCombat(request), Is.True);
            Assert.That(Entry.ActiveSession.Allies[0].Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 1 }));
            Invoke(Entry, "ForceFinishCombat", CombatEndReason.Abort);
            Flow.EnterExploration();
            progression.ApplyExperience("hero.a", 10);
            Assert.That(Entry.StartCombat(request), Is.True);
            Assert.That(Entry.ActiveSession.Allies[0].Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 1, 21 }));
        }

        [Test]
        public void ExcessAuthoringIsValidatedAndNeverSilentlyCreatesExtraUniqueSkills()
        {
            SkillDefinitionSO[] skills = Enumerable.Range(1, 7).Select(i => Skill($"unique.{i}", 20 + i, SkillOwnershipCategory.Unique, "hero.a")).ToArray();
            CharacterProgressionDefinitionSO definition = Character("hero.a", 99, skills, Enumerable.Repeat(5, 7).ToArray());
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, definition);
            Assert.That(CharacterUniqueSkillValidator.CollectIssues(new[] { definition }, skills), Has.Some.Contains("six"));
            Assert.That(UniqueSkillUnlockEvaluator.GetAvailability("hero.a", progression, skills), Is.Empty);
        }

        internal CharacterProgressionDefinitionSO Character(string id, int startingLevel, SkillDefinitionSO[] skills, int[] levels)
        {
            CharacterProgressionDefinitionSO definition = Asset<CharacterProgressionDefinitionSO>();
            SerializedObject data = new(definition);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("startingLevel").intValue = startingLevel;
            data.FindProperty("maximumLevel").intValue = 99;
            data.FindProperty("experienceRequiredByLevel").arraySize = 1;
            data.FindProperty("experienceRequiredByLevel").GetArrayElementAtIndex(0).intValue = 10;
            SerializedProperty entries = data.FindProperty("uniqueSkillUnlocks");
            entries.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("enabled").boolValue = true;
                entry.FindPropertyRelative("persistentSkillKey").stringValue = skills[i].PersistentKey;
                entry.FindPropertyRelative("condition").enumValueIndex = levels[i] == 0 ? 0 : 1;
                entry.FindPropertyRelative("unlockLevel").intValue = levels[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
