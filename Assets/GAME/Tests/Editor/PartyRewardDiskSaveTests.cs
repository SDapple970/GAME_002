#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Reward;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    [Category("CBT_PARTY_05V")]
    public sealed class PartyRewardDiskSaveTests
    {
        internal static readonly string[] CharacterIds =
        {
            "test.party.leader", "test.party.ally.one", "test.party.ally.two", "test.party.ally.three"
        };
        internal const string ItemId = "party05v.token";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            scene.name = "Party05VDiskVerification";
        }

        [Test]
        public void CompletedReward_RealDiskAndFreshServices_BlockEveryRewardChannel()
        {
            using DiskRuntime runtime = new("completed");
            RewardGrantRequest request = PartyRequest("party05v.completed");
            Assert.That(runtime.Rewards.GrantReward(request).PartialFailure, Is.False);
            runtime.AssertBalances(150, 150, 150);
            runtime.Save();
            AssertPartyLedger(runtime.Read(), request.SourceId, true);
            runtime.Rebuild();
            runtime.AssertBalances(0, 0, 0, 0, 0);
            runtime.Load();
            runtime.AssertBalances(150, 150, 150);
            runtime.AssertDuplicate(request);
        }

        [Test]
        public void MissingB_RealDiskRestore_ReconcilesOnlyBAndPersistsFinalSettlement()
        {
            using DiskRuntime runtime = new("partial", missingB: true);
            RewardGrantRequest request = PartyRequest("party05v.partial");
            RewardGrantResult result = runtime.Rewards.GrantReward(request);
            Assert.That(result.PartialFailure, Is.True);
            foreach (ExperienceApplyResult recipient in result.ExperienceResults)
                Assert.That(recipient.Settled, Is.EqualTo(recipient.CharacterId != CharacterIds[1]), recipient.CharacterId);
            Assert.That(runtime.Progression.TryGetState(CharacterIds[1], out _, out _), Is.False);
            runtime.AssertExperience(CharacterIds[0], 150);
            runtime.AssertExperience(CharacterIds[2], 150);
            runtime.Save();
            AssertPartyLedger(runtime.Read(), request.SourceId, false);
            runtime.WriteRestartCheckpoint("partial.json");
            runtime.Rebuild();
            runtime.AssertBalances(0, 0, 0, 0, 0);
            List<string> applied = new();
            runtime.Progression.ProgressionChanged += change => applied.Add(change.CharacterId);
            runtime.Load();
            Assert.That(applied, Is.EqualTo(new[] { CharacterIds[1] }));
            runtime.AssertBalances(150, 150, 150);
            Assert.That(runtime.Rewards.ReconcilePendingExperience(), Is.Zero);
            runtime.AssertDuplicate(request);
            runtime.Save();
            AssertPartyLedger(runtime.Read(), request.SourceId, true);
            Assert.That(File.Exists(runtime.Path + ".bak"), Is.True);
            runtime.Rebuild();
            runtime.Load();
            runtime.AssertBalances(150, 150, 150);
            runtime.AssertDuplicate(request);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void HistoricalSingleReward_DiskMigrationAndSchema12Resave_PreservePaidExpAndCompletion(int schema)
        {
            using DiskRuntime runtime = new("schema-" + schema);
            string completion = "party05v.schema-" + schema;
            // Historical single-target data deliberately has no Schema 12 recipient fields.
            string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":" + schema +
                "},\"currency\":{\"gold\":50},\"inventory\":{\"items\":[{\"id\":\"" + ItemId +
                "\",\"value\":2}]},\"progression\":{\"characters\":[{\"characterId\":\"" + CharacterIds[0] +
                "\",\"level\":1,\"experience\":150}]},\"reward\":{\"ledger\":[{\"sourceType\":\"Combat\",\"sourceId\":\"" +
                completion + "\",\"requestedGold\":50,\"gold\":50,\"requestedExp\":150,\"exp\":150," +
                "\"requestedItemId\":\"" + ItemId + "\",\"requestedItemCount\":2,\"itemId\":\"" + ItemId +
                "\",\"itemCount\":2,\"progressionTargetId\":\"" + CharacterIds[0] + "\",\"expSettled\":true}]}}";
            File.WriteAllText(runtime.Path, json);
            if (schema == 11) runtime.WriteRestartCheckpoint("schema11.json", save: false);
            runtime.Load();
            runtime.AssertBalances(150, 0, 0);
            runtime.AssertDuplicate(PartyRequest(completion));
            runtime.Save();
            GameSaveData migrated = runtime.Read();
            Assert.That(migrated.header.schemaVersion, Is.EqualTo(12));
            Assert.That(migrated.reward.ledger.Single().sourceId, Is.EqualTo(completion));
            Assert.That(migrated.reward.ledger.Single().hasExplicitExperienceRecipients, Is.False);
            Assert.That(migrated.reward.ledger.Single().expSettled, Is.True);
            Assert.That(migrated.reward.ledger.Single().exp, Is.EqualTo(150));
            Assert.That(File.ReadAllText(runtime.Path + ".bak"), Is.EqualTo(json));
            runtime.Rebuild();
            runtime.Load();
            runtime.AssertBalances(150, 0, 0);
            runtime.AssertDuplicate(PartyRequest(completion));
        }

        [Test]
        public void FutureSchema_RealDiskLoad_IsRejectedWithoutMutatingMemoryOrFile()
        {
            using DiskRuntime runtime = new("future");
            runtime.Wallet.SetGold(9);
            GameSaveData future = new();
            future.header.schemaVersion = 13;
            future.currency.gold = 999;
            string json = JsonUtility.ToJson(future);
            File.WriteAllText(runtime.Path, json);
            LogAssert.Expect(LogType.Warning, new Regex("Unsupported future schema version 13"));
            Assert.That(runtime.SaveLoad.TryLoad(out string message), Is.False);
            Assert.That(message, Does.Contain("Unsupported future schema version 13"));
            Assert.That(runtime.Wallet.Gold, Is.EqualTo(9));
            Assert.That(File.ReadAllText(runtime.Path), Is.EqualTo(json));
            Assert.That(runtime.SaveLoad.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
        }

        [Test]
        public void EditorRestart_ReadPreviousProcessFiles_WithFreshRuntimeOwners()
        {
            if (!HasArgument("-party05vRestartStage", "read"))
                Assert.Ignore("Run in a new Unity Editor process with -party05vRestartStage read after the write run.");

            string root = System.IO.Path.Combine(EvidenceRoot, "Restart");
            using (DiskRuntime runtime = new("restart-final", existingPath: System.IO.Path.Combine(root, "final-exchange.json")))
            {
                GameSaveData checkpoint = runtime.Read();
                AssertPartyLedger(checkpoint, checkpoint.reward.ledger.Single().sourceId, true);
                Assert.That(checkpoint.header.activeSceneId, Is.EqualTo(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
                List<string> applied = new();
                runtime.Progression.ProgressionChanged += change => applied.Add(change.CharacterId);
                runtime.Load();
                Assert.That(applied, Is.Empty);
                runtime.AssertBalances(150, 150, 150);
                runtime.AssertDuplicate(PartyRequest(checkpoint.reward.ledger.Single().sourceId));
            }
            using (DiskRuntime runtime = new("restart-partial", existingPath: System.IO.Path.Combine(root, "partial.json")))
            {
                AssertPartyLedger(runtime.Read(), "party05v.partial", false);
                List<string> applied = new();
                runtime.Progression.ProgressionChanged += change => applied.Add(change.CharacterId);
                runtime.Load();
                Assert.That(applied, Is.EqualTo(new[] { CharacterIds[1] }));
                runtime.AssertBalances(150, 150, 150);
                runtime.AssertDuplicate(PartyRequest("party05v.partial"));
                runtime.SaveLoad.SetStoragePathForTests(System.IO.Path.Combine(root, "partial-settled.json"));
                Assert.That(runtime.SaveLoad.TrySave(out string message), Is.True, message);
            }
            using (DiskRuntime runtime = new("restart-schema11", existingPath: System.IO.Path.Combine(root, "schema11.json")))
            {
                Assert.That(runtime.Read().header.schemaVersion, Is.EqualTo(11));
                runtime.Load();
                runtime.AssertBalances(150, 0, 0);
                runtime.AssertDuplicate(PartyRequest("party05v.schema-11"));
                runtime.SaveLoad.SetStoragePathForTests(System.IO.Path.Combine(root, "schema12.json"));
                Assert.That(runtime.SaveLoad.TrySave(out string message), Is.True, message);
            }
            Debug.Log("[CBT-PARTY-05V] Previous Editor process files restored: FinalExchange, Pending B, Schema 11.");
        }

        // Called only after an actual three-member FinalExchange victory in the isolated EnterPlayMode fixture.
        internal static void VerifyCompletedCombatFromDisk(CombatResult result)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            scene.name = "Party05VDiskVerification";
            using DiskRuntime runtime = new("final-exchange", adopt: true);
            runtime.AssertBalances(150, 150, 150);
            Assert.That(result.AllyParticipants.Single(item => item.CharacterId == CharacterIds[1]).IsAlive, Is.False);
            runtime.Save();
            AssertPartyLedger(runtime.Read(), result.CompletionId, true);
            runtime.WriteRestartCheckpoint("final-exchange.json");
            runtime.Rebuild();
            runtime.AssertBalances(0, 0, 0, 0, 0);
            List<string> applied = new();
            runtime.Progression.ProgressionChanged += change => applied.Add(change.CharacterId);
            runtime.Load();
            Assert.That(applied, Is.Empty);
            runtime.AssertBalances(150, 150, 150);
            Assert.That(runtime.Rewards.GrantCombatResult(result).DuplicateBlocked, Is.True);
            runtime.AssertDuplicate(PartyRequest(result.CompletionId));
            AssertPartyLedger(runtime.SaveLoad.CaptureGameSaveDataSnapshot(), result.CompletionId, true);
        }

        internal static RewardGrantRequest WithTestItem(CombatResult result)
        {
            RewardGrantRequest combat = RewardService.CreateCombatRewardRequest(result, null);
            // Combat conversion has no item payload; add a fixture item to exercise the same completion's item channel.
            return new RewardGrantRequest(combat.SourceType, combat.SourceId, combat.Gold, combat.Exp,
                ItemId, 2, combat.ActionId, combat.ProgressionTargetId, combat.ExperienceRecipientIds);
        }

        private static RewardGrantRequest PartyRequest(string completion) =>
            new(RewardSourceType.Combat, completion, 50, 150, ItemId, 2, experienceRecipientIds: CharacterIds.Take(3));

        private static void AssertPartyLedger(GameSaveData data, string completion, bool settled)
        {
            Assert.That(data.header.schemaVersion, Is.EqualTo(12));
            RewardLedgerSaveData entry = data.reward.ledger.Single();
            Assert.That(entry.sourceId, Is.EqualTo(completion));
            Assert.That(entry.gold, Is.EqualTo(50));
            Assert.That(entry.itemId, Is.EqualTo(ItemId));
            Assert.That(entry.itemCount, Is.EqualTo(2));
            Assert.That(entry.hasExplicitExperienceRecipients, Is.True);
            Assert.That(entry.expSettled, Is.EqualTo(settled));
            Assert.That(entry.partialFailure, Is.EqualTo(!settled));
            Assert.That(entry.experienceRecipients, Has.Count.EqualTo(3));
            foreach (string id in CharacterIds.Take(3))
            {
                RewardExperienceSaveData recipient = entry.experienceRecipients.Single(item => item.characterId == id);
                bool paid = settled || id != CharacterIds[1];
                Assert.That(recipient.requestedExp, Is.EqualTo(150), id);
                Assert.That(recipient.appliedExp, Is.EqualTo(paid ? 150 : 0), id);
                Assert.That(recipient.settled, Is.EqualTo(paid), id);
            }
            Assert.That(JsonUtility.ToJson(data.reward.combatLedger.Single()), Is.EqualTo(JsonUtility.ToJson(entry)));
        }

        private static string EvidenceRoot => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/CBT-PARTY-05V"));
        private static bool HasArgument(string key, string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length && args[index + 1] == value;
        }

        private sealed class DiskRuntime : IDisposable
        {
            private readonly List<UnityEngine.Object> _definitions = new();
            public string Path { get; }
            public SaveLoadService SaveLoad { get; private set; }
            public CharacterProgressionService Progression { get; private set; }
            public RewardService Rewards { get; private set; }
            public CurrencyWallet Wallet { get; private set; }
            public InventoryService Inventory { get; private set; }

            public DiskRuntime(string scenario, bool missingB = false, bool adopt = false, string existingPath = null)
            {
                Path = existingPath ?? System.IO.Path.Combine(EvidenceRoot, scenario + "-" + Guid.NewGuid().ToString("N"), "game_save.json");
                Assert.That(System.IO.Path.GetFullPath(Path), Does.StartWith(EvidenceRoot + System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                if (adopt)
                {
                    SaveLoad = SaveLoadService.Instance; Progression = CharacterProgressionService.Instance;
                    Rewards = RewardService.Instance; Wallet = CurrencyWallet.Instance; Inventory = InventoryService.Instance;
                    Assert.That(new UnityEngine.Object[] { SaveLoad, Progression, Rewards, Wallet, Inventory }, Has.None.Null);
                    SaveLoad.SetStoragePathForTests(Path);
                }
                else Create(missingB);
            }

            private void Create(bool missingB)
            {
                // Other EditMode fixtures can leave destroyed Unity wrappers in static fields.
                // Require no live owner using Unity's equality before Awake installs fresh owners.
                Assert.That(SaveLoadService.Instance == null, Is.True);
                Assert.That(CharacterProgressionService.Instance == null, Is.True);
                Assert.That(RewardService.Instance == null, Is.True);
                Assert.That(CurrencyWallet.Instance == null, Is.True);
                Assert.That(InventoryService.Instance == null, Is.True);
                SaveLoad = New<SaveLoadService>();
                SaveLoad.SetStoragePathForTests(Path);
                Progression = New<CharacterProgressionService>();
                List<CharacterProgressionDefinitionSO> definitions = new();
                foreach (string id in CharacterIds)
                {
                    if (missingB && id == CharacterIds[1]) continue;
                    CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
                    _definitions.Add(definition);
                    SetField(definition, "characterId", id);
                    SetField(definition, "startingLevel", 1);
                    SetField(definition, "maximumLevel", 3);
                    SetField(definition, "experienceRequiredByLevel", new[] { 1000, 1000 });
                    definitions.Add(definition);
                }
                Progression.ConfigureForTests(CharacterIds[0], definitions.ToArray());
                Wallet = New<CurrencyWallet>(); Inventory = New<InventoryService>(); Rewards = New<RewardService>();
                SetField(Rewards, "currencyWallet", Wallet);
                SetField(Rewards, "inventoryService", Inventory);
                SetField(Rewards, "characterProgressionService", Progression);
            }

            public void Save()
            {
                Assert.That(SaveLoad.PrimarySavePath, Is.EqualTo(Path));
                Assert.That(SaveLoad.TrySave(out string message), Is.True, message);
                Assert.That(File.Exists(Path), Is.True);
                Assert.That(new FileInfo(Path).Length, Is.GreaterThan(0));
                Assert.That(File.Exists(Path + ".tmp"), Is.False);
                Debug.Log($"[CBT-PARTY-05V] Disk save evidence: {Path} ({new FileInfo(Path).Length} bytes)");
            }

            public GameSaveData Read()
            {
                Assert.That(File.Exists(Path), Is.True, Path);
                return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(Path));
            }

            public void Load()
            {
                string before = File.ReadAllText(Path);
                int completed = 0;
                bool success = false;
                void OnLoaded(bool ok, string _) { completed++; success = ok; }
                SaveLoad.OnLoadCompleted += OnLoaded;
                try { Assert.That(SaveLoad.TryLoad(out string message), Is.True, message); }
                finally { SaveLoad.OnLoadCompleted -= OnLoaded; }
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(success, Is.True);
                Assert.That(SaveLoad.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
                Assert.That(File.ReadAllText(Path), Is.EqualTo(before));
            }

            public void Rebuild()
            {
                UnityEngine.Object[] oldOwners = { SaveLoad, Progression, Rewards, Wallet, Inventory };
                Dispose();
                Assert.That(oldOwners.All(owner => owner == null), Is.True, "All five previous service objects must be destroyed.");
                Create(false);
                Assert.That(SaveLoad.CaptureGameSaveDataSnapshot().reward.ledger, Is.Empty);
            }

            public void AssertExperience(string id, int expected)
            {
                Assert.That(Progression.TryGetState(id, out int level, out int exp), Is.True, id);
                Assert.That(level, Is.EqualTo(1), id);
                Assert.That(exp, Is.EqualTo(expected), id);
            }

            public void AssertBalances(int a, int b, int c, int gold = 50, int items = 2)
            {
                AssertExperience(CharacterIds[0], a); AssertExperience(CharacterIds[1], b);
                AssertExperience(CharacterIds[2], c); AssertExperience(CharacterIds[3], 0);
                Assert.That(Wallet.Gold, Is.EqualTo(gold));
                Assert.That(Inventory.GetCount(ItemId), Is.EqualTo(items));
            }

            public void AssertDuplicate(RewardGrantRequest request)
            {
                GameSaveData before = SaveLoad.CaptureGameSaveDataSnapshot();
                RewardGrantResult duplicate = Rewards.GrantReward(request);
                Assert.That(duplicate.DuplicateBlocked, Is.True);
                Assert.That(duplicate.Gold, Is.Zero); Assert.That(duplicate.Exp, Is.Zero); Assert.That(duplicate.ItemCount, Is.Zero);
                GameSaveData after = SaveLoad.CaptureGameSaveDataSnapshot();
                Assert.That(JsonUtility.ToJson(after.progression), Is.EqualTo(JsonUtility.ToJson(before.progression)));
                Assert.That(JsonUtility.ToJson(after.currency), Is.EqualTo(JsonUtility.ToJson(before.currency)));
                Assert.That(JsonUtility.ToJson(after.inventory), Is.EqualTo(JsonUtility.ToJson(before.inventory)));
                Assert.That(JsonUtility.ToJson(after.reward), Is.EqualTo(JsonUtility.ToJson(before.reward)));
            }

            public void WriteRestartCheckpoint(string filename, bool save = true)
            {
                if (!HasArgument("-party05vRestartStage", "write")) return;
                string target = System.IO.Path.Combine(EvidenceRoot, "Restart", filename);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                Assert.That(File.Exists(target), Is.False, "Restart fixtures must not overwrite a previous run.");
                if (save)
                {
                    SaveLoad.SetStoragePathForTests(target);
                    try { Assert.That(SaveLoad.TrySave(out string message), Is.True, message); }
                    finally { SaveLoad.SetStoragePathForTests(Path); }
                }
                else File.Copy(Path, target);
            }

            public void Dispose()
            {
                foreach (Component owner in new Component[] { Rewards, SaveLoad, Progression, Inventory, Wallet })
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner.gameObject);
                foreach (UnityEngine.Object definition in _definitions)
                    if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                _definitions.Clear();
                // Disk evidence intentionally remains under Logs; no user save files are enumerated or deleted.
            }

            private static T New<T>() where T : Component => new GameObject("PARTY05V." + typeof(T).Name).AddComponent<T>();
            private static void SetField(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
#endif
