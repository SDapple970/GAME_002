using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Player;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class FilmUniqueUIIntegrationTests : FilmUniqueTestFixture
    {
        private sealed class View : ICharacterSkillView
        {
            public event Action<string> CharacterSelected;
            public event Action<string> EquipRequested;
            public event Action<string> UnequipRequested;
            public CharacterSkillViewModel Model;
            public CharacterSkillEquipResult LastResult;
            public int RenderCount;
            public void Render(CharacterSkillViewModel model) { Model = model; RenderCount++; }
            public void ShowEquipResult(CharacterSkillEquipResult result) => LastResult = result;
            public void Select(string id) => CharacterSelected?.Invoke(id);
            public void Equip(string key) => EquipRequested?.Invoke(key);
            public void Unequip(string key) => UnequipRequested?.Invoke(key);
        }

        [Test]
        public void ViewContract_SelectsEquipsRefreshesAndUnsubscribesWithoutOwningRoutes()
        {
            SkillDefinitionSO film = Skill("film.test", 11, SkillOwnershipCategory.Film);
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { film }, State, Entry);
            View view = new();
            CharacterSkillPresenter presenter = new(view, app, Participant.Runtime, Party, null, State, new[] { film });
            try
            {
                Assert.That(view.Model.SelectedCharacterId, Is.EqualTo("hero.a"));
                Participant.Runtime.TryAcquireSharedFilm(film.PersistentKey);
                view.Equip(film.PersistentKey);
                Assert.That(view.Model.SharedFilms.Single().IsEquipped, Is.True);
                CharacterSkillViewModel old = view.Model;
                view.Select("hero.b");
                Assert.That(view.Model.EquippedFilmKeys, Is.Empty);
                view.Equip(film.PersistentKey);
                Assert.That(Participant.Runtime.IsEquipped("hero.a", film.PersistentKey), Is.True);
                Assert.That(view.Model.EquippedFilmKeys, Is.EqualTo(new[] { film.PersistentKey }));
                view.Unequip(film.PersistentKey);
                Assert.That(view.Model.EquippedFilmKeys, Is.Empty);
                Assert.That(old.SharedFilms.Single().IsEquipped, Is.True);
                Assert.Throws<NotSupportedException>(() => ((IList<string>)old.CharacterIds).Clear());
                Flow.BeginDialogue();
                Assert.That(view.Model.CanEdit, Is.False);
                view.Equip(film.PersistentKey);
                Assert.That(view.LastResult.Status, Is.EqualTo(CharacterSkillEquipStatus.EquipNotAllowed));
                Assert.That(view.Model.CanShow, Is.False);
                Flow.EnterExploration();
                Assert.That(view.Model.CanEdit, Is.True);
                Assert.That(view.Model.CanShow, Is.True);
                presenter.Dispose();
                int renders = view.RenderCount;
                Participant.ResetForNewGame();
                view.Select("hero.a");
                view.Equip(film.PersistentKey);
                Assert.That(view.RenderCount, Is.EqualTo(renders));
            }
            finally { presenter.Dispose(); }
        }

        [Test]
        public void ViewContract_ShowsUniqueLocksAndNextLevelAndReevaluatesAfterLevelLoadAndReset()
        {
            SkillDefinitionSO unique = Skill("unique.a", 21, SkillOwnershipCategory.Unique, "hero.a");
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, Character(unique, 4));
            View view = new();
            FilmEquipApplication app = new(Participant.Runtime, Party, new[] { unique }, State, Entry);
            using CharacterSkillPresenter presenter = new(view, app, Participant.Runtime, Party, progression, State, new[] { unique });
            Assert.That(view.Model.UniqueSkills.Single().IsUnlocked, Is.False);
            Assert.That(view.Model.NextUniqueUnlockLevel, Is.EqualTo(5));
            progression.ApplyExperience("hero.a", 10);
            Assert.That(view.Model.UniqueSkills.Single().IsUnlocked, Is.True);
            Assert.That(view.Model.NextUniqueUnlockLevel, Is.Null);
            Assert.That(view.Model.SharedFilms, Is.Empty);
            progression.ResetForNewGame();
            Assert.That(view.Model.UniqueSkills.Single().IsUnlocked, Is.False);
            Party.RemoveMember("hero.a");
            Assert.That(view.Model.SelectedCharacterId, Is.EqualTo("hero.b"));
            Assert.That(view.Model.UniqueSkills, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContactAndFieldAttack_UseSameFilmUniqueSnapshotAndVictorySavesSharedAcquisition(bool fieldAttack)
        {
            SkillDefinitionSO basic = Skill("legacy.basic", 1);
            SkillDefinitionSO equipped = Skill("film.equipped", 11, SkillOwnershipCategory.Film);
            SkillDefinitionSO awarded = Skill("film.awarded", 12, SkillOwnershipCategory.Film);
            SkillDefinitionSO unique = Skill("unique.a", 21, SkillOwnershipCategory.Unique, "hero.a");
            Register(basic, equipped, awarded, unique);
            CharacterProgressionService progression = Component<CharacterProgressionService>();
            progression.ConfigureForTests(null, Character(unique, 5));
            Participant.Runtime.TryAcquireSharedFilm(equipped.PersistentKey);
            new FilmEquipApplication(Participant.Runtime, Party, new[] { equipped }, State, Entry).TryEquipFilm("hero.a", equipped.PersistentKey);
            GameObject player = Actor();
            player.transform.position = new Vector3(5000, 5000);
            Collider2D playerCollider = player.AddComponent<BoxCollider2D>();
            GameObject enemy = Actor();
            enemy.transform.position = new Vector3(5000.5f, 5000);
            enemy.layer = LayerMask.NameToLayer("Enemy");
            enemy.AddComponent<BoxCollider2D>();
            CombatEncounterTrigger2D trigger = enemy.AddComponent<CombatEncounterTrigger2D>();
            SerializedObject triggerData = new(trigger);
            triggerData.FindProperty("entryPoint").objectReferenceValue = Entry;
            triggerData.FindProperty("enemyObject").objectReferenceValue = enemy;
            triggerData.ApplyModifiedPropertiesWithoutUndo();
            Invoke(trigger, "Awake");

            EnemyDefinitionSO enemyDefinition = Asset<EnemyDefinitionSO>();
            SerializedObject definitionData = new(enemyDefinition);
            definitionData.FindProperty("persistentKey").stringValue = "enemy.test";
            definitionData.FindProperty("acquirableSkillPersistentKeys").arraySize = 1;
            definitionData.FindProperty("acquirableSkillPersistentKeys").GetArrayElementAtIndex(0).stringValue = awarded.PersistentKey;
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            EnemySourceComponent source = enemy.AddComponent<EnemySourceComponent>();
            SerializedObject sourceData = new(source);
            sourceData.FindProperty("definition").objectReferenceValue = enemyDefinition;
            sourceData.ApplyModifiedPropertiesWithoutUndo();

            if (fieldAttack)
            {
                PlayerFieldAttackController attack = player.AddComponent<PlayerFieldAttackController>();
                SerializedObject attackData = new(attack);
                attackData.FindProperty("entryPoint").objectReferenceValue = Entry;
                attackData.FindProperty("attackOrigin").objectReferenceValue = player.transform;
                attackData.FindProperty("targetMask").intValue = 1 << enemy.layer;
                attackData.FindProperty("hitBoxSize").vector2Value = new Vector2(3, 3);
                attackData.FindProperty("hitBoxOffset").vector2Value = Vector2.zero;
                attackData.ApplyModifiedPropertiesWithoutUndo();
                Physics2D.SyncTransforms();
                Invoke(attack, "TryResolveHit", 0);
            }
            else
            {
                Invoke(trigger, "OnTriggerEnter2D", playerCollider);
            }

            Assert.That(Entry.ActiveSession, Is.Not.Null);
            Assert.That(Entry.ActiveSession.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 11, 21 }));
            Assert.That(Entry.ActiveSession.IsPartySkillAcquisitionEligible, Is.True);
            Entry.ActiveSession.Enemies.Single().ApplyDamage(10);
            Entry.OnCombatEnded += result => { Flow.HandleCombatResult(result); Flow.HandleRewardClosed(); };
            Invoke(Entry, "ForceFinishCombat", CombatEndReason.Victory);
            Assert.That(State.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(Participant.Runtime.HasSharedFilm(awarded.PersistentKey), Is.True);
            Assert.That(Participant.Runtime.GetEquippedSkills("hero.a"), Is.EqualTo(new[] { equipped.PersistentKey }));
            GameSaveData save = new();
            Participant.CaptureSaveData(save);
            Assert.That(save.characterSkills.sharedFilmSkillKeys, Does.Contain(awarded.PersistentKey));
        }

        [Test]
        public void PresentationAndCore_DoNotIntroduceGlobalRoutingOrPersistentDependencies()
        {
            string presenter = File.ReadAllText("Assets/GAME/Scripts/UI/CharacterSkillPresenter.cs");
            Assert.That(presenter, Does.Not.Contain("SetActive").And.Not.Contain("RequestState").And.Not.Contain("SetState"));
            foreach (string root in new[] { "Assets/GAME/Scripts/Combat/Runtime/Core", "Assets/GAME/Scripts/Combat/Runtime/Model" })
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                    Assert.That(File.ReadAllText(file), Does.Not.Contain("CharacterSkillRuntime").And.Not.Contain("UniqueSkillUnlockEvaluator"), file);
        }

        private CharacterProgressionDefinitionSO Character(SkillDefinitionSO unique, int starting)
        {
            CharacterProgressionDefinitionSO definition = Asset<CharacterProgressionDefinitionSO>();
            SerializedObject data = new(definition);
            data.FindProperty("characterId").stringValue = "hero.a";
            data.FindProperty("startingLevel").intValue = starting;
            data.FindProperty("experienceRequiredByLevel").arraySize = 1;
            data.FindProperty("experienceRequiredByLevel").GetArrayElementAtIndex(0).intValue = 10;
            data.FindProperty("uniqueSkillUnlocks").arraySize = 1;
            SerializedProperty entry = data.FindProperty("uniqueSkillUnlocks").GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("enabled").boolValue = true;
            entry.FindPropertyRelative("persistentSkillKey").stringValue = unique.PersistentKey;
            entry.FindPropertyRelative("condition").enumValueIndex = 1;
            entry.FindPropertyRelative("unlockLevel").intValue = 5;
            data.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
