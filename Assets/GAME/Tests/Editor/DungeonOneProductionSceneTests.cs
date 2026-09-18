using System.Collections;
using System.Linq;
using System.Reflection;
using Game.CameraSys;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission;
using Game.DemoMission.Runtime;
using Game.EditorTools;
using Game.Interaction;
using Game.Player;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Data;
using Game.Story.Interaction;
using Game.UI;
using Game.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public sealed class DungeonOneProductionSceneTests
    {
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (System.Type type in typeof(SaveLoadService).Assembly.GetTypes())
            {
                FieldInfo instance = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                if (instance != null && typeof(Object).IsAssignableFrom(instance.FieldType))
                    instance.SetValue(null, null);
            }
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_IsCleanEnvironmentMigration()
        {
            Scene scene = EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.path, Is.EqualTo(DungeonOneProductionMigrationUtility.ProductionScenePath));
            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            string[] roots = scene.GetRootGameObjects().Select(item => item.name).ToArray();
            Assert.That(roots, Does.Contain("Runtime"));
            Assert.That(roots, Does.Contain("World"));
            Assert.That(roots, Does.Contain("Actors"));
            Assert.That(roots, Does.Contain("Main Camera"));
            Assert.That(roots, Does.Not.Contain("Systems"));
            Assert.That(roots, Does.Not.Contain("Mission"));
            Assert.That(roots, Does.Not.Contain("Enemies"));
            Assert.That(roots, Does.Not.Contain("UI"));
        }

        [Test]
        [Category("D108Gate")]
        public void RuntimeDestinationsAndBuildSettingsUseProductionDungeonOne()
        {
            const string titlePath = "Assets/GAME/Scenes/TitleScene.unity";
            const string casePath = "Assets/GAME/Data/Tutorial/Case_Dungeon01_1.asset";

            EditorSceneManager.OpenScene(titlePath, OpenSceneMode.Single);
            GAME.Title.TitleSceneController titleController = Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Assert.That(titleController, Is.Not.Null);
            Assert.That(new SerializedObject(titleController).FindProperty("dungeonSceneName").stringValue,
                Is.EqualTo("Dungeon_1_Production"));

            CaseFileDataSO caseFile = AssetDatabase.LoadAssetAtPath<CaseFileDataSO>(casePath);
            Assert.That(caseFile, Is.Not.Null);
            Assert.That(caseFile.TargetSceneName, Is.EqualTo("Dungeon_1_Production"));
            Assert.That(caseFile.TargetSpawnPointId, Is.EqualTo("Dungeon1_Start"));

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            Assert.That(enabledScenes, Does.Contain(titlePath));
            Assert.That(enabledScenes, Does.Contain(DungeonOneProductionMigrationUtility.ProductionScenePath));
            Assert.That(enabledScenes, Does.Not.Contain(DungeonOneProductionMigrationUtility.LegacyScenePath));
            Assert.That(DungeonOneProductionMigrationUtility.LegacyScenePath,
                Is.EqualTo("Assets/GAME/Scenes/Dungeon 1.unity"));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_PreservesDungeonOneStartSpawnContract()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            SceneSpawnPoint[] spawnPoints = Object.FindObjectsByType<SceneSpawnPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(spawnPoints, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(spawnPoints[0].transform), Is.EqualTo("World/SpawnPoints/PlayerSpawn"));
            Assert.That(spawnPoints[0].SpawnPointId, Is.EqualTo("Dungeon1_Start"));

            GameObject player = GameObject.FindWithTag("Player");
            Assert.That(player, Is.Not.Null);
            Assert.That(Vector3.Distance(player.transform.position, spawnPoints[0].transform.position),
                Is.LessThan(0.001f));
        }

        [Test]
        [Category("D108Gate")]
        public void SceneTravelService_RemainsCompatibleAndDelegatesProductionLoadingToSceneFlow()
        {
            MethodInfo staticApi = typeof(SceneTravelService).GetMethod(
                "TravelTo",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            MethodInfo instanceApi = typeof(SceneTravelService).GetMethod(
                "Travel",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            Assert.That(staticApi, Is.Not.Null);
            Assert.That(instanceApi, Is.Not.Null);

            string travelSource = System.IO.File.ReadAllText("Assets/GAME/Scripts/Story/SceneTravelService.cs");
            string flowSource = System.IO.File.ReadAllText("Assets/GAME/Scripts/Core/SceneFlowController.cs");
            string titleSource = System.IO.File.ReadAllText("Assets/GAME/Scripts/Title/Runtime/TitleSceneController.cs");
            Assert.That(travelSource, Does.Contain("sceneFlow.LoadScene(sceneName, HandleSceneLoadCompleted)"));
            Assert.That(travelSource, Does.Not.Contain("SceneManager.LoadSceneAsync"));
            Assert.That(travelSource, Does.Not.Contain("GameStateMachine.Instance.SetState"));
            Assert.That(titleSource, Does.Contain("sceneFlow.LoadScene(sceneName)"));
            Assert.That(titleSource, Does.Not.Contain("SceneManager.LoadScene"));
            Assert.That(flowSource, Does.Contain("SceneManager.LoadSceneAsync"));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_HasQuestDerivedCompletionContractWithoutLegacyExitOrFallbackDestination()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            DungeonCompletionFlow[] completionFlows = FindAll<DungeonCompletionFlow>();
            QuestRuntime questRuntime = FindAll<QuestRuntime>().Single();
            Assert.That(completionFlows, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(completionFlows[0].transform), Is.EqualTo("Runtime/DungeonCompletion"));
            Assert.That(new SerializedObject(completionFlows[0]).FindProperty("questRuntime").objectReferenceValue,
                Is.SameAs(questRuntime));
            Assert.That(completionFlows[0].DungeonId, Is.EqualTo("dungeon1"));
            Assert.That(completionFlows[0].CompletionQuestId, Is.EqualTo("ch01.find-first-npc"));
            Assert.That(completionFlows[0].HasAuthoredDestination, Is.False);
            Assert.That(Object.FindObjectsByType<DemoRescueNpcEndFlow>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);

            string completionSource = System.IO.File.ReadAllText(
                "Assets/GAME/Scripts/World/DungeonCompletionFlow.cs");
            Assert.That(completionSource, Does.Contain("SceneFlowController"));
            Assert.That(completionSource, Does.Not.Contain("SceneManager.LoadScene"));
            Assert.That(completionSource, Does.Not.Contain("GameStateMachine.Instance.SetState"));
            Assert.That(completionSource, Does.Not.Contain("RewardService"));
            Assert.That(completionSource, Does.Not.Contain("GrantQuestCompletion"));
        }

        [Test]
        [Category("D108Gate")]
        public void DungeonCompletionFlow_DerivesItsRequestFromPersistedQuestStateWithoutGrantingRewardsOrTraveling()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");
            GameObject owner = new("DungeonCompletionFlowTestOwner");

            try
            {
                QuestRuntime runtime = owner.AddComponent<QuestRuntime>();
                DungeonCompletionFlow completionFlow = owner.AddComponent<DungeonCompletionFlow>();
                SetReference(completionFlow, "questRuntime", runtime);
                SetPrivateField(completionFlow, "dungeonId", "dungeon1");
                SetPrivateField(completionFlow, "completionQuestId", definition.QuestId);
                SetPrivateField(completionFlow, "destinationSceneName", string.Empty);
                SetPrivateField(completionFlow, "destinationSpawnPointId", string.Empty);
                SetPrivateField(completionFlow, "travelWhenCompletionReady", false);
                InvokeIfPresent(completionFlow, "OnEnable");

                int requests = 0;
                DungeonCompletionRequest request = default;
                completionFlow.OnCompletionReady += value =>
                {
                    requests++;
                    request = value;
                };

                runtime.StartQuest(definition);
                runtime.CompleteQuest(definition.QuestId);

                Assert.That(completionFlow.IsCompletionReady, Is.True);
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(request.DungeonId, Is.EqualTo("dungeon1"));
                Assert.That(request.CompletionQuestId, Is.EqualTo(definition.QuestId));
                Assert.That(request.DestinationSceneName, Is.Empty);
                Assert.That(completionFlow.TryTravelToAuthoredDestination(), Is.False);

                Game.NonCombat.Save.GameSaveData snapshot = new();
                runtime.CaptureSaveData(snapshot);
                runtime.RestoreSaveData(snapshot);

                Assert.That(completionFlow.IsCompletionReady, Is.True);
                Assert.That(requests, Is.EqualTo(2),
                    "Restored QuestRuntime state must be sufficient to reconstruct the completion contract.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("D108Gate")]
        public void SceneTravelService_ValidSpawnUsesTheExactMatchingPoint()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject player = CreatePlayerAt(new Vector3(12f, 4f, 0f));
            CreateSpawnPoint("Exact", new Vector3(3f, 7f, 0f));
            CreateSpawnPoint("Other", new Vector3(-4f, 2f, 0f));

            InvokeSpawnMove("Exact");

            Assert.That(player.transform.position, Is.EqualTo(new Vector3(3f, 7f, 0f)));
        }

        [Test]
        [Category("D108Gate")]
        public void SceneTravelService_MissingSpawnKeepsTheAuthoredPlayerPosition()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Vector3 authoredPosition = new(12f, 4f, 0f);
            GameObject player = CreatePlayerAt(authoredPosition);
            CreateSpawnPoint("Other", new Vector3(-4f, 2f, 0f));

            InvokeSpawnMove("Missing");

            Assert.That(player.transform.position, Is.EqualTo(authoredPosition));
        }

        [Test]
        [Category("D108Gate")]
        public void SceneTravelService_DuplicateSpawnIsAmbiguousAndDoesNotTeleport()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Vector3 authoredPosition = new(12f, 4f, 0f);
            GameObject player = CreatePlayerAt(authoredPosition);
            CreateSpawnPoint("Duplicate", new Vector3(3f, 7f, 0f));
            CreateSpawnPoint("Duplicate", new Vector3(-4f, 2f, 0f));

            InvokeSpawnMove("Duplicate");

            Assert.That(player.transform.position, Is.EqualTo(authoredPosition));
        }

        [UnityTest]
        [Category("D108Gate")]
        public IEnumerator TitleScene_StartFlowLoadsProductionDungeonThroughProductionSceneFlow()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/TitleScene.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;

            GAME.Title.TitleSceneController titleController = Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Assert.That(titleController, Is.Not.Null);
            SerializedObject serializedTitle = new(titleController);
            Button startButton = serializedTitle.FindProperty("startButton").objectReferenceValue as Button;
            Button paperClickButton = serializedTitle.FindProperty("paperClickButton").objectReferenceValue as Button;
            Assert.That(startButton, Is.Not.Null);
            Assert.That(paperClickButton, Is.Not.Null);

            float deadline = Time.realtimeSinceStartup + 30f;
            startButton.onClick.Invoke();
            while (!paperClickButton.interactable && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(paperClickButton.interactable, Is.True, "NPC request paper did not become interactive.");

            paperClickButton.onClick.Invoke();
            while (!paperClickButton.interactable && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(paperClickButton.interactable, Is.True, "Monster request paper did not become interactive.");

            paperClickButton.onClick.Invoke();
            while ((SceneManager.GetActiveScene().name != "Dungeon_1_Production" ||
                    GameStateMachine.Instance == null ||
                    GameStateMachine.Instance.Current != GameState.Exploration) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            string loadedSceneName = SceneManager.GetActiveScene().name;
            GameState? loadedState = GameStateMachine.Instance != null
                ? GameStateMachine.Instance.Current
                : null;

            yield return new ExitPlayMode();

            Assert.That(loadedSceneName, Is.EqualTo("Dungeon_1_Production"));
            Assert.That(loadedState, Is.EqualTo(GameState.Exploration));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_HasCanonicalCombatRewardUiCameraAndBootstrapOwners()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);
            Assert.That(FindAll<RuntimeBootstrapper>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<CombatEntryPoint>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<CombatWorldLifecycleAdapter>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<CombatRewardUIBinder>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<RewardService>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<RewardUIPanel>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<UIScreenRouter>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<GameUIRootController>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<EventSystem>(), Has.Length.EqualTo(1));

            CombatEntryPoint entryPoint = FindAll<CombatEntryPoint>().Single();
            Assert.That(GetHierarchyPath(entryPoint.transform),
                Is.EqualTo("Runtime/Combat/CombatRuntime"));

            CombatEncounterTrigger2D[] triggers = FindAll<CombatEncounterTrigger2D>();
            Assert.That(triggers, Has.Length.EqualTo(3));
            foreach (CombatEncounterTrigger2D trigger in triggers)
            {
                Assert.That(new SerializedObject(trigger).FindProperty("entryPoint").objectReferenceValue,
                    Is.SameAs(entryPoint),
                    GetHierarchyPath(trigger.transform));
            }

            PlayerFieldAttackController[] fieldAttacks = FindAll<PlayerFieldAttackController>();
            Assert.That(fieldAttacks, Has.Length.EqualTo(1));
            Assert.That(new SerializedObject(fieldAttacks[0]).FindProperty("entryPoint").objectReferenceValue,
                Is.SameAs(entryPoint));

            CombatRewardUIBinder binder = FindAll<CombatRewardUIBinder>().Single();
            SerializedObject serializedBinder = new(binder);
            Assert.That(serializedBinder.FindProperty("entryPoint").objectReferenceValue, Is.SameAs(entryPoint));
            Assert.That(serializedBinder.FindProperty("rewardPanel").objectReferenceValue,
                Is.SameAs(FindAll<RewardUIPanel>().Single()));
            Object rewardServiceReference = serializedBinder.FindProperty("rewardService").objectReferenceValue;
            Assert.That(
                rewardServiceReference == null || rewardServiceReference == FindAll<RewardService>().Single(),
                Is.True,
                "The binder may resolve the unique persistent/local RewardService at runtime, but must not reference a competing service.");

            GameObject player = GameObject.FindWithTag("Player");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(player.GetComponentsInChildren<Collider2D>(true), Is.Not.Empty);
            CameraFollow2D cameraFollow = FindAll<CameraFollow2D>().Single();
            Assert.That(cameraFollow.GetTarget(), Is.Not.Null);
            Assert.That(
                cameraFollow.GetTarget() == player.transform ||
                cameraFollow.GetTarget().IsChildOf(player.transform) ||
                player.transform.IsChildOf(cameraFollow.GetTarget()),
                Is.True);
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            InteractableObject[] interactables = Object.FindObjectsByType<InteractableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(interactables, Has.Length.EqualTo(1));

            InteractableObject npc = interactables[0];
            Assert.That(GetHierarchyPath(npc.transform), Is.EqualTo("Actors/NPCs/Dungeon1_FirstTalkNpc"));
            Assert.That(Vector3.Distance(npc.transform.position, new Vector3(187.105f, 7.698f, 0f)), Is.LessThan(0.001f));
            Assert.That(npc.PromptText, Is.EqualTo("F: 대화"));
            Assert.That(npc.InteractionId, Is.EqualTo("dungeon1.npc.first-talk"));
            Assert.That(npc.UsePolicy, Is.EqualTo(InteractionUsePolicy.Repeatable));
            Assert.That(npc.Events, Has.Count.EqualTo(1));
            Assert.That(npc.Events[0], Is.TypeOf<StoryInteractionEventSO>());
            Assert.That(npc.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(npc.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(npc.GetComponent<InteractionController>(), Is.Null);
            Assert.That(npc.GetComponent<QuestRuntime>(), Is.Null);
            Assert.That(npc.GetComponentInChildren<StoryInteractable2D>(true), Is.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npc.gameObject), Is.EqualTo(
                "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab"));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_HasExplicitInteractionOwnersWithoutLegacyInteractionPaths()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            InteractionRuntime[] runtimes = Object.FindObjectsByType<InteractionRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            InteractionRunner[] runners = Object.FindObjectsByType<InteractionRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(runtimes, Has.Length.EqualTo(1));
            Assert.That(runners, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(runtimes[0].transform), Is.EqualTo("Runtime/Interaction"));
            Assert.That(runners[0].transform, Is.SameAs(runtimes[0].transform));
            Assert.That(runners[0].Runtime, Is.SameAs(runtimes[0]));
            Assert.That(Object.FindObjectsByType<DemoMissionRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<RescueNpcActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<DemoRescueNpcEndFlow>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<StoryInteractionController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void RuntimeBootstrapper_ReusesExplicitInteractionOwners()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject interactionOwner = new("ExplicitInteractionOwner");
            GameObject bootstrapOwner = new("InteractionBootstrapTest");
            bootstrapOwner.SetActive(false);

            try
            {
                InteractionRuntime runtime = interactionOwner.AddComponent<InteractionRuntime>();
                InteractionRunner runner = interactionOwner.AddComponent<InteractionRunner>();
                RuntimeBootstrapper bootstrapper = bootstrapOwner.AddComponent<RuntimeBootstrapper>();

                MethodInfo bootstrap = typeof(RuntimeBootstrapper).GetMethod(
                    "BootstrapCoreServices",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.Invoke(bootstrapper, new object[] { false, false, false });

                Assert.That(Object.FindObjectsByType<InteractionRuntime>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(Object.FindObjectsByType<InteractionRunner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(runner.Runtime, Is.SameAs(runtime));
                Assert.That(runner.IsCompatibilityFallback, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapOwner);
                Object.DestroyImmediate(interactionOwner);
            }
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_NpcFirstTalkUsesChoiceFreeLegacyContentThroughStoryContract()
        {
            StoryInteractionEventSO interactionEvent = AssetDatabase.LoadAssetAtPath<StoryInteractionEventSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkStoryInteraction.asset");
            StoryEventDefinitionSO dialogue = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset");

            Assert.That(interactionEvent, Is.Not.Null);
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(interactionEvent.EventDefinition, Is.SameAs(dialogue));
            Assert.That(interactionEvent.ActionId, Is.EqualTo("dungeon1.npc.first-talk.interact"));
            Assert.That(dialogue.EventId, Is.EqualTo("dungeon1.npc.first-talk.story"));
            Assert.That(dialogue.Nodes, Has.Count.EqualTo(1));

            StoryNode firstTalk = dialogue.Nodes[0];
            Assert.That(firstTalk.NodeId, Is.EqualTo("first-talk"));
            Assert.That(firstTalk.SpeakerName, Is.EqualTo("NPC"));
            Assert.That(firstTalk.Body, Is.EqualTo("여기 ㅈㄴ 위험한 곳임. 들어갈거임?"));
            Assert.That(firstTalk.Choices, Is.Empty);
            Assert.That(firstTalk.Effects, Has.Count.EqualTo(1));
            Assert.That(firstTalk.EndEvent, Is.True);

            SerializedProperty effect = new SerializedObject(dialogue)
                .FindProperty("nodes").GetArrayElementAtIndex(0)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Assert.That(effect.FindPropertyRelative("type").intValue, Is.EqualTo((int)StoryEffectType.PublishQuestEvent));
            Assert.That(effect.FindPropertyRelative("missionId").stringValue, Is.EqualTo("ch01.find-first-npc"));
            Assert.That(effect.FindPropertyRelative("objectiveId").stringValue, Is.EqualTo("talk_first_npc"));
            Assert.That(effect.FindPropertyRelative("questEventType").intValue, Is.EqualTo((int)QuestEventType.Talk));
            Assert.That(effect.FindPropertyRelative("intValue").intValue, Is.EqualTo(1));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_HasApprovedQuestDefinitionOnItsCanonicalRuntime()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.QuestId, Is.EqualTo("ch01.find-first-npc"));
            Assert.That(definition.QuestTitle, Is.EqualTo("낯선 장소"));
            Assert.That(definition.MissionDayCost, Is.Zero);
            Assert.That(definition.RewardGold, Is.Zero);
            Assert.That(definition.RewardExp, Is.Zero);
            Assert.That(definition.Objectives, Has.Length.EqualTo(1));
            Assert.That(definition.Objectives[0].ObjectiveId, Is.EqualTo("talk_first_npc"));
            Assert.That(definition.Objectives[0].EventType, Is.EqualTo(QuestEventType.Talk));
            Assert.That(definition.Objectives[0].TargetId, Is.EqualTo("dungeon1.npc.first-talk"));
            Assert.That(definition.Objectives[0].RequiredCount, Is.EqualTo(1));

            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);
            QuestRuntime[] runtimes = Object.FindObjectsByType<QuestRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(runtimes, Has.Length.EqualTo(1));

            SerializedProperty definitions = new SerializedObject(runtimes[0]).FindProperty("questDefinitions");
            Assert.That(definitions.arraySize, Is.EqualTo(1));
            Assert.That(definitions.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(definition));
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_FirstTalkStoryStartsAndRestoresExploration()
        {
            StoryInteractionEventSO interactionEvent = AssetDatabase.LoadAssetAtPath<StoryInteractionEventSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkStoryInteraction.asset");
            GameObject core = new("DungeonOneFirstTalkTestCore");
            GameObject narrative = new("DungeonOneFirstTalkTestNarrative");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);

                Assert.That(runner.TryStartEvent(interactionEvent.EventDefinition), Is.True);
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Dialogue));

                runner.Advance();

                Assert.That(runner.IsRunning, Is.False);
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_FirstTalkPublishesItsQuestObjectiveOnlyOnce()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");
            StoryEventDefinitionSO dialogue = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset");
            GameObject core = new("DungeonOneQuestTestCore");
            GameObject narrative = new("DungeonOneQuestTestNarrative");
            GameObject trackerObject = new("DungeonOneQuestTestTracker");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                QuestRuntime runtime = core.AddComponent<QuestRuntime>();
                trackerObject.SetActive(false);
                QuestObjectiveTracker tracker = trackerObject.AddComponent<QuestObjectiveTracker>();
                SerializedObject serializedTracker = new(tracker);
                serializedTracker.FindProperty("questRuntime").objectReferenceValue = runtime;
                serializedTracker.ApplyModifiedPropertiesWithoutUndo();
                trackerObject.SetActive(true);
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(tracker);
                InvokeIfPresent(tracker, "OnEnable");
                runtime.StartQuest(definition);

                Assert.That(runtime.ApplyEvent(new QuestEvent(
                    QuestEventType.Talk,
                    definition.QuestId,
                    "talk_first_npc",
                    1,
                    null,
                    "unrelated-talk",
                    "other.npc")), Is.False);
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.Zero);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);

                Assert.That(runner.TryStartEvent(dialogue), Is.True);
                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.EqualTo(1));

                runner.Advance();
                Assert.That(runner.TryStartEvent(dialogue), Is.True);
                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(runtime.GetObjectiveProgress(definition.QuestId, "talk_first_npc"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(trackerObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        [Category("D108Gate")]
        public void ProductionDungeonOne_SceneStartAdapterUsesCanonicalRunnerAndIntroStory()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");

            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            SceneStartStoryEventAdapter[] adapters = Object.FindObjectsByType<SceneStartStoryEventAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            StoryEventRunner[] runners = Object.FindObjectsByType<StoryEventRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(adapters, Has.Length.EqualTo(1));
            Assert.That(runners, Has.Length.EqualTo(1));
            Assert.That(intro, Is.Not.Null);
            Assert.That(intro.EventId, Is.EqualTo("dungeon1.intro.story"));
            Assert.That(intro.Nodes, Has.Count.EqualTo(2));

            SerializedObject adapter = new(adapters[0]);
            Assert.That(adapter.FindProperty("runner").objectReferenceValue, Is.SameAs(runners[0]));
            Assert.That(adapter.FindProperty("eventDefinition").objectReferenceValue, Is.SameAs(intro));

            SerializedProperty effect = new SerializedObject(intro)
                .FindProperty("nodes").GetArrayElementAtIndex(1)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Assert.That(effect.FindPropertyRelative("type").intValue, Is.EqualTo((int)StoryEffectType.StartQuest));
            Assert.That(effect.FindPropertyRelative("questDefinition").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                    "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset")));
            SerializedProperty completionEffect = new SerializedObject(intro)
                .FindProperty("nodes").GetArrayElementAtIndex(1)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(1);
            Assert.That(completionEffect.FindPropertyRelative("type").intValue,
                Is.EqualTo((int)StoryEffectType.MarkEventCompleted));
            Assert.That(completionEffect.FindPropertyRelative("key").stringValue,
                Is.EqualTo("dungeon1.intro.story"));
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterReevaluatesAfterCoreBecomesAvailable()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject narrative = new("DungeonOneDelayedBootstrapNarrative");
            GameObject adapterObject = new("DungeonOneDelayedBootstrapAdapter");
            GameObject core = new("DungeonOneDelayedBootstrapCore");

            try
            {
                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                int starts = 0;
                runner.OnEventStarted += _ => starts++;
                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                Assert.That(runner.IsRunning, Is.False);

                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeIfPresent(adapter, "OnEnable");

                Assert.That(runner.IsRunning, Is.True);
                Assert.That(starts, Is.EqualTo(1));
                InvokeIfPresent(adapter, "OnEnable");
                Assert.That(starts, Is.EqualTo(1));
                runner.EndEvent();
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        [Category("KnownD105Blocker")]
        public void ProductionDungeonOne_SceneStartAdapterDefersUntilBootstrapUiReadinessIsPublished()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject core = new("DungeonOneBootstrapReadyCore");
            GameObject bootstrapObject = new("DungeonOneBootstrapReadyBootstrap");
            GameObject narrative = new("DungeonOneBootstrapReadyNarrative");
            GameObject adapterObject = new("DungeonOneBootstrapReadyAdapter");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);

                RuntimeBootstrapper bootstrapper = bootstrapObject.AddComponent<RuntimeBootstrapper>();
                InvokeAwake(bootstrapper);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                int starts = 0;
                runner.OnEventStarted += _ => starts++;

                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");

                Assert.That(runner.IsRunning, Is.False,
                    "The adapter must wait until the bootstrapper has published UI routing readiness.");

                InvokeIfPresent(bootstrapper, "ApplyInitialStateForActiveScene");

                Assert.That(runner.IsRunning, Is.True);
                Assert.That(starts, Is.EqualTo(1));
                InvokeIfPresent(adapter, "OnEnable");
                Assert.That(starts, Is.EqualTo(1));
                runner.EndEvent();
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterDefersForRestoreAndStartsAfterCompletion()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject core = new("DungeonOneRestoreCore");
            GameObject bootstrapObject = new("DungeonOneRestoreBootstrap");
            GameObject narrative = new("DungeonOneRestoreNarrative");
            GameObject adapterObject = new("DungeonOneRestoreAdapter");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                SaveLoadService saveLoad = core.AddComponent<SaveLoadService>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(saveLoad);
                SetPrivateField(saveLoad, "_operationState", SaveLoadService.OperationState.Restoring);
                RuntimeBootstrapper bootstrapper = bootstrapObject.AddComponent<RuntimeBootstrapper>();
                InvokeAwake(bootstrapper);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                InvokeIfPresent(bootstrapper, "ApplyInitialStateForActiveScene");
                Assert.That(runner.IsRunning, Is.False);

                SetPrivateField(saveLoad, "_operationState", SaveLoadService.OperationState.Idle);
                InvokeIfPresent(adapter, "HandleLoadCompleted", true, "Loaded primary save.");
                Assert.That(runner.IsRunning, Is.True);
                runner.EndEvent();
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterSkipsPersistentlyCompletedIntro()
        {
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject core = new("DungeonOneCompletedIntroCore");
            GameObject narrative = new("DungeonOneCompletedIntroNarrative");
            GameObject adapterObject = new("DungeonOneCompletedIntroAdapter");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                StoryProgressManager progress = core.AddComponent<StoryProgressManager>();
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(progress);
                progress.MarkEventCompleted(intro.EventId);

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");

                Assert.That(runner.IsRunning, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        [Test]
        public void ProductionDungeonOne_SceneStartAdapterStartsIntroOnceAndRestoredQuestRemainsActive()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(
                "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset");
            StoryEventDefinitionSO intro = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset");
            GameObject core = new("DungeonOneIntroTestCore");
            GameObject narrative = new("DungeonOneIntroTestNarrative");
            GameObject adapterObject = new("DungeonOneIntroTestAdapter");

            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController flow = core.AddComponent<GameFlowController>();
                QuestRuntime runtime = core.AddComponent<QuestRuntime>();
                SetReference(runtime, "questDefinitions", definition);
                InvokeAwake(stateMachine);
                InvokeAwake(flow);
                InvokeAwake(runtime);
                runtime.StartQuest(definition);

                Game.NonCombat.Save.GameSaveData snapshot = new();
                runtime.CaptureSaveData(snapshot);
                runtime.RestoreSaveData(snapshot);
                int starts = 0;
                runtime.OnQuestStarted += _ => starts++;

                StoryEventRunner runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                SceneStartStoryEventAdapter adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                InvokeIfPresent(adapter, "Start");

                Assert.That(runner.IsRunning, Is.True);
                runner.Advance();

                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Active));
                Assert.That(starts, Is.Zero);

                runner.EndEvent();
                runtime.CompleteQuest(definition.QuestId);
                runtime.CaptureSaveData(snapshot);
                runtime.RestoreSaveData(snapshot);

                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                narrative = new GameObject("DungeonOneCompletedIntroTestNarrative");
                adapterObject = new GameObject("DungeonOneCompletedIntroTestAdapter");
                runner = narrative.AddComponent<StoryEventRunner>();
                InvokeAwake(runner);
                adapter = adapterObject.AddComponent<SceneStartStoryEventAdapter>();
                SetReference(adapter, "runner", runner);
                SetReference(adapter, "eventDefinition", intro);
                InvokeIfPresent(adapter, "OnEnable");
                InvokeIfPresent(adapter, "Start");
                runner.Advance();

                Assert.That(runtime.GetQuestStatus(definition.QuestId), Is.EqualTo(QuestStatus.Completed));
                Assert.That(starts, Is.Zero);
                runner.EndEvent();
            }
            finally
            {
                Object.DestroyImmediate(adapterObject);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            InvokeIfPresent(component, "Awake");
        }

        private static void InvokeIfPresent(MonoBehaviour component, string methodName, params object[] arguments)
        {
            MethodInfo awake = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(component, arguments);
        }

        private static GameObject CreatePlayerAt(Vector3 position)
        {
            GameObject player = new("Player");
            player.tag = "Player";
            player.transform.position = position;
            player.AddComponent<Rigidbody2D>();
            return player;
        }

        private static void CreateSpawnPoint(string id, Vector3 position)
        {
            GameObject owner = new($"Spawn_{id}");
            owner.transform.position = position;
            SceneSpawnPoint point = owner.AddComponent<SceneSpawnPoint>();
            SerializedObject serialized = new(point);
            serialized.FindProperty("spawnPointId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InvokeSpawnMove(string spawnPointId)
        {
            MethodInfo method = typeof(SceneTravelService).GetMethod(
                "MovePlayerToSpawnPoint",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { spawnPointId, "Player" });
        }

        private static void SetPrivateField(object owner, string fieldName, object value)
        {
            FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(owner, value);
        }

        private static void SetReference(UnityEngine.Object owner, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.isArray)
            {
                property.arraySize = 1;
                property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            }
            else
            {
                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
