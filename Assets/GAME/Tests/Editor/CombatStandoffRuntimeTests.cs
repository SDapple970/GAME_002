#if UNITY_INCLUDE_TESTS
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Model;
using NUnit.Framework;

namespace Game.Tests.Combat
{
    public sealed class CombatStandoffRuntimeTests
    {
        [Test]
        public void LegacyPlanning_DoesNotAdvanceMpOrPressureWithExplicitRuntimeConfig()
        {
            CombatRuntimeConfig config = CreateConfig(maxMp: 10, initialMp: 1, mpPerSecond: 4f, pressureMax: 5f, pressurePerSecond: 2f);
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning, config);
            fixture.StateMachine.Tick();
            CombatantCombatState state = fixture.Session.GetCombatState(fixture.Ally);

            fixture.StateMachine.Tick(2f);

            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Planning));
            Assert.That(state.CurrentMp, Is.EqualTo(1));
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
        }

        [Test]
        public void Standoff_RecoversMpProportionallyAcrossFractionalFrames()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(10, 0, 2.5f, 10f, 0f));
            CombatantCombatState allyState = fixture.Session.GetCombatState(fixture.Ally);
            CombatantCombatState enemyState = fixture.Session.GetCombatState(fixture.Enemy);

            fixture.StateMachine.Tick(0.2f);
            Assert.That(allyState.CurrentMp, Is.Zero);

            fixture.StateMachine.Tick(0.2f);
            Assert.That(allyState.CurrentMp, Is.EqualTo(1));

            fixture.StateMachine.Tick(0.8f);
            Assert.That(allyState.CurrentMp, Is.EqualTo(3));
            Assert.That(enemyState.CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void Standoff_MpRecoveryClampsAtMaxAndIgnoresInvalidDelta()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(5, 4, 10f, 10f, 0f));
            CombatantCombatState state = fixture.Session.GetCombatState(fixture.Ally);

            fixture.StateMachine.Tick(0f);
            fixture.StateMachine.Tick(-1f);
            fixture.StateMachine.Tick(float.NaN);
            Assert.That(state.CurrentMp, Is.EqualTo(4));

            fixture.StateMachine.Tick(10f);
            Assert.That(state.CurrentMp, Is.EqualTo(5));
            fixture.StateMachine.Tick(10f);
            Assert.That(state.CurrentMp, Is.EqualTo(5));
        }

        [Test]
        public void Standoff_DoesNotRecoverDeadCombatants()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(10, 0, 5f, 10f, 0f));
            fixture.Enemy.ApplyDamage(int.MaxValue);
            CombatantCombatState enemyState = fixture.Session.GetCombatState(fixture.Enemy);

            fixture.StateMachine.Tick(1f);

            Assert.That(enemyState.CurrentMp, Is.Zero);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
        }

        [Test]
        public void NonStandoffAndExitedPhases_DoNotAdvanceRuntime()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(10, 0, 5f, 10f, 3f));
            CombatantCombatState state = fixture.Session.GetCombatState(fixture.Ally);
            SetPhase(fixture.StateMachine, Phase.Approach);

            fixture.StateMachine.Tick(1f);
            Assert.That(state.CurrentMp, Is.Zero);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);

            fixture.StateMachine.ForceExit(CombatEndReason.Abort);
            fixture.StateMachine.Tick(1f);
            Assert.That(state.CurrentMp, Is.Zero);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
        }

        [Test]
        public void Pressure_AdvancesProportionallyClampsAndSignalsReadyOnce()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(0, 0, 0f, 5f, 2f));
            int readySignals = 0;
            fixture.StateMachine.OnEnemyActionRequired += session =>
            {
                Assert.That(session, Is.SameAs(fixture.Session));
                readySignals++;
            };

            fixture.StateMachine.Tick(0.5f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fixture.Session.StandoffState.IsPressureReady, Is.False);

            fixture.StateMachine.Tick(2f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(5f));
            Assert.That(fixture.Session.StandoffState.IsPressureReady, Is.True);
            Assert.That(readySignals, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));

            fixture.StateMachine.Tick(100f);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.EqualTo(5f));
            Assert.That(readySignals, Is.EqualTo(1));
        }

        [Test]
        public void Pressure_IgnoresInvalidDeltaAndCanBeResetExplicitly()
        {
            Fixture fixture = CreateStandoffFixture(CreateConfig(0, 0, 0f, 4f, 2f));

            fixture.StateMachine.Tick(0f);
            fixture.StateMachine.Tick(-2f);
            fixture.StateMachine.Tick(float.PositiveInfinity);
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);

            fixture.StateMachine.Tick(1f);
            fixture.Session.StandoffState.Reset();
            Assert.That(fixture.Session.StandoffState.CurrentPressure, Is.Zero);
            Assert.That(fixture.Session.StandoffState.IsPressureReady, Is.False);
        }

        [Test]
        public void RuntimeConfig_CompatibilityAndInvalidValuesRemainSafe()
        {
            CombatRuntimeConfig compatibility = CombatRuntimeConfig.Compatibility;
            Assert.That(compatibility.MaxMp, Is.Zero);
            Assert.That(compatibility.MpRecoveryPerSecond, Is.Zero);
            Assert.That(compatibility.PressureMax, Is.Zero);
            Assert.That(compatibility.PressurePerSecond, Is.Zero);

            CombatRuntimeConfig invalid = new CombatRuntimeConfig(
                -1,
                99,
                -2,
                99,
                -3f,
                float.NaN,
                float.PositiveInfinity);
            Assert.That(invalid.MaxMp, Is.Zero);
            Assert.That(invalid.InitialMp, Is.Zero);
            Assert.That(invalid.MaxPosture, Is.Zero);
            Assert.That(invalid.InitialPosture, Is.Zero);
            Assert.That(invalid.MpRecoveryPerSecond, Is.Zero);
            Assert.That(invalid.PressureMax, Is.Zero);
            Assert.That(invalid.PressurePerSecond, Is.Zero);
        }

        [Test]
        public void ExplicitRequest_PropagatesRuntimeConfigWithoutChangingTurnOrInspiration()
        {
            CombatRuntimeConfig config = CreateConfig(8, 2, 3f, 6f, 1.5f);
            CombatStartRequest request = new CombatStartRequest(
                StartReason.PlayerFirstHit,
                Side.Allies,
                10,
                3,
                null,
                CombatFlowMode.StandoffClashChain,
                config);

            (CombatSession session, CombatStateMachine stateMachine) = CombatBootstrapper.StartCombat(
                request,
                new SkillBook(),
                new TestCombatantFactory());

            Assert.That(session.RuntimeConfig.MpRecoveryPerSecond, Is.EqualTo(3f));
            Assert.That(session.StandoffState.MaxPressure, Is.EqualTo(6f));
            Assert.That(session.GetCombatState(session.Allies[0]).CurrentMp, Is.EqualTo(2));
            Assert.That(stateMachine.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(session.TurnIndex, Is.Zero);
            Assert.That(session.Inspiration.Current, Is.EqualTo(3));
        }

        private static Fixture CreateStandoffFixture(CombatRuntimeConfig config)
        {
            Fixture fixture = CreateFixture(CombatFlowMode.StandoffClashChain, config);
            fixture.StateMachine.Tick();
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.Standoff));
            return fixture;
        }

        private static Fixture CreateFixture(CombatFlowMode flowMode, CombatRuntimeConfig config)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment(),
                flowMode,
                config);
            DummyCombatant ally = CreateCombatant(1, Side.Allies);
            DummyCombatant enemy = CreateCombatant(100, Side.Enemies);
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            return new Fixture(session, new CombatStateMachine(session), ally, enemy);
        }

        private static CombatRuntimeConfig CreateConfig(
            int maxMp,
            int initialMp,
            float mpPerSecond,
            float pressureMax,
            float pressurePerSecond)
        {
            return new CombatRuntimeConfig(maxMp, initialMp, 0, 0, mpPerSecond, pressureMax, pressurePerSecond);
        }

        private static DummyCombatant CreateCombatant(int id, Side side)
        {
            return new DummyCombatant(id, side, 10, KeywordMask.None, 6);
        }

        private static void SetPhase(CombatStateMachine stateMachine, Phase phase)
        {
            PropertyInfo property = typeof(CombatStateMachine).GetProperty(
                nameof(CombatStateMachine.Phase),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(stateMachine, phase);
        }

        private sealed class TestCombatantFactory : ICombatantFactory
        {
            public void PopulateCombatants(CombatSession session, CombatStartRequest request)
            {
                session.Allies.Add(CreateCombatant(1, Side.Allies));
                session.Enemies.Add(CreateCombatant(100, Side.Enemies));
            }
        }

        private sealed class Fixture
        {
            public readonly CombatSession Session;
            public readonly CombatStateMachine StateMachine;
            public readonly DummyCombatant Ally;
            public readonly DummyCombatant Enemy;

            public Fixture(
                CombatSession session,
                CombatStateMachine stateMachine,
                DummyCombatant ally,
                DummyCombatant enemy)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
            }
        }
    }
}
#endif
