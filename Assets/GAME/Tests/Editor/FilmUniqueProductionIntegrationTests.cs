using System;
using System.Linq;
using Game.Combat.Data;
using Game.Core;
using Game.EditorTools;
using Game.NonCombat.Progress;
using Game.Tests.Integration;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class FilmUniqueProductionIntegrationTests : FilmUniqueTestFixture
    {
        [TestCase(SaveLoadService.OperationState.WaitingForScene)]
        [TestCase(SaveLoadService.OperationState.Restoring)]
        public void SceneStartup_DoesNotReleaseLoadingDuringRestore(SaveLoadService.OperationState operation)
        {
            SaveLoadService save = Component<SaveLoadService>();
            typeof(SaveLoadService).GetField("_operationState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(save, operation);
            Flow.BeginLoading();
            GameObject go = Actor();
            RuntimeBootstrapper bootstrap = go.AddComponent<RuntimeBootstrapper>();
            Invoke(bootstrap, "ApplyInitialStateForActiveScene");
            Assert.That(State.Current, Is.EqualTo(GameState.Loading));
            Assert.That(bootstrap.HasAppliedInitialStateFor(UnityEngine.SceneManagement.SceneManager.GetActiveScene()), Is.True);
        }

        [TestCase(4, 0, 5)]
        [TestCase(5, 1, 10)]
        [TestCase(9, 1, 10)]
        [TestCase(10, 2, 20)]
        [TestCase(20, 3, 40)]
        [TestCase(40, 4, 0)]
        [TestCase(99, 4, 0)]
        public void AuthoredBoundary_RealPanelAndCombatSnapshotAgree(int level, int unlocked, int next)
        {
            SkillDefinitionSO film = Skill("validation.17c6.film", 1010, SkillOwnershipCategory.Film);
            film.displayName = "Film";
            SkillDefinitionSO[] unique = Enumerable.Range(0, 6).Select(i => Skill($"validation.17c6.unique.{i}", 1020 + i, SkillOwnershipCategory.Unique, "hero.a")).ToArray();
            foreach (SkillDefinitionSO skill in unique) skill.displayName = "Unique " + skill.skillId;
            SkillDefinitionSO[] registry = new[] { film }.Concat(unique).ToArray();
            Register(registry);
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests("hero.a", CreateCharacter("hero.a", level, unique), CreateCharacter("hero.b", level, Array.Empty<SkillDefinitionSO>()));
            FilmEquipApplication application = new(Participant.Runtime, Party, registry, State, Entry);
            Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
            Assert.That(application.TryEquipFilm("hero.a", film.PersistentKey).Changed, Is.True);
            GameObject panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCharacterSkillUISetup.PanelPath));
            panel.SetActive(true);
            CharacterSkillPanelView view = panel.GetComponent<CharacterSkillPanelView>();
            using CharacterSkillPresenter presenter = new(view, application, Participant.Runtime, Party, progression, State, registry);
            try
            {
                Assert.That(view.LastModel.UniqueSkills.Count(item => item.IsUnlocked), Is.EqualTo(unlocked));
                Assert.That(view.LastModel.NextUniqueUnlockLevel, Is.EqualTo(next == 0 ? (int?)null : next));
                Assert.That(view.LastModel.UniqueSkills.Skip(4).All(item => item.Status == UniqueSkillAvailabilityStatus.Unconfigured && !item.IsUnlocked), Is.True);
                string text = string.Join("|", panel.GetComponentsInChildren<TMP_Text>().Select(item => item.text));
                Assert.That(text, Does.Contain("조건 미정").And.Not.Contain("validation.17c6"));
                Assert.That(application.TryEquipFilm("hero.a", unique[0].PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.NotFilm));
                Assert.That(Participant.Runtime.GetSharedFilms(), Is.EqualTo(new[] { film.PersistentKey }));
                presenter.SelectCharacter("missing.character");
                Assert.That(view.LastModel.SelectedCharacterId, Is.EqualTo("hero.a"));
                presenter.SelectCharacter("hero.b");
                Assert.That(view.LastModel.UniqueSkills, Is.Empty);
                Assert.That(Entry.StartCombat(Request("hero.a", "hero.b")), Is.True);
                Assert.That(Entry.ActiveSession.Allies[0].Skills.Select(item => item.Id.Value), Is.EqualTo(new[] { 1010 }.Concat(Enumerable.Range(1020, unlocked))));
                Assert.That(Entry.ActiveSession.Allies[1].Skills.Any(item => item.Id.Value >= 1020), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
        }

        internal CharacterProgressionDefinitionSO CreateCharacter(string id, int level, SkillDefinitionSO[] skills)
        {
            CharacterProgressionDefinitionSO definition = Asset<CharacterProgressionDefinitionSO>();
            ConfigureCharacter(definition, id, level, skills, 10);
            return definition;
        }

        internal static void ConfigureCharacter(CharacterProgressionDefinitionSO definition, string id, int level, SkillDefinitionSO[] unique, int experience)
        {
            SerializedObject data = new(definition);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("startingLevel").intValue = level;
            data.FindProperty("maximumLevel").intValue = 99;
            data.FindProperty("experienceRequiredByLevel").arraySize = 1;
            data.FindProperty("experienceRequiredByLevel").GetArrayElementAtIndex(0).intValue = experience;
            SerializedProperty entries = data.FindProperty("uniqueSkillUnlocks"); entries.arraySize = unique.Length;
            int[] levels = { 5, 10, 20, 40, 0, 0 };
            for (int i = 0; i < unique.Length; i++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("enabled").boolValue = true;
                item.FindPropertyRelative("persistentSkillKey").stringValue = unique[i].PersistentKey;
                item.FindPropertyRelative("condition").enumValueIndex = i < 4 ? 1 : 0;
                item.FindPropertyRelative("unlockLevel").intValue = levels[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
