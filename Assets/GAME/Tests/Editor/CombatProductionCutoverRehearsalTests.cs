using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Debugging;
using Game.Combat.Effects;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    /// <summary>Unpacks no saved assets. Only an inactive in-memory prefab copy is edited.</summary>
    public sealed class CombatProductionCutoverRehearsalTests
    {
        private const string PrefabPath = "Assets/GAME/Prefabs/CombatRuntime.prefab";
        private const string DungeonPath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string TemplatePath = "Assets/GAME/Scenes/Dungeon_Template.unity";
        private CombatClashPlaytestV1IntegrationTests _data;
        private GameObject _copy;
        private CombatEntryPoint _entry;
        private CombatFlowOrchestrator _flow;
        private CombatDirector _director;
        private CombatStartRequest _request;
        private FinalCombatRuntimeConfigSO _config;
        private CombatApproachPresentationRequest _approach;
        private CombatOutcomePresentationRequest _outcome;
        private readonly List<CombatClashRoundPresentationRequest> _rounds = new();
        private int _removedDebugCount;
        private int _commands;

        [TearDown]
        public void TearDown()
        {
            if (_entry?.ActiveStateMachine != null) { _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Invoke(_entry, "Update"); }
            if (_copy != null) Object.DestroyImmediate(_copy);
            _data?.TearDown(); _rounds.Clear(); _approach = null; _outcome = null; _removedDebugCount = 0; _commands = 0;
        }

        [Test]
        public void SourceAssets_ReadOnlyFootprint_ReportsDataAndInstanceOverrides()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var entry = source.GetComponent<CombatEntryPoint>();
            Assert.That(source.GetComponents<CombatBpValidationOptIn>().Length, Is.EqualTo(1));
            Assert.That(entry.RegisteredSkillDefinitions, Is.Empty);
            Assert.That(entry.FinalExchangeRuntimeConfig.EnableProductionHandBp, Is.False);
            Assert.That(entry.FinalExchangeRuntimeConfig.HandCardRules, Is.Empty);
            Assert.That(entry.FinalExchangeRuntimeConfig.ClashMaxComparisons, Is.Zero);
            string dungeon = File.ReadAllText(DungeonPath); string template = File.ReadAllText(TemplatePath);
            foreach (string id in new[] { "1665839071176935759", "1665839071176935761", "1665839071176935766" })
                Assert.That(dungeon, Does.Contain("addedObject: {fileID: " + id + "}"));
            Assert.That(dungeon, Does.Contain("addedObject: {fileID: 1665839071176935757}"));
            Assert.That(template, Does.Contain("addedObject: {fileID: 1665839071176935757}"));
            Assert.That(dungeon, Does.Contain("m_SourcePrefab: {fileID: 100100000, guid: 1bfc52cd235e809449d17d48571f5a38"));
            Assert.That(template, Does.Contain("m_SourcePrefab: {fileID: 100100000, guid: 1bfc52cd235e809449d17d48571f5a38"));
            TestContext.WriteLine("Pending: Production Config disabled/missing Hand-BP-Clash authoring; base registry empty (Dungeon/Template overrides differ); official Character IDs/equipped snapshots, Enemy sources/skills, copies/seed/enhancements and optional Ultimate/AllOut content remain separate from fixtures.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DebugRemoval_RehearsesBaseAndSceneAddedComponents_OnlyInMemory(bool sceneAddedStage)
        {
            Prepare(sceneAddedStage: sceneAddedStage);
            Assert.That(_removedDebugCount, Is.EqualTo(sceneAddedStage ? 4 : 1));
            Assert.That(_copy.GetComponents<MonoBehaviour>().Any(IsDebug), Is.False);
            Assert.That(AssetDatabase.Contains(_copy), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CombatBpValidationOptIn>(), Is.Not.Null);
            Assert.That(_entry.GetComponent<CombatRewardUIBinder>(), Is.Not.Null);
            Assert.That(_entry.GetComponent<CombatWorldLifecycleAdapter>(), Is.Not.Null);
            Assert.That(_entry.FlowOrchestrator, Is.SameAs(_flow));
            Assert.That(GetField(_entry, "director"), Is.SameAs(_director));
            Assert.That(GetField(_flow, "entryPoint"), Is.SameAs(_entry));
        }

        [Test]
        public void RemovingDebugAlone_WithOriginalDisabledConfig_DoesNotEnableApprovedMode()
        {
            Prepare(bindFixtureConfig: false);
            Assert.That(_entry.StartCombat(_request), Is.True, "Only the transient skill registry was supplied to isolate the disabled Config contract.");
            Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.False);
            Assert.That(_entry.ActiveSession.HasHandValidation, Is.False); Assert.That(_entry.ActiveSession.BpRuntime, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
            Assert.That(_entry.FinalExchangeRuntimeConfig, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CombatEntryPoint>().FinalExchangeRuntimeConfig));
        }

        [TestCase("config")]
        [TestCase("clash")]
        [TestCase("character")]
        [TestCase("enemy")]
        [TestCase("registry")]
        public void MissingProductionInputs_AreRejectedWithoutPublishingCommands(string missing)
        {
            Prepare();
            if (missing == "config")
            {
                Set(_entry, "finalExchangeRuntimeConfig", null);
                LogAssert.Expect(LogType.Warning, new Regex("Combat start rejected.*FinalExchange"));
            }
            else
            {
                if (missing == "clash") Set(_config, "clashMaxComparisons", 0);
                else if (missing == "character")
                {
                    var noIdentity = new CombatStartRequest(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
                    noIdentity.AllyFieldObjects.AddRange(_request.AllyFieldObjects); noIdentity.EnemyFieldObjects.AddRange(_request.EnemyFieldObjects); _request = noIdentity;
                }
                else if (missing == "enemy") Set(_request.EnemyFieldObjects[0].GetComponent<CombatSkillLoadoutComponent>(), "skillIds", new[] { int.MaxValue });
                else { Set(_entry, "skillDefinitions", Array.Empty<SkillDefinitionSO>()); Invoke(_entry, "Awake"); }
                LogAssert.Expect(LogType.Error, new Regex("Combat startup rolled back"));
            }
            Assert.That(_entry.StartCombat(_request), Is.False);
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_entry.ActiveStateMachine, Is.Null);
            Assert.That(_flow.PendingDecisionRequest, Is.Null); Assert.That(_commands, Is.Zero);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(_request.AllyFieldObjects.All(actor => actor.GetComponent<CombatHpComponent>().HP == 10), Is.True);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public void CopiedPrefab_InitializesAllRuntimeWithoutDebug_AndRestoresAfterAbort(int allies, bool enemyHand)
        {
            Prepare(allies, enemyHand); Start(); var session = _entry.ActiveSession;
            foreach (var actor in session.Allies)
            {
                Assert.That(session.TryGetHand(actor, out var hand), Is.True);
                Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(2));
                Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(3));
                Assert.That(session.BpRuntime.TryGetBalance(actor, out int bp), Is.True); Assert.That(bp, Is.Zero);
                Assert.That(session.UltimateRuntime.TryGetStage(actor, out int stage), Is.True); Assert.That(stage, Is.Zero);
            }
            Assert.That(session.TryGetHand(session.Enemies[0], out _), Is.EqualTo(enemyHand));
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.True);
            Assert.That(_commands, Is.GreaterThan(0));
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Invoke(_entry, "Update");
            Assert.That(_entry.ActiveSession, Is.Null); Assert.That(_flow.PendingDecisionRequest, Is.Null);
            Assert.That(_flow.IsClashSeriesValidationEnabled, Is.False);
            Assert.That(_copy.GetComponent<CombatFieldLock>().IsLocked, Is.False);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        public void CopiedPrefab_ThreeClashRounds_ApplyOneOutcomeAndReachChain()
        {
            Prepare(); Start(); var session = _entry.ActiveSession; Declare();
            Assert.That(_approach.TryComplete(), Is.True);
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(_rounds.Count, Is.EqualTo(round)); Assert.That(session.Enemies[0].HP, Is.EqualTo(10));
                Assert.That(_rounds.Last().TryComplete(), Is.True);
                if (round < 3) Assert.That(_flow.SubmitClashSeriesSkill(_flow.PendingDecisionRequest, session.Allies[0].Skills[1]), Is.True);
            }
            Assert.That(session.Enemies[0].HP, Is.EqualTo(9));
            Assert.That(session.GetCombatState(session.Enemies[0]).CurrentMental, Is.EqualTo(85));
            Assert.That(session.GetCombatState(session.Enemies[0]).TryGetStatus("b3b.fixture.effect", out var status), Is.True); Assert.That(status.StackCount, Is.EqualTo(1));
            Assert.That(_outcome.TryComplete(), Is.True); Assert.That(_outcome.TryComplete(), Is.False);
            Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        [UnityTest]
        public IEnumerator InactivePrefabCopy_ActualDirectorThreeRounds_AbortRestoresTimeAndLock()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                float fixedStep = Time.fixedDeltaTime;
                Prepare(3, manualPresentation: false); Start(); var session = _entry.ActiveSession; Declare();
                float deadline = Time.realtimeSinceStartup + 10f; int repeated = 0;
                while (Time.realtimeSinceStartup < deadline && _entry.ActiveStateMachine.Phase != Phase.ChainDecision)
                {
                    var request = _flow.PendingDecisionRequest;
                    if (request?.Kind == CombatExchangeDecisionKind.ClashAttack)
                    { Assert.That(_flow.SubmitClashSeriesSkill(request, session.Allies[0].Skills[1]), Is.True); repeated++; }
                    yield return null;
                }
                Assert.That(repeated, Is.EqualTo(2)); Assert.That(session.Enemies[0].HP, Is.EqualTo(9));
                Assert.That(_entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
                Assert.That(_entry.ActiveSession, Is.Null); Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
                Assert.That(_copy.GetComponent<CombatFieldLock>().IsLocked, Is.False);
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Prepare(int allies = 1, bool enemyHand = true, bool sceneAddedStage = true, bool bindFixtureConfig = true, bool manualPresentation = true)
        {
            _data = new CombatClashPlaytestV1IntegrationTests();
            typeof(CombatClashPlaytestV1IntegrationTests).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_data, new object[] { allies, enemyHand, true });
            _config = (FinalCombatRuntimeConfigSO)GetField(_data, "_config");
            _request = (CombatStartRequest)GetField(_data, "_request");
            var definitions = (SkillDefinitionSO[])GetField(_data, "_definitions");
            // The data fixture's temporary owner is discarded before the real prefab copy is activated.
            Object.DestroyImmediate(((CombatEntryPoint)GetField(_data, "_entry")).gameObject);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                contents.SetActive(false); _copy = Object.Instantiate(contents); _copy.name = "D2.MemoryOnly.CombatRuntime";
                SceneManager.MoveGameObjectToScene(_copy, _request.AllyFieldObjects[0].scene);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Assert.That(_copy.activeSelf, Is.False); Assert.That(AssetDatabase.Contains(_copy), Is.False);
            RemoveDebug<CombatBpValidationOptIn>();
            if (sceneAddedStage)
            {
                // Ordered rehearsal: base BP is already gone, then model the Scene-added components.
                // This does not recreate or save the protected Scene's prefab overrides.
                _copy.AddComponent<CombatHandValidationOptIn>(); _copy.AddComponent<CombatClashSeriesValidationOptIn>(); _copy.AddComponent<CombatBpValidationOptIn>();
                RemoveDebug<CombatHandValidationOptIn>(); RemoveDebug<CombatBpValidationOptIn>(); RemoveDebug<CombatClashSeriesValidationOptIn>();
            }
            _entry = _copy.GetComponent<CombatEntryPoint>(); _flow = _copy.GetComponent<CombatFlowOrchestrator>(); _director = _copy.GetComponent<CombatDirector>();
            Set(_entry, "skillDefinitions", definitions);
            if (bindFixtureConfig) Set(_entry, "finalExchangeRuntimeConfig", _config);
            _copy.SetActive(true); Invoke(_entry, "Awake");
            if (!Application.isPlaying)
            {
                // Ordinary prefab behaviours do not receive the normal Play Mode enable lifecycle
                // in EditMode. Keep the existing Reward/GameFlow owner responsible for returning.
                var rewardBinder = _copy.GetComponent<CombatRewardUIBinder>();
                Invoke(rewardBinder, "Awake"); Invoke(rewardBinder, "OnEnable");
            }
            _flow.AttackDecisionRequested += _ =>
            {
                _commands++;
                if (_entry.FinalExchangeRuntimeConfig?.EnableProductionHandBp == true)
                { Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.True); Assert.That(_flow.IsClashSeriesValidationEnabled, Is.True); }
            };
            _entry.OnCombatStarted += _ =>
            {
                if (!manualPresentation) return;
                _director.UnbindFinalExchange();
                _flow.ApproachPresentationRequested += value => _approach = value;
                _flow.ClashRoundPresentationRequested += _rounds.Add;
                _flow.OutcomePresentationRequested += value => _outcome = value;
            };
            Assert.That(_copy.GetComponents<MonoBehaviour>().All(component => component != null && !IsDebug(component)), Is.True);
        }
        private void Start() { Assert.That(_entry.StartCombat(_request), Is.True); Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.True); }
        private void Declare() { var session = _entry.ActiveSession; Assert.That(_flow.SubmitAttackDeclaration(new CombatAttackDeclaration(session.Allies[0], session.Enemies[0], session.Allies[0].Skills[0]), session.ExchangeState.Version), Is.True); }
        private void RemoveDebug<T>() where T : MonoBehaviour { var component = _copy.GetComponent<T>(); Assert.That(component, Is.Not.Null); Object.DestroyImmediate(component); _removedDebugCount++; }
        private static bool IsDebug(MonoBehaviour component) => component is CombatHandValidationOptIn || component is CombatBpValidationOptIn || component is CombatClashSeriesValidationOptIn;
        private static object GetField(object owner, string field) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
    }
}
