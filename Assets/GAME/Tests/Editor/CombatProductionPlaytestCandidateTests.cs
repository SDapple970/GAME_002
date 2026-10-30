using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat.Data;
using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class CombatProductionPlaytestCandidateTests
    {
        private readonly List<Object> _created = new();
        private FinalCombatRuntimeConfigSO Candidate => AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(CombatProductionDataAudit.PlaytestCandidatePath);

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void Candidate_ApprovedNumbersRemainDisabled_WithoutInventedContent()
        {
            Assert.That(Candidate, Is.Not.Null);
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(Candidate), Is.Empty);
            Assert.That(Candidate.HandCardRules, Is.Empty);
            Assert.That(Candidate.UltimateSkills, Is.Empty);
            Assert.That(Candidate.EnemyHandSourceKeys, Is.Empty);
            Assert.That(AssetDatabase.GetLabels(Candidate), Does.Contain("ContentPending").And.Contain("ProductionPlaytestCandidate"));
            var defaults = ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>(); _created.Add(defaults);
            Assert.That(Candidate.InitialMp, Is.EqualTo(defaults.InitialMp));
            Assert.That(Candidate.MaxMental, Is.EqualTo(defaults.MaxMental));
        }

        [TestCase("normalHandCapacity", 1)]
        [TestCase("clashHandCapacity", 2)]
        [TestCase("bpMaximum", 4)]
        [TestCase("bpRoundCyclesPerGain", 1)]
        [TestCase("bpMaximumInvestment", 4)]
        [TestCase("clashAttackerDurability", 0)]
        [TestCase("clashResponderDurability", 0)]
        [TestCase("clashMaxComparisons", 0)]
        [TestCase("clashFinalSkillPolicy", 0)]
        [TestCase("clashTimeoutAttackPolicy", 0)]
        public void InvalidOrMissingApprovedField_IsReported(string field, int value)
        {
            var copy = Copy(); var data = new SerializedObject(copy);
            data.FindProperty(field).intValue = value; data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(copy), Is.Not.Empty);
        }

        [TestCase(0f)]
        [TestCase(4f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeout_IsReported(float value)
        {
            var copy = Copy(); var data = new SerializedObject(copy);
            data.FindProperty("clashDecisionDurationSeconds").floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(copy), Is.Not.Empty);
        }

        [Test]
        public void AccidentalActivation_IsReported()
        {
            var copy = Copy(); var data = new SerializedObject(copy);
            data.FindProperty("enableProductionHandBp").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(copy), Has.Some.Contains("disabled"));
        }

        [Test]
        public void MissingConfig_IsNotReady()
        {
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(null), Is.Not.Empty);
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(null), Is.Not.Empty);
        }

        [Test]
        public void NumbersPassing_DoesNotHideMissingContent()
        {
            Assert.That(CombatProductionDataAudit.CollectPlaytestNumberIssues(Candidate), Is.Empty);
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(Candidate), Has.Some.Contains("Content Pending"));
        }

        [Test]
        public void Candidate_HasIndependentGuidAndNoTestDependenciesOrProductionUsers()
        {
            string candidatePath = CombatProductionDataAudit.PlaytestCandidatePath;
            string guid = AssetDatabase.AssetPathToGUID(candidatePath);
            Assert.That(guid, Has.Length.EqualTo(32));
            foreach (string path in new[] { CombatPlaytestV1ConfigTests.ConfigPath, CombatClashPlaytestV1IntegrationTests.ConfigPath,
                "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_V1.asset" })
                Assert.That(guid, Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(AssetDatabase.GetDependencies(candidatePath, true).Any(CombatProductionDataAudit.IsNonProductionPath), Is.False);
            foreach (string path in new[] { "Assets/GAME/Prefabs/CombatRuntime.prefab", "Assets/GAME/Scenes/Dungeon_1_Production.unity", "Assets/GAME/Scenes/Dungeon_Template.unity" })
                Assert.That(AssetDatabase.GetDependencies(path, true), Does.Not.Contain(candidatePath));
        }

        [Test]
        public void EmptyEnemyHandList_PreservesOptOut_UnknownSourceIsReported()
        {
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(Candidate).Any(value => value.StartsWith("Enemy Hand source")), Is.False);
            var copy = Copy(); var data = new SerializedObject(copy); var sources = data.FindProperty("enemyHandSourceKeys");
            sources.arraySize = 1; sources.GetArrayElementAtIndex(0).stringValue = "d3.test.unresolved";
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(copy), Has.Some.Contains("Enemy Hand source"));
        }

        [Test]
        public void SamePersistentSkill_CannotOccupyBothHandKinds()
        {
            var copy = Copy(); var data = new SerializedObject(copy); var rules = data.FindProperty("handCardRules"); rules.arraySize = 2;
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_BasicAttack.asset");
            for (int i = 0; i < 2; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("skill").objectReferenceValue = skill;
                rule.FindPropertyRelative("kind").intValue = i;
                rule.FindPropertyRelative("copies").intValue = 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(copy), Has.Some.Contains("Duplicate Hand skill placement"));
        }

        [Test]
        public void TestFixtureHandReference_IsReportedWithoutSavingIt()
        {
            var copy = Copy(); var data = new SerializedObject(copy); var rules = data.FindProperty("handCardRules"); rules.arraySize = 1;
            var rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("skill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(
                "Assets/GAME/Tests/Editor/Fixtures/CombatStatusValidation/TS_Skill_ApplyStatuses.asset");
            rule.FindPropertyRelative("copies").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectPlaytestContentIssues(copy), Has.Some.Contains("Test Hand skill"));
        }

        [Test]
        public void CreationIsIdempotent_DoesNotResaveExistingCandidate()
        {
            string path = CombatProductionDataAudit.PlaytestCandidatePath;
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            CombatProductionDataAudit.CreatePlaytestCandidateIfMissing();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(meta));
        }

        [Test]
        public void ReadinessAudit_ReportsBlockers_AndDoesNotSaveAssets()
        {
            string path = CombatProductionDataAudit.PlaytestCandidatePath;
            byte[] before = File.ReadAllBytes(path);
            CombatProductionDataAudit.RunPlaytestCandidateAudit();
            string report = File.ReadAllText("Logs/CBT-R2-08-P2B3D3-Readiness.md");
            Assert.That(report, Does.Contain("MATCH / DISABLED").And.Contain("Readiness: BLOCKED").And.Contain("snapshot"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        private FinalCombatRuntimeConfigSO Copy()
        {
            var copy = Object.Instantiate(Candidate); _created.Add(copy); return copy;
        }
    }
}
