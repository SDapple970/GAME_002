using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Daily;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Core;
using Game.Story.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public sealed class SaveLoadSceneIntegrationTests
    {
        private const string TitleScenePath = "Assets/GAME/Scenes/TitleScene.unity";
        private const string DungeonSceneName = "Dungeon_1_Production";

        private string _directory;
        private string _primaryPath;
        private int _questCompletedDuringRestore;
        private bool _loadCompleted;
        private bool _loadSucceeded;
        private bool _flagConditionsAtLoadCompleted;
        private bool _titleServicesReady;
        private StoryEventDefinitionSO _flagStory;

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= ObserveSceneLoaded;
            if (EditorApplication.isPlaying)
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (_flagStory != null) UnityEngine.Object.DestroyImmediate(_flagStory);
            ResetSingletons();
            if (!string.IsNullOrWhiteSpace(_directory) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [UnityTest]
        [Category("SAV03")]
        public IEnumerator TitleNewGame_SaveLeaveAndColdLoad_RestoresProductionDungeonState()
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;

            SaveLoadService service = SaveLoadService.Instance;
            Assert.That(service, Is.Not.Null);
            PrepareTemporaryStorage(service);

            yield return StartNewGameFromTitle();
            yield return WaitForSceneAndState(DungeonSceneName, GameState.Exploration);

            AssertProductionParticipantsReady();
            AssertCanonicalCoreServices();

            CurrencyWallet wallet = CurrencyWallet.Instance;
            InventoryService inventory = InventoryService.Instance;
            RewardService rewards = RewardService.Instance;
            CalendarService calendar = CalendarService.Instance;
            QuestRuntime questRuntime = UnityEngine.Object.FindFirstObjectByType<QuestRuntime>();
            InteractionRuntime interaction = InteractionRuntime.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.That(new UnityEngine.Object[] { wallet, inventory, rewards, calendar, questRuntime, interaction, player }, Has.None.Null);

            QuestDefinitionSO definition = GetOnlyAuthoredDefinition(questRuntime);
            string questId = definition.QuestId;
            questRuntime.StartQuest(definition);
            Assert.That(questRuntime.GetQuestStatus(questId), Is.EqualTo(QuestStatus.Active));

            wallet.SetGold(12);
            inventory.AddItem("sav03.token", 2);
            Assert.That(calendar.TryAdvanceDays(2), Is.True);
            interaction.MarkConsumed("sav03.loot", InteractionUsePolicy.PersistentOnce);
            interaction.RememberResolvedOutcome("sav03.loot", "open", "rare");
            RewardGrantRequest reward = new(RewardSourceType.Story, "sav03.reward", 3, 0, "sav03.token", 1, "grant");
            Assert.That(rewards.GrantReward(reward).DuplicateBlocked, Is.False);

            yield return ChooseStoryFlagBranch();
            AssertFlagState();
            StoryFlagManager originalFlagOwner = StoryFlagManager.Instance;

            Vector3 savedPosition = player.transform.position + new Vector3(2.5f, 0f, 0f);
            player.transform.position = savedPosition;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
                body.linearVelocity = new Vector2(7f, -3f);

            Assert.That(service.TrySave(out string saveMessage), Is.True, saveMessage);
            Assert.That(File.Exists(_primaryPath), Is.True);
            string savedJson = File.ReadAllText(_primaryPath);
            Assert.That(savedJson, Does.Contain(DungeonSceneName));
            Assert.That(savedJson, Does.Contain("sav03.loot"));
            Assert.That(savedJson, Does.Contain("sav03.reward"));

            SceneFlowController.Instance.LoadScene("TitleScene");
            yield return WaitForSceneAndState("TitleScene", GameState.Title);
            Assert.That(SaveLoadService.Instance, Is.SameAs(service));
            Assert.That(StoryFlagManager.Instance, Is.SameAs(originalFlagOwner));
            AssertFlagState();

            // Run the production Title flow a second time so cold Load is proven
            // against a genuinely reset runtime rather than the pre-save memory state.
            yield return StartNewGameFromTitle();
            yield return WaitForSceneAndState(DungeonSceneName, GameState.Exploration);
            AssertNewGameRuntimeIsClean(questId);
            Assert.That(StoryFlagManager.Instance.HasFlag("b01.choice"), Is.False);
            Assert.That(StoryFlagManager.Instance.HasBool("b01.false"), Is.False);
            Assert.That(StoryFlagManager.Instance.HasInt("b01.zero"), Is.False);
            Assert.That(StoryFlagManager.Instance.HasInt("b01.count"), Is.False);
            Assert.That(SaveLoadService.Instance, Is.SameAs(service));

            _titleServicesReady = false;
            SceneFlowController.Instance.LoadScene("TitleScene", HandleTitleServicesReady);
            yield return WaitForSceneAndState("TitleScene", GameState.Title);
            yield return WaitForSceneAndLoadCompletion("TitleScene", () => _titleServicesReady);

            service = SaveLoadService.Instance;
            Assert.That(service, Is.Not.Null, "Title re-entry must retain the canonical SaveLoadService.");
            Assert.That(service.PrimarySavePath, Is.EqualTo(_primaryPath),
                "Title re-entry must retain the active save storage owner.");
            _questCompletedDuringRestore = 0;
            _loadCompleted = false;
            _loadSucceeded = false;
            _flagConditionsAtLoadCompleted = false;
            // Remove the old persistent owner: destination bootstrap must create a fresh
            // participant before restore and before OnLoadCompleted condition evaluation.
            UnityEngine.Object.Destroy(StoryFlagManager.Instance.gameObject);
            float flagDestroyDeadline = Time.realtimeSinceStartup + 10f;
            while (StoryFlagManager.Instance != null && Time.realtimeSinceStartup < flagDestroyDeadline)
                yield return null;
            Assert.That(StoryFlagManager.Instance, Is.Null);
            SceneManager.sceneLoaded += ObserveSceneLoaded;
            service.OnLoadCompleted += HandleLoadCompleted;
            try
            {
                GAME.Title.TitleSceneController title = UnityEngine.Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
                Button continueButton = new SerializedObject(title).FindProperty("continueButton").objectReferenceValue as Button;
                Assert.That(continueButton, Is.Not.Null);
                Assert.That(continueButton.interactable, Is.True);
                continueButton.onClick.Invoke();
                yield return WaitForSceneAndLoadCompletion(DungeonSceneName, () => _loadCompleted);
            }
            finally
            {
                service.OnLoadCompleted -= HandleLoadCompleted;
                SceneManager.sceneLoaded -= ObserveSceneLoaded;
            }

            Assert.That(_loadSucceeded, Is.True);
            Assert.That(_flagConditionsAtLoadCompleted, Is.True);
            Assert.That(StoryFlagManager.Instance, Is.Not.SameAs(originalFlagOwner));
            AssertFlagState();
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            AssertProductionParticipantsReady();
            AssertCanonicalCoreServices();

            wallet = CurrencyWallet.Instance;
            inventory = InventoryService.Instance;
            rewards = RewardService.Instance;
            calendar = CalendarService.Instance;
            questRuntime = UnityEngine.Object.FindFirstObjectByType<QuestRuntime>();
            interaction = InteractionRuntime.Instance;
            player = GameObject.FindGameObjectWithTag("Player");

            Assert.That(wallet.Gold, Is.EqualTo(15));
            Assert.That(inventory.GetCount("sav03.token"), Is.EqualTo(3));
            Assert.That(calendar.CurrentDay, Is.EqualTo(3));
            Assert.That(questRuntime.GetQuestStatus(questId), Is.EqualTo(QuestStatus.Active));
            Assert.That(questRuntime.GetObjectiveProgress(questId, definition.Objectives[0].ObjectiveId), Is.Zero);
            Assert.That(questRuntime.TryGetDefinition(questId, out QuestDefinitionSO restoredDefinition), Is.True);
            Assert.That(restoredDefinition, Is.SameAs(definition));
            Assert.That(_questCompletedDuringRestore, Is.Zero);
            Assert.That(interaction.IsConsumed("sav03.loot", InteractionUsePolicy.PersistentOnce), Is.True);
            Assert.That(interaction.TryGetResolvedOutcome("sav03.loot", "open", out string outcome), Is.True);
            Assert.That(outcome, Is.EqualTo("rare"));
            Assert.That(Vector3.Distance(player.transform.position, savedPosition), Is.LessThan(0.01f));
            if (body != null)
            {
                Rigidbody2D restoredBody = player.GetComponent<Rigidbody2D>();
                Assert.That(restoredBody.linearVelocity, Is.EqualTo(Vector2.zero));
            }

            int goldBeforeDuplicate = wallet.Gold;
            int itemBeforeDuplicate = inventory.GetCount("sav03.token");
            Assert.That(rewards.GrantReward(reward).DuplicateBlocked, Is.True);
            Assert.That(wallet.Gold, Is.EqualTo(goldBeforeDuplicate));
            Assert.That(inventory.GetCount("sav03.token"), Is.EqualTo(itemBeforeDuplicate));

            StoryFlagManager.Instance.SetBool("b01.stale", true);
            StoryFlagManager.Instance.SetInt("b01.count", -99);
            Assert.That(service.TryLoad(out string repeatedLoadMessage), Is.True, repeatedLoadMessage);
            AssertFlagState();
            Assert.That(StoryFlagManager.Instance.HasFlag("b01.stale"), Is.False);
            Assert.That(wallet.Gold, Is.EqualTo(15));
            Assert.That(inventory.GetCount("sav03.token"), Is.EqualTo(3));
            Assert.That(calendar.CurrentDay, Is.EqualTo(3));
            Assert.That(Vector3.Distance(player.transform.position, savedPosition), Is.LessThan(0.01f));
            AssertCanonicalCoreServices();
            Assert.That(File.ReadAllText(_primaryPath), Is.EqualTo(savedJson));

            yield return new ExitPlayMode();
        }

        private void HandleLoadCompleted(bool succeeded, string _)
        {
            _loadSucceeded = succeeded;
            _loadCompleted = true;
            _flagConditionsAtLoadCompleted = FlagConditionsMatch();
        }

        private void HandleTitleServicesReady(bool success) => _titleServicesReady = success;

        private IEnumerator ChooseStoryFlagBranch()
        {
            // Clone existing authored dialogue in memory; no story asset is changed.
            _flagStory = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset"));
            _flagStory.hideFlags = HideFlags.HideAndDontSave;
            SetField(_flagStory, "eventId", "b01.flag-choice");
            StoryNode choiceNode = _flagStory.GetNode("first-talk-choice");
            SetField(choiceNode, "useTimedChoices", false);
            SetField(_flagStory.GetNode("first-talk-terminal"), "effects", new List<StoryEffect>());
            SetField(choiceNode.Choices[0], "effects", new List<StoryEffect>
            {
                FlagEffect(StoryEffectType.SetBoolFlag, "b01.choice", true, 0),
                FlagEffect(StoryEffectType.SetBoolFlag, "b01.false", false, 0),
                FlagEffect(StoryEffectType.SetIntFlag, "b01.count", false, 17),
                FlagEffect(StoryEffectType.SetIntFlag, "b01.zero", false, 0)
            });
            StoryEventRunner runner = UnityEngine.Object.FindFirstObjectByType<StoryEventRunner>();
            Assert.That(runner.TryStartEvent(_flagStory), Is.True);
            yield return null;
            runner.Advance();
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Choice));
            Assert.That(runner.CurrentResolvedChoices, Has.Count.EqualTo(2));
            runner.SelectChoiceByIndex(0);
            yield return null;
            runner.Advance();
            yield return WaitForSceneAndState(DungeonSceneName, GameState.Exploration);
        }

        private static StoryEffect FlagEffect(StoryEffectType type, string key, bool boolean, int integer)
        {
            StoryEffect effect = new();
            SetField(effect, "type", type);
            SetField(effect, "key", key);
            SetField(effect, "boolValue", boolean);
            SetField(effect, "intValue", integer);
            return effect;
        }

        private static bool FlagConditionsMatch()
        {
            StoryCondition boolean = new();
            SetField(boolean, "type", Game.Story.Data.StoryConditionType.BoolFlagEquals);
            SetField(boolean, "key", "b01.choice");
            SetField(boolean, "boolValue", true);
            StoryCondition integer = new();
            SetField(integer, "type", Game.Story.Data.StoryConditionType.IntFlagAtLeast);
            SetField(integer, "key", "b01.count");
            SetField(integer, "intValue", 17);
            return boolean.IsMet() && integer.IsMet();
        }

        private static void AssertFlagState()
        {
            Assert.That(StoryFlagManager.Instance, Is.Not.Null);
            Assert.That(FlagConditionsMatch(), Is.True);
            Assert.That(StoryFlagManager.Instance.HasBool("b01.false"), Is.True);
            Assert.That(StoryFlagManager.Instance.GetBool("b01.false"), Is.False);
            Assert.That(StoryFlagManager.Instance.HasInt("b01.zero"), Is.True);
            Assert.That(StoryFlagManager.Instance.GetInt("b01.zero"), Is.Zero);
            Assert.That(StoryFlagManager.Instance.HasFlag("b01.unset"), Is.False);
            Assert.That(UnityEngine.Object.FindObjectsByType<StoryFlagManager>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private IEnumerator StartNewGameFromTitle()
        {
            GAME.Title.TitleSceneController title = UnityEngine.Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Assert.That(title, Is.Not.Null);
            yield return new WaitForEndOfFrame();

            SerializedObject serializedTitle = new(title);
            Button start = serializedTitle.FindProperty("startButton").objectReferenceValue as Button;
            Button paper = serializedTitle.FindProperty("paperClickButton").objectReferenceValue as Button;
            Assert.That(new[] { start, paper }, Has.None.Null);

            start.onClick.Invoke();
            yield return WaitForButton(paper);
            paper.onClick.Invoke();
            yield return WaitForButton(paper);
            paper.onClick.Invoke();
        }

        private static IEnumerator WaitForButton(Button button)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!button.interactable && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(button.interactable, Is.True, "Title request flow did not advance.");
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }

        private static IEnumerator WaitForSceneAndState(string sceneName, GameState state)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != sceneName || GameStateMachine.Instance == null || GameStateMachine.Instance.Current != state) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            Assert.That(GameStateMachine.Instance?.Current, Is.EqualTo(state));
        }

        private static IEnumerator WaitForSceneAndLoadCompletion(string sceneName, Func<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != sceneName || !completed()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            Assert.That(completed(), Is.True, "SaveLoadService did not complete the scene restore.");
        }

        private void ObserveSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != DungeonSceneName)
                return;

            QuestRuntime runtime = UnityEngine.Object.FindFirstObjectByType<QuestRuntime>();
            if (runtime != null)
                runtime.OnQuestCompleted += _ => _questCompletedDuringRestore++;
        }

        private void PrepareTemporaryStorage(SaveLoadService service)
        {
            _directory = Path.Combine(Path.GetTempPath(), "GAME_002_SAV03_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _primaryPath = Path.Combine(_directory, "game_save.json");
            typeof(SaveLoadService).GetMethod("SetStoragePathForTests", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(service, new object[] { _primaryPath });
        }

        private static QuestDefinitionSO GetOnlyAuthoredDefinition(QuestRuntime runtime)
        {
            QuestDefinitionSO[] definitions = typeof(QuestRuntime)
                .GetField("questDefinitions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runtime) as QuestDefinitionSO[];
            Assert.That(definitions, Is.Not.Null.And.Length.EqualTo(1));
            return definitions[0];
        }

        private static void AssertProductionParticipantsReady()
        {
            Assert.That(UnityEngine.Object.FindFirstObjectByType<QuestRuntime>(), Is.Not.Null);
            Assert.That(CalendarService.Instance, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<QuestCalendarIntegration>(), Is.Not.Null);
            Assert.That(RewardService.Instance, Is.Not.Null);
            Assert.That(InventoryService.Instance, Is.Not.Null);
            Assert.That(CurrencyWallet.Instance, Is.Not.Null);
            Assert.That(InteractionRuntime.Instance, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<InteractionRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(item => item != null && item.enabled), Is.EqualTo(1));
        }

        private static void AssertCanonicalCoreServices()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<GameStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<SceneFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<SaveLoadService>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<RewardService>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        private static void AssertNewGameRuntimeIsClean(string questId)
        {
            AssertProductionParticipantsReady();
            AssertCanonicalCoreServices();
            Assert.That(CurrencyWallet.Instance.Gold, Is.Zero);
            Assert.That(InventoryService.Instance.GetCount("sav03.token"), Is.Zero);
            Assert.That(CalendarService.Instance.CurrentDay, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<QuestRuntime>().GetQuestStatus(questId), Is.EqualTo(QuestStatus.Inactive));
            Assert.That(InteractionRuntime.Instance.IsConsumed("sav03.loot", InteractionUsePolicy.PersistentOnce), Is.False);
        }

        private static void ResetSingletons()
        {
            foreach (Type type in typeof(SaveLoadService).Assembly.GetTypes())
            {
                FieldInfo instance = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                if (instance != null && typeof(UnityEngine.Object).IsAssignableFrom(instance.FieldType))
                    instance.SetValue(null, null);
            }
        }
    }
}
