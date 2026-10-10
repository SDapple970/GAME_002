using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class CharacterSkillRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            Cleanup();
        }

        [TearDown]
        public void TearDown()
        {
            Cleanup();
        }

        [Test]
        public void Acquire_TracksPerCharacterAndRejectsDuplicatesAndInvalidInputs()
        {
            CharacterSkillRuntime runtime = new();

            CharacterSkillAcquireResult acquired = runtime.TryAcquire(" hero.a ", " skill.test.a ");
            Assert.That(acquired.Status, Is.EqualTo(CharacterSkillAcquireStatus.Acquired));
            Assert.That(acquired.CharacterId, Is.EqualTo("hero.a"));
            Assert.That(acquired.PersistentSkillKey, Is.EqualTo("skill.test.a"));
            Assert.That(runtime.TryAcquire("hero.a", "skill.test.a").Status, Is.EqualTo(CharacterSkillAcquireStatus.AlreadyOwned));
            Assert.That(runtime.TryAcquire("hero.b", "skill.test.a").Status, Is.EqualTo(CharacterSkillAcquireStatus.Acquired));
            Assert.That(runtime.TryAcquire(" ", "skill.test.b").Status, Is.EqualTo(CharacterSkillAcquireStatus.InvalidCharacterId));
            Assert.That(runtime.TryAcquire("hero.a", " ").Status, Is.EqualTo(CharacterSkillAcquireStatus.InvalidPersistentSkillKey));
            Assert.That(runtime.GetAcquiredSkills("hero.a"), Is.EqualTo(new[] { "skill.test.a" }));
            Assert.That(runtime.GetAcquiredSkills("hero.b"), Is.EqualTo(new[] { "skill.test.a" }));
        }

        [Test]
        public void Query_ReturnsAnImmutableSortedSnapshotAndResetClearsState()
        {
            CharacterSkillRuntime runtime = new();
            runtime.TryAcquire("hero", "skill.z");
            runtime.TryAcquire("hero", "skill.a");

            IReadOnlyList<string> snapshot = runtime.GetAcquiredSkills("hero");
            Assert.That(snapshot, Is.EqualTo(new[] { "skill.a", "skill.z" }));
            IList<string> attemptedMutation = snapshot as IList<string>;
            Assert.That(attemptedMutation, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => attemptedMutation.Add("skill.mutated"));
            Assert.That(runtime.HasAcquired("hero", "skill.mutated"), Is.False);

            runtime.Reset();
            Assert.That(runtime.GetAcquiredSkills("hero"), Is.Empty);
        }

        [Test]
        public void Equip_RequiresAcquisitionPreservesOrderAndDoesNotMutateAcquiredState()
        {
            CharacterSkillRuntime runtime = new();
            runtime.TryAcquire("hero.a", "skill.z");
            runtime.TryAcquire("hero.a", "skill.a");
            runtime.TryAcquire("hero.b", "skill.a");

            Assert.That(runtime.TryEquip("hero.a", "skill.missing").Status, Is.EqualTo(CharacterSkillEquipStatus.NotAcquired));
            Assert.That(runtime.TryEquip(" ", "skill.a").Status, Is.EqualTo(CharacterSkillEquipStatus.InvalidCharacterId));
            Assert.That(runtime.TryEquip("hero.a", " ").Status, Is.EqualTo(CharacterSkillEquipStatus.InvalidPersistentSkillKey));
            Assert.That(runtime.TryEquip("hero.a", "skill.z").Status, Is.EqualTo(CharacterSkillEquipStatus.Equipped));
            Assert.That(runtime.TryEquip("hero.a", "skill.a").Status, Is.EqualTo(CharacterSkillEquipStatus.Equipped));
            Assert.That(runtime.TryEquip("hero.a", "skill.a").Status, Is.EqualTo(CharacterSkillEquipStatus.AlreadyEquipped));
            Assert.That(runtime.TryEquip("hero.b", "skill.a").Status, Is.EqualTo(CharacterSkillEquipStatus.Equipped));

            Assert.That(runtime.GetEquippedSkills("hero.a"), Is.EqualTo(new[] { "skill.z", "skill.a" }));
            Assert.That(runtime.GetEquippedSkills("hero.b"), Is.EqualTo(new[] { "skill.a" }));
            Assert.That(runtime.GetAcquiredSkills("hero.a"), Is.EqualTo(new[] { "skill.a", "skill.z" }));
            Assert.That(runtime.TryUnequip("hero.a", "skill.z").Status, Is.EqualTo(CharacterSkillEquipStatus.Unequipped));
            Assert.That(runtime.TryUnequip("hero.a", "skill.z").Status, Is.EqualTo(CharacterSkillEquipStatus.NotEquipped));
            Assert.That(runtime.GetAcquiredSkills("hero.a"), Is.EqualTo(new[] { "skill.a", "skill.z" }));
        }

        [Test]
        public void SaveParticipant_CapturesRestoresAndPreservesUnknownSkillKeys()
        {
            CharacterSkillSaveParticipant participant = new GameObject("Skills").AddComponent<CharacterSkillSaveParticipant>();
            participant.Runtime.TryAcquire("hero.b", "skill.unknown.removed");
            participant.Runtime.TryAcquire("hero.a", "skill.production.0002");
            participant.Runtime.TryAcquire("hero.a", "skill.production.0001");
            GameSaveData save = new();

            participant.CaptureSaveData(save);

            Assert.That(save.characterSkills.characters.Select(entry => entry.characterId), Is.EqualTo(new[] { "hero.a", "hero.b" }));
            Assert.That(save.characterSkills.characters[0].acquiredSkillKeys, Is.EqualTo(new[] { "skill.production.0001", "skill.production.0002" }));
            Assert.That(save.characterSkills.characters[1].acquiredSkillKeys, Is.EqualTo(new[] { "skill.unknown.removed" }));

            participant.ResetForNewGame();
            participant.RestoreSaveData(save);
            Assert.That(participant.Runtime.HasAcquired("hero.a", "skill.production.0001"), Is.True);
            Assert.That(participant.Runtime.HasAcquired("hero.b", "skill.unknown.removed"), Is.True);
        }

        [Test]
        public void SaveRoundTrip_RetainsValidatedEquippedOrderAndRejectsMalformedEquipEntries()
        {
            CharacterSkillSaveParticipant participant = new GameObject("Skills").AddComponent<CharacterSkillSaveParticipant>();
            GameSaveData save = new();
            save.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = " hero ",
                acquiredSkillKeys = new List<string> { "skill.b", "skill.a", "skill.unknown" },
                equippedSkillKeys = new List<string> { "skill.unknown", "skill.b", "skill.b", "skill.not-acquired", " " }
            });

            participant.RestoreSaveData(save);
            Assert.That(participant.Runtime.GetEquippedSkills("hero"), Is.EqualTo(new[] { "skill.unknown", "skill.b" }));
            Assert.That(participant.Runtime.IsEquipped("hero", "skill.not-acquired"), Is.False);

            GameSaveData captured = new();
            participant.CaptureSaveData(captured);
            Assert.That(captured.characterSkills.characters.Single().equippedSkillKeys,
                Is.EqualTo(new[] { "skill.unknown", "skill.b" }));
            participant.ResetForNewGame();
            Assert.That(participant.Runtime.GetEquippedSkills("hero"), Is.Empty);
        }

        [Test]
        public void Restore_NormalizesDuplicateCharacterAndSkillEntriesDeterministically()
        {
            CharacterSkillSaveParticipant participant = new GameObject("Skills").AddComponent<CharacterSkillSaveParticipant>();
            GameSaveData save = new();
            save.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = " hero ",
                acquiredSkillKeys = new List<string> { "skill.z", "skill.a", "skill.z", " " }
            });
            save.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = "hero",
                acquiredSkillKeys = new List<string> { "skill.b", "skill.a" }
            });

            participant.RestoreSaveData(save);
            GameSaveData captured = new();
            participant.CaptureSaveData(captured);

            Assert.That(captured.characterSkills.characters, Has.Count.EqualTo(1));
            Assert.That(captured.characterSkills.characters[0].characterId, Is.EqualTo("hero"));
            Assert.That(captured.characterSkills.characters[0].acquiredSkillKeys, Is.EqualTo(new[] { "skill.a", "skill.b", "skill.z" }));
        }

        [Test]
        public void SchemaSevenSave_MigratesToAnEmptySkillCollectionWithoutChangingExistingSections()
        {
            const string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":7},\"currency\":{\"gold\":13},\"inventory\":{\"items\":[{\"id\":\"token\",\"value\":2}]}}";
            GameSaveData migrated = Migrate(json);

            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.characterSkills, Is.Not.Null);
            Assert.That(migrated.characterSkills.characters, Is.Empty);
            Assert.That(migrated.currency.gold, Is.EqualTo(13));
            Assert.That(migrated.inventory.items.Single().id, Is.EqualTo("token"));
        }

        [Test]
        public void SchemaEightSave_MigratesToEmptyEquippedCollectionsWithoutAutoEquip()
        {
            const string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":8},\"characterSkills\":{\"characters\":[{\"characterId\":\"hero\",\"acquiredSkillKeys\":[\"skill.a\"]}]}}";
            GameSaveData migrated = Migrate(json);

            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.characterSkills.characters.Single().acquiredSkillKeys, Is.EqualTo(new[] { "skill.a" }));
            Assert.That(migrated.characterSkills.characters.Single().equippedSkillKeys, Is.Empty);
        }

        [Test]
        public void SaveMigration_NormalizesCharacterSkillEntriesWithoutDiscardingUnknownKeys()
        {
            GameSaveData source = new();
            source.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = "hero",
                acquiredSkillKeys = new List<string> { "skill.unknown", "skill.a", "skill.a" }
            });
            source.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData
            {
                characterId = " hero ",
                acquiredSkillKeys = new List<string> { "skill.b", "skill.unknown" }
            });

            GameSaveData migrated = Migrate(SaveSerializer.ToJson(source));

            Assert.That(migrated.characterSkills.characters, Has.Count.EqualTo(1));
            Assert.That(migrated.characterSkills.characters[0].acquiredSkillKeys,
                Is.EqualTo(new[] { "skill.a", "skill.b", "skill.unknown" }));
        }

        [Test]
        public void NewGameReset_ClearsAcquiredAndEquippedSkillState()
        {
            CharacterSkillSaveParticipant participant = new GameObject("Skills").AddComponent<CharacterSkillSaveParticipant>();
            participant.Runtime.TryAcquire("hero", "skill.a");
            participant.Runtime.TryEquip("hero", "skill.a");
            participant.ResetForNewGame();

            Assert.That(participant.Runtime.GetAcquiredSkills("hero"), Is.Empty);
            Assert.That(participant.Runtime.GetEquippedSkills("hero"), Is.Empty);
        }

        [Test]
        public void Bootstrapper_InstallsOneSaveParticipantWithoutCombatOrRewardMutationPath()
        {
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>();
            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);

            Assert.That(UnityEngine.Object.FindObjectsByType<CharacterSkillSaveParticipant>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Core/CombatEntryPoint.cs"), Does.Not.Contain("CharacterSkillRuntime"));
            Assert.That(Read("Assets/GAME/Scripts/Combat/Runtime/Actions/SkillRunner.cs"), Does.Not.Contain("CharacterSkillRuntime"));
            Assert.That(Read("Assets/GAME/Scripts/Reward/RewardService.cs"), Does.Not.Contain("CharacterSkillRuntime"));
        }

        private static GameSaveData Migrate(string json)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            object[] arguments = { json, null, false, null };
            Assert.That((bool)migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments), Is.True, arguments[3] as string);
            return (GameSaveData)arguments[1];
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }

        private static string Read(string path)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, path));
        }

        private static void Cleanup()
        {
            HashSet<GameObject> gameObjects = new();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || EditorUtility.IsPersistent(behaviour))
                    continue;

                string componentNamespace = behaviour.GetType().Namespace;
                if (!string.IsNullOrEmpty(componentNamespace) && componentNamespace.StartsWith("Game.", StringComparison.Ordinal))
                    gameObjects.Add(behaviour.gameObject);
            }

            foreach (GameObject gameObject in gameObjects)
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
