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
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Reward;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Combat
{
    // All content and scene objects are transient. Never opens or saves a Production scene.
    public sealed class CombatAllOutRecoveryPlayModeTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [UnityTest]
        public IEnumerator AcceptSurvive_ActualEntryDirectorBinderThenOrdinaryVictory()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunScenario("survive");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AcceptLethal_ActualCompletionAndParticipantExperienceOnce()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunScenario("lethal");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DeclineThenTimeout_ActualRecoveryAndDirectorCancellation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunScenario("timeout");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PauseThenAbort_ActualChoiceAndTimeOwnershipCleanup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunScenario("abort");
            yield return new ExitPlayMode();
        }

        private IEnumerator RunScenario(string scenario)
        {
            Assert.That(Application.isPlaying, Is.True);
            float oldScale = Time.timeScale;
            float oldFixed = Time.fixedDeltaTime;
            List<string> errors = new();
            Application.LogCallback capture = (message, stack, kind) =>
            {
                if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                    errors.Add(message + "\n" + stack);
            };
            Application.logMessageReceived += capture;
            try
            {
                Time.timeScale = 0.75f;
                Fixture f = Create(scenario == "lethal" ? 100 : 4);
                yield return OpenCandidate(f);
                Assert.That(f.Enemy.IsStunned, Is.True);
                Assert.That(f.Session.GetCombatState(f.Enemy).CurrentPosture, Is.EqualTo(1));
                Assert.That(Time.timeScale, Is.EqualTo(0.25f));
                CombatExchangeDecisionRequest stale = f.Orchestrator.PendingDecisionRequest;
                Assert.That(f.Binder.ViewState.CanAllOut, Is.True);
                StringAssert.Contains("총공격 수락", f.Context.text);

                if (scenario == "timeout")
                {
                    Click(f, 1);
                    Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
                    Assert.That(f.Enemy.HP, Is.EqualTo(19));
                    Assert.That(f.Enemy.IsStunned, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(0.75f));
                    yield return OpenCandidate(f);
                    f.Director.enabled = false;
                    Assert.That(Time.timeScale, Is.EqualTo(0.75f));
                    f.Orchestrator.Tick(0f, f.Orchestrator.AllOutDecisionTimeRemaining + 1f);
                    Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
                    Assert.That(f.Enemy.HP, Is.EqualTo(18));
                    Assert.That(f.Enemy.IsStunned, Is.False);
                    Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.Zero);
                    f.Orchestrator.TerminateExplicit(CombatEndReason.Abort, f.Session.ExchangeState.Version);
                }
                else if (scenario == "abort")
                {
                    float remaining = f.Orchestrator.AllOutDecisionTimeRemaining;
                    GameFlowController.Instance.Pause();
                    f.Orchestrator.Tick(2f, 100f);
                    Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.EqualTo(remaining));
                    Assert.That(f.Player.ConfirmAllOut(stale), Is.False);
                    Assert.That(f.Binder.ViewState.DecisionKind, Is.Null);
                    GameFlowController.Instance.ResumePreviousState();
                    Assert.That(f.Binder.ViewState.CanAllOut, Is.True);
                    Assert.That(f.Orchestrator.TerminateExplicit(CombatEndReason.Abort, stale.ExchangeVersion), Is.True);
                    Assert.That(Time.timeScale, Is.EqualTo(0.75f));
                }
                else
                {
                    Button acceptedButton = f.Options.GetComponentsInChildren<Button>()[0];
                    acceptedButton.onClick.Invoke();
                    acceptedButton.onClick.Invoke();
                    Assert.That(f.AllOutCues, Is.EqualTo(1));
                    bool applied = f.Session.GetCombatState(f.Enemy).TryGetStatus("06A.status", out CombatStatusRuntime status);
                    Assert.That(applied, Is.EqualTo(scenario == "survive"));
                    if (applied) Assert.That(status.StackCount, Is.EqualTo(1));
                    if (scenario == "survive")
                    {
                        Assert.That(f.Enemy.HP, Is.EqualTo(15));
                        Assert.That(f.Enemy.IsStunned, Is.False);
                        Assert.That(f.Session.GetCombatState(f.Enemy).CurrentPosture, Is.Zero);
                        Assert.That(f.State.Phase, Is.EqualTo(Phase.Standoff));
                        Assert.That(f.Player.SelectActor(f.Ally), Is.True);
                        Assert.That(f.Player.SelectSkill(f.Finisher), Is.True);
                        Assert.That(f.Player.SelectTarget(f.Enemy), Is.True);
                        Assert.That(f.Player.Confirm(), Is.True);
                    }
                    else Assert.That(f.Enemy.IsStunned, Is.True, "Terminal must not recover stun");
                }

                float deadline = Time.realtimeSinceStartup + 10f;
                while (f.Result == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(f.Result, Is.Not.Null);
                Assert.That(f.Result.EndReason, Is.EqualTo(scenario == "abort" || scenario == "timeout"
                    ? CombatEndReason.Abort : CombatEndReason.Victory));
                Assert.That(f.Completions, Is.EqualTo(1));
                Assert.That(f.Entry.ActiveSession, Is.Null);
                Assert.That(f.Orchestrator.PendingDecisionRequest, Is.Null);
                Assert.That(f.Orchestrator.AllOutDecisionTimeRemaining, Is.Zero);
                Assert.That(f.Orchestrator.ExecuteAllOut(stale), Is.False);
                Assert.That(f.Orchestrator.DeclineAllOut(stale), Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.75f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(oldFixed));
                if (f.Result.IsWin) VerifyRewardOnce(f.Result);
                f.Player.Unbind();
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally
            {
                Application.logMessageReceived -= capture;
                Time.timeScale = oldScale;
                Time.fixedDeltaTime = oldFixed;
                GameFlowController.Instance?.EnterExploration();
            }
        }

        private IEnumerator OpenCandidate(Fixture f)
        {
            Assert.That(f.Player.SelectActor(f.Ally), Is.True);
            Assert.That(f.Player.SelectSkill(f.Attack), Is.True);
            Assert.That(f.Player.SelectTarget(f.Enemy), Is.True);
            Assert.That(f.Player.Confirm(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!f.Orchestrator.IsAllOutSelection && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(f.Orchestrator.IsAllOutSelection, Is.True);
            Assert.That(f.State.Phase, Is.EqualTo(Phase.ChainDecision));
        }

        private static void Click(Fixture f, int index)
        {
            Button[] buttons = f.Options.GetComponentsInChildren<Button>();
            Assert.That(buttons, Has.Length.EqualTo(2));
            buttons[index].onClick.Invoke();
        }

        private Fixture Create(int allOutDamage)
        {
            Assert.That(GameStateMachine.Instance, Is.Not.Null);
            Assert.That(GameFlowController.Instance, Is.Not.Null);
            GameFlowController.Instance.EnterExploration();
            SkillDefinitionSO attack = Skill(1, 1, 100);
            SkillDefinitionSO allOut = Skill(2, allOutDamage, 100);
            allOut.targeting = TargetingRule.AllEnemies;
            CombatStatusDefinitionSO status = Track(ScriptableObject.CreateInstance<CombatStatusDefinitionSO>());
            Set(status, "statusId", "06A.status"); Set(status, "stackPolicy", CombatStatusStackPolicy.Stack);
            Set(status, "maximumStacks", 10); Set(allOut, "appliedStatusEffects", new[] { status });
            SkillDefinitionSO enemyAttack = Skill(3, 1, 0);
            SkillDefinitionSO finisher = Skill(4, 100, 100);
            SkillBook book = new();
            foreach (SkillDefinitionSO definition in new[] { attack, allOut, enemyAttack, finisher })
                book.Register(new SoSkill(definition));

            Fixture f = new();
            f.Orchestrator = Object("06A.Orchestrator").AddComponent<CombatFlowOrchestrator>();
            f.Director = Object("06A.Director").AddComponent<CombatDirector>();
            f.Entry = Object("06A.Entry").AddComponent<CombatEntryPoint>();
            FinalCombatRuntimeConfigSO config = Track(ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>());
            Set(config, "maxMp", 10); Set(config, "initialMp", 10); Set(config, "maxPosture", 1);
            Set(config, "mpRecoveryPerSecond", 0f); Set(config, "pressurePerSecond", 0.01f);
            Set(f.Entry, "flowOrchestrator", f.Orchestrator); Set(f.Entry, "director", f.Director);
            Set(f.Entry, "finalExchangeRuntimeConfig", config); Set(f.Entry, "_book", book);
            Set(f.Orchestrator, "entryPoint", f.Entry); Set(f.Director, "entryPoint", f.Entry);
            f.Entry.OnCombatEnded += result => { f.Completions++; f.Result = result; };
            f.Director.FinalPresentationCueRaised += cue =>
            {
                if (cue.Kind == CombatFinalPresentationCueKind.AllOut) f.AllOutCues++;
            };
            CombatStartRequest request = new(StartReason.PlayerFirstHit, Side.Allies, 10, 0, null,
                CombatFlowMode.StandoffClashChain, new CombatRuntimeConfig(10, 10, 1, 0));
            request.AllyFieldObjects.Add(Actor("06A.Ally", new[] { 1, 2, 4 }));
            request.EnemyFieldObjects.Add(Actor("06A.Enemy", new[] { 3 }));
            Assert.That(f.Entry.StartCombat(request), Is.True);
            f.Session = f.Entry.ActiveSession; f.State = f.Entry.ActiveStateMachine;
            f.Ally = f.Session.Allies[0]; f.Enemy = f.Session.Enemies[0];
            typeof(CombatSession).GetMethod("RegisterAllyCharacter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(f.Session, new object[] { f.Ally, "06A.Hero" });
            f.Attack = book.Get(new SkillId(1)); f.Finisher = book.Get(new SkillId(4));
            f.Player = new FinalCombatPlayerCommandController();
            Assert.That(f.Player.Bind(f.Orchestrator, f.Session), Is.True);
            f.Binder = Object("06A.Binder").AddComponent<FinalCombatUIBinder>();
            f.Options = Object("06A.Options");
            f.Context = Object("06A.Context").AddComponent<TextMeshProUGUI>();
            Set(f.Binder, "handoffListRoot", f.Options); Set(f.Binder, "contextText", f.Context);
            Set(f.Binder, "optionButtonPrefab", Object("06A.OptionTemplate").AddComponent<Button>());
            f.Binder.Bind(f.Session, f.State, f.Orchestrator);
            return f;
        }

        private void VerifyRewardOnce(CombatResult result)
        {
            CharacterProgressionService progression = Object("06A.Progression").AddComponent<CharacterProgressionService>();
            CharacterProgressionDefinitionSO definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            Set(definition, "characterId", "06A.Hero"); Set(definition, "startingLevel", 1);
            Set(definition, "maximumLevel", 3); Set(definition, "experienceRequiredByLevel", new[] { 1000, 1000 });
            typeof(CharacterProgressionService).GetMethod("ConfigureForTests", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(progression, new object[] { "06A.Hero", new[] { definition } });
            CurrencyWallet wallet = Object("06A.Wallet").AddComponent<CurrencyWallet>();
            RewardService rewards = Object("06A.Rewards").AddComponent<RewardService>();
            Set(rewards, "currencyWallet", wallet); Set(rewards, "characterProgressionService", progression);
            Assert.That(result.AllyParticipants, Has.Count.EqualTo(1));
            Assert.That(rewards.GrantCombatResult(result).DuplicateBlocked, Is.False);
            Assert.That(rewards.GrantCombatResult(result).DuplicateBlocked, Is.True);
            Assert.That(rewards.CombatGrantLedgerCount, Is.EqualTo(1));
            Assert.That(wallet.Gold, Is.EqualTo(result.TotalGold));
            Assert.That(progression.TryGetState("06A.Hero", out _, out int experience), Is.True);
            Assert.That(experience, Is.EqualTo(result.TotalExp));
        }

        private GameObject Actor(string name, int[] skills)
        {
            GameObject actor = Object(name);
            CombatHpComponent hp = actor.AddComponent<CombatHpComponent>(); hp.MaxHP = 20; hp.HP = 20;
            Set(actor.AddComponent<CombatSkillLoadoutComponent>(), "skillIds", skills);
            return actor;
        }

        private SkillDefinitionSO Skill(int id, int damage, int clashPower)
        {
            SkillDefinitionSO skill = Track(ScriptableObject.CreateInstance<SkillDefinitionSO>());
            skill.skillId = id; skill.displayName = "06A fixture " + id;
            skill.baseDamage = damage; skill.baseStagger = 0; skill.actionDelayAfterMove = 0f;
            Set(skill, "finalMpCost", 0); Set(skill, "clashPower", clashPower);
            return skill;
        }

        private GameObject Object(string name) => Track(new GameObject(name));
        private T Track<T>(T value) where T : UnityEngine.Object { _created.Add(value); return value; }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class Fixture
        {
            public CombatSession Session;
            public CombatStateMachine State;
            public CombatEntryPoint Entry;
            public CombatFlowOrchestrator Orchestrator;
            public CombatDirector Director;
            public FinalCombatUIBinder Binder;
            public FinalCombatPlayerCommandController Player;
            public GameObject Options;
            public TMP_Text Context;
            public ICombatant Ally, Enemy;
            public ISkill Attack, Finisher;
            public CombatResult Result;
            public int Completions, AllOutCues;
        }
    }
}
#endif
