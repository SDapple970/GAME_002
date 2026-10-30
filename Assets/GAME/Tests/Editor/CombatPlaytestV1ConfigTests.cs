using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    /// <summary>Approved Playtest values, with transient content and the existing B2D Clash fixture.</summary>
    public sealed class CombatPlaytestV1ConfigTests
    {
        public const string ConfigPath = "Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_PlaytestV1.asset";
        private readonly List<Object> _created = new();
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private GameFlowController _gameFlow;
        private CharacterSkillRuntime _skills;
        private FinalCombatRuntimeConfigSO _config;
        private SkillDefinitionSO _normal;
        private SkillDefinitionSO _clash;

        // Explicit authoring command for this new test-only asset. Never saves other dirty assets.
        public static void CreatePlaytestAssetIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath) != null) return;
            const string parent = "Assets/GAME/Tests/Editor/Fixtures";
            if (!AssetDatabase.IsValidFolder(parent + "/CombatPlaytestV1"))
                AssetDatabase.CreateFolder(parent, "CombatPlaytestV1");
            var config = ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>();
            var data = new SerializedObject(config);
            data.FindProperty("normalHandCapacity").intValue = 2;
            data.FindProperty("clashHandCapacity").intValue = 3;
            data.FindProperty("bpMaximum").intValue = 3;
            data.FindProperty("bpRoundCyclesPerGain").intValue = 2;
            data.FindProperty("bpMaximumInvestment").intValue = 3;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SetLabels(config, new[] { "TestOnly", "PlaytestV1", "ContentPending", "ClashPending" });
            AssetDatabase.SaveAssetIfDirty(config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_entry?.ActiveStateMachine != null) _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void SavedPlaytestData_IsDisabledAndContainsOnlyApprovedHandBpSettings()
        {
            var config = LoadConfig();
            Assert.That(config.EnableProductionHandBp, Is.False);
            Assert.That(config.NormalHandCapacity, Is.EqualTo(2));
            Assert.That(config.ClashHandCapacity, Is.EqualTo(3));
            Assert.That(config.BpMaximum, Is.EqualTo(3));
            Assert.That(config.BpRoundCyclesPerGain, Is.EqualTo(2));
            Assert.That(config.BpMaximumInvestment, Is.EqualTo(3));
            Assert.That(config.HandCardRules, Is.Empty); Assert.That(config.UltimateSkills, Is.Empty);
            Assert.That(config.EnemyHandSourceKeys, Is.Empty);
            Assert.That(config.ClashAttackerDurability, Is.Zero); Assert.That(config.ClashResponderDurability, Is.Zero);
            Assert.That(config.ClashMaxComparisons, Is.Zero); Assert.That(config.ClashDecisionDurationSeconds, Is.Zero);
            Assert.That(config.FinalSkillPolicy, Is.EqualTo(FinalCombatRuntimeConfigSO.ClashFinalSkillPolicy.Unspecified));
            Assert.That(config.TimeoutAttackPolicy, Is.EqualTo(FinalCombatRuntimeConfigSO.ClashTimeoutAttackPolicy.Unspecified));
            Assert.That(AssetDatabase.GetLabels(config), Does.Contain("TestOnly"));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EquippedSnapshots_DrawThreeClashTwoNormalOnce_WithIndependentHands(int allies)
        {
            Start(allies);
            var session = _entry.ActiveSession;
            HashSet<long> ids = new(); HashSet<CombatHandRuntime> hands = new();
            foreach (var actor in session.Allies)
            {
                Assert.That(session.TryGetHand(actor, out var hand), Is.True);
                Assert.That(hands.Add(hand), Is.True);
                AssertHand(hand, 2, 3); Assert.That(hand.RefillCount, Is.Zero);
                Assert.That(hand.DeckSnapshot.Select(card => card.Skill).Distinct(), Is.EquivalentTo(actor.Skills));
                Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(10), "4/6 copies are existing fixture data, not Playtest content approval.");
                foreach (var card in hand.DeckSnapshot) Assert.That(ids.Add(card.RuntimeId), Is.True);
                Assert.That(hand.DeckSnapshot.Select(card => ((SoSkill)card.Skill).Definition.PersistentKey).Distinct().Count(), Is.EqualTo(2));
                Assert.That(session.TryEnableHandValidation(actor, Array.Empty<CombatHandEntry>(), 2, 3, 0), Is.False);
                Assert.That(Balance(actor), Is.Zero); Assert.That(Stage(actor), Is.Zero);
            }
            _skills.TryUnequip("playtest.fixture.hero.0", _normal.PersistentKey);
            session.TryGetHand(session.Allies[0], out var first);
            AssertHand(first, 2, 3); // Equipment edits never mutate the already captured combat snapshot.
            Assert.That(session.TryGetHand(session.Enemies[0], out _), Is.False);
            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(session, session.Enemies[0], out _), Is.True);
        }

        [Test]
        public void DrawDiscardRefill_UsesExactInstancesAndOneCompletedCycleBoundary()
        {
            Start(1, enemyHand: true);
            var session = _entry.ActiveSession; var ally = session.Allies[0]; var enemy = session.Enemies[0];
            session.TryGetHand(ally, out var hand); session.TryGetHand(enemy, out var enemyHand);
            var normal = hand.Hand.First(card => card.Kind == CombatCardKind.Normal);
            var clash = enemyHand.Hand.First(card => card.Kind == CombatCardKind.Clash);
            var attack = new CombatAttackDeclaration(ally, enemy, normal.Skill, normal.RuntimeId);
            var response = new CombatResponseDeclaration(enemy, clash.Skill, clash.RuntimeId);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, response, out _), Is.True);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, response, out _), Is.False);
            Assert.That(hand.NormalDiscard.Single().RuntimeId, Is.EqualTo(normal.RuntimeId));
            Assert.That(enemyHand.ClashDiscard.Single().RuntimeId, Is.EqualTo(clash.RuntimeId));
            AssertHand(hand, 1, 3); AssertHand(enemyHand, 2, 2);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(hand.RefillCount, Is.Zero);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, 1, true), Is.True);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, 1, true), Is.False);
            Assert.That(hand.RefillCount, Is.EqualTo(1)); Assert.That(enemyHand.RefillCount, Is.EqualTo(1));
            AssertHand(hand, 2, 3); AssertHand(enemyHand, 2, 3);
            Assert.That(session.BpRuntime.RoundCycleCount, Is.Zero, "Hand refill does not advance the independent BP clock.");
        }

        [Test]
        public void BpClock_RequiresTwoValidPairedCycles_RejectsEmptyAndDuplicateCycles_AndCapsAtThree()
        {
            Start(3); var session = _entry.ActiveSession;
            session.Allies[1].SetStunned(true); session.Allies[2].ApplyDamage(99);
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, false), Is.False);
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Enemies, 1, false), Is.False);
            Assert.That(session.BpRuntime.RoundCycleCount, Is.Zero);
            GainCycle(1); Assert.That(Balance(session.Allies[0]), Is.Zero);
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Allies, 1, true), Is.False);
            GainCycle(2); Assert.That(Balance(session.Allies[0]), Is.EqualTo(1));
            Assert.That(Balance(session.Allies[1]), Is.Zero); Assert.That(Balance(session.Allies[2]), Is.Zero);
            for (int cycle = 3; cycle <= 8; cycle++) GainCycle(cycle);
            Assert.That(session.BpRuntime.RoundCycleCount, Is.EqualTo(8));
            Assert.That(Balance(session.Allies[0]), Is.EqualTo(3));
            Assert.That(Stage(session.Allies[0]), Is.Zero, "Gaining BP never charges Ultimate.");
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void Investment_ApprovesOnlyZeroThroughThree_AndConsumesExactCardOnce(int investment, bool accepted)
        {
            Start(1); for (int i = 1; i <= 6; i++) GainCycle(i);
            var session = _entry.ActiveSession; var actor = session.Allies[0]; session.TryGetHand(actor, out var hand);
            var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            int mp = session.GetCombatState(actor).CurrentMp;
            var attack = new CombatAttackDeclaration(actor, session.Enemies[0], card.Skill, card.RuntimeId, investment);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.EqualTo(accepted));
            Assert.That(Balance(actor), Is.EqualTo(accepted ? 3 - investment : 3));
            Assert.That(Stage(actor), Is.EqualTo(accepted ? investment : 0));
            Assert.That(session.GetCombatState(actor).CurrentMp, Is.EqualTo(mp - (accepted ? 1 : 0)));
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(accepted ? 1 : 0));
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(accepted && investment > 0 ? 1 : 0));
            if (accepted)
            {
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
                Assert.That(Stage(actor), Is.EqualTo(investment));
                foreach (var receipt in session.BpRuntime.ApprovedSpends)
                    Assert.That(session.UltimateRuntime.TryAcceptApprovedSpend(receipt), Is.False);
            }
        }

        [Test]
        public void InvalidSupportAndYield_DoNotCreateBpCyclesSpendReceiptsOrCharge()
        {
            Start(2); var session = _entry.ActiveSession; var actor = session.Allies[0];
            ((FinalCombatEnemyCommandController)Get(_entry, "_finalEnemyCommandController")).Unbind();
            session.TryGetHand(actor, out var hand); var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            int mp = session.GetCombatState(actor).CurrentMp;
            // Existing Utility/SingleAlly data cannot be smuggled through a hostile attack declaration.
            // This does not introduce a new support command or a support-cycle policy.
            _normal.tag = SkillTag.Utility; _normal.targeting = TargetingRule.SingleAlly;
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(actor, session.Enemies[0],
                card.Skill, card.RuntimeId), session.ExchangeState.Version), Is.False);
            Assert.That(_flow.YieldStandoffAttackAuthority(Side.Allies, session.ExchangeState.Version), Is.True);
            Assert.That(_flow.YieldStandoffAttackAuthority(Side.Enemies, session.ExchangeState.Version), Is.True);
            _flow.Tick(0f, 10f); // Neither a frame nor unscaled elapsed time is an attack cycle.
            Assert.That(session.BpRuntime.RoundCycleCount, Is.Zero);
            Assert.That(Balance(actor), Is.Zero); Assert.That(Stage(actor), Is.Zero);
            Assert.That(session.BpRuntime.ApprovedSpends, Is.Empty);
            Assert.That(session.GetCombatState(actor).CurrentMp, Is.EqualTo(mp));
            AssertHand(hand, 2, 3); Assert.That(hand.RefillCount, Is.Zero);
        }

        [Test]
        public void Ultimate_ChargesAtCumulativeOneTwoThree_AndDedicatedCommandResetsOnce()
        {
            Start(2); for (int i = 1; i <= 6; i++) GainCycle(i);
            var session = _entry.ActiveSession; var actor = session.Allies[0]; var target = session.Enemies[0];
            for (int stage = 1; stage <= 3; stage++)
            {
                session.GetCombatState(actor).RestoreMp(1); // Isolate the approved gauge contract from fixture MP costs.
                session.TryGetHand(actor, out var hand);
                var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
                var attack = new CombatAttackDeclaration(actor, target, card.Skill, card.RuntimeId, 1);
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.True);
                Assert.That(Stage(actor), Is.EqualTo(stage)); Assert.That(Stage(session.Allies[1]), Is.Zero);
                var receipt = session.BpRuntime.ApprovedSpends.Last();
                Assert.That(session.UltimateRuntime.TryAcceptApprovedSpend(receipt), Is.False);
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
                if (stage < 3) Assert.That(_flow.CanExecuteUltimate(_flow.PendingDecisionRequest, actor, target), Is.False);
                Assert.That(session.TryRecordCompletedAttackCycle(Side.Allies, stage, true), Is.True);
                Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, stage, true), Is.True);
            }
            session.GetCombatState(actor).RestoreMp(1);
            var request = _flow.PendingDecisionRequest; int hp = target.HP;
            Assert.That(_flow.ExecuteUltimate(request, actor, target), Is.True);
            Assert.That(Stage(actor), Is.Zero);
            Assert.That(target.HP, Is.EqualTo(hp - 1), "The effect and MP cost are transient fixtures, not approved content.");
            Assert.That(_flow.ExecuteUltimate(request, actor, target), Is.False);
            Assert.That(target.HP, Is.EqualTo(hp - 1));
        }

        [TestCase("KO")]
        [TestCase("Stun")]
        [TestCase("Panic")]
        public void RestrictedActor_CannotSpendOrChargeUltimate(string restriction)
        {
            Start(2); for (int i = 1; i <= 6; i++) GainCycle(i);
            var session = _entry.ActiveSession; var actor = session.Allies[0]; var state = session.GetCombatState(actor);
            if (restriction == "KO") actor.ApplyDamage(99);
            else if (restriction == "Stun") actor.SetStunned(true);
            else state.ApplyMentalDelta(-state.MaxMental);
            session.TryGetHand(actor, out var hand); int mp = state.CurrentMp;
            var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session,
                new CombatAttackDeclaration(actor, session.Enemies[0], card.Skill, card.RuntimeId, 1), null, out _), Is.False);
            Assert.That(Balance(actor), Is.EqualTo(3)); Assert.That(Stage(actor), Is.Zero);
            Assert.That(state.CurrentMp, Is.EqualTo(mp)); Assert.That(hand.NormalDiscard, Is.Empty);
            Assert.That(session.BpRuntime.ApprovedSpends, Is.Empty);
        }

        [Test]
        public void Handoff_KeepsExistingHandsAndDoesNotGrantCyclesOrFreeRefill()
        {
            Start(2); for (int i = 1; i <= 2; i++) GainCycle(i);
            var session = _entry.ActiveSession; var owner = session.Allies[0]; var receiver = session.Allies[1]; var enemy = session.Enemies[0];
            session.TryGetHand(receiver, out var receivingHand);
            var card = receivingHand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session,
                new CombatAttackDeclaration(receiver, enemy, card.Skill, card.RuntimeId, 1), null, out _), Is.True);
            Assert.That(session.IsHandoffEligible(receiver), Is.True);
            ((FinalCombatEnemyCommandController)Get(_entry, "_finalEnemyCommandController")).Unbind();
            _entry.GetComponent<CombatDirector>().UnbindFinalExchange();
            CombatApproachPresentationRequest approach = null; CombatOutcomePresentationRequest outcome = null;
            _flow.ApproachPresentationRequested += value => approach = value;
            _flow.OutcomePresentationRequested += value => outcome = value;
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(owner, enemy,
                owner.Skills.Single(skill => skill.Id.Value == 921)), session.ExchangeState.Version), Is.True);
            Assert.That(_flow.SubmitNoResponse(session.ExchangeState.Version), Is.True);
            Assert.That(approach.TryComplete(), Is.True); Assert.That(outcome.TryComplete(), Is.True);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            var ids = receivingHand.Hand.Select(value => value.RuntimeId).ToArray();
            int rounds = session.BpRuntime.RoundCycleCount;
            Assert.That(_flow.Handoff(receiver, receiver.Skills.Single(skill => skill.Id.Value == 921), true, session.ExchangeState.Version), Is.True);
            session.TryGetHand(receiver, out var after);
            Assert.That(after, Is.SameAs(receivingHand));
            Assert.That(after.Hand.Select(value => value.RuntimeId), Is.EqualTo(ids));
            Assert.That(after.RefillCount, Is.Zero); Assert.That(session.BpRuntime.RoundCycleCount, Is.EqualTo(rounds));
        }

        [Test]
        public void Recombat_UsesFreshSnapshotHandBpAndUltimate()
        {
            Start(1); var previous = _entry.ActiveSession; previous.TryGetHand(previous.Allies[0], out var hand);
            GainCycle(1); GainCycle(2);
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Invoke(_entry, "Update"); _gameFlow.EnterExploration();
            Assert.That(_entry.StartCombat(Request(1)), Is.True);
            var next = _entry.ActiveSession; next.TryGetHand(next.Allies[0], out var fresh);
            Assert.That(next.CompletionId, Is.Not.EqualTo(previous.CompletionId));
            Assert.That(fresh, Is.Not.SameAs(hand)); AssertHand(fresh, 2, 3);
            Assert.That(Balance(next.Allies[0]), Is.Zero); Assert.That(Stage(next.Allies[0]), Is.Zero);
        }

        [Test]
        public void HandBpValuesWithoutClash_StillRejectEnabledStartup()
        {
            Prepare(withClash: false);
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back.*Clash"));
            Assert.That(_entry.StartCombat(Request(1)), Is.False);
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        public void DisabledProfile_KeepsExistingCompatibilityWithoutImplicitRuntime()
        {
            Prepare(withClash: false, enabled: false);
            Assert.That(_entry.StartCombat(Request(1)), Is.True);
            Assert.That(_entry.ActiveSession.HasHandValidation, Is.False);
            Assert.That(_entry.ActiveSession.BpRuntime, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator UnsavedPlayMode_DataDrivenThreeAllyStartup_AndAbortCleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                EquippedSnapshots_DrawThreeClashTwoNormalOnce_WithIndependentHands(3);
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
                yield return null;
                Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Start(int allies, bool enemyHand = false)
        {
            Prepare(enemyHand: enemyHand);
            Assert.That(_entry.StartCombat(Request(allies)), Is.True);
        }

        private void Prepare(bool withClash = true, bool enabled = true, bool enemyHand = false)
        {
            foreach (var owner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (owner is GameStateMachine || owner is GameFlowController || owner is CharacterSkillSaveParticipant)
                    Object.DestroyImmediate(owner.gameObject);
            Invoke(Track(new GameObject("Playtest.State")).AddComponent<GameStateMachine>(), "Awake");
            _gameFlow = Track(new GameObject("Playtest.Flow")).AddComponent<GameFlowController>(); Invoke(_gameFlow, "Awake");
            _gameFlow.EnterExploration();
            var participant = Track(new GameObject("Playtest.Skills")).AddComponent<CharacterSkillSaveParticipant>(); Invoke(participant, "Awake");
            _skills = participant.Runtime;
            _normal = Definition(921); _clash = Definition(922);
            _config = Track(Object.Instantiate(LoadConfig()));
            var data = new SerializedObject(_config); data.FindProperty("enableProductionHandBp").boolValue = enabled;
            var rules = data.FindProperty("handCardRules"); rules.arraySize = 2;
            Rule(rules.GetArrayElementAtIndex(0), _normal, CombatCardKind.Normal, 4);
            Rule(rules.GetArrayElementAtIndex(1), _clash, CombatCardKind.Clash, 6);
            data.FindProperty("ultimateSkills").arraySize = 1;
            data.FindProperty("ultimateSkills").GetArrayElementAtIndex(0).objectReferenceValue = _normal;
            if (enemyHand)
            {
                data.FindProperty("enemyHandSourceKeys").arraySize = 1;
                data.FindProperty("enemyHandSourceKeys").GetArrayElementAtIndex(0).stringValue = "playtest.fixture.hand-enemy";
            }
            if (withClash)
            {
                // Reuse B2D's explicit 3/3/3/5 fixture on this in-memory clone only.
                data.FindProperty("clashAttackerDurability").intValue = 3;
                data.FindProperty("clashResponderDurability").intValue = 3;
                data.FindProperty("clashMaxComparisons").intValue = 3;
                data.FindProperty("clashDecisionDurationSeconds").floatValue = 5f;
                data.FindProperty("clashFinalSkillPolicy").intValue = 1;
                data.FindProperty("clashTimeoutAttackPolicy").intValue = 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var host = Track(new GameObject("Playtest.Combat")); _flow = host.AddComponent<CombatFlowOrchestrator>();
            var director = host.AddComponent<CombatDirector>(); _entry = host.AddComponent<CombatEntryPoint>();
            Set(_entry, "director", director); Set(_entry, "flowOrchestrator", _flow); Set(_flow, "entryPoint", _entry);
            Set(_entry, "finalExchangeRuntimeConfig", _config); Set(_entry, "skillDefinitions", new[] { _normal, _clash }); Invoke(_entry, "Awake");
        }

        private CombatStartRequest Request(int allies)
        {
            var request = new CombatStartRequest(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            for (int i = 0; i < allies; i++)
            {
                string id = "playtest.fixture.hero." + i;
                foreach (var skill in new[] { _normal, _clash }) { _skills.TryAcquire(id, skill.PersistentKey); _skills.TryEquip(id, skill.PersistentKey); }
                var actor = Actor(); request.AllyFieldObjects.Add(actor); request.BindAllyCharacter(actor, id);
            }
            var enemy = Actor(); request.EnemyFieldObjects.Add(enemy);
            request.SetEnemySourceSnapshot(enemy, new EnemySourceSnapshot("playtest.fixture.hand-enemy", null));
            return request;
        }

        private SkillDefinitionSO Definition(int id)
        {
            var skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); skill.skillId = id; skill.baseStagger = 0;
            var data = new SerializedObject(skill); data.FindProperty("persistentKey").stringValue = "playtest.fixture.skill." + id;
            data.FindProperty("finalMpCost").intValue = 1;
            data.FindProperty("bpEnhancementKind").intValue = (int)CombatBpEnhancementKind.ExtraHits;
            data.FindProperty("bpEnhancementPerPoint").intValue = 1; data.ApplyModifiedPropertiesWithoutUndo(); return skill;
        }
        private GameObject Actor()
        {
            var actor = Track(new GameObject("Playtest.FixtureActor")); actor.AddComponent<CombatHpComponent>();
            var loadout = actor.AddComponent<CombatSkillLoadoutComponent>(); Set(loadout, "skillIds", new[] { 921, 922 }); return actor;
        }
        private static void Rule(SerializedProperty rule, SkillDefinitionSO skill, CombatCardKind kind, int copies)
        { rule.FindPropertyRelative("skill").objectReferenceValue = skill; rule.FindPropertyRelative("kind").intValue = (int)kind; rule.FindPropertyRelative("copies").intValue = copies; }
        private void GainCycle(int id)
        { Assert.That(_entry.ActiveSession.TryRecordCompletedBpAttackCycle(Side.Allies, id, true), Is.True); Assert.That(_entry.ActiveSession.TryRecordCompletedBpAttackCycle(Side.Enemies, id, true), Is.True); }
        private int Balance(ICombatant actor)
        { Assert.That(_entry.ActiveSession.BpRuntime.TryGetBalance(actor, out int value), Is.True); return value; }
        private int Stage(ICombatant actor)
        { Assert.That(_entry.ActiveSession.UltimateRuntime.TryGetStage(actor, out int value), Is.True); return value; }
        private static void AssertHand(CombatHandRuntime hand, int normal, int clash)
        { Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(normal)); Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(clash)); }
        private static FinalCombatRuntimeConfigSO LoadConfig()
        { var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath); Assert.That(config, Is.Not.Null); return config; }
        private static object Get(object owner, string field) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
