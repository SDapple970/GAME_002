using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class CombatSkillIdentityValidationTests
    {
        private const string ProductionDungeon = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string DungeonTemplate = "Assets/GAME/Scenes/Dungeon_Template.unity";

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void AllSkillDefinitionAssets_HaveGloballyUniquePersistentKeys()
        {
            SkillDefinitionSO[] definitions = FindAllSkillDefinitions();

            Assert.That(definitions, Is.Not.Empty);
            Assert.That(SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(definitions), Is.Empty);
        }

        [Test]
        public void PersistentKeyValidator_RejectsNullEmptyAndDuplicateDefinitions()
        {
            SkillDefinitionSO empty = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            SkillDefinitionSO first = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            SkillDefinitionSO second = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            try
            {
                SetPersistentKey(first, "skill.test.duplicate");
                SetPersistentKey(second, "skill.test.duplicate");

                IReadOnlyList<string> issues = SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(new SkillDefinitionSO[]
                {
                    null,
                    empty,
                    first,
                    second
                });

                Assert.That(issues.Any(issue => issue.Contains("null")), Is.True);
                Assert.That(issues.Any(issue => issue.Contains("empty persistentKey")), Is.True);
                Assert.That(issues.Any(issue => issue.Contains("duplicate persistentKey 'skill.test.duplicate'")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void PersistentKeyValidation_AllowsGlobalRuntimeSkillIdCompatibilityDuplicates()
        {
            SkillDefinitionSO production = Load("Assets/GAME/Data/Skill/Skill_BasicAttack.asset");
            SkillDefinitionSO demo = Load("Assets/GAME/Combat/Data/Skills/SO_Skill_BasicAttack.asset");

            Assert.That(production.skillId, Is.EqualTo(demo.skillId));
            Assert.That(production.PersistentKey, Is.Not.EqualTo(demo.PersistentKey));
            Assert.That(SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(new[] { production, demo }), Is.Empty);
            Assert.That(SkillDefinitionPersistentIdentityValidator.CollectRegistrationIssues(new[] { production, demo }, "test"),
                Has.Some.Contains("duplicate skillId 1"));
        }

        [TestCase(ProductionDungeon)]
        [TestCase(DungeonTemplate)]
        public void ProductionCombatRegistries_HaveUniqueRuntimeIdsResolvableLoadoutsAndPersistentKeys(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CombatEntryPoint entryPoint = UnityEngine.Object.FindObjectsByType<CombatEntryPoint>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single();
            SkillDefinitionSO[] definitions = GetRegisteredDefinitions(entryPoint);
            int[] registeredIds = definitions.Select(definition => definition.skillId).ToArray();

            Assert.That(definitions, Has.None.Null, scenePath);
            Assert.That(SkillDefinitionPersistentIdentityValidator.CollectRegistrationIssues(definitions, scenePath), Is.Empty, scenePath);
            Assert.That(registeredIds, Is.SupersetOf(new[] { 1, 2 }),
                $"{scenePath}: Production player fallback skill IDs must resolve from the registry.");

            foreach (CombatSkillLoadoutComponent loadout in UnityEngine.Object.FindObjectsByType<CombatSkillLoadoutComponent>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Assert.That(loadout.SkillIds, Is.Not.Null, loadout.name);
                Assert.That(loadout.SkillIds.All(registeredIds.Contains), Is.True,
                    $"{scenePath}: {loadout.name} contains a Skill ID that is absent from CombatEntryPoint.skillDefinitions.");
            }

            SkillBook book = new();
            foreach (SkillDefinitionSO definition in definitions)
                book.Register(new SoSkill(definition));
            Assert.That(book.Get(new Game.Combat.Model.SkillId(1)), Is.Not.Null);
            Assert.That(book.Get(new Game.Combat.Model.SkillId(2)), Is.Not.Null);
            Assert.That(book.Get(new Game.Combat.Model.SkillId(11)), Is.Not.Null);
            Assert.That(book.Get(new Game.Combat.Model.SkillId(12)), Is.Not.Null);
        }

        private static SkillDefinitionSO[] FindAllSkillDefinitions()
        {
            return AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { "Assets/GAME" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(Load)
                .ToArray();
        }

        private static SkillDefinitionSO[] GetRegisteredDefinitions(CombatEntryPoint entryPoint)
        {
            SerializedProperty property = new SerializedObject(entryPoint).FindProperty("skillDefinitions");
            return Enumerable.Range(0, property.arraySize)
                .Select(index => property.GetArrayElementAtIndex(index).objectReferenceValue as SkillDefinitionSO)
                .ToArray();
        }

        private static SkillDefinitionSO Load(string path)
        {
            SkillDefinitionSO definition = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(path);
            Assert.That(definition, Is.Not.Null, path);
            return definition;
        }

        private static void SetPersistentKey(SkillDefinitionSO definition, string value)
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("persistentKey").stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
