#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatFinalSkillDataMigrationTests
    {
        private const string PlayerBasicPath = "Assets/GAME/Data/Skill/Skill_BasicAttack.asset";
        private const string PlayerSkillPath = "Assets/GAME/Data/Skill/Skill_skill2.asset";
        private const string EnemyBasicPath = "Assets/GAME/Data/Skill/Angel_Skill.asset";
        private const string EnemySkillPath = "Assets/GAME/Data/Skill/Angel_Skill2.asset";

        [Test]
        public void ProductionSkills_HaveExpectedFinalDataWithoutDamageOrSpeedChanges()
        {
            AssertSkill(PlayerBasicPath, 1, 1, 2, TargetingRule.SingleEnemy, 2, 10);
            AssertSkill(PlayerSkillPath, 2, 2, 3, TargetingRule.AllEnemies, 4, 5);
            AssertSkill(EnemyBasicPath, 11, 1, 2, TargetingRule.SingleEnemy, 1, 5);
            AssertSkill(EnemySkillPath, 12, 2, 3, TargetingRule.SingleEnemy, 1, 5);
        }

        [Test]
        public void SoSkill_AndAuthoredProvider_UseFinalStatsNotDamage()
        {
            SkillDefinitionSO definition = Load(PlayerSkillPath);
            SoSkill skill = new SoSkill(definition);
            DummyCombatant actor = Combatant(1, Side.Allies);
            AuthoredCombatClashSideInputProvider provider = new AuthoredCombatClashSideInputProvider();

            Assert.That(provider.TryCreate(actor, skill, true, out CombatClashSideInput input), Is.True);
            Assert.That(input.ClashPower, Is.EqualTo(3));
            Assert.That(input.ClashPower, Is.Not.EqualTo(skill.BaseDamage));
            Assert.That(input.Speed, Is.EqualTo(5));
            Assert.That(input.ClashModifier, Is.Zero);
            Assert.That(input.IsCurrentAttackOwner, Is.True);
        }

        [Test]
        public void AuthoredProvider_RejectsSkillsWithoutFinalStats()
        {
            AuthoredCombatClashSideInputProvider provider = new AuthoredCombatClashSideInputProvider();
            DummyCombatant actor = Combatant(1, Side.Allies);

            Assert.That(provider.TryCreate(actor, new CompatibilitySkill(99, 4), true, out _), Is.False);
        }

        [Test]
        public void MpResolver_PrefersFinalCostAndRetainsCompatibilityFallback()
        {
            Assert.That(CombatMpCostResolver.Resolve(new SoSkill(Load(PlayerBasicPath))), Is.EqualTo(1));
            Assert.That(CombatMpCostResolver.Resolve(new SoSkill(Load(PlayerSkillPath))), Is.EqualTo(2));
            Assert.That(CombatMpCostResolver.Resolve(new CompatibilitySkill(99, 4)), Is.EqualTo(4));
            Assert.That(CombatMpCostResolver.Resolve(new FinalStatsSkill(100, 4, 0, 2)), Is.Zero);
        }

        [Test]
        public void LegacyPlanning_UsesActorRelativeSingleEnemyAndSingleAlly()
        {
            SoSkill playerAttack = new SoSkill(Load(PlayerBasicPath));
            SoSkill enemyAttack = new SoSkill(Load(EnemyBasicPath));
            DummyCombatant player = Combatant(1, Side.Allies);
            DummyCombatant enemy = Combatant(11, Side.Enemies);
            player.AddSkill(playerAttack);
            enemy.AddSkill(enemyAttack);
            CombatSession session = CreateLegacySession(player, enemy);
            DeterministicCycleEnemyCombatPolicy policy = new DeterministicCycleEnemyCombatPolicy();

            Assert.That(policy.TryCreatePlan(new EnemyCombatPlanRequest(session, enemy), out ActionPlan enemyPlan), Is.True);
            Assert.That(enemyPlan.Slot1.TargetCombatantId.Value, Is.EqualTo(player.Id.Value));
            Assert.That(CombatPlanValidator.TryNormalizePlan(session, enemy, enemyPlan, out _, out _), Is.True);

            ActionPlan playerPlan = new ActionPlan(new PlannedAction(
                playerAttack.Id, playerAttack.Tag, playerAttack.Targeting, enemy.Id, playerAttack.Speed, playerAttack.ConsumesTurn),
                PlannedAction.None);
            Assert.That(CombatPlanValidator.TryNormalizePlan(session, player, playerPlan, out _, out _), Is.True);

            CompatibilitySkill allySkill = new CompatibilitySkill(101, 0, TargetingRule.SingleAlly);
            player.AddSkill(allySkill);
            enemy.AddSkill(allySkill);
            Assert.That(CombatPlanValidator.TryNormalizePlan(session, player,
                PlanFor(player, allySkill), out ActionPlan playerAllyPlan, out _), Is.True);
            Assert.That(CombatPlanValidator.TryNormalizePlan(session, enemy,
                PlanFor(enemy, allySkill), out ActionPlan enemyAllyPlan, out _), Is.True);
            Assert.That(playerAllyPlan.Slot1.TargetCombatantId.Value, Is.EqualTo(player.Id.Value));
            Assert.That(enemyAllyPlan.Slot1.TargetCombatantId.Value, Is.EqualTo(enemy.Id.Value));
        }

        [Test]
        public void FinalExchange_DefaultBinding_UsesMigratedMpAndAuthoredClashPower()
        {
            DummyCombatant player = Combatant(1, Side.Allies);
            DummyCombatant enemy = Combatant(11, Side.Enemies);
            SoSkill playerSkill = new SoSkill(Load(PlayerBasicPath));
            SoSkill enemySkill = new SoSkill(Load(EnemyBasicPath));
            player.AddSkill(playerSkill);
            enemy.AddSkill(enemySkill);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(3, 3, 3, 0));
            session.Allies.Add(player);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            GameObject owner = new GameObject("CombatFinalSkillDataMigrationTests.Orchestrator");

            try
            {
                CombatFlowOrchestrator orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
                CombatApproachPresentationRequest approach = null;
                orchestrator.ApproachPresentationRequested += request => approach = request;

                Assert.That(orchestrator.BindFinalExchange(session, stateMachine, new ZeroRandomSource()), Is.True);
                int attackVersion = orchestrator.PendingDecisionRequest.ExchangeVersion;
                Assert.That(orchestrator.SubmitAttackDeclaration(
                    new CombatAttackDeclaration(player, enemy, playerSkill), attackVersion), Is.True);
                int responseVersion = orchestrator.PendingDecisionRequest.ExchangeVersion;
                Assert.That(orchestrator.SubmitResponse(
                    new CombatResponseDeclaration(enemy, enemySkill), responseVersion), Is.True);
                Assert.That(approach, Is.Not.Null);
                Assert.That(approach.TryComplete(), Is.True);

                Assert.That(session.ExchangeState.CurrentClashResult, Is.Not.Null);
                Assert.That(session.ExchangeState.CurrentClashResult.Winner, Is.SameAs(player));
                Assert.That(session.GetCombatState(player).CurrentMp, Is.EqualTo(2));
                Assert.That(session.GetCombatState(enemy).CurrentMp, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static ActionPlan PlanFor(ICombatant actor, ISkill skill)
        {
            return new ActionPlan(new PlannedAction(
                skill.Id, skill.Tag, skill.Targeting, actor.Id, skill.Speed, skill.ConsumesTurn), PlannedAction.None);
        }

        private static CombatSession CreateLegacySession(ICombatant player, ICombatant enemy)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.LegacyPlanning,
                new CombatRuntimeConfig(3, 3, 0, 0));
            session.Allies.Add(player);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(session.RuntimeConfig);
            return session;
        }

        private static DummyCombatant Combatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 10, KeywordMask.None, 0);
        }

        private static SkillDefinitionSO Load(string path)
        {
            SkillDefinitionSO skill = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(path);
            Assert.That(skill, Is.Not.Null, $"Missing production SkillDefinitionSO at {path}.");
            return skill;
        }

        private static void AssertSkill(
            string path,
            int id,
            int finalMpCost,
            int clashPower,
            TargetingRule targeting,
            int damage,
            int speed)
        {
            SkillDefinitionSO skill = Load(path);
            Assert.That(skill.skillId, Is.EqualTo(id));
            Assert.That(skill.FinalMpCost, Is.EqualTo(finalMpCost));
            Assert.That(skill.ClashPower, Is.EqualTo(clashPower));
            Assert.That(skill.targeting, Is.EqualTo(targeting));
            Assert.That(skill.baseDamage, Is.EqualTo(damage));
            Assert.That(skill.speed, Is.EqualTo(speed));
        }

        private class CompatibilitySkill : ISkill, ICombatMpCostProvider
        {
            public CompatibilitySkill(int id, int mpCost, TargetingRule targeting = TargetingRule.SingleEnemy)
            {
                Id = new SkillId(id);
                MpCost = mpCost;
                Targeting = targeting;
            }

            public SkillId Id { get; }
            public string Name => Id.Value.ToString();
            public int InspirationCost => 0;
            public int MpCost { get; }
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting { get; }
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
        }

        private sealed class FinalStatsSkill : CompatibilitySkill, IFinalCombatSkillStats
        {
            public FinalStatsSkill(int id, int mpCost, int finalMpCost, int clashPower)
                : base(id, mpCost)
            {
                FinalMpCost = finalMpCost;
                ClashPower = clashPower;
            }

            public int FinalMpCost { get; }
            public int ClashPower { get; }
        }

        private sealed class ZeroRandomSource : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive)
            {
                return 0;
            }
        }
    }
}
#endif
