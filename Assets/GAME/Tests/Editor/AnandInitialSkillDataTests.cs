using System;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class AnandInitialSkillDataTests
    {
        private const string NormalPath = "Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset";
        private const string ClashPath = "Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset";
        private CombatClashPlaytestV1IntegrationTests _fixture;
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args);

        public static void CreateApprovedSkills()
        {
            if (AssetDatabase.LoadMainAssetAtPath(NormalPath) != null || AssetDatabase.LoadMainAssetAtPath(ClashPath) != null || System.IO.File.Exists(NormalPath) || System.IO.File.Exists(ClashPath))
                throw new InvalidOperationException("Skill path already exists; refusing overwrite.");
            var all = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).Where(x => x != null).ToArray();
            if (all.Any(x => x.skillId == 10001 || x.skillId == 10002 || x.PersistentKey == "skill.playtest.anand.basic" || x.PersistentKey == "skill.playtest.anand.zornhau"))
                throw new InvalidOperationException("Skill identity collision.");
            var existing = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_BasicAttack.asset");
            var basic = Object.Instantiate(existing); basic.name = "Skill_AnandBasic_PlaytestV1"; basic.skillId = 10001;
            Set(basic, "persistentKey", "skill.playtest.anand.basic"); Set(basic, "finalMpCost", 0);
            AssetDatabase.CreateAsset(basic, NormalPath);
            var clash = ScriptableObject.CreateInstance<SkillDefinitionSO>(); clash.name = "Skill_Zornhau_PlaytestV1";
            clash.skillId = 10002; clash.displayName = "촌하우"; clash.baseDamage = 4; clash.baseStagger = 0; clash.weaknessStaggerBonus = 0;
            clash.tag = SkillTag.Attack; clash.targeting = TargetingRule.SingleEnemy;
            Set(clash, "persistentKey", "skill.playtest.anand.zornhau"); Set(clash, "finalMpCost", 1); Set(clash, "clashPower", 3);
            Set(clash, "combatAnimationTrigger", ""); Set(clash, "fieldAnimationTrigger", "");
            AssetDatabase.CreateAsset(clash, ClashPath);
            AssetDatabase.SetLabels(basic, new[] { "PlaytestV1", "ProductionWiringPending" });
            AssetDatabase.SetLabels(clash, new[] { "PlaytestV1", "PresentationPending", "ProductionWiringPending" });
            AssetDatabase.SaveAssetIfDirty(basic); AssetDatabase.SaveAssetIfDirty(clash);
        }

        [TearDown] public void TearDown() { _fixture?.TearDown(); _fixture = null; }

        [Test]
        public void ApprovedAssets_UseDistinctIdsAndPreserveBasicCombatValues()
        {
            var old = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_BasicAttack.asset");
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(NormalPath); var zorn = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(ClashPath);
            Assert.That(old.FinalMpCost, Is.EqualTo(1)); Assert.That(basic.FinalMpCost, Is.Zero);
            Assert.That(basic.baseDamage, Is.EqualTo(old.baseDamage)); Assert.That(basic.baseStagger, Is.EqualTo(old.baseStagger)); Assert.That(basic.speed, Is.EqualTo(old.speed)); Assert.That(basic.ClashPower, Is.EqualTo(old.ClashPower));
            Assert.That(zorn.FinalMpCost, Is.EqualTo(1)); Assert.That(zorn.ClashPower, Is.EqualTo(3)); Assert.That(zorn.baseDamage, Is.EqualTo(4));
            Assert.That(zorn.targeting, Is.EqualTo(TargetingRule.SingleEnemy)); Assert.That(zorn.OwnershipCategory, Is.EqualTo(SkillOwnershipCategory.Compatibility)); Assert.That(zorn.AppliedStatusEffects, Is.Empty);
            var all = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).Where(x => x != null).ToArray();
            Assert.That(all.Count(x => x.skillId == basic.skillId), Is.EqualTo(1)); Assert.That(all.Count(x => x.skillId == zorn.skillId), Is.EqualTo(1));
            Assert.That(Game.Combat.Editor.SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(all), Is.Empty);
        }

        [Test]
        public void ActualSkills_InitializeSnapshotAndPayEachCardOnce()
        {
            _fixture = new CombatClashPlaytestV1IntegrationTests(); Invoke(_fixture, "Prepare", 1, true, true);
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(NormalPath); var zorn = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(ClashPath);
            var definitions = Get<SkillDefinitionSO[]>(_fixture, "_definitions");
            var skillRuntime = CharacterSkillSaveParticipant.Instance.Runtime;
            skillRuntime.TryUnequip("b3b.hero.0", definitions[0].PersistentKey); skillRuntime.TryUnequip("b3b.hero.0", definitions[1].PersistentKey);
            foreach (var skill in new[] { basic, zorn }) { skillRuntime.TryAcquire("char.anand_de_morte", skill.PersistentKey); skillRuntime.TryEquip("char.anand_de_morte", skill.PersistentKey); }
            var request = Get<Game.Combat.Adapters.CombatStartRequest>(_fixture, "_request");
            request.BindAllyCharacter(request.AllyFieldObjects[0], "char.anand_de_morte");
            definitions[0] = basic; definitions[1] = zorn;
            var entry = Get<CombatEntryPoint>(_fixture, "_entry"); var flow = Get<CombatFlowOrchestrator>(_fixture, "_flow");
            var config = Get<FinalCombatRuntimeConfigSO>(_fixture, "_config"); var so = new SerializedObject(config);
            var rules = so.FindProperty("handCardRules"); rules.GetArrayElementAtIndex(0).FindPropertyRelative("skill").objectReferenceValue = basic;
            rules.GetArrayElementAtIndex(1).FindPropertyRelative("skill").objectReferenceValue = zorn; so.FindProperty("ultimateSkills").arraySize = 0; so.ApplyModifiedPropertiesWithoutUndo();
            Set(entry, "skillDefinitions", definitions); Invoke(entry, "Awake");
            Assert.That(entry.StartCombat(request), Is.True); var session = entry.ActiveSession; var ally = session.Allies[0];
            Assert.That(session.TryGetHand(ally, out var hand), Is.True); Assert.That(hand.Hand.Count(x => x.Kind == CombatCardKind.Normal), Is.EqualTo(2)); Assert.That(hand.Hand.Count(x => x.Kind == CombatCardKind.Clash), Is.EqualTo(3));
            Assert.That(hand.DeckSnapshot.Select(x => x.RuntimeId).Distinct().Count(), Is.EqualTo(hand.DeckSnapshot.Count));
            skillRuntime.TryUnequip("char.anand_de_morte", zorn.PersistentKey); Assert.That(ally.Skills.Count, Is.EqualTo(2));
            var normalCard = hand.Hand.First(x => x.Kind == CombatCardKind.Normal); var state = session.GetCombatState(ally); int before = state.CurrentMp;
            Assert.That(flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, session.Enemies[0], normalCard.Skill, normalCard.RuntimeId), session.ExchangeState.Version), Is.True);
            Assert.That(state.CurrentMp, Is.EqualTo(before)); Assert.That(hand.NormalDiscard.Any(x => x.RuntimeId == normalCard.RuntimeId), Is.True);
            var approach = Get<CombatApproachPresentationRequest>(_fixture, "_approach"); Assert.That(approach.TryComplete(), Is.True);
            var rounds = Get<System.Collections.Generic.List<CombatClashRoundPresentationRequest>>(_fixture, "_rounds"); Assert.That(rounds.Last().TryComplete(), Is.True);
            var decision = flow.PendingDecisionRequest; var card = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
            Assert.That(flow.SubmitClashSeriesSkill(decision, card.Skill, card.RuntimeId), Is.True);
            Assert.That(state.CurrentMp, Is.EqualTo(before - 1)); Assert.That(hand.ClashDiscard.Any(x => x.RuntimeId == card.RuntimeId), Is.True);
            Assert.That(flow.SubmitClashSeriesSkill(decision, card.Skill, card.RuntimeId), Is.False); Assert.That(state.CurrentMp, Is.EqualTo(before - 1));
            Assert.That(rounds.Last().TryComplete(), Is.True);
            var finalDecision = flow.PendingDecisionRequest; var next = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
            int remaining = state.CurrentMp; state.SetMp(0);
            Assert.That(flow.SubmitClashSeriesSkill(finalDecision, next.Skill, next.RuntimeId), Is.False);
            Assert.That(hand.Hand.Any(x => x.RuntimeId == next.RuntimeId), Is.True); Assert.That(state.CurrentMp, Is.Zero);
            state.SetMp(remaining); // Isolated test restores the precondition; combat gains no automatic MP.
            Assert.That(flow.SubmitClashSeriesSkill(finalDecision, next.Skill, next.RuntimeId), Is.True);
            Assert.That(state.CurrentMp, Is.EqualTo(remaining - 1)); Assert.That(rounds.Last().TryComplete(), Is.True);
            Assert.That(session.Enemies[0].HP, Is.EqualTo(6));
        }
    }
}
