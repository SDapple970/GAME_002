using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class EnemyCombatProvenanceTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp() => CleanupRuntime();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            CleanupRuntime();
        }

        [Test]
        public void CanonicalEntry_PreservesRequestSourcesAndRecipientThroughNormalizationAndResult()
        {
            Component<GameStateMachine>();
            Component<GameFlowController>();
            CombatEntryPoint entry = Component<CombatEntryPoint>();
            GameObject ally = Actor();
            GameObject defeated = Actor();
            GameObject surviving = Actor();
            CombatStartRequest request = Request(ally, defeated, surviving);
            request.AllyFieldObjects.Add(ally); // Canonical entry removes duplicate field objects.
            request.SetEnemySourceSnapshot(defeated, new EnemySourceSnapshot("enemy.test.a", new[] { "skill.test.a" }));
            request.SetEnemySourceSnapshot(surviving, new EnemySourceSnapshot("enemy.test.b", new[] { "skill.test.b" }));

            Assert.That(entry.StartCombat(request), Is.True);
            CombatSession session = entry.ActiveSession;
            session.Enemies[0].ApplyDamage(10);
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);

            Assert.That(result.SkillAcquisitionRecipientCharacterId, Is.EqualTo("hero.test"));
            Assert.That(result.DefeatedEnemyIds, Is.EqualTo(new[] { session.Enemies[0].Id.Value }));
            Assert.That(result.DefeatedEnemySources, Has.Count.EqualTo(1));
            Assert.That(result.DefeatedEnemySources.Single().CombatantId, Is.EqualTo(session.Enemies[0].Id.Value));
            Assert.That(result.DefeatedEnemySources.Single().Source.SourceKey, Is.EqualTo("enemy.test.a"));
            Assert.That(result.DefeatedEnemySources.Single().Source.AcquirableSkillPersistentKeys, Is.EqualTo(new[] { "skill.test.a" }));
        }

        [Test]
        public void AuthoredSource_CapturesImmutableSnapshotBeforeAssetAndFieldObjectChanges()
        {
            GameObject ally = Actor();
            GameObject enemy = Actor();
            EnemyDefinitionSO definition = Definition(" enemy.test.a ", "skill.z", "skill.a", " skill.z ", " ");
            EnemySourceComponent source = enemy.AddComponent<EnemySourceComponent>();
            SerializedObject serializedSource = new(source);
            serializedSource.FindProperty("definition").objectReferenceValue = definition;
            serializedSource.ApplyModifiedPropertiesWithoutUndo();
            CombatStartRequest request = Request(ally, enemy);
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);
            CombatSession session = Build(request);

            SerializedObject serializedDefinition = new(definition);
            serializedDefinition.FindProperty("persistentKey").stringValue = "changed";
            serializedDefinition.FindProperty("acquirableSkillPersistentKeys").ClearArray();
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            session.Enemies[0].ApplyDamage(10);
            UnityEngine.Object.DestroyImmediate(enemy);

            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);
            EnemySourceSnapshot snapshot = result.DefeatedEnemySources.Single().Source;
            Assert.That(snapshot.SourceKey, Is.EqualTo("enemy.test.a"));
            Assert.That(snapshot.AcquirableSkillPersistentKeys, Is.EqualTo(new[] { "skill.z", "skill.a" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.AcquirableSkillPersistentKeys)[0] = "mutated");
            Assert.Throws<NotSupportedException>(() => ((IList<CombatDefeatedEnemyRecord>)result.DefeatedEnemySources).Clear());
        }

        [Test]
        public void SourcesForNonParticipantsAndSurvivorsAreExcludedAndLegacyEnemiesStillWork()
        {
            GameObject enemy = Actor();
            GameObject outsider = Actor();
            CombatStartRequest request = Request(Actor(), enemy);
            request.SetEnemySourceSnapshot(outsider, new EnemySourceSnapshot("enemy.outsider", new[] { "skill.test" }));
            CombatSession session = Build(request);
            Assert.That(CombatResultBuilder.Build(session, CombatEndReason.Victory).DefeatedEnemySources, Is.Empty);
            session.Enemies[0].ApplyDamage(10);
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);
            Assert.That(result.DefeatedEnemyIds, Has.Count.EqualTo(1));
            Assert.That(result.DefeatedEnemySources, Is.Empty);
        }

        [Test]
        public void MissingOrMultiCharacterRecipientRequiresExplicitBoundIdentity()
        {
            GameObject first = Actor();
            GameObject second = Actor();
            CombatStartRequest request = Request(first, Actor());
            request.AllyFieldObjects.Add(second);
            request.BindAllyCharacter(second, "hero.other");
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);
            Assert.That(request.SkillAcquisitionRecipientCharacterId, Is.Null);
            request.SetSkillAcquisitionRecipient(" hero.other ");
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);
            Assert.That(request.SkillAcquisitionRecipientCharacterId, Is.EqualTo("hero.other"));
            request.SetSkillAcquisitionRecipient("not.participating");
            EnemyCombatProvenanceBridge.PrepareRequest(request, null);
            Assert.That(request.SkillAcquisitionRecipientCharacterId, Is.Null);
        }

        private CombatSession Build(CombatStartRequest request)
        {
            SkillDefinitionSO skill = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            skill.skillId = 1;
            _created.Add(skill);
            SkillBook book = new();
            book.Register(new SoSkill(skill));
            return CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book)).session;
        }

        private static CombatStartRequest Request(GameObject ally, params GameObject[] enemies)
        {
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.AllyFieldObjects.Add(ally);
            request.BindAllyCharacter(ally, "hero.test");
            request.EnemyFieldObjects.AddRange(enemies);
            return request;
        }

        private EnemyDefinitionSO Definition(string key, params string[] skills)
        {
            EnemyDefinitionSO definition = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            _created.Add(definition);
            SerializedObject serialized = new(definition);
            serialized.FindProperty("persistentKey").stringValue = key;
            SerializedProperty values = serialized.FindProperty("acquirableSkillPersistentKeys");
            values.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++) values.GetArrayElementAtIndex(i).stringValue = skills[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private GameObject Actor()
        {
            GameObject actor = new("Unrelated name");
            actor.AddComponent<CombatHpComponent>();
            _created.Add(actor);
            return actor;
        }

        private T Component<T>() where T : MonoBehaviour
        {
            GameObject go = new(typeof(T).Name);
            _created.Add(go);
            T component = go.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }

        private static void CleanupRuntime()
        {
            HashSet<GameObject> objects = new();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (behaviour != null && !EditorUtility.IsPersistent(behaviour) &&
                    behaviour.GetType().Namespace?.StartsWith("Game.", StringComparison.Ordinal) == true)
                    objects.Add(behaviour.gameObject);
            foreach (GameObject go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
