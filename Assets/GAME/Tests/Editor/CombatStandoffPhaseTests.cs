#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatStandoffPhaseTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

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
        public void Defaults_RemainLegacyPlanning()
        {
            CombatStartRequest request = CreateRequest(CombatFlowMode.LegacyPlanning, useLegacyConstructor: true);
            CombatSession session = CreateSession(CombatFlowMode.LegacyPlanning);
            CombatStateMachine stateMachine = new CombatStateMachine(session);

            Assert.That(request.FlowMode, Is.EqualTo(CombatFlowMode.LegacyPlanning));
            Assert.That(session.FlowMode, Is.EqualTo(CombatFlowMode.LegacyPlanning));

            stateMachine.Tick();

            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(session.TurnIndex, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitRequest_BootstrapsDirectlyIntoStandoffWithoutStartingTurn()
        {
            CombatStartRequest request = CreateRequest(CombatFlowMode.StandoffClashChain);

            (CombatSession session, CombatStateMachine stateMachine) = CombatBootstrapper.StartCombat(
                request,
                new SkillBook(),
                new TestCombatantFactory());

            Assert.That(session.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain));
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(session.CombatStateCount, Is.EqualTo(2));
            Assert.That(session.ExchangeState, Is.Not.Null);
            Assert.That(session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(session.TurnIndex, Is.Zero);
            Assert.That(session.Inspiration.Current, Is.EqualTo(3));
        }

        [Test]
        public void StandoffEntry_HasRuntimeStateBeforePhaseEventAndDoesNotRequireTurnStart()
        {
            CombatSession session = CreateSession(CombatFlowMode.StandoffClashChain);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            bool runtimeReadyDuringEvent = false;
            int phaseEvents = 0;
            stateMachine.OnPhaseChanged += (_, next) =>
            {
                if (next != Phase.Standoff)
                    return;

                phaseEvents++;
                runtimeReadyDuringEvent = session.CombatStateCount == 2 && session.ExchangeState != null;
            };

            stateMachine.Tick();

            Assert.That(runtimeReadyDuringEvent, Is.True);
            Assert.That(phaseEvents, Is.EqualTo(1));
            Assert.That(session.TurnIndex, Is.Zero);
            Assert.That(stateMachine.EnterStandoff(), Is.True);
            Assert.That(phaseEvents, Is.EqualTo(1));
        }

        [Test]
        public void EnterStandoff_RejectsLegacyWrongTransitionAndExit()
        {
            CombatSession legacySession = CreateSession(CombatFlowMode.LegacyPlanning);
            CombatStateMachine legacyStateMachine = new CombatStateMachine(legacySession);
            Assert.That(legacyStateMachine.EnterStandoff(), Is.False);

            CombatSession session = CreateSession(CombatFlowMode.StandoffClashChain);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            stateMachine.ForceExit(CombatEndReason.Abort);

            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(stateMachine.EnterStandoff(), Is.False);
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
        }

        [Test]
        public void CompletedSession_CannotEnterStandoff()
        {
            CombatSession session = CreateSession(CombatFlowMode.StandoffClashChain);
            session.Enemies[0].ApplyDamage(int.MaxValue);
            CombatStateMachine stateMachine = new CombatStateMachine(session);

            Assert.That(stateMachine.EnterStandoff(), Is.False);

            stateMachine.Tick();
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(stateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
        }

        [Test]
        public void Standoff_MapsToExistingCombatPlanningGlobalState()
        {
            DestroyExistingGlobalOwners();
            GameStateMachine globalState = CreateComponent<GameStateMachine>("GlobalState");
            Invoke(globalState, "Awake");
            GameFlowController flow = CreateComponent<GameFlowController>("Flow");
            Invoke(flow, "Awake");
            CombatEntryPoint entryPoint = CreateComponent<CombatEntryPoint>("Entry");
            Invoke(entryPoint, "Awake");

            bool synchronized = (bool)Invoke(entryPoint, "TrySynchronizeGlobalCombatState", Phase.Standoff);

            Assert.That(synchronized, Is.True);
            Assert.That(globalState.Current, Is.EqualTo(GameState.CombatPlanning));
        }

        [Test]
        public void Standoff_ShowsCombatContentWithoutOpeningLegacyPlanningPanel()
        {
            GameObject owner = CreateGameObject("UIRootController");
            CombatUIRootController controller = owner.AddComponent<CombatUIRootController>();
            GameObject combatRoot = CreateGameObject("CombatRoot");
            GameObject widgetRoot = CreateGameObject("WidgetRoot");
            GameObject planningPanel = CreateGameObject("PlanningPanel");
            combatRoot.SetActive(false);
            widgetRoot.SetActive(false);
            planningPanel.SetActive(true);
            SetField(controller, "combatHudRoot", combatRoot);
            SetField(controller, "widgetContainer", widgetRoot);
            SetField(controller, "planningPanel", planningPanel);

            Invoke(controller, "ApplyPhase", Phase.Standoff);

            Assert.That(combatRoot.activeSelf, Is.True);
            Assert.That(widgetRoot.activeSelf, Is.True);
            Assert.That(planningPanel.activeSelf, Is.False);
        }

        private CombatSession CreateSession(CombatFlowMode flowMode)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerGotHit,
                Side.Enemies,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment(),
                flowMode);
            session.Allies.Add(CreateCombatant(1, Side.Allies));
            session.Enemies.Add(CreateCombatant(100, Side.Enemies));
            return session;
        }

        private static CombatStartRequest CreateRequest(
            CombatFlowMode flowMode,
            bool useLegacyConstructor = false)
        {
            return useLegacyConstructor
                ? new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null)
                : new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null, flowMode);
        }

        private static DummyCombatant CreateCombatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 10, KeywordMask.None, 6);
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            return CreateGameObject(name).AddComponent<T>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {target.GetType().Name}.{methodName}");
            return method.Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void DestroyExistingGlobalOwners()
        {
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i].gameObject);
            }
        }

        private sealed class TestCombatantFactory : ICombatantFactory
        {
            public void PopulateCombatants(CombatSession session, CombatStartRequest request)
            {
                session.Allies.Add(CreateCombatant(1, Side.Allies));
                session.Enemies.Add(CreateCombatant(100, Side.Enemies));
            }
        }
    }
}
#endif
