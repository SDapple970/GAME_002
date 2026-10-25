using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Integration;
using Game.Combat.Adapters;
using Game.Combat.Model;
using Game.Core;
using Game.Daily;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.UI;
using Game.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    public sealed class GeneralMissionContentTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test] public void Exploration_NinetyPercent_UniqueAuthoredCellsOnly()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Exploration, 10);
            Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission");
            Assert.That(runtime.TryExploreMissionCell("mission", run, 1, "outside"), Is.False);
            Assert.That(runtime.TryExploreMissionCell("mission", "old-run", 1, "target_0"), Is.False);
            for (int i = 0; i < 8; i++) Assert.That(runtime.TryExploreMissionCell("mission", run, 1, $"target_{i}"), Is.True);
            Assert.That(runtime.GetGeneralMissionSnapshot("mission").ExplorationPercent, Is.EqualTo(80));
            Assert.That(runtime.IsQuestComplete("mission"), Is.False);
            Assert.That(runtime.TryExploreMissionCell("mission", run, 1, "target_0"), Is.False);
            Assert.That(runtime.TryExploreMissionCell("mission", run, 1, "target_8"), Is.True);
            Assert.That(runtime.IsQuestComplete("mission"), Is.True);
        }

        [Test] public void GenericEventsAndCompletionHelpers_CannotBypassMissionRules()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            runtime.CompleteQuest("mission"); runtime.CompleteObjective("mission", "objective");
            Assert.That(runtime.ApplyEvent(new QuestEvent(QuestEventType.Escort, "mission", "objective", 100, null,
                "fake", "target", runtime.GetQuestRunId("mission"))), Is.False);
            Assert.That(runtime.IsQuestActive("mission"), Is.True);
        }

        [Test] public void Extermination_AllDesignatedEncounters_NoDuplicateOrUnrelatedCredit()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Extermination, 3); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission");
            Assert.That(runtime.TryClearMissionEncounter("mission", run, 1, "target_0"), Is.True);
            Assert.That(runtime.TryClearMissionEncounter("mission", run, 1, "target_0"), Is.False);
            Assert.That(runtime.TryClearMissionEncounter("mission", run, 1, "other"), Is.False);
            Assert.That(runtime.TryClearMissionEncounter("mission", run, 1, "target_1"), Is.True);
            Assert.That(runtime.IsQuestComplete("mission"), Is.False);
            Assert.That(runtime.TryClearMissionEncounter("mission", run, 1, "target_2"), Is.True);
            Assert.That(runtime.IsQuestComplete("mission"), Is.True);
        }

        [Test] public void Suppressors_SelectThreeDistinct_PersistSelectionAndActivation()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Suppressors, 12); Accept(runtime, definition);
            GeneralMissionSnapshot state = runtime.GetGeneralMissionSnapshot("mission");
            Assert.That(state.SelectedTargetIds.Count, Is.EqualTo(3)); Assert.That(state.SelectedTargetIds.Distinct().Count(), Is.EqualTo(3));
            string unselected = definition.GeneralMission.TargetIds.First(id => !state.SelectedTargetIds.Contains(id));
            Assert.That(runtime.TryActivateMissionSuppressor("mission", state.RunId, 1, unselected), Is.False);
            runtime.TryActivateMissionSuppressor("mission", state.RunId, 1, state.SelectedTargetIds[0]);
            GameSaveData saved = RoundTrip(runtime);
            QuestRuntime restored = Runtime(definition); restored.RestoreSaveData(saved);
            GeneralMissionSnapshot loaded = restored.GetGeneralMissionSnapshot("mission");
            Assert.That(loaded.SelectedTargetIds, Is.EqualTo(state.SelectedTargetIds));
            Assert.That(restored.TryActivateMissionSuppressor("mission", loaded.RunId, 1, state.SelectedTargetIds[0]), Is.False);
            restored.TryActivateMissionSuppressor("mission", loaded.RunId, 1, state.SelectedTargetIds[1]);
            restored.TryActivateMissionSuppressor("mission", loaded.RunId, 1, state.SelectedTargetIds[2]);
            Assert.That(restored.IsQuestComplete("mission"), Is.True);
        }

        [Test] public void Rescue_RequiresInteractionCorrectExitAndEightyPercentHp()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission");
            Assert.That(runtime.TryExitRescueMission("mission", run, 1, "exit"), Is.False);
            Assert.That(runtime.TryRescueMissionTarget("mission", run, 1, "wrong"), Is.False);
            Assert.That(runtime.TryRescueMissionTarget("mission", run, 1, "rescue"), Is.True);
            Assert.That(runtime.TryChangeMissionRescueHp("mission", run, 1, -21, "hit"), Is.True);
            Assert.That(runtime.TryChangeMissionRescueHp("mission", run, 1, -21, "hit"), Is.False);
            Assert.That(runtime.TryExitRescueMission("mission", run, 1, "exit"), Is.False);
            runtime.TryChangeMissionRescueHp("mission", run, 1, 1, "heal");
            Assert.That(runtime.TryExitRescueMission("mission", run, 1, "wrong"), Is.False);
            Assert.That(runtime.TryExitRescueMission("mission", run, 1, "exit"), Is.True);
            Assert.That(runtime.IsQuestComplete("mission"), Is.True);
        }

        [Test] public void Rescue_DeathFails_RetryResetsHpAndRejectsOldAttempt()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission"); runtime.TryRescueMissionTarget("mission", run, 1, "rescue");
            runtime.TryChangeMissionRescueHp("mission", run, 1, -100, "death");
            Assert.That(runtime.IsQuestFailed("mission"), Is.True);
            Assert.That(runtime.RetryQuest("mission"), Is.True);
            Assert.That(runtime.GetQuestRunId("mission"), Is.EqualTo(run));
            Assert.That(runtime.GetGeneralMissionSnapshot("mission").RescueHp, Is.EqualTo(100));
            Assert.That(runtime.TryRescueMissionTarget("mission", run, 1, "rescue"), Is.False);
            Assert.That(runtime.TryRescueMissionTarget("mission", run, 2, "rescue"), Is.True);
        }

        [Test] public void Rescue_SaveLoadPreservesHpFollowingPositionAndOutcomeDedupe()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission"); runtime.TryRescueMissionTarget("mission", run, 1, "rescue");
            runtime.TryChangeMissionRescueHp("mission", run, 1, -10, "hit");
            runtime.UpdateMissionEscortPosition("mission", run, 1, new Vector3(3, 4, 0));
            QuestRuntime restored = Runtime(definition); restored.RestoreSaveData(RoundTrip(runtime));
            Assert.That(restored.GetGeneralMissionSnapshot("mission").Following, Is.True);
            Assert.That(restored.GetGeneralMissionSnapshot("mission").RescueHp, Is.EqualTo(90));
            Assert.That(restored.TryGetMissionEscortPosition("mission", out Vector3 position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector3(3, 4, 0)));
            Assert.That(restored.TryChangeMissionRescueHp("mission", run, 1, -10, "hit"), Is.False);
            Assert.That(restored.TryExitRescueMission("mission", run, 1, "exit"), Is.True);
        }

        [Test] public void MissionAcceptance_OnlyOneActive_RewardAndDayOncePerRun()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Extermination, 1);
            CurrencyWallet wallet = Component<CurrencyWallet>();
            RewardService rewards = Component<RewardService>(false); Set(rewards, "currencyWallet", wallet); Enable(rewards);
            CalendarService calendar = Component<CalendarService>();
            QuestCalendarIntegration integration = Component<QuestCalendarIntegration>(false);
            Set(integration, "questRuntime", runtime); Set(integration, "calendarService", calendar); Enable(integration);
            QuestCompletionFlow flow = Component<QuestCompletionFlow>(false);
            Set(flow, "questRuntime", runtime); Set(flow, "rewardService", rewards); Set(flow, "questCalendarIntegration", integration);
            Set(flow, "notifyDaySettlementOnQuestCompletion", false); Set(flow, "enterRewardStateOnCompletion", false); Enable(flow);
            Accept(runtime, definition);
            QuestDefinitionSO other = Definition(GeneralMissionKind.Exploration, 10, "other");
            Assert.That(runtime.TryAcceptGeneralMission(other, out _), Is.False);
            string first = runtime.GetQuestRunId("mission");
            runtime.TryClearMissionEncounter("mission", first, 1, "target_0");
            Assert.That(wallet.Gold, Is.EqualTo(10)); Assert.That(calendar.CurrentDay, Is.EqualTo(2));
            Assert.That(runtime.TryAcceptGeneralMission(definition, out _), Is.True);
            string second = runtime.GetQuestRunId("mission"); Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(runtime.TryClearMissionEncounter("mission", first, 1, "target_0"), Is.False);
            runtime.TryClearMissionEncounter("mission", second, 1, "target_0");
            Assert.That(wallet.Gold, Is.EqualTo(20)); Assert.That(calendar.CurrentDay, Is.EqualTo(3));
        }

        [Test] public void EncounterAdapter_UsesRunScopeAndRestoresOnlyItsGroup()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Extermination, 1);
            GameObject go = Object("Encounter", false);
            CombatEncounterGroup group = go.AddComponent<CombatEncounterGroup>(); Set(group, "encounterId", "target_0");
            GeneralMissionCombatEncounterAdapter adapter = go.AddComponent<GeneralMissionCombatEncounterAdapter>();
            Set(adapter, "questRuntime", runtime); Set(adapter, "questId", "mission"); go.SetActive(true); Call(adapter, "OnEnable");
            Accept(runtime, definition);
            Assert.That(Lifecycle(group), Is.EqualTo("Idle"));
            Call(group, "AdoptAcceptedSession", "combat_1");
            CombatResult result = new() { IsWin = true, EndReason = CombatEndReason.Victory };
            typeof(CombatResult).GetProperty("CompletionId").SetValue(result, "combat_1");
            Call(group, "TryBeginOutcome", result); Call(group, "CompleteOutcome", result, false);
            Assert.That(runtime.IsQuestComplete("mission"), Is.True);
            GameSaveData save = new(); runtime.CaptureSaveData(save); group.CaptureSaveData(save);
            Assert.That(save.world.clearedEncounterIds, Is.Empty);
            save.world.clearedEncounterIds.Add("unrelated");
            runtime.RestoreSaveData(save); group.RestoreSaveData(save); adapter.RestoreSaveData(save);
            Assert.That(Lifecycle(group), Is.EqualTo("Cleared"));
            Assert.That(save.world.clearedEncounterIds, Is.EqualTo(new[] { "unrelated" }));
        }

        [Test] public void ExistingInteractionController_CanUseSelectedSuppressor()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Suppressors, 5); Accept(runtime, definition);
            GeneralMissionSuppressor device = Component<GeneralMissionSuppressor>(false);
            Set(device, "questRuntime", runtime); Set(device, "questId", "mission");
            Set(device, "deviceId", runtime.GetGeneralMissionSnapshot("mission").SelectedTargetIds[0]); Enable(device);
            InteractionController controller = Component<InteractionController>();
            controller.Register((IInteractable)device);
            Assert.That(controller.TryInteractCurrent(), Is.True);
            Assert.That(controller.TryInteractCurrent(), Is.False);
            Assert.That(runtime.GetObjectiveProgress("mission", "objective"), Is.EqualTo(1));
        }

        [Test] public void AuthoredStarterAssets_AreValidAndNoScenePlacementIsRequired()
        {
            string[] names = { "Explore", "Clear", "Suppress", "Rescue" };
            foreach (string name in names)
            {
                QuestDefinitionSO quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>($"Assets/GAME/Data/GeneralMissions/CH01_General_{name}.asset");
                Assert.That(quest, Is.Not.Null);
                Assert.That(quest.GeneralMission.TryValidate(quest, out string reason), Is.True, reason);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GAME/Data/GeneralMissions/GeneralMissionBoard.prefab"), Is.Not.Null);
            GameObject board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GAME/Data/GeneralMissions/GeneralMissionBoard.prefab");
            Assert.That(board.GetComponentsInChildren<TMPro.TMP_Text>(true).All(text => text.font != null), Is.True);
        }

        [Test] public void InvalidAuthoring_DuplicateTargetsOrMismatchedObjectiveIsRejected()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Extermination, 2);
            Set(definition.GeneralMission, "targetIds", new[] { "same", "same" });
            Assert.That(runtime.TryAcceptGeneralMission(definition, out string reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Set(definition.GeneralMission, "targetIds", new[] { "one", "two" });
            Set(definition.Objectives[0], "requiredCount", 3);
            Assert.That(runtime.TryAcceptGeneralMission(definition, out _), Is.False);
        }

        [Test] public void ScopedEncounter_ReacceptRestoresMembersHpPositionAndAvailability()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Extermination, 1);
            GameObject go = Object("Encounter", false);
            GameObject enemy = Object("Enemy"); enemy.transform.SetParent(go.transform); enemy.transform.position = new Vector3(3, 2, 0);
            CombatHpComponent hp = enemy.AddComponent<CombatHpComponent>();
            CombatEncounterGroup group = go.AddComponent<CombatEncounterGroup>(); Set(group, "encounterId", "target_0");
            GeneralMissionCombatEncounterAdapter adapter = go.AddComponent<GeneralMissionCombatEncounterAdapter>();
            Set(adapter, "questRuntime", runtime); Set(adapter, "questId", "mission"); go.SetActive(true); Call(adapter, "OnEnable");
            Accept(runtime, definition); Assert.That(enemy.activeSelf, Is.True);
            Call(group, "AdoptAcceptedSession", "combat"); hp.HP = 0; enemy.transform.position = new Vector3(20, 20, 0); enemy.SetActive(false);
            CombatResult result = new() { IsWin = true, EndReason = CombatEndReason.Victory };
            typeof(CombatResult).GetProperty("CompletionId").SetValue(result, "combat");
            Call(group, "TryBeginOutcome", result); Call(group, "CompleteOutcome", result, false);
            Call(runtime, "AcknowledgeRunCompletion", runtime.GetPendingRunCompletions()[0]);
            Assert.That(runtime.TryStartNextRun("mission"), Is.True);
            Assert.That(hp.HP, Is.EqualTo(10)); Assert.That(enemy.activeSelf, Is.True);
            Assert.That(enemy.transform.position, Is.EqualTo(new Vector3(3, 2, 0)));
            Assert.That(Lifecycle(group), Is.EqualTo("Idle"));
        }

        [Test] public void Suppressors_RetryKeepsSelectionButClearsActivationAndRejectsOldAttempt()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Suppressors, 8); Accept(runtime, definition);
            GeneralMissionSnapshot first = runtime.GetGeneralMissionSnapshot("mission");
            runtime.TryActivateMissionSuppressor("mission", first.RunId, 1, first.SelectedTargetIds[0]);
            runtime.FailQuest("mission", new Game.Common.Identity.GameplayOutcomeIdentity(
                Game.Common.Identity.GameplayOutcomeSourceType.QuestCompletion, "failure", "test"), "failure", first.RunId);
            Assert.That(runtime.RetryQuest("mission"), Is.True);
            GeneralMissionSnapshot retry = runtime.GetGeneralMissionSnapshot("mission");
            Assert.That(retry.SelectedTargetIds, Is.EqualTo(first.SelectedTargetIds)); Assert.That(retry.CompletedTargetIds, Is.Empty);
            Assert.That(runtime.TryActivateMissionSuppressor("mission", first.RunId, 1, first.SelectedTargetIds[0]), Is.False);
            Assert.That(runtime.TryActivateMissionSuppressor("mission", first.RunId, 2, first.SelectedTargetIds[0]), Is.True);
        }

        [Test] public void MissionBoard_UsesExistingStateGuardAndKoreanProgressText()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Exploration, 10);
            GeneralMissionBoard board = Component<GeneralMissionBoard>(false); Set(board, "questRuntime", runtime);
            Set(board, "missions", new[] { definition }); Enable(board);
            GameStateMachine state = Component<GameStateMachine>(); GameFlowController flow = Component<GameFlowController>();
            flow.Pause(); Assert.That(board.TryAccept(0), Is.False); flow.ResumePreviousState();
            Assert.That(board.TryAccept(0), Is.True);
            Assert.That(board.GetProgressText(0), Does.Contain("진행 중").And.Contain("0% / 90%"));
        }

        [Test] public void EscortTrail_JsonRestorePreservesWalkedPathWithoutSharingMutableLists()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission"); runtime.TryRescueMissionTarget("mission", run, 1, "rescue");
            List<Vector3> path = new() { new Vector3(1, 0, 0), new Vector3(1, 2, 0), new Vector3(3, 2, 0) };
            runtime.UpdateMissionEscortPosition("mission", run, 1, Vector3.zero, path);
            path.Clear(); Assert.That(runtime.GetMissionEscortPath("mission").Count, Is.EqualTo(3));
            QuestRuntime restored = Runtime(definition); restored.RestoreSaveData(RoundTrip(runtime));
            Assert.That(restored.GetMissionEscortPath("mission"), Is.EqualTo(runtime.GetMissionEscortPath("mission")));
        }

        [Test] public void NewGame_RemovesGeneralMissionRuntimeAndEscortState()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            string run = runtime.GetQuestRunId("mission"); runtime.TryRescueMissionTarget("mission", run, 1, "rescue");
            runtime.ResetForNewGame(); Assert.That(runtime.GetGeneralMissionSnapshot("mission"), Is.Null);
            Assert.That(runtime.GetQuestStatus("mission"), Is.EqualTo(QuestStatus.Inactive));
            Accept(runtime, definition); Assert.That(runtime.GetQuestRunId("mission"), Is.Not.EqualTo(run));
        }

        [Test] public void CanonicalSaveLoad_PreservesGeneralMissionRosterAndProgress()
        {
            var (runtime, definition) = Setup(GeneralMissionKind.Suppressors, 9); Accept(runtime, definition);
            GeneralMissionSnapshot first = runtime.GetGeneralMissionSnapshot("mission");
            runtime.TryActivateMissionSuppressor("mission", first.RunId, 1, first.SelectedTargetIds[0]);
            SaveLoadService saves = Component<SaveLoadService>();
            GameSaveData snapshot = Migrate(saves.CaptureGameSaveDataSnapshot());
            runtime.ResetForNewGame(); saves.RestoreGameSaveDataSnapshot(snapshot);
            GeneralMissionSnapshot restored = runtime.GetGeneralMissionSnapshot("mission");
            Assert.That(restored.RunId, Is.EqualTo(first.RunId));
            Assert.That(restored.SelectedTargetIds, Is.EquivalentTo(first.SelectedTargetIds));
            Assert.That(restored.CompletedTargetIds, Is.EqualTo(new[] { first.SelectedTargetIds[0] }));
            Assert.That(restored.Progress, Is.EqualTo(1));
        }

        [Test] public void Schema13Migration_PreservesExistingRunPendingResultAndDayLedger()
        {
            GameSaveData saved = new(); saved.header.schemaVersion = 13;
            saved.quest.quests.Add(new QuestStateSaveData { questId = "old", runId = "old-run", status = "Completed", completed = true, attempt = 1 });
            saved.quest.pendingRunCompletions.Add(new QuestRunCompletionSaveData { questId = "old", runId = "old-run", missionDayCost = 1 });
            saved.futureDaily.appliedQuestRunDayCostIds.Add("old-paid-run");
            GameSaveData migrated = Migrate(saved);
            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.quest.quests[0].runId, Is.EqualTo("old-run"));
            Assert.That(migrated.quest.pendingRunCompletions.Count, Is.EqualTo(1));
            Assert.That(migrated.futureDaily.appliedQuestRunDayCostIds, Does.Contain("old-paid-run"));
        }

        [UnityTest] public IEnumerator Escort_PlayMode_FollowsStopsDuringCombatAndChecksExitDistance()
        {
            yield return new EnterPlayMode();
            var (runtime, definition) = Setup(GeneralMissionKind.Rescue, 0); Accept(runtime, definition);
            GameObject player = Object("FollowTarget"); player.transform.position = new Vector3(4, 0, 0);
            player.tag = "Player";
            GeneralMissionEscortActor actor = Component<GeneralMissionEscortActor>(false);
            actor.GetComponent<Rigidbody2D>().gravityScale = 0;
            Set(actor, "questRuntime", runtime); Set(actor, "questId", "mission"); Set(actor, "targetId", "rescue");
            Set(actor, "followTarget", player.transform); Enable(actor); actor.Interact(player);
            Assert.That(actor.IsFollowing, Is.True, $"Interaction starts following; state={GameStateMachine.Instance?.Current}");
            yield return null;
            Vector3 initial = actor.transform.position;
            yield return WaitForPhysics(0.24f);
            Assert.That(actor.transform.position.x, Is.GreaterThan(initial.x),
                $"following={actor.IsFollowing}, state={GameStateMachine.Instance?.Current}, playing={Application.isPlaying}, enabled={actor.enabled}/{actor.gameObject.activeInHierarchy}, body={actor.GetComponent<Rigidbody2D>().bodyType}/{actor.GetComponent<Rigidbody2D>().simulated}/{actor.GetComponent<Rigidbody2D>().position}, scale={Time.timeScale}, fixed={Time.fixedDeltaTime}, physics={Physics2D.simulationMode}, target={player.transform.position}, path={runtime.GetMissionEscortPath("mission").Count}");
            GameSaveData escortSave = RoundTrip(runtime);
            GeneralMissionStateSaveData escortState = escortSave.quest.quests.Find(q => q.questId == "mission").generalMission;
            Assert.That(escortState.hasEscortPosition, Is.True, "Escort position is captured.");
            Assert.That(escortState.escortPosition.x, Is.GreaterThan(0), "Escort position reflects physical movement.");
            Set(actor, "followTarget", null);
            runtime.RestoreSaveData(escortSave);
            Assert.That(actor.IsFollowing, Is.True, "Restored run is following.");
            Transform rebound = (Transform)typeof(GeneralMissionEscortActor).GetField("followTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
            Assert.That(rebound, Is.SameAs(player.transform), "Player tag must resolve the canonical player in the isolated test.");
            Vector3 restoredPosition = actor.transform.position;
            yield return WaitForPhysics(0.08f);
            Assert.That(actor.transform.position.x, Is.GreaterThan(restoredPosition.x), "Restored escort resolves the player at the restore boundary.");
            GameStateMachine stateMachine = GameStateMachine.Instance != null ? GameStateMachine.Instance : Component<GameStateMachine>();
            GameFlowController gameFlow = GameFlowController.Instance != null ? GameFlowController.Instance : Component<GameFlowController>();
            gameFlow.BeginCombatTransition(); gameFlow.EnterCombatPlanning();
            yield return WaitForPhysics(0.04f);
            Vector3 stopped = actor.transform.position;
            yield return WaitForPhysics(0.1f);
            Assert.That(actor.transform.position, Is.EqualTo(stopped));
            gameFlow.EnterExploration();
            Assert.That(actor.TryEscape("exit", new Vector3(50, 0, 0), 2), Is.False);
            Assert.That(actor.ApplyDamage(21, "hazard"), Is.True);
            Assert.That(actor.TryEscape("exit", actor.transform.position, 2), Is.False);
            Assert.That(actor.ApplyHealing(1, "heal"), Is.True);
            Assert.That(actor.TryEscape("exit", actor.transform.position, 2), Is.True);
            Assert.That(runtime.IsQuestComplete("mission"), Is.True);
            QuestDefinitionSO deviceMission = Definition(GeneralMissionKind.Suppressors, 3, "ghost-check");
            Accept(runtime, deviceMission);
            GeneralMissionSuppressor device = Component<GeneralMissionSuppressor>(false);
            Set(device, "questRuntime", runtime); Set(device, "questId", "ghost-check");
            Set(device, "deviceId", runtime.GetGeneralMissionSnapshot("ghost-check").SelectedTargetIds[0]); Enable(device);
            InteractionController interaction = InteractionController.Instance != null ? InteractionController.Instance : Component<InteractionController>();
            interaction.Register((IInteractable)device);
            interaction.ShowTemporaryMessage("임무 진행", 5);
            UnityEngine.Object.DestroyImmediate(device.gameObject);
            Assert.That(interaction.TryInteractCurrent(), Is.False, "A destroyed interface target cannot interact during a prompt message.");
            Assert.That(runtime.GetGeneralMissionSnapshot("ghost-check").Progress, Is.Zero);
            Cleanup(); yield return new ExitPlayMode();
        }

        private (QuestRuntime, QuestDefinitionSO) Setup(GeneralMissionKind kind, int targets)
        { QuestDefinitionSO definition = Definition(kind, targets); return (Runtime(definition), definition); }
        private QuestDefinitionSO Definition(GeneralMissionKind kind, int count, string id = "mission")
        {
            QuestDefinitionSO definition = ScriptableObject.CreateInstance<QuestDefinitionSO>(); _objects.Add(definition);
            GeneralMissionDefinition rules = new(); Set(rules, "kind", kind); Set(rules, "objectiveId", "objective");
            Set(rules, "targetIds", Enumerable.Range(0, count).Select(i => $"target_{i}").ToArray());
            Set(rules, "rescueTargetId", "rescue"); Set(rules, "exitId", "exit");
            QuestObjectiveDefinition objective = new(); Set(objective, "objectiveId", "objective");
            Set(objective, "eventType", rules.EventType); Set(objective, "requiredCount", rules.RequiredCount);
            Set(definition, "questId", id); Set(definition, "questTitle", id); Set(definition, "repeatable", true);
            Set(definition, "missionDayCost", 1); Set(definition, "retryPolicy", QuestRetryPolicy.RestartFromBeginning);
            Set(definition, "rewardGold", 10); Set(definition, "generalMission", rules); Set(definition, "objectives", new[] { objective });
            return definition;
        }
        private QuestRuntime Runtime(QuestDefinitionSO definition)
        {
            QuestRuntime runtime = Component<QuestRuntime>(false); Set(runtime, "questDefinitions", new[] { definition }); Enable(runtime); return runtime;
        }
        private static void Accept(QuestRuntime runtime, QuestDefinitionSO definition)
        { Assert.That(runtime.TryAcceptGeneralMission(definition, out string reason), Is.True, reason); }
        private GameObject Object(string name, bool active = true)
        { GameObject go = new(name); go.SetActive(active); _objects.Add(go); return go; }
        private T Component<T>(bool active = true) where T : Component
        {
            GameObject go = Object(typeof(T).Name, false);
            if (typeof(GeneralMissionBinding).IsAssignableFrom(typeof(T))) go.AddComponent<BoxCollider2D>().isTrigger = true;
            T value = go.AddComponent<T>(); Assert.That(value, Is.Not.Null, typeof(T).Name);
            if (active) Enable(value); return value;
        }
        private static void Enable(Component value)
        { value.gameObject.SetActive(true); CallIfPresent(value, "Awake"); CallIfPresent(value, "OnEnable"); }
        private static void Set(object value, string field, object data)
        {
            Type type = value.GetType(); FieldInfo info = null;
            while (type != null && info == null) { info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); type = type.BaseType; }
            Assert.That(info, Is.Not.Null, field); info.SetValue(value, data);
        }
        private static MethodInfo Method(object value, string name) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static object Call(object value, string name, params object[] args) => Method(value, name).Invoke(value, args);
        private static void CallIfPresent(object value, string name) => Method(value, name)?.Invoke(value, null);
        private static GameSaveData RoundTrip(QuestRuntime runtime)
        { GameSaveData saved = new(); runtime.CaptureSaveData(saved); return SaveSerializer.FromGameSaveJson(SaveSerializer.ToJson(saved)); }
        private static string Lifecycle(CombatEncounterGroup group) => group.GetType().GetProperty("Lifecycle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(group).ToString();
        private static GameSaveData Migrate(GameSaveData saved)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            object[] args = { SaveSerializer.ToJson(saved), null, false, null };
            bool success = (bool)migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            Assert.That(success, Is.True, args[3] as string); return (GameSaveData)args[1];
        }
        private static IEnumerator WaitForPhysics(float seconds)
        {
            // This is an EditMode test entering Play Mode. WaitForFixedUpdate is not
            // awaited by the EditMode runner; wait for actual Unity physics time.
            float until = Time.fixedTime + seconds;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.fixedTime < until && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.fixedTime, Is.GreaterThanOrEqualTo(until), "Unity physics advanced in Play Mode.");
        }
    }
}
