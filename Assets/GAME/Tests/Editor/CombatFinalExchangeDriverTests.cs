#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatFinalExchangeDriverTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [TestCase(Side.Allies)]
        [TestCase(Side.Enemies)]
        public void Bind_EntersStandoffAndRequestsInitialAuthoritySide(Side initiative)
        {
            Fixture fixture = CreateFixture(initiative: initiative);
            CombatExchangeDecisionRequest request = null;
            fixture.Orchestrator.AttackDecisionRequested += value => request = value;

            Assert.That(fixture.Bind(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Kind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
            Assert.That(request.ActingSide, Is.EqualTo(initiative));
            Assert.That(request.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(request.ExchangeVersion, Is.EqualTo(fixture.Exchange.Version));
        }

        [Test]
        public void ResponsePath_BlocksAtApproachUntilPresentationCompletes()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            CombatExchangeDecisionRequest responseRequest = null;
            CombatApproachPresentationRequest approachRequest = null;
            fixture.Orchestrator.ResponseDecisionRequested += value => responseRequest = value;
            fixture.Orchestrator.ApproachPresentationRequested += value => approachRequest = value;
            fixture.Bind();

            Assert.That(fixture.SubmitAllyAttack(), Is.True);
            Assert.That(responseRequest, Is.Not.Null);
            Assert.That(responseRequest.ActingActor, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Orchestrator.SubmitResponse(
                new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill),
                responseRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(approachRequest, Is.Not.Null);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP));

            Assert.That(approachRequest.TryComplete(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP - fixture.AllySkill.BaseDamage));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentPosture, Is.EqualTo(1));
            Assert.That(approachRequest.TryComplete(), Is.False);
        }

        [Test]
        public void NoResponse_SkipsClashRandomAndAppliesOutcomeExactlyOnce()
        {
            Fixture fixture = CreateFixture(allyDamage: 2);
            CombatApproachPresentationRequest approachRequest = null;
            fixture.Orchestrator.ApproachPresentationRequested += value => approachRequest = value;
            fixture.Bind();
            fixture.SubmitAllyAttack();
            int responseVersion = fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion;

            Assert.That(fixture.Orchestrator.SubmitNoResponse(responseVersion), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(approachRequest.TryComplete(), Is.True);

            int hpAfter = fixture.Enemy.HP;
            fixture.Orchestrator.AdvanceUntilBlocked();
            fixture.Orchestrator.AdvanceUntilBlocked();

            Assert.That(fixture.Random.CallCount, Is.Zero);
            Assert.That(fixture.Exchange.CurrentClashResult.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP - 2));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpAfter));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        [Test]
        public void InsufficientResponseMp_DowngradesToNoResponseWithoutRandom()
        {
            Fixture fixture = CreateFixture();
            CombatApproachPresentationRequest approachRequest = null;
            fixture.Orchestrator.ApproachPresentationRequested += value => approachRequest = value;
            fixture.Bind();
            fixture.Session.GetCombatState(fixture.Enemy).SetMp(0);
            fixture.SubmitAllyAttack();
            CombatExchangeDecisionRequest responseRequest = fixture.Orchestrator.PendingDecisionRequest;

            Assert.That(fixture.Orchestrator.SubmitResponse(
                new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill),
                responseRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.Exchange.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Exchange.CommittedResponseMpCost, Is.Zero);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMp, Is.Zero);
            Assert.That(approachRequest.TryComplete(), Is.True);
            Assert.That(fixture.Random.CallCount, Is.Zero);
        }

        [Test]
        public void DefenderWin_TransfersAuthorityAndContinueRequestsEnemyAttack()
        {
            Fixture fixture = CreateFixture(allyPower: 1, enemyPower: 3, enemyDamage: 2);
            CombatApproachPresentationRequest approachRequest = null;
            CombatExchangeDecisionRequest latestAttackRequest = null;
            fixture.Orchestrator.ApproachPresentationRequested += value => approachRequest = value;
            fixture.Orchestrator.AttackDecisionRequested += value => latestAttackRequest = value;
            fixture.Bind();
            fixture.SubmitAllyAttack();
            CombatExchangeDecisionRequest responseRequest = fixture.Orchestrator.PendingDecisionRequest;

            Assert.That(fixture.Orchestrator.SubmitResponse(
                new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill),
                responseRequest.ExchangeVersion), Is.True);
            Assert.That(approachRequest.TryComplete(), Is.True);

            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP));
            Assert.That(fixture.Ally.HP, Is.EqualTo(fixture.Ally.MaxHP - 2));
            Assert.That(fixture.Exchange.CurrentAttackActor, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Enemies));

            Assert.That(fixture.Orchestrator.ContinueChain(
                fixture.EnemySkill,
                fixture.Exchange.Version), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.AttackDeclaration));
            Assert.That(latestAttackRequest.ActingSide, Is.EqualTo(Side.Enemies));
            Assert.That(latestAttackRequest.ActingActor, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                latestAttackRequest.ExchangeVersion), Is.False);
        }

        [Test]
        public void Handoff_RequestsReceiverWithoutSpendingMpAndRejectsDuplicate()
        {
            Fixture fixture = CreateFixture();
            DriveNoResponseToChainDecision(fixture);
            int receiverMp = fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp;
            int version = fixture.Exchange.Version;

            Assert.That(fixture.Orchestrator.Handoff(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                version), Is.True);

            CombatExchangeDecisionRequest request = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(request.ActingActor, Is.SameAs(fixture.AllyTwo));
            Assert.That(request.ActingSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Exchange.HandoffCount, Is.EqualTo(1));
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(receiverMp));
            Assert.That(fixture.Orchestrator.Handoff(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                version), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(receiverMp));
        }

        [Test]
        public void EndChain_ReturnsToStandoffAndStartsNewPressureCycle()
        {
            CombatRuntimeConfig config = new CombatRuntimeConfig(5, 2, 3, 0, 1f, 2f, 1f);
            Fixture fixture = CreateFixture(config: config);
            DriveNoResponseToChainDecision(fixture);
            fixture.Session.StandoffState.Advance(1f);

            Assert.That(fixture.Orchestrator.EndChain(fixture.Exchange.Version), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
            Assert.That(fixture.Exchange.HandoffCount, Is.Zero);

            fixture.Orchestrator.Tick(1f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(1f));
        }

        [Test]
        public void StandoffTick_RecoversMpClampsAndRaisesEnemyRequestOnce()
        {
            CombatRuntimeConfig config = new CombatRuntimeConfig(5, 1, 3, 0, 2f, 3f, 1f);
            Fixture fixture = CreateFixture(config: config);
            int enemyRequests = 0;
            fixture.Orchestrator.AttackDecisionRequested += request =>
            {
                if (request.ActingSide == Side.Enemies)
                    enemyRequests++;
            };
            fixture.Bind();

            fixture.Orchestrator.Tick(0.5f);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(2));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(0.5f));
            Assert.That(enemyRequests, Is.Zero);

            fixture.Orchestrator.Tick(2.5f);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(3f));
            Assert.That(enemyRequests, Is.EqualTo(1));
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Enemies));

            fixture.Orchestrator.Tick(10f);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(5));
            Assert.That(enemyRequests, Is.EqualTo(1));
        }

        [Test]
        public void PlayerDeclaration_InvalidatesStalePressureDecision()
        {
            Fixture fixture = CreateFixture();
            fixture.Bind();
            CombatExchangeDecisionRequest playerRequest = fixture.Orchestrator.PendingDecisionRequest;

            Assert.That(fixture.SubmitAllyAttack(), Is.True);
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill),
                playerRequest.ExchangeVersion), Is.False);
            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void SynchronousPresentationCompletion_IsReentrySafe()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            int presentationCalls = 0;
            int maximumDepth = 0;
            int currentDepth = 0;
            fixture.Orchestrator.ApproachPresentationRequested += request =>
            {
                currentDepth++;
                maximumDepth = Math.Max(maximumDepth, currentDepth);
                presentationCalls++;
                Assert.That(request.TryComplete(), Is.True);
                fixture.Orchestrator.AdvanceUntilBlocked();
                currentDepth--;
            };
            fixture.Bind();
            fixture.SubmitAllyAttack();
            CombatExchangeDecisionRequest responseRequest = fixture.Orchestrator.PendingDecisionRequest;

            Assert.That(fixture.Orchestrator.SubmitResponse(
                new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill),
                responseRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(presentationCalls, Is.EqualTo(1));
            Assert.That(maximumDepth, Is.EqualTo(1));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(fixture.Enemy.MaxHP - fixture.AllySkill.BaseDamage));
        }

        [Test]
        public void DecisionSubscriberPumpRequest_DoesNotRecurseOrRepublish()
        {
            Fixture fixture = CreateFixture();
            int requestCalls = 0;
            int currentDepth = 0;
            int maximumDepth = 0;
            fixture.Orchestrator.AttackDecisionRequested += request =>
            {
                requestCalls++;
                currentDepth++;
                maximumDepth = Math.Max(maximumDepth, currentDepth);
                fixture.Orchestrator.AdvanceUntilBlocked();
                currentDepth--;
            };

            Assert.That(fixture.Bind(), Is.True);
            fixture.Orchestrator.AdvanceUntilBlocked();
            fixture.Orchestrator.AdvanceUntilBlocked();

            Assert.That(requestCalls, Is.EqualTo(1));
            Assert.That(maximumDepth, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void TerminalCompletion_IsRaisedOnceAndRejectsSubscriberCommand()
        {
            Fixture fixture = CreateFixture(allyDamage: 10);
            int terminalCalls = 0;
            bool subscriberCommandResult = true;
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            fixture.Orchestrator.TerminalCompleted += (session, reason, version) =>
            {
                terminalCalls++;
                Assert.That(session, Is.SameAs(fixture.Session));
                Assert.That(reason, Is.EqualTo(CombatEndReason.Victory));
                subscriberCommandResult = fixture.Orchestrator.EndChain(version);
                fixture.Orchestrator.AdvanceUntilBlocked();
            };
            fixture.Bind();
            fixture.SubmitAllyAttack();

            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
            fixture.Orchestrator.AdvanceUntilBlocked();
            fixture.Orchestrator.AdvanceUntilBlocked();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(terminalCalls, Is.EqualTo(1));
            Assert.That(subscriberCommandResult, Is.False);
            Assert.That(fixture.Enemy.HP, Is.Zero);
        }

        [Test]
        public void SessionSwitch_RejectsOldPresentationCallback()
        {
            Fixture first = CreateFixture();
            CombatApproachPresentationRequest oldRequest = null;
            first.Orchestrator.ApproachPresentationRequested += value => oldRequest = value;
            first.Bind();
            first.SubmitAllyAttack();
            first.Orchestrator.SubmitNoResponse(
                first.Orchestrator.PendingDecisionRequest.ExchangeVersion);
            Assert.That(oldRequest, Is.Not.Null);

            Fixture second = CreateFixture(orchestrator: first.Orchestrator);
            Assert.That(second.Bind(), Is.True);
            int secondVersion = second.Exchange.Version;

            Assert.That(oldRequest.TryComplete(), Is.False);
            Assert.That(second.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(second.Exchange.Version, Is.EqualTo(secondVersion));
            Assert.That(second.Enemy.HP, Is.EqualTo(second.Enemy.MaxHP));
        }

        [Test]
        public void LegacySession_IsNotAcceptedByFinalExchangeBinding()
        {
            Fixture fixture = CreateFixture(flowMode: CombatFlowMode.LegacyPlanning);

            Assert.That(fixture.Bind(), Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.EnterCombat));
        }

        private static void DriveNoResponseToChainDecision(Fixture fixture)
        {
            CombatApproachPresentationRequest approach = null;
            fixture.Orchestrator.ApproachPresentationRequested += value => approach = value;
            fixture.Bind();
            fixture.SubmitAllyAttack();
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        private Fixture CreateFixture(
            Side initiative = Side.Allies,
            int allyPower = 2,
            int enemyPower = 1,
            int allyDamage = 1,
            int enemyDamage = 1,
            CombatRuntimeConfig? config = null,
            CombatFlowMode flowMode = CombatFlowMode.StandoffClashChain,
            CombatFlowOrchestrator orchestrator = null)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies);
            TestCombatant enemy = new TestCombatant(11, Side.Enemies);
            TestSkill allySkill = AddSkill(ally, 1, allyDamage);
            TestSkill allyTwoSkill = AddSkill(allyTwo, 2, allyDamage);
            TestSkill enemySkill = AddSkill(enemy, 11, enemyDamage);
            CombatRuntimeConfig runtimeConfig = config ??
                new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f);
            CombatSession session = new CombatSession(
                initiative == Side.Allies ? StartReason.PlayerFirstHit : StartReason.PlayerGotHit,
                initiative,
                new InspirationPool(10),
                new CombatEnvironment(),
                flowMode,
                runtimeConfig);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);

            if (orchestrator == null)
            {
                GameObject owner = new GameObject("CombatFinalExchangeDriverTests.Orchestrator");
                _createdObjects.Add(owner);
                orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
            }

            return new Fixture(
                session,
                stateMachine,
                orchestrator,
                new ZeroRandomSource(),
                new FixedClashInputProvider(allyPower, enemyPower),
                ally,
                allyTwo,
                enemy,
                allySkill,
                allyTwoSkill,
                enemySkill);
        }

        private static TestSkill AddSkill(TestCombatant actor, int id, int damage)
        {
            TestSkill skill = new TestSkill(id, damage);
            actor.AddSkill(skill);
            return skill;
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public ZeroRandomSource Random { get; }
            public FixedClashInputProvider ClashInputs { get; }
            public TestCombatant Ally { get; }
            public TestCombatant AllyTwo { get; }
            public TestCombatant Enemy { get; }
            public TestSkill AllySkill { get; }
            public TestSkill AllyTwoSkill { get; }
            public TestSkill EnemySkill { get; }
            public CombatExchangeState Exchange => Session.ExchangeState;

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                CombatFlowOrchestrator orchestrator,
                ZeroRandomSource random,
                FixedClashInputProvider clashInputs,
                TestCombatant ally,
                TestCombatant allyTwo,
                TestCombatant enemy,
                TestSkill allySkill,
                TestSkill allyTwoSkill,
                TestSkill enemySkill)
            {
                Session = session;
                StateMachine = stateMachine;
                Orchestrator = orchestrator;
                Random = random;
                ClashInputs = clashInputs;
                Ally = ally;
                AllyTwo = allyTwo;
                Enemy = enemy;
                AllySkill = allySkill;
                AllyTwoSkill = allyTwoSkill;
                EnemySkill = enemySkill;
            }

            public bool Bind()
            {
                return Orchestrator.BindFinalExchange(
                    Session,
                    StateMachine,
                    Random,
                    ClashInputs);
            }

            public bool SubmitAllyAttack()
            {
                CombatExchangeDecisionRequest request = Orchestrator.PendingDecisionRequest;
                return request != null && Orchestrator.SubmitAttackDeclaration(
                    new CombatAttackDeclaration(Ally, Enemy, AllySkill),
                    request.ExchangeVersion);
            }
        }

        private sealed class ZeroRandomSource : ICombatRuleRandomSource
        {
            public int CallCount { get; private set; }

            public int NextInclusive(int minInclusive, int maxInclusive)
            {
                CallCount++;
                return 0;
            }
        }

        private sealed class FixedClashInputProvider : ICombatClashSideInputProvider
        {
            private readonly int _allyPower;
            private readonly int _enemyPower;

            public FixedClashInputProvider(int allyPower, int enemyPower)
            {
                _allyPower = allyPower;
                _enemyPower = enemyPower;
            }

            public bool TryCreate(
                ICombatant actor,
                ISkill skill,
                bool isCurrentAttackOwner,
                out CombatClashSideInput input)
            {
                input = default;
                if (actor == null || skill == null)
                    return false;

                input = new CombatClashSideInput(
                    actor.Id,
                    skill.Id,
                    actor.Side == Side.Allies ? _allyPower : _enemyPower,
                    skill.Speed,
                    isCurrentAttackOwner);
                return true;
            }
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider
        {
            public SkillId Id { get; }
            public string Name => $"Skill-{Id.Value}";
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
            public int MpCost => 1;

            public TestSkill(int id, int damage)
            {
                Id = new SkillId(id);
                BaseDamage = damage;
            }
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;
            private int _hp = 10;

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? _hp;
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

            public void AddSkill(ISkill skill)
            {
                _skills.Add(skill);
            }

            public void BindCombatState(CombatantCombatState state)
            {
                _state = state;
            }

            public void ApplyDamage(int amount)
            {
                if (_state != null)
                    _state.ApplyDamage(amount);
                else if (amount > 0)
                    _hp = Math.Max(0, _hp - amount);
            }

            public void AddStagger(int amount)
            {
            }

            public void SetStunned(bool value)
            {
                IsStunned = value;
            }

            public void ResetStaggerIfNeededOnStunEnd()
            {
            }
        }
    }
}
#endif
