using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Reward;
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

        private static object Invoke(object target, string method)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void Cleanup()
        {
            HashSet<GameObject> owners = new();
            foreach (MonoBehaviour item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item is InteractionRunner || item is InteractionRuntime || item is InteractionController || item is InteractableObject ||
                    item is RewardService || item is InventoryService || item is CurrencyWallet)
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
            public int ExecutionCount { get; private set; }
            public override bool SupportsProductionExecution => true;
            public override void Execute(InteractionContext context) => ExecutionCount++;
            public override InteractionEventResult ExecuteProduction(InteractionExecutionContext context)
            {
                ExecutionCount++;
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
