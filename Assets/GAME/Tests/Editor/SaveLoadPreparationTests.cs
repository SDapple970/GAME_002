using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Common.Identity;
using Game.Combat.Integration;
using Game.Core;
using Game.Daily;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Supply;
using Game.Systems.Persona;
using Game.World.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Integration
{
    public sealed class SaveLoadPreparationTests
    {
        private string _directory;
        private string _primary;
        private GameObject _serviceObject;
        private SaveLoadService _service;

        [SetUp]
        public void SetUp()
        {
            CleanupObjects();
            _directory = Path.Combine(Path.GetTempPath(), "GAME_002_Batch10_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _primary = Path.Combine(_directory, "game_save.json");
            _serviceObject = new GameObject("SaveLoadService_Test");
            _service = _serviceObject.AddComponent<SaveLoadService>();
            Invoke(_service, "SetStoragePathForTests", _primary);
        }

        [TearDown]
        public void TearDown()
        {
            CleanupObjects();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveManagerHotkeys_AreEditorGuardedAndNotInputServiceOwned()
        {
            string source = File.ReadAllText(ProjectPath("Assets/GAME/Scripts/NonCombat/Save/SaveManager.cs"));
            Assert.That(source, Does.Contain("#if UNITY_EDITOR"));
            Assert.That(source, Does.Contain("SaveLoadService.Instance.Save()"));
            Assert.That(File.ReadAllText(ProjectPath("Assets/GAME/Scripts/Input/InputService.cs")), Does.Not.Contain("KeyCode.F5"));
        }

        [Test]
        public void CurrentDto_RoundTripsHeaderAndSections()
        {
            GameSaveData source = new();
            source.header.activeSceneId = "Dungeon 1";
            source.currency.gold = 42;
            source.inventory.items.Add(new SaveIntEntry { id = "item", value = 3 });
            Assert.That(SaveSerializer.TryFromGameSaveJson(SaveSerializer.ToJson(source), out GameSaveData restored), Is.True);
            Assert.That(restored.header.formatId, Is.EqualTo(GameSaveDataFormat.FormatId));
            Assert.That(restored.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(restored.currency.gold, Is.EqualTo(42));
        }

        [Test]
        public void DuplicateSpawnId_CapturesAndRestoresSavedPositionInsteadOfArbitraryMarker()
        {
            string spawnId = "save-duplicate-" + Guid.NewGuid().ToString("N");
            GameObject player = new("SavedPositionPlayer");
            GameObject first = new("FirstSpawn");
            GameObject second = new("SecondSpawn");
            try
            {
                player.transform.position = new Vector3(2f, 3f, 0f);
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                first.transform.position = player.transform.position;
                second.transform.position = new Vector3(-4f, 1f, 0f);
                SetField(first.AddComponent<SceneSpawnPoint>(), "spawnPointId", spawnId);
                SetField(second.AddComponent<SceneSpawnPoint>(), "spawnPointId", spawnId);
                SetField(_service, "player", player.transform);

                GameSaveData save = new();
                Invoke(_service, "CaptureLocation", save);
                Assert.That(save.location.hasPositionFallback, Is.True);
                Assert.That(save.header.playerSpawnId, Is.Empty);

                // Older saves can still contain an ambiguous marker ID.
                save.header.playerSpawnId = spawnId;
                save.location.positionX = 9f;
                save.location.positionY = -2f;
                player.transform.position = Vector3.zero;
                body.linearVelocity = new Vector2(7f, -3f);
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("Spawn point .* is ambiguous"));
                Invoke(_service, "RestoreLocation", save);

                Assert.That(player.transform.position, Is.EqualTo(new Vector3(9f, -2f, 0f)));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void UniqueSavedSpawnId_RestoresMarkerAndMissingIdUsesSavedPosition()
        {
            string spawnId = "save-unique-" + Guid.NewGuid().ToString("N");
            GameObject player = new("UniqueSpawnPlayer");
            GameObject marker = new("UniqueSpawn");
            try
            {
                marker.transform.position = new Vector3(4f, 5f, 0f);
                SetField(marker.AddComponent<SceneSpawnPoint>(), "spawnPointId", spawnId);
                SetField(_service, "player", player.transform);

                GameSaveData save = new();
                save.header.playerSpawnId = spawnId;
                save.location.hasPositionFallback = true;
                save.location.positionX = -3f;
                save.location.positionY = 2f;

                Invoke(_service, "RestoreLocation", save);
                Assert.That(player.transform.position, Is.EqualTo(marker.transform.position));

                save.header.playerSpawnId = "missing-" + spawnId;
                Invoke(_service, "RestoreLocation", save);
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(-3f, 2f, 0f)));
                Invoke(_service, "RestoreLocation", save);
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(-3f, 2f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(marker);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [TestCase(GameplayOutcomeSourceType.Unknown)]
        [TestCase((GameplayOutcomeSourceType)(-1))]
        [TestCase((GameplayOutcomeSourceType)999)]
        public void GameplayOutcomeIdentity_RejectsUndefinedSourceValues(GameplayOutcomeSourceType sourceType)
        {
            Assert.That(GameplayOutcomeIdentity.TryCreate(sourceType, "source", "action", out GameplayOutcomeIdentity identity), Is.False);
            Assert.That(identity.IsValid, Is.False);
            Assert.That(identity.CanonicalId, Is.Empty);
        }

        [Test]
        public void GameplayOutcomeIdentity_AllDeclaredProductionSourcesRemainCanonicalAndStable()
        {
            foreach (GameplayOutcomeSourceType sourceType in Enum.GetValues(typeof(GameplayOutcomeSourceType)))
            {
                if (sourceType == GameplayOutcomeSourceType.Unknown)
                    continue;

                Assert.That(GameplayOutcomeIdentity.TryCreate(sourceType, " source ", " action ", out GameplayOutcomeIdentity identity), Is.True, sourceType.ToString());
                Assert.That(identity.CanonicalId, Is.EqualTo($"{(int)sourceType}|6:source|6:action"), sourceType.ToString());
            }
        }

        [TestCase("")]
        [TestCase("not json")]
        public void InvalidJson_DoesNotProduceCanonicalSnapshot(string json)
        {
            Assert.That(SaveSerializer.TryFromGameSaveJson(json, out _), Is.False);
        }

        [Test]
        public void SuccessfulSaveCreatesPrimaryAndRotatesBackup()
        {
            Assert.That(_service.TrySave(out _), Is.True);
            string first = File.ReadAllText(_primary);
            Assert.That(_service.TrySave(out _), Is.True);
            Assert.That(File.Exists(_primary + ".bak"), Is.True);
            Assert.That(File.ReadAllText(_primary + ".bak"), Is.EqualTo(first));
            Assert.That(File.Exists(_primary + ".tmp"), Is.False);
        }

        [Test]
        public void MissingFileReturnsFailureWithoutCreatingDefault()
        {
            Assert.That(_service.TryLoad(out string message), Is.False);
            Assert.That(message, Does.Contain("No save file"));
            Assert.That(File.Exists(_primary), Is.False);
        }

        [Test]
        public void HasSaveData_ReportsCanonicalPrimaryOrBackupWithoutReadingFromUi()
        {
            Assert.That(_service.HasSaveData, Is.False);

            File.WriteAllText(_primary, SaveSerializer.ToJson(new GameSaveData()));
            Assert.That(_service.HasSaveData, Is.True);

            File.Delete(_primary);
            File.WriteAllText(_primary + ".bak", SaveSerializer.ToJson(new GameSaveData()));
            Assert.That(_service.HasSaveData, Is.True);
        }

        [Test]
        public void SaveCompletionEvent_ReportsCanonicalSaveResult()
        {
            bool? succeeded = null;
            string completionMessage = null;
            _service.OnSaveCompleted += (result, message) =>
            {
                succeeded = result;
                completionMessage = message;
            };

            Assert.That(_service.TrySave(out _), Is.True);
            Assert.That(succeeded, Is.True);
            Assert.That(completionMessage, Does.StartWith("Saved "));
        }

        [Test]
        public void UnsafeState_RemainsBlockedForSave()
        {
            GameStateMachine stateMachine = new GameObject("UnsafeSaveState").AddComponent<GameStateMachine>();
            Invoke(stateMachine, "Awake");
            Assert.That(stateMachine.TrySetState(GameState.Dialogue, "save policy test"), Is.True);

            Assert.That(_service.CanSave, Is.False);
            Assert.That(_service.TrySave(out string message), Is.False);
            Assert.That(message, Does.Contain("Save blocked in state Dialogue"));
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
        }

        [Test]
        public void SaveEligibility_AllowsExplorationAndItsPauseOnly()
        {
            GameStateMachine stateMachine = new GameObject("SaveEligibilityState").AddComponent<GameStateMachine>();
            Invoke(stateMachine, "Awake");
            Assert.That(_service.CanSave, Is.True);

            Assert.That(stateMachine.TrySetState(GameState.Paused, "exploration pause"), Is.True);
            Assert.That(_service.CanSave, Is.True);
            Assert.That(stateMachine.TrySetState(GameState.Exploration, "resume"), Is.True);

            foreach (GameState blocked in new[]
            {
                GameState.Dialogue, GameState.Choice, GameState.CombatTransition,
                GameState.CombatPlanning, GameState.CombatResolving, GameState.Reward,
                GameState.Cutscene, GameState.Loading, GameState.UIOnly, GameState.Title
            })
            {
                if (blocked == GameState.Choice)
                    Assert.That(stateMachine.TrySetState(GameState.Dialogue, "choice setup"), Is.True);
                else if (blocked == GameState.CombatPlanning)
                    Assert.That(stateMachine.TrySetState(GameState.CombatTransition, "combat setup"), Is.True);
                else if (blocked == GameState.CombatResolving)
                {
                    Assert.That(stateMachine.TrySetState(GameState.CombatTransition, "combat setup"), Is.True);
                    Assert.That(stateMachine.TrySetState(GameState.CombatPlanning, "resolving setup"), Is.True);
                }

                Assert.That(stateMachine.TrySetState(blocked, "save eligibility"), Is.True, blocked.ToString());
                Assert.That(_service.CanSave, Is.False, blocked.ToString());
                if (blocked == GameState.Dialogue)
                {
                    Assert.That(stateMachine.TrySetState(GameState.Paused, "dialogue pause"), Is.True);
                    Assert.That(_service.CanSave, Is.False);
                }
                if (blocked == GameState.CombatTransition)
                    Assert.That(stateMachine.TrySetState(GameState.CombatPlanning, "combat exit setup"), Is.True);
                Assert.That(stateMachine.TrySetState(GameState.Exploration, "reset eligibility"), Is.True);
            }
        }

        [Test]
        public void ProductionSaveUx_DelegatesToCanonicalOwnersWithoutDirectStorageOrNewGameFlow()
        {
            string pauseSource = File.ReadAllText(ProjectPath("Assets/GAME/Scripts/UI/PauseSavePanel.cs"));
            Assert.That(pauseSource, Does.Contain("service.TrySave(out string message)"));
            Assert.That(pauseSource, Does.Contain("flow.ResumePreviousState()"));
            Assert.That(pauseSource, Does.Not.Contain("File."));

            string titleSource = File.ReadAllText(ProjectPath("Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs"));
            int continueStart = titleSource.IndexOf("private void HandleContinueClicked", StringComparison.Ordinal);
            int continueEnd = titleSource.IndexOf("private IEnumerator Co_OpenRequestPaper", continueStart, StringComparison.Ordinal);
            string continueMethod = titleSource.Substring(continueStart, continueEnd - continueStart);
            Assert.That(continueMethod, Does.Contain("service.TryLoad(out string message)"));
            Assert.That(continueMethod, Does.Not.Contain("ResetMissionProgress"));
            Assert.That(continueMethod, Does.Not.Contain("SceneManager"));
            Assert.That(titleSource, Does.Contain("continueButton.interactable = !_transitioning && service != null && service.CanLoad"));
        }

        [Test]
        public void TitleNewGame_DelegatesRuntimeResetWithoutDeletingSaveOrResettingFeaturesDirectly()
        {
            string titleSource = File.ReadAllText(ProjectPath("Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs"));
            Assert.That(titleSource, Does.Contain("service.TryResetForNewGame(out string message)"));
            Assert.That(titleSource, Does.Contain("sceneFlow.LoadScene(sceneName)"));
            Assert.That(titleSource, Does.Not.Contain("ResetMissionProgress"));
            Assert.That(titleSource, Does.Not.Contain("File.Delete"));
        }

        [Test]
        public void NewGameReset_PreservesSaveFileAndRestoresOldTimelineAfterReset()
        {
            CharacterProgressionDefinitionSO progressionDefinition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
            QuestDefinitionSO questDefinition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            try
            {
                SetField(progressionDefinition, "characterId", "hero");
                SetField(progressionDefinition, "maximumLevel", 3);
                SetField(progressionDefinition, "experienceRequiredByLevel", new[] { 10, 20 });
                SetField(questDefinition, "questId", "quest.newgame");

                GameStateMachine state = new GameObject("GameStateMachine").AddComponent<GameStateMachine>();
                Invoke(state, "Awake");
                GameFlowController flow = new GameObject("GameFlowController").AddComponent<GameFlowController>();
                Invoke(flow, "Awake");
                CurrencyWallet wallet = new GameObject("CurrencyWallet").AddComponent<CurrencyWallet>();
                Invoke(wallet, "Awake");
                InventoryService inventory = new GameObject("InventoryService").AddComponent<InventoryService>();
                PartyRuntime party = new GameObject("PartyRuntime").AddComponent<PartyRuntime>();
                CharacterProgressionService progression = new GameObject("CharacterProgressionService").AddComponent<CharacterProgressionService>();
                Invoke(progression, "ConfigureForTests", "hero", new[] { progressionDefinition });
                PersonaStatusManager persona = new GameObject("PersonaStatusManager").AddComponent<PersonaStatusManager>();
                new GameObject("PersonaSaveAdapter").AddComponent<PersonaSaveAdapter>();
                StoryProgressManager story = new GameObject("StoryProgressManager").AddComponent<StoryProgressManager>();
                Invoke(story, "Awake");
                StoryFlagDatabase flags = new GameObject("StoryFlagDatabase").AddComponent<StoryFlagDatabase>();
                QuestRuntime quest = new GameObject("QuestRuntime").AddComponent<QuestRuntime>();
                SetField(quest, "questDefinitions", new[] { questDefinition });
                Invoke(quest, "Awake");
                CalendarService calendar = new GameObject("CalendarService").AddComponent<CalendarService>();
                Invoke(calendar, "Awake");
                QuestCalendarIntegration questCalendar = new GameObject("QuestCalendarIntegration").AddComponent<QuestCalendarIntegration>();
                DaySettlementFlow settlement = new GameObject("DaySettlementFlow").AddComponent<DaySettlementFlow>();
                RewardService rewards = new GameObject("RewardService").AddComponent<RewardService>();
                SetField(rewards, "currencyWallet", wallet);
                SetField(rewards, "inventoryService", inventory);
                InteractionRuntime interaction = new GameObject("InteractionRuntime").AddComponent<InteractionRuntime>();
                Invoke(interaction, "Awake");
                ExplorationResourceRuntime resources = new GameObject("ExplorationResourceRuntime").AddComponent<ExplorationResourceRuntime>();
                PersistentConditionRuntime conditions = new GameObject("PersistentConditionRuntime").AddComponent<PersistentConditionRuntime>();
                SupplyLoadoutService supply = new GameObject("SupplyLoadoutService").AddComponent<SupplyLoadoutService>();
                Invoke(supply, "Awake");
                CombatEncounterGroup oldEncounter = new GameObject("OldEncounter").AddComponent<CombatEncounterGroup>();
                SetField(oldEncounter, "encounterId", "encounter.once");

                wallet.SetGold(12);
                inventory.AddItem("potion", 2);
                party.AddMember("hero");
                Assert.That(progression.ApplyExperience("hero", 15).AppliedExperience, Is.EqualTo(15));
                persona.SetStat(PersonaStat.Courage, 3, 4);
                story.SetChapter(2);
                story.SetMainProgress(7);
                story.MarkEventCompleted("story.event");
                flags.SetFlag("story.flag", true);
                quest.StartQuest(questDefinition);
                GameSaveData questSeed = new();
                quest.CaptureSaveData(questSeed);
                questSeed.quest.quests.Single(item => item.questId == "quest.newgame").processedEventIds.Add("quest.event");
                quest.RestoreSaveData(questSeed);
                Assert.That(calendar.TryAdvanceDays(2), Is.True);
                GameSaveData dailySeed = new();
                dailySeed.futureDaily.appliedQuestDayCostIds.Add("quest.newgame");
                dailySeed.futureDaily.completedSettlementIds.Add("settlement.once");
                questCalendar.RestoreSaveData(dailySeed);
                settlement.RestoreSaveData(dailySeed);
                interaction.MarkConsumed("loot.once", InteractionUsePolicy.PersistentOnce);
                interaction.MarkConsumed("talk.once", InteractionUsePolicy.OncePerSession);
                interaction.RememberResolvedOutcome("loot.once", "open", "rare");
                resources.TrySetShining(5);
                resources.TrySetHunger(2);
                conditions.TryAcquire("hero", "flu", PersistentConditionCategory.Disease);
                supply.AddItem("supply", 2);
                GameSaveData worldSeed = new();
                worldSeed.world.clearedEncounterIds.Add("encounter.once");
                oldEncounter.RestoreSaveData(worldSeed);
                RewardGrantRequest grant = new(RewardSourceType.Story, "story.once", 3, 0, "potion", 1, "grant");
                Assert.That(rewards.GrantReward(grant).DuplicateBlocked, Is.False);
                Assert.That(_service.TrySave(out _), Is.True);
                string savedJson = File.ReadAllText(_primary);

                // Scene-owned encounters are destroyed on the Title transition, then reauthored by the starting scene.
                UnityEngine.Object.DestroyImmediate(oldEncounter.gameObject);
                Assert.That(state.TrySetState(GameState.Title, "return to title"), Is.True);
                Assert.That(_service.TryResetForNewGame(out string resetMessage), Is.True, resetMessage);
                Assert.That(state.Current, Is.EqualTo(GameState.Title));
                Assert.That(File.ReadAllText(_primary), Is.EqualTo(savedJson));
                Assert.That(wallet.Gold, Is.Zero);
                Assert.That(inventory.GetCount("potion"), Is.Zero);
                Assert.That(party.Members, Is.Empty);
                Assert.That(progression.TryGetState("hero", out int resetLevel, out int resetExperience), Is.True);
                Assert.That((resetLevel, resetExperience), Is.EqualTo((1, 0)));
                Assert.That((persona.GetLevel(PersonaStat.Courage), persona.GetXp(PersonaStat.Courage)), Is.EqualTo((1, 0)));
                Assert.That((story.CurrentChapter, story.MainProgress, story.IsEventCompleted("story.event")), Is.EqualTo((1, 0, false)));
                Assert.That(flags.HasFlag("story.flag"), Is.False);
                Assert.That(quest.GetQuestStatus("quest.newgame"), Is.EqualTo(QuestStatus.Inactive));
                GameSaveData resetQuest = new();
                quest.CaptureSaveData(resetQuest);
                Assert.That(resetQuest.quest.quests.Single(item => item.questId == "quest.newgame").processedEventIds, Is.Empty);
                Assert.That(calendar.CurrentDay, Is.EqualTo(1));
                GameSaveData resetDaily = new();
                questCalendar.CaptureSaveData(resetDaily);
                settlement.CaptureSaveData(resetDaily);
                Assert.That(resetDaily.futureDaily.appliedQuestDayCostIds, Is.Empty);
                Assert.That(resetDaily.futureDaily.completedSettlementIds, Is.Empty);
                Assert.That(interaction.IsConsumed("loot.once", InteractionUsePolicy.PersistentOnce), Is.False);
                Assert.That(interaction.IsConsumed("talk.once", InteractionUsePolicy.OncePerSession), Is.False);
                Assert.That(interaction.TryGetResolvedOutcome("loot.once", "open", out _), Is.False);
                Assert.That((resources.Shining, resources.Hunger), Is.EqualTo((0, 0)));
                Assert.That(conditions.HasCondition("hero", "flu", PersistentConditionCategory.Disease), Is.False);
                Assert.That(supply.GetSnapshot().GetCount("supply"), Is.Zero);
                Assert.That(rewards.GrantReward(grant).DuplicateBlocked, Is.False);

                CombatEncounterGroup newEncounter = new GameObject("NewEncounter").AddComponent<CombatEncounterGroup>();
                SetField(newEncounter, "encounterId", "encounter.once");
                GameSaveData cleanWorld = new();
                newEncounter.CaptureSaveData(cleanWorld);
                Assert.That(cleanWorld.world.clearedEncounterIds, Is.Empty);

                Assert.That(_service.TryLoad(out string loadMessage), Is.True, loadMessage);
                Assert.That(wallet.Gold, Is.EqualTo(15));
                Assert.That(inventory.GetCount("potion"), Is.EqualTo(3));
                Assert.That(party.Contains("hero"), Is.True);
                Assert.That(progression.TryGetState("hero", out int restoredLevel, out int restoredExperience), Is.True);
                Assert.That((restoredLevel, restoredExperience), Is.EqualTo((2, 5)));
                Assert.That((persona.GetLevel(PersonaStat.Courage), persona.GetXp(PersonaStat.Courage)), Is.EqualTo((3, 4)));
                Assert.That((story.CurrentChapter, story.MainProgress, story.IsEventCompleted("story.event")), Is.EqualTo((2, 7, true)));
                Assert.That(flags.HasFlag("story.flag"), Is.True);
                Assert.That(quest.GetQuestStatus("quest.newgame"), Is.EqualTo(QuestStatus.Active));
                GameSaveData restoredQuest = new();
                quest.CaptureSaveData(restoredQuest);
                Assert.That(restoredQuest.quest.quests.Single(item => item.questId == "quest.newgame").processedEventIds, Does.Contain("quest.event"));
                Assert.That(calendar.CurrentDay, Is.EqualTo(3));
                GameSaveData restoredDaily = new();
                questCalendar.CaptureSaveData(restoredDaily);
                settlement.CaptureSaveData(restoredDaily);
                Assert.That(restoredDaily.futureDaily.appliedQuestDayCostIds, Does.Contain("quest.newgame"));
                Assert.That(restoredDaily.futureDaily.completedSettlementIds, Does.Contain("settlement.once"));
                Assert.That(interaction.IsConsumed("loot.once", InteractionUsePolicy.PersistentOnce), Is.True);
                Assert.That(interaction.TryGetResolvedOutcome("loot.once", "open", out string outcome), Is.True);
                Assert.That(outcome, Is.EqualTo("rare"));
                Assert.That((resources.Shining, resources.Hunger), Is.EqualTo((5, 2)));
                Assert.That(conditions.HasCondition("hero", "flu", PersistentConditionCategory.Disease), Is.True);
                Assert.That(supply.GetSnapshot().GetCount("supply"), Is.EqualTo(2));
                Assert.That(rewards.GrantReward(grant).DuplicateBlocked, Is.True);
                Assert.That(wallet.Gold, Is.EqualTo(15));
                GameSaveData restoredWorld = new();
                newEncounter.CaptureSaveData(restoredWorld);
                Assert.That(restoredWorld.world.clearedEncounterIds, Does.Contain("encounter.once"));
                Assert.That(File.ReadAllText(_primary), Is.EqualTo(savedJson));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(progressionDefinition);
                UnityEngine.Object.DestroyImmediate(questDefinition);
            }
        }

        [Test]
        public void NewGameReset_RepeatedRequestIsSafeAndNonTitleRequestIsRejected()
        {
            GameStateMachine state = new GameObject("GameStateMachine").AddComponent<GameStateMachine>();
            Invoke(state, "Awake");
            new GameObject("GameFlowController").AddComponent<GameFlowController>();
            CurrencyWallet wallet = new GameObject("CurrencyWallet").AddComponent<CurrencyWallet>();
            wallet.SetGold(8);

            Assert.That(_service.TryResetForNewGame(out _), Is.False);
            Assert.That(wallet.Gold, Is.EqualTo(8));
            Assert.That(state.TrySetState(GameState.Title, "title"), Is.True);
            Assert.That(_service.TryResetForNewGame(out _), Is.True);
            Assert.That(_service.TryResetForNewGame(out _), Is.True);
            Assert.That(wallet.Gold, Is.Zero);
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
        }

        [Test]
        public void NewGameReset_UsesAuthoredStartingValuesAndDoesNotReplayQuestEvents()
        {
            CharacterProgressionDefinitionSO progressionDefinition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
            QuestDefinitionSO questDefinition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            try
            {
                SetField(progressionDefinition, "characterId", "hero");
                SetField(progressionDefinition, "startingLevel", 2);
                SetField(progressionDefinition, "maximumLevel", 3);
                SetField(progressionDefinition, "experienceRequiredByLevel", new[] { 10, 20 });
                SetField(questDefinition, "questId", "quest.authored");

                GameStateMachine state = new GameObject("GameStateMachine").AddComponent<GameStateMachine>();
                Invoke(state, "Awake");
                CurrencyWallet wallet = new GameObject("CurrencyWallet").AddComponent<CurrencyWallet>();
                SetField(wallet, "gold", 7);
                Invoke(wallet, "Awake");
                CalendarService calendar = new GameObject("CalendarService").AddComponent<CalendarService>();
                SetField(calendar, "currentDay", 5);
                SetField(calendar, "currentWeek", 2);
                Invoke(calendar, "Awake");
                StoryProgressManager story = new GameObject("StoryProgressManager").AddComponent<StoryProgressManager>();
                SetField(story, "currentChapter", 2);
                SetField(story, "mainProgress", 4);
                Invoke(story, "Awake");
                CharacterProgressionService progression = new GameObject("CharacterProgressionService").AddComponent<CharacterProgressionService>();
                Invoke(progression, "ConfigureForTests", "hero", new[] { progressionDefinition });
                SupplyLoadoutService supply = new GameObject("SupplyLoadoutService").AddComponent<SupplyLoadoutService>();
                supply.AddItem("ration", 2);
                Invoke(supply, "Awake");
                QuestRuntime quest = new GameObject("QuestRuntime").AddComponent<QuestRuntime>();
                SetField(quest, "questDefinitions", new[] { questDefinition });
                Invoke(quest, "Awake");
                int started = 0;
                int completed = 0;
                quest.OnQuestStarted += _ => started++;
                quest.OnQuestCompleted += _ => completed++;

                wallet.SetGold(20);
                calendar.TryAdvanceDays(2);
                story.SetChapter(3);
                story.SetMainProgress(9);
                progression.ApplyExperience("hero", 5);
                supply.AddItem("ration", 3);
                quest.StartQuest(questDefinition);
                Assert.That(started, Is.EqualTo(1));
                Assert.That(state.TrySetState(GameState.Title, "title"), Is.True);

                Assert.That(_service.TryResetForNewGame(out string message), Is.True, message);
                Assert.That(wallet.Gold, Is.EqualTo(7));
                Assert.That((calendar.CurrentDay, calendar.CurrentWeek), Is.EqualTo((5, 2)));
                Assert.That((story.CurrentChapter, story.MainProgress), Is.EqualTo((2, 4)));
                Assert.That(progression.TryGetState("hero", out int level, out int experience), Is.True);
                Assert.That((level, experience), Is.EqualTo((2, 0)));
                Assert.That(supply.GetSnapshot().GetCount("ration"), Is.EqualTo(2));
                Assert.That(quest.GetQuestStatus("quest.authored"), Is.EqualTo(QuestStatus.Inactive));
                Assert.That((started, completed), Is.EqualTo((1, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(progressionDefinition);
                UnityEngine.Object.DestroyImmediate(questDefinition);
            }
        }

        [Test]
        public void DuplicateSceneInteractionRuntime_CannotReplacePersistentSaveOwner()
        {
            InteractionRuntime owner = new GameObject("InteractionRuntime").AddComponent<InteractionRuntime>();
            Invoke(owner, "Awake");
            InteractionRuntime duplicate = new GameObject("Interaction").AddComponent<InteractionRuntime>();
            Invoke(duplicate, "Awake");
            Assert.That(InteractionRuntime.Instance, Is.SameAs(owner));
            Assert.That(duplicate.enabled, Is.False);

            owner.MarkConsumed("persistent.loot", InteractionUsePolicy.PersistentOnce);
            GameSaveData snapshot = _service.CaptureGameSaveDataSnapshot();
            Assert.That(snapshot.world.interactions.Single(item => item.interactionId == "persistent.loot").consumed,
                Is.True);

            owner.MarkConsumed("later.timeline", InteractionUsePolicy.PersistentOnce);
            _service.RestoreGameSaveDataSnapshot(snapshot);
            _service.RestoreGameSaveDataSnapshot(snapshot);

            Assert.That(owner.IsConsumed("persistent.loot", InteractionUsePolicy.PersistentOnce), Is.True);
            Assert.That(owner.IsConsumed("later.timeline", InteractionUsePolicy.PersistentOnce), Is.False);
        }

        [Test]
        public void SavedFile_RestoresFreshRuntimeOwnersAndRepeatedLoadWithoutRegrant()
        {
            CharacterProgressionDefinitionSO definition = ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>();
            try
            {
                SetField(definition, "characterId", "hero");
                SetField(definition, "maximumLevel", 3);
                SetField(definition, "experienceRequiredByLevel", new[] { 10, 20 });

                CurrencyWallet sourceWallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
                InventoryService sourceInventory = new GameObject("Inventory").AddComponent<InventoryService>();
                CharacterProgressionService sourceProgression = new GameObject("Progression").AddComponent<CharacterProgressionService>();
                Invoke(sourceProgression, "ConfigureForTests", "hero", new[] { definition });
                RewardService sourceRewards = new GameObject("Rewards").AddComponent<RewardService>();
                SetField(sourceRewards, "currencyWallet", sourceWallet);
                SetField(sourceRewards, "inventoryService", sourceInventory);
                CalendarService sourceCalendar = new GameObject("Calendar").AddComponent<CalendarService>();
                InteractionRuntime sourceInteraction = new GameObject("InteractionRuntime").AddComponent<InteractionRuntime>();
                Invoke(sourceInteraction, "Awake");

                sourceWallet.SetGold(12);
                sourceInventory.AddItem("potion", 2);
                Assert.That(sourceProgression.ApplyExperience("hero", 15).AppliedExperience, Is.EqualTo(15));
                Assert.That(sourceCalendar.TryAdvanceDays(2), Is.True);
                sourceInteraction.MarkConsumed("persistent.loot", InteractionUsePolicy.PersistentOnce);
                sourceInteraction.RememberResolvedOutcome("persistent.loot", "open", "rare");
                RewardGrantRequest grant = new(RewardSourceType.Story, "story.once", 3, 0, "potion", 1, "grant");
                Assert.That(sourceRewards.GrantReward(grant).DuplicateBlocked, Is.False);
                Assert.That(_service.TrySave(out _), Is.True);

                UnityEngine.Object.DestroyImmediate(_serviceObject);
                _serviceObject = new GameObject("SaveLoadService_Restored");
                _service = _serviceObject.AddComponent<SaveLoadService>();
                Invoke(_service, "SetStoragePathForTests", _primary);

                UnityEngine.Object.DestroyImmediate(sourceWallet.gameObject);
                UnityEngine.Object.DestroyImmediate(sourceInventory.gameObject);
                UnityEngine.Object.DestroyImmediate(sourceProgression.gameObject);
                UnityEngine.Object.DestroyImmediate(sourceRewards.gameObject);
                UnityEngine.Object.DestroyImmediate(sourceCalendar.gameObject);
                UnityEngine.Object.DestroyImmediate(sourceInteraction.gameObject);

                CurrencyWallet restoredWallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
                InventoryService restoredInventory = new GameObject("Inventory").AddComponent<InventoryService>();
                CharacterProgressionService restoredProgression = new GameObject("Progression").AddComponent<CharacterProgressionService>();
                Invoke(restoredProgression, "ConfigureForTests", "hero", new[] { definition });
                RewardService restoredRewards = new GameObject("Rewards").AddComponent<RewardService>();
                SetField(restoredRewards, "currencyWallet", restoredWallet);
                SetField(restoredRewards, "inventoryService", restoredInventory);
                CalendarService restoredCalendar = new GameObject("Calendar").AddComponent<CalendarService>();
                InteractionRuntime restoredInteraction = new GameObject("InteractionRuntime").AddComponent<InteractionRuntime>();
                Invoke(restoredInteraction, "Awake");

                for (int load = 0; load < 2; load++)
                {
                    Assert.That(_service.TryLoad(out string message), Is.True, message);
                    Assert.That(restoredWallet.Gold, Is.EqualTo(15));
                    Assert.That(restoredInventory.GetCount("potion"), Is.EqualTo(3));
                    Assert.That(restoredProgression.TryGetState("hero", out int level, out int experience), Is.True);
                    Assert.That(level, Is.EqualTo(2));
                    Assert.That(experience, Is.EqualTo(5));
                    Assert.That(restoredCalendar.CurrentDay, Is.EqualTo(3));
                    Assert.That(restoredInteraction.IsConsumed("persistent.loot", InteractionUsePolicy.PersistentOnce), Is.True);
                    Assert.That(restoredInteraction.TryGetResolvedOutcome("persistent.loot", "open", out string outcome), Is.True);
                    Assert.That(outcome, Is.EqualTo("rare"));
                    Assert.That(restoredRewards.GrantReward(grant).DuplicateBlocked, Is.True);
                    Assert.That(restoredWallet.Gold, Is.EqualTo(15));
                    Assert.That(restoredInventory.GetCount("potion"), Is.EqualTo(3));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CorruptedPrimaryFallsBackToValidBackup()
        {
            GameSaveData data = new();
            File.WriteAllText(_primary + ".bak", SaveSerializer.ToJson(data));
            File.WriteAllText(_primary, "corrupt");
            Assert.That(_service.TryLoad(out string message), Is.True);
            Assert.That(message, Does.Contain("backup"));
            Assert.That(File.ReadAllText(_primary), Is.EqualTo("corrupt"));
        }

        [Test]
        public void FutureSchemaIsRejectedWithoutMutation()
        {
            GameSaveData data = new(); data.header.schemaVersion = 999;
            File.WriteAllText(_primary, SaveSerializer.ToJson(data));
            Assert.That(_service.TryLoad(out string message), Is.False);
            Assert.That(message, Does.Contain("future schema"));
        }

        [Test]
        public void LegacySaveMigratesGoldInventoryFlagsPersonaAndPosition()
        {
            SaveData legacy = new() { gold = 12, playerPosition = new Vector3(1, 2, 3), currentChapterId = "chapter" };
            legacy.inventory.Add(new IntEntry { id = "potion", value = 2 });
            legacy.flags.Add(new BoolEntry { id = "seen", value = true });
            legacy.personaStats.Add(new PersonaStatEntry { stat = "Courage", level = 2, xp = 4 });
            File.WriteAllText(_primary, SaveSerializer.ToJson(legacy));
            Assert.That(_service.TryLoad(out _), Is.True);
            GameSaveData migrated = ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(legacy));
            Assert.That(migrated.currency.gold, Is.EqualTo(12));
            Assert.That(migrated.inventory.items.Single().id, Is.EqualTo("potion"));
            Assert.That(migrated.story.flags.Single().id, Is.EqualTo("seen"));
            Assert.That(migrated.progression.personaStats.Single().level, Is.EqualTo(2));
            Assert.That(migrated.location.positionY, Is.EqualTo(2));
        }

        [Test]
        public void SchemaOneMigratesCompletedBoolAndFormatHeader()
        {
            string json = "{\"header\":{\"schemaVersion\":1},\"quest\":{\"quests\":[{\"questId\":\"q\",\"completed\":true}]}}";
            GameSaveData data = ReadSnapshotFromLegacyJson(json);
            Assert.That(data.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(data.header.formatId, Is.EqualTo(GameSaveDataFormat.FormatId));
            Assert.That(data.quest.quests[0].status, Is.EqualTo("Completed"));
        }

        [Test]
        public void InventoryRestoreMergesDuplicatesAndReplacesStaleState()
        {
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            inventory.AddItem("stale", 5);
            GameSaveData data = new();
            data.inventory.items.Add(new SaveIntEntry { id = "item", value = 2 });
            data.inventory.items.Add(new SaveIntEntry { id = "item", value = 3 });
            data.inventory.items.Add(new SaveIntEntry { id = "", value = 99 });
            inventory.RestoreSaveData(data);
            Assert.That(inventory.GetCount("item"), Is.EqualTo(5));
            Assert.That(inventory.GetCount("stale"), Is.Zero);
        }

        [Test]
        public void CurrencyRestoreClampsNegativeGold()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            GameSaveData data = new(); data.currency.gold = -10;
            wallet.RestoreSaveData(data);
            Assert.That(wallet.Gold, Is.Zero);
        }

        [Test]
        public void StoryProgressRoundTripsSilentlyAndNormalizesIds()
        {
            StoryProgressManager source = new GameObject("StorySource").AddComponent<StoryProgressManager>();
            source.SetChapter(3); source.SetMainProgress(7); source.MarkEventCompleted("b"); source.MarkEventCompleted("a"); source.MarkEventCompleted("a");
            GameSaveData data = new(); source.CaptureSaveData(data);
            Assert.That(data.story.completedEventIds, Is.EqualTo(new[] { "a", "b" }));
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            StoryProgressManager target = new GameObject("StoryTarget").AddComponent<StoryProgressManager>();
            target.RestoreSaveData(data);
            Assert.That(target.CurrentChapter, Is.EqualTo(3)); Assert.That(target.MainProgress, Is.EqualTo(7)); Assert.That(target.IsEventCompleted("a"), Is.True);
        }

        [Test]
        public void RewardLedgerRestoreDoesNotGrantAndBlocksDuplicate()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            RewardService rewards = new GameObject("Rewards").AddComponent<RewardService>();
            GameSaveData data = new();
            data.reward.combatLedger.Add(new RewardLedgerSaveData { sourceType = RewardSourceType.Combat.ToString(), sourceId = "combat-1", gold = 50 });
            rewards.RestoreSaveData(data);
            Assert.That(wallet.Gold, Is.Zero);
            RewardGrantResult result = rewards.GrantReward(new RewardGrantRequest(RewardSourceType.Combat, "combat-1", 50, 0));
            Assert.That(result.DuplicateBlocked, Is.True);
            Assert.That(wallet.Gold, Is.Zero);
        }

        [Test]
        public void SchemaTwoCombatLedger_MigratesIntoCanonicalAllSourceLedger()
        {
            string json =
                "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":2}," +
                "\"reward\":{\"combatLedger\":[{\"sourceType\":\"Combat\",\"sourceId\":\"combat-old\",\"gold\":9,\"exp\":4}]}}";

            GameSaveData migrated = ReadSnapshotFromLegacyJson(json);

            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.reward.ledger, Has.Count.EqualTo(1));
            Assert.That(migrated.reward.ledger[0].sourceType, Is.EqualTo(RewardSourceType.Combat.ToString()));
            Assert.That(migrated.reward.ledger[0].sourceId, Is.EqualTo("combat-old"));
            Assert.That(migrated.reward.ledger[0].requestedGold, Is.EqualTo(9));
            Assert.That(migrated.reward.ledger[0].requestedExp, Is.EqualTo(4));
            Assert.That(migrated.reward.ledger[0].exp, Is.Zero);
            Assert.That(migrated.reward.ledger[0].partialFailure, Is.True);
        }

        [Test]
        public void SchemaThreeFlatQuests_MigrateIntoDefaultGroupAndPreserveCompletion()
        {
            string json =
                "{\"header\":{\"formatId\":\"GAME_002\",\"schemaVersion\":3}," +
                "\"quest\":{\"quests\":[" +
                "{\"questId\":\"active\",\"status\":\"Active\",\"objectives\":[{\"objectiveId\":\"kill\",\"progress\":1,\"requiredCount\":2}]}," +
                "{\"questId\":\"done\",\"completed\":true,\"status\":\"Completed\"}]}}";

            GameSaveData migrated = ReadSnapshotFromLegacyJson(json);

            Assert.That(migrated.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(migrated.quest.quests[0].status, Is.EqualTo(QuestStatus.Active.ToString()));
            Assert.That(migrated.quest.quests[0].activeGroupIndex, Is.Zero);
            Assert.That(migrated.quest.quests[0].attempt, Is.EqualTo(1));
            Assert.That(migrated.quest.quests[0].objectives.Single().progress, Is.EqualTo(1));
            Assert.That(migrated.quest.quests[1].status, Is.EqualTo(QuestStatus.Completed.ToString()));
            Assert.That(migrated.quest.quests[1].completed, Is.True);
        }

        [Test]
        public void MalformedSchemaFourQuestState_NormalizesDeterministically()
        {
            GameSaveData data = new();
            QuestStateSaveData quest = new()
            {
                questId = "malformed",
                status = "999",
                activeGroupIndex = -4,
                attempt = -2,
                failureReasonId = "should-clear"
            };
            quest.revealedObjectiveIds.Add("secret");
            quest.revealedObjectiveIds.Add("secret");
            data.quest.quests.Add(quest);

            GameSaveData normalized = ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(data));
            QuestStateSaveData result = normalized.quest.quests.Single();

            Assert.That(result.status, Is.EqualTo(QuestStatus.Inactive.ToString()));
            Assert.That(result.activeGroupIndex, Is.Zero);
            Assert.That(result.attempt, Is.Zero);
            Assert.That(result.failureReasonId, Is.Null);
            Assert.That(result.revealedObjectiveIds, Is.EqualTo(new[] { "secret" }));
        }

        [Test]
        public void InvalidAndDuplicateCanonicalLedgerEntries_NormalizeSafely()
        {
            GameSaveData data = new();
            data.reward.ledger.Add(new RewardLedgerSaveData
            {
                sourceType = RewardSourceType.Story.ToString(),
                sourceId = "story",
                actionId = "reward",
                gold = 3
            });
            data.reward.ledger.Add(new RewardLedgerSaveData
            {
                sourceType = RewardSourceType.Story.ToString(),
                sourceId = "story",
                actionId = "reward",
                gold = 99
            });
            data.reward.ledger.Add(new RewardLedgerSaveData { sourceType = "Invalid", sourceId = "bad" });
            data.reward.ledger.Add(new RewardLedgerSaveData { sourceType = "999", sourceId = "undefined-enum" });
            data.reward.ledger.Add(new RewardLedgerSaveData { sourceType = RewardSourceType.Choice.ToString(), sourceId = "" });

            GameSaveData normalized = ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(data));

            Assert.That(normalized.reward.ledger, Has.Count.EqualTo(1));
            Assert.That(normalized.reward.ledger[0].gold, Is.EqualTo(3));
        }

        [Test]
        public void RewardLedger_MoreThan256EntriesRoundTripsAndBlocksEarlyMiddleAndLateDuplicates()
        {
            CurrencyWallet wallet = new GameObject("Wallet").AddComponent<CurrencyWallet>();
            InventoryService inventory = new GameObject("Inventory").AddComponent<InventoryService>();
            RewardService source = new GameObject("RewardSource").AddComponent<RewardService>();
            SetField(source, "currencyWallet", wallet);
            SetField(source, "inventoryService", inventory);

            for (int i = 0; i < 300; i++)
            {
                RewardGrantResult result = source.GrantReward(new RewardGrantRequest(
                    RewardSourceType.Story, $"story-{i:D3}", 1, 2, "token", 1, "grant"));
                Assert.That(result.DuplicateBlocked, Is.False, i.ToString());
            }

            GameSaveData save = new();
            source.CaptureSaveData(save);
            Assert.That(save.reward.ledger, Has.Count.EqualTo(300));
            UnityEngine.Object.DestroyImmediate(source.gameObject);

            RewardService restored = new GameObject("RewardRestored").AddComponent<RewardService>();
            SetField(restored, "currencyWallet", wallet);
            SetField(restored, "inventoryService", inventory);
            restored.RestoreSaveData(ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(save)));
            foreach (int index in new[] { 0, 150, 299 })
            {
                RewardGrantResult duplicate = restored.GrantReward(new RewardGrantRequest(
                    RewardSourceType.Story, $"story-{index:D3}", 1, 2, "token", 1, "grant"));
                Assert.That(duplicate.DuplicateBlocked, Is.True, index.ToString());
                Assert.That(duplicate.Gold, Is.Zero);
                Assert.That(duplicate.Exp, Is.Zero);
                Assert.That(duplicate.ItemCount, Is.Zero);
            }

            Assert.That(wallet.Gold, Is.EqualTo(300));
            Assert.That(inventory.GetCount("token"), Is.EqualTo(300));
        }

        [Test]
        public void SaveNormalization_PreservesAllValidIdentityEntriesAndRequestedUnappliedExp()
        {
            GameSaveData data = new();
            QuestStateSaveData quest = new() { questId = "quest", status = QuestStatus.Active.ToString() };
            data.quest.quests.Add(quest);
            for (int i = 0; i < 300; i++)
            {
                quest.processedEventIds.Add($"quest-event-{i:D3}");
                data.reward.ledger.Add(new RewardLedgerSaveData
                {
                    sourceType = RewardSourceType.QuestCompletion.ToString(),
                    sourceId = $"quest-{i:D3}",
                    actionId = "complete",
                    requestedExp = 7,
                    exp = 0,
                    partialFailure = true
                });
            }

            GameSaveData normalized = ReadSnapshotFromLegacyJson(SaveSerializer.ToJson(data));

            Assert.That(normalized.quest.quests.Single().processedEventIds, Has.Count.EqualTo(300));
            Assert.That(normalized.reward.ledger, Has.Count.EqualTo(300));
            Assert.That(normalized.reward.ledger.All(entry => entry.requestedExp == 7 && entry.exp == 0), Is.True);
        }

        [Test]
        public void OversizedIdentityCollection_IsRejectedExplicitlyInsteadOfTruncated()
        {
            GameSaveData data = new();
            QuestStateSaveData quest = new() { questId = "hostile", status = QuestStatus.Active.ToString() };
            data.quest.quests.Add(quest);
            quest.processedEventIds.AddRange(Enumerable.Repeat("identity", 100001));

            Type validator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataValidator");
            MethodInfo method = validator.GetMethod("TryValidateCollectionSizes", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { data, null };

            Assert.That((bool)method.Invoke(null, args), Is.False);
            Assert.That(args[1] as string, Does.Contain("100001 identity records"));
            Assert.That(quest.processedEventIds, Has.Count.EqualTo(100001));
        }

        [Test]
        public void CanonicalDtoContainsNoUnityObjectFields()
        {
            Type[] dtoTypes = typeof(GameSaveData).Assembly.GetTypes().Where(type => type.Namespace == "Game.NonCombat.Save" && type.IsSerializable).ToArray();
            foreach (Type type in dtoTypes)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType), Is.False, $"{type.FullName}.{field.Name}");
        }

        [Test]
        public void CanonicalJsonExcludesTransientRuntimeNames()
        {
            string json = SaveSerializer.ToJson(new GameSaveData());
            Assert.That(json, Does.Not.Contain("CombatSession"));
            Assert.That(json, Does.Not.Contain("CombatTurn"));
            Assert.That(json, Does.Not.Contain("StoryEventRunner"));
            Assert.That(json, Does.Not.Contain("RewardUIPanel"));
        }

        [Test]
        public void OperationReturnsToIdleAfterSuccessAndFailure()
        {
            Assert.That(_service.TryLoad(out _), Is.False);
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
            Assert.That(_service.TrySave(out _), Is.True);
            Assert.That(_service.CurrentOperationState, Is.EqualTo(SaveLoadService.OperationState.Idle));
        }

        [Test]
        public void TestsUseInjectedTemporaryPath()
        {
            Assert.That(_service.PrimarySavePath, Is.EqualTo(_primary));
            Assert.That(_primary, Does.StartWith(Path.GetTempPath()));
            Assert.That(_primary, Is.Not.EqualTo(Path.Combine(Application.persistentDataPath, "game_save.json")));
        }

        private static GameSaveData ReadSnapshotFromLegacyJson(string json)
        {
            Type migrator = typeof(GameSaveData).Assembly.GetType("Game.NonCombat.Save.GameSaveDataMigrator");
            MethodInfo method = migrator.GetMethod("TryMigrate", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { json, null, false, null };
            Assert.That((bool)method.Invoke(null, args), Is.True, args[3] as string);
            return (GameSaveData)args[1];
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static string ProjectPath(string relative) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative.Replace('/', Path.DirectorySeparatorChar));

        private static void CleanupObjects()
        {
            foreach (MonoBehaviour item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item is SaveLoadService || item is InventoryService || item is CurrencyWallet || item is RewardService || item is StoryProgressManager || item is GameStateMachine || item is GameFlowController || item is InteractionRuntime || item is CharacterProgressionService || item is CalendarService || item is PartyRuntime || item is PersonaStatusManager || item is PersonaSaveAdapter || item is StoryFlagDatabase || item is QuestRuntime || item is QuestCalendarIntegration || item is DaySettlementFlow || item is ExplorationResourceRuntime || item is PersistentConditionRuntime || item is SupplyLoadoutService || item is CombatEncounterGroup)
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
        }
    }
}
