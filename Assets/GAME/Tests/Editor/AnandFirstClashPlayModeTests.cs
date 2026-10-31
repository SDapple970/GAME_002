using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class AnandFirstClashPlayModeTests
    {
        private const string Path = "Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/AnandHand_C2_TestOnly.asset";
        private const string BasicPath = "Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset";
        private const string ZornPath = "Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset";
        private CombatClashPlaytestV1IntegrationTests _fixture;
        private FinalCombatRuntimeConfigSO _copy;
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private static void Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

        public static void CreateTestOnlyCandidate()
        {
            if (AssetDatabase.LoadMainAssetAtPath(Path) != null || System.IO.File.Exists(Path)) throw new InvalidOperationException("Existing candidate must not be overwritten.");
            var source = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>("Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_ProductionPlaytestV1_Candidate.asset");
            var config = Object.Instantiate(source); config.name = "AnandHand_C2_TestOnly";
            var data = new SerializedObject(config); var rules = data.FindProperty("handCardRules"); rules.arraySize = 2;
            string[] paths = { BasicPath, ZornPath }; int[] copies = { 2, 3 };
            for (int i = 0; i < 2; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i); rule.FindPropertyRelative("skill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(paths[i]);
                rule.FindPropertyRelative("kind").intValue = i; rule.FindPropertyRelative("copies").intValue = copies[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(config, Path);
            AssetDatabase.SetLabels(config, new[] { "TestOnly", "PlaytestV1", "CopiesFixtureOnly", "ProductionWiringPending" });
            AssetDatabase.SaveAssetIfDirty(config);
        }

        [TearDown] public void TearDown()
        {
            _fixture?.TearDown(); _fixture = null;
            if (_copy != null) Object.DestroyImmediate(_copy); _copy = null;
        }

        [Test]
        public void CandidateIsDisabledAndReferencesActualSkills()
        {
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(Path);
            Assert.That(config.EnableProductionHandBp, Is.False); Assert.That(config.InitialMp, Is.EqualTo(2));
            Assert.That(config.NormalHandCapacity, Is.EqualTo(2)); Assert.That(config.ClashHandCapacity, Is.EqualTo(3));
            Assert.That(config.ClashAttackerDurability, Is.EqualTo(3)); Assert.That(config.ClashResponderDurability, Is.EqualTo(3));
            Assert.That(config.ClashMaxComparisons, Is.EqualTo(3)); Assert.That(config.ClashDecisionDurationSeconds, Is.EqualTo(5));
            Assert.That(config.HandCardRules[0].Skill, Is.SameAs(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(BasicPath)));
            Assert.That(config.HandCardRules[1].Skill, Is.SameAs(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(ZornPath)));
            Assert.That(config.HandCardRules[0].Copies, Is.EqualTo(2)); Assert.That(config.HandCardRules[1].Copies, Is.EqualTo(3));
            Assert.That(AssetDatabase.GetLabels(config), Does.Contain("TestOnly").And.Contain("CopiesFixtureOnly"));
        }

        [UnityTest]
        public IEnumerator AllyFirst_ActualSkillsSpendTwoMpAcrossThreeComparisons_ThenChain()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                Prepare(false); var entry = Get<CombatEntryPoint>(_fixture, "_entry"); var flow = Get<CombatFlowOrchestrator>(_fixture, "_flow");
                Assert.That(entry.StartCombat(Get<CombatStartRequest>(_fixture, "_request")), Is.True);
                var session = entry.ActiveSession; var ally = session.Allies[0]; var state = session.GetCombatState(ally);
                Assert.That(state.CurrentMp, Is.EqualTo(2)); Assert.That(session.TryGetHand(ally, out var hand), Is.True);
                var normal = hand.Hand.First(x => x.Kind == CombatCardKind.Normal);
                Assert.That(flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, session.Enemies[0], normal.Skill, normal.RuntimeId), session.ExchangeState.Version), Is.True);
                Assert.That(state.CurrentMp, Is.EqualTo(2)); Assert.That(hand.NormalDiscard.Count(x => x.RuntimeId == normal.RuntimeId), Is.EqualTo(1));
                Assert.That(Get<CombatApproachPresentationRequest>(_fixture, "_approach").TryComplete(), Is.True);
                var rounds = Get<List<CombatClashRoundPresentationRequest>>(_fixture, "_rounds");
                Assert.That(rounds.Last().TryComplete(), Is.True); Assert.That(state.CurrentMp, Is.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    var token = flow.PendingDecisionRequest; var card = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                    Assert.That(token.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashAttack));
                    Assert.That(flow.SubmitClashSeriesSkill(token, card.Skill, card.RuntimeId), Is.True);
                    Assert.That(state.CurrentMp, Is.EqualTo(1 - i)); Assert.That(hand.ClashDiscard.Count(x => x.RuntimeId == card.RuntimeId), Is.EqualTo(1));
                    Assert.That(flow.SubmitClashSeriesSkill(token, card.Skill, card.RuntimeId), Is.False);
                    Assert.That(rounds.Last().TryComplete(), Is.True);
                }
                Assert.That(rounds.Count, Is.EqualTo(3)); Assert.That(session.Enemies[0].HP, Is.EqualTo(6));
                var outcome = Get<CombatOutcomePresentationRequest>(_fixture, "_outcome"); Assert.That(outcome.TryComplete(), Is.True); Assert.That(outcome.TryComplete(), Is.False);
                Assert.That(entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EnemyFirst_ActualZornIsFirstResponse_PaysOnceAndAbortCleansUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                float fixedStep = Time.fixedDeltaTime;
                Prepare(true); var entry = Get<CombatEntryPoint>(_fixture, "_entry"); var flow = Get<CombatFlowOrchestrator>(_fixture, "_flow");
                var request = Get<CombatStartRequest>(_fixture, "_request"); Assert.That(entry.StartCombat(request), Is.True);
                var session = entry.ActiveSession; var ally = session.Allies[0]; var state = session.GetCombatState(ally);
                Assert.That(state.CurrentMp, Is.EqualTo(2)); Assert.That(session.TryGetHand(ally, out var hand), Is.True);
                Assert.That(hand.Hand.Count(x => x.Kind == CombatCardKind.Normal), Is.EqualTo(2)); Assert.That(hand.Hand.Count(x => x.Kind == CombatCardKind.Clash), Is.EqualTo(3));
                Assert.That(hand.DeckSnapshot.Select(x => x.RuntimeId).Distinct().Count(), Is.EqualTo(5));
                var response = flow.PendingDecisionRequest;
                Assert.That(response, Is.Not.Null); Assert.That(response.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                var card = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), response), Is.True);
                Assert.That(state.CurrentMp, Is.EqualTo(1)); Assert.That(hand.ClashDiscard.Count(x => x.RuntimeId == card.RuntimeId), Is.EqualTo(1));
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), response), Is.False);
                Assert.That(state.CurrentMp, Is.EqualTo(1));
                var approach = Get<CombatApproachPresentationRequest>(_fixture, "_approach"); Assert.That(approach.TryComplete(), Is.True);
                var rounds = Get<List<CombatClashRoundPresentationRequest>>(_fixture, "_rounds");
                Assert.That(rounds.Last().TryComplete(), Is.True); Assert.That(state.CurrentMp, Is.EqualTo(1));
                var nextResponse = flow.PendingDecisionRequest;
                Assert.That(nextResponse.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashResponse));
                var second = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(flow.SubmitClashSeriesSkill(nextResponse, second.Skill, second.RuntimeId), Is.True);
                Assert.That(state.CurrentMp, Is.Zero); Assert.That(hand.ClashDiscard.Count(x => x.RuntimeId == second.RuntimeId), Is.EqualTo(1));
                Assert.That(rounds.Last().TryComplete(), Is.True);
                var exhausted = flow.PendingDecisionRequest; var third = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(exhausted.Kind, Is.EqualTo(CombatExchangeDecisionKind.ClashResponse));
                Assert.That(flow.SubmitClashSeriesSkill(exhausted, third.Skill, third.RuntimeId), Is.False);
                Assert.That(state.CurrentMp, Is.Zero); Assert.That(hand.Hand.Any(x => x.RuntimeId == third.RuntimeId), Is.True);
                Assert.That(flow.SubmitClashSeriesNoResponse(exhausted), Is.True);
                Assert.That(flow.SubmitClashSeriesNoResponse(exhausted), Is.False);
                entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
                Assert.That(entry.ActiveSession, Is.Null); Assert.That(flow.PendingDecisionRequest, Is.Null);
                Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Prepare(bool enemyFirst)
        {
            _fixture = new CombatClashPlaytestV1IntegrationTests(); Invoke(_fixture, "Prepare", 1, false, true);
            var definitions = Get<SkillDefinitionSO[]>(_fixture, "_definitions");
            // Existing enemy fixture skills cost zero only in this test, so the opponent can
            // issue a third attack without raising either actor's approved initial MP of two.
            Set(definitions[2], "finalMpCost", 0); Set(definitions[3], "finalMpCost", 0);
            var runtime = CharacterSkillSaveParticipant.Instance.Runtime;
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(BasicPath); var zorn = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(ZornPath);
            foreach (var skill in new[] { basic, zorn }) { runtime.TryAcquire("char.anand_de_morte", skill.PersistentKey); runtime.TryEquip("char.anand_de_morte", skill.PersistentKey); }
            definitions[0] = basic; definitions[1] = zorn;
            var original = Get<CombatStartRequest>(_fixture, "_request");
            var request = new CombatStartRequest(enemyFirst ? StartReason.PlayerGotHit : StartReason.PlayerFirstHit, enemyFirst ? Side.Enemies : Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.AddRange(original.AllyFieldObjects); request.EnemyFieldObjects.AddRange(original.EnemyFieldObjects);
            request.BindAllyCharacter(request.AllyFieldObjects[0], "char.anand_de_morte"); Set(_fixture, "_request", request);
            _copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(Path)); Set(_copy, "enableProductionHandBp", true);
            var entry = Get<CombatEntryPoint>(_fixture, "_entry"); Set(entry, "finalExchangeRuntimeConfig", _copy); Set(entry, "skillDefinitions", definitions); Invoke(entry, "Awake");
        }
    }
}
