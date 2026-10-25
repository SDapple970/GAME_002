#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    public sealed class CombatClashSeriesCoreTests
    {
        // Durability, power, cost, and comparison limits in these tests are fixtures, not balance values.
        [Test]
        public void ThreeComparisons_OnlyFinalClaimCanApplyHpOnce()
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 4, 3, 3);
            int hpBefore = fixture.Enemy.HP;
            int postureBefore = fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture;
            int mentalBefore = fixture.Session.GetCombatState(fixture.Enemy).CurrentMental;

            Assert.That(Resolve(fixture, series, 3, 2).IsComplete, Is.False);
            Assert.That(series.ResponderDurability, Is.EqualTo(2));
            Assert.That(series.AttackMpSpent, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(Commit(fixture, series, fixture.AllySkill, fixture.EnemySkill).Succeeded, Is.True);
            Assert.That(Resolve(fixture, series, 2, 3).IsComplete, Is.False);
            Assert.That(series.AttackerDurability, Is.EqualTo(3));
            Commit(fixture, series, fixture.AllySkill, fixture.EnemySkill);
            Assert.That(Resolve(fixture, series, 4, 2).FinalOutcome,
                Is.EqualTo(CombatClashOutcome.AttackerWin));

            Assert.That(series.ComparisonCount, Is.EqualTo(3));
            Assert.That(series.AttackMpSpent, Is.EqualTo(3));
            Assert.That(series.ResponseMpSpent, Is.EqualTo(3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpBefore));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(postureBefore));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental, Is.EqualTo(mentalBefore));

            long claimToken = series.CommandToken;
            Assert.That(series.TryClaimFinalOutcome(claimToken, fixture.AllySkill, out CombatClashResult final), Is.True);
            Assert.That(CombatOutcomeSelector.TrySelect(final, out CombatOutcomeAction action), Is.True);
            Assert.That(CombatOutcomeTargetResolver.TryResolve(action, fixture.Session, out CombatSkillExecutionRequest request), Is.True);
            Assert.That(SkillRunner.TryExecute(fixture.Session, request, out CombatSkillExecutionResult execution), Is.True);
            Assert.That(execution, Is.Not.Null);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpBefore - fixture.AllySkill.BaseDamage));
            Assert.That(series.TryClaimFinalOutcome(claimToken, fixture.AllySkill, out _), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
        }

        [Test]
        public void PowerDifferenceAndTieOnlyReduceDurability()
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 5, 5, 3);

            CombatClashRoundResult first = Resolve(fixture, series, 6, 2);
            Assert.That(first.ResponderDurabilityAfter, Is.EqualTo(1));
            Assert.That(first.AttackerDurabilityAfter, Is.EqualTo(5));
            Commit(fixture, series, fixture.AllySkill, fixture.EnemySkill);
            CombatClashRoundResult tied = Resolve(fixture, series, 2, 2);
            Assert.That(tied.AttackerDurabilityAfter, Is.EqualTo(4));
            Assert.That(tied.ResponderDurabilityAfter, Is.Zero);
            Assert.That(tied.FinalOutcome, Is.EqualTo(CombatClashOutcome.AttackerWin));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
        }

        [TestCase(2, 3, CombatClashOutcome.ResponderWin)]
        [TestCase(3, 2, CombatClashOutcome.AttackerWin)]
        [TestCase(2, 2, CombatClashOutcome.AttackerWin)]
        public void SimultaneousZero_UsesSpeedThenAttackAuthority(
            int attackerSpeed,
            int responderSpeed,
            CombatClashOutcome expected)
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 1, 1, 2);

            CombatClashRoundResult result = Resolve(
                fixture, series, 2, 2, attackerSpeed, responderSpeed);

            Assert.That(result.AttackerDurabilityAfter, Is.Zero);
            Assert.That(result.ResponderDurabilityAfter, Is.Zero);
            Assert.That(result.FinalOutcome, Is.EqualTo(expected));
        }

        [Test]
        public void ComparisonLimit_EndsInDrawWithoutAttackOrRewardRequest()
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 5, 5, 2);
            Resolve(fixture, series, 2, 2);
            Commit(fixture, series, fixture.AllySkill, fixture.EnemySkill);

            CombatClashRoundResult last = Resolve(fixture, series, 2, 2);

            Assert.That(last.FinalOutcome, Is.EqualTo(CombatClashOutcome.Tie));
            Assert.That(series.CanClaimFinalAttack, Is.False);
            Assert.That(series.TryClaimFinalOutcome(series.CommandToken, fixture.AllySkill, out _), Is.False);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
            Assert.That(series.TryCommitNextRound(series.CommandToken, fixture.AllySkill, fixture.EnemySkill, out _), Is.False);
        }

        [Test]
        public void InsufficientResponseMp_DowngradesToNoResponseWithoutSecondResponseCost()
        {
            Fixture fixture = CreateFixture(initialMp: 1);
            CombatClashSeriesState series = Begin(fixture, 5, 5, 3);
            Resolve(fixture, series, 2, 2);
            fixture.Session.GetCombatState(fixture.Ally).RestoreMp(1);

            CombatExchangeCommitResult commit = Commit(fixture, series, fixture.AllySkill, fixture.EnemySkill);

            Assert.That(commit.ResponseDowngradedForInsufficientMp, Is.True);
            Assert.That(commit.ResponseMpSpent, Is.Zero);
            Assert.That(series.FinalOutcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(series.ResponseMpSpent, Is.EqualTo(1));
            Assert.That(series.AttackMpSpent, Is.EqualTo(2));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(10));
        }

        [Test]
        public void UnaffordableAttackAndStaleCommandsDoNotMutateState()
        {
            Fixture fixture = CreateFixture(initialMp: 1);
            CombatClashSeriesState series = Begin(fixture, 5, 5, 3);
            long firstToken = series.CommandToken;
            Resolve(fixture, series, 2, 2);
            int attackerMp = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;

            Assert.That(series.TryResolveRound(firstToken,
                Input(fixture.Ally, fixture.AllySkill, 2, 1, true),
                Input(fixture.Enemy, fixture.EnemySkill, 2, 1, false), out _), Is.False);
            Assert.That(series.TryCommitNextRound(firstToken, fixture.AllySkill, fixture.EnemySkill, out _), Is.False);
            Assert.That(series.TryCommitNextRound(series.CommandToken, fixture.AllySkill, fixture.EnemySkill, out CombatExchangeCommitResult failure), Is.False);
            Assert.That(failure.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackMp));
            Assert.That(series.ComparisonCount, Is.EqualTo(1));
            Assert.That(series.AttackerDurability, Is.EqualTo(4));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(attackerMp));

            fixture.Session.GetCombatState(fixture.Ally).RestoreMp(1);
            long commitToken = series.CommandToken;
            Commit(fixture, series, fixture.AllySkill, null);
            Assert.That(series.TryCommitNextRound(commitToken, fixture.AllySkill, null, out _), Is.False);
            Assert.That(series.AttackMpSpent, Is.EqualTo(2));
        }

        [Test]
        public void FinalSkillSelectionRequiresExplicitCommittedSkill()
        {
            Fixture fixture = CreateFixture();
            TestSkill laterSkill = new TestSkill(2, 1, 3);
            fixture.Ally.AddSkill(laterSkill);
            CombatClashSeriesState series = Begin(fixture, 2, 2, 2);
            Resolve(fixture, series, 3, 2);
            Commit(fixture, series, laterSkill, fixture.EnemySkill);
            Resolve(fixture, series, 4, 2, attackSkill: laterSkill);

            TestSkill uncommitted = new TestSkill(3, 1, 3);
            fixture.Ally.AddSkill(uncommitted);
            Assert.That(series.TryClaimFinalOutcome(series.CommandToken, uncommitted, out _), Is.False);
            Assert.That(series.TryClaimFinalOutcome(series.CommandToken, laterSkill, out CombatClashResult final), Is.True);
            Assert.That(final.AttackDeclaration.Skill, Is.SameAs(laterSkill));
        }

        [Test]
        public void ResponderWin_KeepsBothApprovedCostsAndClaimsResponseSkill()
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 1, 2, 2);

            Assert.That(Resolve(fixture, series, 1, 3).FinalOutcome,
                Is.EqualTo(CombatClashOutcome.ResponderWin));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(4));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.EqualTo(4));
            Assert.That(series.TryClaimFinalOutcome(
                series.CommandToken, fixture.EnemySkill, out CombatClashResult final), Is.True);
            Assert.That(CombatOutcomeSelector.TrySelect(final, out CombatOutcomeAction action), Is.True);
            Assert.That(action.Actor, Is.SameAs(fixture.Enemy));
            Assert.That(action.Skill, Is.SameAs(fixture.EnemySkill));
        }

        [Test]
        public void OptInBlocksV1ResolutionAndDuplicateSeriesAttachment()
        {
            Fixture fixture = CreateFixture();
            CombatClashSeriesState series = Begin(fixture, 2, 2, 2);

            Assert.That(fixture.Exchange.ClashSeries, Is.SameAs(series));
            Assert.That(fixture.StateMachine.TryResolveClash(), Is.False);
            Assert.Throws<InvalidOperationException>(() => Begin(fixture, 2, 2, 2));
            Assert.That(fixture.Exchange.CurrentClashResult, Is.Null);
        }

        private static Fixture CreateFixture(int initialMp = 5)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies);
            TestSkill allySkill = new TestSkill(1, 1, 3);
            TestSkill enemySkill = new TestSkill(2, 1, 3);
            ally.AddSkill(allySkill);
            enemy.AddSkill(enemySkill);
            CombatRuntimeConfig config = new CombatRuntimeConfig(10, initialMp, 3, 0);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                config);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            stateMachine.Tick();
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(stateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(ally, enemy, allySkill), session.ExchangeState.Version), Is.True);
            Assert.That(stateMachine.TryDeclareResponse(
                new CombatResponseDeclaration(enemy, enemySkill), session.ExchangeState.Version), Is.True);
            Assert.That(stateMachine.TryCommitExchange(session.ExchangeState.Version), Is.True);
            Assert.That(stateMachine.TryBeginApproach(session.ExchangeState.Version), Is.True);
            Assert.That(stateMachine.CompleteApproach(session.ExchangeState.Version), Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Clash));
            return new Fixture(session, stateMachine, ally, enemy, allySkill, enemySkill);
        }

        private static CombatClashSeriesState Begin(Fixture fixture, int attackerDurability, int responderDurability, int maxComparisons)
        {
            return new CombatClashSeriesState(
                fixture.Session,
                fixture.StateMachine,
                new CombatClashSeriesConfig(attackerDurability, responderDurability, maxComparisons));
        }

        private static CombatExchangeCommitResult Commit(
            Fixture fixture, CombatClashSeriesState series, ISkill attackSkill, ISkill responseSkill)
        {
            Assert.That(series.TryCommitNextRound(
                series.CommandToken, attackSkill, responseSkill, out CombatExchangeCommitResult result), Is.True);
            return result;
        }

        private static CombatClashRoundResult Resolve(
            Fixture fixture,
            CombatClashSeriesState series,
            int attackPower,
            int responsePower,
            int attackSpeed = 1,
            int responseSpeed = 1,
            ISkill attackSkill = null,
            ISkill responseSkill = null)
        {
            ISkill usedAttack = attackSkill ?? series.CommittedAttackSkills[series.CommittedAttackSkills.Count - 1];
            ISkill usedResponse = responseSkill ?? series.CommittedResponseSkills[series.CommittedResponseSkills.Count - 1];
            Assert.That(series.TryResolveRound(
                series.CommandToken,
                Input(fixture.Ally, usedAttack, attackPower, attackSpeed, true),
                Input(fixture.Enemy, usedResponse, responsePower, responseSpeed, false),
                out CombatClashRoundResult result), Is.True);
            return result;
        }

        private static CombatClashSideInput Input(
            ICombatant actor, ISkill skill, int power, int speed, bool ownsAttack)
        {
            return new CombatClashSideInput(actor.Id, skill.Id, power, speed, ownsAttack);
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public CombatExchangeState Exchange => Session.ExchangeState;
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public TestSkill AllySkill { get; }
            public TestSkill EnemySkill { get; }

            public Fixture(CombatSession session, CombatStateMachine stateMachine,
                TestCombatant ally, TestCombatant enemy, TestSkill allySkill, TestSkill enemySkill)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
                AllySkill = allySkill;
                EnemySkill = enemySkill;
            }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public SkillId Id { get; }
            public string Name => "Fixture Skill";
            public int InspirationCost => 0;
            public KeywordMask Keywords => KeywordMask.None;
            public SkillTag Tag => SkillTag.Attack;
            public TargetingRule Targeting => TargetingRule.SingleEnemy;
            public SkillMovementMode MovementMode => SkillMovementMode.None;
            public float DesiredTargetDistance => 0f;
            public float MoveSpeed => 0f;
            public float ActionDelayAfterMove => 0f;
            public int BaseDamage { get; }
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost { get; }

            public TestSkill(int id, int mpCost, int damage)
            {
                Id = new SkillId(id);
                MpCost = mpCost;
                BaseDamage = damage;
            }
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 0;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public void AddSkill(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) => _state?.ApplyDamage(amount);
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
