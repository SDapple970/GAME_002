using System;
using System.Collections.Generic;
using System.Collections;
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
    public sealed class CombatProductionHandBpStartupTests
    {
        private readonly List<Object> _created = new();
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private GameFlowController _gameFlow;
        private FinalCombatRuntimeConfigSO _config;
        private SkillDefinitionSO _skill;
        private CharacterSkillRuntime _skills;

        [SetUp]
        public void SetUp()
        {
            foreach (var owner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (owner is GameStateMachine || owner is GameFlowController || owner is CharacterSkillSaveParticipant)
                    Object.DestroyImmediate(owner.gameObject);
            var state = Track(new GameObject("B1.State")).AddComponent<GameStateMachine>(); Invoke(state, "Awake");
            _gameFlow = Track(new GameObject("B1.Flow")).AddComponent<GameFlowController>(); Invoke(_gameFlow, "Awake");
            _gameFlow.EnterExploration();
            var participant = Track(new GameObject("B1.Skills")).AddComponent<CharacterSkillSaveParticipant>(); Invoke(participant, "Awake");
            _skills = participant.Runtime;
            _skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); _skill.skillId = 21;
            var definition = new SerializedObject(_skill);
            definition.FindProperty("persistentKey").stringValue = "b1.fixture.skill";
            definition.FindProperty("finalMpCost").intValue = 1;
            definition.FindProperty("bpEnhancementKind").enumValueIndex = (int)CombatBpEnhancementKind.ExtraHits;
            definition.FindProperty("bpEnhancementPerPoint").intValue = 1;
            definition.ApplyModifiedPropertiesWithoutUndo();
            _config = Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>());
            var config = new SerializedObject(_config);
            config.FindProperty("enableProductionHandBp").boolValue = true;
            config.FindProperty("normalHandCapacity").intValue = 2;
            config.FindProperty("clashHandCapacity").intValue = 1;
            config.FindProperty("handSeed").intValue = 37;
            config.FindProperty("bpMaximum").intValue = 3;
            config.FindProperty("bpRoundCyclesPerGain").intValue = 2;
            config.FindProperty("bpMaximumInvestment").intValue = 3;
            // Authored fixture values only; Production assets remain disabled and untouched.
            config.FindProperty("clashAttackerDurability").intValue = 3;
            config.FindProperty("clashResponderDurability").intValue = 3;
            config.FindProperty("clashMaxComparisons").intValue = 3;
            config.FindProperty("clashDecisionDurationSeconds").floatValue = 5f;
            config.FindProperty("clashFinalSkillPolicy").enumValueIndex = 1;
            config.FindProperty("clashTimeoutAttackPolicy").enumValueIndex = 1;
            var enemySources = config.FindProperty("enemyHandSourceKeys"); enemySources.arraySize = 1;
            enemySources.GetArrayElementAtIndex(0).stringValue = "b1.enemy.hand";
            var rules = config.FindProperty("handCardRules"); rules.arraySize = 1;
            rules.GetArrayElementAtIndex(0).FindPropertyRelative("skill").objectReferenceValue = _skill;
            rules.GetArrayElementAtIndex(0).FindPropertyRelative("kind").enumValueIndex = (int)CombatCardKind.Normal;
            rules.GetArrayElementAtIndex(0).FindPropertyRelative("copies").intValue = 2;
            var ultimates = config.FindProperty("ultimateSkills"); ultimates.arraySize = 1;
            ultimates.GetArrayElementAtIndex(0).objectReferenceValue = _skill;
            config.ApplyModifiedPropertiesWithoutUndo();
            var host = Track(new GameObject("B1.Combat"));
            _flow = host.AddComponent<CombatFlowOrchestrator>();
            var director = host.AddComponent<CombatDirector>();
            _entry = host.AddComponent<CombatEntryPoint>();
            Set(_entry, "flowOrchestrator", _flow); Set(_entry, "director", director);
            Set(_entry, "finalExchangeRuntimeConfig", _config); Set(_entry, "skillDefinitions", new[] { _skill });
            Set(_flow, "entryPoint", _entry);
            Invoke(_entry, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_entry?.ActiveStateMachine != null) _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCase(1, StartReason.PlayerGotHit)]
        [TestCase(2, StartReason.PlayerFirstHit)]
        [TestCase(3, StartReason.PlayerGotHit)]
        public void Entry_InitializesSnapshotsBeforeFirstCommand_WithoutDebug(int count, StartReason reason)
        {
            int commands = 0;
            _flow.AttackDecisionRequested += request =>
            {
                commands++;
                Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.True);
                Assert.That(_entry.ActiveSession.BpRuntime, Is.Not.Null);
                Assert.That(_flow.IsClashSeriesValidationEnabled, Is.True);
            };
            Assert.That(_entry.StartCombat(Request(count, reason)), Is.True);
            var session = _entry.ActiveSession;
            Assert.That(session.HasProductionHandBpInitialization, Is.True);
            HashSet<long> ids = new();
            foreach (var actor in session.Allies.Concat(session.Enemies))
            {
                Assert.That(session.TryGetHand(actor, out var hand), Is.True);
                Assert.That(hand.Hand.Count, Is.EqualTo(2));
                foreach (var card in hand.DeckSnapshot) Assert.That(ids.Add(card.RuntimeId), Is.True);
                Assert.That(session.BpRuntime.TryGetBalance(actor, out int bp), Is.True); Assert.That(bp, Is.Zero);
                Assert.That(session.UltimateRuntime.TryGetStage(actor, out int stage), Is.True); Assert.That(stage, Is.Zero);
                Assert.That(session.UltimateRuntime.TryGetSkill(actor, out var ultimate), Is.True);
                Assert.That(ultimate, Is.SameAs(actor.Skills[0]));
            }
            Assert.That(commands, Is.GreaterThan(0));
        }

        [TestCase("bpMaximum")]
        [TestCase("copies")]
        [TestCase("missingIdentity")]
        [TestCase("duplicateIdentity")]
        [TestCase("missingRule")]
        [TestCase("invalidKind")]
        [TestCase("unmappedSkill")]
        [TestCase("undrawableHand")]
        public void InvalidStartup_LeavesNoSessionCommandsOrCosts(string invalid)
        {
            var request = Request(2, StartReason.PlayerFirstHit);
            if (invalid == "missingIdentity") request = Request(1, StartReason.PlayerFirstHit, bindIdentity: false);
            else if (invalid == "duplicateIdentity") request.BindAllyCharacter(request.AllyFieldObjects[1], "b1.hero.0");
            else
            {
                var config = new SerializedObject(_config);
                if (invalid == "copies") config.FindProperty("handCardRules").GetArrayElementAtIndex(0).FindPropertyRelative("copies").intValue = 0;
                else if (invalid == "undrawableHand") config.FindProperty("normalHandCapacity").intValue = 0;
                else if (invalid == "invalidKind") config.FindProperty("handCardRules").GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = 99;
                else if (invalid == "unmappedSkill")
                {
                    var other = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
                    config.FindProperty("handCardRules").GetArrayElementAtIndex(0).FindPropertyRelative("skill").objectReferenceValue = other;
                }
                else if (invalid == "missingRule") config.FindProperty("handCardRules").arraySize = 0;
                else config.FindProperty(invalid).intValue = 0;
                config.ApplyModifiedPropertiesWithoutUndo();
            }
            int commands = 0, starts = 0;
            _flow.AttackDecisionRequested += _ => commands++;
            _entry.OnCombatStarted += _ => starts++;
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back"));
            Assert.That(_entry.StartCombat(request), Is.False);
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_entry.ActiveStateMachine, Is.Null);
            Assert.That(commands, Is.Zero); Assert.That(starts, Is.Zero);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(request.AllyFieldObjects.All(go => go.GetComponent<CombatHpComponent>().HP == 10), Is.True);
        }

        [Test]
        public void DisabledConfig_PreservesCompatibilityWithoutImplicitHandOrBp()
        {
            var config = new SerializedObject(_config); config.FindProperty("enableProductionHandBp").boolValue = false;
            config.FindProperty("clashAttackerDurability").intValue = 0;
            config.FindProperty("clashFinalSkillPolicy").intValue = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            Assert.That(_entry.ActiveSession.HasHandValidation, Is.False);
            Assert.That(_entry.ActiveSession.BpRuntime, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
        }

        [Test]
        public void DuplicateStartAndReinitialization_DoNotReplaceSession_AndNextStartIsFresh()
        {
            var request = Request(1, StartReason.PlayerFirstHit);
            Assert.That(_entry.StartCombat(request), Is.True);
            var first = _entry.ActiveSession; first.TryGetHand(first.Allies[0], out var oldHand);
            Assert.That(_entry.StartCombat(request), Is.False);
            Assert.That(_entry.ActiveSession, Is.SameAs(first));
            var initialize = _entry.GetType().GetMethod("InitializeProductionHandBp", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Throws<TargetInvocationException>(() => initialize.Invoke(_entry, new object[] { first, request }));
            first.TryGetHand(first.Allies[0], out var sameHand); Assert.That(sameHand, Is.SameAs(oldHand));
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
            Invoke(_entry, "Update"); // EditMode does not run the frame that publishes canonical completion.
            Assert.That(_entry.ActiveSession, Is.Null);
            _gameFlow.EnterExploration();
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerGotHit)), Is.True);
            Assert.That(_entry.ActiveSession.CompletionId, Is.Not.EqualTo(first.CompletionId));
            _entry.ActiveSession.TryGetHand(_entry.ActiveSession.Allies[0], out var nextHand);
            Assert.That(nextHand, Is.Not.SameAs(oldHand));
        }

        [Test]
        public void ProductionMarker_PreventsDebugFixtureOverwrite()
        {
            var hand = _entry.gameObject.AddComponent<Game.Combat.Debugging.CombatHandValidationOptIn>();
            var bp = _entry.gameObject.AddComponent<Game.Combat.Debugging.CombatBpValidationOptIn>();
            var clash = _entry.gameObject.AddComponent<Game.Combat.Debugging.CombatClashSeriesValidationOptIn>();
            Set(hand, "entryPoint", _entry); Set(hand, "orchestrator", _flow); Set(hand, "enableValidation", true);
            Set(bp, "entryPoint", _entry); Set(bp, "orchestrator", _flow); Set(bp, "enableValidation", true);
            Set(clash, "enableValidation", true);
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var original = session.BpRuntime;
            Assert.That(hand.TryApplyToSession(session), Is.False); Assert.That(bp.TryApplyToSession(session), Is.False);
            Assert.That(session.BpRuntime, Is.SameAs(original));
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.True);
            Assert.That(_flow.TryEnableClashSeriesValidation(new CombatClashSeriesConfig(99, 99, 99),
                99f, _ => null, _ => null), Is.False);
        }

        [Test]
        public void AuthoredEnhancement_UsesApprovedBpReceiptOnce()
        {
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var actor = session.Allies[0];
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Allies, cycle, true), Is.True);
                Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Enemies, cycle, true), Is.True);
            }
            session.TryGetHand(actor, out var hand);
            var attack = new CombatAttackDeclaration(actor, session.Enemies[0], actor.Skills[0], hand.Hand[0].RuntimeId, 1);
            int mp = session.GetCombatState(actor).CurrentMp;
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.True);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(1));
            Assert.That(session.GetCombatState(actor).CurrentMp, Is.EqualTo(mp - 1));
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(1));
            session.UltimateRuntime.TryGetStage(actor, out int stage); Assert.That(stage, Is.EqualTo(1));
        }

        [Test]
        public void StartupException_DiscardsPartialRuntimeBeforeOpeningEffect()
        {
            var original = Request(1, StartReason.PlayerFirstHit);
            var opening = Track(ScriptableObject.CreateInstance<OpeningEffectSO>()); opening.inspirationDelta = -3;
            var request = new CombatStartRequest(original.Reason, Side.Allies, 10, 3, opening,
                CombatFlowMode.StandoffClashChain, _config.CreateRuntimeConfig());
            request.AllyFieldObjects.AddRange(original.AllyFieldObjects); request.EnemyFieldObjects.AddRange(original.EnemyFieldObjects);
            SkillBook book = new(); book.Register(new SoSkill(_skill));
            request.BindAllyCharacter(request.AllyFieldObjects[0], "b1.hero.0");
            request.SetAllyLoadoutSnapshot(request.AllyFieldObjects[0], new CombatSkillLoadoutSnapshot(new[] { book.Get(new SkillId(21)) }));
            var initialize = _entry.GetType().GetMethod("InitializeProductionHandBp", BindingFlags.NonPublic | BindingFlags.Instance);
            CombatSession captured = null;
            Action<CombatSession> setup = session =>
            {
                captured = session;
                initialize.Invoke(_entry, new object[] { session, request });
                Assert.That(session.HasProductionHandBpInitialization, Is.True);
                throw new InvalidOperationException("fixture startup cancellation");
            };
            var bootstrap = typeof(CombatBootstrapper).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "StartCombat" && method.GetParameters().Length == 4);
            Assert.Throws<TargetInvocationException>(() => bootstrap.Invoke(null, new object[] { request, book, new FieldCombatantFactory(book), setup }));
            Assert.That(captured.HasHandValidation, Is.False); Assert.That(captured.BpRuntime, Is.Null);
            Assert.That(captured.UltimateRuntime, Is.Null); Assert.That(captured.Inspiration.Current, Is.EqualTo(3));
            Assert.That(captured.HasProductionHandBpInitialization, Is.False);
            Assert.That(typeof(CombatSession).GetProperty("ProductionClashConfig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(captured), Is.Null);
            Assert.That(captured.TryEnableBpValidation(new CombatBpValidationConfig(3, 2, 3)), Is.False);
        }

        [Test]
        public void EnabledConfig_DoesNotChangeLegacyPlanning()
        {
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit, mode: CombatFlowMode.LegacyPlanning)), Is.True);
            Assert.That(_entry.ActiveSession.HasHandValidation, Is.False);
            Assert.That(_entry.ActiveSession.BpRuntime, Is.Null);
        }

        [UnityTest]
        public IEnumerator AuthoredStartup_InUnsavedPlayMode_PublishesReadyHandsAndCleansUpOnAbort()
        {
            TearDown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                SetUp();
                Assert.That(Application.isPlaying, Is.True);
                Assert.That(_entry.StartCombat(Request(2, StartReason.PlayerFirstHit)), Is.True);
                var session = _entry.ActiveSession;
                yield return null;
                Assert.That(session.HasProductionHandBpInitialization, Is.True);
                Assert.That(session.Allies.Count, Is.EqualTo(2));
                Assert.That(session.Allies.All(actor => session.TryGetHand(actor, out _)), Is.True);
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
                yield return null;
                Assert.That(_entry.ActiveSession, Is.Null);
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private CombatStartRequest Request(int count, StartReason reason, bool bindIdentity = true,
            CombatFlowMode mode = CombatFlowMode.StandoffClashChain)
        {
            var request = new CombatStartRequest(reason, Side.Allies, 10, 3, null, mode);
            for (int i = 0; i < count; i++)
            {
                string id = "b1.hero." + i; _skills.TryAcquire(id, _skill.PersistentKey); _skills.TryEquip(id, _skill.PersistentKey);
                var actor = Actor("B1.Ally." + i); request.AllyFieldObjects.Add(actor);
                if (bindIdentity) request.BindAllyCharacter(actor, id);
            }
            var enemy = Actor("B1.Enemy"); request.EnemyFieldObjects.Add(enemy);
            request.SetEnemySourceSnapshot(enemy, new EnemySourceSnapshot("b1.enemy.hand", null));
            return request;
        }

        [UnityTest]
        public IEnumerator ProductionClash_InUnsavedPlayMode_UsesStartupSnapshotAndCleansBinding()
        {
            TearDown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                SetUp();
                ProductionClash_UsesAuthoredDurabilityAndTimeout_RejectsOldRoundRequest();
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
                yield return null;
                Assert.That(_entry.ActiveSession, Is.Null);
                Assert.That(_flow.PendingDecisionRequest, Is.Null);
                Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
                Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.Zero);
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        [TestCase("clashAttackerDurability")]
        [TestCase("clashResponderDurability")]
        [TestCase("clashMaxComparisons")]
        [TestCase("clashDecisionDurationSeconds")]
        [TestCase("clashFinalSkillPolicy")]
        [TestCase("clashTimeoutAttackPolicy")]
        [TestCase("nanTimeout")]
        [TestCase("infiniteTimeout")]
        [TestCase("unknownPolicy")]
        public void InvalidClash_RejectsBeforeOpeningEffectsAndCommands(string invalid)
        {
            var source = Request(1, StartReason.PlayerFirstHit);
            var opening = Track(ScriptableObject.CreateInstance<OpeningEffectSO>()); opening.inspirationDelta = -3;
            var request = new CombatStartRequest(source.Reason, Side.Allies, 10, 3, opening,
                CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.AddRange(source.AllyFieldObjects);
            request.EnemyFieldObjects.AddRange(source.EnemyFieldObjects);
            request.BindAllyCharacter(source.AllyFieldObjects[0], "b1.hero.0");
            var config = new SerializedObject(_config);
            if (invalid == "nanTimeout" || invalid == "infiniteTimeout")
                config.FindProperty("clashDecisionDurationSeconds").floatValue = invalid == "nanTimeout" ? float.NaN : float.PositiveInfinity;
            else if (invalid == "unknownPolicy") config.FindProperty("clashFinalSkillPolicy").intValue = 77;
            else if (invalid == "clashDecisionDurationSeconds") config.FindProperty(invalid).floatValue = 0f;
            else config.FindProperty(invalid).intValue = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            int commands = 0, started = 0;
            _flow.AttackDecisionRequested += _ => commands++;
            _entry.OnCombatStarted += _ => started++;
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back.*Clash"));
            Assert.That(_entry.StartCombat(request), Is.False);
            Assert.That(_entry.ActiveSession, Is.Null);
            Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
            Assert.That(commands, Is.Zero); Assert.That(started, Is.Zero);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(request.AllyFieldObjects[0].GetComponent<CombatHpComponent>().HP, Is.EqualTo(10));
        }

        [Test]
        public void EnemyHand_IsPerAuthoredSource_OrdinaryEnemyNeedsNoCardRule()
        {
            var otherSkill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); otherSkill.skillId = 22;
            var data = new SerializedObject(otherSkill); data.FindProperty("persistentKey").stringValue = "b2d.enemy.skill";
            data.FindProperty("finalMpCost").intValue = 1; data.ApplyModifiedPropertiesWithoutUndo();
            Set(_entry, "skillDefinitions", new[] { _skill, otherSkill }); Invoke(_entry, "Awake");
            var request = Request(1, StartReason.PlayerFirstHit);
            var ordinary = Actor("B2D.OrdinaryEnemy"); Set(ordinary.GetComponent<CombatSkillLoadoutComponent>(), "skillIds", new[] { 22 });
            request.EnemyFieldObjects.Add(ordinary);
            Assert.That(_entry.StartCombat(request), Is.True);
            var session = _entry.ActiveSession;
            Assert.That(session.TryGetHand(session.Enemies[0], out var hand), Is.True);
            Assert.That(session.TryGetHand(session.Enemies[1], out _), Is.False);
            var policy = new ConditionalFinalCombatEnemyDecisionPolicy();
            Assert.That(policy.TryCreateAttack(session, session.Enemies[0], out var cardAttack), Is.True);
            Assert.That(hand.TryFindInHand(cardAttack.Skill, CombatCardKind.Normal, null, out _), Is.True);
            Assert.That(policy.TryCreateAttack(session, session.Enemies[1], out var ordinaryAttack), Is.True);
            Assert.That(ordinaryAttack.Skill.Id.Value, Is.EqualTo(22));
            Assert.That(session.BpRuntime.TryGetBalance(session.Enemies[1], out int balance), Is.True);
            Assert.That(balance, Is.Zero);
        }

        [Test]
        public void EmptyEnemyOptIn_DefaultUsesAuthoredSkills_AndDoesNotReadPlayerHand()
        {
            var config = new SerializedObject(_config); config.FindProperty("enemyHandSourceKeys").arraySize = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var enemy = session.Enemies[0];
            Assert.That(session.TryGetHand(enemy, out _), Is.False);
            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(session, enemy, out var attack), Is.True);
            Assert.That(attack.Skill, Is.SameAs(enemy.Skills[0]));
            Assert.That(session.TryGetHand(session.Allies[0], out _), Is.True);
        }

        [Test]
        public void EmptyEnemyHand_YieldsToPlayer_WithoutFabricatingCardsOrSpendingBp()
        {
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var enemy = session.Enemies[0];
            session.TryGetHand(enemy, out var hand);
            foreach (var card in hand.Hand)
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session,
                    new CombatAttackDeclaration(enemy, session.Allies[0], card.Skill, card.RuntimeId), null, out _), Is.True);
            Assert.That(hand.Hand, Is.Empty);
            session.GetCombatState(enemy).SetMp(2); // Isolate card exhaustion from MP exhaustion.
            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(session, enemy, out _), Is.False);
            Assert.That(_flow.YieldStandoffAttackAuthority(Side.Allies, session.ExchangeState.Version), Is.True);
            Assert.That(_flow.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
            Assert.That(hand.Hand, Is.Empty);
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(2));
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.Zero);
            Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void StaleCommandAndUnaffordableInvestment_LeaveCardMpAndBpUntouched()
        {
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var actor = session.Allies[0];
            session.TryGetHand(actor, out var hand); var card = hand.Hand[0];
            int mp = session.GetCombatState(actor).CurrentMp;
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(actor, session.Enemies[0],
                card.Skill, card.RuntimeId), session.ExchangeState.Version - 1), Is.False);
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(actor, session.Enemies[0],
                card.Skill, card.RuntimeId, 1), session.ExchangeState.Version), Is.False);
            Assert.That(session.GetCombatState(actor).CurrentMp, Is.EqualTo(mp));
            Assert.That(hand.Hand.Count, Is.EqualTo(2));
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.Zero);
        }

        [Test]
        public void HandEnemy_RejectsUnavailableCardAndMp_WithoutConsumingAnything()
        {
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; var enemy = session.Enemies[0];
            session.TryGetHand(enemy, out var hand);
            var attack = new CombatAttackDeclaration(enemy, session.Allies[0], enemy.Skills[0], long.MaxValue, 0);
            int mp = session.GetCombatState(enemy).CurrentMp;
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(mp));
            Assert.That(session.GetCombatState(enemy).TrySpendMp(mp), Is.True);
            Assert.That(new ConditionalFinalCombatEnemyDecisionPolicy().TryCreateAttack(session, enemy, out _), Is.False);
            Assert.That(hand.NormalDiscard.Count, Is.Zero);
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.Zero);
        }

        [Test]
        public void DualPlacementRule_IsRejected_BeforeAnyRuntimeOrCommand()
        {
            var config = new SerializedObject(_config); var rules = config.FindProperty("handCardRules");
            rules.arraySize = 2;
            rules.GetArrayElementAtIndex(1).FindPropertyRelative("skill").objectReferenceValue = _skill;
            rules.GetArrayElementAtIndex(1).FindPropertyRelative("kind").intValue = (int)CombatCardKind.Clash;
            rules.GetArrayElementAtIndex(1).FindPropertyRelative("copies").intValue = 1;
            config.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back.*duplicate"));
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.False);
            Assert.That(_entry.ActiveSession, Is.Null);
            Assert.That(_flow.PendingDecisionRequest, Is.Null);
        }

        [Test]
        public void MissingClash_AbortsCapturedSessionBeforeOpeningOrPartialRuntime()
        {
            var source = Request(1, StartReason.PlayerFirstHit);
            var opening = Track(ScriptableObject.CreateInstance<OpeningEffectSO>()); opening.inspirationDelta = -3;
            var request = new CombatStartRequest(source.Reason, Side.Allies, 10, 3, opening,
                CombatFlowMode.StandoffClashChain, _config.CreateRuntimeConfig());
            request.AllyFieldObjects.AddRange(source.AllyFieldObjects); request.EnemyFieldObjects.AddRange(source.EnemyFieldObjects);
            request.BindAllyCharacter(source.AllyFieldObjects[0], "b1.hero.0");
            SkillBook book = new(); book.Register(new SoSkill(_skill));
            request.SetAllyLoadoutSnapshot(source.AllyFieldObjects[0], new CombatSkillLoadoutSnapshot(new[] { book.Get(new SkillId(21)) }));
            var config = new SerializedObject(_config); config.FindProperty("clashMaxComparisons").intValue = 0;
            config.ApplyModifiedPropertiesWithoutUndo();
            CombatSession captured = null;
            var initialize = _entry.GetType().GetMethod("InitializeProductionHandBp", BindingFlags.NonPublic | BindingFlags.Instance);
            Action<CombatSession> setup = session => { captured = session; initialize.Invoke(_entry, new object[] { session, request }); };
            var bootstrap = typeof(CombatBootstrapper).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "StartCombat" && method.GetParameters().Length == 4);
            Assert.Throws<TargetInvocationException>(() => bootstrap.Invoke(null, new object[] { request, book, new FieldCombatantFactory(book), setup }));
            Assert.That(captured.IsCombatEnded, Is.True);
            Assert.That(captured.Inspiration.Current, Is.EqualTo(3));
            Assert.That(captured.HasHandValidation, Is.False); Assert.That(captured.BpRuntime, Is.Null);
            Assert.That(captured.UltimateRuntime, Is.Null); Assert.That(captured.HasProductionHandBpInitialization, Is.False);
            Assert.That(captured.GetCombatState(captured.Allies[0]).CurrentMp, Is.EqualTo(2));
            Assert.That(_flow.PendingDecisionRequest, Is.Null);
        }

        [Test]
        public void ProductionClash_UsesAuthoredDurabilityAndTimeout_RejectsOldRoundRequest()
        {
            var clashSkill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); clashSkill.skillId = 22;
            var skillData = new SerializedObject(clashSkill);
            skillData.FindProperty("persistentKey").stringValue = "b2d.clash";
            skillData.FindProperty("finalMpCost").intValue = 0;
            skillData.ApplyModifiedPropertiesWithoutUndo();
            Set(_entry, "skillDefinitions", new[] { _skill, clashSkill }); Invoke(_entry, "Awake");
            _skills.TryAcquire("b1.hero.0", clashSkill.PersistentKey); _skills.TryEquip("b1.hero.0", clashSkill.PersistentKey);
            var config = new SerializedObject(_config);
            config.FindProperty("enemyHandSourceKeys").arraySize = 0;
            config.FindProperty("clashAttackerDurability").intValue = 7;
            config.FindProperty("clashResponderDurability").intValue = 8;
            config.FindProperty("clashMaxComparisons").intValue = 2;
            config.FindProperty("clashDecisionDurationSeconds").floatValue = 0.5f;
            var rules = config.FindProperty("handCardRules"); rules.arraySize = 2;
            var rule = rules.GetArrayElementAtIndex(1);
            rule.FindPropertyRelative("skill").objectReferenceValue = clashSkill;
            rule.FindPropertyRelative("kind").intValue = (int)CombatCardKind.Clash;
            rule.FindPropertyRelative("copies").intValue = 1;
            config.ApplyModifiedPropertiesWithoutUndo();
            int debugEvents = 0; _flow.ClashSeriesValidationEnabled += () => debugEvents++;
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            _entry.GetComponent<CombatDirector>().UnbindFinalExchange();
            var session = _entry.ActiveSession; var actor = session.Allies[0];
            CombatApproachPresentationRequest approach = null;
            List<CombatClashRoundPresentationRequest> rounds = new();
            _flow.ApproachPresentationRequested += value => approach = value;
            _flow.ClashRoundPresentationRequested += rounds.Add;
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(actor, session.Enemies[0],
                actor.Skills.Single(skill => skill.Id.Value == 21)), session.ExchangeState.Version), Is.True);
            Assert.That(approach, Is.Not.Null); Assert.That(approach.TryComplete(), Is.True);
            Assert.That(rounds.Count, Is.EqualTo(1));
            Assert.That(rounds[0].Result.AttackerDurabilityBefore, Is.EqualTo(7));
            Assert.That(rounds[0].Result.ResponderDurabilityBefore, Is.EqualTo(8));
            Assert.That(rounds[0].TryComplete(), Is.True);
            var old = _flow.PendingDecisionRequest;
            Assert.That(old.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack));
            Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.EqualTo(0.5f));
            _flow.Tick(0f, 0.6f);
            Assert.That(rounds.Count, Is.EqualTo(2));
            Assert.That(_flow.SubmitClashSeriesSkill(old, actor.Skills.Single(skill => skill.Id.Value == 22)), Is.False);
            Assert.That(debugEvents, Is.Zero);
            Assert.That(rounds[1].TryComplete(), Is.True);
            Assert.That(session.ExchangeState.ClashSeries == null || session.ExchangeState.ClashSeries.IsComplete, Is.True);
        }

        [TestCase(CombatCardKind.Normal)]
        [TestCase(CombatCardKind.Clash)]
        public void FilmOwnership_DoesNotDetermineCardKind_OrPersistentIdentity(CombatCardKind kind)
        {
            var skill = new SerializedObject(_skill); skill.FindProperty("ownershipCategory").intValue = (int)SkillOwnershipCategory.Film;
            skill.ApplyModifiedPropertiesWithoutUndo();
            var config = new SerializedObject(_config);
            config.FindProperty("handCardRules").GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = (int)kind;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession;
            session.TryGetHand(session.Allies[0], out var hand);
            Assert.That(hand.DeckSnapshot.All(card => card.Kind == kind), Is.True);
            Assert.That(hand.DeckSnapshot.Select(card => card.RuntimeId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(hand.DeckSnapshot.All(card => ((SoSkill)card.Skill).Definition.PersistentKey == _skill.PersistentKey), Is.True);
            Assert.That(_skill.OwnershipCategory, Is.EqualTo(SkillOwnershipCategory.Film));
        }

        [TestCase(CombatCardKind.Normal)]
        [TestCase(CombatCardKind.Clash)]
        public void UnlockedUnique_UsesExplicitSingleCardKind_WithoutChangingOwnership(CombatCardKind kind)
        {
            var unique = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); unique.skillId = 22;
            var skillData = new SerializedObject(unique);
            skillData.FindProperty("persistentKey").stringValue = "b2d.unique";
            skillData.FindProperty("ownershipCategory").intValue = (int)SkillOwnershipCategory.Unique;
            skillData.FindProperty("uniqueOwnerCharacterId").stringValue = "b1.hero.0";
            skillData.ApplyModifiedPropertiesWithoutUndo();
            var definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            var characterData = new SerializedObject(definition);
            characterData.FindProperty("characterId").stringValue = "b1.hero.0";
            characterData.FindProperty("startingLevel").intValue = 5;
            characterData.FindProperty("maximumLevel").intValue = 99;
            var unlocks = characterData.FindProperty("uniqueSkillUnlocks"); unlocks.arraySize = 1;
            var unlock = unlocks.GetArrayElementAtIndex(0);
            unlock.FindPropertyRelative("enabled").boolValue = true;
            unlock.FindPropertyRelative("persistentSkillKey").stringValue = unique.PersistentKey;
            unlock.FindPropertyRelative("condition").intValue = 1;
            unlock.FindPropertyRelative("unlockLevel").intValue = 5;
            characterData.ApplyModifiedPropertiesWithoutUndo();
            var progression = Track(new GameObject("B2D.Progression")).AddComponent<CharacterProgressionService>();
            Invoke(progression, "Awake"); progression.ConfigureForTests(null, definition);
            Set(_entry, "skillDefinitions", new[] { _skill, unique }); Invoke(_entry, "Awake");
            var config = new SerializedObject(_config); var rules = config.FindProperty("handCardRules"); rules.arraySize = 2;
            var rule = rules.GetArrayElementAtIndex(1);
            rule.FindPropertyRelative("skill").objectReferenceValue = unique;
            rule.FindPropertyRelative("kind").intValue = (int)kind;
            rule.FindPropertyRelative("copies").intValue = 1;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_entry.StartCombat(Request(1, StartReason.PlayerFirstHit)), Is.True);
            var session = _entry.ActiveSession; session.TryGetHand(session.Allies[0], out var hand);
            Assert.That(hand.DeckSnapshot.Single(card => card.Skill.Id.Value == 22).Kind, Is.EqualTo(kind));
            Assert.That(hand.DeckSnapshot.Select(card => card.RuntimeId).Distinct().Count(), Is.EqualTo(hand.DeckSnapshot.Count));
            Assert.That(session.Enemies[0].Skills.Any(skill => skill.Id.Value == 22), Is.False);
            Assert.That(unique.OwnershipCategory, Is.EqualTo(SkillOwnershipCategory.Unique));
            Assert.That(unique.UniqueOwnerCharacterId, Is.EqualTo("b1.hero.0"));
            Assert.That(_skills.GetSharedFilms(), Does.Not.Contain(unique.PersistentKey));
        }
        private GameObject Actor(string name)
        {
            var actor = Track(new GameObject(name)); actor.AddComponent<CombatHpComponent>();
            var loadout = actor.AddComponent<CombatSkillLoadoutComponent>(); Set(loadout, "skillIds", new[] { 21 }); return actor;
        }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
