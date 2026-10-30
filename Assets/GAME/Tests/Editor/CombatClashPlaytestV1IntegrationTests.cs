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
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Reward;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    /// <summary>Production entry and execution with isolated data and deterministic presentation gates.</summary>
    public sealed class CombatClashPlaytestV1IntegrationTests
    {
        public const string ConfigPath = "Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/FinalCombatRuntimeConfig_ClashPlaytestV1.asset";
        private readonly List<Object> _created = new();
        private readonly List<CombatClashRoundPresentationRequest> _rounds = new();
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private CombatDirector _director;
        private GameFlowController _gameFlow;
        private CombatStartRequest _request;
        private FinalCombatRuntimeConfigSO _config;
        private SkillDefinitionSO[] _definitions;
        private CombatStatusDefinitionSO _status;
        private CombatApproachPresentationRequest _approach;
        private CombatOutcomePresentationRequest _outcome;
        private int _commands;

        // Adds only a separate test profile. B3A and all Production assets are read-only.
        public static void CreateClashPlaytestAssetIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(CombatPlaytestV1ConfigTests.ConfigPath);
            if (source == null) throw new InvalidOperationException("Create the B3A Playtest profile first.");
            var config = Object.Instantiate(source);
            config.name = "FinalCombatRuntimeConfig_ClashPlaytestV1";
            var data = new SerializedObject(config);
            // Existing B2D/B3A fixture; these are not Production balance values.
            data.FindProperty("clashAttackerDurability").intValue = 3;
            data.FindProperty("clashResponderDurability").intValue = 3;
            data.FindProperty("clashMaxComparisons").intValue = 3;
            data.FindProperty("clashDecisionDurationSeconds").floatValue = 5f;
            data.FindProperty("clashFinalSkillPolicy").intValue = 1;
            data.FindProperty("clashTimeoutAttackPolicy").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SetLabels(config, new[] { "TestOnly", "PlaytestV1", "ClashFixture", "ContentPending" });
            AssetDatabase.SaveAssetIfDirty(config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_entry?.ActiveStateMachine != null) _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear(); _rounds.Clear(); _approach = null; _outcome = null; _commands = 0;
        }

        [Test]
        public void SeparateProfile_PreservesB3A_AndKeepsSavedActivationAndContentDisabled()
        {
            var source = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(CombatPlaytestV1ConfigTests.ConfigPath);
            var integrated = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(integrated, Is.Not.Null); Assert.That(integrated, Is.Not.SameAs(source));
            Assert.That(source.ClashAttackerDurability, Is.Zero); Assert.That(source.ClashMaxComparisons, Is.Zero);
            Assert.That(integrated.EnableProductionHandBp, Is.False); Assert.That(source.EnableProductionHandBp, Is.False);
            Assert.That(integrated.NormalHandCapacity, Is.EqualTo(2)); Assert.That(integrated.ClashHandCapacity, Is.EqualTo(3));
            Assert.That(integrated.BpMaximum, Is.EqualTo(3)); Assert.That(integrated.BpRoundCyclesPerGain, Is.EqualTo(2));
            Assert.That(integrated.BpMaximumInvestment, Is.EqualTo(3));
            Assert.That(integrated.ClashAttackerDurability, Is.EqualTo(3)); Assert.That(integrated.ClashResponderDurability, Is.EqualTo(3));
            Assert.That(integrated.ClashMaxComparisons, Is.EqualTo(3)); Assert.That(integrated.ClashDecisionDurationSeconds, Is.EqualTo(5f));
            Assert.That(integrated.HandCardRules, Is.Empty); Assert.That(integrated.UltimateSkills, Is.Empty);
            Assert.That(AssetDatabase.GetLabels(integrated), Does.Contain("TestOnly"));
        }

        [TestCase(1, false, false)]
        [TestCase(2, true, true)]
        [TestCase(3, true, false)]
        public void ThreeRounds_UseProductionStartup_ExactCosts_AndOneFinalEffect(int allies, bool enemyHand, bool invest)
        {
            Prepare(allies, enemyHand);
            SetAllyPower(invest ? 2 : 3); // +1 configured BP power yields a difference of one in every invested round.
            Start();
            var session = _entry.ActiveSession; var actor = session.Allies[0]; var enemy = session.Enemies[0];
            Assert.That(session.TryGetHand(enemy, out _), Is.EqualTo(enemyHand));
            if (invest) PrimeBp();
            Begin(invest ? 1 : 0);
            var series = session.ExchangeState.ClashSeries;
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(_rounds.Count, Is.EqualTo(round));
                Assert.That(series.ComparisonCount, Is.EqualTo(round));
                Assert.That(_rounds.Last().Result.AttackerPower, Is.EqualTo(3));
                Assert.That(_rounds.Last().Result.ResponderPower, Is.EqualTo(2));
                Assert.That(series.AttackMpSpent, Is.EqualTo(round)); Assert.That(series.ResponseMpSpent, Is.EqualTo(round));
                Assert.That(session.GetCombatState(actor).CurrentMp, Is.EqualTo(3 - round));
                Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(3 - round));
                Assert.That(enemy.HP, Is.EqualTo(10)); Assert.That(session.GetCombatState(enemy).CurrentMental, Is.EqualTo(100));
                Assert.That(session.GetCombatState(enemy).HasStatus(_status.StatusId), Is.False);
                Assert.That(session.GetCombatState(enemy).CurrentPosture, Is.Zero, "Durability comparisons do not apply posture.");
                Assert.That(Stage(actor), Is.EqualTo(invest ? round : 0));
                Assert.That(Balance(actor), Is.EqualTo(invest ? 3 - round : 0));
                Assert.That(_rounds.Last().TryComplete(), Is.True);
                Assert.That(_rounds.Last().TryComplete(), Is.False);
                if (round < 3) NextRound(invest ? 1 : 0);
            }
            Assert.That(series.ResponderDurability, Is.Zero); Assert.That(series.AttackerDurability, Is.EqualTo(3));
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(enemy.HP, Is.EqualTo(9)); Assert.That(session.GetCombatState(enemy).CurrentMental, Is.EqualTo(85));
            Assert.That(session.GetCombatState(enemy).TryGetStatus(_status.StatusId, out var applied), Is.True);
            Assert.That(applied.StackCount, Is.EqualTo(1));
            Assert.That(session.GetCombatState(enemy).CurrentPosture, Is.GreaterThan(0));
            session.TryGetHand(actor, out var hand);
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(1)); Assert.That(hand.ClashDiscard.Count, Is.EqualTo(2));
            if (enemyHand) { session.TryGetHand(enemy, out var eh); Assert.That(eh.ClashDiscard.Count, Is.EqualTo(3)); }
            Assert.That(session.BpRuntime.ApprovedSpends.Count, Is.EqualTo(invest ? 3 : 0));
            foreach (var receipt in session.BpRuntime.ApprovedSpends) Assert.That(session.UltimateRuntime.TryAcceptApprovedSpend(receipt), Is.False);
            Assert.That(_entry.ActiveStateMachine.TryExecutePreparedSkill(session.ExchangeState.Version), Is.False);
            Assert.That(_outcome.TryComplete(), Is.True); Assert.That(_outcome.TryComplete(), Is.False);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(enemy.HP, Is.EqualTo(9)); Assert.That(applied.StackCount, Is.EqualTo(1));
            Assert.That(session.BpRuntime.RoundCycleCount, Is.EqualTo(invest ? 6 : 0), "Three Clash rounds never create BP RoundCycles.");
        }

        [TestCase(5, CombatClashOutcome.AttackerWin)]
        [TestCase(6, CombatClashOutcome.ResponderWin)]
        public void EqualPower_BothReachZero_UsesSpeedThenAttackOwner(int enemySpeed, CombatClashOutcome expected)
        {
            Prepare(); SetAllyPower(2); _definitions[2].speed = enemySpeed; _definitions[3].speed = enemySpeed;
            Start(); Begin(); var session = _entry.ActiveSession; var series = session.ExchangeState.ClashSeries;
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(_rounds.Last().Result.AttackerDurabilityAfter, Is.EqualTo(3 - round));
                Assert.That(_rounds.Last().Result.ResponderDurabilityAfter, Is.EqualTo(3 - round));
                _rounds.Last().TryComplete(); if (round < 3) NextRound();
            }
            Assert.That(series.FinalOutcome, Is.EqualTo(expected));
            Assert.That(session.Allies[0].HP, Is.EqualTo(expected == CombatClashOutcome.ResponderWin ? 9 : 10));
            Assert.That(session.Enemies[0].HP, Is.EqualTo(expected == CombatClashOutcome.AttackerWin ? 9 : 10));
        }

        [Test]
        public void ComparisonCap_ReachesSafeDrawWithoutDamageEffectsOrBpCycles()
        {
            Prepare(); SetAllyPower(2);
            // Existing B2D 7/8/2 fixture leaves both sides above zero at the comparison cap.
            Set(_config, "clashAttackerDurability", 7); Set(_config, "clashResponderDurability", 8); Set(_config, "clashMaxComparisons", 2);
            Start(); Begin(); var session = _entry.ActiveSession; var series = session.ExchangeState.ClashSeries;
            _rounds.Last().TryComplete(); NextRound(); _rounds.Last().TryComplete();
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Tie));
            Assert.That(series.AttackerDurability, Is.EqualTo(5)); Assert.That(series.ResponderDurability, Is.EqualTo(6));
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(session.Allies[0].HP, Is.EqualTo(10)); Assert.That(session.Enemies[0].HP, Is.EqualTo(10));
            Assert.That(_outcome, Is.Null); Assert.That(session.BpRuntime.RoundCycleCount, Is.Zero);
        }

        [Test]
        public void ResponderDurabilityAdvantage_DeterminesWinnerWithoutEarlyDamage()
        {
            Prepare(); SetAllyPower(1); Start(); Begin(); var session = _entry.ActiveSession;
            var series = session.ExchangeState.ClashSeries;
            for (int round = 1; round <= 3; round++) { Assert.That(session.Allies[0].HP, Is.EqualTo(10)); _rounds.Last().TryComplete(); if (round < 3) NextRound(); }
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.ResponderWin));
            Assert.That(series.AttackerDurability, Is.Zero); Assert.That(series.ResponderDurability, Is.EqualTo(3));
            Assert.That(session.Allies[0].HP, Is.EqualTo(9));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoResponse_UsesExistingUnopposedOutcomeOnce(bool duringSeries)
        {
            Prepare(); Start(); var session = _entry.ActiveSession;
            ((FinalCombatEnemyCommandController)Get(_entry, "_finalEnemyCommandController")).Unbind();
            if (!duringSeries)
            {
                Declare(); Assert.That(_flow.SubmitNoResponse(session.ExchangeState.Version), Is.True);
                Assert.That(_approach.TryComplete(), Is.True);
            }
            else
            {
                Declare(); Assert.That(_flow.SubmitResponse(new CombatResponseDeclaration(session.Enemies[0], session.Enemies[0].Skills[1]), session.ExchangeState.Version), Is.True);
                _approach.TryComplete(); _rounds.Last().TryComplete(); NextRound(expectEnemyAutoResponse: false);
                Assert.That(_flow.SubmitClashSeriesNoResponse(_flow.PendingDecisionRequest), Is.True);
            }
            Assert.That(session.Enemies[0].HP, Is.EqualTo(9));
            Assert.That(_outcome.TryComplete(), Is.True); Assert.That(_outcome.TryComplete(), Is.False);
            Assert.That(session.Enemies[0].HP, Is.EqualTo(9));
        }

        [Test]
        public void ChainHandoff_PreservesReceiverRuntimeAndDoesNotRefill()
        {
            Prepare(2); Start(); PrimeBp(); var session = _entry.ActiveSession; var receiver = session.Allies[1];
            session.TryGetHand(receiver, out var hand); var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, new CombatAttackDeclaration(receiver, session.Enemies[0], card.Skill, card.RuntimeId, 1), null, out _), Is.True);
            Begin(); FinishThreeRounds(); Assert.That(_outcome.TryComplete(), Is.True);
            long[] ids = hand.Hand.Select(value => value.RuntimeId).ToArray();
            Assert.That(_flow.Handoff(receiver, receiver.Skills[0], true, session.ExchangeState.Version), Is.True);
            session.TryGetHand(receiver, out var after); Assert.That(after, Is.SameAs(hand));
            Assert.That(after.Hand.Select(value => value.RuntimeId), Is.EqualTo(ids)); Assert.That(after.RefillCount, Is.Zero);
            Assert.That(session.BpRuntime.RoundCycleCount, Is.EqualTo(6));
        }

        [Test]
        public void AbortAtRoundDecision_RejectsDelayedCallbacks_AndNextCombatStartsFresh()
        {
            Prepare(); Start(); Begin(); var first = _entry.ActiveSession;
            _rounds.Last().TryComplete(); var old = _flow.PendingDecisionRequest; var presentation = _rounds.Last();
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Invoke(_entry, "Update");
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False); Assert.That(_flow.ClashSeriesDecisionTimeRemaining, Is.Zero);
            Assert.That(_flow.SubmitClashSeriesSkill(old, first.Allies[0].Skills[1]), Is.False); Assert.That(presentation.TryComplete(), Is.False);
            _gameFlow.EnterExploration(); Start();
            var next = _entry.ActiveSession; Assert.That(next.CompletionId, Is.Not.EqualTo(first.CompletionId));
            Assert.That(Balance(next.Allies[0]), Is.Zero); Assert.That(Stage(next.Allies[0]), Is.Zero);
            next.TryGetHand(next.Allies[0], out var hand); Assert.That(hand.NormalDiscard, Is.Empty); Assert.That(hand.ClashDiscard, Is.Empty);
            Assert.That(next.GetCombatState(next.Allies[0]).CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void MissingClashBeforeStartup_RejectsCommandsAndRollsBack_ThenAllowsValidRetry()
        {
            Prepare(); Assert.That(_flow.SubmitAttackDeclaration(null, 0), Is.False);
            Assert.That(_flow.SubmitClashSeriesSkill(null, null), Is.False);
            Set(_config, "clashMaxComparisons", 0);
            LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back.*Clash"));
            Assert.That(_entry.StartCombat(_request), Is.False);
            Assert.That(_commands, Is.Zero); Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Set(_config, "clashMaxComparisons", 3); Start();
        }

        [Test]
        public void TerminalFromFinalClash_PublishesCompletionAndParticipantRewardsOnce()
        {
            Prepare(2); Start(); var session = _entry.ActiveSession;
            var progression = Component<CharacterProgressionService>();
            var definitions = new List<CharacterProgressionDefinitionSO>();
            for (int i = 0; i < 2; i++)
            {
                var definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
                Set(definition, "characterId", "b3b.hero." + i); Set(definition, "startingLevel", 1);
                Set(definition, "maximumLevel", 3); Set(definition, "experienceRequiredByLevel", new[] { 1000, 1000 }); definitions.Add(definition);
            }
            progression.ConfigureForTests(null, definitions.ToArray());
            var wallet = Component<CurrencyWallet>(); var inventory = Component<InventoryService>(); var rewards = Component<RewardService>();
            Set(rewards, "currencyWallet", wallet); Set(rewards, "inventoryService", inventory); Set(rewards, "characterProgressionService", progression);
            CombatResult completion = null; int completions = 0;
            _entry.OnCombatEnded += result => { completions++; completion = result; };
            session.Enemies[0].ApplyDamage(9); Begin(); FinishThreeRounds();
            Assert.That(session.Enemies[0].HP, Is.Zero);
            if (_outcome != null) _outcome.TryComplete();
            Invoke(_entry, "Update"); Invoke(_entry, "Update");
            Assert.That(completions, Is.EqualTo(1)); Assert.That(completion.CompletionId, Is.EqualTo(session.CompletionId));
            Assert.That(completion.EndReason, Is.EqualTo(CombatEndReason.Victory)); Assert.That(_entry.ActiveSession, Is.Null);
            var request = RewardService.CreateCombatRewardRequest(completion, null);
            Assert.That(rewards.GrantReward(request).DuplicateBlocked, Is.False);
            Assert.That(rewards.GrantReward(request).DuplicateBlocked, Is.True);
            for (int i = 0; i < 2; i++) { Assert.That(progression.TryGetState("b3b.hero." + i, out _, out int exp), Is.True); Assert.That(exp, Is.EqualTo(completion.TotalExp)); }
            Assert.That(wallet.Gold, Is.EqualTo(completion.TotalGold));
            Assert.That(_rounds.Last().TryComplete(), Is.False); Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_ProductionStartupAndDirector_RunThreeRoundsToChain()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                float fixedStep = Time.fixedDeltaTime;
                Prepare(3, manualPresentation: false); Start(); var session = _entry.ActiveSession;
                Declare(); int submitted = 0;
                // Batch mode may execute hundreds of frames before a timed presentation completes.
                float deadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < deadline && _entry.ActiveStateMachine.Phase != Phase.ChainDecision)
                {
                    var request = _flow.PendingDecisionRequest;
                    if (request?.Kind == CombatExchangeDecisionKind.ClashAttack)
                    {
                        var skill = session.Allies[0].Skills[1];
                        Assert.That(_flow.SubmitClashSeriesSkill(request, skill), Is.True); submitted++;
                    }
                    yield return null;
                }
                Assert.That(submitted, Is.EqualTo(2));
                Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
                Assert.That(session.ExchangeState.ClashSeries.ComparisonCount, Is.EqualTo(3));
                Assert.That(session.Enemies[0].HP, Is.EqualTo(9));
                Assert.That(session.GetCombatState(session.Enemies[0]).CurrentMental, Is.EqualTo(85));
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
                Assert.That(_entry.ActiveSession, Is.Null); Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Prepare(int allies = 1, bool enemyHand = true, bool manualPresentation = true)
        {
            foreach (var owner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (owner is GameStateMachine || owner is GameFlowController || owner is CharacterSkillSaveParticipant ||
                    owner is CharacterProgressionService || owner is RewardService || owner is CurrencyWallet || owner is InventoryService)
                    Object.DestroyImmediate(owner.gameObject);
            Component<GameStateMachine>(); _gameFlow = Component<GameFlowController>(); _gameFlow.EnterExploration();
            var skills = Component<CharacterSkillSaveParticipant>().Runtime;
            _config = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath)));
            Set(_config, "enableProductionHandBp", true);
            // Three decisions at fixture MP cost 1 need 3 MP. The saved profiles remain unchanged.
            Set(_config, "initialMp", 3);
            _status = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            Set(_status, "statusId", "b3b.fixture.effect"); Set(_status, "stackPolicy", CombatStatusStackPolicy.Stack); Set(_status, "maximumStacks", 5);
            _definitions = new[] { Definition(941, 3, true), Definition(942, 3, true), Definition(943, 2, false), Definition(944, 2, false) };
            var config = new SerializedObject(_config); var rules = config.FindProperty("handCardRules"); rules.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i); rule.FindPropertyRelative("skill").objectReferenceValue = _definitions[i];
                rule.FindPropertyRelative("kind").intValue = i % 2; rule.FindPropertyRelative("copies").intValue = i % 2 == 0 ? 4 : 6;
            }
            config.FindProperty("ultimateSkills").arraySize = 1; config.FindProperty("ultimateSkills").GetArrayElementAtIndex(0).objectReferenceValue = _definitions[0];
            if (enemyHand) { config.FindProperty("enemyHandSourceKeys").arraySize = 1; config.FindProperty("enemyHandSourceKeys").GetArrayElementAtIndex(0).stringValue = "b3b.hand-enemy"; }
            config.ApplyModifiedPropertiesWithoutUndo();
            var host = Track(new GameObject("B3B.Combat")); _flow = host.AddComponent<CombatFlowOrchestrator>();
            _director = host.AddComponent<CombatDirector>(); _entry = host.AddComponent<CombatEntryPoint>();
            Set(_entry, "director", _director); Set(_entry, "flowOrchestrator", _flow); Set(_flow, "entryPoint", _entry);
            Set(_entry, "finalExchangeRuntimeConfig", _config); Set(_entry, "skillDefinitions", _definitions); Invoke(_entry, "Awake");
            _request = new CombatStartRequest(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            for (int i = 0; i < allies; i++)
            {
                string id = "b3b.hero." + i;
                foreach (var definition in _definitions.Take(2)) { skills.TryAcquire(id, definition.PersistentKey); skills.TryEquip(id, definition.PersistentKey); }
                var actor = Actor(941, 942); _request.AllyFieldObjects.Add(actor); _request.BindAllyCharacter(actor, id);
            }
            var enemy = Actor(943, 944); _request.EnemyFieldObjects.Add(enemy);
            _request.SetEnemySourceSnapshot(enemy, new EnemySourceSnapshot("b3b.hand-enemy", null));
            _flow.AttackDecisionRequested += _ =>
            {
                _commands++;
                Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.True);
                Assert.That(_flow.IsClashSeriesValidationEnabled, Is.True);
            };
            bool manualPresentersAttached = false;
            _entry.OnCombatStarted += session =>
            {
                if (manualPresentation)
                {
                    _director.UnbindFinalExchange();
                    if (!manualPresentersAttached)
                    {
                        _flow.ApproachPresentationRequested += value => _approach = value;
                        _flow.ClashRoundPresentationRequested += _rounds.Add;
                        _flow.OutcomePresentationRequested += value => _outcome = value;
                        manualPresentersAttached = true;
                    }
                }
            };
        }

        private void Start()
        {
            Assert.That(_entry.StartCombat(_request), Is.True);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Standoff));
            var session = _entry.ActiveSession;
            foreach (var actor in session.Allies)
            {
                Assert.That(Balance(actor), Is.Zero); Assert.That(Stage(actor), Is.Zero);
                session.TryGetHand(actor, out var hand); Assert.That(hand.Hand.Count, Is.EqualTo(5));
            }
            Assert.That(_entry.gameObject.GetComponents<MonoBehaviour>().Any(value => value.GetType().Namespace?.Contains("Debugging") == true), Is.False);
        }
        private void Declare(int bp = 0)
        {
            var session = _entry.ActiveSession; var actor = session.Allies[0]; session.TryGetHand(actor, out var hand);
            var card = hand.Hand.First(value => value.Kind == CombatCardKind.Normal);
            Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(actor, session.Enemies[0], card.Skill, card.RuntimeId, bp), session.ExchangeState.Version), Is.True);
        }
        private void Begin(int bp = 0) { Declare(bp); Assert.That(_approach, Is.Not.Null); Assert.That(_approach.TryComplete(), Is.True); Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.Clash)); }
        private void NextRound(int bp = 0, bool expectEnemyAutoResponse = true)
        {
            var request = _flow.PendingDecisionRequest; var session = _entry.ActiveSession; var actor = session.Allies[0];
            Assert.That(request.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack)); session.TryGetHand(actor, out var hand);
            var card = hand.Hand.First(value => value.Kind == CombatCardKind.Clash);
            int count = _rounds.Count;
            Assert.That(_flow.SubmitClashSeriesSkill(request, card.Skill, card.RuntimeId, bp), Is.True);
            Assert.That(_flow.SubmitClashSeriesSkill(request, card.Skill, card.RuntimeId, bp), Is.False);
            Assert.That(_rounds.Count, Is.EqualTo(expectEnemyAutoResponse ? count + 1 : count));
        }
        private void FinishThreeRounds() { _rounds.Last().TryComplete(); NextRound(); _rounds.Last().TryComplete(); NextRound(); _rounds.Last().TryComplete(); }
        private void PrimeBp() { for (int i = 1; i <= 6; i++) { _entry.ActiveSession.TryRecordCompletedBpAttackCycle(Side.Allies, i, true); _entry.ActiveSession.TryRecordCompletedBpAttackCycle(Side.Enemies, i, true); } }
        private void SetAllyPower(int power) { Set(_definitions[0], "clashPower", power); Set(_definitions[1], "clashPower", power); }
        private SkillDefinitionSO Definition(int id, int power, bool ally)
        {
            var definition = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>()); definition.skillId = id; definition.baseStagger = 0;
            Set(definition, "persistentKey", "b3b.fixture.skill." + id); Set(definition, "finalMpCost", 1); Set(definition, "clashPower", power);
            Set(definition, "bpEnhancementKind", CombatBpEnhancementKind.ClashPower); Set(definition, "bpEnhancementPerPoint", 1);
            if (ally) Set(definition, "appliedStatusEffects", new[] { _status }); return definition;
        }
        private GameObject Actor(params int[] ids) { var go = Track(new GameObject("B3B.FixtureActor")); go.AddComponent<CombatHpComponent>(); var loadout = go.AddComponent<CombatSkillLoadoutComponent>(); Set(loadout, "skillIds", ids); return go; }
        private int Balance(ICombatant actor) { Assert.That(_entry.ActiveSession.BpRuntime.TryGetBalance(actor, out int value), Is.True); return value; }
        private int Stage(ICombatant actor) { Assert.That(_entry.ActiveSession.UltimateRuntime.TryGetStage(actor, out int value), Is.True); return value; }
        private T Component<T>() where T : MonoBehaviour { var component = Track(new GameObject("B3B." + typeof(T).Name)).AddComponent<T>(); typeof(T).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(component, null); return component; }
        private static object Get(object owner, string field) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
