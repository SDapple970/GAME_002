using System;
using System.Collections.Generic;
using Game.Combat.Data;
using Game.EditorTools;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class EnemySkillAcquisitionDataTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _created) UnityEngine.Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void Validator_RequiresExplicitEnemySourceKeyAndDetectsCanonicalDuplicates()
        {
            EnemyDefinitionSO empty = Enemy(" ");
            EnemyDefinitionSO first = Enemy("enemy.test.a");
            EnemyDefinitionSO duplicate = Enemy(" enemy.test.a ");
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { empty, first, duplicate }, null),
                Has.Some.Contains("empty persistentKey"));
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { first, duplicate }, null),
                Has.Some.Contains("duplicate enemy persistentKey"));
        }

        [Test]
        public void Validator_DetectsEmptyDuplicateUnknownAndAmbiguousSkillMapping()
        {
            SkillDefinitionSO skill = Skill("skill.test.a", 11);
            EnemyDefinitionSO enemy = Enemy("enemy.test.a", " ", "skill.test.a", " skill.test.a ", "skill.unknown");
            IReadOnlyList<string> issues = EnemySkillAcquisitionValidator.CollectIssues(new[] { enemy }, new[] { skill });
            Assert.That(issues, Has.Some.Contains("empty acquirable"));
            Assert.That(issues, Has.Some.Contains("duplicate acquirable"));
            Assert.That(issues, Has.Some.Contains("unresolved or ambiguous"));
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { Enemy("enemy.test.b", "skill.test.a") },
                new[] { skill, Skill("skill.test.a", 12) }), Has.Some.Contains("ambiguous"));
        }

        [Test]
        public void Mapping_UsesPersistentKeyEvenWhenRuntimeIdsMatchDemoAssets()
        {
            SkillDefinitionSO production = Skill("skill.production.a", 1);
            SkillDefinitionSO demo = Skill("skill.demo.a", 1);
            EnemyDefinitionSO enemy = Enemy("enemy.test.a", "skill.production.a");
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { enemy }, new[] { production, demo }), Is.Empty);
            Assert.That(EnemySkillAcquisitionValidator.CollectIssues(new[] { enemy }, new[] { demo }),
                Has.Some.Contains("unresolved"));
        }

        [Test]
        public void Validation_IsReadOnlyAndAuthoredMappingCannotBeMutatedThroughProperty()
        {
            EnemyDefinitionSO enemy = Enemy("enemy.test.a", "skill.test.a");
            string before = EditorJsonUtility.ToJson(enemy);
            EnemySkillAcquisitionValidator.CollectIssues(new[] { enemy }, null);
            Assert.That(EditorJsonUtility.ToJson(enemy), Is.EqualTo(before));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)enemy.AcquirableSkillPersistentKeys)[0] = "changed");
        }

        private EnemyDefinitionSO Enemy(string key, params string[] mappedKeys)
        {
            EnemyDefinitionSO enemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(enemy);
            SerializedObject serialized = new(enemy);
            serialized.FindProperty("persistentKey").stringValue = key;
            SerializedProperty keys = serialized.FindProperty("acquirableSkillPersistentKeys");
            keys.arraySize = mappedKeys.Length;
            for (int i = 0; i < mappedKeys.Length; i++) keys.GetArrayElementAtIndex(i).stringValue = mappedKeys[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private SkillDefinitionSO Skill(string key, int id)
        {
            SkillDefinitionSO skill = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            _created.Add(skill);
            skill.skillId = id;
            SerializedObject serialized = new(skill);
            serialized.FindProperty("persistentKey").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }
    }
}
