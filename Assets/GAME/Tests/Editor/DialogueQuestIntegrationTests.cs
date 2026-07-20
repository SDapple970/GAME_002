using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.DemoMission.Data;
using Game.DemoMission.Runtime;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Data;
using Game.Story.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public sealed class DialogueQuestIntegrationTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            DestroyRuntimeObjects();
            ResetStaticRegistry(typeof(StoryEventRunner), "ResetActiveOwnershipForTests");
            ResetStaticRegistry(typeof(QuestObjectiveTracker), "ResetOwnershipForTests");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            DestroyRuntimeObjects();
            ResetStaticRegistry(typeof(StoryEventRunner), "ResetActiveOwnershipForTests");
            ResetStaticRegistry(typeof(QuestObjectiveTracker), "ResetOwnershipForTests");
        }

        [Test]
        public void OneRunner_StartsOneEvent_AndSecondEventIsRejected()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            StoryEventDefinitionSO first = CreateStoryEvent("first", endOnStart: true);
            StoryEventDefinitionSO second = CreateStoryEvent("second", endOnStart: true);

            runner.StartEvent(first);
            runner.StartEvent(second);

            Assert.That(runner.IsRunning, Is.True);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Dialogue));
        }

        [Test]
        public void DuplicateRunners_CannotOwnProductionRunTogether()
        {
            CreateFlowState();
            StoryEventRunner first = CreateComponent<StoryEventRunner>("FirstRunner");
            StoryEventRunner second = CreateComponent<StoryEventRunner>("SecondRunner");
            first.StartEvent(CreateStoryEvent("first", true));
            second.StartEvent(CreateStoryEvent("second", true));

            Assert.That(first.IsRunning, Is.True);
            Assert.That(second.IsRunning, Is.False);
        }

        [Test]
        public void EndEvent_IsIdempotent_AndCompletionRaisesOnce()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            StoryEventDefinitionSO definition = CreateStoryEvent("event", true);
            int completed = 0;
            runner.OnEventCompleted += _ => completed++;

            runner.StartEvent(definition);
            runner.EndEvent();
            runner.EndEvent();

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisableOrDestroy_ReleasesActiveOwnership(bool destroy)
        {
            CreateFlowState();
            StoryEventRunner first = CreateComponent<StoryEventRunner>("FirstRunner");
            first.StartEvent(CreateStoryEvent("first", true));
            if (destroy)
            {
                InvokeIfPresent(first, "OnDisable");
                InvokeIfPresent(first, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(first.gameObject);
            }
            else
            {
                InvokeIfPresent(first, "OnDisable");
                first.enabled = false;
            }

            StoryEventRunner second = CreateComponent<StoryEventRunner>("SecondRunner");
            second.StartEvent(CreateStoryEvent("second", true));
            Assert.That(second.IsRunning, Is.True);
        }

        [Test]
        public void Exploration_StartsDialogue_AndNormalEndRestoresExplorationOnce()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            int explorationTransitions = 0;
            GameStateMachine.Instance.OnStateChanged += (_, next) =>
            {
                if (next == GameState.Exploration) explorationTransitions++;
            };

            runner.StartEvent(CreateStoryEvent("event", true));
            runner.EndEvent();
            runner.EndEvent();

            Assert.That(explorationTransitions, Is.EqualTo(1));
        }

        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        [TestCase(GameState.Reward)]
        [TestCase(GameState.Loading)]
        [TestCase(GameState.Paused)]
        public void StoryStart_IsRejectedFromBlockingStates(GameState blockedState)
        {
            (GameStateMachine state, GameFlowController flow) = CreateFlowState();
            ForceState(state, blockedState);
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateStoryEvent("blocked", true));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(state.Current, Is.EqualTo(blockedState));
        }

        [Test]
        public void MissingGameFlowController_RejectsProductionStartSafely()
        {
            CreateComponent<GameStateMachine>("State");
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateStoryEvent("missing-flow", true));
            Assert.That(runner.IsRunning, Is.False);
        }

        [TestCase(GameState.Reward)]
        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        [TestCase(GameState.Loading)]
        public void Cleanup_DoesNotOverwriteHigherPriorityState(GameState stateAfterStory)
        {
            (GameStateMachine state, _) = CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateStoryEvent("event", true));
            ForceState(state, stateAfterStory);
            runner.EndEvent();
            Assert.That(state.Current, Is.EqualTo(stateAfterStory));
        }

        [Test]
        public void ChoiceNode_EntersChoice_AndAcceptedChoiceReturnsDialogue()
        {
            CreateFlowState();
            StoryChoice choice;
            StoryEventDefinitionSO definition = CreateChoiceStory("choice", out choice);
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(definition);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Choice));

            runner.SelectChoice(choice);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Dialogue));
            Assert.That(runner.IsRunning, Is.True);
        }

        [Test]
        public void AdvanceWhileWaitingForChoice_DoesNothing()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateChoiceStory("choice", out _));
            runner.Advance();
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Choice));
            Assert.That(runner.IsRunning, Is.True);
        }

        [Test]
        public void OneAdvanceAndSameFrameRace_MoveOnlyOneNode()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateThreeLineStory("lines"));
            runner.Advance();
            runner.Advance();
            StoryNode current = GetField<StoryNode>(runner, "_currentNode");
            Assert.That(current.NodeId, Is.EqualTo("second"));
        }

        [Test]
        public void ChoiceClickAndTimeoutRace_AcceptsOneOutcome()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            StoryEventDefinitionSO definition = CreateChoiceStory("choice", out StoryChoice choice);
            runner.StartEvent(definition);
            int generation = GetField<int>(runner, "_generation");
            int token = GetField<int>(runner, "_nodeToken");

            runner.SelectChoice(choice);
            Invoke(runner, "TryAcceptTimeout", generation, token);

            Assert.That(GetField<StoryNode>(runner, "_currentNode").NodeId, Is.EqualTo("chosen"));
        }

        [Test]
        public void StaleChoiceAndTimeoutCallbacks_AreIgnored()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            StoryEventDefinitionSO definition = CreateChoiceStory("choice", out StoryChoice choice);
            runner.StartEvent(definition);
            int generation = GetField<int>(runner, "_generation");
            int token = GetField<int>(runner, "_nodeToken");
            runner.SelectChoice(choice);

            Invoke(runner, "TryAcceptChoice", choice, generation, token);
            Invoke(runner, "TryAcceptTimeout", generation, token);
            Assert.That(GetField<StoryNode>(runner, "_currentNode").NodeId, Is.EqualTo("chosen"));
        }

        [Test]
        public void StoryCompletionFlag_IsWrittenOnce()
        {
            CreateFlowState();
            StoryProgressManager progress = CreateComponent<StoryProgressManager>("StoryProgress");
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateStoryEvent("one-shot", true));
            runner.EndEvent();
            runner.EndEvent();
            Assert.That(progress.IsEventCompleted("one-shot"), Is.True);
        }

        [Test]
        public void MissingPresenter_IsSafe()
        {
            CreateFlowState();
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            runner.StartEvent(CreateStoryEvent("no-presenter", true));
            Assert.That(runner.IsRunning, Is.True);
            runner.EndEvent();
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void AuthoredStoryEffect_PublishesOneQuestEventWithRuntimeIdentity()
        {
            CreateFlowState();
            StoryEffect effect = CreateQuestStoryEffect("quest", "talk", QuestEventType.Talk, 1);
            StoryEventDefinitionSO definition = CreateStoryEvent("story", true, effect);
            StoryEventRunner runner = CreateComponent<StoryEventRunner>("Runner");
            int count = 0;
            QuestEvent received = default;
            void Handler(QuestEvent questEvent) { count++; received = questEvent; }
            QuestEventChannel.OnEventRaised += Handler;
            try
            {
                runner.StartEvent(definition);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(received.QuestId, Is.EqualTo("quest"));
                Assert.That(received.ObjectiveId, Is.EqualTo("talk"));
                Assert.That(received.EventId, Does.Contain("story:story:run:"));
            }
            finally
            {
                QuestEventChannel.OnEventRaised -= Handler;
            }
        }

        [Test]
        public void StartQuest_ActivatesOnce_AndCompletedQuestDoesNotRestart()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestDefinitionSO definition = CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 1);
            int started = 0;
            runtime.OnQuestStarted += _ => started++;
            runtime.StartQuest(definition);
            runtime.StartQuest(definition);
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill"));
            runtime.StartQuest(definition);

            Assert.That(started, Is.EqualTo(1));
            Assert.That(runtime.GetQuestStatus("quest"), Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void RegisteredDefinition_RemainsInactiveUntilExplicitStart()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestDefinitionSO definition = CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 1);
            SetField(runtime, "questDefinitions", new[] { definition });
            Invoke(runtime, "RegisterSerializedDefinitions");

            Assert.That(runtime.GetQuestStatus("quest"), Is.EqualTo(QuestStatus.Inactive));
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill")), Is.False);
        }

        [Test]
        public void ExplicitReset_AllowsCompletedQuestToProgressAgain_AndClearsEventLedger()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestDefinitionSO definition = CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 1);
            runtime.StartQuest(definition);
            QuestEvent questEvent = new(QuestEventType.Kill, "quest", "kill", 1, null, "event-1");
            runtime.ApplyEvent(questEvent);
            runtime.ResetQuestProgress("quest");

            Assert.That(runtime.IsQuestActive("quest"), Is.True);
            Assert.That(runtime.ApplyEvent(questEvent), Is.True);
        }

        [Test]
        public void ConfigureCompatibilityQuest_ActivatesCanonicalRuntime()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.ConfigureCompatibilityQuest("demo", 1, false, false);
            Assert.That(runtime.IsQuestActive("demo"), Is.True);
        }

        [Test]
        public void RestorePolicy_ActivatesIncompleteAndKeepsCompletedWithoutEvents()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            GameSaveData save = new();
            save.quest = new QuestSaveData();
            save.quest.quests.Add(new QuestStateSaveData { questId = "active", completed = false });
            save.quest.quests.Add(new QuestStateSaveData { questId = "done", completed = true });
            int completions = 0;
            runtime.OnQuestCompleted += _ => completions++;
            runtime.RestoreSaveData(save);

            Assert.That(runtime.GetQuestStatus("active"), Is.EqualTo(QuestStatus.Active));
            Assert.That(runtime.GetQuestStatus("done"), Is.EqualTo(QuestStatus.Completed));
            Assert.That(completions, Is.Zero);
        }

        [TestCase("wrong-quest", "kill", QuestEventType.Kill)]
        [TestCase("quest", "wrong-objective", QuestEventType.Kill)]
        [TestCase("quest", "kill", QuestEventType.Talk)]
        public void WrongQuestObjectiveOrType_IsIgnored(string questId, string objectiveId, QuestEventType type)
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2));
            Assert.That(runtime.ApplyEvent(new QuestEvent(type, questId, objectiveId)), Is.False);
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ZeroOrNegativeProductionAmount_IsRejected(int amount)
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2));
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", amount)), Is.False);
        }

        [Test]
        public void ValidProgress_ClampsAndRaisesProgressAndCompletionOnce()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2));
            int progressEvents = 0;
            int completionEvents = 0;
            runtime.OnObjectiveProgressChanged += (_, _, _, _) => progressEvents++;
            runtime.OnQuestCompleted += _ => completionEvents++;

            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 5));
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1));

            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(2));
            Assert.That(progressEvents, Is.EqualTo(1));
            Assert.That(completionEvents, Is.EqualTo(1));
        }

        [Test]
        public void OptionalObjective_DoesNotBlockRequiredCompletion()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestObjectiveDefinition required = CreateObjective(QuestEventType.Kill, "kill", 1, false);
            QuestObjectiveDefinition optional = CreateObjective(QuestEventType.Talk, "talk", 1, true);
            QuestDefinitionSO definition = CreateQuestDefinition("quest", required, optional);
            runtime.StartQuest(definition);
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill"));
            Assert.That(runtime.IsQuestComplete("quest"), Is.True);
        }

        [Test]
        public void QuestWithNoRequiredObjectives_DoesNotCompleteAccidentally()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestObjectiveDefinition optional = CreateObjective(QuestEventType.Talk, "talk", 1, true);
            runtime.StartQuest(CreateQuestDefinition("quest", optional));
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Talk, "quest", "talk"));
            Assert.That(runtime.IsQuestComplete("quest"), Is.False);
        }

        [Test]
        public void DuplicateEventIdAppliesOnce_WhileDifferentIdsApplyIndependently()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 3));
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "a")), Is.True);
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "a")), Is.False);
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "b")), Is.True);
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(2));
        }

        [Test]
        public void LegacyEmptyEventId_PreservesCompatibilityBehavior()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 3));
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill"));
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill"));
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(2));
        }

        [Test]
        public void CompleteObjectiveAndCompleteQuest_UpdateCanonicalRuntimeOnlyOnce()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2));
            int completed = 0;
            runtime.OnQuestCompleted += _ => completed++;
            runtime.CompleteObjective("quest", "kill");
            runtime.CompleteQuest("quest");
            runtime.CompleteQuest("quest");
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void OneTrackerClaimsRuntime_AndDuplicateTrackerDoesNotDoubleApply()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 3));
            QuestObjectiveTracker first = CreateTracker(runtime, "FirstTracker");
            QuestObjectiveTracker second = CreateTracker(runtime, "SecondTracker");

            QuestEventChannel.Publish(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "one"));
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(1));

            InvokeIfPresent(first, "OnDisable");
            first.enabled = false;
            InvokeIfPresent(second, "OnDisable");
            second.enabled = false;
            second.enabled = true;
            InvokeIfPresent(second, "OnEnable");
            QuestEventChannel.Publish(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "two"));
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(2));
        }

        [Test]
        public void TrackerEnableDisable_DoesNotDuplicateSubscription()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 3));
            QuestObjectiveTracker tracker = CreateTracker(runtime, "Tracker");
            InvokeIfPresent(tracker, "OnDisable");
            tracker.enabled = false;
            tracker.enabled = true;
            InvokeIfPresent(tracker, "OnEnable");
            InvokeIfPresent(tracker, "OnDisable");
            tracker.enabled = false;
            tracker.enabled = true;
            InvokeIfPresent(tracker, "OnEnable");
            QuestEventChannel.Publish(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "event"));
            Assert.That(runtime.GetObjectiveProgress("quest", "kill"), Is.EqualTo(1));
        }

        [Test]
        public void DemoMissionCanonicalMode_UsesQuestRuntimeAndMirrorsCompletionOnce()
        {
            (DemoMissionRuntime demo, QuestRuntime runtime) = CreateCanonicalDemoMission(2, false);
            int completed = 0;
            demo.OnMissionCompleted += () => completed++;
            demo.RegisterEnemyDefeated("enemy-a");
            demo.RegisterEnemyDefeated("enemy-a");
            demo.RegisterEnemyDefeated("enemy-b");

            Assert.That(demo.EnemyDefeatCount, Is.EqualTo(2));
            Assert.That(runtime.IsQuestComplete(demo.CurrentQuestId), Is.True);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void DemoMissionRescue_PublishesOnceAndResetResetsCanonicalQuest()
        {
            (DemoMissionRuntime demo, QuestRuntime runtime) = CreateCanonicalDemoMission(0, true);
            demo.RegisterNpcRescued("rescue-one");
            demo.RegisterNpcRescued("rescue-one");
            Assert.That(demo.IsNpcRescued, Is.True);
            Assert.That(runtime.IsQuestComplete(demo.CurrentQuestId), Is.True);

            demo.ResetMissionProgress();
            Assert.That(runtime.IsQuestActive(demo.CurrentQuestId), Is.True);
            Assert.That(demo.IsNpcRescued, Is.False);
        }

        [Test]
        public void DemoMissionWithoutQuestRuntime_PreservesFallback()
        {
            DemoMissionDefinitionSO definition = CreateDemoDefinition("fallback", 1);
            GameObject go = CreateGameObject("DemoFallback");
            go.SetActive(false);
            DemoMissionRuntime demo = go.AddComponent<DemoMissionRuntime>();
            SetField(demo, "dontDestroyOnLoad", false);
            SetField(demo, "bridgeToQuestRuntime", false);
            InvokeIfPresent(demo, "Awake");
            InvokeIfPresent(demo, "OnEnable");
            demo.SetCurrentMission(definition);
            demo.RegisterEnemyDefeated();
            demo.RegisterNpcRescued();
            Assert.That(demo.IsMissionComplete(), Is.True);
        }

        [Test]
        public void QuestCompletionFlow_GrantsOnceAndCompleteQuestCompatibilityDoesNotDoubleGrant()
        {
            (GameStateMachine state, _) = CreateFlowState();
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet);
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.ConfigureCompatibilityQuest("quest", 1, false, false);
            QuestCompletionFlow flow = CreateCompletionFlow(runtime, service, 5, false);

            runtime.CompleteQuest("quest");
            flow.CompleteQuest("quest");

            Assert.That(wallet.Gold, Is.EqualTo(5));
            Assert.That(state.Current, Is.EqualTo(GameState.Exploration));
        }

        [TestCase(GameState.CombatPlanning)]
        [TestCase(GameState.CombatResolving)]
        [TestCase(GameState.Reward)]
        [TestCase(GameState.Dialogue)]
        [TestCase(GameState.Choice)]
        [TestCase(GameState.Cutscene)]
        public void QuestCompletion_IsDeferredDuringBlockingStates(GameState blockingState)
        {
            (GameStateMachine state, GameFlowController gameFlow) = CreateFlowState();
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet);
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.ConfigureCompatibilityQuest("quest", 1, false, false);
            CreateCompletionFlow(runtime, service, 5, false);
            ForceState(state, blockingState);

            runtime.CompleteQuest("quest");
            Assert.That(wallet.Gold, Is.Zero);
            ForceState(state, GameState.Exploration, raiseEvent: true);
            Assert.That(wallet.Gold, Is.EqualTo(5));
        }

        [Test]
        public void MultipleDeferredCompletions_ProcessInStableOrderOnce()
        {
            (GameStateMachine state, _) = CreateFlowState();
            CurrencyWallet wallet = CreateWallet();
            RewardService service = CreateRewardService(wallet);
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.ConfigureCompatibilityQuest("q1", 1, false, false);
            runtime.ConfigureCompatibilityQuest("q2", 1, false, false);
            CreateCompletionFlow(runtime, service, 3, false);
            ForceState(state, GameState.Dialogue);
            runtime.CompleteQuest("q1");
            runtime.CompleteQuest("q2");
            ForceState(state, GameState.Exploration, true);
            Assert.That(wallet.Gold, Is.EqualTo(6));
        }

        [TestCase(false, GameState.Exploration)]
        [TestCase(true, GameState.Reward)]
        public void EnterRewardPolicy_IsRespectedOnlyFromSafeState(bool enterReward, GameState expected)
        {
            (GameStateMachine state, _) = CreateFlowState();
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            runtime.ConfigureCompatibilityQuest("quest", 1, false, false);
            CreateCompletionFlow(runtime, null, 0, enterReward, grantReward: false);
            runtime.CompleteQuest("quest");
            Assert.That(state.Current, Is.EqualTo(expected));
        }

        [Test]
        public void QuestTrackerUI_RebuildsFromCanonicalRuntimeAndRefreshesProgress()
        {
            QuestRuntime runtime = CreateComponent<QuestRuntime>("Runtime");
            QuestDefinitionSO definition = CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2);
            SetField(definition, "questTitle", "Canonical Quest");
            GameObject hudObject = CreateGameObject("HUD");
            hudObject.SetActive(false);
            QuestTrackerUI hud = hudObject.AddComponent<QuestTrackerUI>();
            GameObject root = CreateGameObject("HUDRoot");
            Text title = CreateGameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Text objective = CreateGameObject("Objective", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            SetField(hud, "questRuntime", runtime);
            SetField(hud, "root", root);
            SetField(hud, "titleText", title);
            SetField(hud, "objectiveText", objective);
            hudObject.SetActive(true);
            InvokeIfPresent(hud, "Awake");
            InvokeIfPresent(hud, "OnEnable");

            runtime.StartQuest(definition);
            runtime.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "one"));

            Assert.That(title.text, Is.EqualTo("Canonical Quest"));
            Assert.That(objective.text, Does.Contain("1/2"));
            Assert.That(GameStateMachine.Instance == null, Is.True);
        }

        [Test]
        public void CaptureAndRestore_PreserveIdsAndProgressWithoutCompletionEmission()
        {
            QuestRuntime source = CreateComponent<QuestRuntime>("Source");
            source.StartQuest(CreateQuestDefinition("quest", QuestEventType.Kill, "kill", 2));
            source.ApplyEvent(new QuestEvent(QuestEventType.Kill, "quest", "kill", 1, null, "runtime-only-id"));
            GameSaveData save = new();
            source.CaptureSaveData(save);
            UnityEngine.Object.DestroyImmediate(source.gameObject);

            QuestRuntime restored = CreateComponent<QuestRuntime>("Restored");
            int completed = 0;
            restored.OnQuestCompleted += _ => completed++;
            restored.RestoreSaveData(save);

            Assert.That(restored.GetQuestStatus("quest"), Is.EqualTo(QuestStatus.Active));
            Assert.That(restored.GetObjectiveProgress("quest", "kill"), Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
        }

        private (GameStateMachine, GameFlowController) CreateFlowState()
        {
            GameStateMachine state = CreateComponent<GameStateMachine>("GameStateMachine");
            GameFlowController flow = CreateComponent<GameFlowController>("GameFlowController");
            return (state, flow);
        }

        private StoryEventDefinitionSO CreateStoryEvent(string id, bool endOnStart, StoryEffect effect = null)
        {
            StoryNode node = new();
            SetField(node, "nodeId", "start");
            SetField(node, "speakerName", "NPC");
            SetField(node, "body", "Line");
            SetField(node, "endEvent", endOnStart);
            if (effect != null)
                SetField(node, "effects", new List<StoryEffect> { effect });

            StoryEventDefinitionSO definition = ScriptableObject.CreateInstance<StoryEventDefinitionSO>();
            _objects.Add(definition);
            SetField(definition, "eventId", id);
            SetField(definition, "startNodeId", "start");
            SetField(definition, "nodes", new List<StoryNode> { node });
            return definition;
        }

        private StoryEventDefinitionSO CreateThreeLineStory(string id)
        {
            StoryNode first = CreateNode("start", "second", false);
            StoryNode second = CreateNode("second", "third", false);
            StoryNode third = CreateNode("third", null, true);
            return CreateStoryDefinition(id, first, second, third);
        }

        private StoryEventDefinitionSO CreateChoiceStory(string id, out StoryChoice choice)
        {
            choice = new StoryChoice();
            SetField(choice, "text", "Choose");
            SetField(choice, "nextNodeId", "chosen");
            StoryNode start = CreateNode("start", null, false);
            SetField(start, "choices", new List<StoryChoice> { choice });
            StoryNode chosen = CreateNode("chosen", null, true);
            return CreateStoryDefinition(id, start, chosen);
        }

        private StoryEventDefinitionSO CreateStoryDefinition(string id, params StoryNode[] nodes)
        {
            StoryEventDefinitionSO definition = ScriptableObject.CreateInstance<StoryEventDefinitionSO>();
            _objects.Add(definition);
            SetField(definition, "eventId", id);
            SetField(definition, "startNodeId", "start");
            SetField(definition, "nodes", new List<StoryNode>(nodes));
            return definition;
        }

        private static StoryNode CreateNode(string id, string next, bool end)
        {
            StoryNode node = new();
            SetField(node, "nodeId", id);
            SetField(node, "speakerName", "NPC");
            SetField(node, "body", id);
            SetField(node, "nextNodeId", next);
            SetField(node, "endEvent", end);
            return node;
        }

        private static StoryEffect CreateQuestStoryEffect(string questId, string objectiveId, QuestEventType type, int amount)
        {
            StoryEffect effect = new();
            SetField(effect, "type", StoryEffectType.PublishQuestEvent);
            SetField(effect, "missionId", questId);
            SetField(effect, "objectiveId", objectiveId);
            SetField(effect, "questEventType", type);
            SetField(effect, "intValue", amount);
            return effect;
        }

        private QuestDefinitionSO CreateQuestDefinition(string id, QuestEventType type, string objectiveId, int required)
        {
            return CreateQuestDefinition(id, CreateObjective(type, objectiveId, required, false));
        }

        private QuestDefinitionSO CreateQuestDefinition(string id, params QuestObjectiveDefinition[] objectives)
        {
            QuestDefinitionSO definition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            _objects.Add(definition);
            SetField(definition, "questId", id);
            SetField(definition, "questTitle", id);
            SetField(definition, "objectives", objectives);
            return definition;
        }

        private static QuestObjectiveDefinition CreateObjective(QuestEventType type, string id, int required, bool optional)
        {
            QuestObjectiveDefinition objective = new();
            SetField(objective, "eventType", type);
            SetField(objective, "objectiveId", id);
            SetField(objective, "requiredCount", required);
            SetField(objective, "optional", optional);
            SetField(objective, "description", id);
            return objective;
        }

        private QuestObjectiveTracker CreateTracker(QuestRuntime runtime, string name)
        {
            GameObject go = CreateGameObject(name);
            go.SetActive(false);
            QuestObjectiveTracker tracker = go.AddComponent<QuestObjectiveTracker>();
            SetField(tracker, "questRuntime", runtime);
            go.SetActive(true);
            InvokeIfPresent(tracker, "Awake");
            InvokeIfPresent(tracker, "OnEnable");
            return tracker;
        }

        private (DemoMissionRuntime, QuestRuntime) CreateCanonicalDemoMission(int kills, bool rescue)
        {
            GameObject go = CreateGameObject("CanonicalDemo");
            go.SetActive(false);
            QuestRuntime runtime = go.AddComponent<QuestRuntime>();
            DemoMissionRuntime demo = go.AddComponent<DemoMissionRuntime>();
            DemoMissionDefinitionSO definition = CreateDemoDefinition("demo", kills);
            SetField(demo, "dontDestroyOnLoad", false);
            SetField(demo, "currentMission", definition);
            SetField(demo, "questRuntime", runtime);
            SetField(demo, "bridgeToQuestRuntime", true);
            SetField(demo, "requireNpcTalkForQuestCompletion", false);
            SetField(demo, "requireNpcRescueForQuestCompletion", rescue);
            go.SetActive(true);
            InvokeIfPresent(runtime, "Awake");
            InvokeIfPresent(demo, "Awake");
            InvokeIfPresent(demo, "OnEnable");
            return (demo, runtime);
        }

        private DemoMissionDefinitionSO CreateDemoDefinition(string id, int kills)
        {
            DemoMissionDefinitionSO definition = ScriptableObject.CreateInstance<DemoMissionDefinitionSO>();
            definition.missionId = id;
            definition.requiredEnemyKills = kills;
            _objects.Add(definition);
            return definition;
        }

        private QuestCompletionFlow CreateCompletionFlow(
            QuestRuntime runtime,
            RewardService service,
            int fallbackGold,
            bool enterReward,
            bool grantReward = true)
        {
            GameObject go = CreateGameObject("QuestCompletionFlow");
            go.SetActive(false);
            QuestCompletionFlow flow = go.AddComponent<QuestCompletionFlow>();
            SetField(flow, "questRuntime", runtime);
            SetField(flow, "rewardService", service);
            SetField(flow, "fallbackRewardGold", fallbackGold);
            SetField(flow, "enterRewardStateOnCompletion", enterReward);
            SetField(flow, "grantRewardOnCompletion", grantReward);
            go.SetActive(true);
            InvokeIfPresent(flow, "Awake");
            InvokeIfPresent(flow, "OnEnable");
            return flow;
        }

        private CurrencyWallet CreateWallet()
        {
            CurrencyWallet wallet = CreateComponent<CurrencyWallet>("Wallet");
            wallet.SetGold(0);
            return wallet;
        }

        private RewardService CreateRewardService(CurrencyWallet wallet)
        {
            GameObject go = CreateGameObject("RewardService");
            go.SetActive(false);
            RewardService service = go.AddComponent<RewardService>();
            SetField(service, "currencyWallet", wallet);
            go.SetActive(true);
            InvokeIfPresent(service, "Awake");
            return service;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            T component = CreateGameObject(name).AddComponent<T>();
            InvokeIfPresent(component, "Awake");
            InvokeIfPresent(component, "OnEnable");
            return component;
        }

        private GameObject CreateGameObject(string name, params Type[] components)
        {
            GameObject go = components.Length == 0 ? new GameObject(name) : new GameObject(name, components);
            _objects.Add(go);
            return go;
        }

        private static void ForceState(GameStateMachine state, GameState next, bool raiseEvent = false)
        {
            GameState previous = state.Current;
            SetAutoProperty(state, "Previous", previous);
            SetAutoProperty(state, "Current", next);
            if (raiseEvent)
            {
                FieldInfo eventField = typeof(GameStateMachine).GetField("OnStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                ((Action<GameState, GameState>)eventField?.GetValue(state))?.Invoke(previous, next);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {target.GetType().Name}.{methodName}");
            method.Invoke(target, args);
        }

        private static void InvokeIfPresent(object target, string methodName)
        {
            target?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static void ResetStaticRegistry(Type type, string methodName)
        {
            type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<StoryEventRunner>();
            DestroyAll<StoryProgressManager>();
            DestroyAll<QuestObjectiveTracker>();
            DestroyAll<QuestCompletionFlow>();
            DestroyAll<QuestTrackerUI>();
            DestroyAll<DemoMissionRuntime>();
            DestroyAll<QuestRuntime>();
            DestroyAll<RewardService>();
            DestroyAll<CurrencyWallet>();
            DestroyAll<GameFlowController>();
            DestroyAll<GameStateMachine>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    InvokeIfPresent(components[i], "OnDisable");
                    InvokeIfPresent(components[i], "OnDestroy");
                    UnityEngine.Object.DestroyImmediate(components[i].gameObject);
                }
            }
        }
    }
}
