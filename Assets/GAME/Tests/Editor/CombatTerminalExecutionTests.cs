#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatTerminalExecutionTests
    {
        private GameObject _entryObject;

        [TearDown]
        public void TearDown()
        {
            if (_entryObject != null)
                UnityEngine.Object.DestroyImmediate(_entryObject);
        }

        [TestCase(CombatEndReason.Victory)]
        [TestCase(CombatEndReason.Defeat)]
        [TestCase(CombatEndReason.Scripted)]
        [TestCase(CombatEndReason.Abort)]
        public void PreparedTerminalDecision_EntersCanonicalExitWithExplicitReason(CombatEndReason reason)
        {
            Fixture fixture = CreatePreparedFixture(reason);

            Assert.That(fixture.StateMachine.TryExecutePreparedTerminalDecision(), Is.True);
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(reason));
            Assert.That(fixture.StateMachine.TryExecutePreparedTerminalDecision(), Is.False);
        }

        [Test]
        public void Command_RejectsMissingDecisionNonTerminalWrongPhaseAndLegacy()
        {
            Fixture missing = CreateFixture(CombatFlowMode.StandoffClashChain, Phase.ApplyOutcome);
            Fixture nonTerminal = CreateFixture(CombatFlowMode.StandoffClashChain, Phase.ApplyOutcome);
            nonTerminal.Session.ExchangeState.StoreAftermathDecision(
                new CombatAftermathDecision(CombatAftermathDecisionKind.ChainDecisionRequired));
            Fixture wrongPhase = CreatePreparedFixture(CombatEndReason.Victory, Phase.ChainDecision);
            Fixture legacy = CreateFixture(CombatFlowMode.LegacyPlanning, Phase.ApplyOutcome);
            legacy.Session.ExchangeState.StoreAftermathDecision(new CombatAftermathDecision(
                CombatAftermathDecisionKind.TerminalCandidate,
                CombatTerminalCandidate.EnemiesWiped));
            legacy.Session.ExchangeState.StoreTerminalDecision(new CombatTerminalDecision(
                CombatTerminalCandidate.EnemiesWiped,
                CombatEndReason.Victory));

            Assert.That(missing.StateMachine.TryExecutePreparedTerminalDecision(), Is.False);
            Assert.That(nonTerminal.StateMachine.TryExecutePreparedTerminalDecision(), Is.False);
            Assert.That(wrongPhase.StateMachine.TryExecutePreparedTerminalDecision(), Is.False);
            Assert.That(legacy.StateMachine.TryExecutePreparedTerminalDecision(), Is.False);
            Assert.That(missing.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
            Assert.That(nonTerminal.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
        }

        [Test]
        public void EntryPoint_UsesCanonicalFinishAndPublishesOneBuiltResult()
        {
            Fixture fixture = CreatePreparedFixture(CombatEndReason.Scripted);
            CombatEntryPoint entry = CreateEntryPoint(fixture);
            int eventCount = 0;
            CombatResult published = null;
            entry.OnCombatEnded += result =>
            {
                eventCount++;
                published = result;
            };

            Assert.That(entry.TryExecutePreparedTerminalDecision(), Is.True);
            Assert.That(entry.TryExecutePreparedTerminalDecision(), Is.False);

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(published, Is.Not.Null);
            Assert.That(published.EndReason, Is.EqualTo(CombatEndReason.Scripted));
            Assert.That(published.CompletionId, Is.EqualTo(fixture.Session.CompletionId));
            Assert.That(entry.ActiveSession, Is.Null);
            Assert.That(entry.ActiveStateMachine, Is.Null);
        }

        [Test]
        public void EntryPoint_RejectionDoesNotBuildOrPublishResult()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.StandoffClashChain, Phase.ApplyOutcome);
            CombatEntryPoint entry = CreateEntryPoint(fixture);
            int eventCount = 0;
            entry.OnCombatEnded += _ => eventCount++;

            Assert.That(entry.TryExecutePreparedTerminalDecision(), Is.False);
            Assert.That(eventCount, Is.Zero);
            Assert.That(entry.ActiveSession, Is.SameAs(fixture.Session));
            Assert.That(fixture.StateMachine.Phase, Is.EqualTo(Phase.ApplyOutcome));
            Assert.That(fixture.StateMachine.EndReason, Is.EqualTo(CombatEndReason.None));
        }

        private CombatEntryPoint CreateEntryPoint(Fixture fixture)
        {
            _entryObject = new GameObject("CombatTerminalExecutionEntry");
            CombatEntryPoint entry = _entryObject.AddComponent<CombatEntryPoint>();
            SetAutoProperty(entry, nameof(CombatEntryPoint.ActiveSession), fixture.Session);
            SetAutoProperty(entry, nameof(CombatEntryPoint.ActiveStateMachine), fixture.StateMachine);
            return entry;
        }

        private static Fixture CreatePreparedFixture(
            CombatEndReason reason,
            Phase phase = Phase.ApplyOutcome)
        {
            Fixture fixture = CreateFixture(CombatFlowMode.StandoffClashChain, phase);
            fixture.Session.ExchangeState.StoreAftermathDecision(new CombatAftermathDecision(
                CombatAftermathDecisionKind.TerminalCandidate,
                CombatTerminalCandidate.EnemiesWiped));
            fixture.Session.ExchangeState.StoreTerminalDecision(new CombatTerminalDecision(
                CombatTerminalCandidate.EnemiesWiped,
                reason));
            return fixture;
        }

        private static Fixture CreateFixture(CombatFlowMode flowMode, Phase phase)
        {
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10, 3),
                new Game.Combat.Environment.CombatEnvironment(),
                flowMode,
                CombatRuntimeConfig.Compatibility);
            session.Allies.Add(new FakeCombatant(1, Side.Allies));
            session.Enemies.Add(new FakeCombatant(2, Side.Enemies));
            CombatStateMachine stateMachine = new CombatStateMachine(session);
            SetAutoProperty(stateMachine, nameof(CombatStateMachine.Phase), phase);
            return new Fixture(session, stateMachine);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, propertyName);
            field.SetValue(target, value);
        }

        private readonly struct Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }

            public Fixture(CombatSession session, CombatStateMachine stateMachine)
            {
                Session = session;
                StateMachine = stateMachine;
            }
        }

        private sealed class FakeCombatant : ICombatant
        {
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP { get; private set; } = 10;
            public int MaxHP => 10;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger { get; private set; }
            public int StaggerMax => 10;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills { get; } = Array.Empty<ISkill>();

            public FakeCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public void ApplyDamage(int amount) => HP = Mathf.Max(0, HP - amount);
            public void AddStagger(int amount) => Stagger += amount;
            public void SetStunned(bool value) => IsStunned = value;
            public void ResetStaggerIfNeededOnStunEnd() => Stagger = 0;
        }
    }
}
#endif
