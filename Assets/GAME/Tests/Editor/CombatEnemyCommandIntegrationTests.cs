#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatEnemyCommandIntegrationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject value in _objects)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
        }

        [Test]
        public void EnemyInitiative_AutomaticallyDeclaresFirstValidSkillAgainstFirstAlly()
        {
            Fixture fixture = Create(Side.Enemies);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Exchange.CurrentDeclaration.Skill, Is.SameAs(fixture.EnemySkill));
            Assert.That(fixture.Exchange.CurrentDeclaration.Target, Is.SameAs(fixture.Ally));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void AllyRequest_IsIgnored()
        {
            Fixture fixture = Create(Side.Allies);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void EnemyResponse_UsesFirstAffordableSkill_OrNoResponse()
        {
            Fixture fixture = Create(Side.Allies);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            SubmitAllyAttack(fixture);

            Assert.That(fixture.Exchange.CurrentResponse.Responder, Is.SameAs(fixture.Enemy));

            Fixture noMp = Create(Side.Allies);
            noMp.Bind();
            noMp.Session.GetCombatState(noMp.Enemy).SetMp(0);
            FinalCombatEnemyCommandController noMpController = new FinalCombatEnemyCommandController();
            noMpController.Bind(noMp.Orchestrator, noMp.Session);
            SubmitAllyAttack(noMp);
            Assert.That(noMp.Exchange.ResponseState, Is.EqualTo(CombatResponseState.NoResponse));
        }

        [Test]
        public void EnemyDefenderWin_ContinuesWithoutPreselectingSkill()
        {
            Fixture fixture = Create(Side.Allies, allyPower: 1, enemyPower: 3);
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            SubmitAllyAttack(fixture);

            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void EnemyWithoutAttack_HandsStandoffAuthorityToAlliesWithoutFakeSkill()
        {
            Fixture fixture = Create(Side.Enemies, enemyHasSkill: false);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void PressureRequest_TriggersEnemyAttackOnce()
        {
            Fixture fixture = Create(Side.Allies, pressurePerSecond: 1f, pressureThreshold: 1f);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            fixture.Bind();
            controller.Bind(fixture.Orchestrator, fixture.Session);
            fixture.Orchestrator.Tick(1f);

            Assert.That(fixture.Exchange.CurrentDeclaration.Attacker, Is.SameAs(fixture.Enemy));
            Assert.That(fixture.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void StunnedEnemy_CannotDeclareAndYieldsAuthority()
        {
            Fixture fixture = Create(Side.Enemies);
            fixture.Bind();
            fixture.Enemy.SetStunned(true);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            controller.Bind(fixture.Orchestrator, fixture.Session);

            Assert.That(fixture.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(fixture.Exchange.CurrentAttackSide, Is.EqualTo(Side.Allies));
        }

        [Test]
        public void SessionSwitch_OldPendingRequestCannotCommandNewSession()
        {
            Fixture first = Create(Side.Enemies);
            FinalCombatEnemyCommandController controller = new FinalCombatEnemyCommandController();
            first.Bind();
            controller.Bind(first.Orchestrator, first.Session);
            controller.Unbind();

            Fixture second = Create(Side.Allies, orchestrator: first.Orchestrator);
            second.Bind();
            controller.Bind(second.Orchestrator, second.Session);

            Assert.That(second.Exchange.CurrentDeclaration, Is.Null);
            Assert.That(second.Orchestrator.PendingDecisionRequest.ActingSide, Is.EqualTo(Side.Allies));
        }

        private static void SubmitAllyAttack(Fixture fixture)
        {
            CombatExchangeDecisionRequest request = fixture.Orchestrator.PendingDecisionRequest;
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllySkill),
                request.ExchangeVersion), Is.True);
        }

        private Fixture Create(Side initiative, int allyPower = 2, int enemyPower = 1,
            bool enemyHasSkill = true, float pressurePerSecond = 0f, float pressureThreshold = 10f,
            CombatFlowOrchestrator orchestrator = null)
        {
            Actor ally = new Actor(1, Side.Allies); Actor allyTwo = new Actor(2, Side.Allies); Actor enemy = new Actor(11, Side.Enemies);
            Skill allySkill = new Skill(1); ally.Add(allySkill); allyTwo.Add(new Skill(2));
            Skill enemySkill = new Skill(11); if (enemyHasSkill) enemy.Add(enemySkill);
            CombatSession session = new CombatSession(StartReason.PlayerFirstHit, initiative, new InspirationPool(10), new Game.Combat.Environment.CombatEnvironment(), CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0, pressurePerSecond, pressureThreshold, 1f));
            session.Allies.Add(ally); session.Allies.Add(allyTwo); session.Enemies.Add(enemy);
            if (orchestrator == null) { GameObject owner = new GameObject("CombatEnemyCommandTests"); _objects.Add(owner); orchestrator = owner.AddComponent<CombatFlowOrchestrator>(); }
            return new Fixture(session, new CombatStateMachine(session), orchestrator, new ZeroRandom(), new Inputs(allyPower, enemyPower), ally, enemy, allySkill, enemySkill);
        }

        private sealed class Fixture
        {
            public readonly CombatSession Session; public readonly CombatStateMachine State; public readonly CombatFlowOrchestrator Orchestrator; public readonly Actor Ally; public readonly Actor Enemy; public readonly Skill AllySkill; public readonly Skill EnemySkill; public CombatExchangeState Exchange => Session.ExchangeState;
            private readonly ZeroRandom _random; private readonly Inputs _inputs;
            public Fixture(CombatSession s, CombatStateMachine state, CombatFlowOrchestrator o, ZeroRandom r, Inputs i, Actor a, Actor e, Skill as_, Skill es) { Session=s; State=state; Orchestrator=o; _random=r; _inputs=i; Ally=a; Enemy=e; AllySkill=as_; EnemySkill=es; }
            public bool Bind() => Orchestrator.BindFinalExchange(Session, State, _random, _inputs);
        }
        private sealed class ZeroRandom : ICombatRuleRandomSource { public int NextInclusive(int a, int b) => 0; }
        private sealed class Inputs : ICombatClashSideInputProvider { private readonly int _a,_e; public Inputs(int a,int e){_a=a;_e=e;} public bool TryCreate(ICombatant actor, ISkill skill, bool owner, out CombatClashSideInput input) { input=new CombatClashSideInput(actor.Id,skill.Id,actor.Side==Side.Allies?_a:_e,skill.Speed,owner); return true; } }
        private sealed class Skill : ISkill, ICombatMpCostProvider { public Skill(int id){Id=new SkillId(id);} public SkillId Id{get;} public string Name=>Id.Value.ToString(); public int InspirationCost=>0; public KeywordMask Keywords=>KeywordMask.None; public SkillTag Tag=>SkillTag.Attack; public TargetingRule Targeting=>TargetingRule.SingleEnemy; public SkillMovementMode MovementMode=>SkillMovementMode.None; public float DesiredTargetDistance=>0; public float MoveSpeed=>0; public float ActionDelayAfterMove=>0; public int BaseDamage=>1; public int BaseStagger=>0; public int WeaknessStaggerBonus=>0; public int Speed=>1; public bool ConsumesTurn=>true; public int MpCost=>1; }
        private sealed class Actor : ICombatant, ICombatantRuntimeStateBinding { private readonly List<ISkill> _skills=new List<ISkill>(); private CombatantCombatState _state; public Actor(int id,Side side){Id=new CombatantId(id);Side=side;} public CombatantId Id{get;} public Side Side{get;} public int HP=>_state?.CurrentHp??10; public int MaxHP=>10; public KeywordMask Weakness=>KeywordMask.None; public KeywordMask Resist=>KeywordMask.None; public int Stagger=>0; public int StaggerMax=>0; public bool IsStunned{get;private set;} public IReadOnlyList<ISkill> Skills=>_skills; public void Add(ISkill skill)=>_skills.Add(skill); public void BindCombatState(CombatantCombatState state)=>_state=state; public void ApplyDamage(int amount)=>_state?.ApplyDamage(amount); public void AddStagger(int amount){} public void SetStunned(bool value)=>IsStunned=value; public void ResetStaggerIfNeededOnStunEnd(){} }
    }
}
#endif
