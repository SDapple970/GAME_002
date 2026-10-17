#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Combat
{
    [Category("CBT_ENEMY_04B")]
    public sealed class FinalCombatDeclarationFeintTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCase(null)]
        [TestCase(false)]
        public void OrdinaryEnemy_KeepsFirstValidAndRejectsReplacement(bool? enabled)
        {
            Fixture f = Create(enabled);
            Assert.That(f.Exchange.CurrentDeclaration.Skill.Id.Value, Is.EqualTo(1));
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.False);
            Assert.That(f.Exchange.DeclarationRevision, Is.Zero);
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(3));
        }

        [Test]
        public void ReplaceFirst_InvalidatesOldResponseAndSelection_RepublishesDeclarationAndPaysFinalSkillOnce()
        {
            Fixture f = Create();
            FinalCombatPlayerCommandController player = new();
            Assert.That(player.Bind(f.Orchestrator, f.Session), Is.True);
            Assert.That(player.SelectSkill(f.ResponseSkill), Is.True);
            Assert.That(player.SelectTarget(f.Enemy), Is.True);
            CombatExchangeDecisionRequest oldRequest = f.Orchestrator.PendingDecisionRequest;
            CombatAttackDeclaration oldAttack = f.Exchange.CurrentDeclaration;
            CombatDeclarationToken token = f.Token;
            float pressure = f.Session.StandoffState.CurrentPressure;

            Assert.That(f.Controller.TryReplaceDeclaration(token, f.Replacement), Is.True);
            Assert.That(f.Exchange.CurrentDeclaration, Is.Not.SameAs(oldAttack));
            Assert.That(f.Exchange.CurrentDeclaration.Skill, Is.SameAs(f.Replacement));
            Assert.That(f.Token.DeclarationId, Is.EqualTo(token.DeclarationId));
            Assert.That(f.Token.Revision, Is.EqualTo(token.Revision + 1));
            Assert.That(f.Token.ExchangeVersion, Is.GreaterThan(token.ExchangeVersion));
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(3));
            Assert.That(f.Session.StandoffState.CurrentPressure, Is.EqualTo(pressure));
            Assert.That(player.ViewState.SelectedSkill, Is.Null);
            Assert.That(player.ViewState.SelectedTarget, Is.Null);
            Assert.That(player.ViewState.DeclaredAttack.Skill, Is.SameAs(f.Replacement));
            Assert.That(player.ViewState.DeclarationToken.Value.Revision, Is.EqualTo(1));
            Assert.That(player.ViewState.CanNoResponse, Is.True);
            Assert.That(f.Orchestrator.SubmitResponse(new CombatResponseDeclaration(f.Ally, f.ResponseSkill), oldRequest), Is.False);
            Assert.That(f.Orchestrator.SubmitNoResponse(oldRequest), Is.False);
            Assert.That(f.Orchestrator.SubmitResponse(new CombatResponseDeclaration(f.Ally, f.ResponseSkill), token.ExchangeVersion), Is.False);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Original), Is.False);

            Assert.That(player.SelectSkill(f.ResponseSkill), Is.True);
            Assert.That(player.SelectTarget(f.Enemy), Is.True);
            Assert.That(player.Confirm(), Is.True);
            Assert.That(f.Exchange.CommittedAttackMpCost, Is.EqualTo(2));
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(1));
            Assert.That(f.AllyState.CurrentMp, Is.EqualTo(2));
            Assert.That(f.Orchestrator.SubmitNoResponse(oldRequest), Is.False);
            Assert.That(f.State.TryCommitExchange(f.Exchange.Version), Is.False);
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(1));
            player.Unbind();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResponseFirst_LocksOriginalBeforeOrchestratorCommit(bool noResponse)
        {
            Fixture f = Create();
            CombatDeclarationToken token = f.Token;
            // Deliberately exercise the Core approval boundary without immediately pumping a commit.
            Assert.That(noResponse ? f.State.ConfirmNoResponse(token.ExchangeVersion) :
                f.State.TryDeclareResponse(new CombatResponseDeclaration(f.Ally, f.ResponseSkill), token.ExchangeVersion), Is.True);
            Assert.That(f.Exchange.IsCommitted, Is.False);
            Assert.That(f.State.TryReplaceEnemyDeclaration(f.Token, f.Replacement), Is.False);
            Assert.That(f.Controller.TryReplaceDeclaration(token, f.Replacement), Is.False);
            f.Orchestrator.AdvanceUntilBlocked();
            Assert.That(f.Exchange.CurrentDeclaration.Skill, Is.SameAs(f.Original));
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(2));
            Assert.That(f.Exchange.CommittedAttackMpCost, Is.EqualTo(1));
        }

        [TestCase("unowned")]
        [TestCase("same")]
        [TestCase("mp")]
        [TestCase("target")]
        [TestCase("utility")]
        [TestCase("stun")]
        [TestCase("panic")]
        [TestCase("dead")]
        [TestCase("deadTarget")]
        public void InvalidReplacement_LeavesOriginalResourcesAndPendingRequestUntouched(string failure)
        {
            Fixture f = Create();
            ISkill replacement = f.Replacement;
            if (failure == "unowned") replacement = new SoSkill(Skill(99, 1, 1));
            if (failure == "same") replacement = f.Original;
            if (failure == "mp") f.EnemyState.SetMp(1);
            if (failure == "target") replacement = f.Enemy.Skills[2];
            if (failure == "utility") replacement = f.Enemy.Skills[3];
            if (failure == "stun") f.Enemy.SetStunned(true);
            if (failure == "panic") f.EnemyState.ApplyMentalDelta(-100);
            if (failure == "dead") f.EnemyState.ApplyDamage(100);
            if (failure == "deadTarget") f.AllyState.ApplyDamage(100);
            CombatAttackDeclaration original = f.Exchange.CurrentDeclaration;
            CombatExchangeDecisionRequest pending = f.Orchestrator.PendingDecisionRequest;
            int version = f.Exchange.Version;
            int mp = f.EnemyState.CurrentMp;
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, replacement), Is.False);
            Assert.That(f.Exchange.CurrentDeclaration, Is.SameAs(original));
            Assert.That(f.Orchestrator.PendingDecisionRequest, Is.SameAs(pending));
            Assert.That(f.Exchange.Version, Is.EqualTo(version));
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(mp));
            Assert.That(f.Exchange.HasReplacedDeclaration, Is.False);
            Assert.That(f.Exchange.CurrentExecutionResult, Is.Null);
        }

        [TestCase("session")]
        [TestCase("declaration")]
        [TestCase("revision")]
        [TestCase("version")]
        public void MismatchedToken_IsRejected(string mismatch)
        {
            Fixture f = Create();
            CombatDeclarationToken t = f.Token;
            CombatDeclarationToken wrong = new(mismatch == "session" ? "old-session" : t.SessionId,
                t.DeclarationId + (mismatch == "declaration" ? 1 : 0),
                t.Revision + (mismatch == "revision" ? 1 : 0),
                t.ExchangeVersion + (mismatch == "version" ? 1 : 0));
            Assert.That(f.Controller.TryReplaceDeclaration(wrong, f.Replacement), Is.False);
            Assert.That(f.Exchange.DeclarationRevision, Is.Zero);
        }

        [Test]
        public void ProfileSnapshot_SeparatesCuePermissionFromReplacementPermission()
        {
            Fixture f = Create(enabled: true, cue: false);
            SetField(f.Profile, "declarationFeintEnabled", false);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.True,
                "Changing a definition after combat entry must not change its snapshot.");
            Fixture cueOnly = Create(enabled: false, cue: true);
            Assert.That(cueOnly.Controller.TryReplaceDeclaration(cueOnly.Token, cueOnly.Replacement), Is.False);
        }

        [Test]
        public void InvalidAttempt_DoesNotConsumeTheSuccessfulReplacementAllowance()
        {
            Fixture f = Create();
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Enemy.Skills[2]), Is.False);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.True);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Original), Is.False);
        }

        [Test]
        public void ReentrantReplacement_OnlyPublishesCurrentResponseToLaterSubscribers()
        {
            Fixture f = Create(startController: false);
            List<CombatExchangeDecisionRequest> received = new();
            bool? nestedResponse = null;
            f.Exchange.OnStateChanged += _ =>
            {
                if (f.Exchange.HasReplacedDeclaration)
                    nestedResponse = f.Orchestrator.SubmitNoResponse(f.Exchange.Version);
            };
            f.Orchestrator.ResponseDecisionRequested += request =>
            {
                if (request.DeclarationToken?.Revision == 0)
                    Assert.That(f.Controller.TryReplaceDeclaration(request.DeclarationToken.Value, f.Replacement), Is.True);
            };
            f.Orchestrator.ResponseDecisionRequested += received.Add;
            f.Controller.Bind(f.Orchestrator, f.Session);
            Assert.That(nestedResponse, Is.False, "No command may enter a half-published replacement transaction.");
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].DeclarationToken.Value.Revision, Is.EqualTo(1));
            Assert.That(f.Exchange.ResponseState, Is.EqualTo(CombatResponseState.Pending));
        }

        [Test]
        public void MultipleEnemiesAndNextSession_DoNotShareReplacementUsageOrTokens()
        {
            Fixture f = Create(enemyCount: 2);
            CombatDeclarationToken old = f.Token;
            Assert.That(f.Controller.TryReplaceDeclaration(old, f.Replacement), Is.True);
            Assert.That(f.State.TryCancelAttackDeclaration(), Is.True);
            ICombatant second = f.Session.Enemies[1];
            Assert.That(f.Orchestrator.SubmitAttackDeclaration(new CombatAttackDeclaration(second, f.Ally, second.Skills[0]),
                f.Exchange.Version), Is.True);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, second.Skills[1]), Is.True);
            Assert.That(f.Controller.TryReplaceDeclaration(old, f.Original), Is.False);
            f.State.ForceExit(CombatEndReason.Abort);
            f.Orchestrator.BindSession(null);
            Assert.That(f.Orchestrator.CurrentDeclarationToken, Is.Null);
            Assert.That(f.Controller.TryReplaceDeclaration(old, f.Original), Is.False);
            Fixture next = Create();
            Assert.That(next.Controller.TryReplaceDeclaration(old, next.Replacement), Is.False);
            Assert.That(next.Exchange.HasReplacedDeclaration, Is.False);
            Assert.That(next.Controller.TryReplaceDeclaration(next.Token, next.Replacement), Is.True);
        }

        [Test]
        public void ChainDeclaration_UsesExistingCombatRulesWithoutFeint()
        {
            Fixture f = Create();
            Assert.That(f.Orchestrator.SubmitNoResponse(f.Orchestrator.PendingDecisionRequest), Is.True);
            Assert.That(f.Orchestrator.PendingApproachRequest.TryComplete(), Is.True);
            Assert.That(f.Exchange.IsChainActive, Is.True);
            Assert.That(f.Exchange.CurrentDeclaration, Is.Not.Null);
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.False);
        }

        [Test]
        public void AbortedOrDeadPendingAttack_DoesNotSpendMpOrApplyEffects()
        {
            Fixture f = Create();
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.True);
            f.EnemyState.ApplyDamage(100);
            Assert.That(f.Orchestrator.SubmitNoResponse(f.Orchestrator.PendingDecisionRequest), Is.False);
            Assert.That(f.State.TryCommitExchange(f.Exchange.Version), Is.False);
            f.State.ForceExit(CombatEndReason.Abort);
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(3));
            Assert.That(f.AllyState.CurrentHp, Is.EqualTo(10));
            Assert.That(f.AllyState.ActiveStatuses, Is.Empty);
            Assert.That(f.Orchestrator.CurrentDeclarationToken, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacedSkill_ExecutesOnlyOnceThroughClashOrUnopposed(bool respond)
        {
            Fixture f = Create();
            CombatOutcomePresentationRequest outcome = null;
            f.Orchestrator.OutcomePresentationRequested += request => outcome = request;
            Assert.That(f.Controller.TryReplaceDeclaration(f.Token, f.Replacement), Is.True);
            CombatExchangeDecisionRequest pending = f.Orchestrator.PendingDecisionRequest;
            Assert.That(respond
                ? f.Orchestrator.SubmitResponse(new CombatResponseDeclaration(f.Ally, f.ResponseSkill), pending)
                : f.Orchestrator.SubmitNoResponse(pending), Is.True);
            CombatApproachPresentationRequest approach = f.Orchestrator.PendingApproachRequest;
            Assert.That(approach.TryComplete(), Is.True);
            Assert.That(approach.TryComplete(), Is.False);
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.HasClash, Is.EqualTo(respond));
            Assert.That(f.Exchange.CurrentExecutionResult.Skill, Is.SameAs(f.Replacement));
            Assert.That(f.AllyState.CurrentHp, Is.EqualTo(7));
            Assert.That(f.AllyState.HasStatus("feint.old"), Is.False);
            Assert.That(f.AllyState.TryGetStatus("feint.new", out CombatStatusRuntime status), Is.True);
            Assert.That(status.StackCount, Is.EqualTo(1));
            Assert.That(f.EnemyState.CurrentMp, Is.EqualTo(1));
            Assert.That(f.AllyState.CurrentMp, Is.EqualTo(respond ? 2 : 3));
            f.Orchestrator.AdvanceUntilBlocked();
            Assert.That(status.StackCount, Is.EqualTo(1));
            Assert.That(f.AllyState.CurrentHp, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator ReplaceFirst_ActualEntryClashSkillRunnerAndResult()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunPlayMode(replaceFirst: true);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ResponseFirst_ActualEntryKeepsOriginalAndResult()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunPlayMode(replaceFirst: false);
            yield return new ExitPlayMode();
        }

        private IEnumerator RunPlayMode(bool replaceFirst)
        {
            Assert.That(Application.isPlaying, Is.True);
            List<string> errors = new();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message + "\n" + stack);
            };
            Application.logMessageReceived += capture;
            try
            {
                Fixture f = Create(production: true);
                FinalCombatPlayerCommandController player = new();
                Assert.That(player.Bind(f.Orchestrator, f.Session), Is.True);
                CombatDeclarationToken initial = f.Token;
                CombatExchangeDecisionRequest stale = f.Orchestrator.PendingDecisionRequest;
                List<int> executed = new();
                f.Director.FinalPresentationCueRaised += cue =>
                {
                    if (cue.Kind == CombatFinalPresentationCueKind.WinningSkill) executed.Add(cue.Skill.Id.Value);
                };
                if (replaceFirst)
                {
                    Assert.That(f.Controller.TryReplaceDeclaration(initial, f.Replacement), Is.True);
                    Assert.That(f.Orchestrator.SubmitNoResponse(stale), Is.False);
                    Assert.That(player.ViewState.DeclaredAttack.Skill, Is.SameAs(f.Replacement));
                }
                Assert.That(player.SelectSkill(f.ResponseSkill), Is.True);
                Assert.That(player.SelectTarget(f.Enemy), Is.True);
                Assert.That(player.Confirm(), Is.True);
                Assert.That(f.Controller.TryReplaceDeclaration(initial, f.Replacement), Is.False);
                Assert.That(f.Exchange.CommittedAttackMpCost, Is.EqualTo(2));
                Assert.That(f.EnemyState.CurrentMp, Is.Zero);
                Assert.That(f.AllyState.CurrentMp, Is.EqualTo(1));

                float deadline = Time.realtimeSinceStartup + 10f;
                while ((f.Orchestrator.PendingDecisionRequest?.Kind != CombatExchangeDecisionKind.Attack ||
                        f.Orchestrator.PendingDecisionRequest.ActingSide != Side.Allies) && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
                Assert.That(f.AllyState.CurrentHp, Is.EqualTo(replaceFirst ? 7 : 9));
                Assert.That(f.AllyState.HasStatus(replaceFirst ? "feint.old" : "feint.new"), Is.False);
                Assert.That(f.AllyState.TryGetStatus(replaceFirst ? "feint.new" : "feint.old", out CombatStatusRuntime status), Is.True);
                Assert.That(status.StackCount, Is.EqualTo(1));
                Assert.That(executed, Is.EqualTo(new[] { replaceFirst ? 2 : 1 }));
                Assert.That(f.EnemyState.CurrentMp, Is.Zero);
                Assert.That(player.SelectActor(f.Ally), Is.True);
                Assert.That(player.SelectSkill(f.Finisher), Is.True);
                Assert.That(player.SelectTarget(f.Enemy), Is.True);
                Assert.That(player.Confirm(), Is.True);
                while (f.Result == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(f.Result?.EndReason, Is.EqualTo(CombatEndReason.Victory));
                Assert.That(f.Entry.ActiveSession, Is.Null);
                Assert.That(f.Entry.ActiveStateMachine, Is.Null);
                Assert.That(f.Orchestrator.CurrentDeclarationToken, Is.Null);
                Assert.That(f.Controller.TryReplaceDeclaration(initial, f.Replacement), Is.False);
                player.Unbind();
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally { Application.logMessageReceived -= capture; }
        }

        private Fixture Create(bool? enabled = true, bool cue = false, int enemyCount = 1,
            bool startController = true, bool production = false)
        {
            SkillDefinitionSO original = Skill(1, 1, production ? 2 : 1, Status("feint.old"));
            SkillDefinitionSO replacement = Skill(2, 3, 2, Status("feint.new"));
            SkillDefinitionSO invalidTarget = Skill(3, 1, 1);
            invalidTarget.targeting = TargetingRule.SingleAlly;
            SkillDefinitionSO utility = Skill(4, 0, 3);
            utility.tag = SkillTag.Utility;
            SkillDefinitionSO response = Skill(10, 0, 1);
            SetField(response, "clashPower", 0);
            SkillDefinitionSO finisher = Skill(11, 100, 0);
            SkillBook book = new();
            foreach (SkillDefinitionSO definition in new[] { original, replacement, invalidTarget, utility, response, finisher })
                book.Register(new SoSkill(definition));

            FinalCombatEnemyBehaviorProfileSO profile = null;
            if (enabled.HasValue)
            {
                profile = Track(ScriptableObject.CreateInstance<FinalCombatEnemyBehaviorProfileSO>());
                SetField(profile, "declarationFeintEnabled", enabled.Value);
                SetField(profile, "feintCueEnabled", cue);
            }
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Enemies, 10, 3, null,
                CombatFlowMode.StandoffClashChain, new CombatRuntimeConfig(3, 3, 100, 0, 0f, 10f, 0.1f));
            request.AllyFieldObjects.Add(Actor("Feint.Ally", new[] { 10, 11 }));
            for (int i = 0; i < enemyCount; i++)
            {
                GameObject enemy = Actor("Feint.Enemy" + i, new[] { 1, 2, 3, 4 });
                EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
                SetField(definition, "persistentKey", "test.feint." + i);
                SetField(definition, "finalCombatBehaviorProfile", profile);
                SetField(enemy.AddComponent<EnemySourceComponent>(), "definition", definition);
                request.EnemyFieldObjects.Add(enemy);
            }
            CombatFlowOrchestrator orchestrator = Track(new GameObject("04B.Orchestrator")).AddComponent<CombatFlowOrchestrator>();
            Fixture f = new() { Orchestrator = orchestrator, Profile = profile };
            if (production)
            {
                Assert.That(GameStateMachine.Instance, Is.Not.Null);
                Assert.That(GameFlowController.Instance, Is.Not.Null);
                GameFlowController.Instance.EnterExploration();
                CombatDirector director = Track(new GameObject("04B.Director")).AddComponent<CombatDirector>();
                CombatEntryPoint entry = Track(new GameObject("04B.Entry")).AddComponent<CombatEntryPoint>();
                FinalCombatRuntimeConfigSO config = Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>());
                SetField(config, "maxMp", 2); SetField(config, "initialMp", 2);
                SetField(config, "mpRecoveryPerSecond", 0f); SetField(config, "pressurePerSecond", 0.1f);
                SetField(config, "maxPosture", 100);
                SetField(entry, "flowOrchestrator", orchestrator); SetField(entry, "director", director);
                SetField(entry, "finalExchangeRuntimeConfig", config); SetField(entry, "_book", book);
                SetField(orchestrator, "entryPoint", entry); SetField(director, "entryPoint", entry);
                f.Entry = entry; f.Director = director;
                entry.OnCombatEnded += result => f.Result = result;
                Assert.That(entry.StartCombat(request), Is.True);
                f.Session = entry.ActiveSession; f.State = entry.ActiveStateMachine;
                f.Controller = (FinalCombatEnemyCommandController)GetField(entry, "_finalEnemyCommandController");
            }
            else
            {
                EnemyCombatProvenanceBridge.PrepareRequest(request, null);
                (f.Session, f.State) = CombatBootstrapper.StartCombat(request, book, new FieldCombatantFactory(book));
                Assert.That(orchestrator.BindFinalExchange(f.Session, f.State, new ZeroRandom(), new EnemyWins()), Is.True);
                f.Controller = new FinalCombatEnemyCommandController();
                if (startController) Assert.That(f.Controller.Bind(orchestrator, f.Session), Is.True);
            }
            f.Original = book.Get(new SkillId(1)); f.Replacement = book.Get(new SkillId(2));
            f.ResponseSkill = book.Get(new SkillId(10)); f.Finisher = book.Get(new SkillId(11));
            return f;
        }

        private GameObject Actor(string name, int[] skills)
        {
            GameObject actor = Track(new GameObject(name));
            CombatHpComponent hp = actor.AddComponent<CombatHpComponent>(); hp.MaxHP = 10; hp.HP = 10;
            SetField(actor.AddComponent<CombatSkillLoadoutComponent>(), "skillIds", skills);
            return actor;
        }

        private SkillDefinitionSO Skill(int id, int damage, int mpCost, CombatStatusDefinitionSO status = null)
        {
            SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            skill.skillId = id; skill.displayName = "Test attack " + id; skill.baseDamage = damage;
            skill.baseStagger = 0; skill.actionDelayAfterMove = 0f;
            SetField(skill, "finalMpCost", mpCost); SetField(skill, "clashPower", 100);
            if (status != null) SetField(skill, "appliedStatusEffects", new[] { status });
            return skill;
        }

        private CombatStatusDefinitionSO Status(string id)
        {
            CombatStatusDefinitionSO status = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            SetField(status, "statusId", id); SetField(status, "stackPolicy", CombatStatusStackPolicy.Stack);
            SetField(status, "maximumStacks", 10);
            return status;
        }

        private T Track<T>(T value) where T : UnityEngine.Object { _created.Add(value); return value; }
        private static object GetField(object value, string name) =>
            value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object data) =>
            value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);

        private sealed class Fixture
        {
            public CombatSession Session;
            public CombatStateMachine State;
            public CombatFlowOrchestrator Orchestrator;
            public FinalCombatEnemyCommandController Controller;
            public FinalCombatEnemyBehaviorProfileSO Profile;
            public CombatEntryPoint Entry;
            public CombatDirector Director;
            public CombatResult Result;
            public ISkill Original, Replacement, ResponseSkill, Finisher;
            public CombatExchangeState Exchange => Session.ExchangeState;
            public CombatDeclarationToken Token => Orchestrator.CurrentDeclarationToken.Value;
            public ICombatant Enemy => Session.Enemies[0];
            public ICombatant Ally => Session.Allies[0];
            public CombatantCombatState EnemyState => Session.GetCombatState(Enemy);
            public CombatantCombatState AllyState => Session.GetCombatState(Ally);
        }

        private sealed class ZeroRandom : ICombatRuleRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => 0;
        }

        private sealed class EnemyWins : ICombatClashSideInputProvider
        {
            public bool TryCreate(ICombatant actor, ISkill skill, bool owner, out CombatClashSideInput input)
            {
                input = new CombatClashSideInput(actor.Id, skill.Id, actor.Side == Side.Enemies ? 100 : 0, 1, owner);
                return true;
            }
        }
    }
}
#endif
