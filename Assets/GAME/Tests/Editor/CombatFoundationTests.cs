#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatFoundationTests
    {
        private readonly List<GameObject> _createdObjects = new();

        private GameStateMachine _stateMachine;
        private GameFlowController _flow;
        private CombatEntryPoint _entryPoint;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingRuntimeOwners();

            _stateMachine = CreateComponent<GameStateMachine>("GameStateMachineTest");
            InvokePrivate(_stateMachine, "Awake");

            _flow = CreateComponent<GameFlowController>("GameFlowControllerTest");
            InvokePrivate(_flow, "Awake");

            _entryPoint = CreateComponent<CombatEntryPoint>("CombatEntryPointTest");
            InvokePrivate(_entryPoint, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            DestroyExistingRuntimeOwners();
        }

        [Test]
        public void StartCombatFromField_PreservesAllDistinctFieldCombatants()
        {
            GameObject allyOne = CreateCombatant("AllyOne");
            GameObject allyTwo = CreateCombatant("AllyTwo");
            GameObject enemyOne = CreateCombatant("EnemyOne");
            GameObject enemyTwo = CreateCombatant("EnemyTwo");

            bool started = _entryPoint.StartCombatFromField(
                new List<GameObject> { allyOne, null, allyTwo, allyOne },
                new List<GameObject> { enemyOne, enemyTwo, null, enemyOne },
                StartReason.PlayerFirstHit,
                Side.Allies,
                null);

            Assert.That(started, Is.True);
            Assert.That(_entryPoint.ActiveSession, Is.Not.Null);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(2));
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(2));
        }

        [Test]
        public void ProductionFlow_RepeatsTurnsAndRestoresExplorationOnce()
        {
            GameObject ally = CreateCombatant("Ally");
            GameObject enemy = CreateCombatant("Enemy");
            CombatHpComponent enemyHp = enemy.GetComponent<CombatHpComponent>();

            List<GameState> enteredStates = new();
            _stateMachine.OnStateChanged += (_, next) => enteredStates.Add(next);

            int combatEndedCount = 0;
            CombatResult combatResult = null;
            _entryPoint.OnCombatEnded += result =>
            {
                combatEndedCount++;
                combatResult = result;
                _flow.HandleCombatResult(result);
            };

            CombatStartRequest request = new CombatStartRequest(
                StartReason.PlayerFirstHit,
                Side.Allies,
                10,
                3,
                null);
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.CombatPlanning));

            Assert.That(_entryPoint.SubmitCurrentTurn(), Is.True);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.CombatResolving));
            _entryPoint.ActiveStateMachine.Tick();
            _entryPoint.ActiveStateMachine.Tick();
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.CombatPlanning));

            enemyHp.HP = 0;
            Assert.That(_entryPoint.SubmitCurrentTurn(), Is.True);
            _entryPoint.ActiveStateMachine.Tick();
            _entryPoint.ActiveStateMachine.Tick();
            InvokePrivate(_entryPoint, "Update");

            Assert.That(combatEndedCount, Is.EqualTo(1));
            Assert.That(combatResult, Is.Not.Null);
            Assert.That(combatResult.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Reward));
            Assert.That(enemy.activeSelf, Is.False);
            CollectionAssert.AreEqual(
                new[]
                {
                    GameState.CombatPlanning,
                    GameState.CombatResolving,
                    GameState.CombatPlanning,
                    GameState.CombatResolving,
                    GameState.Reward
                },
                enteredStates);

            _flow.HandleRewardClosed();
            _flow.HandleRewardClosed();

            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(enteredStates.FindAll(state => state == GameState.Exploration).Count, Is.EqualTo(1));
        }

        private GameObject CreateCombatant(string name)
        {
            GameObject gameObject = CreateGameObject(name);
            CombatHpComponent hp = gameObject.AddComponent<CombatHpComponent>();
            hp.MaxHP = 10;
            hp.HP = 10;
            return gameObject;
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

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static void DestroyExistingRuntimeOwners()
        {
            DestroyAll<CombatEntryPoint>();
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    Object.DestroyImmediate(components[i].gameObject);
            }
        }
    }
}
#endif
