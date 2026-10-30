using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    /// <summary>Core capability tests with explicit fixture setup; not a Production auto-start installer.</summary>
    public sealed class CombatHandBpInitializationTests
    {
        private readonly List<Object> _created = new();
        private CharacterSkillRuntime _skills;
        private SkillDefinitionSO[] _definitions;
        private GameObject[] _allies;
        private GameObject _enemy;

        [SetUp]
        public void SetUp()
        {
            foreach (var old in Object.FindObjectsByType<CharacterSkillSaveParticipant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            var participant = Track(new GameObject("P2A.CharacterSkills")).AddComponent<CharacterSkillSaveParticipant>();
            typeof(CharacterSkillSaveParticipant).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(participant, null);
            _skills = participant.Runtime;
            _definitions = new[] { Definition(21), Definition(22) };
            _allies = new[] { Actor("P2A.AllyA"), Actor("P2A.AllyB") };
            _enemy = Actor("P2A.Enemy");
            foreach (string id in new[] { "p2a.a", "p2a.b" })
                foreach (var definition in _definitions)
                {
                    _skills.TryAcquire(id, definition.PersistentKey);
                    _skills.TryEquip(id, definition.PersistentKey);
                }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void SnapshotHands_AreIndependentAndIdsAreSessionLocal_WithoutDebugComponents()
        {
            CombatSession session = Start(out _);
            Assert.That(session.HasHandValidation, Is.False, "Bootstrapper currently has no authored hand auto-setup.");
            Assert.That(session.BpRuntime, Is.Null);
            InitializeFixture(session);
            HashSet<long> ids = new();
            var actors = session.Allies.Concat(session.Enemies).ToArray();
            foreach (ICombatant actor in actors)
            {
                Assert.That(session.TryGetHand(actor, out var hand), Is.True);
                Assert.That(hand.DeckSnapshot.Select(card => card.Skill), Is.EquivalentTo(actor.Skills));
                foreach (var card in hand.DeckSnapshot)
                {
                    Assert.That(ids.Add(card.RuntimeId), Is.True);
                    Assert.That(((SoSkill)card.Skill).Definition.PersistentKey, Does.StartWith("p2a.skill."));
                }
            }
            session.TryGetHand(actors[0], out var first);
            session.TryGetHand(actors[1], out var second);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(value => value.GetType().Namespace?.Contains("Debugging") == true), Is.False);
        }

        [Test]
        public void Reinitialization_IsRejectedWithoutReplacingHandsOrBp()
        {
            CombatSession session = Start(out _); InitializeFixture(session);
            var actor = session.Allies[0]; session.TryGetHand(actor, out var before);
            var bp = session.BpRuntime;
            Assert.That(session.TryEnableHandValidation(actor, Entries(actor), 2, 0, 7), Is.False);
            Assert.That(session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.False);
            session.TryGetHand(actor, out var after);
            Assert.That(after, Is.SameAs(before));
            Assert.That(session.BpRuntime, Is.SameAs(bp));
        }

        [Test]
        public void NewCombat_UsesNewEquippedSnapshotAndFreshHandAndBp()
        {
            CombatSession previous = Start(out _); InitializeFixture(previous);
            var actor = previous.Allies[0]; previous.TryGetHand(actor, out var oldHand);
            Gain(previous, 1);
            Assert.That(previous.BpRuntime.TryGetBalance(actor, out int balance), Is.True);
            Assert.That(balance, Is.EqualTo(1));
            _skills.TryUnequip("p2a.a", _definitions[0].PersistentKey);
            CombatSession next = Start(out _); InitializeFixture(next);
            next.TryGetHand(next.Allies[0], out var newHand);
            Assert.That(next.CompletionId, Is.Not.EqualTo(previous.CompletionId));
            Assert.That(newHand, Is.Not.SameAs(oldHand));
            Assert.That(newHand.DeckSnapshot.Select(card => card.Skill.Id.Value), Is.EqualTo(new[] { 22 }));
            Assert.That(oldHand.DeckSnapshot.Select(card => card.Skill.Id.Value), Is.EqualTo(new[] { 21, 22 }));
            Assert.That(next.BpRuntime.TryGetBalance(next.Allies[0], out int nextBalance), Is.True);
            Assert.That(nextBalance, Is.Zero);
            Assert.That(newHand.RefillCount, Is.Zero);
        }

        [Test]
        public void ApprovedSpend_ConsumesOnceChargesUltimateAndRefillsOnlyAtCycleBoundary()
        {
            CombatSession session = Start(out _); InitializeFixture(session);
            var actor = session.Allies[0]; var skill = actor.Skills[0];
            Assert.That(session.TryAssignUltimateSkill(actor, skill), Is.True);
            session.TryGetHand(actor, out var hand);
            session.TryGetHand(session.Allies[1], out var companion);
            Gain(session, 1);
            var card = hand.Hand.Single(value => ReferenceEquals(value.Skill, skill));
            var attack = new CombatAttackDeclaration(actor, session.Enemies[0], skill, card.RuntimeId, 1);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.True);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(1));
            Assert.That(session.UltimateRuntime.TryGetStage(actor, out int stage), Is.True);
            Assert.That(stage, Is.EqualTo(1));
            Assert.That(session.UltimateRuntime.TryAcceptApprovedSpend(session.BpRuntime.ApprovedSpends[0]), Is.False);
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(1));
            Assert.That(companion.NormalDiscard.Count, Is.Zero);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(hand.RefillCount, Is.Zero);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, 1, true), Is.True);
            Assert.That(hand.RefillCount, Is.EqualTo(1));
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, 1, true), Is.False);
            Assert.That(hand.RefillCount, Is.EqualTo(1));
        }

        [Test]
        public void LegacyPlanning_DoesNotAdoptFinalExchangeHandOrBp()
        {
            CombatSession session = Start(out _, CombatFlowMode.LegacyPlanning);
            Assert.That(session.TryEnableHandValidation(session.Allies[0], Entries(session.Allies[0]), 2, 0, 7), Is.False);
            Assert.That(session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3)), Is.False);
            Assert.That(session.HasHandValidation, Is.False);
            Assert.That(session.BpRuntime, Is.Null);
        }

        private CombatSession Start(out CombatStateMachine machine, CombatFlowMode mode = CombatFlowMode.StandoffClashChain)
        {
            var request = new CombatStartRequest(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null,
                mode, new CombatRuntimeConfig(10, 10, 3, 0));
            request.AllyFieldObjects.AddRange(_allies); request.EnemyFieldObjects.Add(_enemy);
            request.BindAllyCharacter(_allies[0], "p2a.a"); request.BindAllyCharacter(_allies[1], "p2a.b");
            PersistentSkillCombatLoadoutBridge.ApplyToRequest(request, _definitions, null);
            SkillBook book = new(); foreach (var definition in _definitions) book.Register(new SoSkill(definition));
            (CombatSession session, CombatStateMachine stateMachine) = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book));
            machine = stateMachine; return session;
        }

        private static IEnumerable<CombatHandEntry> Entries(ICombatant actor) => actor.Skills.Select(skill => new CombatHandEntry(skill, CombatCardKind.Normal));
        private static void InitializeFixture(CombatSession session)
        {
            // Explicit fixture classification/capacity/seed and BP clock, never Production defaults.
            foreach (var actor in session.Allies.Concat(session.Enemies))
                Assert.That(session.TryEnableHandValidation(actor, Entries(actor), 2, 0, 7), Is.True);
            var enhancements = session.Allies.SelectMany(actor => actor.Skills)
                .Select(skill => new CombatBpEnhancementEntry(skill, CombatBpEnhancementKind.ExtraHits, 1)).ToArray();
            Assert.That(session.TryEnableBpValidation(new CombatBpValidationConfig(3, 1, 3), enhancements), Is.True);
        }
        private static void Gain(CombatSession session, int id)
        {
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Allies, id, true), Is.True);
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Enemies, id, true), Is.True);
        }
        private SkillDefinitionSO Definition(int id)
        {
            var definition = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            definition.skillId = id;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("persistentKey").stringValue = "p2a.skill." + id;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return definition;
        }
        private GameObject Actor(string name)
        {
            var go = Track(new GameObject(name)); go.AddComponent<CombatHpComponent>();
            var loadout = go.AddComponent<CombatSkillLoadoutComponent>();
            var serialized = new SerializedObject(loadout); var ids = serialized.FindProperty("skillIds");
            ids.arraySize = 1; ids.GetArrayElementAtIndex(0).intValue = 21; serialized.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
