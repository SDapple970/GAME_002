#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatOvercomeIntegrationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void PlayerOvercome_RecoversMentalConsumesAuthorityAndRejectsDuplicate()
        {
            Fixture fixture = Create(Side.Allies);
            fixture.Bind();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            Panic(fixture, fixture.Ally);

            Assert.That(controller.SelectActor(fixture.Ally), Is.True);
            Assert.That(controller.ViewState.CanOvercome, Is.True);
            int version = fixture.Exchange.Version;

            Assert.That(controller.ConfirmOvercome(), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).CurrentMental, Is.EqualTo(35));
            Assert.That(fixture.Session.GetCombatState(fixture.Ally).MentalState,
                Is.EqualTo(CombatMentalState.Stable));
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version + 1));
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Orchestrator.PendingApproachRequest, Is.Null);
            Assert.That(controller.ConfirmOvercome(), Is.False);
            Assert.That(fixture.Exchange.Version, Is.EqualTo(version + 1));
        }

        [Test]
        public void StateMachine_OvercomeRejectsStableStunnedDeadWrongAuthorityWrongPhaseAndStale()
        {
            Fixture stable = Create(Side.Allies);
            stable.Bind();
            AssertRejected(stable, stable.Ally, stable.Exchange.Version, CombatOvercomeFailureReason.NotPanicked);

            Fixture stunned = Create(Side.Allies);
            stunned.Bind();
            Panic(stunned, stunned.Ally);
            stunned.Ally.SetStunned(true);
            AssertRejected(stunned, stunned.Ally, stunned.Exchange.Version, CombatOvercomeFailureReason.Stunned);

            Fixture dead = Create(Side.Allies);
            dead.Bind();
            Panic(dead, dead.Ally);
            dead.Session.GetCombatState(dead.Ally).ApplyDamage(10);
            AssertRejected(dead, dead.Ally, dead.Exchange.Version, CombatOvercomeFailureReason.InvalidActor);

            Fixture wrongAuthority = Create(Side.Allies);
            wrongAuthority.Bind();
            Panic(wrongAuthority, wrongAuthority.Enemy);
            AssertRejected(
                wrongAuthority,
                wrongAuthority.Enemy,
                wrongAuthority.Exchange.Version,
                CombatOvercomeFailureReason.NotActionAuthority);

            Fixture stale = Create(Side.Allies);
            stale.Bind();
            Panic(stale, stale.Ally);
            AssertRejected(stale, stale.Ally, stale.Exchange.Version - 1, CombatOvercomeFailureReason.StaleRequest);

            Fixture wrongPhase = Create(Side.Allies);
            wrongPhase.Bind();
            Assert.That(wrongPhase.State.TryDeclareAttack(
                new CombatAttackDeclaration(wrongPhase.Ally, wrongPhase.Enemy, wrongPhase.AllySkill),
                wrongPhase.Exchange.Version), Is.True);
            Panic(wrongPhase, wrongPhase.Ally);
            AssertRejected(
                wrongPhase,
                wrongPhase.Ally,
                wrongPhase.Exchange.Version,
                CombatOvercomeFailureReason.InvalidPhase);
        }

        [Test]
        public void EnemyPanicked_AutomaticallyOvercomesInsteadOfSubmittingAttack()
        {
            Fixture fixture = Create(Side.Enemies);
            fixture.Bind();
            Panic(fixture, fixture.Enemy);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();

            Assert.That(controller.Bind(fixture.Orchestrator, fixture.Session), Is.True);
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).CurrentMental, Is.EqualTo(35));
            Assert.That(fixture.Session.GetCombatState(fixture.Enemy).IsPanicked, Is.False);
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void PlayerViewState_ExposesOnlyTheSelectedEligiblePanickedActor()
        {
            Fixture fixture = Create(Side.Allies);
            fixture.Bind();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(controller.ViewState.CanOvercome, Is.False);
            Panic(fixture, fixture.Ally);
            Assert.That(controller.SelectActor(fixture.Ally), Is.True);
            Assert.That(controller.ViewState.CanOvercome, Is.True);
            fixture.Ally.SetStunned(true);
            Assert.That(controller.CancelSelection(), Is.True);
            Assert.That(controller.ViewState.CanOvercome, Is.False);
        }

        [Test]
        public void SessionSwitch_OldPlayerOvercomeCommandCannotMutateTheNewSession()
        {
            Fixture first = Create(Side.Allies);
            first.Bind();
            FinalCombatPlayerCommandController controller = new FinalCombatPlayerCommandController();
            controller.Bind(first.Orchestrator, first.Session);
            Panic(first, first.Ally);
            Assert.That(controller.SelectActor(first.Ally), Is.True);

            Fixture second = Create(Side.Allies);
            Assert.That(first.Orchestrator.BindFinalExchange(
                second.Session,
                second.State,
                new ZeroRandom(),
                new Inputs()), Is.True);

            Assert.That(controller.ConfirmOvercome(), Is.False);
            Assert.That(second.Session.GetCombatState(second.Ally).CurrentMental, Is.EqualTo(100));
            Assert.That(second.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
        }

        private static void AssertRejected(
            Fixture fixture,
            Actor actor,
            int version,
            CombatOvercomeFailureReason reason)
        {
            int mental = fixture.Session.GetCombatState(actor).CurrentMental;
            Assert.That(fixture.State.TryExecuteOvercome(actor, version, out CombatOvercomeResult result), Is.False);
            Assert.That(result.WasAccepted, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(fixture.Session.GetCombatState(actor).CurrentMental, Is.EqualTo(mental));
        }

        private static void Panic(Fixture fixture, Actor actor)
        {
            CombatantCombatState state = fixture.Session.GetCombatState(actor);
            state.ApplyMentalDelta(-state.MaxMental);
            Assert.That(state.IsPanicked, Is.True);
        }

        private Fixture Create(Side initiative)
        {
            Actor ally = new Actor(1, Side.Allies);
            Actor enemy = new Actor(11, Side.Enemies);
            Skill allySkill = new Skill(1);
            Skill enemySkill = new Skill(11);
            ally.Add(allySkill);
            enemy.Add(enemySkill);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                initiative,
                new InspirationPool(10),
                new Game.Combat.Environment.CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0, 0f, 10f, 1f));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            GameObject owner = new GameObject("CombatOvercomeIntegrationTests");
            _objects.Add(owner);
            CombatFlowOrchestrator orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
            return new Fixture(session, new CombatStateMachine(session), orchestrator, ally, enemy, allySkill);
        }

        private sealed class Fixture
        {
            public Fixture(
                CombatSession session,
                CombatStateMachine state,
                CombatFlowOrchestrator orchestrator,
                Actor ally,
                Actor enemy,
                Skill allySkill)
            {
                Session = session;
                State = state;
                Orchestrator = orchestrator;
                Ally = ally;
                Enemy = enemy;
                AllySkill = allySkill;
            }

            public CombatSession Session { get; }
            public CombatStateMachine State { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public Actor Ally { get; }
            public Actor Enemy { get; }
            public Skill AllySkill { get; }
            public CombatExchangeState Exchange => Session.ExchangeState;

            public bool Bind()
            {
                return Orchestrator.BindFinalExchange(
                    Session,
                    State,
                    new ZeroRandom(),
                    new Inputs());
            }
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => minInclusive;
        }

        private sealed class Inputs : ICombatClashSideInputProvider
        {
            public bool TryCreate(
                ICombatant actor,
                ISkill skill,
                bool isAttackOwner,
                out CombatClashSideInput input)
            {
                input = new CombatClashSideInput(actor.Id, skill.Id, 1, skill.Speed, isAttackOwner);
                return true;
            }
        }

        private sealed class Skill : ISkill, ICombatMpCostProvider
        {
            public Skill(int id) => Id = new SkillId(id);

            public SkillId Id { get; }
            public string Name => Id.Value.ToString();
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
        }

        private sealed class Actor : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;

            public Actor(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

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

            public void Add(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) => _state?.ApplyDamage(amount);
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() { }
        }
    }
}
#endif
