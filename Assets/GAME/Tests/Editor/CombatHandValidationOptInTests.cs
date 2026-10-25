#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Debugging;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatHandValidationOptInTests
    {
        private GameObject _owner;
        private readonly List<ScriptableObject> _definitions = new();

        [TearDown]
        public void TearDown()
        {
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
            for (int i = 0; i < _definitions.Count; i++)
                UnityEngine.Object.DestroyImmediate(_definitions[i]);
            _definitions.Clear();
        }

        // Capacities and seed are opt-in validation fixtures, not Production balance.
        [Test]
        public void SessionSnapshot_AppliesBeforeFirstDeclaration_AndClassifiesConfiguredClashSkills()
        {
            Fixture fixture = CreateFixture();
            CombatHandValidationOptIn optIn = _owner.AddComponent<CombatHandValidationOptIn>();
            Set(optIn, "normalHandCapacity", 2);
            Set(optIn, "clashHandCapacity", 3);
            Set(optIn, "seed", 79);
            Set(optIn, "clashSkillDefinitions", new[] { fixture.AllyClashDefinition });

            Assert.That(optIn.TryApplyToSession(fixture.Session), Is.True);
            Assert.That(optIn.WasApplied, Is.True);
            Assert.That(fixture.Session.TryGetHand(fixture.Ally, out CombatHandRuntime hand), Is.True);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(2));
            Assert.That(Count(hand.Hand, CombatCardKind.Normal), Is.EqualTo(1));
            Assert.That(Count(hand.Hand, CombatCardKind.Clash), Is.EqualTo(1));
            Assert.That(new HashSet<long> { hand.Hand[0].RuntimeId, hand.Hand[1].RuntimeId },
                Is.EquivalentTo(new[] { 1L, 2L }));
        }

        [Test]
        public void SessionSnapshot_RejectsLateApplication_WithoutCreatingCards()
        {
            Fixture fixture = CreateFixture();
            CombatHandValidationOptIn optIn = _owner.AddComponent<CombatHandValidationOptIn>();
            Assert.That(fixture.StateMachine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, fixture.AllyNormal),
                fixture.Session.ExchangeState.Version), Is.True);

            Assert.That(optIn.TryApplyToSession(fixture.Session), Is.False);
            Assert.That(optIn.WasApplied, Is.False);
            Assert.That(fixture.Session.TryGetHand(fixture.Ally, out _), Is.False);
            Assert.That(optIn.LastStatus, Does.Contain("before the first declaration"));
        }

        [Test]
        public void ClashValidationEvent_AppliesHandBeforeTheFirstDeclaration()
        {
            Fixture fixture = CreateFixture();
            CombatEntryPoint entryPoint = _owner.AddComponent<CombatEntryPoint>();
            CombatFlowOrchestrator flow = _owner.AddComponent<CombatFlowOrchestrator>();
            SetAutoProperty(entryPoint, nameof(CombatEntryPoint.ActiveSession), fixture.Session);
            SetAutoProperty(entryPoint, nameof(CombatEntryPoint.ActiveStateMachine), fixture.StateMachine);
            Assert.That(flow.BindFinalExchange(fixture.Session, fixture.StateMachine,
                new ZeroRandom()), Is.True);

            CombatHandValidationOptIn optIn = _owner.AddComponent<CombatHandValidationOptIn>();
            Set(optIn, "enableValidation", true);
            Set(optIn, "clashSkillDefinitions", new[] { fixture.AllyClashDefinition });
            Invoke(optIn, "OnEnable");

            Assert.That(flow.TryEnableClashSeriesValidation(
                new CombatClashSeriesConfig(3, 3, 3), 5f,
                _ => fixture.AllyNormal, _ => fixture.AllyNormal), Is.True);
            Assert.That(optIn.WasApplied, Is.True);
            Assert.That(fixture.Session.ExchangeState.DeclarationId, Is.Zero);
            Assert.That(fixture.Session.TryGetHand(fixture.Ally, out CombatHandRuntime hand), Is.True);
            Assert.That(hand.DeckSnapshot, Has.Count.EqualTo(2));
        }

        [Test]
        public void EnemyInitiative_DefersItsFirstDeclarationUntilClashHandAndBpOptInsFinish()
        {
            Fixture fixture = CreateFixture(StartReason.PlayerGotHit, Side.Enemies);
            CombatEntryPoint entryPoint = _owner.AddComponent<CombatEntryPoint>();
            CombatFlowOrchestrator flow = _owner.AddComponent<CombatFlowOrchestrator>();
            SetAutoProperty(entryPoint, nameof(CombatEntryPoint.ActiveSession), fixture.Session);
            SetAutoProperty(entryPoint, nameof(CombatEntryPoint.ActiveStateMachine), fixture.StateMachine);
            Assert.That(flow.BindFinalExchange(fixture.Session, fixture.StateMachine,
                new ZeroRandom()), Is.True);
            Assert.That(fixture.Session.ExchangeState.DeclarationId, Is.Zero);
            Assert.That(flow.PendingDecisionRequest?.ActingSide, Is.EqualTo(Side.Enemies));

            FinalCombatEnemyCommandController enemy = new FinalCombatEnemyCommandController();
            Assert.That(enemy.BindForStartup(flow, fixture.Session), Is.True);

            CombatHandValidationOptIn handOptIn = _owner.AddComponent<CombatHandValidationOptIn>();
            Set(handOptIn, "enableValidation", true);
            Set(handOptIn, "clashSkillDefinitions", new[] { fixture.AllyClashDefinition });
            Invoke(handOptIn, "OnEnable");

            CombatBpValidationOptIn bpOptIn = _owner.AddComponent<CombatBpValidationOptIn>();
            Set(bpOptIn, "enableValidation", true);
            Invoke(bpOptIn, "OnEnable");

            Assert.That(flow.TryEnableClashSeriesValidation(
                new CombatClashSeriesConfig(3, 3, 3), 5f,
                _ => fixture.AllyNormal, _ => fixture.AllyNormal), Is.True);
            Assert.That(fixture.Session.ExchangeState.DeclarationId, Is.Zero);
            Assert.That(handOptIn.WasApplied, Is.True);
            Assert.That(bpOptIn.WasApplied, Is.True);
            Assert.That(fixture.Session.TryGetHand(fixture.Ally, out _), Is.True);
            Assert.That(fixture.Session.BpRuntime, Is.Not.Null);

            enemy.ActivateStartupRequests();
            Assert.That(fixture.Session.ExchangeState.DeclarationId, Is.EqualTo(1));
            Assert.That(fixture.Session.ExchangeState.CurrentDeclaration?.DeclaringSide,
                Is.EqualTo(Side.Enemies));
        }

        [Test]
        public void HandStatus_ReportsOptInStateAndActualRuntimeCardGroups()
        {
            Fixture fixture = CreateFixture();

            Assert.That(BuildHandStatus(fixture.Session, fixture.Ally),
                Is.EqualTo("손패 시스템 비활성"));

            Assert.That(fixture.Session.TryEnableHandValidation(fixture.Ally, new[]
            {
                new CombatHandEntry(fixture.AllyNormal, CombatCardKind.Normal)
            }, 2, 3, 79), Is.True);

            string status = BuildHandStatus(fixture.Session, fixture.Ally);
            StringAssert.Contains("[일반] 손패 1장", status);
            StringAssert.Contains("Draw", status);
            StringAssert.Contains("Discard", status);
            StringAssert.Contains("[합] 손패 0장", status);
            StringAssert.Contains("Refill 0", status);
            StringAssert.Contains("합 카드 없음", status);
        }

        private Fixture CreateFixture(
            StartReason reason = StartReason.PlayerFirstHit,
            Side initiative = Side.Allies)
        {
            _owner = new GameObject("CombatHandValidationOptInTests");
            SkillDefinitionSO normal = Definition(1, "normal");
            SkillDefinitionSO clash = Definition(2, "clash");
            SkillDefinitionSO enemy = Definition(3, "enemy");
            TestCombatant ally = new(1, Side.Allies);
            TestCombatant opponent = new(2, Side.Enemies);
            SoSkill allyNormal = new(normal);
            SoSkill allyClash = new(clash);
            ally.AddSkill(allyNormal);
            ally.AddSkill(allyClash);
            opponent.AddSkill(new SoSkill(enemy));

            CombatSession session = new(
                reason, initiative, new InspirationPool(10),
                new CombatEnvironment(), CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 5, 3, 0));
            session.Allies.Add(ally);
            session.Enemies.Add(opponent);
            CombatStateMachine stateMachine = new(session);
            stateMachine.Tick();
            return new Fixture(session, stateMachine, ally, opponent, allyNormal, clash);
        }

        private SkillDefinitionSO Definition(int id, string name)
        {
            SkillDefinitionSO result = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            result.skillId = id;
            result.displayName = name;
            result.tag = SkillTag.Attack;
            result.targeting = TargetingRule.SingleEnemy;
            result.baseDamage = 1;
            result.speed = 1;
            _definitions.Add(result);
            return result;
        }

        private static int Count(IReadOnlyList<CombatHandCard> cards, CombatCardKind kind)
        {
            int count = 0;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].Kind == kind) count++;
            return count;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            Set(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static string BuildHandStatus(CombatSession session, ICombatant player)
        {
            MethodInfo method = typeof(FinalCombatUIBinder).GetMethod("BuildHandStatus",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { session, player });
        }

        private sealed class Fixture
        {
            public CombatSession Session { get; }
            public CombatStateMachine StateMachine { get; }
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public SoSkill AllyNormal { get; }
            public SkillDefinitionSO AllyClashDefinition { get; }

            public Fixture(CombatSession session, CombatStateMachine stateMachine, TestCombatant ally,
                TestCombatant enemy, SoSkill allyNormal, SkillDefinitionSO allyClashDefinition)
            {
                Session = session;
                StateMachine = stateMachine;
                Ally = ally;
                Enemy = enemy;
                AllyNormal = allyNormal;
                AllyClashDefinition = allyClashDefinition;
            }
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => minInclusive;
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new();
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
