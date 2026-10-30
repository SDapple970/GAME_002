using System.Collections.Generic;
using System.IO;
using Game.EditorTools;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class CombatProductionDataAuditTests
    {
        private readonly List<Object> _created = new();
        [TearDown] public void TearDown()
        {
            foreach (Object value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [TestCase("Assets/GAME/Tests/Editor/Fixtures/status.asset", true)]
        [TestCase("Assets/GAME/Data/Test/CP_TestHero.asset", true)]
        [TestCase("Assets/GAME/Scripts/Combat/Debugging/OptIn.cs", true)]
        [TestCase("Assets/GAME/Data/Skill/Skill_BasicAttack.asset", false)]
        public void DependencyClassification_DetectsNonProductionDirectories(string path, bool expected)
        {
            Assert.That(CombatProductionDataAudit.IsNonProductionPath(path), Is.EqualTo(expected));
        }

        [Test]
        public void DuplicateOrMissingCharacterId_IsReported()
        {
            var definition = Create<CharacterProgressionDefinitionSO>();
            Assert.That(CombatProductionDataAudit.CollectCharacterIssues(new[] { definition }), Is.Not.Empty);
            Set(definition, "characterId", "07.fixture.hero");
            Assert.That(CombatProductionDataAudit.CollectCharacterIssues(new[] { definition, definition }),
                Has.Some.Contains("Duplicate Character ID"));
        }

        [Test]
        public void SingleMemberStart_RequiresAuthoredLeaderAndLineup()
        {
            var character = Create<CharacterProgressionDefinitionSO>();
            Set(character, "characterId", "07.fixture.hero");
            var start = Create<CharacterStartDefinitionSO>();
            var serialized = new SerializedObject(start);
            serialized.FindProperty("definitions").arraySize = 1;
            serialized.FindProperty("definitions").GetArrayElementAtIndex(0).objectReferenceValue = character;
            foreach (string field in new[] { "initialMemberIds", "initialCombatMemberIds" })
            {
                serialized.FindProperty(field).arraySize = 1;
                serialized.FindProperty(field).GetArrayElementAtIndex(0).stringValue = character.CharacterId;
            }
            serialized.FindProperty("initialLeaderId").stringValue = character.CharacterId;
            serialized.FindProperty("defaultRewardTargetId").stringValue = character.CharacterId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(CombatProductionDataAudit.CollectStartIssues(start), Is.Empty);
            Set(start, "initialLeaderId", "unknown");
            Assert.That(CombatProductionDataAudit.CollectStartIssues(start), Is.Not.Empty);
        }

        [Test]
        public void MissingStartup_IsContentIssue_NotImplicitApproval()
        {
            Assert.That(CombatProductionDataAudit.CollectStartIssues(null), Is.Not.Empty);
        }

        [Test]
        public void ProductionAudit_WritesFindingsWithoutSavingProtectedAssets()
        {
            string[] paths = { "Assets/GAME/Scenes/TitleScene.unity", "Assets/GAME/Scenes/Dungeon_1_Production.unity",
                "Assets/GAME/Prefabs/CombatRuntime.prefab", "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab" };
            byte[][] before = System.Array.ConvertAll(paths, File.ReadAllBytes);
            CombatProductionDataAudit.Run();
            string report = File.ReadAllText("Logs/CBT-R2-07-ContentAudit.md");
            Assert.That(report, Does.Contain("characterStartDefinition").And.Contain("CombatItemUseAdapter"));
            for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>(); _created.Add(value); return value;
        }
        private static void Set(Object target, string name, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
