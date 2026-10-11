#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Combat
{
    public sealed class CombatStatusFinalExchangeTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

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

        [Test]
        public void DamageModifier_AppliesSignedAndStackedValuesWithoutNegativeDamage()
        {
            Fixture fixture = CreateFixture();
            CombatStatusDefinitionSO power = CreateStatus(
                "power", CombatStatusStackPolicy.Stack, 3, damageModifier: 2);
            fixture.AllyState.ApplyStatus(power);
            fixture.AllyState.ApplyStatus(power);

            ExecuteDirect(fixture, fixture.Ally, fixture.Enemy, new TestSkill(1, 3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(3)); // 3 + (2 * 2)

            CombatStatusDefinitionSO weakness = CreateStatus(
                "weakness", CombatStatusStackPolicy.Replace, 1, damageModifier: -20);
            fixture.AllyState.ApplyStatus(weakness);
            int hpBefore = fixture.Enemy.HP;
            ExecuteDirect(fixture, fixture.Ally, fixture.Enemy, new TestSkill(2, 3));
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpBefore));
        }

        [Test]
        public void ClashModifier_ChangesAuthoredInputAndFinalRuleOutcome()
        {
            Fixture fixture = CreateFixture();
            TestSkill allySkill = new TestSkill(1, 0, clashPower: 1);
            TestSkill enemySkill = new TestSkill(2, 0, clashPower: 2);
            CombatStatusDefinitionSO focus = CreateStatus(
                "focus", CombatStatusStackPolicy.Replace, 1, clashModifier: 2);
            fixture.AllyState.ApplyStatus(focus);

            AuthoredCombatClashSideInputProvider provider =
                new AuthoredCombatClashSideInputProvider(fixture.Session);
            Assert.That(provider.TryCreate(fixture.Ally, allySkill, true, out CombatClashSideInput boosted), Is.True);
            Assert.That(provider.TryCreate(fixture.Enemy, enemySkill, false, out CombatClashSideInput enemy), Is.True);
            Assert.That(boosted.ClashModifier, Is.EqualTo(2));

            FinalCombatClashRule boostedRule = new FinalCombatClashRule(boosted, enemy, new ZeroRandom());
            Assert.That(boostedRule.Resolve(CreateClashRequest(fixture, allySkill, enemySkill)),
                Is.EqualTo(CombatClashOutcome.AttackerWin));

            fixture.AllyState.RemoveStatus("focus");
            Assert.That(provider.TryCreate(fixture.Ally, allySkill, true, out CombatClashSideInput normal), Is.True);
            FinalCombatClashRule normalRule = new FinalCombatClashRule(normal, enemy, new ZeroRandom());
            Assert.That(normalRule.Resolve(CreateClashRequest(fixture, allySkill, enemySkill)),
                Is.EqualTo(CombatClashOutcome.ResponderWin));
        }

        [Test]
        public void PostureModifier_UsesFinalPostureRuleAndPreservesStunThreshold()
        {
            Fixture fixture = CreateFixture(maxPosture: 3);
            TestSkill allySkill = new TestSkill(1, 0);
            TestSkill enemySkill = new TestSkill(2, 0);
            fixture.Ally.Add(allySkill);
            fixture.Enemy.Add(enemySkill);
            fixture.AllyState.ApplyStatus(CreateStatus(
                "break", CombatStatusStackPolicy.Replace, 1, postureModifier: 2));

            BeginCounteredExchange(fixture, allySkill, enemySkill);
            FinalCombatClashRule rule = new FinalCombatClashRule(
                new CombatClashSideInput(fixture.Ally.Id, allySkill.Id, 3, 1, true),
                new CombatClashSideInput(fixture.Enemy.Id, enemySkill.Id, 1, 1, false),
                new ZeroRandom());
            Assert.That(fixture.Machine.TryResolveClash(rule, fixture.Session.ExchangeState.Version), Is.True);
            PrepareExecution(fixture);
            Assert.That(fixture.Machine.TryResolvePosture(new FinalCombatPostureRule()), Is.True);
            Assert.That(fixture.EnemyState.CurrentPosture, Is.EqualTo(3));
            Assert.That(fixture.Machine.TryResolveStun(), Is.True);
            Assert.That(fixture.Enemy.IsStunned, Is.True);
        }

        [Test]
        public void RefreshAndReplace_PreserveExistingStackPolicyAndSnapshotContract()
        {
            Fixture fixture = CreateFixture();
            CombatStatusDefinitionSO refresh = CreateStatus(
                "timer", CombatStatusStackPolicy.Refresh, 3, duration: 3, damageModifier: 1);
            CombatStatusDefinitionSO replace = CreateStatus(
                "timer", CombatStatusStackPolicy.Replace, 3, duration: 2, damageModifier: 4);

            fixture.AllyState.ApplyStatus(refresh);
            fixture.AllyState.ApplyStatus(refresh);
            fixture.AllyState.ApplyStatus(replace);

            Assert.That(fixture.AllyState.TryGetStatus("timer", out CombatStatusRuntime runtime), Is.True);
            Assert.That(runtime.StackCount, Is.EqualTo(1));
            Assert.That(runtime.RemainingDuration, Is.EqualTo(2));
            CombatStatusSnapshot snapshot = fixture.AllyState.StatusSnapshots[0];
            Assert.That(snapshot.StatusId, Is.EqualTo("timer"));
            Assert.That(snapshot.DisplayName, Is.EqualTo("timer"));
            Assert.That(snapshot.Polarity, Is.EqualTo(CombatStatusPolarity.Buff));
            Assert.That(snapshot.StackCount, Is.EqualTo(1));
            Assert.That(snapshot.RemainingDuration, Is.EqualTo(2));
            Assert.That(snapshot.IsExpired, Is.False);
        }

        [Test]
        public void CompletedExchange_DecrementsEachParticipantOnceAndRejectsStaleDuplicateCallbacks()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = new TestSkill(1, 0);
            fixture.Ally.Add(skill);
            CombatStatusDefinitionSO status = CreateStatus(
                "timer", CombatStatusStackPolicy.Replace, 1, duration: 2);
            fixture.AllyState.ApplyStatus(status);
            fixture.EnemyState.ApplyStatus(status);

            CompleteUnopposedExchange(fixture, skill);
            Assert.That(fixture.AllyState.TryGetStatus("timer", out CombatStatusRuntime allyTimer), Is.True);
            Assert.That(fixture.EnemyState.TryGetStatus("timer", out CombatStatusRuntime enemyTimer), Is.True);
            Assert.That(allyTimer.RemainingDuration, Is.EqualTo(1));
            Assert.That(enemyTimer.RemainingDuration, Is.EqualTo(1));

            int finalizedVersion = fixture.Session.ExchangeState.Version;
            Assert.That(fixture.Machine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), finalizedVersion), Is.False);
            Assert.That(allyTimer.RemainingDuration, Is.EqualTo(1));
            Assert.That(enemyTimer.RemainingDuration, Is.EqualTo(1));
        }

        [Test]
        public void IncompleteExchange_DoesNotAdvanceDuration()
        {
            Fixture fixture = CreateFixture();
            fixture.AllyState.ApplyStatus(CreateStatus(
                "guard", CombatStatusStackPolicy.Replace, 1, duration: 2));

            Assert.That(fixture.Machine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Session.ExchangeState.Version), Is.False);
            Assert.That(fixture.AllyState.TryGetStatus("guard", out CombatStatusRuntime active), Is.True);
            Assert.That(active.RemainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void ExplicitZeroDuration_PreservesPriorNonExpiringRuntimeBehavior()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = new TestSkill(1, 0);
            fixture.Ally.Add(skill);
            fixture.AllyState.ApplyStatus(CreateStatus(
                "persistent", CombatStatusStackPolicy.Replace, 1, duration: 0));

            CompleteUnopposedExchange(fixture, skill);
            Assert.That(fixture.AllyState.TryGetStatus("persistent", out CombatStatusRuntime active), Is.True);
            Assert.That(active.RemainingDuration, Is.Zero);
            Assert.That(active.IsExpired, Is.False);
        }

        [Test]
        public void StatusAppliedDuringExchange_IsActiveButDoesNotExpireUntilNextCompletedChainExchange()
        {
            Fixture fixture = CreateFixture();
            CombatStatusDefinitionSO fresh = CreateStatus(
                "fresh", CombatStatusStackPolicy.Replace, 1, duration: 1, damageModifier: -1);
            TestSkill skill = new TestSkill(1, 1, appliedStatuses: new[] { fresh });
            fixture.Ally.Add(skill);

            CompleteUnopposedExchange(fixture, skill);
            Assert.That(fixture.EnemyState.TryGetStatus("fresh", out CombatStatusRuntime first), Is.True);
            Assert.That(first.RemainingDuration, Is.EqualTo(1));

            Assert.That(fixture.Machine.TryContinueChain(skill, fixture.Session.ExchangeState.Version), Is.True);
            CompleteUnopposedExchange(fixture, skill);
            Assert.That(fixture.EnemyState.HasStatus("fresh"), Is.False);
        }

        [Test]
        public void ExpiredStatus_NoLongerChangesLaterDamageAndDoesNotLeakToNewSession()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = new TestSkill(1, 2);
            fixture.Ally.Add(skill);
            fixture.AllyState.ApplyStatus(CreateStatus(
                "fading", CombatStatusStackPolicy.Replace, 1, duration: 1, damageModifier: 4));

            CompleteUnopposedExchange(fixture, skill);
            Assert.That(fixture.AllyState.HasStatus("fading"), Is.False);
            int hpBefore = fixture.Enemy.HP;
            ExecuteDirect(fixture, fixture.Ally, fixture.Enemy, skill);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpBefore - 2));

            Fixture second = CreateFixture();
            Assert.That(second.AllyState.HasStatus("fading"), Is.False);
        }

        [Test]
        public void NoStatus_PreservesDamageClashAndPostureDefaults()
        {
            Fixture fixture = CreateFixture();
            TestSkill skill = new TestSkill(1, 3, clashPower: 2);
            ExecuteDirect(fixture, fixture.Ally, fixture.Enemy, skill);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(7));

            AuthoredCombatClashSideInputProvider provider =
                new AuthoredCombatClashSideInputProvider(fixture.Session);
            Assert.That(provider.TryCreate(fixture.Ally, skill, true, out CombatClashSideInput input), Is.True);
            Assert.That(input.ClashModifier, Is.Zero);
            Assert.That(new FinalCombatPostureRule().ResolvePostureDelta(CreatePostureRequest(fixture, skill)),
                Is.EqualTo(FinalCombatPostureRule.BasePostureDamage));
        }

        [Test]
        public void LegacyPlanning_DoesNotConsumeFinalExchangeDamageModifier()
        {
            Fixture fixture = CreateFixture(CombatFlowMode.LegacyPlanning);
            TestSkill skill = new TestSkill(1, 3);
            fixture.Ally.Add(skill);
            fixture.AllyState.ApplyStatus(CreateStatus(
                "final-only", CombatStatusStackPolicy.Replace, 1, damageModifier: 5));

            ExecuteDirect(fixture, fixture.Ally, fixture.Enemy, skill);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(7));
        }

        private void CompleteUnopposedExchange(Fixture fixture, TestSkill skill)
        {
            Assert.That(fixture.Machine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, skill),
                fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.ConfirmNoResponse(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.TryCommitExchange(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.TryBeginApproach(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.CompleteApproach(fixture.Session.ExchangeState.Version), Is.True);
            PrepareExecution(fixture);
            Assert.That(fixture.Machine.TryResolvePosture(null), Is.True);
            Assert.That(fixture.Machine.TryResolveStun(), Is.True);
            Assert.That(fixture.Machine.TryPrepareAftermath(), Is.True);
            Assert.That(fixture.Machine.TryPrepareAftermathDecision(), Is.True);
            Assert.That(fixture.Machine.TryFinalizeApplyOutcome(
                new FinalCombatTerminalPolicy(), fixture.Session.ExchangeState.Version), Is.True);
        }

        private static void PrepareExecution(Fixture fixture)
        {
            Assert.That(fixture.Machine.TryPrepareOutcome(), Is.True);
            Assert.That(fixture.Machine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Machine.TryExecutePreparedSkill(), Is.True);
        }

        private static void BeginCounteredExchange(Fixture fixture, TestSkill allySkill, TestSkill enemySkill)
        {
            Assert.That(fixture.Machine.TryDeclareAttack(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, allySkill),
                fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.TryDeclareResponse(
                new CombatResponseDeclaration(fixture.Enemy, enemySkill),
                fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.TryCommitExchange(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.TryBeginApproach(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Machine.CompleteApproach(fixture.Session.ExchangeState.Version), Is.True);
        }

        private static void ExecuteDirect(Fixture fixture, ICombatant actor, ICombatant target, TestSkill skill)
        {
            if (actor is TestCombatant testCombatant)
                testCombatant.Add(skill);

            Assert.That(SkillRunner.TryExecute(
                fixture.Session,
                new CombatSkillExecutionRequest(actor, skill, new[] { target }, target, CombatClashOutcome.AttackerWin),
                out _), Is.True);
        }

        private static CombatClashRequest CreateClashRequest(
            Fixture fixture,
            TestSkill attackerSkill,
            TestSkill responderSkill)
        {
            return new CombatClashRequest(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, attackerSkill),
                CombatResponseState.CounterDeclared,
                new CombatResponseDeclaration(fixture.Enemy, responderSkill));
        }

        private static CombatPostureRequest CreatePostureRequest(Fixture fixture, TestSkill skill)
        {
            return new CombatPostureRequest(
                fixture.Ally,
                fixture.Enemy,
                skill,
                CombatClashOutcome.AttackerWin,
                null,
                0,
                fixture.EnemyState.MaxPosture);
        }

        private Fixture CreateFixture(
            CombatFlowMode flowMode = CombatFlowMode.StandoffClashChain,
            int maxPosture = 3)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                flowMode,
                new CombatRuntimeConfig(10, 10, maxPosture, 0));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine machine = new CombatStateMachine(session);
            machine.Tick();
            return new Fixture(session, machine, ally, enemy);
        }

        private CombatStatusDefinitionSO CreateStatus(
            string id,
            CombatStatusStackPolicy policy,
            int maximumStacks,
            int duration = 0,
            int damageModifier = 0,
            int clashModifier = 0,
            int postureModifier = 0)
        {
            CombatStatusDefinitionSO status = ScriptableObject.CreateInstance<CombatStatusDefinitionSO>();
            _createdObjects.Add(status);
            SetPrivate(status, "statusId", id);
            SetPrivate(status, "displayName", id);
            SetPrivate(status, "stackPolicy", policy);
            SetPrivate(status, "maximumStacks", maximumStacks);
            SetPrivate(status, "durationKind", CombatStatusDurationKind.Explicit);
            SetPrivate(status, "explicitDuration", duration);
            SetPrivate(status, "damageModifier", damageModifier);
            SetPrivate(status, "clashModifier", clashModifier);
            SetPrivate(status, "postureModifier", postureModifier);
            return status;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class Fixture
        {
            public Fixture(CombatSession session, CombatStateMachine machine, TestCombatant ally, TestCombatant enemy)
            {
                Session = session;
                Machine = machine;
                Ally = ally;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public CombatStateMachine Machine { get; }
            public TestCombatant Ally { get; }
            public TestCombatant Enemy { get; }
            public CombatantCombatState AllyState => Session.GetCombatState(Ally);
            public CombatantCombatState EnemyState => Session.GetCombatState(Enemy);
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minInclusive, int maxInclusive) => 0;
        }

        private sealed class TestSkill : ISkill, ICombatMpCostProvider, IFinalCombatSkillStats, ICombatStatusEffectProvider
        {
            private readonly CombatStatusDefinitionSO[] _appliedStatuses;

            public TestSkill(
                int id,
                int baseDamage,
                int clashPower = 1,
                CombatStatusDefinitionSO[] appliedStatuses = null)
            {
                Id = new SkillId(id);
                BaseDamage = baseDamage;
                ClashPower = clashPower;
                _appliedStatuses = appliedStatuses ?? Array.Empty<CombatStatusDefinitionSO>();
            }

            public SkillId Id { get; }
            public string Name => Id.Value.ToString();
            public int InspirationCost => 0;
            public int MpCost => 0;
            public int FinalMpCost => 0;
            public int ClashPower { get; }
            public IReadOnlyList<CombatStatusDefinitionSO> AppliedStatusEffects => _appliedStatuses;
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
        }

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;
            private int _hp = 10;

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

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
