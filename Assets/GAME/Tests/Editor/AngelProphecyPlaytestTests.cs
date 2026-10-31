using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Enemies;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class AngelProphecyPlaytestTests
    {
        private const string SkillPath = "Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset";
        private CombatClashPlaytestV1IntegrationTests _fixture;
        private FinalCombatRuntimeConfigSO _config;
        private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
        private static void Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);

        public static void CreateApprovedSkill()
        {
            if (AssetDatabase.LoadMainAssetAtPath(SkillPath) != null || System.IO.File.Exists(SkillPath)) throw new InvalidOperationException("Existing asset must not be overwritten.");
            var all = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).Where(x => x != null);
            if (all.Any(x => x.skillId == 10003 || x.PersistentKey == "skill.playtest.angel.prophecy")) throw new InvalidOperationException("Skill identity collision.");
            var skill = ScriptableObject.CreateInstance<SkillDefinitionSO>(); skill.name = "Skill_AngelTheProphecy_PlaytestV1";
            skill.skillId = 10003; skill.displayName = "The Prophecy"; skill.tag = SkillTag.Attack; skill.targeting = TargetingRule.SingleEnemy;
            skill.baseDamage = 3; skill.baseStagger = 0; skill.weaknessStaggerBonus = 0;
            Set(skill, "persistentKey", "skill.playtest.angel.prophecy"); Set(skill, "finalMpCost", 1); Set(skill, "clashPower", 2);
            Set(skill, "combatAnimationTrigger", ""); Set(skill, "fieldAnimationTrigger", "");
            AssetDatabase.CreateAsset(skill, SkillPath); AssetDatabase.SetLabels(skill, new[] { "PlaytestV1", "PresentationPending", "ProductionWiringPending" }); AssetDatabase.SaveAssetIfDirty(skill);
        }

        [TearDown] public void TearDown() { _fixture?.TearDown(); _fixture = null; if (_config != null) Object.DestroyImmediate(_config); _config = null; }

        [Test]
        public void ApprovedSkill_HasUniqueIdentityAndExpectedData()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(SkillPath); Assert.That(skill, Is.Not.Null);
            Assert.That(skill.skillId, Is.EqualTo(10003)); Assert.That(skill.PersistentKey, Is.EqualTo("skill.playtest.angel.prophecy"));
            Assert.That(skill.FinalMpCost, Is.EqualTo(1)); Assert.That(skill.ClashPower, Is.EqualTo(2)); Assert.That(skill.baseDamage, Is.EqualTo(3));
            Assert.That(skill.targeting, Is.EqualTo(TargetingRule.SingleEnemy)); Assert.That(skill.OwnershipCategory, Is.EqualTo(SkillOwnershipCategory.Compatibility)); Assert.That(skill.AppliedStatusEffects, Is.Empty);
            var all = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).Where(x => x != null).ToArray();
            Assert.That(all.Count(x => x.skillId == skill.skillId), Is.EqualTo(1)); Assert.That(Game.Combat.Editor.SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(all), Is.Empty);
            var source = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>("Assets/GAME/Data/Enemy/Enemy_Angel.asset"); Assert.That(source.PersistentKey, Is.EqualTo("enemy.chapter1.angel")); Assert.That(source.AcquirableSkillPersistentKeys, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnemyFirst_ExistingAiUsesProphecy_AnandRespondsAndAbortRestores()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            try
            {
                float fixedStep = Time.fixedDeltaTime;
                Prepare(true); var entry = Get<CombatEntryPoint>(_fixture, "_entry"); var flow = Get<CombatFlowOrchestrator>(_fixture, "_flow");
                Assert.That(entry.StartCombat(Get<CombatStartRequest>(_fixture, "_request")), Is.True);
                var session = entry.ActiveSession; var ally = session.Allies[0]; var enemy = session.Enemies[0];
                Assert.That(enemy.HP, Is.EqualTo(14)); Assert.That(enemy.Skills.Single().Id.Value, Is.EqualTo(10003));
                Assert.That(session.TryGetEnemySource(enemy, out var source), Is.True); Assert.That(source.SourceKey, Is.EqualTo("enemy.chapter1.angel"));
                Assert.That(session.TryGetHand(enemy, out _), Is.False);
                var decision = flow.PendingDecisionRequest; Assert.That(decision.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                Assert.That(session.TryGetHand(ally, out var hand), Is.True); var card = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), decision), Is.True);
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(1)); Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(1));
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), decision), Is.False);
                var approach = Get<CombatApproachPresentationRequest>(_fixture, "_approach"); Assert.That(approach.TryComplete(), Is.True);
                var rounds = Get<List<CombatClashRoundPresentationRequest>>(_fixture, "_rounds"); Assert.That(rounds, Is.Not.Empty);
                var comparison = rounds.Last().Result;
                Assert.That(comparison.AttackerPower, Is.EqualTo(2)); Assert.That(comparison.ResponderPower, Is.EqualTo(3));
                Assert.That(comparison.AttackerDurabilityBefore, Is.EqualTo(3)); Assert.That(comparison.AttackerDurabilityAfter, Is.EqualTo(2));
                Assert.That(comparison.ResponderDurabilityBefore, Is.EqualTo(3)); Assert.That(comparison.ResponderDurabilityAfter, Is.EqualTo(3));
                Assert.That(rounds.Last().TryComplete(), Is.True);
                Assert.That(session.GetCombatState(enemy).CurrentMp, Is.LessThanOrEqualTo(1));
                entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
                Assert.That(entry.ActiveSession, Is.Null); Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AllyFirst_ExistingAiRespondsWithProphecy_AndOutOfMpCannotRespondAgain()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            try
            {
                Prepare(false); var entry = Get<CombatEntryPoint>(_fixture, "_entry"); var flow = Get<CombatFlowOrchestrator>(_fixture, "_flow");
                Assert.That(entry.StartCombat(Get<CombatStartRequest>(_fixture, "_request")), Is.True);
                var session = entry.ActiveSession; var ally = session.Allies[0]; var enemy = session.Enemies[0];
                Assert.That(session.TryGetHand(ally, out var hand), Is.True); var normal = hand.Hand.First(x => x.Kind == CombatCardKind.Normal);
                Assert.That(flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, enemy, normal.Skill, normal.RuntimeId), session.ExchangeState.Version), Is.True);
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2)); Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(1));
                Assert.That(Get<CombatApproachPresentationRequest>(_fixture, "_approach").TryComplete(), Is.True);
                var rounds = Get<List<CombatClashRoundPresentationRequest>>(_fixture, "_rounds"); Assert.That(rounds.Last().TryComplete(), Is.True);
                var token = flow.PendingDecisionRequest; var zorn = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(flow.SubmitClashSeriesSkill(token, zorn.Skill, zorn.RuntimeId), Is.True);
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(1)); Assert.That(session.GetCombatState(enemy).CurrentMp, Is.Zero);
                Assert.That(flow.SubmitClashSeriesSkill(token, zorn.Skill, zorn.RuntimeId), Is.False);
                Assert.That(rounds.Last().TryComplete(), Is.True);
                var final = flow.PendingDecisionRequest; var second = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
                Assert.That(flow.SubmitClashSeriesSkill(final, second.Skill, second.RuntimeId), Is.True);
                Assert.That(session.GetCombatState(enemy).CurrentMp, Is.Zero);
                Assert.That(enemy.HP, Is.EqualTo(10));
                var outcome = Get<CombatOutcomePresentationRequest>(_fixture, "_outcome"); Assert.That(outcome.TryComplete(), Is.True);
                Assert.That(entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Prepare(bool enemyFirst)
        {
            _fixture = new CombatClashPlaytestV1IntegrationTests(); Invoke(_fixture, "Prepare", 1, false, true);
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset");
            var zorn = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset");
            var prophecy = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(SkillPath);
            var runtime = CharacterSkillSaveParticipant.Instance.Runtime;
            foreach (var skill in new[] { basic, zorn }) { runtime.TryAcquire("char.anand_de_morte", skill.PersistentKey); runtime.TryEquip("char.anand_de_morte", skill.PersistentKey); }
            _config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>("Assets/GAME/Tests/Editor/Fixtures/CombatPlaytestV1/AnandHand_C2_TestOnly.asset")); Set(_config, "enableProductionHandBp", true);
            var old = Get<CombatStartRequest>(_fixture, "_request");
            var request = new CombatStartRequest(enemyFirst ? StartReason.PlayerGotHit : StartReason.PlayerFirstHit, enemyFirst ? Side.Enemies : Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.AddRange(old.AllyFieldObjects); request.EnemyFieldObjects.AddRange(old.EnemyFieldObjects); request.BindAllyCharacter(request.AllyFieldObjects[0], "char.anand_de_morte");
            var enemy = request.EnemyFieldObjects[0]; var hp = enemy.GetComponent<CombatHpComponent>(); hp.MaxHP = 14; hp.HP = 14;
            Set(enemy.GetComponent<CombatSkillLoadoutComponent>(), "skillIds", new[] { 10003 });
            var source = enemy.AddComponent<EnemySourceComponent>(); Set(source, "definition", AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>("Assets/GAME/Data/Enemy/Enemy_Angel.asset"));
            Set(_fixture, "_request", request); var entry = Get<CombatEntryPoint>(_fixture, "_entry");
            Set(entry, "finalExchangeRuntimeConfig", _config); Set(entry, "skillDefinitions", new[] { basic, zorn, prophecy }); Invoke(entry, "Awake");
        }
    }
}
