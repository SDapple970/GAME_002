using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Story.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class StoryFlagPersistenceTests
    {
        private readonly List<GameObject> _objects = new();

        [SetUp]
        public void SetUp() => Cleanup();

        [TearDown]
        public void TearDown() => Cleanup();

        [Test]
        public void BoolAndSignedInt_RoundTripPreservesPresenceDefaultsAndExactTypedKeys()
        {
            StoryFlagManager flags = CreateOwner();
            flags.SetBool("yes", true);
            flags.SetBool("same", false);
            flags.SetInt("same", 0);
            flags.SetInt("negative", -17);
            flags.SetInt("maximum", int.MaxValue);
            flags.SetInt("minimum", int.MinValue);
            flags.SetBool(" spaced key ", false);
            flags.SetInt(" ", -1);
            GameSaveData save = new();
            flags.CaptureSaveData(save);
            GameSaveData loaded = Migrate(SaveSerializer.ToJson(save));
            flags.ClearAll();
            flags.RestoreSaveData(loaded);

            Assert.That(flags.GetBool("yes"), Is.True);
            Assert.That(flags.HasBool("same"), Is.True);
            Assert.That(flags.GetBool("same"), Is.False);
            Assert.That(flags.HasInt("same"), Is.True);
            Assert.That(flags.GetInt("same"), Is.Zero);
            Assert.That(flags.GetInt("negative"), Is.EqualTo(-17));
            Assert.That(flags.GetInt("maximum"), Is.EqualTo(int.MaxValue));
            Assert.That(flags.GetInt("minimum"), Is.EqualTo(int.MinValue));
            Assert.That(flags.HasBool(" spaced key "), Is.True);
            Assert.That(flags.HasBool("spaced key"), Is.False);
            Assert.That(flags.GetInt(" "), Is.EqualTo(-1));
            Assert.That(flags.GetBool("unset"), Is.False);
            Assert.That(flags.GetInt("unset"), Is.Zero);
            Assert.That(flags.HasFlag("unset"), Is.False);
            Assert.That(flags.HasInt("yes"), Is.False);
        }

        [Test]
        public void DuplicateEntries_LastTypedValueWinsWithoutMergingBoolAndIntDomains()
        {
            GameSaveData save = new();
            save.story.flags.AddRange(new[] { null, new SaveBoolEntry { id = "" },
                new SaveBoolEntry { id = "shared", value = true }, new SaveBoolEntry { id = "shared", value = false } });
            save.story.intFlags.AddRange(new[] { null, new SaveIntEntry { id = null },
                new SaveIntEntry { id = "shared", value = 99 }, new SaveIntEntry { id = "shared", value = 0 } });
            GameSaveData loaded = Migrate(SaveSerializer.ToJson(save));
            Assert.That(loaded.story.flags, Has.Count.EqualTo(1));
            Assert.That(loaded.story.intFlags, Has.Count.EqualTo(1));
            StoryFlagManager flags = CreateOwner();
            flags.RestoreSaveData(loaded);
            Assert.That(flags.HasBool("shared"), Is.True);
            Assert.That(flags.HasInt("shared"), Is.True);
            Assert.That(flags.GetBool("shared"), Is.False);
            Assert.That(flags.GetInt("shared"), Is.Zero);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void OldCanonicalSchemas_PreserveBoolAndUnrelatedProgress(int schema)
        {
            GameSaveData old = new();
            old.header.schemaVersion = schema;
            old.currency.gold = 73;
            old.inventory.items.Add(new SaveIntEntry { id = "token", value = 3 });
            old.story.currentChapter = 2;
            old.story.mainProgress = 8;
            old.story.completedEventIds.Add("event.done");
            old.story.flags.Add(new SaveBoolEntry { id = "saved.false", value = false });
            old.story.flags.Add(new SaveBoolEntry { id = "saved.true", value = true });
            // Use the actual pre-B01 payload shape rather than the new DTO default.
            string json = SaveSerializer.ToJson(old).Replace("\"intFlags\": []", "\"unusedOldField\": []");
            GameSaveData loaded = Migrate(json);
            Assert.That(loaded.header.schemaVersion, Is.EqualTo(11));
            Assert.That(loaded.story.intFlags, Is.Empty);
            Assert.That(loaded.currency.gold, Is.EqualTo(73));
            Assert.That(loaded.inventory.items.Single().value, Is.EqualTo(3));
            Assert.That(loaded.story.currentChapter, Is.EqualTo(2));
            Assert.That(loaded.story.mainProgress, Is.EqualTo(8));
            Assert.That(loaded.story.completedEventIds, Is.EqualTo(new[] { "event.done" }));
            StoryFlagManager flags = CreateOwner();
            flags.RestoreSaveData(loaded);
            Assert.That(flags.HasBool("saved.false"), Is.True);
            Assert.That(flags.GetBool("saved.false"), Is.False);
            Assert.That(flags.GetBool("saved.true"), Is.True);
            Assert.That(flags.HasInt("saved.true"), Is.False);
        }

        [Test]
        public void HeaderlessLegacySave_PreservesFalseExactKeysAndOtherProgress()
        {
            SaveData old = new() { gold = 19, currentChapterId = "chapter.old" };
            old.flags.Add(new BoolEntry { id = " false ", value = false });
            old.flags.Add(new BoolEntry { id = "true", value = true });
            old.inventory.Add(new IntEntry { id = "item", value = 2 });
            GameSaveData loaded = Migrate(SaveSerializer.ToJson(old));
            Assert.That(loaded.story.flags.Single(entry => entry.id == " false ").value, Is.False);
            Assert.That(loaded.currency.gold, Is.EqualTo(19));
            Assert.That(loaded.inventory.items.Single().value, Is.EqualTo(2));
            Assert.That(loaded.futureDaily.currentChapterId, Is.EqualTo("chapter.old"));
            Assert.That(loaded.story.intFlags, Is.Empty);
        }

        [Test]
        public void RepeatedRestore_ReplacesBothDomainsAndMissingSectionsClearStaleState()
        {
            StoryFlagManager flags = CreateOwner();
            GameSaveData save = new();
            save.story.flags.Add(new SaveBoolEntry { id = "kept", value = false });
            save.story.intFlags.Add(new SaveIntEntry { id = "kept", value = 0 });
            for (int i = 0; i < 3; i++)
            {
                flags.SetBool("stale", true);
                flags.SetInt("stale", 7);
                flags.RestoreSaveData(save);
                Assert.That(flags.HasFlag("stale"), Is.False);
                Assert.That(flags.ExportBoolFlags(), Has.Count.EqualTo(1));
                Assert.That(flags.ExportIntFlags(), Has.Count.EqualTo(1));
            }
            flags.RestoreSaveData(new GameSaveData { story = null });
            Assert.That(flags.HasFlag("kept"), Is.False);
        }

        [Test]
        public void DatabaseFacade_UsesSameOwnerAndPreservesLegacyClearImportAndSnapshotSemantics()
        {
            StoryFlagManager flags = CreateOwner();
            StoryFlagDatabase facade = Track(new GameObject("Compatibility")).AddComponent<StoryFlagDatabase>();
            facade.SetFlag("shared", true);
            Assert.That(flags.GetBool("shared"), Is.True);
            flags.SetBool("owner", true);
            Assert.That(facade.HasFlag("owner"), Is.True);
            facade.ClearFlag("shared");
            Assert.That(flags.HasBool("shared"), Is.True, "Legacy ClearFlag means explicit false.");
            Assert.That(facade.HasFlag("shared"), Is.False);
            flags.SetInt("counter", 7);
            facade.ImportFlags(new Dictionary<string, bool> { ["replacement"] = false });
            Assert.That(flags.HasBool("owner"), Is.False);
            Assert.That(flags.HasBool("replacement"), Is.True);
            Assert.That(flags.GetInt("counter"), Is.EqualTo(7));
            Dictionary<string, bool> exported = facade.ExportFlags();
            exported["replacement"] = true;
            Assert.That(flags.GetBool("replacement"), Is.False);
            GameSaveData save = new();
            facade.CaptureSaveData(save);
            facade.ResetForNewGame();
            Assert.That(flags.HasFlag("counter"), Is.False);
            facade.RestoreSaveData(save);
            Assert.That(flags.GetInt("counter"), Is.EqualTo(7));
        }

        [Test]
        public void SaveDiscovery_ProcessesOnlyCanonicalOwnerEvenWithFacadeAndDuplicateComponent()
        {
            StoryFlagManager flags = CreateOwner();
            Track(new GameObject("Compatibility")).AddComponent<StoryFlagDatabase>();
            StoryFlagManager duplicate = Track(new GameObject("Duplicate")).AddComponent<StoryFlagManager>();
            InvokeAwake(duplicate);
            flags.SetBool("canonical", false);
            flags.SetInt("canonical", -9);
            MethodInfo discover = typeof(SaveLoadService).GetMethod("Discover", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (Type contract in new[] { typeof(ISaveDataProvider), typeof(ISaveDataConsumer), typeof(INewGameRuntimeReset) })
            {
                object[] participants = ((System.Collections.IEnumerable)discover.MakeGenericMethod(contract).Invoke(null, null)).Cast<object>().ToArray();
                Assert.That(participants.Count(item => item is StoryFlagManager), Is.EqualTo(1));
                Assert.That(participants.Any(item => item is StoryFlagDatabase), Is.False);
            }
            Assert.That(StoryFlagManager.Instance, Is.SameAs(flags));
            Assert.That(flags.GetInt("canonical"), Is.EqualTo(-9));
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            Assert.That(StoryFlagManager.Instance, Is.SameAs(flags));
        }

        [Test]
        public void OwnerDestroyAndReinstall_DoesNotLeaveStaleSingletonOrSessionValues()
        {
            StoryFlagManager flags = CreateOwner();
            flags.SetBool("old", true);
            // EditMode objects use explicit Awake above; mirror their terminal callback.
            // The Production scene test exercises real Play Mode destruction separately.
            typeof(StoryFlagManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flags, null);
            UnityEngine.Object.DestroyImmediate(flags.gameObject);
            Assert.That(StoryFlagManager.Instance, Is.Null);
            StoryFlagDatabase facade = Track(new GameObject("Compatibility")).AddComponent<StoryFlagDatabase>();
            facade.SetFlag("new", true);
            Assert.That(StoryFlagManager.Instance, Is.Not.Null);
            Assert.That(StoryFlagManager.Instance.HasFlag("old"), Is.False);
            Assert.That(StoryFlagManager.Instance.GetBool("new"), Is.True);
        }

        [Test]
        public void FlagCollectionsBeyondGeneralCap_ArePreservedAndFutureSchemaIsRejected()
        {
            GameSaveData save = new();
            for (int i = 0; i < 300; i++)
            {
                save.story.flags.Add(new SaveBoolEntry { id = "flag." + i, value = false });
                save.story.intFlags.Add(new SaveIntEntry { id = "flag." + i, value = 0 });
            }
            GameSaveData loaded = Migrate(SaveSerializer.ToJson(save));
            Assert.That(loaded.story.flags, Has.Count.EqualTo(300));
            Assert.That(loaded.story.intFlags, Has.Count.EqualTo(300));
            save.header.schemaVersion = 12;
            object[] args = { SaveSerializer.ToJson(save), null, false, null };
            Assert.That((bool)MigrationMethod().Invoke(null, args), Is.False);
        }

        private StoryFlagManager CreateOwner()
        {
            StoryFlagManager owner = Track(new GameObject("StoryFlagManager")).AddComponent<StoryFlagManager>();
            InvokeAwake(owner);
            return owner;
        }

        private GameObject Track(GameObject value) { _objects.Add(value); return value; }
        private static void InvokeAwake(StoryFlagManager owner) => typeof(StoryFlagManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        private static MethodInfo MigrationMethod() => typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator").GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic);
        private static GameSaveData Migrate(string json)
        {
            object[] args = { json, null, false, null };
            Assert.That((bool)MigrationMethod().Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }

        private void Cleanup()
        {
            foreach (GameObject item in _objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            _objects.Clear();
            foreach (StoryFlagDatabase item in UnityEngine.Object.FindObjectsByType<StoryFlagDatabase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(item.gameObject);
            foreach (StoryFlagManager item in UnityEngine.Object.FindObjectsByType<StoryFlagManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
    }
}
