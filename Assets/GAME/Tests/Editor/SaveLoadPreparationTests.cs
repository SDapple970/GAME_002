using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.Story;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class SaveLoadPreparationTests
    {
        private string _directory;
        private string _primary;
        private GameObject _serviceObject;
        private SaveLoadService _service;

        [SetUp]
        public void SetUp()
        {
            CleanupObjects();
            _directory = Path.Combine(Path.GetTempPath(), "GAME_002_Batch10_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _primary = Path.Combine(_directory, "game_save.json");
            _serviceObject = new GameObject("SaveLoadService_Test");
            _service = _serviceObject.AddComponent<SaveLoadService>();
            Invoke(_service, "SetStoragePathForTests", _primary);
        }

        [TearDown]
        public void TearDown()
        {
            CleanupObjects();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveManagerHotkeys_AreEditorGuardedAndNotInputServiceOwned()
        {
            string source = File.ReadAllText(ProjectPath("Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs"));
            Assert.That(source, Does.Contain("#if UNITY_EDITOR"));
            Assert.That(source, Does.Contain("SaveLoadService.Instance.Save()"));
            Assert.That(File.ReadAllText(ProjectPath("Assets/GAME/Scripts/Input/InputService.cs")), Does.Not.Contain("KeyCode.F5"));
        }

        [Test]
        public void CurrentDto_RoundTripsHeaderAndSections()
        {
            GameSaveData source = new();
            source.header.activeSceneId = "Dungeon 1";
            source.currency.gold = 42;
            source.inventory.items.Add(new SaveIntEntry { id = "item", value = 3 });
            Assert.That(SaveSerializer.TryFromGameSaveJson(SaveSerializer.ToJson(source), out GameSaveData restored), Is.True);
            Assert.That(restored.header.formatId, Is.EqualTo(GameSaveDataFormat.FormatId));
            Assert.That(restored.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(restored.currency.gold, Is.EqualTo(42));
        }

        [TestCase("")]
        [TestCase("not json")]
        public void InvalidJson_DoesNotProduceCanonicalSnapshot(string json)
        {
            Assert.That(SaveSerializer.TryFromGameSaveJson(json, out _), Is.False);
        }

        [Test]
        public void SuccessfulSaveCreatesPrimaryAndRotatesBackup()
        {
            Assert.That(_service.TrySave(out _), Is.True);
            string first = File.ReadAllText(_primary);
            Assert.That(_service.TrySave(out _), Is.True);
            Assert.That(File.Exists(_primary + ".bak"), Is.True);
            Assert.That(File.ReadAllText(_primary + ".bak"), Is.EqualTo(first));
            Assert.That(File.Exists(_primary + ".tmp"), Is.False);
        }

        [Test]
        public void MissingFileReturnsFailureWithoutCreatingDefault()
        {
            Assert.That(_service.TryLoad(out string message), Is.False);
            Assert.That(message, Does.Contain("No save file"));
            Assert.That(File.Exists(_primary), Is.False);
        }

        [Test]
        public void CorruptedPrimaryFallsBackToValidBackup()
        {
            GameSaveData data = new();
            File.WriteAllText(_primary + ".bak", SaveSerializer.ToJson(data));
            File.WriteAllText(_primary, "corrupt");
            Assert.That(_service.TryLoad(out string message), Is.True);
            Assert.That(message, Does.Contain("backup"));
            Assert.That(File.ReadAllText(_primary), Is.EqualTo("corrupt"));
        }

        [Test]
        public void FutureSchemaIsRejectedWithoutMutation()
        {
            GameSaveData data = new(); data.header.schemaVersion = 999;
            File.WriteAllText(_primary, SaveSerializer.ToJson(data));
            Assert.That(_service.TryLoad(out string message), Is.False);
            Assert.That(message, Does.Contain("future schema"));
        }

        [Test]
        public void LegacySaveMigratesGoldInventoryFlagsPersonaAndPosition()
        {
            SaveData legacy = new() { gold = 12, playerPosition = new Vector3(1, 2, 3), currentChapterId = "chapter" };
            legacy.inventory.Add(new IntEntry { id = "potion", value = 2 });
            legacy.flags.Add(new BoolEntry { id = "seen", value = true });
            legacy.personaStats.Add(new PersonaStatEntry { stat = "Courage", level = 2, xp = 4 });
            File.WriteAllText(_primary, SaveSerializer.ToJson(legacy));
            Assert.That(_service.TryLoad(out _), Is.True);
            GameSaveData migrated = ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(legacy));
            Assert.That(migrated.currency.gold, Is.EqualTo(12));
            Assert.That(migrated.inventory.items.Single().id, Is.EqualTo("potion"));
            Assert.That(migrated.story.flags.Single().id, Is.EqualTo("seen"));
            Assert.That(migrated.progression.personaStats.Single().level, Is.EqualTo(2));
            Assert.That(migrated.location.positionY, Is.EqualTo(2));
        }

        [Test]
        public void SchemaOneMigratesCompletedBoolAndFormatHeader()
        {
            string json = "{\"header\":{\"schemaVersion\":1},\"quest\":{\"quests\":[{\"questId\":\"q\",\"completed\":true}]}}";
            GameSaveData data = ReadSnapshotFromLegacyJson(json);
            Assert.That(data.header.schemaVersion, Is.EqualTo(2));
            Assert.That(data.header.formatId, Is.EqualTo(GameSaveDataFormat.FormatId));
            Assert.That(data.quest.quests[0].status, Is.EqualTo("Completed"));
        }

        [Test]
        public void InventoryRestoreMergesDuplicatesAndReplacesStaleState()
        {
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            inventory.AddItem("stale", 5);
            GameSaveData data = new();
            data.inventory.items.Add(new SaveIntEntry { id = "item", value = 2 });
            data.inventory.items.Add(new SaveIntEntry { id = "item", value = 3 });
            data.inventory.items.Add(new SaveIntEntry { id = "", value = 99 });
            inventory.RestoreSaveData(data);
            Assert.That(inventory.GetCount("item"), Is.EqualTo(5));
            Assert.That(inventory.GetCount("stale"), Is.Zero);
        }

        [Test]
        public void CurrencyRestoreClampsNegativeGold()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            GameSaveData data = new(); data.currency.gold = -10;
            wallet.RestoreSaveData(data);
            Assert.That(wallet.Gold, Is.Zero);
        }

        [Test]
        public void StoryProgressRoundTripsSilentlyAndNormalizesIds()
        {
            StoryProgressManager source = new GameObject("StorySource").AddComponent<StoryProgressManager>();
            source.SetChapter(3); source.SetMainProgress(7); source.MarkEventCompleted("b"); source.MarkEventCompleted("a"); source.MarkEventCompleted("a");
            GameSaveData data = new(); source.CaptureSaveData(data);
            Assert.That(data.story.completedEventIds, Is.EqualTo(new[] { "a", "b" }));
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            StoryProgressManager target = new GameObject("StoryTarget").AddComponent<StoryProgressManager>();
            target.RestoreSaveData(data);
            Assert.That(target.CurrentChapter, Is.EqualTo(3)); Assert.That(target.MainProgress, Is.EqualTo(7)); Assert.That(target.IsEventCompleted("a"), Is.True);
        }

        [Test]
        public void RewardLedgerRestoreDoesNotGrantAndBlocksDuplicate()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            GameSaveData data = new();
            data.reward.combatLedger.Add(new RewardLedgerSaveData { sourceType = RewardSourceType.Combat.ToString(), sourceId = "combat-1", gold = 50 });
            rewards.RestoreSaveData(data);
            Assert.That(wallet.Gold, Is.Zero);
            RewardGrantResult result = rewards.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "combat-1", 50, 0));
            Assert.That(result.DuplicateBlocked, Is.True);
            Assert.That(wallet.Gold, Is.Zero);
        }

        [Test]
        public void CanonicalDtoContainsNoUnityObjectFields()
        {
            Type[] dtoTypes = typeof(GameSaveData).Assembly.GetTypes().Where(type => type.Namespace == "Game.NonCombat.Save" && type.IsSerializable).ToArray();
            foreach (Type type in dtoTypes)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType), Is.False, $"{type.FullName}.{field.Name}");
        }

        [Test]
        public void CanonicalJsonExcludesTransientRuntimeNames()
        {
            string json = SaveSerializer.ToJson(new GameSaveData());
            Assert.That(json, Does.Not.Contain("CombatSession"));
            Assert.That(json, Does.Not.Contain("CombatTurn"));
            Assert.That(json, Does.Not.Contain("StoryEventRunner"));
            Assert.That(json, Does.Not.Contain("RewardUIPanel"));
        }

        [Test]
        public void OperationReturnsToIdleAfterSuccessAndFailure()
        {
            Assert.That(_service.TryLoad(out _), Is.False);
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
            Assert.That(_service.TrySave(out _), Is.True);
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
        }

        [Test]
        public void TestsUseInjectedTemporaryPath()
        {
            Assert.That(_service.PrimarySavePath, Is.EqualTo(_primary));
            Assert.That(_primary, Does.StartWith(Path.GetTempPath()));
            Assert.That(_primary, Is.Not.EqualTo(Path.Combine(Application.persistentDataPath, "game_save.json")));
        }

        private static GameSaveData ReadSnapshotFromLegacyJson(string json)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            MethodInfo method = migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { json, null, false, null };
            Assert.That((bool)method.Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }

        private static string ProjectPath(string relative) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative.Replace('/', Path.DirectorySeparatorChar));

        private static void CleanupObjects()
        {
            foreach (MonoBehaviour item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item is SaveLoadService || item is InventoryService || item is CurrencyWallet || item is RewardService || item is StoryProgressManager || item is GameStateMachine || item is GameFlowController)
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
    }
}
