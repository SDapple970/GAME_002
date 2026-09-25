#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class EnemyCombatPolicyTests
    {
        [Test]
        public void DeterministicCycle_RepeatsForSameTurnAndAdvancesWithTurnIndex()
        {
            DummyCombatant player = Combatant(1, Side.Allies);
            DummyCombatant enemy = Combatant(100, Side.Enemies);
            TestSkill first = Skill(11, TargetingRule.SingleAlly);
            TestSkill second = Skill(12, TargetingRule.SingleAlly);
            enemy.AddSkill(first);
            enemy.AddSkill(second);
            CombatSession session = CreateLegacySession(player, enemy);
            DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();

            Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan firstPlan), Is.True);
            Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan repeatedPlan), Is.True);
            Assert.That(firstPlan.Slot1.SkillId.Value, Is.EqualTo(second.Id.Value));
            Assert.That(repeatedPlan.Slot1.SkillId.Value, Is.EqualTo(second.Id.Value));

            session.CurrentTurn.CompleteForExit();
            session.BeginNewTurn();

            Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan nextPlan), Is.True);
            Assert.That(nextPlan.Slot1.SkillId.Value, Is.EqualTo(first.Id.Value));
        }

        [Test]
        public void SingleSkill_AndNoValidSkills_ProduceExpectedPlans()
        {
            DummyCombatant player = Combatant(1, Side.Allies);
            DummyCombatant singleSkillEnemy = Combatant(100, Side.Enemies);
            TestSkill skill = Skill(11, TargetingRule.SingleAlly);
            singleSkillEnemy.AddSkill(skill);
            DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();
            CombatSession singleSkillSession = CreateLegacySession(player, singleSkillEnemy);

            Assert.That(policy.TryCreatePlan(
                new EnemyCombatPlanRequest(singleSkillSession, singleSkillEnemy), out ActionPlan singleSkillPlan), Is.True);
            Assert.That(singleSkillPlan.Slot1.SkillId.Value, Is.EqualTo(skill.Id.Value));
            Assert.That(singleSkillPlan.Slot1.TargetCombatantId.Value, Is.EqualTo(player.Id.Value));

            DummyCombatant noSkillPlayer = Combatant(2, Side.Allies);
            DummyCombatant noSkillEnemy = Combatant(101, Side.Enemies);
            CombatSession noSkillSession = CreateLegacySession(noSkillPlayer, noSkillEnemy);
            Assert.That(policy.TryCreatePlan(
                new EnemyCombatPlanRequest(noSkillSession, noSkillEnemy), out ActionPlan noSkillPlan), Is.True);
            Assert.That(noSkillPlan.Slot1.IsNone, Is.True);

            DummyCombatant invalidSkillPlayer = Combatant(3, Side.Allies);
            DummyCombatant invalidSkillEnemy = Combatant(102, Side.Enemies);
            invalidSkillEnemy.AddSkill(null);
            CombatSession invalidSkillSession = CreateLegacySession(invalidSkillPlayer, invalidSkillEnemy);
            Assert.That(policy.TryCreatePlan(
                new EnemyCombatPlanRequest(invalidSkillSession, invalidSkillEnemy), out ActionPlan invalidSkillPlan), Is.True);
            Assert.That(invalidSkillPlan.Slot1.IsNone, Is.True);
        }

        [Test]
        public void TargetSelection_SkipsDeadRuntimeCombatants()
        {
            DummyCombatant firstAlly = Combatant(1, Side.Allies);
            DummyCombatant secondAlly = Combatant(2, Side.Allies);
            DummyCombatant enemy = Combatant(100, Side.Enemies);
            enemy.AddSkill(Skill(11, TargetingRule.SingleAlly));
            CombatSession session = CreateLegacySession(firstAlly, enemy, secondAlly);
            session.GetCombatState(firstAlly).ApplyDamage(999);

            DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();
            Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan plan), Is.True);
            Assert.That(plan.Slot1.TargetCombatantId.Value, Is.EqualTo(secondAlly.Id.Value));
        }

        [Test]
        public void TargetSelection_UsesSessionSnapshotInsteadOfFieldHp()
        {
            GameObject fieldObject = new GameObject("Runtime State Target");
            try
            {
                CombatHpComponent fieldHp = fieldObject.AddComponent<CombatHpComponent>();
                fieldHp.MaxHP = 15;
                fieldHp.HP = 15;
                FieldCombatantAdapter fieldAlly = new FieldCombatantAdapter(
                    1,
                    Side.Allies,
                    fieldObject,
                    HpAccessor.TryCreate(fieldObject),
                    10);
                DummyCombatant enemy = Combatant(100, Side.Enemies);
                enemy.AddSkill(Skill(11, TargetingRule.SingleAlly));
                CombatSession session = CreateLegacySession(fieldAlly, enemy);
                fieldHp.HP = 0;

                DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();
                Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan plan), Is.True);
                Assert.That(session.GetCombatState(fieldAlly).CurrentHp, Is.EqualTo(15));
                Assert.That(plan.Slot1.TargetCombatantId.Value, Is.EqualTo(fieldAlly.Id.Value));
            }
            finally
            {
                Object.DestroyImmediate(fieldObject);
            }
        }

        [Test]
        public void StandoffDecision_UsesTheSamePolicyAndRequiresAnOpposingTarget()
        {
            DummyCombatant player = Combatant(1, Side.Allies);
            DummyCombatant enemy = Combatant(100, Side.Enemies);
            TestSkill skill = Skill(11, TargetingRule.SingleAlly);
            enemy.AddSkill(skill);
            CombatSession session = CreateStandoffSession(player, enemy);
            DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();

            Assert.That(policy.TryCreateDeclaration(new EnemyCombatDecisionRequest(session), out CombatAttackDeclaration declaration), Is.True);
            Assert.That(declaration.Attacker, Is.SameAs(enemy));
            Assert.That(declaration.Target, Is.SameAs(player));
            Assert.That(declaration.Skill, Is.SameAs(skill));

            CombatStateMachine stateMachine = new CombatStateMachine(session);
            stateMachine.Tick();
            stateMachine.Tick(1f);
            Assert.That(stateMachine.TrySubmitEnemyDecision(policy), Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));

            DummyCombatant isolatedEnemy = Combatant(101, Side.Enemies);
            isolatedEnemy.AddSkill(skill);
            CombatSession isolatedSession = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Enemies,
                new InspirationPool(10, 0),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(0, 0, 0, 0, 0f, 1f, 1f));
            isolatedSession.Enemies.Add(isolatedEnemy);
            isolatedSession.InitializeCombatStates(isolatedSession.RuntimeConfig);

            Assert.That(policy.TryCreateDeclaration(
                new EnemyCombatDecisionRequest(isolatedSession), out CombatAttackDeclaration noTargetDeclaration), Is.False);
            Assert.That(noTargetDeclaration, Is.Null);
        }

        private static CombatSession CreateLegacySession(
            ICombatant firstAlly,
            ICombatant enemy,
            ICombatant additionalAlly = null)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 0),
                new CombatEnvironment());
            session.Allies.Add(firstAlly);
            if (additionalAlly != null)
                session.Allies.Add(additionalAlly);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(session.RuntimeConfig);
            session.BeginNewTurn();
            return session;
        }

        private static CombatSession CreateStandoffSession(ICombatant player, ICombatant enemy)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Enemies,
                new InspirationPool(10, 0),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(0, 0, 0, 0, 0f, 1f, 1f));
            session.Allies.Add(player);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(session.RuntimeConfig);
            return session;
        }

        private static DummyCombatant Combatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 20, KeywordMask.None, 10);
        }

        private static TestSkill Skill(int id, TargetingRule targeting)
        {
            return new TestSkill(id, targeting);
        }

        private sealed class TestSkill : ISkill
        {
            public SkillId Id { get; }
            public string Name => $"Test Skill {Id.Value}";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting { get; }
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 1f;
            public float MoveSpeed => 1f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;

            public TestSkill(int id, TargetingRule targeting)
            {
                Id = new SkillId(id);
                Targeting = targeting;
            }
        }
    }
}
#endif
