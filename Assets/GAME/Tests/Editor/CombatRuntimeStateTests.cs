#if UNITY_INCLUDE_TESTS
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using NUnit.Framework;

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
    }
}
#endif
