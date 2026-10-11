#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Verifies the isolated TS_ authoring fixtures without modifying production combat content.
    /// </summary>
    public sealed class CombatStatusValidationAssetTests
    {
        private const string FixtureRoot =
            "Assets/GAME/Tests/Editor/Fixtures/CombatStatusValidation/";

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void StatusDefinitions_DeserializeWithStableIdentityAndIntendedModifiers()
        {
            CombatStatusDefinitionSO damageBoost = LoadStatus("TS_Status_DamageBoost.asset");
            CombatStatusDefinitionSO damageWeaken = LoadStatus("TS_Status_DamageWeaken.asset");
            CombatStatusDefinitionSO clashFocus = LoadStatus("TS_Status_ClashFocus.asset");
            CombatStatusDefinitionSO postureFracture = LoadStatus("TS_Status_PostureFracture.asset");

            AssertStatus(damageBoost, "test.status.damage_boost", "Test Damage Boost",
                CombatStatusPolarity.Buff, CombatStatusStackPolicy.Stack, 2, 2, 2, 0, 0);
            AssertStatus(damageWeaken, "test.status.damage_weaken", "Test Damage Weaken",
                CombatStatusPolarity.Debuff, CombatStatusStackPolicy.Replace, 1, 2, -2, 0, 0);
            AssertStatus(clashFocus, "test.status.clash_focus", "Test Clash Focus",
                CombatStatusPolarity.Buff, CombatStatusStackPolicy.Refresh, 1, 2, 0, 2, 0);
            AssertStatus(postureFracture, "test.status.posture_fracture", "Test Posture Fracture",
                CombatStatusPolarity.Debuff, CombatStatusStackPolicy.Stack, 2, 2, 0, 0, 1);
        }

        [Test]
        public void StatusApplicatorSkill_SerializesReferencesToEveryValidationDefinition()
        {
            SkillDefinitionSO applicator = LoadSkill("TS_Skill_ApplyStatuses.asset");

            Assert.That(applicator.skillId, Is.EqualTo(920001));
            Assert.That(applicator.AppliedStatusEffects.Select(status => status.StatusId), Is.EqualTo(new[]
            {
                "test.status.damage_boost",
                "test.status.damage_weaken",
                "test.status.clash_focus",
                "test.status.posture_fracture"
            }));
        }

        [Test]
        public void StatusApplicatorSkill_UsesExistingSkillRunnerAndStackRefreshReplacePolicies()
        {
            Fixture fixture = CreateFixture();
            SoSkill applicator = new SoSkill(LoadSkill("TS_Skill_ApplyStatuses.asset"));
            fixture.Ally.Add(applicator);

            Assert.That(Execute(fixture, fixture.Ally, fixture.Enemy, applicator).AppliedStatusResults,
                Has.Count.EqualTo(4));
            Assert.That(fixture.EnemyState.StatusSnapshots, Has.Count.EqualTo(4));

            AdvanceDurations(fixture.EnemyState);
            Assert.That(fixture.EnemyState.StatusSnapshots.All(snapshot => snapshot.RemainingDuration == 1), Is.True);

            CombatSkillExecutionResult refreshed = Execute(fixture, fixture.Ally, fixture.Enemy, applicator);
            Assert.That(refreshed.AppliedStatusResults, Has.Count.EqualTo(4));
            Assert.That(GetStatus(fixture.EnemyState, "test.status.damage_boost").StackCount, Is.EqualTo(2));
            Assert.That(GetStatus(fixture.EnemyState, "test.status.posture_fracture").StackCount, Is.EqualTo(2));
            Assert.That(GetStatus(fixture.EnemyState, "test.status.damage_weaken").RemainingDuration, Is.EqualTo(2));
            Assert.That(GetStatus(fixture.EnemyState, "test.status.clash_focus").RemainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void ValidationDefinitions_ModifyFinalExchangeDamageClashAndPosture()
        {
            Fixture fixture = CreateFixture(maxPosture: 3);
            SoSkill finalAttack = new SoSkill(LoadSkill("TS_Skill_FinalAttack.asset"));
            fixture.Ally.Add(finalAttack);
            fixture.Enemy.Add(finalAttack);

            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_DamageBoost.asset"));
            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_DamageBoost.asset"));
            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_DamageWeaken.asset"));
            int hpBefore = fixture.Enemy.HP;
            Execute(fixture, fixture.Ally, fixture.Enemy, finalAttack);
            Assert.That(fixture.Enemy.HP, Is.EqualTo(hpBefore - 5));

            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_ClashFocus.asset"));
            AuthoredCombatClashSideInputProvider provider =
                new AuthoredCombatClashSideInputProvider(fixture.Session);
            Assert.That(provider.TryCreate(fixture.Ally, finalAttack, true, out CombatClashSideInput attacker), Is.True);
            Assert.That(provider.TryCreate(fixture.Enemy, finalAttack, false, out CombatClashSideInput responder), Is.True);
            Assert.That(attacker.ClashModifier, Is.EqualTo(2));
            Assert.That(new FinalCombatClashRule(attacker, responder, new ZeroRandom()).Resolve(
                new CombatClashRequest(
                    new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, finalAttack),
                    CombatResponseState.CounterDeclared,
                    new CombatResponseDeclaration(fixture.Enemy, finalAttack))),
                Is.EqualTo(CombatClashOutcome.AttackerWin));

            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_PostureFracture.asset"));
            fixture.AllyState.ApplyStatus(LoadStatus("TS_Status_PostureFracture.asset"));
            BeginCounteredExchange(fixture, finalAttack, finalAttack);
            FinalCombatClashRule postureClash = new FinalCombatClashRule(
                new CombatClashSideInput(fixture.Ally.Id, finalAttack.Id, 3, 1, true),
                new CombatClashSideInput(fixture.Enemy.Id, finalAttack.Id, 1, 1, false),
                new ZeroRandom());
            Assert.That(fixture.Machine.TryResolveClash(postureClash, fixture.Session.ExchangeState.Version), Is.True);
            PrepareExecution(fixture);
            Assert.That(fixture.Machine.TryResolvePosture(new FinalCombatPostureRule()), Is.True);
            Assert.That(fixture.EnemyState.CurrentPosture, Is.EqualTo(3));
        }

        [Test]
        public void ValidationDefinitions_ExpireThroughCompletedExchangesAndPresenterRemovesSnapshots()
        {
            Fixture fixture = CreateFixture();
            SoSkill applicator = new SoSkill(LoadSkill("TS_Skill_ApplyStatuses.asset"));
            SoSkill finalAttack = new SoSkill(LoadSkill("TS_Skill_FinalAttack.asset"));
            fixture.Ally.Add(applicator);
            fixture.Ally.Add(finalAttack);

            CompleteUnopposedExchange(fixture, applicator);
            Assert.That(GetStatus(fixture.EnemyState, "test.status.damage_boost").RemainingDuration, Is.EqualTo(2));

            GameObject root = Track(new GameObject("StatusValidationRoot"));
            Text text = Track(new GameObject("StatusValidationText")).AddComponent<Text>();
            text.transform.SetParent(root.transform);
            FinalCombatStatusListPresenter presenter = root.AddComponent<FinalCombatStatusListPresenter>();
            SetPrivateField(presenter, "statusRoot", root);
            SetPrivateField(presenter, "statusText", text);
            presenter.Render(fixture.Session);
            StringAssert.Contains("Test Damage Boost [test.status.damage_boost] x1 | EX 2", text.text);

            Assert.That(fixture.Machine.TryContinueChain(finalAttack, fixture.Session.ExchangeState.Version), Is.True);
            CompleteUnopposedExchange(fixture, finalAttack);
            Assert.That(GetStatus(fixture.EnemyState, "test.status.damage_boost").RemainingDuration, Is.EqualTo(1));

            Assert.That(fixture.Machine.TryContinueChain(finalAttack, fixture.Session.ExchangeState.Version), Is.True);
            CompleteUnopposedExchange(fixture, finalAttack);
            Assert.That(fixture.EnemyState.StatusSnapshots, Is.Empty);
            presenter.Render(fixture.Session);
            Assert.That(text.text, Is.Empty);
            Assert.That(root.activeSelf, Is.False);
        }

        [Test]
        public void MissingStatusReference_IsIgnoredWithoutPartialRuntimeState()
        {
            Fixture fixture = CreateFixture();
            SkillDefinitionSO missingReferenceDefinition = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            missingReferenceDefinition.skillId = 920003;
            missingReferenceDefinition.displayName = "Test Missing Status Reference";
            SetPrivateField(missingReferenceDefinition, "appliedStatusEffects", new CombatStatusDefinitionSO[] { null });
            SoSkill missingReferenceSkill = new SoSkill(missingReferenceDefinition);
            fixture.Ally.Add(missingReferenceSkill);

            CombatSkillExecutionResult result = Execute(
                fixture,
                fixture.Ally,
                fixture.Enemy,
                missingReferenceSkill);

            Assert.That(result.AppliedStatusResults, Is.Empty);
            Assert.That(fixture.EnemyState.StatusSnapshots, Is.Empty);
        }

        private static void AssertStatus(
            CombatStatusDefinitionSO status,
            string statusId,
            string displayName,
            CombatStatusPolarity polarity,
            CombatStatusStackPolicy stackPolicy,
            int maximumStacks,
            int duration,
            int damageModifier,
            int clashModifier,
            int postureModifier)
        {
            Assert.That(status.StatusId, Is.EqualTo(statusId));
            Assert.That(status.DisplayName, Is.EqualTo(displayName));
            Assert.That(status.Polarity, Is.EqualTo(polarity));
            Assert.That(status.StackPolicy, Is.EqualTo(stackPolicy));
            Assert.That(status.MaximumStacks, Is.EqualTo(maximumStacks));
            Assert.That(status.DurationKind, Is.EqualTo(CombatStatusDurationKind.Explicit));
            Assert.That(status.ExplicitDuration, Is.EqualTo(duration));
            Assert.That(status.DamageModifier, Is.EqualTo(damageModifier));
            Assert.That(status.ClashModifier, Is.EqualTo(clashModifier));
            Assert.That(status.PostureModifier, Is.EqualTo(postureModifier));
        }

        private Fixture CreateFixture(int maxPosture = 6)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies);
            CombatSession session = new CombatSession(
                StartReason.PlayerFirstHit,
                Side.Allies,
                new InspirationPool(10),
                new CombatEnvironment(),
                CombatFlowMode.StandoffClashChain,
                new CombatRuntimeConfig(10, 10, maxPosture, 0));
            session.Allies.Add(ally);
            session.Enemies.Add(enemy);
            CombatStateMachine machine = new CombatStateMachine(session);
            machine.Tick();
            return new Fixture(session, machine, ally, enemy);
        }

        private static CombatSkillExecutionResult Execute(
            Fixture fixture,
            ICombatant actor,
            ICombatant target,
            ISkill skill)
        {
            Assert.That(SkillRunner.TryExecute(
                fixture.Session,
                new CombatSkillExecutionRequest(actor, skill, new[] { target }, target, CombatClashOutcome.AttackerWin),
                out CombatSkillExecutionResult result), Is.True);
            return result;
        }

        private static void BeginCounteredExchange(Fixture fixture, ISkill allySkill, ISkill enemySkill)
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

        private static void CompleteUnopposedExchange(Fixture fixture, ISkill skill)
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

        private static CombatStatusRuntime GetStatus(CombatantCombatState state, string id)
        {
            Assert.That(state.TryGetStatus(id, out CombatStatusRuntime status), Is.True, id);
            return status;
        }

        private static void AdvanceDurations(CombatantCombatState state)
        {
            MethodInfo method = typeof(CombatantCombatState).GetMethod(
                "AdvanceStatusDurations",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(state, new object[] { null });
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static CombatStatusDefinitionSO LoadStatus(string fileName)
        {
            return Load<CombatStatusDefinitionSO>(fileName);
        }

        private static SkillDefinitionSO LoadSkill(string fileName)
        {
            return Load<SkillDefinitionSO>(fileName);
        }

        private static T Load<T>(string fileName) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, FixtureRoot + fileName);
            return asset;
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

        private sealed class TestCombatant : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new List<ISkill>();
            private CombatantCombatState _state;
            private int _hp = 20;

            public TestCombatant(int id, Side side)
            {
                Id = new CombatantId(id);
                Side = side;
            }

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? _hp;
            public int MaxHP => 20;
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
