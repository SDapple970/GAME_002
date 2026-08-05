using System;
using System.Reflection;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Reward;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class InventoryProgressionBatch5Tests
    {
        [SetUp] public void SetUp() => Cleanup();
        [TearDown] public void TearDown() => Cleanup();

        [Test]
        public void InventoryMutation_IsPreciseOverflowSafeAndEventedOnce()
        {
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            int events = 0; inventory.Changed += _ => events++;
            InventoryMutationResult added = inventory.TryAddItem(" arbitrary.id ", 3);
            Assert.That(added.AppliedAmount, Is.EqualTo(3));
            Assert.That(inventory.GetCount("arbitrary.id"), Is.EqualTo(3));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(inventory.TryAddItem("arbitrary.id", 0).Changed, Is.False);
            Assert.That(events, Is.EqualTo(1));
            inventory.ImportItems(new System.Collections.Generic.Dictionary<string, int> { ["arbitrary.id"] = int.MaxValue });
            Assert.That(inventory.TryAddItem("arbitrary.id", 1).Status, Is.EqualTo(InventoryMutationStatus.OverflowPrevented));
        }

        [Test]
        public void InventoryCatalog_RejectsUnknownAndAppliesStackLimitPartially()
        {
            ItemDefinitionSO item = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            SerializedObject itemSerialized = new(item);
            itemSerialized.FindProperty("itemId").stringValue = "item.stack";
            itemSerialized.FindProperty("maximumStackCount").intValue = 5;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();
            ItemCatalogSO catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            SerializedObject catalogSerialized = new(catalog);
            SerializedProperty items = catalogSerialized.FindProperty("items"); items.arraySize = 1; items.GetArrayElementAtIndex(0).objectReferenceValue = item;
            catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            SerializedObject inventorySerialized = new(inventory); inventorySerialized.FindProperty("itemCatalog").objectReferenceValue = catalog; inventorySerialized.ApplyModifiedPropertiesWithoutUndo();
            InventoryMutationResult result = inventory.TryAddItem("item.stack", 8);
            Assert.That(result.Status, Is.EqualTo(InventoryMutationStatus.Partial)); Assert.That(result.AppliedAmount, Is.EqualTo(5));
            Assert.That(inventory.TryAddItem("unknown", 1).Status, Is.EqualTo(InventoryMutationStatus.UnknownDefinition));
        }

        [Test]
        public void CurrencyMutation_RejectsOverflowAndRestoreUsesRefreshOnly()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            int changes = 0, refreshes = 0; wallet.Changed += _ => changes++; wallet.Refreshed += () => refreshes++;
            Assert.That(wallet.TryAddGold(5).AppliedAmount, Is.EqualTo(5));
            wallet.RestoreSaveData(new GameSaveData { currency = new CurrencySaveData { gold = -1 } });
            Assert.That(wallet.Gold, Is.Zero); Assert.That(changes, Is.EqualTo(1)); Assert.That(refreshes, Is.EqualTo(1));
            wallet.RestoreSaveData(new GameSaveData { currency = new CurrencySaveData { gold = int.MaxValue } });
            Assert.That(wallet.TryAddGold(1).Status, Is.EqualTo(CurrencyMutationStatus.OverflowPrevented));
        }

        [Test]
        public void CharacterProgression_HandlesMultipleLevelsAndMaxSettlement()
        {
            CharacterProgressionDefinitionSO definition = CreateDefinition("hero", 1, 3, 10, 20);
            CharacterProgressionService progression = new GameObject("Progression").AddComponent<CharacterProgressionService>();
            Invoke(progression, "ConfigureForTests", "hero", new[] { definition });
            ExperienceApplyResult result = progression.ApplyExperience("hero", 35);
            Assert.That(result.AppliedExperience, Is.EqualTo(30));
            Assert.That(result.ResultingLevel, Is.EqualTo(3));
            Assert.That(result.LevelsGained, Is.EqualTo(2));
            Assert.That(result.Settled, Is.True);
            Assert.That(progression.ApplyExperience("hero", 5).Settled, Is.True);
            Assert.That(progression.ApplyExperience("missing", 5).Status, Is.EqualTo(ExperienceApplyStatus.UnresolvedCharacter));
        }

        [Test]
        public void RewardReconciliation_AppliesOnlyPendingExperienceOnce()
        {
            CharacterProgressionDefinitionSO definition = CreateDefinition("hero", 1, 10, 10);
            CharacterProgressionService progression = new GameObject("Progression").AddComponent<CharacterProgressionService>();
            Invoke(progression, "ConfigureForTests", "hero", new[] { definition });
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>(); wallet.SetGold(7);
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>(); inventory.AddItem("item", 2);
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            typeof(RewardService).GetField("characterProgressionService", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rewards, progression);
            GameSaveData save = new();
            save.reward.ledger.Add(new RewardLedgerSaveData { sourceType = RewardSourceType.Combat.ToString(), sourceId = "old", requestedGold = 7, gold = 7, requestedExp = 15, exp = 0, requestedItemId = "item", requestedItemCount = 2, itemId = "item", itemCount = 2, progressionTargetId = "hero", expSettled = false });
            rewards.RestoreSaveData(save);
            Assert.That(progression.TryGetState("hero", out int level, out int xp), Is.True);
            Assert.That(level, Is.EqualTo(2)); Assert.That(xp, Is.EqualTo(5));
            Assert.That(wallet.Gold, Is.EqualTo(7)); Assert.That(inventory.GetCount("item"), Is.EqualTo(2));
            Assert.That(rewards.ReconcilePendingExperience(), Is.Zero);
        }

        [Test]
        public void RewardTargetMetadata_DoesNotChangeIdentityOrReplayChannels()
        {
            CharacterProgressionDefinitionSO definition = CreateDefinition("hero", 1, 10, 10);
            CharacterProgressionService progression = new GameObject("Progression").AddComponent<CharacterProgressionService>();
            Invoke(progression, "ConfigureForTests", "hero", new[] { definition });
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            typeof(RewardService).GetField("currencyWallet", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rewards, wallet);
            typeof(RewardService).GetField("characterProgressionService", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rewards, progression);
            RewardGrantResult first = rewards.GrantReward(new RewardGrantRequest(RewardSourceType.QuestCompletion, "quest:q", 5, 4, progressionTargetId: "hero"));
            RewardGrantResult duplicate = rewards.GrantReward(new RewardGrantRequest(RewardSourceType.QuestCompletion, "quest:q", 5, 4, progressionTargetId: "different"));
            Assert.That(first.Gold, Is.EqualTo(5)); Assert.That(first.Exp, Is.EqualTo(4));
            Assert.That(duplicate.DuplicateBlocked, Is.True); Assert.That(duplicate.Gold, Is.Zero); Assert.That(duplicate.Exp, Is.Zero); Assert.That(wallet.Gold, Is.EqualTo(5));
        }

        [Test]
        public void SchemaFive_MigratesPartyLevelAndPendingExperience()
        {
            const string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":5},\"party\":{\"memberLevels\":[{\"id\":\"hero\",\"value\":4}]},\"reward\":{\"ledger\":[{\"sourceType\":\"Combat\",\"sourceId\":\"old\",\"requestedExp\":9,\"exp\":0}]}}";
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            object[] args = { json, null, false, null };
            Assert.That((bool)migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True, args[3] as string);
            GameSaveData data = (GameSaveData)args[1];
            Assert.That(data.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(data.progression.characters[0].characterId, Is.EqualTo("hero"));
            Assert.That(data.progression.characters[0].level, Is.EqualTo(4));
            Assert.That(data.reward.ledger[0].expSettled, Is.False);
        }

        private static CharacterProgressionDefinitionSO CreateDefinition(string id, int start, int max, params int[] curve)
        {
            CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
            SerializedObject serialized = new(definition);
            serialized.FindProperty("characterId").stringValue = id;
            serialized.FindProperty("startingLevel").intValue = start;
            serialized.FindProperty("maximumLevel").intValue = max;
            SerializedProperty values = serialized.FindProperty("experienceRequiredByLevel"); values.arraySize = curve.Length;
            for (int i = 0; i < curve.Length; i++) values.GetArrayElementAtIndex(i).intValue = curve[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void Cleanup()
        {
            foreach (RewardService value in Resources.FindObjectsOfTypeAll<RewardService>()) if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
            foreach (CurrencyWallet value in Resources.FindObjectsOfTypeAll<CurrencyWallet>()) if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
            foreach (InventoryService value in Resources.FindObjectsOfTypeAll<InventoryService>()) if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
            foreach (CharacterProgressionService value in Resources.FindObjectsOfTypeAll<CharacterProgressionService>()) if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
            foreach (CharacterProgressionDefinitionSO value in Resources.FindObjectsOfTypeAll<CharacterProgressionDefinitionSO>()) UnityEngine.Object.DestroyImmediate(value);
            foreach (ItemDefinitionSO value in Resources.FindObjectsOfTypeAll<ItemDefinitionSO>()) UnityEngine.Object.DestroyImmediate(value);
            foreach (ItemCatalogSO value in Resources.FindObjectsOfTypeAll<ItemCatalogSO>()) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
