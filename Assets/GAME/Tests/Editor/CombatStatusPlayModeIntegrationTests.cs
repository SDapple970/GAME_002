#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    /// <summary>
    /// Editor Play Mode coverage using only transient scene objects and CBT-STATUS test assets.
    /// </summary>
    public sealed class CombatStatusPlayModeIntegrationTests
    {
        private const string FixtureRoot =
            "Assets/GAME/Tests/Editor/Fixtures/CombatStatusValidation/";

        private readonly List<string> _runtimeErrors = new List<string>();

        [UnityTest]
        [Category("CBT_STATUS_02C")]
        public IEnumerator IsolatedFinalExchange_StatusRuntimeAndBinderRunInActualPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);

            yield return RunScenario();

            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
        }

        private IEnumerator RunScenario()
        {
            _runtimeErrors.Clear();
            Application.logMessageReceived += CaptureRuntimeError;
            try
            {
                CombatStatusDefinitionSO damageBoost = Load<CombatStatusDefinitionSO>("TS_Status_DamageBoost.asset");
                CombatStatusDefinitionSO damageWeaken = Load<CombatStatusDefinitionSO>("TS_Status_DamageWeaken.asset");
                CombatStatusDefinitionSO clashFocus = Load<CombatStatusDefinitionSO>("TS_Status_ClashFocus.asset");
                CombatStatusDefinitionSO postureFracture = Load<CombatStatusDefinitionSO>("TS_Status_PostureFracture.asset");
                SoSkill applicator = new SoSkill(Load<SkillDefinitionSO>("TS_Skill_ApplyStatuses.asset"));
                SoSkill finalAttack = new SoSkill(Load<SkillDefinitionSO>("TS_Skill_FinalAttack.asset"));

                RuntimeFixture lifecycle = CreateFixture();
                lifecycle.Ally.Add(applicator);
                lifecycle.Ally.Add(finalAttack);
                BindWithSynchronousPresentation(lifecycle);
                lifecycle.Binder.Bind(lifecycle.Session, lifecycle.Machine, lifecycle.Orchestrator);

                SubmitUnopposedAttack(lifecycle, applicator);
                yield return null;

                Assert.That(lifecycle.EnemyState.StatusSnapshots, Has.Count.EqualTo(4));
                Assert.That(GetStatus(lifecycle.EnemyState, damageBoost.StatusId).RemainingDuration, Is.EqualTo(2));
                StringAssert.Contains("BUFF | Test Damage Boost [test.status.damage_boost] x1 | EX 2", lifecycle.StatusText.text);
                StringAssert.Contains("DEBUFF | Test Damage Weaken [test.status.damage_weaken] x1 | EX 2", lifecycle.StatusText.text);
                StringAssert.Contains("BUFF | Test Clash Focus [test.status.clash_focus] x1 | EX 2", lifecycle.StatusText.text);
                StringAssert.Contains("DEBUFF | Test Posture Fracture [test.status.posture_fracture] x1 | EX 2", lifecycle.StatusText.text);
                Assert.That(lifecycle.StatusRoot.activeSelf, Is.True);

                ContinueUnopposedChain(lifecycle, finalAttack);
                yield return null;
                Assert.That(GetStatus(lifecycle.EnemyState, damageBoost.StatusId).RemainingDuration, Is.EqualTo(1));
                int finalizedVersion = lifecycle.Session.ExchangeState.Version;
                Assert.That(lifecycle.Machine.TryFinalizeApplyOutcome(
                    new FinalCombatTerminalPolicy(), finalizedVersion), Is.False);
                Assert.That(GetStatus(lifecycle.EnemyState, damageBoost.StatusId).RemainingDuration, Is.EqualTo(1));

                ContinueUnopposedChain(lifecycle, finalAttack);
                yield return null;
                Assert.That(lifecycle.EnemyState.StatusSnapshots, Is.Empty);
                Assert.That(lifecycle.StatusText.text, Is.Empty);
                Assert.That(lifecycle.StatusRoot.activeSelf, Is.False);

                VerifyDamageClashPostureAndClashLoss(
                    damageBoost,
                    damageWeaken,
                    clashFocus,
                    postureFracture,
                    finalAttack);
                VerifyAllOutDoesNotAdvanceStatusDuration(damageBoost, finalAttack);
                VerifyVictoryProducesCombatResult(finalAttack);

                lifecycle.Binder.Unbind();
                Assert.That(lifecycle.StatusText.text, Is.Empty);
                Assert.That(lifecycle.StatusRoot.activeSelf, Is.False);

                RuntimeFixture freshSession = CreateFixture();
                BindWithSynchronousPresentation(freshSession);
                lifecycle.Binder.Bind(freshSession.Session, freshSession.Machine, freshSession.Orchestrator);
                yield return null;
                Assert.That(lifecycle.StatusText.text, Is.Empty);
                Assert.That(lifecycle.StatusRoot.activeSelf, Is.False);
                lifecycle.Binder.Unbind();

                Assert.That(_runtimeErrors, Is.Empty,
                    string.Join("\n", _runtimeErrors));
            }
            finally
            {
                Application.logMessageReceived -= CaptureRuntimeError;
            }
        }

        private static void VerifyDamageClashPostureAndClashLoss(
            CombatStatusDefinitionSO damageBoost,
            CombatStatusDefinitionSO damageWeaken,
            CombatStatusDefinitionSO clashFocus,
            CombatStatusDefinitionSO postureFracture,
            SoSkill finalAttack)
        {
            RuntimeFixture values = CreateFixture(maxPosture: 3);
            values.Ally.Add(finalAttack);
            values.Enemy.Add(finalAttack);
            values.AllyState.ApplyStatus(damageBoost);
            values.AllyState.ApplyStatus(damageBoost);
            values.AllyState.ApplyStatus(damageWeaken);
            int hpBefore = values.Enemy.HP;
            Assert.That(SkillRunner.TryExecute(
                values.Session,
                new CombatSkillExecutionRequest(
                    values.Ally,
                    finalAttack,
                    new[] { values.Enemy },
                    values.Enemy,
                    CombatClashOutcome.AttackerWin),
                out _), Is.True);
            Assert.That(values.Enemy.HP, Is.EqualTo(hpBefore - 5));

            values.AllyState.ApplyStatus(clashFocus);
            AuthoredCombatClashSideInputProvider provider =
                new AuthoredCombatClashSideInputProvider(values.Session);
            Assert.That(provider.TryCreate(values.Ally, finalAttack, true, out CombatClashSideInput attacker), Is.True);
            Assert.That(provider.TryCreate(values.Enemy, finalAttack, false, out CombatClashSideInput responder), Is.True);
            Assert.That(attacker.ClashModifier, Is.EqualTo(2));
            Assert.That(new FinalCombatClashRule(attacker, responder, new ZeroRandom()).Resolve(
                new CombatClashRequest(
                    new CombatAttackDeclaration(values.Ally, values.Enemy, finalAttack),
                    CombatResponseState.CounterDeclared,
                    new CombatResponseDeclaration(values.Enemy, finalAttack))),
                Is.EqualTo(CombatClashOutcome.AttackerWin));

            values.AllyState.ApplyStatus(postureFracture);
            values.AllyState.ApplyStatus(postureFracture);
            BeginCounteredExchange(values, finalAttack, finalAttack);
            Assert.That(values.Machine.TryResolveClash(new FinalCombatClashRule(
                new CombatClashSideInput(values.Ally.Id, finalAttack.Id, 3, 1, true),
                new CombatClashSideInput(values.Enemy.Id, finalAttack.Id, 1, 1, false),
                new ZeroRandom()), values.Session.ExchangeState.Version), Is.True);
            PrepareExecution(values);
            Assert.That(values.Machine.TryResolvePosture(new FinalCombatPostureRule()), Is.True);
            Assert.That(values.EnemyState.CurrentPosture, Is.EqualTo(3));

            RuntimeFixture loss = CreateFixture();
            loss.Ally.Add(finalAttack);
            loss.Enemy.Add(finalAttack);
            loss.AllyState.ApplyStatus(damageBoost);
            int enemyHpBefore = loss.Enemy.HP;
            BeginCounteredExchange(loss, finalAttack, finalAttack);
            Assert.That(loss.Machine.TryResolveClash(new FinalCombatClashRule(
                new CombatClashSideInput(loss.Ally.Id, finalAttack.Id, 1, 1, true),
                new CombatClashSideInput(loss.Enemy.Id, finalAttack.Id, 3, 1, false),
                new ZeroRandom()), loss.Session.ExchangeState.Version), Is.True);
            PrepareExecution(loss);
            Assert.That(loss.Enemy.HP, Is.EqualTo(enemyHpBefore));
        }

        private static void VerifyAllOutDoesNotAdvanceStatusDuration(
            CombatStatusDefinitionSO damageBoost,
            SoSkill finalAttack)
        {
            RuntimeFixture fixture = CreateFixture();
            SoSkill allOutSkill = CreateAllOutSkill();
            fixture.Ally.Add(finalAttack);
            fixture.Ally.Add(allOutSkill);
            fixture.Enemy.SetStunned(true);
            fixture.AllyState.ApplyStatus(damageBoost);
            BindWithSynchronousPresentation(fixture);

            SubmitUnopposedAttack(fixture, finalAttack);
            Assert.That(fixture.Machine.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(fixture.Session.ExchangeState.IsAllOutCandidate, Is.True);
            Assert.That(GetStatus(fixture.AllyState, damageBoost.StatusId).RemainingDuration, Is.EqualTo(1));

            Assert.That(fixture.Orchestrator.ExecuteAllOut(fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(GetStatus(fixture.AllyState, damageBoost.StatusId).RemainingDuration, Is.EqualTo(1));
        }

        private static void VerifyVictoryProducesCombatResult(SoSkill finalAttack)
        {
            RuntimeFixture fixture = CreateFixture(enemyMaxHp: 3);
            fixture.Ally.Add(finalAttack);
            BindWithSynchronousPresentation(fixture);
            SubmitUnopposedAttack(fixture, finalAttack);

            Assert.That(fixture.Machine.Phase, Is.EqualTo(Phase.ExitCombat));
            Assert.That(fixture.Machine.EndReason, Is.EqualTo(CombatEndReason.Victory));
            CombatResult result = CombatResultBuilder.Build(fixture.Session, fixture.Machine.EndReason);
            Assert.That(result.IsWin, Is.True);
            Assert.That(result.DefeatedEnemyIds, Is.EquivalentTo(new[] { fixture.Enemy.Id.Value }));
        }

        private static RuntimeFixture CreateFixture(int maxPosture = 6, int enemyMaxHp = 20)
        {
            TestCombatant ally = new TestCombatant(1, Side.Allies, 20);
            TestCombatant enemy = new TestCombatant(2, Side.Enemies, enemyMaxHp);
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

            GameObject owner = new GameObject("CBT_STATUS_02C.Runtime");
            CombatFlowOrchestrator orchestrator = owner.AddComponent<CombatFlowOrchestrator>();
            GameObject binderObject = new GameObject("CBT_STATUS_02C.Binder");
            FinalCombatUIBinder binder = binderObject.AddComponent<FinalCombatUIBinder>();
            GameObject statusRoot = new GameObject("CBT_STATUS_02C.StatusRoot");
            statusRoot.SetActive(false);
            Text statusText = new GameObject("CBT_STATUS_02C.StatusText").AddComponent<Text>();
            statusText.transform.SetParent(statusRoot.transform);
            FinalCombatStatusListPresenter presenter = statusRoot.AddComponent<FinalCombatStatusListPresenter>();
            SetPrivateField(presenter, "statusRoot", statusRoot);
            SetPrivateField(presenter, "statusText", statusText);
            SetPrivateField(binder, "statusListPresenter", presenter);
            SetPrivateField(binder, "panelRoot", binderObject);

            return new RuntimeFixture(
                session,
                machine,
                orchestrator,
                binder,
                statusRoot,
                statusText,
                ally,
                enemy);
        }

        private static void BindWithSynchronousPresentation(RuntimeFixture fixture)
        {
            fixture.Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
            fixture.Orchestrator.OutcomePresentationRequested += request => request.TryComplete();
            Assert.That(fixture.Orchestrator.BindFinalExchange(
                fixture.Session,
                fixture.Machine,
                new ZeroRandom(),
                new AuthoredCombatClashSideInputProvider(fixture.Session)), Is.True);
        }

        private static void SubmitUnopposedAttack(RuntimeFixture fixture, ISkill skill)
        {
            Assert.That(fixture.Orchestrator.SubmitAttackDeclaration(
                new CombatAttackDeclaration(fixture.Ally, fixture.Enemy, skill),
                fixture.Session.ExchangeState.Version), Is.True);
            Assert.That(fixture.Orchestrator.SubmitNoResponse(
                fixture.Session.ExchangeState.Version), Is.True);
        }

        private static void ContinueUnopposedChain(RuntimeFixture fixture, ISkill skill)
        {
            Assert.That(fixture.Orchestrator.ContinueChain(skill, fixture.Session.ExchangeState.Version), Is.True);
            SubmitUnopposedAttack(fixture, skill);
        }

        private static void BeginCounteredExchange(RuntimeFixture fixture, ISkill allySkill, ISkill enemySkill)
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

        private static void PrepareExecution(RuntimeFixture fixture)
        {
            Assert.That(fixture.Machine.TryPrepareOutcome(), Is.True);
            Assert.That(fixture.Machine.TryPrepareSkillExecution(), Is.True);
            Assert.That(fixture.Machine.TryExecutePreparedSkill(), Is.True);
        }

        private static CombatStatusRuntime GetStatus(CombatantCombatState state, string statusId)
        {
            Assert.That(state.TryGetStatus(statusId, out CombatStatusRuntime status), Is.True, statusId);
            return status;
        }

        private static SoSkill CreateAllOutSkill()
        {
            SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
            definition.skillId = 920004;
            definition.displayName = "Test All-Out";
            definition.targeting = TargetingRule.AllEnemies;
            definition.baseDamage = 1;
            return new SoSkill(definition);
        }

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(FixtureRoot + name);
            Assert.That(asset, Is.Not.Null, FixtureRoot + name);
            return asset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private void CaptureRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                CombatSession session,
                CombatStateMachine machine,
                CombatFlowOrchestrator orchestrator,
                FinalCombatUIBinder binder,
                GameObject statusRoot,
                Text statusText,
                TestCombatant ally,
                TestCombatant enemy)
            {
                Session = session;
                Machine = machine;
                Orchestrator = orchestrator;
                Binder = binder;
                StatusRoot = statusRoot;
                StatusText = statusText;
                Ally = ally;
                Enemy = enemy;
            }

            public CombatSession Session { get; }
            public CombatStateMachine Machine { get; }
            public CombatFlowOrchestrator Orchestrator { get; }
            public FinalCombatUIBinder Binder { get; }
            public GameObject StatusRoot { get; }
            public Text StatusText { get; }
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
            private readonly int _maxHp;
            private CombatantCombatState _state;
            private int _hp;

            public TestCombatant(int id, Side side, int maxHp)
            {
                Id = new CombatantId(id);
                Side = side;
                _maxHp = maxHp;
                _hp = maxHp;
            }

            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? _hp;
            public int MaxHP => _maxHp;
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
