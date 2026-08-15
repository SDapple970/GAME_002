using System.Reflection;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.World.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class ExplorationFoundationBatch9Tests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (MonoBehaviour item in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item is ExplorationResourceRuntime || item is PersistentConditionRuntime || item is FeastService || item is InventoryService)
                    Object.DestroyImmediate(item.gameObject);
        }

        [Test]
        public void Shining_ValidChangesAndSaveRestoreAreValidated()
        {
            ExplorationResourceRuntime runtime = New<ExplorationResourceRuntime>();
            int changes = 0; runtime.Changed += _ => changes++;
            Assert.That(runtime.TryAddShining(5), Is.True);
            Assert.That(runtime.TrySpendShining(2), Is.True);
            Assert.That(runtime.TrySpendShining(4), Is.False);
            Assert.That(runtime.TryAddShining(0), Is.False);
            Assert.That(runtime.Shining, Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(2));
            GameSaveData save = new(); runtime.CaptureSaveData(save);
            runtime.TrySetShining(0); runtime.RestoreSaveData(save);
            Assert.That(runtime.Shining, Is.EqualTo(3));
        }

        [Test]
        public void Hunger_ChangesOnlyThroughExplicitCallsAndRoundTrips()
        {
            ExplorationResourceRuntime runtime = New<ExplorationResourceRuntime>();
            Assert.That(runtime.Hunger, Is.Zero);
            Assert.That(runtime.TryChangeHunger(4), Is.True);
            Assert.That(runtime.TryChangeHunger(-5), Is.False);
            GameSaveData save = new(); runtime.CaptureSaveData(save);
            runtime.TrySetHunger(1); runtime.RestoreSaveData(save);
            Assert.That(runtime.Hunger, Is.EqualTo(4));
        }

        [Test]
        public void Conditions_ArePerCharacterTypedUniqueAndRestoreSilently()
        {
            PersistentConditionRuntime runtime = New<PersistentConditionRuntime>();
            int changes = 0; runtime.Changed += _ => changes++;
            Assert.That(runtime.TryAcquire("hero", "flu", PersistentConditionCategory.Disease), Is.EqualTo(PersistentConditionMutationStatus.Success));
            Assert.That(runtime.TryAcquire("hero", "flu", PersistentConditionCategory.Disease), Is.EqualTo(PersistentConditionMutationStatus.AlreadyAcquired));
            Assert.That(runtime.TryAcquire("hero", "flu", PersistentConditionCategory.Quirk), Is.EqualTo(PersistentConditionMutationStatus.Success));
            Assert.That(runtime.HasCondition("hero", "flu", PersistentConditionCategory.Disease), Is.True);
            Assert.That(runtime.HasCondition("hero", "flu", PersistentConditionCategory.Quirk), Is.True);
            GameSaveData save = new(); runtime.CaptureSaveData(save);
            runtime.TryRemove("hero", "flu", PersistentConditionCategory.Disease);
            int beforeRestore = changes;
            runtime.RestoreSaveData(save);
            Assert.That(changes, Is.EqualTo(beforeRestore));
            Assert.That(runtime.HasCondition("hero", "flu", PersistentConditionCategory.Disease), Is.True);
            Assert.That(runtime.TryRemove("hero", "flu", PersistentConditionCategory.Disease), Is.EqualTo(PersistentConditionMutationStatus.Success));
        }

        [Test]
        public void Feast_ValidatesBeforeMutatingBothOwners()
        {
            InventoryService inventory = New<InventoryService>();
            ExplorationResourceRuntime resources = New<ExplorationResourceRuntime>();
            FeastService feast = New<FeastService>();
            inventory.TryAddItem("ration", 2);
            Assert.That(feast.TryFeast(new FeastRequest("ration", 3, 5)).Status, Is.EqualTo(FeastStatus.InsufficientItems));
            Assert.That(inventory.GetCount("ration"), Is.EqualTo(2)); Assert.That(resources.Hunger, Is.Zero);
            Assert.That(feast.TryFeast(new FeastRequest("ration", 1, 5)).Succeeded, Is.True);
            Assert.That(inventory.GetCount("ration"), Is.EqualTo(1)); Assert.That(resources.Hunger, Is.EqualTo(5));
        }

        [Test]
        public void SaveDefaultsAndInvalidConditionsNormalizeSafely()
        {
            GameSaveData data = new();
            data.exploration.shining = -2; data.exploration.hunger = -3;
            data.exploration.conditions.Add(new PersistentConditionSaveData { ownerId = " ", conditionId = "unknown", category = (int)PersistentConditionCategory.Disease });
            InvokeNormalize(data);
            Assert.That(data.exploration.shining, Is.Zero); Assert.That(data.exploration.hunger, Is.Zero);
            Assert.That(data.exploration.conditions, Is.Empty);
            Assert.That((int)PersistentConditionCategory.Disease, Is.EqualTo(0));
            Assert.That((int)PersistentConditionCategory.Quirk, Is.EqualTo(10));
        }

        [Test]
        public void ConditionCatalog_ResolvesStableIdsAndUnknownIdsFailSafely()
        {
            PersistentConditionDefinitionSO definition = ScriptableObject.CreateInstance<PersistentConditionDefinitionSO>();
            PersistentConditionCatalogSO catalog = ScriptableObject.CreateInstance<PersistentConditionCatalogSO>();
            SetField(definition, "conditionId", " disease.flu ");
            SetField(catalog, "definitions", new System.Collections.Generic.List<PersistentConditionDefinitionSO> { definition });
            Assert.That(catalog.TryGet("disease.flu", out PersistentConditionDefinitionSO resolved), Is.True);
            Assert.That(resolved, Is.SameAs(definition));
            Assert.That(catalog.TryGet("missing", out _), Is.False);
            Object.DestroyImmediate(catalog); Object.DestroyImmediate(definition);
        }

        private static T New<T>() where T : MonoBehaviour => new GameObject(typeof(T).Name).AddComponent<T>();

        private static void InvokeNormalize(GameSaveData data)
        {
            System.Type validator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataValidator");
            validator.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { data });
        }

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
