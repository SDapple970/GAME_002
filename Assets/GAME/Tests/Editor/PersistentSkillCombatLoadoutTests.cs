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
using Game.NonCombat.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class PersistentSkillCombatLoadoutTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            DestroyExistingParticipants();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            DestroyExistingParticipants();
        }

        [Test]
        public void Bridge_ResolvesPersistentKeysIntoIndependentImmutableCombatSnapshots()
        {
            CharacterSkillRuntime runtime = CreateParticipant().Runtime;
            runtime.TryAcquire("hero.a", "skill.production.a");
            runtime.TryAcquire("hero.a", "skill.production.b");
            runtime.TryEquip("hero.a", "skill.production.a");
            runtime.TryEquip("hero.a", "skill.production.b");
            runtime.TryAcquire("hero.b", "skill.production.b");
            runtime.TryEquip("hero.b", "skill.production.b");

            SkillDefinitionSO skillA = CreateSkill("skill.production.a", 11);
            SkillDefinitionSO skillB = CreateSkill("skill.production.b", 12);
            GameObject allyA = CreateActor("AllyA");
            GameObject allyB = CreateActor("AllyB");
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.AllyFieldObjects.AddRange(new[] { allyA, allyB });
            request.BindAllyCharacter(allyA, "hero.a");
            request.BindAllyCharacter(allyB, "hero.b");

            PersistentSkillCombatLoadoutBridge.ApplyToRequest(request, new[] { skillA, skillB }, null);

            Assert.That(request.TryGetAllyLoadoutSnapshot(allyA, out CombatSkillLoadoutSnapshot first), Is.True);
            Assert.That(request.TryGetAllyLoadoutSnapshot(allyB, out CombatSkillLoadoutSnapshot second), Is.True);
            Assert.That(first.Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 11, 12 }));
            Assert.That(second.Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 12 }));

            runtime.TryUnequip("hero.a", "skill.production.a");
            Assert.That(first.Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 11, 12 }));
        }

        [Test]
        public void Bridge_OmitsUnresolvedAndDuplicateRuntimeIdsWithoutSelectingTheWrongDefinition()
        {
            CharacterSkillRuntime runtime = CreateParticipant().Runtime;
            runtime.TryAcquire("hero", "skill.good");
            runtime.TryAcquire("hero", "skill.demo.duplicate-id");
            runtime.TryAcquire("hero", "skill.unresolved");
            runtime.TryEquip("hero", "skill.good");
            runtime.TryEquip("hero", "skill.demo.duplicate-id");
            runtime.TryEquip("hero", "skill.unresolved");

            SkillDefinitionSO production = CreateSkill("skill.good", 21);
            SkillDefinitionSO demo = CreateSkill("skill.demo.duplicate-id", 21);
            GameObject ally = CreateActor("Ally");
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.AllyFieldObjects.Add(ally);
            request.BindAllyCharacter(ally, "hero");

            PersistentSkillCombatLoadoutBridge.ApplyToRequest(request, new[] { production, demo }, null);

            Assert.That(request.TryGetAllyLoadoutSnapshot(ally, out CombatSkillLoadoutSnapshot snapshot), Is.True);
            Assert.That(snapshot.Skills, Has.Count.EqualTo(1));
            Assert.That(((SoSkill)snapshot.Skills[0]).Definition, Is.SameAs(production));
            Assert.That(runtime.IsEquipped("hero", "skill.unresolved"), Is.True);
        }

        [Test]
        public void Factory_UsesSnapshotBeforeSerializedCompatibilityLoadout()
        {
            CharacterSkillRuntime runtime = CreateParticipant().Runtime;
            runtime.TryAcquire("hero", "skill.equipped");
            runtime.TryEquip("hero", "skill.equipped");
            SkillDefinitionSO equipped = CreateSkill("skill.equipped", 31);
            SkillDefinitionSO compatibility = CreateSkill("skill.compatibility", 1);
            GameObject ally = CreateActor("Ally");
            GameObject enemy = CreateActor("Enemy");
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null);
            request.AllyFieldObjects.Add(ally);
            request.EnemyFieldObjects.Add(enemy);
            request.BindAllyCharacter(ally, "hero");
            PersistentSkillCombatLoadoutBridge.ApplyToRequest(request, new[] { equipped, compatibility }, null);

            SkillBook book = new();
            book.Register(new SoSkill(equipped));
            book.Register(new SoSkill(compatibility));
            CombatSession session = new(StartReason.PlayerFirstHit, Side.Allies, new InspirationPool(10, 3), new Game.Combat.Environment.CombatEnvironment());
            new FieldCombatantFactory(book).PopulateCombatants(session, request);

            Assert.That(session.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 31 }));
            runtime.TryUnequip("hero", "skill.equipped");
            Assert.That(session.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 31 }));
        }

        private CharacterSkillSaveParticipant CreateParticipant()
        {
            GameObject gameObject = new("CharacterSkills");
            _created.Add(gameObject);
            CharacterSkillSaveParticipant participant = gameObject.AddComponent<CharacterSkillSaveParticipant>();
            typeof(CharacterSkillSaveParticipant)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(participant, null);
            Assert.That(CharacterSkillSaveParticipant.Instance, Is.SameAs(participant));
            return participant;
        }

        private SkillDefinitionSO CreateSkill(string persistentKey, int skillId)
        {
            SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            SerializedObject serialized = new(definition);
            serialized.FindProperty("persistentKey").stringValue = persistentKey;
            serialized.FindProperty("skillId").intValue = skillId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _created.Add(definition);
            return definition;
        }

        private GameObject CreateActor(string name)
        {
            GameObject actor = new(name);
            CombatHpComponent hp = actor.AddComponent<CombatHpComponent>();
            hp.MaxHP = 10;
            hp.HP = 10;
            _created.Add(actor);
            return actor;
        }

        private static void DestroyExistingParticipants()
        {
            foreach (CharacterSkillSaveParticipant participant in Resources.FindObjectsOfTypeAll<CharacterSkillSaveParticipant>())
                if (participant != null && !EditorUtility.IsPersistent(participant))
                    UnityEngine.Object.DestroyImmediate(participant.gameObject);
        }
    }
}
