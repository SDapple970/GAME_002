#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Combat
{
    public sealed class CombatEntryConsolidationTests
    {
        private readonly List<GameObject> _createdObjects = new();

        private GameStateMachine _stateMachine;
        private GameFlowController _flow;
        private CombatEntryPoint _entryPoint;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingOwners();
            _stateMachine = CreateComponent<GameStateMachine>("CombatEntryState");
            InvokePrivate(_stateMachine, "Awake");
            _flow = CreateComponent<GameFlowController>("CombatEntryFlow");
            InvokePrivate(_flow, "Awake");
            _entryPoint = CreateComponent<CombatEntryPoint>("CombatEntryPoint");
            InvokePrivate(_entryPoint, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
            DestroyExistingOwners();
        }

        [Test]
        public void NullRequestIsRejected()
        {
            Assert.That(_entryPoint.StartCombat(null), Is.False);
        }

        [Test]
        public void RequestWithNoAlliesIsRejected()
        {
            CombatStartRequest request = CreateRequest(Array.Empty<GameObject>(), new[] { CreateCombatant("Enemy") });
            Assert.That(_entryPoint.StartCombat(request), Is.False);
        }

        [Test]
        public void RequestWithNoEnemiesIsRejected()
        {
            CombatStartRequest request = CreateRequest(new[] { CreateCombatant("Ally") }, Array.Empty<GameObject>());
            Assert.That(_entryPoint.StartCombat(request), Is.False);
        }

        [Test]
        public void NullRosterEntriesAreRemovedSafely()
        {
            GameObject ally = CreateCombatant("Ally");
            GameObject enemy = CreateCombatant("Enemy");
            CombatStartRequest request = CreateRequest(new[] { null, ally, null }, new[] { enemy, null });

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(1));
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void InactiveRosterObjectsAreRemoved()
        {
            GameObject inactiveAlly = CreateCombatant("InactiveAlly");
            inactiveAlly.SetActive(false);
            CombatStartRequest request = CreateRequest(
                new[] { inactiveAlly, CreateCombatant("ActiveAlly") },
                new[] { CreateCombatant("Enemy") });

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(1));
            Assert.That(FieldObject(_entryPoint.ActiveSession.Allies[0]).name, Is.EqualTo("ActiveAlly"));
        }

        [Test]
        public void DuplicateAllyEntriesProduceOneCombatant()
        {
            GameObject ally = CreateCombatant("Ally");
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { ally, ally },
                new[] { CreateCombatant("Enemy") })), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateEnemyEntriesProduceOneCombatant()
        {
            GameObject enemy = CreateCombatant("Enemy");
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { enemy, enemy })), Is.True);
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void ObjectAssignedToBothSidesIsRejected()
        {
            GameObject shared = CreateCombatant("Shared");
            Assert.That(_entryPoint.StartCombat(CreateRequest(new[] { shared }, new[] { shared })), Is.False);
        }

        [Test]
        public void CombatantWithoutHpAdapterRejectsStart()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateGameObject("InvalidEnemy") })), Is.False);
        }

        [Test]
        public void InvalidMixedRosterDoesNotStartPartialEncounter()
        {
            CombatStartRequest request = CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("ValidEnemy"), CreateGameObject("InvalidEnemy") });

            Assert.That(_entryPoint.StartCombat(request), Is.False);
            Assert.That(_entryPoint.ActiveSession, Is.Null);
        }

        [Test]
        public void OneAllyVersusOneEnemyStarts()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("Enemy") })), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(1));
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void OneAllyVersusTwoEnemiesPreservesBothEnemies()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("EnemyOne"), CreateCombatant("EnemyTwo") })), Is.True);
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(2));
        }

        [Test]
        public void TwoAlliesVersusTwoEnemiesPreservesAllCombatants()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("AllyOne"), CreateCombatant("AllyTwo") },
                new[] { CreateCombatant("EnemyOne"), CreateCombatant("EnemyTwo") })), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(2));
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(2));
        }

        [Test]
        public void ConstructedCombatantSidesAreCorrect()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("AllyOne"), CreateCombatant("AllyTwo") },
                new[] { CreateCombatant("EnemyOne"), CreateCombatant("EnemyTwo") })), Is.True);

            Assert.That(_entryPoint.ActiveSession.Allies.TrueForAll(value => value.Side == Side.Allies), Is.True);
            Assert.That(_entryPoint.ActiveSession.Enemies.TrueForAll(value => value.Side == Side.Enemies), Is.True);
        }

        [Test]
        public void CombatantIdsAreUnique()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("AllyOne"), CreateCombatant("AllyTwo") },
                new[] { CreateCombatant("EnemyOne"), CreateCombatant("EnemyTwo") })), Is.True);

            HashSet<int> ids = new HashSet<int>();
            foreach (ICombatant combatant in _entryPoint.ActiveSession.Allies)
                Assert.That(ids.Add(combatant.Id.Value), Is.True);
            foreach (ICombatant combatant in _entryPoint.ActiveSession.Enemies)
                Assert.That(ids.Add(combatant.Id.Value), Is.True);
        }

        [Test]
        public void CallerRequestListsRemainUnchanged()
        {
            GameObject ally = CreateCombatant("Ally");
            GameObject enemy = CreateCombatant("Enemy");
            CombatStartRequest request = CreateRequest(new[] { null, ally, ally }, new[] { enemy, null, enemy });
            GameObject[] alliesBefore = request.AllyFieldObjects.ToArray();
            GameObject[] enemiesBefore = request.EnemyFieldObjects.ToArray();

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            CollectionAssert.AreEqual(alliesBefore, request.AllyFieldObjects);
            CollectionAssert.AreEqual(enemiesBefore, request.EnemyFieldObjects);
        }

        [Test]
        public void DefaultInspirationIsNormalizedOnce()
        {
            CombatStartRequest request = CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("Enemy") },
                inspirationMax: 0,
                inspirationStart: -1);

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_entryPoint.ActiveSession.Inspiration.Max, Is.EqualTo(10));
            Assert.That(_entryPoint.ActiveSession.Inspiration.Current, Is.EqualTo(4));
            Assert.That(request.InspirationMax, Is.Zero);
            Assert.That(request.InspirationStart, Is.EqualTo(-1));
        }

        [Test]
        public void InspirationStartIsClampedWithoutMutatingRequest()
        {
            CombatStartRequest request = CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("Enemy") },
                inspirationMax: 5,
                inspirationStart: 99);

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_entryPoint.ActiveSession.Inspiration.Max, Is.EqualTo(5));
            Assert.That(_entryPoint.ActiveSession.Inspiration.Current, Is.EqualTo(5));
            Assert.That(request.InspirationStart, Is.EqualTo(99));
        }

        [Test]
        public void DuplicateStartDuringActiveCombatIsRejected()
        {
            Assert.That(_entryPoint.StartCombat(ValidRequest("First")), Is.True);
            Assert.That(_entryPoint.StartCombat(ValidRequest("Second")), Is.False);
        }

        [Test]
        public void RejectedDuplicateDoesNotRaiseSecondStartedEvent()
        {
            int startedCount = 0;
            _entryPoint.OnCombatStarted += _ => startedCount++;

            Assert.That(_entryPoint.StartCombat(ValidRequest("First")), Is.True);
            Assert.That(_entryPoint.StartCombat(ValidRequest("Second")), Is.False);
            Assert.That(startedCount, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulCombatRaisesStartedExactlyOnce()
        {
            int startedCount = 0;
            _entryPoint.OnCombatStarted += _ => startedCount++;

            Assert.That(_entryPoint.StartCombat(ValidRequest("Valid")), Is.True);
            Assert.That(startedCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedCombatRaisesNoStartedEvent()
        {
            int startedCount = 0;
            _entryPoint.OnCombatStarted += _ => startedCount++;

            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateGameObject("Invalid") })), Is.False);
            Assert.That(startedCount, Is.Zero);
        }

        [Test]
        public void FailedCombatLeavesActiveSessionNull()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                Array.Empty<GameObject>())), Is.False);
            Assert.That(_entryPoint.ActiveSession, Is.Null);
        }

        [Test]
        public void FailedCombatLeavesActiveStateMachineNull()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                Array.Empty<GameObject>())), Is.False);
            Assert.That(_entryPoint.ActiveStateMachine, Is.Null);
        }

        [Test]
        public void FailedEntryRemainsReusableForValidRequest()
        {
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                Array.Empty<GameObject>())), Is.False);
            Assert.That(_entryPoint.StartCombat(ValidRequest("Valid")), Is.True);
        }

        [Test]
        public void StartCombatFromFieldUsesCanonicalValidation()
        {
            Assert.That(_entryPoint.StartCombatFromField(
                new List<GameObject> { CreateCombatant("Ally") },
                new List<GameObject> { CreateGameObject("InvalidEnemy") },
                StartReason.PlayerFirstHit,
                Side.Allies,
                null), Is.False);
        }

        [Test]
        public void StartCombatFromFieldPreservesCompleteRosters()
        {
            Assert.That(_entryPoint.StartCombatFromField(
                new List<GameObject> { CreateCombatant("AllyOne"), CreateCombatant("AllyTwo") },
                new List<GameObject> { CreateCombatant("EnemyOne"), CreateCombatant("EnemyTwo") },
                StartReason.SpecialSkill,
                Side.Enemies,
                null), Is.True);
            Assert.That(_entryPoint.ActiveSession.Allies.Count, Is.EqualTo(2));
            Assert.That(_entryPoint.ActiveSession.Enemies.Count, Is.EqualTo(2));
        }

        [Test]
        public void EncounterGroupReturnsDeduplicatedManualSnapshot()
        {
            CombatEncounterGroup group = CreateComponent<CombatEncounterGroup>("Group");
            GameObject enemy = CreateCombatant("Enemy");
            GameObject inactive = CreateCombatant("Inactive");
            inactive.SetActive(false);
            List<GameObject> serialized = new List<GameObject> { enemy, enemy, null, inactive };
            SetPrivateField(group, "autoCollectChildren", false);
            SetPrivateField(group, "enemies", serialized);

            List<GameObject> first = group.GetActiveEnemies();
            List<GameObject> second = group.GetActiveEnemies();

            CollectionAssert.AreEqual(new[] { enemy }, first);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(serialized.Count, Is.EqualTo(4));
        }

        [Test]
        public void EncounterGroupExcludesNonCombatHelperChildren()
        {
            CombatEncounterGroup group = CreateComponent<CombatEncounterGroup>("Group");
            GameObject enemy = CreateCombatant("Enemy");
            enemy.transform.SetParent(group.transform);
            GameObject helper = CreateGameObject("FormationMarker");
            helper.transform.SetParent(group.transform);
            LogAssert.Expect(LogType.Warning, new Regex("Auto-collected child 'FormationMarker'.*excluded"));

            CollectionAssert.AreEqual(new[] { enemy }, group.GetActiveEnemies());
        }

        [Test]
        public void ValidRosterOrderRemainsStable()
        {
            GameObject allyOne = CreateCombatant("AllyOne");
            GameObject allyTwo = CreateCombatant("AllyTwo");
            GameObject enemyOne = CreateCombatant("EnemyOne");
            GameObject enemyTwo = CreateCombatant("EnemyTwo");

            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { allyTwo, allyOne },
                new[] { enemyTwo, enemyOne })), Is.True);
            CollectionAssert.AreEqual(
                new[] { allyTwo, allyOne },
                _entryPoint.ActiveSession.Allies.ConvertAll(FieldObject));
            CollectionAssert.AreEqual(
                new[] { enemyTwo, enemyOne },
                _entryPoint.ActiveSession.Enemies.ConvertAll(FieldObject));
        }

        [Test]
        public void StartReasonAndInitiativeArePreserved()
        {
            CombatStartRequest request = CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { CreateCombatant("Enemy") },
                StartReason.PlayerGotHit,
                Side.Enemies);

            Assert.That(_entryPoint.StartCombat(request), Is.True);
            Assert.That(_entryPoint.ActiveSession.StartReason, Is.EqualTo(StartReason.PlayerGotHit));
            Assert.That(_entryPoint.ActiveSession.InitiativeSide, Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void StartupExceptionRollsBackAndEntryRemainsReusable()
        {
            bool throwOnce = true;
            _stateMachine.OnStateChanged += (_, next) =>
            {
                if (next == GameState.CombatPlanning && throwOnce)
                {
                    throwOnce = false;
                    throw new InvalidOperationException("Injected startup failure");
                }
            };
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back.*Injected startup failure"));

            Assert.That(_entryPoint.StartCombat(ValidRequest("First")), Is.False);
            Assert.That(_entryPoint.ActiveSession, Is.Null);
            Assert.That(_entryPoint.ActiveStateMachine, Is.Null);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(_entryPoint.StartCombat(ValidRequest("Second")), Is.True);
        }

        [Test]
        public void NonExplorationGameStateRejectsEntry()
        {
            _flow.BeginDialogue();
            Assert.That(_entryPoint.StartCombat(ValidRequest("Dialogue")), Is.False);
            Assert.That(_stateMachine.Current, Is.EqualTo(GameState.Dialogue));
        }

        [Test]
        public void MissingRequiredFlowDependencyRejectsEntry()
        {
            UnityEngine.Object.DestroyImmediate(_flow.gameObject);
            _flow = null;

            Assert.That(_entryPoint.StartCombat(ValidRequest("MissingFlow")), Is.False);
            Assert.That(_entryPoint.ActiveSession, Is.Null);
        }

        [Test]
        public void FieldOutcomeProcessingSeesEveryDefeatedEnemy()
        {
            GameObject enemyOne = CreateCombatant("EnemyOne");
            GameObject enemyTwo = CreateCombatant("EnemyTwo");
            Assert.That(_entryPoint.StartCombat(CreateRequest(
                new[] { CreateCombatant("Ally") },
                new[] { enemyOne, enemyTwo })), Is.True);
            enemyOne.GetComponent<CombatHpComponent>().HP = 0;
            enemyTwo.GetComponent<CombatHpComponent>().HP = 0;

            InvokePrivate(_entryPoint, "ApplyCombatOutcomeToField", _entryPoint.ActiveSession);

            Assert.That(enemyOne.activeSelf, Is.False);
            Assert.That(enemyTwo.activeSelf, Is.False);
        }

        private CombatStartRequest ValidRequest(string suffix)
        {
            return CreateRequest(
                new[] { CreateCombatant($"Ally{suffix}") },
                new[] { CreateCombatant($"Enemy{suffix}") });
        }

        private static CombatStartRequest CreateRequest(
            IEnumerable<GameObject> allies,
            IEnumerable<GameObject> enemies,
            StartReason reason = StartReason.PlayerFirstHit,
            Side initiative = Side.Allies,
            int inspirationMax = 10,
            int inspirationStart = 3)
        {
            CombatStartRequest request = new CombatStartRequest(reason, initiative, inspirationMax, inspirationStart, null);
            request.AllyFieldObjects.AddRange(allies);
            request.EnemyFieldObjects.AddRange(enemies);
            return request;
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

        private static GameObject FieldObject(ICombatant combatant)
        {
            return ((FieldCombatantAdapter)combatant).FieldObject;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {target.GetType().Name}.{methodName}");
            method.Invoke(target, arguments);
        }

        private static void DestroyExistingOwners()
        {
            DestroyAll<CombatEntryPoint>();
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    UnityEngine.Object.DestroyImmediate(components[i].gameObject);
            }
        }
    }
}
#endif
