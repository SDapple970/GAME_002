#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.NonCombat.Inventory;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Exercises the existing item adapter path in an unsaved Editor Play Mode scene.
    /// </summary>
    public sealed class CombatItemUsePlayModeIntegrationTests
    {
        private readonly List<string> _runtimeErrors = new List<string>();

        [UnityTest]
        [Category("CBT_ITEM_01")]
        public IEnumerator IsolatedFinalExchange_ItemAdapterInventoryAndBinderRunInActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);

            yield return RunScenario();

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private IEnumerator RunScenario()
        {
            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                RuntimeFixture fixture = CreateFixture();
                BindWithSynchronousPresentation(fixture);
                fixture.Binder.Bind(fixture.Session, fixture.Machine, fixture.Orchestrator);
                yield return null;

                Assert.That(fixture.Inventory == null, Is.False, "The isolated InventoryService must survive the first Play Mode frame.");
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(2));
                Assert.That(fixture.AllyTwoState.CurrentHp, Is.EqualTo(4));
                Assert.That(fixture.AllyTwo.HP, Is.EqualTo(4), "Combat item target validation reads ICombatant.HP through the session binding.");
                Assert.That(fixture.AdapterExecutor, Is.Not.Null, "The adapter must create its existing executor.");
                Assert.That(fixture.RegisteredExecutor, Is.SameAs(fixture.AdapterExecutor), "The adapter must register its executor with the production orchestrator.");
                Assert.That(fixture.Orchestrator.PendingDecisionRequest, Is.Not.Null);
                Assert.That(fixture.Orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
                Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
                Assert.That(fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion, Is.EqualTo(fixture.Session.ExchangeState.Version));
                Assert.That(fixture.Machine.Phase, Is.EqualTo(Phase.Standoff));
                Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Allies));
                Assert.That(fixture.Orchestrator.GetUsableCombatItems(fixture.Ally), Is.Not.Empty);
                Click(FindButton(fixture.ActorListRoot, "1"));
                Assert.That(FindButton(fixture.SkillListRoot, "ITEM Test Ally Heal x2"), Is.Not.Null);
                Click(FindButton(fixture.SkillListRoot, "ITEM Test Ally Heal x2"));
                Assert.That(FindButton(fixture.TargetListRoot, "2"), Is.Not.Null);
                Click(FindButton(fixture.TargetListRoot, "2"));
                Assert.That(fixture.ConfirmButton.gameObject.activeSelf, Is.True);
                Click(fixture.ConfirmButton);
                yield return null;

                Assert.That(fixture.AllyTwoState.CurrentHp, Is.EqualTo(8));
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(1));
                Assert.That(fixture.Machine.Phase, Is.EqualTo(Phase.Standoff));
                Assert.That(fixture.Session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));

                int staleVersion = fixture.Session.ExchangeState.Version - 1;
                CombatItemUseRequest stale = new CombatItemUseRequest(
                    fixture.HealItem.ItemId,
                    fixture.Ally,
                    fixture.AllyTwo,
                    staleVersion);
                Assert.That(fixture.Orchestrator.TryUseCombatItem(stale, out CombatItemUseResult staleResult), Is.False);
                Assert.That(staleResult.Status, Is.EqualTo(CombatItemUseStatus.StaleRequest));
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(1));

                Assert.That(fixture.Orchestrator.YieldStandoffAttackAuthority(
                    Side.Enemies,
                    fixture.Session.ExchangeState.Version), Is.True);
                yield return null;
                Click(FindButton(fixture.ActorListRoot, "1"));
                Assert.That(FindButton(fixture.SkillListRoot, "ITEM Test Ally Heal x1"), Is.Not.Null);
                Assert.That(FindButton(fixture.SkillListRoot, "ITEM Test Empty x0"), Is.Null);

                int activeVersion = fixture.Session.ExchangeState.Version;
                CombatItemUseRequest insufficient = new CombatItemUseRequest(
                    fixture.EmptyItem.ItemId,
                    fixture.Ally,
                    fixture.AllyTwo,
                    activeVersion);
                Assert.That(fixture.Orchestrator.TryUseCombatItem(insufficient, out CombatItemUseResult insufficientResult), Is.False);
                Assert.That(insufficientResult.Status, Is.EqualTo(CombatItemUseStatus.InsufficientInventory));

                CombatItemUseRequest invalidTarget = new CombatItemUseRequest(
                    fixture.HealItem.ItemId,
                    fixture.Ally,
                    fixture.Enemy,
                    activeVersion);
                Assert.That(fixture.Orchestrator.TryUseCombatItem(invalidTarget, out CombatItemUseResult invalidTargetResult), Is.False);
                Assert.That(invalidTargetResult.Status, Is.EqualTo(CombatItemUseStatus.InvalidTarget));
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(1));

                fixture.Adapter.enabled = false;
                fixture.Orchestrator.RegisterCombatItemUseExecutor(
                    new FailingEffectExecutor(fixture.Inventory, fixture.CombatCatalog));
                CombatItemUseRequest effectFailure = new CombatItemUseRequest(
                    fixture.HealItem.ItemId,
                    fixture.Ally,
                    fixture.AllyTwo,
                    activeVersion);
                Assert.That(fixture.Orchestrator.TryUseCombatItem(effectFailure, out CombatItemUseResult effectFailureResult), Is.False);
                Assert.That(effectFailureResult.Status, Is.EqualTo(CombatItemUseStatus.EffectFailed));
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(1));
                Assert.That(fixture.AllyTwoState.CurrentHp, Is.EqualTo(8));
                fixture.Adapter.enabled = true;

                CombatItemUseRequest oldSessionRequest = new CombatItemUseRequest(
                    fixture.HealItem.ItemId,
                    fixture.Ally,
                    fixture.AllyTwo,
                    activeVersion);
                CombatSession replacementSession = CreateReplacementSession(out _, out _);
                CombatStateMachine replacementMachine = new CombatStateMachine(replacementSession);
                replacementMachine.Tick();
                while (replacementSession.ExchangeState.Version == oldSessionRequest.ExchangeVersion)
                {
                    replacementSession.ExchangeState.SetAttackSide(Side.Enemies);
                    replacementSession.ExchangeState.SetAttackSide(Side.Allies);
                }

                Assert.That(fixture.Orchestrator.BindFinalExchange(
                    replacementSession,
                    replacementMachine,
                    new ZeroRandom(),
                    new AuthoredCombatClashSideInputProvider(replacementSession)), Is.True);
                Assert.That(fixture.Orchestrator.TryUseCombatItem(oldSessionRequest, out CombatItemUseResult oldSessionResult), Is.False);
                Assert.That(oldSessionResult.Status, Is.EqualTo(CombatItemUseStatus.StaleRequest));
                Assert.That(fixture.Inventory.GetCount(fixture.HealItem.ItemId), Is.EqualTo(1));

                fixture.Binder.Unbind();
                Assert.That(fixture.Binder.IsBound, Is.False);
                Assert.That(fixture.ActorListRoot.activeSelf, Is.False);
                Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }
        }

        private static RuntimeFixture CreateFixture()
        {
            CombatItemDefinitionSO healItem = CreateCombatItem(
                "test.item.heal",
                "Test Ally Heal",
                CombatItemTargetRule.SingleAlly,
                CombatItemEffectKind.RestoreHp,
                4);
            CombatItemDefinitionSO emptyItem = CreateCombatItem(
                "test.item.empty",
                "Test Empty",
                CombatItemTargetRule.SingleAlly,
                CombatItemEffectKind.RestoreHp,
                4);
            CombatItemCatalogSO combatCatalog = ScriptableObject.CreateInstance<CombatItemCatalogSO>();
            SetPrivateField(combatCatalog, "items", new List<CombatItemDefinitionSO> { healItem, emptyItem });

            ItemDefinitionSO inventoryHeal = CreateInventoryItem(healItem.ItemId);
            ItemDefinitionSO inventoryEmpty = CreateInventoryItem(emptyItem.ItemId);
            ItemCatalogSO inventoryCatalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            SetPrivateField(inventoryCatalog, "items", new List<ItemDefinitionSO> { inventoryHeal, inventoryEmpty });
            InventoryService inventory = InventoryService.Instance;
            if (inventory == null)
                inventory = new GameObject("CBT_ITEM_01.Inventory").AddComponent<InventoryService>();
            SetPrivateField(inventory, "itemCatalog", inventoryCatalog);
            inventory.ImportItems(null);
            Assert.That(inventory.TryAddItem(healItem.ItemId, 2).Status, Is.EqualTo(InventoryMutationStatus.Success));

            TestCombatant ally = new TestCombatant(1, Side.Allies, 10);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies, 10);
            TestCombatant enemy = new TestCombatant(100, Side.Enemies, 10);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                CombatRuntimeConfig.Compatibility);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            CombatStateMachine machine = new CombatStateMachine(session);
            machine.Tick();
            session.GetCombatState(allyTwo).ApplyDamage(6);

            CombatFlowOrchestrator orchestrator = new GameObject("CBT_ITEM_01.Orchestrator")
                .AddComponent<CombatFlowOrchestrator>();
            GameObject adapterOwner = new GameObject("CBT_ITEM_01.Adapter");
            adapterOwner.SetActive(false);
            CombatItemUseAdapter adapter = adapterOwner.AddComponent<CombatItemUseAdapter>();
            SetPrivateField(adapter, "flowOrchestrator", orchestrator);
            SetPrivateField(adapter, "inventoryService", inventory);
            SetPrivateField(adapter, "combatItemCatalog", combatCatalog);
            adapterOwner.SetActive(true);
            InvokePrivate(adapter, "Bind");

            GameObject binderObject = new GameObject("CBT_ITEM_01.Binder");
            FinalCombatUIBinder binder = binderObject.AddComponent<FinalCombatUIBinder>();
            GameObject actorListRoot = new GameObject("CBT_ITEM_01.Actors");
            GameObject skillListRoot = new GameObject("CBT_ITEM_01.Skills");
            GameObject targetListRoot = new GameObject("CBT_ITEM_01.Targets");
            GameObject handoffListRoot = new GameObject("CBT_ITEM_01.Handoff");
            Button optionButtonPrefab = CreateButton("CBT_ITEM_01.OptionPrefab");
            Button confirmButton = CreateButton("CBT_ITEM_01.Confirm");
            SetPrivateField(binder, "panelRoot", binderObject);
            SetPrivateField(binder, "actorListRoot", actorListRoot);
            SetPrivateField(binder, "skillListRoot", skillListRoot);
            SetPrivateField(binder, "targetListRoot", targetListRoot);
            SetPrivateField(binder, "handoffListRoot", handoffListRoot);
            SetPrivateField(binder, "optionButtonPrefab", optionButtonPrefab);
            SetPrivateField(binder, "confirmButton", confirmButton);
            binder.enabled = false;
            binder.enabled = true;

            return new RuntimeFixture(
                session,
                machine,
                orchestrator,
                adapter,
                inventory,
                combatCatalog,
                binder,
                actorListRoot,
                skillListRoot,
                targetListRoot,
                confirmButton,
                healItem,
                emptyItem,
                ally,
                allyTwo,
                enemy);
        }

        private static CombatSession CreateReplacementSession(out TestCombatant ally, out TestCombatant enemy)
        {
            ally = new TestCombatant(11, Side.Allies, 10);
            enemy = new TestCombatant(200, Side.Enemies, 10);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                CombatRuntimeConfig.Compatibility);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            return session;
        }

        private static void BindWithSynchronousPresentation(RuntimeFixture fixture)
        {
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            fixture.Orchestrator.OutcomePresentationRequested += request => request.TryComplete();
            Assert.That(fixture.Orchestrator.BindFinalExchange(
                fixture.Session,
                fixture.Machine,
                new ZeroRandom(),
                new AuthoredCombatClashSideInputProvider(fixture.Session)), Is.True);
        }

        private static CombatItemDefinitionSO CreateCombatItem(
            string itemId,
            string displayName,
            CombatItemTargetRule targetRule,
            CombatItemEffectKind effectKind,
            int recovery)
        {
            CombatItemDefinitionSO item = ScriptableObject.CreateInstance<CombatItemDefinitionSO>();
            SetPrivateField(item, "itemId", itemId);
            SetPrivateField(item, "displayNameOverride", displayName);
            SetPrivateField(item, "targetRule", targetRule);
            SetPrivateField(item, "effectKind", effectKind);
            SetPrivateField(item, "hpRecovery", recovery);
            return item;
        }

        private static ItemDefinitionSO CreateInventoryItem(string itemId)
        {
            ItemDefinitionSO item = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            SetPrivateField(item, "itemId", itemId);
            SetPrivateField(item, "maximumStackCount", 10);
            return item;
        }

        private static Button CreateButton(string name)
        {
            GameObject root = new GameObject(name);
            Button button = root.AddComponent<Button>();
            Text text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(root.transform);
            return button;
        }

        private static Button FindButton(GameObject root, string expectedText)
        {
            if (root == null)
                return null;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && string.Equals(text.text, expectedText, StringComparison.Ordinal))
                    return buttons[i];
            }

            return null;
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            button.onClick.Invoke();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private void CaptureRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                CombatSession session,
                CombatStateMachine machine,
                CombatFlowOrchestrator orchestrator,
                CombatItemUseAdapter adapter,
                InventoryService inventory,
                CombatItemCatalogSO combatCatalog,
                FinalCombatUIBinder binder,
                GameObject actorListRoot,
                GameObject skillListRoot,
                GameObject targetListRoot,
                Button confirmButton,
                CombatItemDefinitionSO healItem,
                CombatItemDefinitionSO emptyItem,
                TestCombatant ally,
                TestCombatant allyTwo,
                TestCombatant enemy)
            {
                Session = session;
                Machine = machine;
                Orchestrator = orchestrator;
                Adapter = adapter;
                Inventory = inventory;
                CombatCatalog = combatCatalog;
                Binder = binder;
                ActorListRoot = actorListRoot;
                SkillListRoot = skillListRoot;
                TargetListRoot = targetListRoot;
                ConfirmButton = confirmButton;
                HealItem = healItem;
                EmptyItem = emptyItem;
                Ally = ally;
                AllyTwo = allyTwo;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public CombatStateMachine Machine { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public CombatItemUseAdapter Adapter { get; }
            public InventoryService Inventory { get; }
            public CombatItemCatalogSO CombatCatalog { get; }
            public FinalCombatUIBinder Binder { get; }
            public GameObject ActorListRoot { get; }
            public GameObject SkillListRoot { get; }
            public GameObject TargetListRoot { get; }
            public Button ConfirmButton { get; }
            public CombatItemDefinitionSO HealItem { get; }
            public CombatItemDefinitionSO EmptyItem { get; }
            public TestCombatant Ally { get; }
            public TestCombatant AllyTwo { get; }
            public TestCombatant Enemy { get; }
            public CombatantCombatState AllyTwoState => Session.GetCombatState(AllyTwo);
            public CombatItemUseExecutor AdapterExecutor =>
                GetPrivateField<CombatItemUseExecutor>(Adapter, "_executor");
            public ICombatItemUseExecutor RegisteredExecutor =>
                GetPrivateField<ICombatItemUseExecutor>(Orchestrator, "_combatItemUseExecutor");
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

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => 0;
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private readonly int _maxHp;
            private CombatantCombatState _state;
            private int _hp;

            public TestCombatant(int id, Side side, int maxHp)
            {
                Id = new CombatantId(id);
                Side = side;
                _maxHp = maxHp;
                _hp = maxHp;
            }

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? _hp;
            public int MaxHP => _maxHp;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) => _state?.ApplyDamage(amount);
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
