#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class CombatPlayerCommandIntegrationTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void AllyAttack_RequestExposesActorRelativeOptions_AndSubmitsOnce()
        {
            Fixture fixture = CreateFixture();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            fixture.Bind();

            Assert.That(controller.Bind(fixture.Orchestrator, fixture.Session), Is.True);
            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
            Assert.That(controller.ViewState.ActingActor, Is.Null);
            Assert.That(controller.ViewState.SelectableActors, Does.Contain(fixture.Ally));
            Assert.That(controller.ViewState.SelectableActors, Does.Contain(fixture.AllyTwo));

            Assert.That(controller.SelectActor(fixture.Ally), Is.True);
            Assert.That(controller.ViewState.SelectableSkills, Does.Contain(fixture.AllySkill));
            Assert.That(controller.SelectSkill(fixture.AllySkill), Is.True);
            Assert.That(controller.ViewState.SelectableTargets, Does.Contain(fixture.Enemy));
            Assert.That(controller.SelectTarget(fixture.Enemy), Is.True);
            Assert.That(controller.ViewState.CanConfirm, Is.True);

            int mpBefore = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;
            Assert.That(controller.Confirm(), Is.True);
            Assert.That(controller.Confirm(), Is.False);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mpBefore));
            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
            Assert.That(controller.ViewState.DecisionKind, Is.Null);
        }

        [Test]
        public void EnemyAttackRequest_IsIgnored_WithoutAutomaticCommand()
        {
            Fixture fixture = CreateFixture(initiative: Side.Enemies);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Enemies));
            Assert.That(controller.ViewState.DecisionKind, Is.Null);
            Assert.That(controller.Confirm(), Is.False);
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
        }

        [Test]
        public void PlayerResponse_SelectsSkillAndCanonicalTarget_ThenEntersApproach()
        {
            Fixture fixture = CreateFixture(initiative: Side.Enemies);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            CombatApproachPresentationRequest approach = null;
            fixture.Orchestrator.ApproachPresentationRequested += request => approach = request;
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            CombatExchangeDecisionRequest enemyRequest = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill),
                enemyRequest.ExchangeVersion), Is.True);

            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Response));
            Assert.That(controller.ViewState.ActingActor, Is.SameAs(fixture.Ally));
            Assert.That(controller.SelectSkill(fixture.AllySkill), Is.True);
            Assert.That(controller.ViewState.SelectableTargets, Does.Contain(fixture.Enemy));
            Assert.That(controller.SelectTarget(fixture.Enemy), Is.True);
            Assert.That(controller.Confirm(), Is.True);
            Assert.That(approach, Is.Not.Null);
            Assert.That(fixture.Exchange.CurrentResponse.Responder, Is.SameAs(fixture.Ally));
        }

        [Test]
        public void PlayerResponse_NoResponse_UsesDriverCommandAndDoesNotSpendResponseMp()
        {
            Fixture fixture = CreateFixture(initiative: Side.Enemies);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            CombatApproachPresentationRequest approach = null;
            fixture.Orchestrator.ApproachPresentationRequested += request => approach = request;
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            CombatExchangeDecisionRequest enemyRequest = fixture.Orchestrator.PendingDecisionRequest;
            fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Enemy, fixture.Ally, fixture.EnemySkill),
                enemyRequest.ExchangeVersion);
            int mpBefore = fixture.Session.GetCombatState(fixture.Ally).CurrentMp;

            Assert.That(controller.ViewState.CanNoResponse, Is.True);
            Assert.That(controller.ConfirmNoResponse(), Is.True);
            Assert.That(approach, Is.Not.Null);
            Assert.That(fixture.Exchange.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMp, Is.EqualTo(mpBefore));
        }

        [Test]
        public void CancelSelection_IsLocalAndDoesNotMutateDriver()
        {
            Fixture fixture = CreateFixture();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            controller.SelectActor(fixture.Ally);
            controller.SelectSkill(fixture.AllySkill);
            controller.SelectTarget(fixture.Enemy);
            int version = fixture.Exchange.Version;

            Assert.That(controller.CancelSelection(), Is.True);
            Assert.That(controller.ViewState.SelectedActor, Is.Null);
            Assert.That(controller.ViewState.SelectedSkill, Is.Null);
            Assert.That(controller.ViewState.SelectedTarget, Is.Null);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version));
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
        }

        [Test]
        public void PlayerChain_CanContinueThenEnd()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            DrivePlayerNoResponseToChain(fixture, controller);

            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Chain));
            Assert.That(controller.ViewState.SelectableSkills, Is.Empty);
            Assert.That(controller.ViewState.CanContinue, Is.True);
            Assert.That(controller.ConfirmContinue(), Is.True);
            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
            Assert.That(controller.ViewState.ActingActor, Is.SameAs(fixture.Ally));

            Assert.That(controller.EndChain(), Is.False);
            Assert.That(controller.SelectSkill(fixture.AllySkill), Is.True);
            Assert.That(controller.SelectTarget(fixture.Enemy), Is.True);
            Assert.That(controller.CancelSelection(), Is.True);
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
        }

        [Test]
        public void PlayerChain_EndReturnsToStandoff()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            DrivePlayerNoResponseToChain(fixture, controller);

            Assert.That(controller.EndChain(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
        }

        [Test]
        public void Handoff_RequiresEligibilityProvider_ThenRequestsReceivingActorAttack()
        {
            Fixture fixture = CreateFixture(allyPower: 3, enemyPower: 1);
            FinalCombatPlayerCommandController unavailable = new FinalCombatPlayerCommandController();
            fixture.Bind();
            unavailable.Bind(fixture.Orchestrator, fixture.Session);
            DriveCurrentPlayerNoResponseToChain(fixture, unavailable);
            Assert.That(unavailable.ViewState.HandoffCandidates, Is.Empty);

            unavailable.Unbind();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController(
                new AllowOnlyActorProvider(fixture.AllyTwo));
            controller.Bind(fixture.Orchestrator, fixture.Session);

            int receivingMp = fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp;
            Assert.That(controller.ViewState.HandoffCandidates, Does.Contain(fixture.AllyTwo));
            Assert.That(controller.SelectHandoffTarget(fixture.AllyTwo), Is.True);
            Assert.That(controller.SelectHandoffSkill(fixture.AllyTwoSkill), Is.True);
            Assert.That(controller.ViewState.CanHandoff, Is.True);
            Assert.That(controller.ConfirmHandoff(), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.AllyTwo).CurrentMp, Is.EqualTo(receivingMp));
            Assert.That(controller.ViewState.DecisionKind, Is.EqualTo(CombatExchangeDecisionKind.Attack));
            Assert.That(controller.ViewState.ActingActor, Is.SameAs(fixture.AllyTwo));
        }

        [Test]
        public void StaleSelection_IsRejectedAfterDriverAdvances()
        {
            Fixture fixture = CreateFixture();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            controller.SelectActor(fixture.Ally);
            controller.SelectSkill(fixture.AllySkill);
            controller.SelectTarget(fixture.Enemy);

            CombatExchangeDecisionRequest request = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.AllyTwo, fixture.Enemy, fixture.AllyTwoSkill),
                request.ExchangeVersion), Is.True);

            Assert.That(controller.Confirm(), Is.False);
            Assert.That(controller.ViewState.DecisionKind, Is.Null);
            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.AllyTwo));
        }

        [Test]
        public void SessionSwitch_ClearsOldSelectionAndCannotCommandNewSession()
        {
            Fixture first = CreateFixture();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            first.Bind();
            controller.Bind(first.Orchestrator, first.Session);
            controller.SelectActor(first.Ally);
            controller.SelectSkill(first.AllySkill);
            controller.SelectTarget(first.Enemy);

            Fixture second = CreateFixture(orchestrator: first.Orchestrator);
            second.Bind();
            Assert.That(controller.Bind(second.Orchestrator, second.Session), Is.True);

            Assert.That(controller.Confirm(), Is.False);
            Assert.That(second.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(second.Orchestrator.PendingDecisionRequest, Is.Not.Null);
        }

        [Test]
        public void InsufficientMpSkill_IsNotSelectable()
        {
            Fixture fixture = CreateFixture();
            fixture.Bind();
            fixture.Session.GetCombatState(fixture.Ally).SetMp(0);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(Contains(controller.ViewState.SelectableActors, fixture.Ally), Is.False);
            Assert.That(controller.SelectActor(fixture.Ally), Is.False);
        }

        [Test]
        public void StunnedAndDefeatedActors_AreNotSelectable()
        {
            Fixture fixture = CreateFixture();
            fixture.Bind();
            fixture.Ally.SetStunned(true);
            fixture.Session.GetCombatState(fixture.AllyTwo).ApplyDamage(fixture.AllyTwo.MaxHP);
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(Contains(controller.ViewState.SelectableActors, fixture.Ally), Is.False);
            Assert.That(Contains(controller.ViewState.SelectableActors, fixture.AllyTwo), Is.False);
        }

        private void DrivePlayerNoResponseToChain(Fixture fixture, FinalCombatPlayerCommandController controller)
        {
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            DriveCurrentPlayerNoResponseToChain(fixture, controller);
        }

        private static void DriveCurrentPlayerNoResponseToChain(
            Fixture fixture,
            FinalCombatPlayerCommandController controller)
        {
            CombatApproachPresentationRequest approach = null;
            fixture.Orchestrator.ApproachPresentationRequested += request => approach = request;
            controller.SelectActor(fixture.Ally);
            controller.SelectSkill(fixture.AllySkill);
            controller.SelectTarget(fixture.Enemy);
            Assert.That(controller.Confirm(), Is.True);
            CombatExchangeDecisionRequest responseRequest = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(fixture.Orchestrator.SubmitNoResponse(responseRequest.ExchangeVersion), Is.True);
            Assert.That(approach, Is.Not.Null);
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        private Fixture CreateFixture(
            Side initiative = Side.Allies,
            int allyPower = 2,
            int enemyPower = 1,
            CombatFlowOrchestrator orchestrator = null)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant allyTwo = new TestCombatant(2, Side.Allies);
            TestCombatant enemy = new TestCombatant(11, Side.Enemies);
            TestSkill allySkill = AddSkill(ally, 1);
            TestSkill allyTwoSkill = AddSkill(allyTwo, 2);
            TestSkill enemySkill = AddSkill(enemy, 11);
            CombatRuntimeConfig config = new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f);
            CombatSession session = new CombatSession(
                initiative == Side.Allies ? StartReason.PlayerFirstHit : StartReason.PlayerGotHit,
                initiative,
                new InspirationPool(10),
                new Game.Combat.Environment.CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                config);
            session.Allies.Add(ally);
            session.Allies.Add(allyTwo);
            session.Enemies.Add(enemy);
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            if (orchestrator == null)
            {
                GameObject owner = new GameObject("CombatPlayerCommandIntegrationTests.Orchestrator");
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

        private static TestSkill AddSkill(TestCombatant actor, int id)
        {
            TestSkill skill = new TestSkill(id);
            actor.AddSkill(skill);
            return skill;
        }

        private static bool Contains(IReadOnlyList<ICombatant> values, ICombatant expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], expected))
                    return true;
            }

            return false;
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; set; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public ICombatant Ally { get; }
            public ICombatant AllyTwo { get; }
            public ICombatant Enemy { get; }
            public ISkill AllySkill { get; }
            public ISkill AllyTwoSkill { get; }
            public ISkill EnemySkill { get; }
            public CombatExchangeState Exchange => Session.ExchangeState;
            private ICombatRuleRandomSource Random { get; }
            private ICombatClashSideInputProvider ClashInputs { get; }

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                CombatFlowOrchestrator orchestrator,
                ICombatRuleRandomSource random,
                ICombatClashSideInputProvider clashInputs,
                ICombatant ally,
                ICombatant allyTwo,
                ICombatant enemy,
                ISkill allySkill,
                ISkill allyTwoSkill,
                ISkill enemySkill)
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
        }

        private sealed class AllowOnlyActorProvider : ICombatHandoffEligibilityProvider
        {
            private readonly ICombatant _allowed;

            public AllowOnlyActorProvider(ICombatant allowed)
            {
                _allowed = allowed;
            }

            public bool IsEligible(
                CombatSession session,
                ICombatant currentAttackOwner,
                ICombatant receivingActor)
            {
                return ReferenceEquals(receivingActor, _allowed);
            }
        }

        private sealed class ZeroRandomSource : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => 0;
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
            public int BaseDamage => 1;
            public int BaseStagger => 0;
            public int WeaknessStaggerBonus => 0;
            public int Speed => 1;
            public bool ConsumesTurn => true;
            public int MpCost => 1;

            public TestSkill(int id)
            {
                Id = new SkillId(id);
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

            public void AddSkill(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount)
            {
                if (_state != null)
                    _state.ApplyDamage(amount);
                else
                    _hp = Math.Max(0, _hp - amount);
            }

            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
