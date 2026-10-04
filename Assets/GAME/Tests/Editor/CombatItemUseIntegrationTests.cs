#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatItemUseIntegrationTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void StandoffItemCommand_ExposesReadOnlyOption_HealsOnceAndConsumesOnce()
        {
            CombatItemDefinitionSO item = CreateHealItem("test.heal", CombatItemTargetRule.SingleAlly, 4);
            Fixture fixture = CreateFixture(item, 1);
            fixture.Session.GetCombatState(fixture.Ally).ApplyDamage(6);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            fixture.Bind();
            Assert.That(controller.Bind(fixture.Orchestrator, fixture.Session), Is.True);

            Assert.That(controller.SelectActor(fixture.Ally), Is.True);
            CombatItemOption option = GetOnlyItem(controller);
            Assert.That(option.DisplayName, Is.EqualTo("test.heal"));
            Assert.That(option.Count, Is.EqualTo(1));
            Assert.That(controller.SelectCombatItem(option), Is.True);
            Assert.That(Contains(controller.ViewState.SelectableTargets, fixture.Ally), Is.True);
            Assert.That(controller.SelectTarget(fixture.Ally), Is.True);

            Assert.That(controller.Confirm(), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentHp, Is.EqualTo(8));
            Assert.That(fixture.Inventory.GetCount("test.heal"), Is.Zero);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(controller.Confirm(), Is.False);
            Assert.That(fixture.Inventory.GetCount("test.heal"), Is.Zero);
        }

        [Test]
        public void InvalidTargetFullHpAndStaleCommand_DoNotConsumeInventory()
        {
            CombatItemDefinitionSO item = CreateHealItem("test.heal", CombatItemTargetRule.SingleAlly, 4);
            Fixture fixture = CreateFixture(item, 2);
            fixture.Bind();
            int version = fixture.Session.ExchangeState.Version;

            CombatItemUseRequest wrongSide = new CombatItemUseRequest(
                item.ItemId, fixture.Ally, fixture.Enemy, version);
            Assert.That(fixture.Orchestrator.TryUseCombatItem(wrongSide, out CombatItemUseResult wrongSideResult), Is.False);
            Assert.That(wrongSideResult.Status, Is.EqualTo(CombatItemUseStatus.InvalidTarget));
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.EqualTo(2));

            CombatItemUseRequest fullHp = new CombatItemUseRequest(
                item.ItemId, fixture.Ally, fixture.Ally, version);
            Assert.That(fixture.Orchestrator.TryUseCombatItem(fullHp, out CombatItemUseResult fullHpResult), Is.False);
            Assert.That(fullHpResult.Status, Is.EqualTo(CombatItemUseStatus.EffectNotApplicable));
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.EqualTo(2));

            CombatItemUseRequest stale = new CombatItemUseRequest(
                item.ItemId, fixture.Ally, fixture.AllyTwo, version + 1);
            Assert.That(fixture.Orchestrator.TryUseCombatItem(stale, out CombatItemUseResult staleResult), Is.False);
            Assert.That(staleResult.Status, Is.EqualTo(CombatItemUseStatus.StaleRequest));
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.EqualTo(2));
        }

        [Test]
        public void PanickedItemUser_IsRejectedBeforeInventoryOrEffectMutation()
        {
            CombatItemDefinitionSO item = CreateHealItem("test.panic", CombatItemTargetRule.Self, 4);
            Fixture fixture = CreateFixture(item, 1);
            fixture.Session.GetCombatState(fixture.Ally).ApplyDamage(4);
            fixture.Bind();
            CombatantCombatState state = fixture.Session.GetCombatState(fixture.Ally);
            state.ApplyMentalDelta(-state.MaxMental);
            int version = fixture.Session.ExchangeState.Version;

            CombatItemUseRequest request = new CombatItemUseRequest(item.ItemId, fixture.Ally, fixture.Ally, version);
            Assert.That(fixture.Orchestrator.TryUseCombatItem(request, out CombatItemUseResult result), Is.False);
            Assert.That(result.Status, Is.EqualTo(CombatItemUseStatus.InvalidRequest));
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.EqualTo(1));
            Assert.That(state.CurrentHp, Is.EqualTo(6));
            Assert.That(fixture.Session.ExchangeState.Version, Is.EqualTo(version));
        }

        [Test]
        public void StatusItem_UsesCombatStatusRuntimeExactlyOnce()
        {
            CombatStatusDefinitionSO status = CreateStatus("test.guard", CombatStatusStackPolicy.Stack, 2);
            CombatItemDefinitionSO item = CreateStatusItem("test.guard.item", CombatItemTargetRule.Self, status);
            Fixture fixture = CreateFixture(item, 1);
            fixture.Bind();
            int version = fixture.Session.ExchangeState.Version;
            CombatItemUseRequest request = new CombatItemUseRequest(item.ItemId, fixture.Ally, fixture.Ally, version);

            Assert.That(fixture.Orchestrator.TryUseCombatItem(request, out CombatItemUseResult result), Is.True);
            Assert.That(result.ItemConsumed, Is.True);
            Assert.That(result.EffectApplied, Is.True);
            Assert.That(result.StatusApplication, Is.Not.Null);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).HasStatus(status.StatusId), Is.True);
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.Zero);

            Assert.That(fixture.Orchestrator.TryUseCombatItem(request, out CombatItemUseResult duplicate), Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(CombatItemUseStatus.StaleRequest));
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.Zero);
        }

        [Test]
        public void EffectFailureAfterRemoval_RollsInventoryBackWithoutCombatMutation()
        {
            CombatItemDefinitionSO item = CreateHealItem("test.rollback", CombatItemTargetRule.Self, 4);
            Fixture fixture = CreateFixture(item, 1, useFailingExecutor: true);
            fixture.Session.GetCombatState(fixture.Ally).ApplyDamage(5);
            fixture.Bind();
            int version = fixture.Session.ExchangeState.Version;
            CombatItemUseRequest request = new CombatItemUseRequest(item.ItemId, fixture.Ally, fixture.Ally, version);

            Assert.That(fixture.Orchestrator.TryUseCombatItem(request, out CombatItemUseResult result), Is.False);
            Assert.That(result.Status, Is.EqualTo(CombatItemUseStatus.EffectFailed));
            Assert.That(result.ItemConsumed, Is.False);
            Assert.That(result.EffectApplied, Is.False);
            Assert.That(fixture.Inventory.GetCount(item.ItemId), Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentHp, Is.EqualTo(5));
        }

        [Test]
        public void InventorySavePath_CapturesAndRestoresPostUseQuantity()
        {
            CombatItemDefinitionSO item = CreateHealItem("test.save", CombatItemTargetRule.Self, 2);
            Fixture fixture = CreateFixture(item, 2);
            fixture.Session.GetCombatState(fixture.Ally).ApplyDamage(4);
            fixture.Bind();
            CombatItemUseRequest request = new CombatItemUseRequest(
                item.ItemId,
                fixture.Ally,
                fixture.Ally,
                fixture.Session.ExchangeState.Version);

            Assert.That(fixture.Orchestrator.TryUseCombatItem(request, out _), Is.True);
            GameSaveData save = new GameSaveData();
            fixture.Inventory.CaptureSaveData(save);
            Assert.That(save.inventory.items, Has.Count.EqualTo(1));
            Assert.That(save.inventory.items[0].id, Is.EqualTo(item.ItemId));
            Assert.That(save.inventory.items[0].value, Is.EqualTo(1));

            InventoryService restored = CreateInventory(item.ItemId);
            restored.RestoreSaveData(save);
            Assert.That(restored.GetCount(item.ItemId), Is.EqualTo(1));
        }

        private Fixture CreateFixture(
            CombatItemDefinitionSO item,
            int inventoryCount,
            bool useFailingExecutor = false)
        {
            InventoryService inventory = CreateInventory(item.ItemId);
            Assert.That(inventory.TryAddItem(item.ItemId, inventoryCount).Status, Is.EqualTo(InventoryMutationStatus.Success));

            CombatItemCatalogSO catalog = ScriptableObject.CreateInstance<CombatItemCatalogSO>();
            _createdObjects.Add(catalog);
            SetPrivateField(catalog, "items", new List<CombatItemDefinitionSO> { item });

            DummyCombatant ally = new DummyCombatant(1, Side.Allies, 10, KeywordMask.None, 3);
            DummyCombatant allyTwo = new DummyCombatant(2, Side.Allies, 10, KeywordMask.None, 3);
            DummyCombatant enemy = new DummyCombatant(100, Side.Enemies, 10, KeywordMask.None, 3);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 3),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                CombatRuntimeConfig.Compatibility);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            GameObject owner = new GameObject("CombatItemUseIntegrationTests.Orchestrator");
            _createdObjects.Add(owner);
            CombatFlowOrchestrator orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
            CombatItemUseExecutor executor = useFailingExecutor
                ? new FailingEffectExecutor(inventory, catalog)
                : new CombatItemUseExecutor(inventory, catalog);
            orchestrator.RegisterCombatItemUseExecutor(executor);

            return new Fixture(session, stateMachine, orchestrator, inventory, ally, allyTwo, enemy);
        }

        private InventoryService CreateInventory(string itemId)
        {
            ItemDefinitionSO definition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            ItemCatalogSO catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            _createdObjects.Add(definition);
            _createdObjects.Add(catalog);
            SetPrivateField(definition, "itemId", itemId);
            SetPrivateField(catalog, "items", new List<ItemDefinitionSO> { definition });

            GameObject owner = new GameObject("CombatItemUseIntegrationTests.Inventory");
            _createdObjects.Add(owner);
            InventoryService inventory = owner.AddComponent<InventoryService>();
            SetPrivateField(inventory, "itemCatalog", catalog);
            return inventory;
        }

        private CombatItemDefinitionSO CreateHealItem(
            string itemId,
            CombatItemTargetRule targetRule,
            int recovery)
        {
            CombatItemDefinitionSO item = ScriptableObject.CreateInstance<CombatItemDefinitionSO>();
            _createdObjects.Add(item);
            SetPrivateField(item, "itemId", itemId);
            SetPrivateField(item, "targetRule", targetRule);
            SetPrivateField(item, "effectKind", CombatItemEffectKind.RestoreHp);
            SetPrivateField(item, "hpRecovery", recovery);
            return item;
        }

        private CombatItemDefinitionSO CreateStatusItem(
            string itemId,
            CombatItemTargetRule targetRule,
            CombatStatusDefinitionSO status)
        {
            CombatItemDefinitionSO item = ScriptableObject.CreateInstance<CombatItemDefinitionSO>();
            _createdObjects.Add(item);
            SetPrivateField(item, "itemId", itemId);
            SetPrivateField(item, "targetRule", targetRule);
            SetPrivateField(item, "effectKind", CombatItemEffectKind.ApplyStatus);
            SetPrivateField(item, "appliedStatus", status);
            return item;
        }

        private CombatStatusDefinitionSO CreateStatus(
            string id,
            CombatStatusStackPolicy policy,
            int maximumStacks)
        {
            CombatStatusDefinitionSO status = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            _createdObjects.Add(status);
            SetPrivateField(status, "statusId", id);
            SetPrivateField(status, "stackPolicy", policy);
            SetPrivateField(status, "maximumStacks", maximumStacks);
            return status;
        }

        private static CombatItemOption GetOnlyItem(FinalCombatPlayerCommandController controller)
        {
            Assert.That(controller.ViewState.SelectableItems.Count, Is.EqualTo(1));
            return controller.ViewState.SelectableItems[0];
        }

        private static bool Contains(IReadOnlyList<ICombatant> values, ICombatant expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], expected))
                    return true;
            }

            return false;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class Fixture
        {
            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                CombatFlowOrchestrator orchestrator,
                InventoryService inventory,
                DummyCombatant ally,
                DummyCombatant allyTwo,
                DummyCombatant enemy)
            {
                Session = session;
                StateMachine = stateMachine;
                Orchestrator = orchestrator;
                Inventory = inventory;
                Ally = ally;
                AllyTwo = allyTwo;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public InventoryService Inventory { get; }
            public DummyCombatant Ally { get; }
            public DummyCombatant AllyTwo { get; }
            public DummyCombatant Enemy { get; }

            public void Bind()
            {
                Assert.That(Orchestrator.BindFinalExchange(
                    Session,
                    StateMachine,
                    new ZeroRandomSource(),
                    new FixedClashInputProvider(2, 1)), Is.True);
            }
        }

        private sealed class FailingEffectExecutor : CombatItemUseExecutor
        {
            public FailingEffectExecutor(InventoryService inventory, CombatItemCatalogSO catalog)
                : base(inventory, catalog)
            {
            }

            protected override bool TryApplyEffect(
                CombatantCombatState targetState,
                CombatItemDefinitionSO definition,
                out int hpRecovered,
                out CombatStatusApplicationResult statusApplication)
            {
                hpRecovered = 0;
                statusApplication = null;
                return false;
            }
        }

        private sealed class ZeroRandomSource : ICombatRuleRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private sealed class FixedClashInputProvider : ICombatClashSideInputProvider
        {
            private readonly int _attackerPower;
            private readonly int _defenderPower;

            public FixedClashInputProvider(int attackerPower, int defenderPower)
            {
                _attackerPower = attackerPower;
                _defenderPower = defenderPower;
            }

            public bool TryCreate(
                ICombatant actor,
                ISkill skill,
                bool isAttacker,
                out CombatClashSideInput input)
            {
                input = new CombatClashSideInput(
                    actor.Id,
                    skill.Id,
                    isAttacker ? _attackerPower : _defenderPower,
                    skill.Speed,
                    isAttacker);
                return true;
            }
        }
    }
}
#endif
