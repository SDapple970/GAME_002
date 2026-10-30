#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Environment;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    public sealed class CombatAllOutRecoveryTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<FinalCombatPlayerCommandController> _players = new();
        private float _timeScale;
        private float _fixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _fixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FinalCombatPlayerCommandController player in _players) player.Unbind();
            _players.Clear();
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
            Time.timeScale = _timeScale;
            Time.fixedDeltaTime = _fixedDeltaTime;
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(2, 2, true)]
        public void Candidate_RequiresEveryLivingEnemyAndNonterminalRoster(int enemies, int stunned, bool expected)
        {
            Fixture f = Create(enemies);
            for (int i = 0; i < stunned; i++) f.Enemies[i].SetStunned(true);
            Assert.That(CombatAllOutPolicy.IsCandidate(f.Session), Is.EqualTo(expected));
            if (enemies == 0)
            {
                f.Bind();
                Assert.That(f.State.EndReason, Is.EqualTo(CombatEndReason.Victory));
                Assert.That(f.Orchestrator.PendingDecisionRequest, Is.Null);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeathAndStun_AreDistinctAndTerminalPrecedesSelection(bool alliesWiped)
        {
            Fixture f = Create();
            f.Enemies[0].SetStunned(true);
            if (alliesWiped) f.Ally.ApplyDamage(100); else f.Enemies[0].ApplyDamage(100);
            f.Bind();
            Assert.That(CombatAllOutPolicy.IsCandidate(f.Session), Is.False);
            Assert.That(f.State.EndReason, Is.EqualTo(alliesWiped ? CombatEndReason.Defeat : CombatEndReason.Victory));
            Assert.That(f.Enemies[0].RecoveryCount, Is.Zero);
        }

        [Test]
        public void InitialAllStunned_OffersBeforeRecoveryAndDeclineRecoversLivingOnlyOnce()
        {
            Fixture f = Create(2);
            f.Ally.SetStunned(true);
            foreach (Actor enemy in f.Enemies) enemy.SetStunned(true);
            f.Bind();
            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));
            Assert.That(f.Player.ViewState.CanAllOut, Is.False);
            Assert.That(f.Ally.IsStunned, Is.True);
            f.Session.GetCombatState(f.Ally).SetPosture(3);
            f.Session.GetCombatState(f.Enemies[0]).SetPosture(3);
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            Assert.That(f.Player.DeclineAllOut(request), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Ally.IsStunned, Is.False);
            Assert.That(f.Session.GetCombatState(f.Ally).CurrentPosture, Is.Zero);
            Assert.That(f.Session.GetCombatState(f.Enemies[0]).CurrentPosture, Is.Zero);
            Assert.That(f.Ally.RecoveryCount, Is.EqualTo(1));
            Assert.That(f.Orchestrator.DeclineAllOut(request), Is.False);
            f.Orchestrator.AdvanceUntilBlocked();
            Assert.That(f.Ally.RecoveryCount, Is.EqualTo(1));
        }

        [Test]
        public void StunnedActingSideWithoutCandidate_RecoversInsteadOfWaitingForever()
        {
            Fixture f = Create(2);
            f.Ally.SetStunned(true);
            f.Enemies[0].SetStunned(true);
            f.Bind();
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Ally.IsStunned, Is.False);
            Assert.That(f.Player.ViewState.SelectableActors, Contains.Item(f.Ally));
        }

        [Test]
        public void ClashCandidate_IsObservedBeforeRecovery_DeclineResetsPosture()
        {
            Fixture f = Create();
            f.Bind();
            f.Session.GetCombatState(f.Enemies[0]).SetPosture(2);
            DriveClash(f);
            Assert.That(f.Session.ExchangeState.IsAllOutCandidate, Is.True);
            Assert.That(f.Enemies[0].IsStunned, Is.True);
            Assert.That(f.Enemies[0].RecoveryCount, Is.Zero);
            Assert.That(f.Session.GetCombatState(f.Enemies[0]).CurrentPosture, Is.EqualTo(3));
            Assert.That(f.Player.ConfirmContinue(), Is.False);
            Assert.That(f.Orchestrator.ContinueChain(f.Session.ExchangeState.Version), Is.False);
            Assert.That(f.Player.EndChain(), Is.True);
            Assert.That(f.Enemies[0].RecoveryCount, Is.EqualTo(1));
            Assert.That(f.Session.GetCombatState(f.Enemies[0]).CurrentPosture, Is.Zero);
            Assert.That(f.State.TryDeclareAttack(f.Ally, f.Enemies[0], f.Attack), Is.True);
        }

        [TestCase("wake")]
        [TestCase("dead")]
        [TestCase("remove")]
        [TestCase("ally-dead")]
        public void ChangedCandidate_RejectsEffectsThenRefreshesFlow(string change)
        {
            Fixture f = Candidate();
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            int hp = f.Enemies[0].HP;
            if (change == "wake") f.Enemies[0].SetStunned(false);
            if (change == "dead") f.Enemies[0].ApplyDamage(100);
            if (change == "remove") f.Session.Enemies.Clear();
            if (change == "ally-dead") f.Ally.ApplyDamage(100);
            int version = f.Session.ExchangeState.Version;
            Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
            Assert.That(f.Session.ExchangeState.Version, Is.EqualTo(version));
            Assert.That(f.Enemies[0].HP, Is.EqualTo(change == "dead" ? 0 : hp));
            f.Orchestrator.Tick(0f, 0f);
            Assert.That(f.Orchestrator.IsAllOutSelection, Is.False);
            Assert.That(f.State.Phase, Is.EqualTo(change == "wake" ? Phase.Standoff : Phase.ExitCombat));
            if (change == "ally-dead") Assert.That(f.State.EndReason, Is.EqualTo(CombatEndReason.Defeat));
        }

        [Test]
        public void RemovedOneOfManyTargets_UsesCurrentRosterAndSkipsRemovedActor()
        {
            Fixture f = Candidate(2);
            Actor removed = f.Enemies[1];
            f.Session.Enemies.Remove(removed);
            Assert.That(f.Player.ConfirmAllOut(), Is.True);
            Assert.That(removed.HP, Is.EqualTo(20));
            Assert.That(f.Enemies[0].HP, Is.EqualTo(16));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Accept_UsesSkillRunnerDamageStatusesAndSingleExecution(bool lethal)
        {
            Fixture f = Candidate(2, lethal ? 30 : 4);
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            int presentations = 0;
            int completions = 0;
            f.Orchestrator.AllOutPresentationRequested += result =>
            {
                presentations++;
                Assert.That(result.Skill, Is.SameAs(f.AllOut));
                Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
            };
            f.Orchestrator.TerminalCompleted += (_, _, _) => completions++;
            Assert.That(f.Player.ConfirmAllOut(request), Is.True);
            Assert.That(f.Player.ConfirmAllOut(request), Is.False);
            Assert.That(f.Orchestrator.ExecuteAllOut(request.ExchangeVersion), Is.False);
            foreach (Actor enemy in f.Enemies)
            {
                Assert.That(enemy.HP, Is.EqualTo(lethal ? 0 : 16));
                bool applied = f.Session.GetCombatState(enemy).TryGetStatus("test.allout", out CombatStatusRuntime status);
                Assert.That(applied, Is.EqualTo(!lethal));
                if (!lethal) Assert.That(status.StackCount, Is.EqualTo(1));
                Assert.That(enemy.RecoveryCount, Is.EqualTo(lethal ? 0 : 1));
            }
            Assert.That(presentations, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(lethal ? 1 : 0));
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.Zero);
            f.Orchestrator.AdvanceUntilBlocked();
            Assert.That(completions, Is.EqualTo(lethal ? 1 : 0));
            if (!lethal)
            {
                Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
                Assert.That(f.State.TryDeclareAttack(f.Ally, f.Enemies[0], f.Attack), Is.True);
            }
        }

        [Test]
        public void DamageCallbackAndRecoveryCallback_CannotReenterExecutionOrDecline()
        {
            Fixture f = Candidate(2);
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            f.Enemies[0].DamageCallback = () =>
            {
                Assert.That(f.State.TryExecuteAllOut(request.ExchangeVersion, out _), Is.False);
                Assert.That(f.State.TryEndChain(request.ExchangeVersion), Is.False);
                Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
                f.Orchestrator.AdvanceUntilBlocked();
            };
            f.Enemies[0].RecoveryCallback = () =>
                Assert.That(f.State.TryEndChain(request.ExchangeVersion), Is.False);
            Assert.That(f.Player.ConfirmAllOut(), Is.True);
            Assert.That(f.Enemies[0].HP, Is.EqualTo(16));
            Assert.That(f.Enemies[1].HP, Is.EqualTo(16));
            Assert.That(f.Enemies[0].RecoveryCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingDamageCallback_AbortsPartialEffectsAndCannotRetry()
        {
            Fixture f = Candidate(2);
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            f.Enemies[0].DamageCallback = () => throw new InvalidOperationException("06A injected damage failure");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("06A injected damage failure"));
            Assert.That(f.Player.ConfirmAllOut(), Is.False);
            Assert.That(f.State.EndReason, Is.EqualTo(CombatEndReason.Abort));
            Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
            Assert.That(f.Enemies[0].HP, Is.EqualTo(16));
            Assert.That(f.Enemies[1].HP, Is.EqualTo(20));
            Assert.That(f.Enemies[0].RecoveryCount, Is.Zero);
            Assert.That(f.Orchestrator.PendingDecisionRequest, Is.Null);
        }

        [Test]
        public void DeadStunnedActor_IsExcludedAndNotRecoveredAtDecline()
        {
            Fixture f = Candidate(2);
            f.Enemies[1].ApplyDamage(100);
            Assert.That(CombatAllOutPolicy.IsCandidate(f.Session), Is.True);
            Assert.That(f.Player.EndChain(), Is.True);
            Assert.That(f.Enemies[0].RecoveryCount, Is.EqualTo(1));
            Assert.That(f.Enemies[1].HP, Is.Zero);
            Assert.That(f.Enemies[1].RecoveryCount, Is.Zero);
        }

        [Test]
        public void RepeatedPump_PreservesOneCandidateRequestAndDoesNotRestartTimer()
        {
            Fixture f = Create();
            f.Enemies[0].SetStunned(true);
            int requests = 0;
            f.Orchestrator.ChainDecisionRequested += _ => requests++;
            f.Bind();
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            f.Orchestrator.Tick(0f, 2f);
            float remaining = f.Orchestrator.AllOutDecisionTimeRemaining;
            f.Orchestrator.AdvanceUntilBlocked();
            f.Orchestrator.AdvanceUntilBlocked();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(f.Orchestrator.PendingDecisionRequest, Is.SameAs(request));
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.EqualTo(remaining));
        }

        [Test]
        public void AbortDuringAllOut_StopsLaterTargetsAndAllStatuses()
        {
            Fixture f = Candidate(2);
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            f.Enemies[0].DamageCallback = () => f.State.ForceExit(CombatEndReason.Abort);
            Assert.That(f.Player.ConfirmAllOut(request), Is.True);
            Assert.That(f.State.EndReason, Is.EqualTo(CombatEndReason.Abort));
            Assert.That(f.Enemies[0].HP, Is.EqualTo(16));
            Assert.That(f.Enemies[1].HP, Is.EqualTo(20));
            Assert.That(f.Session.GetCombatState(f.Enemies[0]).HasStatus("test.allout"), Is.False);
            Assert.That(f.Session.GetCombatState(f.Enemies[1]).HasStatus("test.allout"), Is.False);
            Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
        }

        [Test]
        public void RemovedTargetDuringAllOut_IsNotDamagedOrGivenStatus()
        {
            Fixture f = Candidate(2);
            f.Enemies[0].DamageCallback = () => f.Session.Enemies.Remove(f.Enemies[1]);
            Assert.That(f.Player.ConfirmAllOut(), Is.True);
            Assert.That(f.Enemies[0].HP, Is.EqualTo(16));
            Assert.That(f.Enemies[1].HP, Is.EqualTo(20));
            Assert.That(f.Session.GetCombatState(f.Enemies[1]).HasStatus("test.allout"), Is.False);
        }

        [Test]
        public void SessionSwitchDuringEffect_DoesNotClearOrDamageNewSession()
        {
            Fixture f = Candidate();
            Fixture next = Candidate();
            CombatExchangeDecisionRequest stale = f.Orchestrator.PendingDecisionRequest;
            f.Enemies[0].DamageCallback = () =>
                Assert.That(f.Orchestrator.BindFinalExchange(next.Session, next.State, new ZeroRandom(), new AllyWins()), Is.True);
            Assert.That(f.Player.ConfirmAllOut(stale), Is.True);
            Assert.That(next.Enemies[0].HP, Is.EqualTo(20));
            Assert.That(f.Orchestrator.PendingDecisionRequest, Is.Not.Null);
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.GreaterThan(0f));
            Assert.That(f.Orchestrator.ExecuteAllOut(stale), Is.False);
            Assert.That(f.Player.ViewState.DecisionKind, Is.Null);
        }

        [Test]
        public void Timeout_UsesUnscaledTimeDeclinesAndDoesNotApplyDamage()
        {
            Fixture f = Candidate();
            CombatExchangeDecisionRequest old = f.Orchestrator.PendingDecisionRequest;
            float duration = f.Orchestrator.AllOutDecisionTimeRemaining;
            Time.timeScale = 0.25f;
            f.Orchestrator.Tick(0f, duration + 1f);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
            Assert.That(f.Enemies[0].HP, Is.EqualTo(20));
            Assert.That(f.Enemies[0].IsStunned, Is.False);
            Assert.That(f.Orchestrator.ExecuteAllOut(old), Is.False);
            Assert.That(f.Orchestrator.DeclineAllOut(old), Is.False);
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.Zero);
        }

        [Test]
        public void Pause_SuspendsTimerAndCommandsAndResumesExistingChoice()
        {
            GameStateMachine state = Object("State").AddComponent<GameStateMachine>();
            typeof(GameStateMachine).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state, null);
            GameFlowController flow = Object("Flow").AddComponent<GameFlowController>();
            typeof(GameFlowController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
            Fixture f = Candidate();
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            float remaining = f.Orchestrator.AllOutDecisionTimeRemaining;
            flow.Pause();
            f.Orchestrator.Tick(5f, 100f);
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.EqualTo(remaining));
            Assert.That(f.Player.ViewState.DecisionKind, Is.Null);
            Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
            Assert.That(f.Orchestrator.DeclineAllOut(request), Is.False);
            flow.ResumePreviousState();
            Assert.That(f.Player.ViewState.CanAllOut, Is.True);
            Assert.That(f.Player.DeclineAllOut(request), Is.True);
        }

        [TestCase("abort")]
        [TestCase("unbind")]
        [TestCase("disable")]
        public void Cancellation_ClearsChoiceTimerAndDirectorSlowMotion(string kind)
        {
            Fixture f = Candidate();
            Time.timeScale = 0.75f;
            CombatDirector director = Object("Director").AddComponent<CombatDirector>();
            director.BindFinalExchange(f.Orchestrator, f.Session);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f));
            CombatExchangeDecisionRequest request = f.Orchestrator.PendingDecisionRequest;
            if (kind == "abort") f.Orchestrator.TerminateExplicit(CombatEndReason.Abort, request.ExchangeVersion);
            else if (kind == "disable")
            {
                f.Orchestrator.gameObject.SetActive(false);
                // EditMode does not dispatch the MonoBehaviour lifecycle for this fixture.
                typeof(CombatFlowOrchestrator).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(f.Orchestrator, null);
            }
            else f.Orchestrator.BindSession(null);
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(_fixedDeltaTime));
            Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.Zero);
            Assert.That(f.Orchestrator.PendingDecisionRequest, Is.Null);
            Assert.That(f.Player.ViewState.DecisionKind, Is.Null);
            Assert.That(f.Orchestrator.ExecuteAllOut(request), Is.False);
        }

        [Test]
        public void StaleRequestAcrossSessions_CannotAcceptOrDeclineEvenWithSameVersion()
        {
            Fixture first = Candidate();
            CombatExchangeDecisionRequest stale = first.Orchestrator.PendingDecisionRequest;
            Fixture second = Candidate();
            Assert.That(second.Session.ExchangeState.Version, Is.EqualTo(stale.ExchangeVersion));
            Assert.That(second.Orchestrator.ExecuteAllOut(stale), Is.False);
            Assert.That(second.Orchestrator.DeclineAllOut(stale), Is.False);
            Assert.That(second.Player.ConfirmAllOut(stale), Is.False);
        }

        [Test]
        public void MissingAuthoredSkill_IsVisibleDisabledWithReasonAndDeclineAvailable()
        {
            Fixture f = Candidate(authoredSkill: false);
            FinalCombatUIBinder binder = Object("Binder").AddComponent<FinalCombatUIBinder>();
            GameObject options = Object("Options");
            TMP_Text context = Object("Context").AddComponent<TextMeshProUGUI>();
            Set(binder, "handoffListRoot", options);
            Set(binder, "contextText", context);
            Set(binder, "optionButtonPrefab", Object("OptionTemplate").AddComponent<Button>());
            binder.Bind(f.Session, f.State, f.Orchestrator);
            Button[] buttons = options.GetComponentsInChildren<Button>();
            Assert.That(buttons, Has.Length.EqualTo(2));
            Assert.That(buttons[0].interactable, Is.False);
            Assert.That(buttons[1].interactable, Is.True);
            StringAssert.Contains("AllEnemies", context.text);
            buttons[1].onClick.Invoke();
            Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
        }

        [Test]
        public void MissingChoiceConnections_ReportsVerifiableError()
        {
            Fixture f = Candidate();
            FinalCombatUIBinder binder = Object("UnwiredBinder").AddComponent<FinalCombatUIBinder>();
            LogAssert.Expect(LogType.Error, "[FinalCombatUIBinder] All-Out requires handoffListRoot, contextText and optionButtonPrefab connections.");
            binder.Bind(f.Session, f.State, f.Orchestrator);
            Assert.That(binder.ViewState.CanAllOut, Is.True);
        }

        private Fixture Candidate(int count = 1, int damage = 4, bool authoredSkill = true)
        {
            Fixture f = Create(count, damage, authoredSkill);
            foreach (Actor enemy in f.Enemies) enemy.SetStunned(true);
            f.Bind();
            return f;
        }

        private Fixture Create(int count = 1, int damage = 4, bool authoredSkill = true)
        {
            SkillDefinitionSO attackDefinition = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            attackDefinition.skillId = 1; attackDefinition.baseDamage = 1;
            attackDefinition.targeting = TargetingRule.SingleEnemy;
            SkillDefinitionSO allOutDefinition = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            allOutDefinition.skillId = 2; allOutDefinition.baseDamage = damage;
            allOutDefinition.targeting = TargetingRule.AllEnemies;
            CombatStatusDefinitionSO status = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            Set(status, "statusId", "test.allout"); Set(status, "stackPolicy", CombatStatusStackPolicy.Stack);
            Set(status, "maximumStacks", 10);
            Set(allOutDefinition, "appliedStatusEffects", new[] { status });
            Fixture f = new() { Attack = new SoSkill(attackDefinition), AllOut = new SoSkill(allOutDefinition) };
            f.Ally = new Actor(1, Side.Allies, f.Attack);
            if (authoredSkill) f.Ally.Add(f.AllOut);
            f.Session = new CombatSession(StartReason.PlayerFirstHit, Side.Allies, new InspirationPool(10, 0),
                new CombatEnvironment(), CombatFlowMode.StandoffClashChain, new CombatRuntimeConfig(10, 10, 3, 0));
            f.Session.Allies.Add(f.Ally);
            for (int i = 0; i < count; i++)
            {
                Actor enemy = new(10 + i, Side.Enemies, f.Attack);
                f.Enemies.Add(enemy); f.Session.Enemies.Add(enemy);
            }
            f.State = new CombatStateMachine(f.Session);
            f.Orchestrator = Object("06A.Orchestrator").AddComponent<CombatFlowOrchestrator>();
            f.Player = new FinalCombatPlayerCommandController();
            _players.Add(f.Player);
            return f;
        }

        private static void DriveClash(Fixture f)
        {
            Assert.That(f.Orchestrator.SubmitAttackDeclaration(new CombatAttackDeclaration(f.Ally, f.Enemies[0], f.Attack),
                f.Session.ExchangeState.Version), Is.True);
            Assert.That(f.Orchestrator.SubmitResponse(new CombatResponseDeclaration(f.Enemies[0], f.Attack),
                f.Orchestrator.PendingDecisionRequest), Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        private GameObject Object(string name) => Track(new GameObject(name));
        private T Track<T>(T value) where T : UnityEngine.Object { _created.Add(value); return value; }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class Fixture
        {
            public CombatSession Session;
            public CombatStateMachine State;
            public CombatFlowOrchestrator Orchestrator;
            public FinalCombatPlayerCommandController Player;
            public Actor Ally;
            public readonly List<Actor> Enemies = new();
            public ISkill Attack, AllOut;
            public void Bind()
            {
                Orchestrator.ApproachPresentationRequested += request => request.TryComplete();
                Assert.That(Orchestrator.BindFinalExchange(Session, State, new ZeroRandom(), new AllyWins()), Is.True);
                Assert.That(Player.Bind(Orchestrator, Session), Is.True);
            }
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => 0;
        }

        private sealed class AllyWins : ICombatClashSideInputProvider
        {
            public bool TryCreate(ICombatant actor, ISkill skill, bool owner, out CombatClashSideInput input)
            {
                input = new CombatClashSideInput(actor.Id, skill.Id, actor.Side == Side.Allies ? 100 : 0, 1, owner);
                return true;
            }
        }

        private sealed class Actor : ICombatant, ICombatantRuntimeStateBinding
        {
            private readonly List<ISkill> _skills = new();
            private CombatantCombatState _state;
            public CombatantId Id { get; }
            public Side Side { get; }
            public int HP => _state?.CurrentHp ?? 20;
            public int MaxHP => 20;
            public KeywordMask Weakness => KeywordMask.None;
            public KeywordMask Resist => KeywordMask.None;
            public int Stagger => 0;
            public int StaggerMax => 3;
            public bool IsStunned { get; private set; }
            public IReadOnlyList<ISkill> Skills => _skills;
            public int RecoveryCount { get; private set; }
            public Action DamageCallback, RecoveryCallback;
            public Actor(int id, Side side, ISkill skill) { Id = new CombatantId(id); Side = side; Add(skill); }
            public void Add(ISkill skill) => _skills.Add(skill);
            public void BindCombatState(CombatantCombatState state) => _state = state;
            public void ApplyDamage(int amount) { _state.ApplyDamage(amount); DamageCallback?.Invoke(); }
            public void AddStagger(int amount) { }
            public void SetStunned(bool value) { IsStunned = value; }
            public void ResetStaggerIfNeededOnStunEnd() { RecoveryCount++; RecoveryCallback?.Invoke(); }
        }
    }
}
#endif
