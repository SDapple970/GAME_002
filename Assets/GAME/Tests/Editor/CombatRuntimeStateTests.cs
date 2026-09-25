#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatRuntimeStateTests
    {
        [Test]
        public void CombatantState_InitialValuesAreClampedToValidRanges()
        {
            DummyCombatant combatant = CreateCombatant(1, Side.Allies);
            CombatantCombatState state = new CombatantCombatState(
                combatant,
                new CombatRuntimeConfig(5, 99, -10, 4));

            Assert.That(state.CurrentMp, Is.EqualTo(5));
            Assert.That(state.MaxMp, Is.EqualTo(5));
            Assert.That(state.CurrentPosture, Is.Zero);
            Assert.That(state.MaxPosture, Is.Zero);
        }

        [Test]
        public void FieldCombatant_SnapshotsHpAndIgnoresFieldMutationDuringCombat()
        {
            GameObject fieldObject = new GameObject("RuntimeStateFieldCombatant");
            try
            {
                CombatHpComponent fieldHp = fieldObject.AddComponent<CombatHpComponent>();
                fieldHp.MaxHP = 20;
                fieldHp.HP = 15;
                FieldCombatantAdapter combatant = new FieldCombatantAdapter(
                    1,
                    Side.Allies,
                    fieldObject,
                    HpAccessor.TryCreate(fieldObject),
                    6);
                CombatSession session = CreateSession(Side.Allies);
                session.Allies.Add(combatant);
                session.InitializeCombatStates(CombatRuntimeConfig.Compatibility);

                CombatantCombatState state = session.GetCombatState(combatant);
                fieldHp.HP = 3;
                combatant.ApplyDamage(5);

                Assert.That(state.MaxHp, Is.EqualTo(20));
                Assert.That(state.CurrentHp, Is.EqualTo(10));
                Assert.That(state.IsAlive, Is.True);
                Assert.That(combatant.HP, Is.EqualTo(10));
                Assert.That(fieldHp.HP, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(fieldObject);
            }
        }

        [Test]
        public void SkillRunner_DamagesRuntimeStateWithoutMutatingFieldHp()
        {
            GameObject fieldObject = new GameObject("RuntimeStateSkillTarget");
            try
            {
                CombatHpComponent fieldHp = fieldObject.AddComponent<CombatHpComponent>();
                fieldHp.MaxHP = 10;
                fieldHp.HP = 10;
                FieldCombatantAdapter target = new FieldCombatantAdapter(
                    100,
                    Side.Enemies,
                    fieldObject,
                    HpAccessor.TryCreate(fieldObject),
                    8);
                DummyCombatant actor = CreateCombatant(1, Side.Allies);
                RuntimeStateSkill skill = new RuntimeStateSkill(4);
                actor.AddSkill(skill);

                CombatSession session = CreateSession(Side.Allies);
                session.Allies.Add(actor);
                session.Enemies.Add(target);
                session.InitializeCombatStates(CombatRuntimeConfig.Compatibility);
                CombatSkillExecutionRequest request = new CombatSkillExecutionRequest(
                    actor,
                    skill,
                    new List<ICombatant> { target },
                    target,
                    CombatClashOutcome.AttackerWin);

                Assert.That(SkillRunner.TryExecute(session, request, out CombatSkillExecutionResult result), Is.True);
                Assert.That(result.TargetResults[0].HpBefore, Is.EqualTo(10));
                Assert.That(result.TargetResults[0].HpAfter, Is.EqualTo(6));
                Assert.That(session.GetCombatState(target).CurrentHp, Is.EqualTo(6));
                Assert.That(fieldHp.HP, Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(fieldObject);
            }
        }

        [Test]
        public void ResultBuilder_UsesRuntimeHpForDeathAndWritebackSnapshot()
        {
            CombatSession session = CreateSession(Side.Allies);
            DummyCombatant ally = CreateCombatant(1, Side.Allies);
            DummyCombatant enemy = CreateCombatant(100, Side.Enemies);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            session.InitializeCombatStates(CombatRuntimeConfig.Compatibility);

            enemy.ApplyDamage(int.MaxValue);
            CombatResult result = CombatResultBuilder.Build(session, CombatEndReason.Victory);

            Assert.That(session.GetCombatState(enemy).CurrentHp, Is.Zero);
            Assert.That(session.GetCombatState(enemy).IsAlive, Is.False);
            Assert.That(result.DefeatedEnemyIds, Does.Contain(100));
            Assert.That(result.SurvivedAllyIds, Does.Contain(1));
            Assert.That(result.RemainingHpByCombatantId[100], Is.Zero);
            Assert.That(result.RemainingHpByCombatantId[1], Is.EqualTo(10));
        }

        [Test]
        public void CombatantState_MpSpendAndRestoreStayWithinBounds()
        {
            CombatantCombatState state = CreateState(maxMp: 5, initialMp: 4, maxPosture: 10);

            Assert.That(state.CanSpendMp(3), Is.True);
            Assert.That(state.TrySpendMp(3), Is.True);
            Assert.That(state.CurrentMp, Is.EqualTo(1));
            Assert.That(state.TrySpendMp(2), Is.False);
            Assert.That(state.TrySpendMp(-1), Is.False);
            Assert.That(state.CurrentMp, Is.EqualTo(1));

            state.RestoreMp(int.MaxValue);
            Assert.That(state.CurrentMp, Is.EqualTo(5));
            state.SetMp(-100);
            Assert.That(state.CurrentMp, Is.Zero);
        }

        [Test]
        public void CombatantState_PostureChangesStayWithinBoundsAndReportMaximum()
        {
            CombatantCombatState state = CreateState(maxMp: 5, initialMp: 5, maxPosture: 10);

            state.AddPosture(7);
            Assert.That(state.CurrentPosture, Is.EqualTo(7));
            Assert.That(state.IsPostureMax, Is.False);

            state.AddPosture(int.MaxValue);
            Assert.That(state.CurrentPosture, Is.EqualTo(10));
            Assert.That(state.IsPostureMax, Is.True);

            state.ReducePosture(99);
            Assert.That(state.CurrentPosture, Is.Zero);
            state.SetPosture(-1);
            Assert.That(state.CurrentPosture, Is.Zero);
        }

        [Test]
        public void Session_RegistersEveryRosterMemberOnceAndHandlesUnknownCombatants()
        {
            CombatSession session = CreateSession(Side.Allies);
            DummyCombatant player = CreateCombatant(1, Side.Allies);
            DummyCombatant ally = CreateCombatant(2, Side.Allies);
            DummyCombatant enemy = CreateCombatant(100, Side.Enemies);
            DummyCombatant unknown = CreateCombatant(999, Side.Enemies);
            session.Allies.Add(player);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);

            CombatRuntimeConfig config = new CombatRuntimeConfig(6, 4, 12, 2);
            session.InitializeCombatStates(config);
            session.InitializeCombatStates(config);

            Assert.That(session.CombatStateCount, Is.EqualTo(3));
            Assert.That(session.GetCombatState(player), Is.SameAs(session.GetCombatState(player)));
            Assert.That(session.TryGetCombatState(ally, out CombatantCombatState allyState), Is.True);
            Assert.That(allyState.Combatant, Is.SameAs(ally));
            Assert.That(session.TryGetCombatState(enemy, out _), Is.True);
            Assert.That(session.TryGetCombatState(unknown, out CombatantCombatState missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(session.TryGetCombatState(null, out _), Is.False);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => session.GetCombatState(unknown));
        }

        [Test]
        public void Bootstrapper_InitializesRuntimeStatesAndAttackRightFromRequestInitiative()
        {
            CombatStartRequest request = new CombatStartRequest(
                StartReason.PlayerGotHit,
                Side.Enemies,
                10,
                3,
                null);

            (CombatSession session, CombatStateMachine stateMachine) = CombatBootstrapper.StartCombat(
                request,
                new SkillBook(),
                new ThreeCombatantFactory());

            Assert.That(session.CombatStateCount, Is.EqualTo(3));
            Assert.That(session.ExchangeState.InitialInitiative, Is.EqualTo(Side.Enemies));
            Assert.That(session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));
            Assert.That(session.ExchangeState.IsChainActive, Is.False);
            Assert.That(session.ExchangeState.ChainOwner, Is.Null);
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(session.TurnIndex, Is.EqualTo(1));
            Assert.That(session.Inspiration.Current, Is.EqualTo(4));
        }

        [Test]
        public void StateMachineConstruction_InitializesRuntimeStateBeforeFirstTurnAndPreservesIdentity()
        {
            CombatSession session = CreateSession(Side.Enemies);
            DummyCombatant ally = CreateCombatant(1, Side.Allies);
            DummyCombatant enemy = CreateCombatant(100, Side.Enemies);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);

            CombatStateMachine stateMachine = new CombatStateMachine(session, null, null);

            Assert.That(session.TurnIndex, Is.Zero);
            Assert.That(session.CombatStateCount, Is.EqualTo(2));
            Assert.That(session.TryGetCombatState(ally, out CombatantCombatState stateBeforeTurn), Is.True);
            Assert.That(session.TryGetCombatState(enemy, out _), Is.True);
            Assert.That(session.ExchangeState, Is.Not.Null);
            Assert.That(session.ExchangeState.CurrentAttackSide, Is.EqualTo(Side.Enemies));

            stateMachine.Tick();

            Assert.That(session.TurnIndex, Is.EqualTo(1));
            Assert.That(session.GetCombatState(ally), Is.SameAs(stateBeforeTurn));
        }

        [Test]
        public void ExchangeState_RejectsOwnerlessActiveChainAndClearsOwnerWhenInactive()
        {
            DummyCombatant owner = CreateCombatant(1, Side.Allies);
            CombatExchangeState state = new CombatExchangeState(Side.Allies);

            state.SetChainState(true, null);
            Assert.That(state.IsChainActive, Is.False);
            Assert.That(state.ChainOwner, Is.Null);

            state.SetChainState(true, owner);
            Assert.That(state.IsChainActive, Is.True);
            Assert.That(state.ChainOwner, Is.SameAs(owner));

            state.SetChainState(false, owner);
            Assert.That(state.IsChainActive, Is.False);
            Assert.That(state.ChainOwner, Is.Null);
        }

        private static CombatantCombatState CreateState(int maxMp, int initialMp, int maxPosture)
        {
            return new CombatantCombatState(
                CreateCombatant(1, Side.Allies),
                new CombatRuntimeConfig(maxMp, initialMp, maxPosture, 0));
        }

        private static CombatSession CreateSession(Side initiative)
        {
            return new CombatSession(
                StartReason.PlayerFirstHit,
                initiative,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment());
        }

        private static DummyCombatant CreateCombatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 10, KeywordMask.None, 6);
        }

        private sealed class ThreeCombatantFactory : ICombatantFactory
        {
            public void PopulateCombatants(CombatSession session, CombatStartRequest request)
            {
                session.Allies.Add(CreateCombatant(1, Side.Allies));
                session.Allies.Add(CreateCombatant(2, Side.Allies));
                session.Enemies.Add(CreateCombatant(100, Side.Enemies));
            }
        }

        private sealed class RuntimeStateSkill : ISkill
        {
            public SkillId Id { get; } = new SkillId(777);
            public string Name => "Runtime State Test";
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
            public int Speed => 0;
            public bool ConsumesTurn => true;

            public RuntimeStateSkill(int baseDamage)
            {
                BaseDamage = baseDamage;
            }
        }
    }
}
#endif
