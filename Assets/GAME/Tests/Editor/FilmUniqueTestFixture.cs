using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public abstract class FilmUniqueTestFixture
    {
        private readonly List<UnityEngine.Object> _created = new();
        protected CharacterSkillSaveParticipant Participant;
        protected PartyRuntime Party;
        protected GameStateMachine State;
        protected GameFlowController Flow;
        protected CombatEntryPoint Entry;

        [SetUp]
        public void SetUp()
        {
            CleanupRuntime();
            State = Component<GameStateMachine>();
            Flow = Component<GameFlowController>();
            Party = Component<PartyRuntime>();
            Party.AddMember("hero.a");
            Party.AddMember("hero.b");
            Participant = Component<CharacterSkillSaveParticipant>();
            Entry = Component<CombatEntryPoint>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            CleanupRuntime();
        }

        protected T Component<T>() where T : MonoBehaviour
        {
            GameObject go = new(typeof(T).Name);
            _created.Add(go);
            T component = go.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }

        protected T Asset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        protected GameObject Actor()
        {
            GameObject actor = new("Test actor");
            actor.AddComponent<CombatHpComponent>();
            _created.Add(actor);
            return actor;
        }

        protected SkillDefinitionSO Skill(string key, int id, SkillOwnershipCategory category = SkillOwnershipCategory.Compatibility, string owner = null)
        {
            SkillDefinitionSO skill = Asset<SkillDefinitionSO>();
            skill.skillId = id;
            SerializedObject data = new(skill);
            data.FindProperty("persistentKey").stringValue = key;
            data.FindProperty("ownershipCategory").enumValueIndex = (int)category;
            data.FindProperty("uniqueOwnerCharacterId").stringValue = owner;
            data.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        protected void Register(params SkillDefinitionSO[] definitions)
        {
            SerializedObject data = new(Entry);
            SerializedProperty skills = data.FindProperty("skillDefinitions");
            skills.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++) skills.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            Invoke(Entry, "Awake");
        }

        protected CombatStartRequest Request(params string[] characterIds)
        {
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            foreach (string id in characterIds)
            {
                GameObject ally = Actor();
                request.AllyFieldObjects.Add(ally);
                request.BindAllyCharacter(ally, id);
            }
            request.EnemyFieldObjects.Add(Actor());
            return request;
        }

        protected static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

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
