#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.UI
{
    public sealed class CombatUIRoutingTests
    {
        private readonly List<GameObject> _objects = new();
        private GameStateMachine _stateMachine;
        private GameUIRootController _roots;
        private UIScreenRouter _router;
        private Dictionary<string, GameObject> _rootObjects;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingSingletons();

            _stateMachine = CreateComponent<GameStateMachine>("StateMachine");
            Invoke(_stateMachine, "Awake");

            _roots = CreateComponent<GameUIRootController>("GlobalRoots");
            _rootObjects = new Dictionary<string, GameObject>
            {
                ["titleRoot"] = CreateObject("TitleRoot"),
                ["fieldRoot"] = CreateObject("FieldRoot"),
                ["dialogueRoot"] = CreateObject("DialogueRoot"),
                ["choiceRoot"] = CreateObject("ChoiceRoot"),
                ["combatRoot"] = CreateObject("CombatRoot"),
                ["rewardRoot"] = CreateObject("RewardRoot"),
                ["pauseRoot"] = CreateObject("PauseRoot"),
                ["loadingRoot"] = CreateObject("LoadingRoot")
            };

            foreach (KeyValuePair<string, GameObject> pair in _rootObjects)
                SetField(_roots, pair.Key, pair.Value);

            _router = CreateComponent<UIScreenRouter>("Router");
            SetField(_router, "uiRoot", _roots);
            SetField(_router, "stateMachine", _stateMachine);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
            DestroyExistingSingletons();
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(GameState.Boot, "loadingRoot")]
        [TestCase(GameState.Title, "titleRoot")]
        [TestCase(GameState.Loading, "loadingRoot")]
        [TestCase(GameState.Exploration, "fieldRoot")]
        [TestCase(GameState.CombatTransition, "combatRoot")]
        [TestCase(GameState.CombatPlanning, "combatRoot")]
        [TestCase(GameState.CombatResolving, "combatRoot")]
        [TestCase(GameState.Reward, "rewardRoot")]
        public void GlobalRoute_ShowsExpectedPrimaryRoot(GameState state, string expectedRoot)
        {
            _router.ApplyState(state);

            foreach (KeyValuePair<string, GameObject> pair in _rootObjects)
            {
                bool expected = pair.Key == expectedRoot;
                Assert.That(pair.Value.activeSelf, Is.EqualTo(expected), pair.Key);
            }
        }

        [Test]
        public void Dialogue_PreservesFieldUnderOverlay()
        {
            _router.ApplyState(GameState.Dialogue);

            Assert.That(_roots.FieldVisible, Is.True);
            Assert.That(_roots.DialogueVisible, Is.True);
            Assert.That(_roots.ChoiceVisible, Is.False);
        }

        [Test]
        public void Choice_PreservesFieldAndDialogueUnderOverlay()
        {
            _router.ApplyState(GameState.Choice);

            Assert.That(_roots.FieldVisible, Is.True);
            Assert.That(_roots.DialogueVisible, Is.True);
            Assert.That(_roots.ChoiceVisible, Is.True);
        }

        [Test]
        public void UIOnly_HidesKnownContentRoots()
        {
            _router.ApplyState(GameState.UIOnly);

            AssertAllContentHidden();
        }

        [Test]
        public void ReapplyingSameState_IsIdempotent()
        {
            _router.ApplyState(GameState.CombatPlanning);
            _router.ApplyState(GameState.CombatPlanning);

            Assert.That(_roots.CombatVisible, Is.True);
            Assert.That(_router.CurrentRoutedState, Is.EqualTo(GameState.CombatPlanning));
        }

        [Test]
        public void MissingOptionalRoot_DoesNotThrow()
        {
            SetField(_roots, "pauseRoot", null);
            Assert.DoesNotThrow(() => _router.ApplyState(GameState.Exploration));
        }

        [Test]
        public void MissingRequestedRoot_WarnsOnce()
        {
            SetField(_roots, "loadingRoot", null);
            LogAssert.Expect(LogType.Warning, "[GameUIRootController] loadingRoot is missing. Assign the global UI root in the Inspector.");

            _router.ApplyState(GameState.Boot);
            _router.ApplyState(GameState.Boot);
        }

        [TestCase(GameState.Exploration, "fieldRoot")]
        [TestCase(GameState.CombatPlanning, "combatRoot")]
        [TestCase(GameState.CombatResolving, "combatRoot")]
        [TestCase(GameState.Dialogue, "dialogueRoot")]
        [TestCase(GameState.Choice, "choiceRoot")]
        [TestCase(GameState.Reward, "rewardRoot")]
        public void Pause_PreservesExactUnderlyingRoute(GameState previous, string expectedRoot)
        {
            SetAutoProperty(_stateMachine, "Previous", previous);
            _router.ApplyState(GameState.Paused);

            Assert.That(_rootObjects[expectedRoot].activeSelf, Is.True);
            Assert.That(_roots.PauseVisible, Is.True);
        }

        [Test]
        public void PauseRoot_IsVisibleOnlyWhilePaused()
        {
            SetAutoProperty(_stateMachine, "Previous", GameState.Exploration);
            _router.ApplyState(GameState.Paused);
            Assert.That(_roots.PauseVisible, Is.True);

            _router.ApplyState(GameState.Exploration);
            Assert.That(_roots.PauseVisible, Is.False);
        }

        [Test]
        public void Resume_RestoresExactRouteWithoutIntermediateReplacement()
        {
            SetAutoProperty(_stateMachine, "Previous", GameState.CombatPlanning);
            _router.ApplyState(GameState.Paused);
            _router.ApplyState(GameState.CombatPlanning);

            Assert.That(_roots.CombatVisible, Is.True);
            Assert.That(_roots.PauseVisible, Is.False);
            Assert.That(_roots.FieldVisible, Is.False);
        }

        [Test]
        public void RepeatedPausedApplication_DoesNotCorruptBaseRoute()
        {
            SetAutoProperty(_stateMachine, "Previous", GameState.Reward);
            _router.ApplyState(GameState.Paused);
            _router.ApplyState(GameState.Paused);

            Assert.That(_roots.RewardVisible, Is.True);
            Assert.That(_roots.PauseVisible, Is.True);
            Assert.That(_router.CurrentContentState, Is.EqualTo(GameState.Reward));
        }

        [TestCase(Phase.Planning, true, true)]
        [TestCase(Phase.Resolution, false, true)]
        [TestCase(Phase.EndTurn, false, true)]
        [TestCase(Phase.EnterCombat, false, false)]
        [TestCase(Phase.ExitCombat, false, false)]
        public void CombatInternalRoute_AppliesPhase(Phase phase, bool planning, bool widgets)
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            fixture.Controller.ApplyPhase(phase);

            Assert.That(fixture.Planning.activeSelf, Is.EqualTo(planning));
            Assert.That(fixture.Widgets.activeSelf, Is.EqualTo(widgets));
        }

        [Test]
        public void CombatInternalRoute_RepeatedPhaseIsIdempotent()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            fixture.Controller.ApplyPhase(Phase.Planning);
            fixture.Controller.ApplyPhase(Phase.Planning);

            Assert.That(fixture.Planning.activeSelf, Is.True);
            Assert.That(fixture.Widgets.activeSelf, Is.True);
        }

        [Test]
        public void CanonicalCombatController_DoesNotToggleFieldOrRewardRoots()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(true);
            fixture.Field.SetActive(true);
            fixture.Reward.SetActive(true);

            Invoke(fixture.Controller, "ResolveRoutingMode");
            fixture.Controller.ApplyPhase(Phase.Planning);

            Assert.That(fixture.Controller.UsesCanonicalGlobalRouting, Is.True);
            Assert.That(fixture.Field.activeSelf, Is.True);
            Assert.That(fixture.Reward.activeSelf, Is.True);
        }

        [Test]
        public void DemoFallbackCombatController_PreservesLegacyCanvasBehavior()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            fixture.Field.SetActive(true);
            fixture.Reward.SetActive(true);

            Invoke(fixture.Controller, "ResolveRoutingMode");
            fixture.Controller.ApplyPhase(Phase.Planning);

            Assert.That(fixture.Controller.UsesCanonicalGlobalRouting, Is.False);
            Assert.That(fixture.Field.activeSelf, Is.False);
            Assert.That(fixture.Reward.activeSelf, Is.False);
        }

        [Test]
        public void OnEnableDuringPlanning_RecoversSessionAndPhase()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            CombatSession session = CreateSession();
            CombatStateMachine machine = new CombatStateMachine(session);
            SetAutoProperty(machine, "Phase", Phase.Planning);
            CombatEntryPoint entry = CreateComponent<CombatEntryPoint>("Entry");
            SetAutoProperty(entry, "ActiveSession", session);
            SetAutoProperty(entry, "ActiveStateMachine", machine);
            SetField(fixture.Controller, "entryPoint", entry);

            Invoke(fixture.Controller, "OnEnable");

            Assert.That(fixture.Controller.ActiveSession, Is.SameAs(session));
            Assert.That(fixture.Planning.activeSelf, Is.True);
            Invoke(fixture.Controller, "OnDisable");
        }

        [Test]
        public void OnEnableDuringResolution_RecoversHiddenPlanningAndVisibleWidgets()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            CombatSession session = CreateSession();
            CombatStateMachine machine = new CombatStateMachine(session);
            SetAutoProperty(machine, "Phase", Phase.Resolution);
            CombatEntryPoint entry = CreateComponent<CombatEntryPoint>("Entry");
            SetAutoProperty(entry, "ActiveSession", session);
            SetAutoProperty(entry, "ActiveStateMachine", machine);
            SetField(fixture.Controller, "entryPoint", entry);

            Invoke(fixture.Controller, "OnEnable");

            Assert.That(fixture.Planning.activeSelf, Is.False);
            Assert.That(fixture.Widgets.activeSelf, Is.True);
            Invoke(fixture.Controller, "OnDisable");
        }

        [Test]
        public void CombatEnd_ClearsStaleSessionReferences()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(false);
            CombatSession session = CreateSession();
            SetField(fixture.Controller, "_activeSession", session);
            SetField(fixture.Controller, "_activeStateMachine", new CombatStateMachine(session));

            Invoke(fixture.Controller, "HandleCombatEnded", new object[] { null });

            Assert.That(fixture.Controller.ActiveSession, Is.Null);
            Assert.That(fixture.Controller.ActiveStateMachine, Is.Null);
            Assert.That(fixture.Planning.activeSelf, Is.False);
        }

        [Test]
        public void PlanningHUD_CompatibilityShowAndHideRemainFunctional()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.Show();
            Assert.That(fixture.Panel.activeSelf, Is.True);

            fixture.Hud.Hide();
            Assert.That(fixture.Panel.activeSelf, Is.False);
        }

        [Test]
        public void PlanningHUD_RepeatedPlanningForSameTurnDoesNotRebuild()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.EnterPlanning(fixture.Session.CurrentTurn);
            int targetCount = fixture.TargetRoot.childCount;

            fixture.Hud.EnterPlanning(fixture.Session.CurrentTurn);
            Assert.That(fixture.TargetRoot.childCount, Is.EqualTo(targetCount));
        }

        [Test]
        public void PlanningHUD_RepresentsEveryLivingEnemy()
        {
            PlanningFixture fixture = CreatePlanningFixture(enemyCount: 3);
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.EnterPlanning(fixture.Session.CurrentTurn);

            Assert.That(fixture.TargetRoot.childCount, Is.EqualTo(3));
        }

        [Test]
        public void PlanningHUD_ExcludesDeadTargets()
        {
            PlanningFixture fixture = CreatePlanningFixture(enemyCount: 3, deadEnemyIndex: 1);
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.EnterPlanning(fixture.Session.CurrentTurn);

            Assert.That(fixture.TargetRoot.childCount, Is.EqualTo(2));
        }

        [Test]
        public void PlanningHUD_NewTurnClearsPreviousSelection()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            SetField(fixture.Hud, "_selectedSkill", fixture.Skill);
            SetField(fixture.Hud, "_selectedTarget", fixture.Session.Enemies[0]);
            fixture.Session.CurrentTurn.TrySubmit();
            fixture.Session.CurrentTurn.TryMarkResolutionFailed();
            SetAutoProperty(fixture.Session.CurrentTurn, "Lifecycle", CombatTurnLifecycle.Completed);
            fixture.Session.BeginNewTurn();

            fixture.Hud.EnterPlanning(fixture.Session.CurrentTurn);

            Assert.That(GetField<object>(fixture.Hud, "_selectedSkill"), Is.Null);
            Assert.That(GetField<object>(fixture.Hud, "_selectedTarget"), Is.Null);
        }

        [Test]
        public void PlanningHUD_ConfirmBindingIsNotDuplicated()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            Invoke(fixture.Hud, "BindConfirmButton");
            Invoke(fixture.Hud, "BindConfirmButton");

            Assert.That(GetField<bool>(fixture.Hud, "_confirmBound"), Is.True);
            Invoke(fixture.Hud, "UnbindConfirmButton");
            Assert.That(GetField<bool>(fixture.Hud, "_confirmBound"), Is.False);
        }

        [Test]
        public void PlanningHUD_ExitPlanningDisablesInteraction()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.Show();

            fixture.Hud.ExitPlanning();

            Assert.That(fixture.Panel.activeSelf, Is.False);
            Assert.That(GetField<Button>(fixture.Hud, "confirmButton").interactable, Is.False);
        }

        [Test]
        public void PlanningHUD_RejectedSubmissionKeepsPlanningAvailable()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.Show();
            SetField(fixture.Hud, "_selectedSkill", fixture.Skill);
            SetField(fixture.Hud, "_selectedTarget", fixture.Session.Enemies[0]);

            Invoke(fixture.Hud, "Confirm");

            Assert.That(fixture.Panel.activeSelf, Is.True);
            Assert.That(GetField<bool>(fixture.Hud, "_submittedThisPlanning"), Is.False);
        }

        [Test]
        public void PlanningHUD_CombatEndPreventsStaleRebuild()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.Show();
            Invoke(fixture.Hud, "HandleCombatEnded", new object[] { null });

            fixture.Hud.Show();

            Assert.That(fixture.Panel.activeSelf, Is.False);
        }

        [Test]
        public void PlanningHUD_DoesNotModifyGlobalFieldOrRewardRoots()
        {
            PlanningFixture fixture = CreatePlanningFixture();
            _rootObjects["fieldRoot"].SetActive(true);
            _rootObjects["rewardRoot"].SetActive(true);

            fixture.Hud.Bind(fixture.Session);
            fixture.Hud.Show();
            fixture.Hud.Hide();

            Assert.That(_rootObjects["fieldRoot"].activeSelf, Is.True);
            Assert.That(_rootObjects["rewardRoot"].activeSelf, Is.True);
        }

        [Test]
        public void GlobalStateBeforeCombatPhase_ConvergesToCombatPlanning()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(true);
            _router.ApplyState(GameState.CombatPlanning);
            fixture.Controller.ApplyPhase(Phase.Planning);

            Assert.That(_roots.CombatVisible, Is.True);
            Assert.That(fixture.Planning.activeSelf, Is.True);
        }

        [Test]
        public void CombatPhaseBeforeGlobalState_ConvergesToCombatPlanning()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(true);
            fixture.Controller.ApplyPhase(Phase.Planning);
            _router.ApplyState(GameState.CombatPlanning);

            Assert.That(_roots.CombatVisible, Is.True);
            Assert.That(fixture.Planning.activeSelf, Is.True);
        }

        [Test]
        public void RewardStateBeforePanelShow_ConvergesWithoutCombatRoot()
        {
            RewardUIPanel panel = CreateRewardPanel(out GameObject panelRoot);
            _router.ApplyState(GameState.Reward);
            panel.Show(null);

            Assert.That(_roots.RewardVisible, Is.True);
            Assert.That(_roots.CombatVisible, Is.False);
            Assert.That(panelRoot.activeSelf, Is.True);
        }

        [Test]
        public void PanelShowBeforeRewardState_ConvergesWithoutCombatRoot()
        {
            RewardUIPanel panel = CreateRewardPanel(out GameObject panelRoot);
            panel.Show(null);
            _router.ApplyState(GameState.Reward);

            Assert.That(_roots.RewardVisible, Is.True);
            Assert.That(_roots.CombatVisible, Is.False);
            Assert.That(panelRoot.activeSelf, Is.True);
        }

        [Test]
        public void RewardPanel_DoesNotReactivateCombatRoot()
        {
            RewardUIPanel panel = CreateRewardPanel(out _);
            _rootObjects["combatRoot"].SetActive(false);
            panel.Show(null);

            Assert.That(_rootObjects["combatRoot"].activeSelf, Is.False);
        }

        [Test]
        public void RewardPanel_HideIsIdempotent()
        {
            RewardUIPanel panel = CreateRewardPanel(out GameObject panelRoot);
            panel.Hide();
            panel.Hide();

            Assert.That(panelRoot.activeSelf, Is.False);
        }

        [Test]
        public void CombatDemoFlow_DoesNotCompeteWhenCanonicalRoutingExists()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(true);
            CombatDemoFlowController demo = CreateComponent<CombatDemoFlowController>("DemoFlow");
            GameObject demoCombatRoot = CreateObject("DemoCombatRoot");
            demoCombatRoot.SetActive(false);
            SetField(demo, "combatCanvasRoot", demoCombatRoot);
            Invoke(demo, "Awake");

            Invoke(demo, "OnCombatStarted", CreateSession());

            Assert.That(demoCombatRoot.activeSelf, Is.False);
            Assert.That(fixture.Controller, Is.Not.Null);
        }

        [Test]
        public void CombatDemoFlow_PreservesFallbackWithoutCanonicalRouting()
        {
            Object.DestroyImmediate(_router.gameObject);
            Object.DestroyImmediate(_roots.gameObject);
            CombatDemoFlowController demo = CreateComponent<CombatDemoFlowController>("DemoFallback");
            GameObject demoCombatRoot = CreateObject("DemoCombatRoot");
            demoCombatRoot.SetActive(false);
            SetField(demo, "combatCanvasRoot", demoCombatRoot);
            Invoke(demo, "Awake");

            Invoke(demo, "OnCombatStarted", CreateSession());

            Assert.That(demoCombatRoot.activeSelf, Is.True);
        }

        [Test]
        public void DuplicateRouteAndPhaseNotificationsKeepFinalVisibility()
        {
            CombatUIFixture fixture = CreateCombatUIFixture(true);
            _router.ApplyState(GameState.CombatResolving);
            fixture.Controller.ApplyPhase(Phase.Resolution);
            _router.ApplyState(GameState.CombatResolving);
            fixture.Controller.ApplyPhase(Phase.Resolution);

            Assert.That(_roots.CombatVisible, Is.True);
            Assert.That(fixture.Planning.activeSelf, Is.False);
            Assert.That(fixture.Widgets.activeSelf, Is.True);
        }

        [Test]
        public void RouterObjectCannotBeDisabledByMisconfiguredRoot()
        {
            GameObject owner = _roots.gameObject;
            SetField(_roots, "fieldRoot", owner);
            LogAssert.Expect(LogType.Warning, "[GameUIRootController] fieldRoot contains the routing owner and cannot be toggled safely. Assign a child content root instead.");

            _roots.SetFieldVisible(false);

            Assert.That(owner.activeSelf, Is.True);
        }

        private CombatUIFixture CreateCombatUIFixture(bool canonical)
        {
            if (!canonical)
            {
                Object.DestroyImmediate(_router.gameObject);
                Object.DestroyImmediate(_roots.gameObject);
            }

            CombatUIRootController controller = CreateComponent<CombatUIRootController>("CombatInternalController");
            GameObject hud = CreateObject("InternalHUD");
            GameObject planning = CreateObject("PlanningPanel");
            GameObject widgets = CreateObject("WidgetContainer");
            GameObject field = CreateObject("LegacyField");
            GameObject reward = CreateObject("LegacyReward");
            SetField(controller, "combatHudRoot", hud);
            SetField(controller, "planningPanel", planning);
            SetField(controller, "widgetContainer", widgets);
            SetField(controller, "overworldCanvas", field);
            SetField(controller, "rewardCanvas", reward);
            SetField(controller, "autoBindOnAwake", false);

            return new CombatUIFixture(controller, planning, widgets, field, reward);
        }

        private PlanningFixture CreatePlanningFixture(int enemyCount = 1, int deadEnemyIndex = -1)
        {
            CombatPlanningHUD hud = CreateComponent<CombatPlanningHUD>("PlanningHUD");
            GameObject panel = CreateObject("PlanningPanel");
            RectTransform skills = CreateObject("SkillRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            RectTransform targets = CreateObject("TargetRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            Button prefab = CreateObject("ButtonPrefab", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            Button confirm = CreateObject("Confirm", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            SetField(hud, "panelPlanning", panel);
            SetField(hud, "skillListRoot", skills);
            SetField(hud, "targetListRoot", targets);
            SetField(hud, "buttonPrefab", prefab);
            SetField(hud, "confirmButton", confirm);

            FakeSkill skill = new();
            CombatSession session = CreateSession();
            session.Allies.Add(new FakeCombatant(1, Side.Allies, 10, new[] { skill }));
            for (int i = 0; i < enemyCount; i++)
            {
                int hp = i == deadEnemyIndex ? 0 : 10;
                session.Enemies.Add(new FakeCombatant(10 + i, Side.Enemies, hp, new[] { skill }));
            }

            return new PlanningFixture(hud, panel, targets, session, skill);
        }

        private RewardUIPanel CreateRewardPanel(out GameObject panelRoot)
        {
            RewardUIPanel panel = CreateComponent<RewardUIPanel>("RewardPanel");
            panelRoot = CreateObject("RewardContent");
            Transform rows = CreateObject("RewardRows", typeof(RectTransform)).transform;
            SetField(panel, "root", panelRoot);
            SetField(panel, "rewardRowRoot", rows);
            return panel;
        }

        private CombatSession CreateSession()
        {
            CombatSession session = new(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 0),
                new CombatEnvironment());
            session.BeginNewTurn();
            return session;
        }

        private void AssertAllContentHidden()
        {
            Assert.That(_roots.TitleVisible, Is.False);
            Assert.That(_roots.FieldVisible, Is.False);
            Assert.That(_roots.DialogueVisible, Is.False);
            Assert.That(_roots.ChoiceVisible, Is.False);
            Assert.That(_roots.CombatVisible, Is.False);
            Assert.That(_roots.RewardVisible, Is.False);
            Assert.That(_roots.LoadingVisible, Is.False);
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = CreateObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateObject(string name, params System.Type[] components)
        {
            GameObject gameObject = components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static void DestroyExistingSingletons()
        {
            GameStateMachine[] stateMachines = Object.FindObjectsByType<GameStateMachine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < stateMachines.Length; i++)
                Object.DestroyImmediate(stateMachines[i].gameObject);
        }

        private readonly struct CombatUIFixture
        {
            public readonly CombatUIRootController Controller;
            public readonly GameObject Planning;
            public readonly GameObject Widgets;
            public readonly GameObject Field;
            public readonly GameObject Reward;

            public CombatUIFixture(
                CombatUIRootController controller,
                GameObject planning,
                GameObject widgets,
                GameObject field,
                GameObject reward)
            {
                Controller = controller;
                Planning = planning;
                Widgets = widgets;
                Field = field;
                Reward = reward;
            }
        }

        private readonly struct PlanningFixture
        {
            public readonly CombatPlanningHUD Hud;
            public readonly GameObject Panel;
            public readonly RectTransform TargetRoot;
            public readonly CombatSession Session;
            public readonly FakeSkill Skill;

            public PlanningFixture(
                CombatPlanningHUD hud,
                GameObject panel,
                RectTransform targetRoot,
                CombatSession session,
                FakeSkill skill)
            {
                Hud = hud;
                Panel = panel;
                TargetRoot = targetRoot;
                Session = session;
                Skill = skill;
            }
        }

        private sealed class FakeCombatant : ICombatant
        {
            private readonly List<ISkill> _skills;

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP { get; private set; }
            public int MaxHP { get; } = 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger { get; private set; }
            public int StaggerMax => 10;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public FakeCombatant(int id, Side side, int hp, IEnumerable<ISkill> skills)
            {
                Id = new CombatantId(id);
                Side = side;
                HP = hp;
                _skills = new List<ISkill>(skills);
            }

            public void ApplyDamage(int amount) => HP = Mathf.Max(0, HP - amount);
            public void AddStagger(int amount) => Stagger += amount;
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() => Stagger = 0;
        }

        private sealed class FakeSkill : ISkill
        {
            public SkillId Id => new(1);
            public string Name => "Test Skill";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
        }
    }
}
#endif
