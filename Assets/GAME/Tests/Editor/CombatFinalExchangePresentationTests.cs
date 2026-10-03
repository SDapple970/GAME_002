#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatFinalExchangePresentationTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void OutcomeGate_BlocksChainDecisionUntilPresentationCompletes()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.Orchestrator.PendingOutcomePresentationRequest, Is.SameAs(outcome));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest, Is.Null);
        }

        [Test]
        public void OutcomeCompletion_AdvancesToChainDecision()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.TryComplete(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Orchestrator.PendingOutcomePresentationRequest, Is.Null);
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.Kind,
                Is.EqualTo(CombatExchangeDecisionKind.Chain));
        }

        [Test]
        public void OutcomeCompletion_IsAcceptedExactlyOnce()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);

            Assert.That(outcome.TryComplete(), Is.True);
            int version = fixture.Exchange.Version;
            Assert.That(outcome.TryComplete(), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
        }

        [Test]
        public void ChainDecisionEvent_IsWithheldUntilOutcomeCompletion()
        {
            Fixture fixture = CreateFixture();
            int chainRequests = 0;
            fixture.Orchestrator.ChainDecisionRequested += _ => chainRequests++;
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(chainRequests, Is.Zero);
            Assert.That(outcome.TryComplete(), Is.True);
            Assert.That(chainRequests, Is.EqualTo(1));
        }

        [Test]
        public void ResponseSnapshot_ContainsDeclarationsAndClash()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.AttackDeclaration.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(outcome.AttackDeclaration.Target, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
            Assert.That(outcome.ResponseDeclaration.Responder, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.HasResponse, Is.True);
            Assert.That(outcome.HasClash, Is.True);
        }

        [Test]
        public void AttackerWinSnapshot_ContainsWinnerLoserAndWinningSkill()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.Winner, Is.SameAs(fixture.Ally));
            Assert.That(outcome.Loser, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.WinningAction.Actor, Is.SameAs(fixture.Ally));
            Assert.That(outcome.WinningAction.Skill, Is.SameAs(fixture.AllySkill));
            Assert.That(outcome.NextAttackSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void DefenderWinSnapshot_TransfersPresentationAuthority()
        {
            Fixture fixture = CreateFixture(allyPower: 1, enemyPower: 3);
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.Winner, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.Loser, Is.SameAs(fixture.Ally));
            Assert.That(outcome.WinningAction.Skill, Is.SameAs(fixture.EnemySkill));
            Assert.That(outcome.NextAttackSide, Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void NoResponseSnapshot_IsUnopposedAndHasNoClashCueRequirement()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);

            Assert.That(outcome.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(outcome.ResponseDeclaration, Is.Null);
            Assert.That(outcome.ClashResult.Outcome, Is.EqualTo(CombatClashOutcome.Unopposed));
            Assert.That(outcome.HasResponse, Is.False);
            Assert.That(outcome.HasClash, Is.False);
        }

        [Test]
        public void ExecutionSnapshot_ReportsAlreadyAppliedDamage()
        {
            Fixture fixture = CreateFixture(allyDamage: 3);
            bool sawPostDamageStateChange = false;
            fixture.Exchange.OnStateChanged += _ =>
                sawPostDamageStateChange |= fixture.Enemy.HP == 7;
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);
            CombatSkillTargetResult target = outcome.ExecutionResult.TargetResults[0];

            Assert.That(outcome.ExecutionResult.WasExecuted, Is.True);
            Assert.That(target.Target, Is.SameAs(fixture.Enemy));
            Assert.That(target.HpBefore, Is.EqualTo(10));
            Assert.That(target.HpAfter, Is.EqualTo(7));
            Assert.That(target.DamageApplied, Is.EqualTo(3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(7));
            Assert.That(sawPostDamageStateChange, Is.True);
        }

        [Test]
        public void PostureSnapshot_ReportsReactionWithoutPresenterMutation()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.PostureResult.Target, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.PostureResult.PostureBefore, Is.Zero);
            Assert.That(outcome.PostureResult.PostureAfter, Is.EqualTo(1));
            Assert.That(outcome.PostureResult.PostureApplied, Is.EqualTo(1));
        }

        [Test]
        public void StunSnapshot_ReportsAppliedStun()
        {
            Fixture fixture = CreateFixture(maxPosture: 1);
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.StunResult.Target, Is.SameAs(fixture.Enemy));
            Assert.That(outcome.StunResult.StunApplied, Is.True);
            Assert.That(outcome.StunResult.IsStunnedAfter, Is.True);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [Test]
        public void AllOutCandidate_IsExposedBeforeFinalization()
        {
            Fixture fixture = CreateFixture(maxPosture: 1);
            CombatOutcomePresentationRequest outcome = DriveResponseToOutcomeGate(fixture);

            Assert.That(outcome.IsAllOutCandidate, Is.True);
            Assert.That(outcome.IsTerminal, Is.False);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void TerminalOutcome_WaitsForPresentationBeforeExit()
        {
            Fixture fixture = CreateFixture(allyDamage: 10);
            int terminalCalls = 0;
            fixture.Orchestrator.TerminalCompleted += (_, __, ___) => terminalCalls++;
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);

            Assert.That(outcome.IsTerminal, Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(terminalCalls, Is.Zero);
        }

        [Test]
        public void TerminalOutcome_CompletesAfterPresentationExactlyOnce()
        {
            Fixture fixture = CreateFixture(allyDamage: 10);
            int terminalCalls = 0;
            fixture.Orchestrator.TerminalCompleted += (_, __, ___) => terminalCalls++;
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);

            Assert.That(outcome.TryComplete(), Is.True);
            fixture.Orchestrator.AdvanceUntilBlocked();

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(terminalCalls, Is.EqualTo(1));
            Assert.That(outcome.TryComplete(), Is.False);
            Assert.That(terminalCalls, Is.EqualTo(1));
        }

        [Test]
        public void MissingOutcomePresenter_AutoCompletesForHeadlessCompatibility()
        {
            Fixture fixture = CreateFixture();
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            fixture.Bind();
            fixture.SubmitAllyAttack();

            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Orchestrator.PendingOutcomePresentationRequest, Is.Null);
        }

        [Test]
        public void SynchronousOutcomeCompletion_IsReentrySafe()
        {
            Fixture fixture = CreateFixture();
            int calls = 0;
            int depth = 0;
            int maximumDepth = 0;
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            fixture.Orchestrator.OutcomePresentationRequested += request =>
            {
                depth++;
                maximumDepth = Math.Max(maximumDepth, depth);
                calls++;
                Assert.That(request.TryComplete(), Is.True);
                fixture.Orchestrator.AdvanceUntilBlocked();
                depth--;
            };
            fixture.Bind();
            fixture.SubmitAllyAttack();

            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(maximumDepth, Is.EqualTo(1));
        }

        [Test]
        public void SessionSwitch_RejectsStaleOutcomeCompletion()
        {
            Fixture first = CreateFixture();
            CombatOutcomePresentationRequest stale = DriveNoResponseToOutcomeGate(first);
            Fixture second = CreateFixture(orchestrator: first.Orchestrator);

            Assert.That(second.Bind(), Is.True);
            Assert.That(stale.TryComplete(), Is.False);
            Assert.That(second.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(second.Enemy.HP, Is.EqualTo(second.Enemy.MaxHP));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectorBinding_RejectsMissingOrLegacyFinalSession(bool missingSession)
        {
            Fixture fixture = CreateFixture(flowMode: CombatFlowMode.LegacyPlanning);
            CombatDirector director = CreateDisabledDirector();

            Assert.That(director.BindFinalExchange(
                missingSession ? null : fixture.Orchestrator,
                missingSession ? fixture.Session : null), Is.False);
            Assert.That(director.BindFinalExchange(fixture.Orchestrator, fixture.Session), Is.False);
        }

        [Test]
        public void DisabledDirector_CompletesBothPresentationGatesWithoutChangingTimeScale()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Bind(), Is.True);
            CombatDirector director = CreateDisabledDirector();
            List<CombatFinalPresentationCueKind> cues = new List<CombatFinalPresentationCueKind>();
            director.FinalPresentationCueRaised += cue => cues.Add(cue.Kind);
            Assert.That(director.BindFinalExchange(fixture.Orchestrator, fixture.Session), Is.True);
            float timeScale = Time.timeScale;

            fixture.SubmitAllyAttack();
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(cues, Is.EqualTo(new[]
            {
                CombatFinalPresentationCueKind.Standoff,
                CombatFinalPresentationCueKind.Approach
            }));
            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
        }

        [Test]
        public void DirectorRebind_ConsumesPendingApproachAfterUnbind()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Bind(), Is.True);
            CombatDirector firstDirector = CreateDisabledDirector();
            int cues = 0;
            firstDirector.FinalPresentationCueRaised += _ => cues++;
            Assert.That(firstDirector.BindFinalExchange(fixture.Orchestrator, fixture.Session), Is.True);
            firstDirector.UnbindFinalExchange();
            cues = 0;

            fixture.SubmitAllyAttack();
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Approach));
            Assert.That(cues, Is.Zero);
            CombatDirector replacementDirector = CreateDisabledDirector();
            replacementDirector.FinalPresentationCueRaised += _ => cues++;
            Assert.That(replacementDirector.BindFinalExchange(
                fixture.Orchestrator,
                fixture.Session), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(cues, Is.EqualTo(1));
        }

        [Test]
        public void PlayerEnemyDirector_EndToEnd_ReachesPlayerChainDecision()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            Assert.That(fixture.Bind(), Is.True);
            CombatDirector director = CreateDisabledDirector();
            Assert.That(director.BindFinalExchange(fixture.Orchestrator, fixture.Session), Is.True);
            FinalCombatPlayerCommandController player = new FinalCombatPlayerCommandController();
            FinalCombatEnemyCommandController enemy = new FinalCombatEnemyCommandController();
            Assert.That(player.Bind(fixture.Orchestrator, fixture.Session), Is.True);
            Assert.That(enemy.Bind(fixture.Orchestrator, fixture.Session), Is.True);

            Assert.That(player.SelectActor(fixture.Ally), Is.True);
            Assert.That(player.SelectSkill(fixture.AllySkill), Is.True);
            Assert.That(player.SelectTarget(fixture.Enemy), Is.True);
            Assert.That(player.Confirm(), Is.True);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(player.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Chain));
            Assert.That(player.ViewState.CanEnd, Is.True);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(9));
        }

        [Test]
        public void Handoff_NextApproachCueUsesReceivingActor()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Bind(), Is.True);
            CombatDirector director = CreateDisabledDirector();
            CombatFinalPresentationCue latestApproach = null;
            director.FinalPresentationCueRaised += cue =>
            {
                if (cue.Kind == CombatFinalPresentationCueKind.Approach)
                    latestApproach = cue;
            };
            Assert.That(director.BindFinalExchange(fixture.Orchestrator, fixture.Session), Is.True);
            fixture.SubmitAllyAttack();
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(fixture.Orchestrator.Handoff(
                fixture.AllyTwo,
                fixture.AllyTwoSkill,
                true,
                fixture.Exchange.Version), Is.True);
            CombatExchangeDecisionRequest attack = fixture.Orchestrator.PendingDecisionRequest;

            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.AllyTwo, fixture.Enemy, fixture.AllyTwoSkill),
                attack.ExchangeVersion), Is.True);
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);

            Assert.That(latestApproach, Is.Not.Null);
            Assert.That(latestApproach.Actor, Is.SameAs(fixture.AllyTwo));
            Assert.That(latestApproach.Skill, Is.SameAs(fixture.AllyTwoSkill));
        }

        [Test]
        public void EndChain_DoesNotRestoreOrMovePresentationObjects()
        {
            Fixture fixture = CreateFixture();
            CombatOutcomePresentationRequest outcome = DriveNoResponseToOutcomeGate(fixture);
            Assert.That(outcome.TryComplete(), Is.True);
            GameObject sentinel = NewObject("FinalPresentation.PositionSentinel");
            sentinel.transform.position = new Vector3(7f, 3f, -2f);
            Vector3 expected = sentinel.transform.position;

            Assert.That(fixture.Orchestrator.EndChain(fixture.Exchange.Version), Is.True);

            Assert.That(sentinel.transform.position, Is.EqualTo(expected));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
        }

        private CombatOutcomePresentationRequest DriveResponseToOutcomeGate(Fixture fixture)
        {
            CombatApproachPresentationRequest approach = null;
            CombatOutcomePresentationRequest outcome = null;
            fixture.Orchestrator.ApproachPresentationRequested += request => approach = request;
            fixture.Orchestrator.OutcomePresentationRequested += request => outcome = request;
            Assert.That(fixture.Bind(), Is.True);
            Assert.That(fixture.SubmitAllyAttack(), Is.True);
            CombatExchangeDecisionRequest response = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(fixture.Orchestrator.SubmitResponse(
                new CombatResponseDeclaration(fixture.Enemy, fixture.EnemySkill),
                response.ExchangeVersion), Is.True);
            Assert.That(approach, Is.Not.Null);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(outcome, Is.Not.Null);
            return outcome;
        }

        private CombatOutcomePresentationRequest DriveNoResponseToOutcomeGate(Fixture fixture)
        {
            CombatApproachPresentationRequest approach = null;
            CombatOutcomePresentationRequest outcome = null;
            fixture.Orchestrator.ApproachPresentationRequested += request => approach = request;
            fixture.Orchestrator.OutcomePresentationRequested += request => outcome = request;
            Assert.That(fixture.Bind(), Is.True);
            Assert.That(fixture.SubmitAllyAttack(), Is.True);
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Orchestrator.PendingDecisionRequest.ExchangeVersion), Is.True);
            Assert.That(approach, Is.Not.Null);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(outcome, Is.Not.Null);
            return outcome;
        }

        private Fixture CreateFixture(
            int allyPower = 2,
            int enemyPower = 1,
            int allyDamage = 1,
            int enemyDamage = 1,
            int maxPosture = 3,
            CombatFlowMode flowMode = CombatFlowMode.StandoffClashChain,
            CombatFlowOrchestrator orchestrator = null)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies);
            TestCombatant enemy = new TestCombatant(11, Side.Enemies);
            TestSkill allySkill = AddSkill(ally, 1, allyDamage);
            TestSkill allyTwoSkill = AddSkill(allyTwo, 2, allyDamage);
            TestSkill enemySkill = AddSkill(enemy, 11, enemyDamage);
            CombatRuntimeConfig config = new CombatRuntimeConfig(10, 5, maxPosture, 0, 0f, 10f, 1f);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                flowMode,
                config);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);

            if (orchestrator == null)
                orchestrator = NewObject("CombatFinalExchangePresentationTests.Orchestrator")
                    .AddComponent<CombatFlowOrchestrator>();

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

        private CombatDirector CreateDisabledDirector()
        {
            CombatDirector director = NewObject("CombatFinalExchangePresentationTests.Director")
                .AddComponent<CombatDirector>();
            director.enabled = false;
            return director;
        }

        private GameObject NewObject(string name)
        {
            GameObject value = new GameObject(name);
            _createdObjects.Add(value);
            return value;
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
                return Orchestrator.BindFinalExchange(Session, StateMachine, Random, ClashInputs);
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
            public int NextInclusive(int minInclusive, int maxInclusive)
            {
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
