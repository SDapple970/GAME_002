using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Reward;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class ProductionInteractionTests
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
        public void Runner_AllowsOnlyOneProductionOwner()
        {
            InteractionRunner first = CreateRunner(out _);
            InteractionRunner duplicate = new GameObject("DuplicateRunner").AddComponent<InteractionRunner>();
            Invoke(duplicate, "Awake");

            Assert.That(InteractionRunner.Instance, Is.SameAs(first));
            Assert.That(duplicate.enabled, Is.False);
        }

        [Test]
        public void RuntimeBootstrapper_InstallsOneProductionPairAndRepeatedInstallDoesNotAccumulate()
        {
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>();

            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);
            InteractionRuntime installedRuntime = UnityEngine.Object.FindFirstObjectByType<InteractionRuntime>();
            InteractionRunner installedRunner = UnityEngine.Object.FindFirstObjectByType<InteractionRunner>();
            Invoke(installedRuntime, "Awake");
            Invoke(installedRunner, "Awake");
            InteractionRunner first = InteractionRunner.Instance;
            InteractionRuntime runtime = InteractionRuntime.Instance;
            Invoke(bootstrapper, "BootstrapCoreServices", true, false, false);

            Assert.That(first, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(first.Runtime, Is.SameAs(runtime));
            Assert.That(first.IsCompatibilityFallback, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<InteractionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<InteractionRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void AuthoredPairIsAdoptedByProductionBootstrap()
        {
            InteractionRunner authored = CreateRunner(out InteractionRuntime runtime);
            RuntimeBootstrapper bootstrapper = new GameObject("Bootstrap").AddComponent<RuntimeBootstrapper>();

            Invoke(bootstrapper, "BootstrapCoreServices", false, false, false);

            Assert.That(InteractionRunner.Instance, Is.SameAs(authored));
            Assert.That(InteractionRuntime.Instance, Is.SameAs(runtime));
            Assert.That(authored.Runtime, Is.SameAs(runtime));
            Assert.That(authored.IsCompatibilityFallback, Is.False);

            runtime.RestoreSaveData(ConsumedSave("authored-object"));
            InteractableObject source = CreateSource("authored-object", InteractionUsePolicy.PersistentOnce);
            TestVisualState visual = source.gameObject.AddComponent<TestVisualState>();
            Invoke(source, "OnEnable");
            Assert.That(visual.LastConsumed, Is.True);
        }

        [Test]
        public void OnDemandCompatibilityBootstrapCreatesOnePairAndClearsOwnershipWhenDestroyed()
        {
            InteractionRunner fallback = InteractionRunner.ResolveOrCreate();
            InteractionRuntime runtime = fallback.Runtime;

            Assert.That(fallback.IsCompatibilityFallback, Is.True);
            Assert.That(InteractionRunner.ResolveOrCreate(), Is.SameAs(fallback));
            Assert.That(UnityEngine.Object.FindObjectsByType<InteractionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(fallback.gameObject);
            Assert.That(InteractionRunner.Instance, Is.Null);
            Assert.That(InteractionRuntime.Instance, Is.Null);
            Assert.That(runtime == null, Is.True);
        }

        [Test]
        public void SubsystemRegistrationClearsStaleStaticOwnership()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);

            InvokeStatic(typeof(InteractionRunner), "ResetStaticOwnership");
            InvokeStatic(typeof(InteractionRuntime), "ResetStaticOwnership");

            Assert.That(InteractionRunner.Instance, Is.Null);
            Assert.That(InteractionRuntime.Instance, Is.Null);
            Invoke(runtime, "Awake");
            Invoke(runner, "Awake");
            Assert.That(InteractionRunner.Instance, Is.SameAs(runner));
            Assert.That(InteractionRuntime.Instance, Is.SameAs(runtime));
        }

        [Test]
        public void RepeatableExecutesAgainButSameFrameDuplicateIsBlocked()
        {
            InteractionRunner runner = CreateRunner(out _);
            CountingEventSO interactionEvent = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject source = CreateSource("repeat", InteractionUsePolicy.Repeatable, interactionEvent);

            InteractionResult first = runner.Execute(Request(source));
            InteractionResult duplicate = runner.Execute(Request(source));
            ResetFrameGuard(runner);
            InteractionResult nextFrame = runner.Execute(Request(source));

            Assert.That(first.Status, Is.EqualTo(InteractionResultStatus.Success));
            Assert.That(duplicate.Status, Is.EqualTo(InteractionResultStatus.AlreadyConsumed));
            Assert.That(nextFrame.Status, Is.EqualTo(InteractionResultStatus.Success));
            Assert.That(interactionEvent.ExecutionCount, Is.EqualTo(2));
        }

        [Test]
        public void OncePerSessionAndPersistentOnceUseSeparateRetention()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            CountingEventSO sessionEvent = ScriptableObject.CreateInstance<CountingEventSO>();
            CountingEventSO persistentEvent = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject session = CreateSource("session", InteractionUsePolicy.OncePerSession, sessionEvent);
            InteractableObject persistent = CreateSource("persistent", InteractionUsePolicy.PersistentOnce, persistentEvent);

            Assert.That(runner.Execute(Request(session)).Succeeded, Is.True);
            ResetFrameGuard(runner);
            Assert.That(runner.Execute(Request(session)).Status, Is.EqualTo(InteractionResultStatus.AlreadyConsumed));
            Invoke(runtime, "ResetSessionForTests");
            ResetFrameGuard(runner);
            Assert.That(runner.Execute(Request(session)).Succeeded, Is.True);

            Assert.That(runner.Execute(Request(persistent)).Succeeded, Is.True);
            GameSaveData save = new();
            runtime.CaptureSaveData(save);
            Assert.That(save.world.interactions.Single(item => item.interactionId == "persistent").consumed, Is.True);

            runtime.RestoreSaveData(save);
            ResetFrameGuard(runner);
            Assert.That(runner.Execute(Request(persistent)).Status, Is.EqualTo(InteractionResultStatus.AlreadyConsumed));
        }

        [Test]
        public void PersistentOnceRejectsMissingStableIdentity()
        {
            InteractionRunner runner = CreateRunner(out _);
            InteractableObject source = CreateSource("  ", InteractionUsePolicy.PersistentOnce, ScriptableObject.CreateInstance<CountingEventSO>());

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.InvalidIdentity));
        }

        [Test]
        public void BlockedConditionDoesNotExecuteOrConsume()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            CountingEventSO interactionEvent = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject source = CreateSource("conditioned", InteractionUsePolicy.PersistentOnce, interactionEvent);
            SetField(source, "conditions", new List<InteractionConditionSO> { ScriptableObject.CreateInstance<BlockedConditionSO>() });

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.BlockedCondition));
            Assert.That(interactionEvent.ExecutionCount, Is.Zero);
            Assert.That(runtime.IsConsumed("conditioned", InteractionUsePolicy.PersistentOnce), Is.False);
        }

        [Test]
        public void StableIdBreaksEqualDistanceTargetTieAndConsumedTargetIsIgnored()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            InteractionController controller = new GameObject("PlayerInteraction").AddComponent<InteractionController>();
            InteractableObject laterId = CreateSource("target.z", InteractionUsePolicy.PersistentOnce, ScriptableObject.CreateInstance<CountingEventSO>());
            InteractableObject earlierId = CreateSource("target.a", InteractionUsePolicy.PersistentOnce, ScriptableObject.CreateInstance<CountingEventSO>());
            laterId.transform.position = Vector3.right;
            earlierId.transform.position = Vector3.left;
            controller.Register(laterId);
            controller.Register(earlierId);

            Assert.That(Invoke(controller, "FindNearestInteractable"), Is.SameAs(earlierId));
            runtime.MarkConsumed("target.a", InteractionUsePolicy.PersistentOnce);
            Assert.That(Invoke(controller, "FindNearestInteractable"), Is.SameAs(laterId));
        }

        [Test]
        public void LegacyInteractOnceStillBlocksAfterCompatibilityExecution()
        {
            InteractionRunner runner = CreateRunner(out _);
            InteractableObject source = CreateSource("", InteractionUsePolicy.LegacyCompatibility, ScriptableObject.CreateInstance<CountingEventSO>());
            SetField(source, "interactOnce", true);

            Assert.That(runner.Execute(Request(source)).Succeeded, Is.True);
            Assert.That(source.CanInteract, Is.False);
        }

        [Test]
        public void PartialFailureConsumesAfterAcceptedIrreversibleEffect()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            InteractableObject source = CreateSource(
                "partial",
                InteractionUsePolicy.PersistentOnce,
                ScriptableObject.CreateInstance<CountingEventSO>(),
                ScriptableObject.CreateInstance<FailingEventSO>());

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.PartialFailure));
            Assert.That(runtime.IsConsumed("partial", InteractionUsePolicy.PersistentOnce), Is.True);
        }

        [Test]
        public void RestoreAppliesVisualStateWithoutReexecutingEvents()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            CountingEventSO interactionEvent = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject source = CreateSource("visual", InteractionUsePolicy.PersistentOnce, interactionEvent);
            TestVisualState visual = source.gameObject.AddComponent<TestVisualState>();
            Assert.That(runner.Execute(Request(source)).Succeeded, Is.True);
            GameSaveData save = new();
            runtime.CaptureSaveData(save);
            int executions = interactionEvent.ExecutionCount;
            visual.LastConsumed = false;

            runtime.RestoreSaveData(save);

            Assert.That(visual.LastConsumed, Is.True);
            Assert.That(interactionEvent.ExecutionCount, Is.EqualTo(executions));
        }

        [Test]
        public void RestoreBeforeObjectRegistrationAppliesConsumedVisualOnBind()
        {
            CreateRunner(out InteractionRuntime runtime);
            GameSaveData save = ConsumedSave("late-object");
            runtime.RestoreSaveData(save);
            InteractableObject source = CreateSource("late-object", InteractionUsePolicy.PersistentOnce);
            TestVisualState visual = source.gameObject.AddComponent<TestVisualState>();

            Invoke(source, "OnEnable");

            Assert.That(visual.LastConsumed, Is.True);
            Assert.That(source.CanInteract, Is.False);
        }

        [Test]
        public void ObjectRegistrationBeforeRestoreReceivesStateAndRepeatedRestoreDoesNotExecuteEffects()
        {
            CreateRunner(out InteractionRuntime runtime);
            CountingEventSO effect = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject source = CreateSource("early-object", InteractionUsePolicy.PersistentOnce, effect);
            TestVisualState visual = source.gameObject.AddComponent<TestVisualState>();
            Invoke(source, "OnEnable");
            GameSaveData save = ConsumedSave("early-object");

            runtime.RestoreSaveData(save);
            runtime.RestoreSaveData(save);

            Assert.That(visual.LastConsumed, Is.True);
            Assert.That(effect.ExecutionCount, Is.Zero);
        }

        [Test]
        public void CompatibilityBootstrapCanRestoreBeforeObjectRegistration()
        {
            InteractionRunner fallback = InteractionRunner.ResolveOrCreate();
            Invoke(fallback.Runtime, "Awake");
            Invoke(fallback, "Awake");
            fallback.Runtime.RestoreSaveData(ConsumedSave("compat-object"));
            InteractableObject source = CreateSource("compat-object", InteractionUsePolicy.PersistentOnce);
            TestVisualState visual = source.gameObject.AddComponent<TestVisualState>();

            Invoke(source, "OnEnable");

            Assert.That(fallback.IsCompatibilityFallback, Is.True);
            Assert.That(visual.LastConsumed, Is.True);
        }

        [Test]
        public void SchemaFourInteractionRewardIdentityRemainsConsumedAfterMigration()
        {
            InteractionRunner runner = CreateRunner(out _);
            InventoryService inventory = CreateRewardServices(out RewardService rewards);
            GameSaveData migrated = Migrate(SchemaFourLedgerJson("Interaction", "legacy.reward", null, "token"));
            rewards.RestoreSaveData(migrated);
            RewardInteractionEventSO reward = ScriptableObject.CreateInstance<RewardInteractionEventSO>();
            SetField(reward, "rewardSourceId", " legacy.reward ");
            SetField(reward, "itemId", "token");
            SetField(reward, "showPromptMessage", false);
            InteractableObject source = CreateSource("new-field-id", InteractionUsePolicy.PersistentOnce, reward);

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Reward.DuplicateBlocked, Is.True);
            Assert.That(result.Reward.ItemCount, Is.Zero);
            Assert.That(inventory.GetCount("token"), Is.Zero);
        }

        [Test]
        public void SchemaFourLootIdentityRemainsConsumedAfterMigration()
        {
            InteractionRunner runner = CreateRunner(out _);
            InventoryService inventory = CreateRewardServices(out RewardService rewards);
            GameSaveData migrated = Migrate(SchemaFourLedgerJson("Loot", "legacy.loot", "token", "token"));
            rewards.RestoreSaveData(migrated);
            RandomLootInteractionEventSO loot = ScriptableObject.CreateInstance<RandomLootInteractionEventSO>();
            SetField(loot, "rewardSourceId", "legacy.loot");
            SetField(loot, "showMessage", false);
            SetField(loot, "entries", new[] { new RandomLootEntry { entryId = "stable", itemId = "token", amount = 1, weight = 1f } });
            InteractableObject source = CreateSource("new-loot-field", InteractionUsePolicy.PersistentOnce, loot);

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Reward.DuplicateBlocked, Is.True);
            Assert.That(result.Reward.ItemCount, Is.Zero);
            Assert.That(inventory.GetCount("token"), Is.Zero);
        }

        [Test]
        public void ProductionAndLegacyEventsEachExecuteExactlyOnceThroughTheirSingleRoute()
        {
            InteractionRunner runner = CreateRunner(out _);
            CountingEventSO production = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject productionSource = CreateSource("production-once", InteractionUsePolicy.Repeatable, production);
            Assert.That(runner.Execute(Request(productionSource)).Succeeded, Is.True);
            Assert.That(production.ProductionCount, Is.EqualTo(1));
            Assert.That(production.LegacyCount, Is.Zero);

            ResetFrameGuard(runner);
            LegacyCountingEventSO legacy = ScriptableObject.CreateInstance<LegacyCountingEventSO>();
            InteractableObject legacySource = CreateSource("legacy-once", InteractionUsePolicy.LegacyCompatibility, legacy);
            Assert.That(runner.Execute(Request(legacySource)).Succeeded, Is.True);
            Assert.That(legacy.LegacyCount, Is.EqualTo(1));
        }

        [Test]
        public void AuthoredOrderIsPreservedAndFailedEventIsNotRetriedWithinRequest()
        {
            InteractionRunner runner = CreateRunner(out _);
            List<string> order = new();
            OrderedEventSO first = ScriptableObject.CreateInstance<OrderedEventSO>();
            OrderedEventSO failed = ScriptableObject.CreateInstance<OrderedEventSO>();
            OrderedEventSO last = ScriptableObject.CreateInstance<OrderedEventSO>();
            first.Configure("first", order, false);
            failed.Configure("failed", order, true);
            last.Configure("last", order, false);
            InteractableObject source = CreateSource("ordered", InteractionUsePolicy.Repeatable, first, failed, last);

            InteractionResult result = runner.Execute(Request(source));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.PartialFailure));
            Assert.That(order, Is.EqualTo(new[] { "first", "failed", "last" }));
            Assert.That(failed.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ConsumedAndReentrantRequestsCannotExecuteSequenceAgain()
        {
            InteractionRunner runner = CreateRunner(out _);
            ReentrantEventSO reentrant = ScriptableObject.CreateInstance<ReentrantEventSO>();
            CountingEventSO tail = ScriptableObject.CreateInstance<CountingEventSO>();
            InteractableObject source = CreateSource("reentrant", InteractionUsePolicy.PersistentOnce, reentrant, tail);

            InteractionResult first = runner.Execute(Request(source));
            ResetFrameGuard(runner);
            InteractionResult consumed = runner.Execute(Request(source));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(reentrant.Calls, Is.EqualTo(1));
            Assert.That(reentrant.ReentrantStatus, Is.EqualTo(InteractionResultStatus.BlockedState));
            Assert.That(tail.ProductionCount, Is.EqualTo(1));
            Assert.That(consumed.Status, Is.EqualTo(InteractionResultStatus.AlreadyConsumed));
            Assert.That(reentrant.Calls, Is.EqualTo(1));
            Assert.That(tail.ProductionCount, Is.EqualTo(1));
        }

        [Test]
        public void SameEventAssetOnDifferentObjectsUsesIndependentRewardIdentity()
        {
            InteractionRunner runner = CreateRunner(out _);
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            Invoke(inventory, "Awake");
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            Invoke(rewards, "Awake");
            RewardInteractionEventSO reward = ScriptableObject.CreateInstance<RewardInteractionEventSO>();
            SetField(reward, "itemId", "token");
            SetField(reward, "amount", 1);
            SetField(reward, "showPromptMessage", false);

            InteractableObject first = CreateSource("chest.a", InteractionUsePolicy.PersistentOnce, reward);
            InteractableObject second = CreateSource("chest.b", InteractionUsePolicy.PersistentOnce, reward);
            Assert.That(runner.Execute(Request(first)).Succeeded, Is.True);
            ResetFrameGuard(runner);
            Assert.That(runner.Execute(Request(second)).Succeeded, Is.True);

            Assert.That(inventory.GetCount("token"), Is.EqualTo(2));
        }

        [Test]
        public void StableActionIdSurvivesEventReordering()
        {
            CountingEventSO first = ScriptableObject.CreateInstance<CountingEventSO>();
            CountingEventSO second = ScriptableObject.CreateInstance<CountingEventSO>();
            SetField(first, "actionId", "open");
            SetField(second, "actionId", "inspect");

            Assert.That(InteractionIdentity.ResolveActionId(first, 0), Is.EqualTo("open"));
            Assert.That(InteractionIdentity.ResolveActionId(first, 1), Is.EqualTo("open"));
            Assert.That(InteractionIdentity.ResolveActionId(second, 0), Is.EqualTo("inspect"));
            Assert.That(InteractionIdentity.ResolveActionId(ScriptableObject.CreateInstance<CountingEventSO>(), 2), Is.EqualTo("event:2"));
        }

        [Test]
        public void RandomOutcomeIsSavedBeforeDependencyFailureAndDoesNotReroll()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            RandomLootInteractionEventSO loot = ScriptableObject.CreateInstance<RandomLootInteractionEventSO>();
            RandomLootEntry only = new() { entryId = "fixed", itemId = "token", amount = 1, weight = 1f };
            SetField(loot, "entries", new[] { only });
            InteractableObject source = CreateSource("loot.fixed", InteractionUsePolicy.PersistentOnce, loot);

            InteractionResult failed = runner.Execute(Request(source));
            Assert.That(failed.Status, Is.EqualTo(InteractionResultStatus.Failed));
            GameSaveData save = new();
            runtime.CaptureSaveData(save);
            Assert.That(save.world.interactions.Single().resolvedOutcomes.Single().outcomeId, Is.EqualTo("fixed"));

            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            Invoke(inventory, "Awake");
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            Invoke(rewards, "Awake");
            ResetFrameGuard(runner);
            Assert.That(runner.Execute(Request(source)).Succeeded, Is.True);
            Assert.That(inventory.GetCount("token"), Is.EqualTo(1));
        }

        [Test]
        public void NothingLootConsumesPersistentInteraction()
        {
            InteractionRunner runner = CreateRunner(out InteractionRuntime runtime);
            RandomLootInteractionEventSO loot = ScriptableObject.CreateInstance<RandomLootInteractionEventSO>();
            SetField(loot, "entries", new[] { new RandomLootEntry { entryId = "none", isNothing = true, weight = 1f } });
            InteractableObject source = CreateSource("loot.none", InteractionUsePolicy.PersistentOnce, loot);

            Assert.That(runner.Execute(Request(source)).Succeeded, Is.True);
            Assert.That(runtime.IsConsumed("loot.none", InteractionUsePolicy.PersistentOnce), Is.True);
        }

        [Test]
        public void SchemaFourMigratesWithEmptyInteractionState()
        {
            string json = "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":4},\"world\":{\"clearedEncounterIds\":[\"combat.1\"]}}";
            GameSaveData migrated = Migrate(json);

            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.world.interactions, Is.Empty);
            Assert.That(migrated.world.clearedEncounterIds, Is.EqualTo(new[] { "combat.1" }));
        }

        [Test]
        public void InteractionStatesNormalizeWithout256EntryTruncation()
        {
            GameSaveData data = new();
            for (int i = 0; i < 300; i++)
                data.world.interactions.Add(new InteractionStateSaveData { interactionId = $"object.{i:D3}", consumed = true });
            data.world.interactions.Add(new InteractionStateSaveData { interactionId = " object.000 ", consumed = false });

            GameSaveData normalized = Migrate(SaveSerializer.ToJson(data));

            Assert.That(normalized.world.interactions, Has.Count.EqualTo(300));
            Assert.That(normalized.world.interactions.First().interactionId, Is.EqualTo("object.000"));
            Assert.That(normalized.world.interactions.First().consumed, Is.True);
        }

        [Test]
        public void OversizedInteractionStateIsRejectedExplicitly()
        {
            GameSaveData data = new();
            for (int i = 0; i < 100001; i++)
                data.world.interactions.Add(new InteractionStateSaveData { interactionId = $"object.{i}" });

            Type validator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataValidator");
            MethodInfo method = validator.GetMethod("TryValidateCollectionSizes", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { data, null };

            Assert.That((bool)method.Invoke(null, args), Is.False);
            Assert.That(args[1] as string, Does.Contain("maximum supported"));
            Assert.That(data.world.interactions, Has.Count.EqualTo(100001));
        }

        private static InteractionRunner CreateRunner(out InteractionRuntime runtime)
        {
            GameObject owner = new("InteractionProduction");
            runtime = owner.AddComponent<InteractionRuntime>();
            Invoke(runtime, "Awake");
            InteractionRunner runner = owner.AddComponent<InteractionRunner>();
            SetField(runner, "runtime", runtime);
            Invoke(runner, "Awake");
            return runner;
        }

        private static InventoryService CreateRewardServices(out RewardService rewards)
        {
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            Invoke(inventory, "Awake");
            rewards = new GameObject("Rewards").AddComponent<RewardService>();
            Invoke(rewards, "Awake");
            return inventory;
        }

        private static GameSaveData ConsumedSave(string interactionId)
        {
            GameSaveData save = new();
            save.world.interactions.Add(new InteractionStateSaveData
            {
                interactionId = interactionId,
                consumed = true
            });
            return save;
        }

        private static string SchemaFourLedgerJson(string sourceType, string sourceId, string actionId, string itemId)
        {
            string action = actionId == null ? string.Empty : $",\"actionId\":\"{actionId}\"";
            return "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":4}," +
                   "\"reward\":{\"ledger\":[{" +
                   $"\"sourceType\":\"{sourceType}\",\"sourceId\":\"{sourceId}\"{action}," +
                   $"\"requestedItemId\":\"{itemId}\",\"requestedItemCount\":1,\"itemId\":\"{itemId}\",\"itemCount\":1" +
                   "}]}}";
        }

        private static InteractableObject CreateSource(string id, InteractionUsePolicy policy, params InteractionEventSO[] events)
        {
            GameObject owner = new(id ?? "interaction");
            owner.AddComponent<BoxCollider2D>();
            InteractableObject source = owner.AddComponent<InteractableObject>();
            SetField(source, "interactionId", id);
            SetField(source, "usePolicy", policy);
            SetField(source, "events", new List<InteractionEventSO>(events));
            return source;
        }

        private static InteractionRequest Request(InteractableObject source)
        {
            return new InteractionRequest(source, source.InteractionId, null, null, source.UsePolicy, source.Events);
        }

        private static void ResetFrameGuard(InteractionRunner runner)
        {
            SetField(runner, "_lastExecutionFrame", -1);
            SetField(runner, "_lastExecutionId", null);
        }

        private static GameSaveData Migrate(string json)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            MethodInfo method = migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { json, null, false, null };
            Assert.That((bool)method.Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo candidate = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            return candidate.Invoke(target, arguments);
        }

        private static void Cleanup()
        {
            HashSet<GameObject> owners = new();
            foreach (MonoBehaviour item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item is InteractionRunner || item is InteractionRuntime || item is InteractionController || item is InteractableObject ||
                    item is RuntimeBootstrapper || item is SaveLoadService || item is GameStateMachine || item is GameFlowController ||
                    item is SceneFlowController || item is GameInputInstaller || item is RewardService || item is InventoryService ||
                    item is CurrencyWallet || item is GameUIRootController || item is UIScreenRouter)
                {
                    if (item != null)
                        owners.Add(item.gameObject);
                }
            }

            foreach (GameObject owner in owners)
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);

            InvokeStatic(typeof(InteractionRunner), "ResetOwnershipForTests");
            InvokeStatic(typeof(InteractionRuntime), "ResetOwnershipForTests");
        }

        private static void InvokeStatic(Type type, string method)
        {
            type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
        }

        public sealed class CountingEventSO : InteractionEventSO
        {
            public int ProductionCount { get; private set; }
            public int LegacyCount { get; private set; }
            public int ExecutionCount => ProductionCount + LegacyCount;
            public override bool SupportsProductionExecution => true;
            public override void Execute(InteractionContext context) => LegacyCount++;
            public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
            {
                ProductionCount++;
                return InteractionEventResult.AcceptedResult(true, true);
            }
        }

        public sealed class LegacyCountingEventSO : InteractionEventSO
        {
            public int LegacyCount { get; private set; }
            public override void Execute(InteractionContext context) => LegacyCount++;
        }

        public sealed class OrderedEventSO : InteractionEventSO
        {
            private string _id;
            private List<string> _order;
            private bool _fail;
            public int Calls { get; private set; }
            public override bool SupportsProductionExecution => true;
            public void Configure(string id, List<string> order, bool fail)
            {
                _id = id;
                _order = order;
                _fail = fail;
            }
            public override void Execute(InteractionContext context) { }
            public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
            {
                Calls++;
                _order.Add(_id);
                return _fail
                    ? InteractionEventResult.Failed("failed")
                    : InteractionEventResult.AcceptedResult(true, true);
            }
        }

        public sealed class ReentrantEventSO : InteractionEventSO
        {
            public int Calls { get; private set; }
            public InteractionResultStatus ReentrantStatus { get; private set; }
            public override bool SupportsProductionExecution => true;
            public override void Execute(InteractionContext context) { }
            public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
            {
                Calls++;
                ReentrantStatus = InteractionRunner.Instance.Execute(context.Request).Status;
                return InteractionEventResult.AcceptedResult(true, true);
            }
        }

        public sealed class BlockedConditionSO : InteractionConditionSO
        {
            public override bool IsMet(InteractionContext context, out string blockedReason)
            {
                blockedReason = "interaction.test-blocked";
                return false;
            }
        }

        public sealed class FailingEventSO : InteractionEventSO
        {
            public override bool SupportsProductionExecution => true;
            public override void Execute(InteractionContext context) { }
            public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
            {
                return InteractionEventResult.Failed("interaction.test-failure");
            }
        }

        public sealed class TestVisualState : MonoBehaviour, IInteractionVisualState
        {
            public bool LastConsumed { get; set; }
            public void ApplyConsumedState(bool consumed) => LastConsumed = consumed;
        }
    }
}
