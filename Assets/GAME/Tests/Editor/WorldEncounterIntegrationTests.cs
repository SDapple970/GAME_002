#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.Combat
{
    public sealed class WorldEncounterIntegrationTests
    {
        private readonly List<GameObject> _created = new();
        private GameStateMachine _stateMachine;
        private GameFlowController _flow;
        private CombatEntryPoint _entry;
        private CombatWorldLifecycleAdapter _world;

        [SetUp]
        public void SetUp()
        {
            DestroyExistingOwners();
            CombatWorldLifecycleAdapter.ResetOwnershipForTests();

            _stateMachine = CreateComponent<GameStateMachine>("State");
            Invoke(_stateMachine, "Awake");
            _flow = CreateComponent<GameFlowController>("Flow");
            Invoke(_flow, "Awake");
            _entry = CreateComponent<CombatEntryPoint>("Entry");
            Invoke(_entry, "Awake");
            _world = _entry.GetComponent<CombatWorldLifecycleAdapter>();
            Assert.That(_world, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
            CombatWorldLifecycleAdapter.ResetOwnershipForTests();
            DestroyExistingOwners();
        }

        [Test]
        public void IdleEncounter_ReservesOnce()
        {
            CombatEncounterGroup group = CreateGroup();
            Assert.That(group.TryReserve(_entry), Is.True);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.StartReserved));
        }

        [Test]
        public void DuplicateReservation_IsRejected()
        {
            CombatEncounterGroup group = CreateGroup();
            Assert.That(group.TryReserve(_entry), Is.True);
            Assert.That(group.TryReserve(_flow), Is.False);
        }

        [Test]
        public void RejectedStart_ReleasesReservation()
        {
            CombatEncounterGroup group = CreateGroup();
            group.TryReserve(_entry);
            group.ReleaseReservation(_entry);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Idle));
            Assert.That(group.TryReserve(_flow), Is.True);
        }

        [Test]
        public void WrongRequester_CannotReleaseReservation()
        {
            CombatEncounterGroup group = CreateGroup();
            group.TryReserve(_entry);
            group.ReleaseReservation(_flow);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.StartReserved));
        }

        [Test]
        public void AcceptedStart_CommitsActiveCombat()
        {
            CombatEncounterGroup group = CreateGroup();
            group.TryReserve(_entry);
            group.CommitReservation("completion");
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.ActiveCombat));
            Assert.That(group.ActiveCompletionId, Is.EqualTo("completion"));
        }

        [Test]
        public void ContactAndAttackRace_AcceptsOneOwner()
        {
            CombatEncounterGroup group = CreateGroup();
            Assert.That(group.TryReserve(_entry), Is.True);
            Assert.That(group.TryReserve(_flow), Is.False);
        }

        [Test]
        public void ActiveEncounter_CannotStartAgain()
        {
            CombatEncounterGroup group = ActiveGroup("active");
            Assert.That(group.TryReserve(_flow), Is.False);
        }

        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        public void NonVictoryOutcome_EntersRearmPending(CombatEndReason reason)
        {
            CombatEncounterGroup group = ActiveGroup("result");
            CombatResult result = Result("result", reason);
            Assert.That(group.TryBeginOutcome(result), Is.True);
            group.CompleteOutcome(result, true);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.RearmPending));
        }

        [Test]
        public void VictoryWithoutActiveMembers_ClearsEncounter()
        {
            CombatEncounterGroup group = ActiveGroup("victory");
            CombatResult result = Result("victory", CombatEndReason.Victory);
            group.TryBeginOutcome(result);
            group.CompleteOutcome(result, false);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Cleared));
        }

        [Test]
        public void ForcedVictoryWithActiveMembers_RearmsInsteadOfClearing()
        {
            CombatEncounterGroup group = ActiveGroup("victory");
            CombatResult result = Result("victory", CombatEndReason.Victory);
            group.TryBeginOutcome(result);
            group.CompleteOutcome(result, true);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.RearmPending));
        }

        [Test]
        public void ClearedEncounter_CannotRearm()
        {
            CombatEncounterGroup group = ActiveGroup("victory");
            CombatResult result = Result("victory", CombatEndReason.Victory);
            group.TryBeginOutcome(result);
            group.CompleteOutcome(result, false);
            group.ObserveExploration();
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Cleared));
            Assert.That(group.TryReserve(_entry), Is.False);
        }

        [Test]
        public void RearmPending_WaitsForExploration()
        {
            CombatEncounterGroup group = PendingGroup();
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.RearmPending));
        }

        [Test]
        public void RearmPending_WaitsForAllPlayerCollidersToExit()
        {
            CombatEncounterGroup group = PendingGroup();
            Collider2D first = CreateCollider("PlayerColliderA");
            Collider2D second = CreateCollider("PlayerColliderB");
            group.RegisterPlayerCollider(first);
            group.RegisterPlayerCollider(second);
            group.ObserveExploration();
            group.UnregisterPlayerCollider(first);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.RearmPending));
            group.UnregisterPlayerCollider(second);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Idle));
        }

        [Test]
        public void RearmPending_RearmsAfterExplorationWithoutPresence()
        {
            CombatEncounterGroup group = PendingGroup();
            group.ObserveExploration();
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Idle));
        }

        [Test]
        public void ResultForDifferentEncounter_IsIgnored()
        {
            CombatEncounterGroup group = ActiveGroup("expected");
            Assert.That(group.TryBeginOutcome(Result("other", CombatEndReason.Victory)), Is.False);
            Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.ActiveCombat));
        }

        [Test]
        public void DuplicateResult_IsIgnored()
        {
            CombatEncounterGroup group = ActiveGroup("same");
            CombatResult result = Result("same", CombatEndReason.Defeat);
            Assert.That(group.TryBeginOutcome(result), Is.True);
            Assert.That(group.TryBeginOutcome(result), Is.False);
        }

        [Test]
        public void ManualGroup_ReturnsActiveUniqueMembers()
        {
            CombatEncounterGroup group = CreateGroup(autoCollect: false);
            GameObject first = CreateCombatant("EnemyA", 10);
            GameObject second = CreateCombatant("EnemyB", 10);
            SetField(group, "enemies", new List<GameObject> { first, second, first, null });
            CollectionAssert.AreEqual(new[] { first, second }, group.GetActiveEnemies());
        }

        [Test]
        public void ManualGroup_ExcludesInactiveMembers()
        {
            CombatEncounterGroup group = CreateGroup(autoCollect: false);
            GameObject active = CreateCombatant("Active", 10);
            GameObject inactive = CreateCombatant("Inactive", 10);
            inactive.SetActive(false);
            SetField(group, "enemies", new List<GameObject> { active, inactive });
            CollectionAssert.AreEqual(new[] { active }, group.GetActiveEnemies());
        }

        [Test]
        public void ManualGroup_ReturnsFreshSnapshot()
        {
            CombatEncounterGroup group = CreateGroup(autoCollect: false);
            GameObject enemy = CreateCombatant("Enemy", 10);
            SetField(group, "enemies", new List<GameObject> { enemy });
            List<GameObject> first = group.GetActiveEnemies();
            first.Clear();
            Assert.That(group.GetActiveEnemies(), Has.Count.EqualTo(1));
        }

        [Test]
        public void ManualMembers_TakePrecedenceOverAutoCollectedChildren()
        {
            CombatEncounterGroup group = CreateGroup(autoCollect: true);
            GameObject authored = CreateCombatant("Authored", 10);
            CreateCombatant("UnrelatedChild", 10, group.transform);
            SetField(group, "enemies", new List<GameObject> { authored });
            CollectionAssert.AreEqual(new[] { authored }, group.GetActiveEnemies());
        }

        [Test]
        public void Reservation_DoesNotMutateAuthoredMembership()
        {
            CombatEncounterGroup group = CreateGroup(autoCollect: false);
            GameObject enemy = CreateCombatant("Enemy", 10);
            List<GameObject> authored = new List<GameObject> { enemy };
            SetField(group, "enemies", authored);
            group.TryReserve(_entry);
            Assert.That(authored, Has.Count.EqualTo(1));
        }

        [Test]
        public void AutoCollectedGroup_ExcludesInvalidHelpers()
        {
            CombatEncounterGroup group = CreateGroup();
            GameObject valid = CreateCombatant("Valid", 10, group.transform);
            CreateGameObject("Helper", group.transform);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Auto-collected child.*excluded"));
            CollectionAssert.AreEqual(new[] { valid }, group.GetActiveEnemies());
        }

        [Test]
        public void AutoCollectedGroup_WarnsForInvalidHelperOnce()
        {
            CombatEncounterGroup group = CreateGroup();
            CreateGameObject("Helper", group.transform);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Auto-collected child.*excluded"));
            group.GetActiveEnemies();
            group.GetActiveEnemies();
        }

        [Test]
        public void AutoCollectedGroup_ReturnsFullMultiEnemyRoster()
        {
            CombatEncounterGroup group = CreateGroup();
            CreateCombatant("EnemyA", 10, group.transform);
            CreateCombatant("EnemyB", 10, group.transform);
            CreateCombatant("EnemyC", 10, group.transform);
            Assert.That(group.GetActiveEnemies(), Has.Count.EqualTo(3));
        }

        [Test]
        public void LocalTrigger_ActsAsEncounterOwner()
        {
            CombatEncounterTrigger2D trigger = CreateTrigger("EnemyTrigger");
            Assert.That(trigger.RuntimeOwner, Is.SameAs(trigger));
            Assert.That(trigger.TryReserve(_entry), Is.True);
        }

        [Test]
        public void GroupedTrigger_UsesGroupOwner()
        {
            CombatEncounterGroup group = CreateGroup();
            CombatEncounterTrigger2D trigger = CreateTrigger("Trigger", group.transform);
            SetField(trigger, "encounterGroup", group);
            Assert.That(trigger.RuntimeOwner, Is.SameAs(group));
        }

        [Test]
        public void LocalTrigger_RejectedReservationRearms()
        {
            CombatEncounterTrigger2D trigger = CreateTrigger("Trigger");
            trigger.TryReserve(_entry);
            trigger.ReleaseReservation(_entry);
            Assert.That(trigger.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Idle));
        }

        [Test]
        public void LocalTrigger_VictoryStaysClearedAcrossEnableCycle()
        {
            CombatEncounterTrigger2D trigger = CreateTrigger("Trigger");
            trigger.AdoptAcceptedSession("id");
            CombatResult result = Result("id", CombatEndReason.Victory);
            trigger.TryBeginOutcome(result);
            trigger.CompleteOutcome(result, false);
            trigger.enabled = false;
            trigger.enabled = true;
            Assert.That(trigger.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Cleared));
        }

        [Test]
        public void FieldLock_LockIsIdempotent()
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            ToggleBehaviour behaviour = CreateComponent<ToggleBehaviour>("Behaviour");
            SetField(fieldLock, "behavioursToDisable", new List<Behaviour> { behaviour });
            fieldLock.Lock();
            fieldLock.Lock();
            Assert.That(behaviour.enabled, Is.False);
            fieldLock.Unlock();
            Assert.That(behaviour.enabled, Is.True);
        }

        [Test]
        public void FieldLock_UnlockIsIdempotent()
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            fieldLock.Lock();
            Assert.DoesNotThrow(() => { fieldLock.Unlock(); fieldLock.Unlock(); });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FieldLock_RestoresOriginalBehaviourState(bool initiallyEnabled)
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            ToggleBehaviour behaviour = CreateComponent<ToggleBehaviour>("Behaviour");
            behaviour.enabled = initiallyEnabled;
            SetField(fieldLock, "behavioursToDisable", new List<Behaviour> { behaviour });
            fieldLock.Lock();
            fieldLock.Unlock();
            Assert.That(behaviour.enabled, Is.EqualTo(initiallyEnabled));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FieldLock_RestoresOriginalColliderState(bool initiallyEnabled)
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            Collider2D collider = CreateCollider("Collider");
            collider.enabled = initiallyEnabled;
            SetField(fieldLock, "disableColliders2D", new List<Collider2D> { collider });
            fieldLock.Lock();
            fieldLock.Unlock();
            Assert.That(collider.enabled, Is.EqualTo(initiallyEnabled));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FieldLock_RestoresOriginalActiveState(bool initiallyActive)
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            GameObject target = CreateGameObject("Target");
            target.SetActive(initiallyActive);
            SetField(fieldLock, "gameObjectsToDisable", new List<GameObject> { target });
            fieldLock.Lock();
            fieldLock.Unlock();
            Assert.That(target.activeSelf, Is.EqualTo(initiallyActive));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FieldLock_RestoresSimulatedButNotVelocity(bool initiallySimulated)
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            Rigidbody2D body = CreateBody("Body");
            body.simulated = initiallySimulated;
            body.linearVelocity = new Vector2(4f, 2f);
            body.angularVelocity = 3f;
            SetField(fieldLock, "freezeBodies2D", new List<Rigidbody2D> { body });
            fieldLock.Lock();
            Assert.That(body.simulated, Is.False);
            fieldLock.Unlock();
            Assert.That(body.simulated, Is.EqualTo(initiallySimulated));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(body.angularVelocity, Is.Zero);
        }

        [Test]
        public void FieldLock_DestroyedReferencesDoNotThrow()
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            ToggleBehaviour behaviour = CreateComponent<ToggleBehaviour>("Behaviour");
            SetField(fieldLock, "behavioursToDisable", new List<Behaviour> { behaviour });
            fieldLock.Lock();
            Object.DestroyImmediate(behaviour.gameObject);
            Assert.DoesNotThrow(fieldLock.Unlock);
        }

        [Test]
        public void FieldLock_ClearedEnemyIsNotReactivated()
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            GameObject enemy = CreateGameObject("Enemy");
            SetField(fieldLock, "gameObjectsToDisable", new List<GameObject> { enemy });
            fieldLock.Lock();
            fieldLock.UnlockExcept(new HashSet<GameObject> { enemy });
            Assert.That(enemy.activeSelf, Is.False);
        }

        [Test]
        public void FieldLock_InvalidOptionalTargetDoesNotBlockValidTarget()
        {
            CombatFieldLock fieldLock = CreateComponent<CombatFieldLock>("Lock");
            ToggleBehaviour valid = CreateComponent<ToggleBehaviour>("Valid");
            SetField(fieldLock, "behavioursToDisable", new List<Behaviour> { null, valid });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("lock target is missing"));
            fieldLock.Lock();
            Assert.That(valid.enabled, Is.False);
        }

        [Test]
        public void WorldContext_CapturesAllActorsAndIds()
        {
            SessionFixture fixture = CreateSessionFixture(enemyCount: 2);
            StartWorld(fixture.Session);
            Assert.That(_world.CapturedActorCount, Is.EqualTo(3));
            Assert.That(_world.HasCapturedCombatant(1), Is.True);
            Assert.That(_world.HasCapturedCombatant(100), Is.True);
            Assert.That(_world.HasCapturedCombatant(101), Is.True);
            Assert.That(_world.ActiveCompletionId, Is.EqualTo(fixture.Session.CompletionId));
        }

        [Test]
        public void WorldContext_CapturesPositionBeforeMovement()
        {
            SessionFixture fixture = CreateSessionFixture();
            Vector3 original = fixture.Player.transform.position;
            StartWorld(fixture.Session);
            fixture.Player.transform.position += Vector3.right * 10f;
            Assert.That(_world.TryGetCapturedPosition(1, out Vector3 captured), Is.True);
            Assert.That(captured, Is.EqualTo(original));
        }

        [Test]
        public void WorldStart_LocksKnownMovementWithoutDisablingAnimator()
        {
            SessionFixture fixture = CreateSessionFixture(addMovement: true);
            Animator animator = fixture.Enemies[0].AddComponent<Animator>();
            StartWorld(fixture.Session);
            Assert.That(fixture.Enemies[0].GetComponent<FieldEnemyMotor2D>().enabled, Is.False);
            Assert.That(fixture.Enemies[0].GetComponent<FieldEnemyPatrolAI2D>().enabled, Is.False);
            Assert.That(animator.enabled, Is.True);
            Assert.That(_world.IsFieldLocked, Is.True);
        }

        [Test]
        public void WorldStart_FreezesBodiesAndStopsVelocity()
        {
            SessionFixture fixture = CreateSessionFixture();
            Rigidbody2D body = fixture.Enemies[0].GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.right * 5f;
            StartWorld(fixture.Session);
            Assert.That(body.simulated, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void WorldStart_IsIdempotent()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            StartWorld(fixture.Session);
            Assert.That(_world.CapturedActorCount, Is.EqualTo(2));
            Assert.That(_world.CameraEnterCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void DifferentSessionCannotReplaceActiveContext()
        {
            SessionFixture first = CreateSessionFixture();
            StartWorld(first.Session);
            SessionFixture second = CreateSessionFixture();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("second session was rejected"));
            StartWorld(second.Session);
            Assert.That(_world.ActiveCompletionId, Is.EqualTo(first.Session.CompletionId));
        }

        [Test]
        public void MismatchedResult_IsIgnored()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Ignored mismatched completion"));
            EndWorld(Result("stale", CombatEndReason.Defeat));
            Assert.That(_world.OutcomeApplicationCount, Is.Zero);
        }

        [Test]
        public void DuplicateResult_IsAppliedOnce()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            CombatResult result = Result(fixture.Session.CompletionId, CombatEndReason.Defeat);
            EndWorld(result);
            EndWorld(result);
            Assert.That(_world.OutcomeApplicationCount, Is.EqualTo(1));
        }

        [Test]
        public void CombatEnded_DoesNotImmediatelyUnlock()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            Assert.That(_world.IsFieldLocked, Is.True);
        }

        [Test]
        public void RewardState_KeepsFieldLocked()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            Invoke(_world, "HandleGameStateChanged", GameState.CombatResolving, GameState.Reward);
            Assert.That(_world.IsFieldLocked, Is.True);
        }

        [Test]
        public void Exploration_RestoresOnce()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();
            RestoreWorld();
            Assert.That(_world.RestorationCount, Is.EqualTo(1));
            Assert.That(_world.IsFieldLocked, Is.False);
        }

        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        public void NonVictory_RestoresTransformsAndActiveObjects(CombatEndReason reason)
        {
            SessionFixture fixture = CreateSessionFixture();
            Vector3 playerPosition = fixture.Player.transform.position;
            Vector3 enemyPosition = fixture.Enemies[0].transform.position;
            StartWorld(fixture.Session);
            fixture.Player.transform.position += Vector3.right * 7f;
            fixture.Enemies[0].transform.position += Vector3.left * 5f;
            EndWorld(Result(fixture.Session.CompletionId, reason));
            RestoreWorld();
            Assert.That(fixture.Player.transform.position, Is.EqualTo(playerPosition));
            Assert.That(fixture.Enemies[0].transform.position, Is.EqualTo(enemyPosition));
            Assert.That(fixture.Enemies[0].activeSelf, Is.True);
        }

        [Test]
        public void Restoration_RestoresParentRotationAndScale()
        {
            Transform parent = CreateGameObject("Parent").transform;
            SessionFixture fixture = CreateSessionFixture();
            fixture.Enemies[0].transform.SetParent(parent);
            fixture.Enemies[0].transform.rotation = Quaternion.Euler(0f, 0f, 20f);
            fixture.Enemies[0].transform.localScale = new Vector3(2f, 3f, 1f);
            Quaternion rotation = fixture.Enemies[0].transform.rotation;
            Vector3 scale = fixture.Enemies[0].transform.localScale;
            StartWorld(fixture.Session);
            fixture.Enemies[0].transform.SetParent(null);
            fixture.Enemies[0].transform.rotation = Quaternion.identity;
            fixture.Enemies[0].transform.localScale = Vector3.one;
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();
            Assert.That(fixture.Enemies[0].transform.parent, Is.SameAs(parent));
            Assert.That(fixture.Enemies[0].transform.rotation, Is.EqualTo(rotation));
            Assert.That(fixture.Enemies[0].transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void Victory_ClearsEveryUniqueDefeatedEnemyOnce()
        {
            SessionFixture fixture = CreateSessionFixture(enemyCount: 2, withTriggers: true);
            fixture.EnemyHp[0].HP = 0;
            fixture.EnemyHp[1].HP = 0;
            StartWorld(fixture.Session);
            CombatResult result = Result(fixture.Session.CompletionId, CombatEndReason.Victory);
            result.DefeatedEnemyIds.AddRange(new[] { 100, 101, 100 });
            EndWorld(result);
            Assert.That(fixture.Enemies[0].activeSelf, Is.False);
            Assert.That(fixture.Enemies[1].activeSelf, Is.False);
            Assert.That(_world.OutcomeApplicationCount, Is.EqualTo(1));
        }

        [Test]
        public void Victory_DoesNotClearUnrelatedEnemy()
        {
            SessionFixture fixture = CreateSessionFixture(enemyCount: 2);
            fixture.EnemyHp[0].HP = 0;
            StartWorld(fixture.Session);
            CombatResult result = Result(fixture.Session.CompletionId, CombatEndReason.Victory);
            result.DefeatedEnemyIds.Add(100);
            EndWorld(result);
            Assert.That(fixture.Enemies[0].activeSelf, Is.False);
            Assert.That(fixture.Enemies[1].activeSelf, Is.True);
        }

        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Escape)]
        [TestCase(CombatEndReason.Abort)]
        public void NonVictory_DoesNotApplyVictoryCleanup(CombatEndReason reason)
        {
            SessionFixture fixture = CreateSessionFixture();
            fixture.EnemyHp[0].HP = 0;
            StartWorld(fixture.Session);
            CombatResult result = Result(fixture.Session.CompletionId, reason);
            result.DefeatedEnemyIds.Add(100);
            EndWorld(result);
            Assert.That(fixture.Enemies[0].activeSelf, Is.True);
        }

        [Test]
        public void DefeatedEnemy_IsNotRestoredOnExploration()
        {
            SessionFixture fixture = CreateSessionFixture();
            fixture.EnemyHp[0].HP = 0;
            StartWorld(fixture.Session);
            CombatResult result = Result(fixture.Session.CompletionId, CombatEndReason.Victory);
            result.DefeatedEnemyIds.Add(100);
            EndWorld(result);
            RestoreWorld();
            Assert.That(fixture.Enemies[0].activeSelf, Is.False);
        }

        [Test]
        public void SurvivingBody_RestoresWithZeroVelocity()
        {
            SessionFixture fixture = CreateSessionFixture();
            Rigidbody2D body = fixture.Enemies[0].GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.right * 3f;
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();
            Assert.That(body.simulated, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FieldAI_RemainsDisabledThroughRewardAndRestoresInExploration()
        {
            SessionFixture fixture = CreateSessionFixture(addMovement: true);
            FieldEnemyPatrolAI2D patrol = fixture.Enemies[0].GetComponent<FieldEnemyPatrolAI2D>();
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            Assert.That(patrol.enabled, Is.False);
            Invoke(_world, "HandleGameStateChanged", GameState.CombatResolving, GameState.Reward);
            Assert.That(patrol.enabled, Is.False);
            RestoreWorld();
            Assert.That(patrol.enabled, Is.True);
        }

        [Test]
        public void OriginallyDisabledAI_RemainsDisabledAfterRestore()
        {
            SessionFixture fixture = CreateSessionFixture(addMovement: true);
            FieldEnemyPatrolAI2D patrol = fixture.Enemies[0].GetComponent<FieldEnemyPatrolAI2D>();
            patrol.enabled = false;
            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();
            Assert.That(patrol.enabled, Is.False);
        }

        [Test]
        public void Camera_EntersHoldsAndExitsOnce()
        {
            CombatCameraController camera = CreateComponent<CombatCameraController>("CameraController");
            SetField(_world, "cameraController", camera);
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            Assert.That(camera.IsInCombatMode, Is.True);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            Assert.That(camera.IsInCombatMode, Is.True);
            RestoreWorld();
            Assert.That(camera.IsInCombatMode, Is.False);
            Assert.That(_world.CameraEnterCount, Is.EqualTo(1));
            Assert.That(_world.CameraExitCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingCamera_IsSafe()
        {
            SetField(_world, "cameraController", null);
            SessionFixture fixture = CreateSessionFixture();
            Assert.DoesNotThrow(() => StartWorld(fixture.Session));
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            Assert.DoesNotThrow(RestoreWorld);
        }

        [Test]
        public void CameraWithoutFollow_RestoresCapturedTransformAndSize()
        {
            GameObject cameraObject = CreateGameObject("Camera", null, typeof(Camera), typeof(CombatCameraController));
            Camera camera = cameraObject.GetComponent<Camera>();
            CombatCameraController controller = cameraObject.GetComponent<CombatCameraController>();
            camera.transform.position = new Vector3(3f, 4f, -10f);
            camera.orthographicSize = 6f;
            Vector3 originalPosition = camera.transform.position;
            SetField(controller, "transitionDuration", 0f);
            SetField(_world, "cameraController", controller);
            SessionFixture fixture = CreateSessionFixture();

            StartWorld(fixture.Session);
            EndWorld(Result(fixture.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();

            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(6f));
        }

        [Test]
        public void Formation_AppliesOnceWhenEnabled()
        {
            CombatFormationManager formation = CreateComponent<CombatFormationManager>("Formation");
            SetField(formation, "enableFormationPlacement", true);
            SetField(formation, "moveDuration", 0f);
            SetField(_world, "formationManager", formation);
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            StartWorld(fixture.Session);
            Assert.That(formation.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateWorldOwners_DoNotBothOwnEntry()
        {
            GameObject duplicateObject = CreateGameObject("DuplicateWorld");
            CombatWorldLifecycleAdapter duplicate = duplicateObject.AddComponent<CombatWorldLifecycleAdapter>();
            SetField(duplicate, "entryPoint", _entry);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate owner blocked"));
            Invoke(duplicate, "OnEnable");
            Assert.That(_world.OwnsEntryPoint, Is.True);
            Assert.That(duplicate.OwnsEntryPoint, Is.False);
        }

        [Test]
        public void Ownership_ReleasesWhenOwnerDisabled()
        {
            Invoke(_world, "OnDisable");
            GameObject replacementObject = CreateGameObject("ReplacementWorld");
            CombatWorldLifecycleAdapter replacement = replacementObject.AddComponent<CombatWorldLifecycleAdapter>();
            SetField(replacement, "entryPoint", _entry);
            Invoke(replacement, "OnEnable");
            Assert.That(replacement.OwnsEntryPoint, Is.True);
        }

        [Test]
        public void EnableDisable_DoesNotDuplicateSubscriptions()
        {
            _world.enabled = false;
            _world.enabled = true;
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            Assert.That(_world.CapturedActorCount, Is.EqualTo(2));
        }

        [Test]
        public void DisableWithContext_ReleasesPhysicalLockSafely()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Disabled with an active field context"));
            Invoke(_world, "OnDisable");
            Assert.That(_world.IsFieldLocked, Is.False);
        }

        [Test]
        public void ReenableWithContext_RelocksWithoutRecapture()
        {
            SessionFixture fixture = CreateSessionFixture();
            StartWorld(fixture.Session);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Disabled with an active field context"));
            Invoke(_world, "OnDisable");
            SetAutoProperty(_stateMachine, "Current", GameState.CombatPlanning);
            Invoke(_world, "OnEnable");
            Assert.That(_world.IsFieldLocked, Is.True);
            Assert.That(_world.CapturedActorCount, Is.EqualTo(2));
        }

        [Test]
        public void SecondCombatAfterRestoration_UsesFreshIdentityAndSnapshot()
        {
            SessionFixture first = CreateSessionFixture();
            StartWorld(first.Session);
            EndWorld(Result(first.Session.CompletionId, CombatEndReason.Defeat));
            RestoreWorld();

            SessionFixture second = CreateSessionFixture();
            StartWorld(second.Session);

            Assert.That(second.Session.CompletionId, Is.Not.EqualTo(first.Session.CompletionId));
            Assert.That(_world.ActiveCompletionId, Is.EqualTo(second.Session.CompletionId));
            Assert.That(_world.CapturedActorCount, Is.EqualTo(2));
        }

        [Test]
        public void LocalTrigger_MultipleColliderPresenceDoesNotClearPrematurely()
        {
            CombatEncounterTrigger2D trigger = CreateTrigger("Trigger");
            Collider2D first = CreateCollider("First");
            Collider2D second = CreateCollider("Second");
            trigger.RegisterPlayerCollider(first);
            trigger.RegisterPlayerCollider(second);
            trigger.UnregisterPlayerCollider(first);
            Assert.That(trigger.HasPlayerPresence, Is.True);
            trigger.UnregisterPlayerCollider(second);
            Assert.That(trigger.HasPlayerPresence, Is.False);
        }

        [Test]
        public void SceneLikeTeardown_ClearsRegistrySafely()
        {
            Object.DestroyImmediate(_entry.gameObject);
            CombatWorldLifecycleAdapter.ResetOwnershipForTests();
            Assert.DoesNotThrow(() => CombatWorldLifecycleAdapter.FindFor(null));
        }

        [Test]
        public void PlayerFieldAttack_CancelsPendingStateOutsideExploration()
        {
            PlayerFieldAttackController attack = CreateComponent<PlayerFieldAttackController>("Attack");
            SetField(attack, "_attackRunning", true);
            SetField(attack, "_combatStarted", true);
            Invoke(attack, "HandleGameStateChanged", GameState.Exploration, GameState.CombatPlanning);
            Assert.That(GetField<bool>(attack, "_attackRunning"), Is.False);
        }

        [Test]
        public void PlayerFieldAttack_ResetsCombatLatchOnExploration()
        {
            PlayerFieldAttackController attack = CreateComponent<PlayerFieldAttackController>("Attack");
            SetField(attack, "_combatStarted", true);
            Invoke(attack, "HandleGameStateChanged", GameState.Reward, GameState.Exploration);
            Assert.That(GetField<bool>(attack, "_combatStarted"), Is.False);
            Assert.That(GetField<bool>(attack, "_attackRunning"), Is.False);
        }

        [TestCase(GameState.CombatTransition)]
        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        [TestCase(GameState.Reward)]
        [TestCase(GameState.Dialogue)]
        public void PlayerFieldAttack_CannotAttackOutsideExploration(GameState state)
        {
            SetAutoProperty(_stateMachine, "Current", state);
            MethodInfo canAttack = typeof(PlayerFieldAttackController).GetMethod("CanAttack", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((bool)canAttack.Invoke(null, null), Is.False);
        }

        [Test]
        public void WorldAdapter_DoesNotExposeGameStateWriter()
        {
            Assert.That(typeof(CombatWorldLifecycleAdapter).GetMethod("EnterExploration"), Is.Null);
            Assert.That(typeof(CombatWorldLifecycleAdapter).GetMethod("RequestState"), Is.Null);
        }

        [Test]
        public void CombatEntryPoint_HasOneRuntimeWorldAdapter()
        {
            Assert.That(_entry.GetComponents<CombatWorldLifecycleAdapter>(), Has.Length.EqualTo(1));
            Assert.That(CombatWorldLifecycleAdapter.EnsureFor(_entry), Is.SameAs(_world));
            Assert.That(_entry.GetComponents<CombatWorldLifecycleAdapter>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void CanonicalWorldOwner_PreventsEntryFallbackDoubleCleanup()
        {
            SessionFixture fixture = CreateSessionFixture();
            fixture.EnemyHp[0].HP = 0;
            StartWorld(fixture.Session);
            Assert.That(CombatWorldLifecycleAdapter.OwnsSession(_entry, fixture.Session), Is.True);
            CombatResult result = Result(fixture.Session.CompletionId, CombatEndReason.Victory);
            result.DefeatedEnemyIds.Add(100);
            EndWorld(result);
            EndWorld(result);
            Assert.That(_world.OutcomeApplicationCount, Is.EqualTo(1));
        }

        [Test]
        public void EntryFallback_RemainsAvailableWithoutCanonicalOwner()
        {
            SessionFixture fixture = CreateSessionFixture();
            fixture.EnemyHp[0].HP = 0;
            Object.DestroyImmediate(_world);
            Invoke(_entry, "ApplyCombatOutcomeToField", fixture.Session);
            Assert.That(fixture.Enemies[0].activeSelf, Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FullRosterSnapshot_PreservesEveryEnemy(int enemyCount)
        {
            SessionFixture fixture = CreateSessionFixture(enemyCount: enemyCount);
            StartWorld(fixture.Session);
            Assert.That(_world.CapturedActorCount, Is.EqualTo(enemyCount + 1));
        }

        [TestCase(CombatEndReason.Victory, true)]
        [TestCase(CombatEndReason.Defeat, false)]
        [TestCase(CombatEndReason.Escape, false)]
        [TestCase(CombatEndReason.Abort, false)]
        public void OutcomeAuthority_UsesEndReason(CombatEndReason reason, bool victory)
        {
            CombatResult result = Result("id", reason);
            result.IsWin = !victory;
            RewardOutcomeProbe probe = new RewardOutcomeProbe(result);
            Assert.That(probe.IsVictory, Is.EqualTo(victory));
        }

        private void StartWorld(CombatSession session)
        {
            Invoke(_world, "HandleCombatStarted", session);
        }

        private void EndWorld(CombatResult result)
        {
            Invoke(_world, "HandleCombatEnded", result);
        }

        private void RestoreWorld()
        {
            Invoke(_world, "HandleGameStateChanged", GameState.Reward, GameState.Exploration);
        }

        private SessionFixture CreateSessionFixture(int enemyCount = 1, bool withTriggers = false, bool addMovement = false)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment());

            GameObject player = CreateCombatant("Player", 10);
            player.transform.position = new Vector3(-2f, 1f, 0f);
            session.Allies.Add(new FieldCombatantAdapter(1, Side.Allies, player, HpAccessor.TryCreate(player), 6));

            List<GameObject> enemies = new();
            List<CombatHpComponent> hp = new();
            for (int i = 0; i < enemyCount; i++)
            {
                GameObject enemy = CreateCombatant($"Enemy{i}", 10);
                enemy.transform.position = new Vector3(2f + i, 1f, 0f);
                if (withTriggers)
                    enemy.AddComponent<CombatEncounterTrigger2D>();
                if (addMovement)
                {
                    enemy.AddComponent<FieldEnemyMotor2D>();
                    enemy.AddComponent<FieldEnemyPatrolAI2D>();
                }
                enemies.Add(enemy);
                hp.Add(enemy.GetComponent<CombatHpComponent>());
                session.Enemies.Add(new FieldCombatantAdapter(100 + i, Side.Enemies, enemy, HpAccessor.TryCreate(enemy), 8));
            }

            return new SessionFixture(session, player, enemies, hp);
        }

        private CombatEncounterGroup CreateGroup(bool autoCollect = true)
        {
            CombatEncounterGroup group = CreateComponent<CombatEncounterGroup>("Group");
            SetField(group, "autoCollectChildren", autoCollect);
            return group;
        }

        private CombatEncounterGroup ActiveGroup(string completionId)
        {
            CombatEncounterGroup group = CreateGroup();
            group.AdoptAcceptedSession(completionId);
            return group;
        }

        private CombatEncounterGroup PendingGroup()
        {
            CombatEncounterGroup group = ActiveGroup("pending");
            CombatResult result = Result("pending", CombatEndReason.Defeat);
            group.TryBeginOutcome(result);
            group.CompleteOutcome(result, true);
            return group;
        }

        private CombatEncounterTrigger2D CreateTrigger(string name, Transform parent = null)
        {
            GameObject go = CreateGameObject(name, parent, typeof(BoxCollider2D));
            return go.AddComponent<CombatEncounterTrigger2D>();
        }

        private Collider2D CreateCollider(string name)
        {
            return CreateGameObject(name, null, typeof(BoxCollider2D)).GetComponent<Collider2D>();
        }

        private Rigidbody2D CreateBody(string name)
        {
            return CreateGameObject(name, null, typeof(Rigidbody2D)).GetComponent<Rigidbody2D>();
        }

        private GameObject CreateCombatant(string name, int hp, Transform parent = null)
        {
            GameObject go = CreateGameObject(name, parent, typeof(Rigidbody2D));
            CombatHpComponent component = go.AddComponent<CombatHpComponent>();
            component.MaxHP = Math.Max(1, hp);
            component.HP = hp;
            return go;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            return CreateGameObject(name).AddComponent<T>();
        }

        private GameObject CreateGameObject(string name, Transform parent = null, params Type[] components)
        {
            GameObject go = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            _created.Add(go);
            return go;
        }

        private static CombatResult Result(string completionId, CombatEndReason reason)
        {
            return new CombatResult
            {
                CompletionId = completionId,
                EndReason = reason,
                IsWin = reason == CombatEndReason.Victory,
                EscapeSucceeded = reason == CombatEndReason.Escape
            };
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {target.GetType().Name}.{methodName}");
            try
            {
                method.Invoke(target, args);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void DestroyExistingOwners()
        {
            foreach (CombatEntryPoint entry in Object.FindObjectsByType<CombatEntryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (entry != null) Object.DestroyImmediate(entry.gameObject);
            foreach (GameFlowController flow in Object.FindObjectsByType<GameFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (flow != null) Object.DestroyImmediate(flow.gameObject);
            foreach (GameStateMachine state in Object.FindObjectsByType<GameStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (state != null) Object.DestroyImmediate(state.gameObject);
        }

        private sealed class SessionFixture
        {
            internal readonly CombatSession Session;
            internal readonly GameObject Player;
            internal readonly List<GameObject> Enemies;
            internal readonly List<CombatHpComponent> EnemyHp;

            internal SessionFixture(CombatSession session, GameObject player, List<GameObject> enemies, List<CombatHpComponent> enemyHp)
            {
                Session = session;
                Player = player;
                Enemies = enemies;
                EnemyHp = enemyHp;
            }
        }

        private sealed class RewardOutcomeProbe
        {
            internal readonly bool IsVictory;

            internal RewardOutcomeProbe(CombatResult result)
            {
                IsVictory = result.EndReason != CombatEndReason.None
                    ? result.EndReason == CombatEndReason.Victory
                    : result.IsWin;
            }
        }

        private sealed class ToggleBehaviour : MonoBehaviour
        {
        }
    }
}
#endif
