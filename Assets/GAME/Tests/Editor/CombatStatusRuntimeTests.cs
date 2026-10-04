#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatStatusRuntimeTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void RuntimeIdentity_ApplyQueryRemoveAndCombatantIsolationAreStable()
        {
            CombatStatusDefinitionSO status = CreateStatus("focus", CombatStatusStackPolicy.Replace, 1);
            CombatSession session = CreateSession(out DummyCombatant ally, out DummyCombatant enemy);
            CombatantCombatState allyState = session.GetCombatState(ally);
            CombatantCombatState enemyState = session.GetCombatState(enemy);

            CombatStatusApplicationResult applied = allyState.ApplyStatus(status);

            Assert.That(applied.WasAdded, Is.True);
            Assert.That(allyState.HasStatus("focus"), Is.True);
            Assert.That(allyState.TryGetStatus("focus", out CombatStatusRuntime active), Is.True);
            Assert.That(active.StackCount, Is.EqualTo(1));
            Assert.That(enemyState.HasStatus("focus"), Is.False);
            Assert.That(allyState.RemoveStatus("focus"), Is.True);
            Assert.That(allyState.HasStatus("focus"), Is.False);
        }

        [Test]
        public void DuplicateApply_UsesStableIdentityAndNeverExceedsMaximumStacks()
        {
            CombatStatusDefinitionSO stack = CreateStatus("momentum", CombatStatusStackPolicy.Stack, 2);
            CombatStatusDefinitionSO other = CreateStatus("guard", CombatStatusStackPolicy.Stack, 3);
            CombatSession session = CreateSession(out DummyCombatant ally, out _);
            CombatantCombatState state = session.GetCombatState(ally);

            state.ApplyStatus(stack);
            CombatStatusApplicationResult second = state.ApplyStatus(stack);
            CombatStatusApplicationResult third = state.ApplyStatus(stack);
            state.ApplyStatus(other);

            Assert.That(second.CurrentStacks, Is.EqualTo(2));
            Assert.That(third.CurrentStacks, Is.EqualTo(2));
            Assert.That(third.Changed, Is.False);
            Assert.That(state.ActiveStatuses, Has.Count.EqualTo(2));
            Assert.That(state.TryGetStatus("momentum", out CombatStatusRuntime momentum), Is.True);
            Assert.That(momentum.StackCount, Is.EqualTo(2));
        }

        [Test]
        public void SessionIsolation_DoesNotCarryCombatLocalStatusesIntoNewSession()
        {
            CombatStatusDefinitionSO status = CreateStatus("combat-only", CombatStatusStackPolicy.Replace, 1);
            CombatSession first = CreateSession(out DummyCombatant firstAlly, out _);
            first.GetCombatState(firstAlly).ApplyStatus(status);
            CombatSession second = CreateSession(out DummyCombatant secondAlly, out _);

            Assert.That(first.GetCombatState(firstAlly).HasStatus("combat-only"), Is.True);
            Assert.That(second.GetCombatState(secondAlly).HasStatus("combat-only"), Is.False);
        }

        [Test]
        public void SkillRunner_AppliesAuthoredStatusOnceAndReturnsReadOnlyPresentationResult()
        {
            CombatStatusDefinitionSO status = CreateStatus("exposed", CombatStatusStackPolicy.Stack, 1);
            SkillDefinitionSO definition = CreateSkillDefinition(status);
            SoSkill skill = new SoSkill(definition);
            CombatSession session = CreateSession(out DummyCombatant ally, out DummyCombatant enemy);
            ally.AddSkill(skill);
            CombatSkillExecutionRequest request = new CombatSkillExecutionRequest(
                ally,
                skill,
                new[] { enemy },
                enemy,
                CombatClashOutcome.AttackerWin);

            Assert.That(SkillRunner.TryExecute(session, request, out CombatSkillExecutionResult first), Is.True);
            Assert.That(first.AppliedStatusResults, Has.Count.EqualTo(1));
            Assert.That(first.AppliedStatusResults[0].Target, Is.SameAs(enemy));
            Assert.That(session.GetCombatState(enemy).HasStatus("exposed"), Is.True);

            Assert.That(SkillRunner.TryExecute(session, request, out CombatSkillExecutionResult duplicate), Is.True);
            Assert.That(duplicate.AppliedStatusResults, Is.Empty);
            Assert.That(session.GetCombatState(enemy).TryGetStatus("exposed", out CombatStatusRuntime active), Is.True);
            Assert.That(active.StackCount, Is.EqualTo(1));
            Assert.That(((IList<CombatStatusApplicationResult>)first.AppliedStatusResults).IsReadOnly, Is.True);
        }

        [Test]
        public void SkillRunner_InvalidDeadTargetLeavesNoPartialStatus()
        {
            CombatStatusDefinitionSO status = CreateStatus("unsafe", CombatStatusStackPolicy.Replace, 1);
            SkillDefinitionSO definition = CreateSkillDefinition(status);
            SoSkill skill = new SoSkill(definition);
            CombatSession session = CreateSession(out DummyCombatant ally, out DummyCombatant enemy);
            ally.AddSkill(skill);
            enemy.ApplyDamage(int.MaxValue);
            CombatSkillExecutionRequest request = new CombatSkillExecutionRequest(
                ally,
                skill,
                new[] { enemy },
                enemy,
                CombatClashOutcome.AttackerWin);

            Assert.That(SkillRunner.TryExecute(session, request, out _), Is.False);
            Assert.That(session.GetCombatState(enemy).HasStatus("unsafe"), Is.False);
        }

        private CombatSession CreateSession(out DummyCombatant ally, out DummyCombatant enemy)
        {
            ally = new DummyCombatant(1, Side.Allies, 10, KeywordMask.None, 3);
            enemy = new DummyCombatant(100, Side.Enemies, 10, KeywordMask.None, 3);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 3),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                CombatRuntimeConfig.Compatibility);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(CombatRuntimeConfig.Compatibility);
            return session;
        }

        private CombatStatusDefinitionSO CreateStatus(
            string id,
            CombatStatusStackPolicy policy,
            int maximumStacks)
        {
            CombatStatusDefinitionSO definition = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            _createdObjects.Add(definition);
            SetPrivateField(definition, "statusId", id);
            SetPrivateField(definition, "displayName", id);
            SetPrivateField(definition, "stackPolicy", policy);
            SetPrivateField(definition, "maximumStacks", maximumStacks);
            return definition;
        }

        private SkillDefinitionSO CreateSkillDefinition(CombatStatusDefinitionSO status)
        {
            SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            _createdObjects.Add(definition);
            definition.skillId = 900;
            definition.displayName = "Status Test";
            definition.baseDamage = 0;
            SetPrivateField(definition, "appliedStatusEffects", new[] { status });
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
