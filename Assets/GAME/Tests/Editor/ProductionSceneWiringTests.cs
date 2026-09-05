using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.Input;
using Game.Interaction;
using Game.Interaction.Editor;
using Game.NonCombat.Inventory;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Data;
using Game.Story.Interaction;
using Game.Story.UI;
using Game.UI;
using Game.UI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public sealed class ProductionSceneWiringTests
    {
        private const string Dungeon = "Assets/GAME/Scenes/Dungeon 1.unity";
        private const string DungeonTemplate = "Assets/GAME/Scenes/Dungeon_Template.unity";
        private const string TestingDungeonTemplate = "Assets/GAME/Scenes/Testing_Dungeon_Template.unity";
        private const string Title = "Assets/GAME/Scenes/TitleScene.unity";

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

        [TestCase(Dungeon)]
        [TestCase(Title)]
        public void ProductionScene_HasNoMissingScripts(string path)
        {
            Open(path);
            int missing = AllGameObjects().Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Assert.That(missing, Is.Zero, path);
        }

        [Test]
        public void Dungeon_HasExactlyOneCanonicalOwnerPerSystem()
        {
            Open(Dungeon);
            AssertOne<GameStateMachine>();
            AssertOne<GameFlowController>();
            AssertOne<GameInputInstaller>();
            AssertOne<UIScreenRouter>();
            AssertOne<GameUIRootController>();
            AssertOne<CombatEntryPoint>();
            AssertOne<CombatWorldLifecycleAdapter>();
            AssertOne<CombatRewardUIBinder>();
            AssertOne<StoryEventRunner>();
            AssertOne<StoryProgressManager>();
            AssertOne<QuestRuntime>();
            AssertOne<QuestObjectiveTracker>();
            AssertOne<QuestCompletionFlow>();
            AssertOne<SaveLoadService>();
            AssertOne<CurrencyWallet>();
            AssertOne<InventoryService>();
            AssertOne<RewardService>();
        }

        [Test]
        public void Dungeon_CombatReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<CombatWorldLifecycleAdapter>("entryPoint", "fieldLock", "cameraController", "formationManager");
            AssertReferences<CombatRewardUIBinder>("entryPoint", "rewardPanel", "rewardService");
            AssertReferences<CombatUIRootController>("entryPoint", "combatHudRoot", "planningPanel", "planningHUD", "rewardCanvas");
            SerializedObject fieldLock = Serialized<CombatFieldLock>();
            Assert.That(fieldLock.FindProperty("behavioursToDisable").arraySize, Is.GreaterThanOrEqualTo(3));
            Assert.That(fieldLock.FindProperty("freezeBodies2D").arraySize, Is.GreaterThanOrEqualTo(1));
            Assert.That(fieldLock.FindProperty("disableColliders2D").arraySize, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Dungeon_NarrativeAndQuestReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<StoryEventRunner>("dialoguePanel", "timedChoiceDialoguePanel");
            foreach (StoryInteractionController controller in FindAll<StoryInteractionController>())
                Assert.That(Reference(controller, "runner"), Is.Not.Null, controller.name);
            AssertReferences<QuestObjectiveTracker>("questRuntime");
            AssertReferences<QuestCompletionFlow>("questRuntime", "rewardService");
            AssertReferences<DemoMissionRuntime>("questRuntime", "currentMission");
            AssertReferences<RescueNpcActor>("missionRuntime", "npcDefinition", "interactPromptRoot");
        }

        [Test]
        public void Dungeon_EncounterAndSpawnIdsAreStableAndUnique()
        {
            Open(Dungeon);
            string[] ids = FindAll<CombatEncounterTrigger2D>()
                .Select(component => new SerializedObject(component).FindProperty("encounterId").stringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Assert.That(ids, Has.Length.EqualTo(3));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Is.EquivalentTo(new[] { "dungeon1.ghost.01", "dungeon1.ghost.02", "dungeon1.ghost.03" }));
            string[] spawns = FindAll<SceneSpawnPoint>().Select(point => point.SpawnPointId).ToArray();
            Assert.That(spawns, Has.None.Null.Or.Empty);
            Assert.That(spawns.Distinct().Count(), Is.EqualTo(spawns.Length));
        }

        [Test]
        public void Dungeon_SaveAndPlayerReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<SaveLoadService>("player");
            Assert.That(GameObject.Find("Player_new"), Is.Not.Null);
        }

        [Test]
        public void BuildSettingsContainCanonicalSceneTargets()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(enabled, Does.Contain(Title));
            Assert.That(enabled, Does.Contain(Dungeon));
        }

        [Test]
        public void DungeonTemplate_IsCanonicalSingleOwnerProductionSource()
        {
            Assert.That(System.IO.File.Exists(DungeonTemplate), Is.True);
            Open(DungeonTemplate);
            Assert.That(FindAll<CombatEntryPoint>(), Has.Length.EqualTo(1));
            MonoBehaviour[] components = FindAll<MonoBehaviour>();
            Assert.That(components.Any(component => component != null && component.gameObject.name == "CombatRuntime"), Is.True);
            Assert.That(components.Any(component =>
                component != null &&
                (component.GetType().Name == "CombatDemoFlowController" ||
                 component.GetType().Name == "SeamlessBattleManager" ||
                 component.GetType().Name == "BattleTrigger2D" ||
                 component.GetType().Namespace?.Contains("Debugging") == true ||
                 component.GetType().Namespace?.Contains("Legacy") == true)), Is.False);
        }

        [Test]
        public void DungeonTemplate_InteractionIdsAndRunnerOwnershipAreValid()
        {
            Open(DungeonTemplate);
            Assert.That(FindAll<InteractionRunner>(), Has.Length.LessThanOrEqualTo(1));

            InteractionController[] controllers = FindAll<InteractionController>();
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(controllers[0].transform), Is.EqualTo("Actors/Player/PlayerRoot"));
            InteractionPromptUI[] prompts = FindAll<InteractionPromptUI>();
            Assert.That(prompts, Has.Length.EqualTo(1));
            Assert.That(controllers[0].PromptUI, Is.SameAs(prompts[0]));

            InteractableObject[] persistent = FindAll<InteractableObject>()
                .Where(item => item.UsePolicy == InteractionUsePolicy.PersistentOnce)
                .ToArray();
            Assert.That(persistent.Select(item => item.InteractionId), Has.None.Null.Or.Empty);
            Assert.That(
                persistent.Select(item => item.InteractionId).Distinct(System.StringComparer.Ordinal).Count(),
                Is.EqualTo(persistent.Length));
        }

        [Test]
        public void DungeonTemplate_HasCanonicalNarrativeRuntimeAndProductionNpc()
        {
            Open(DungeonTemplate);

            StoryEventRunner[] runners = FindAll<StoryEventRunner>();
            Assert.That(runners, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(runners[0].transform), Is.EqualTo("Runtime/Narrative/StoryEventRunner"));

            StoryDialogueHUD[] huds = FindAll<StoryDialogueHUD>();
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(huds[0].IsPresentationReady, Is.True);
            Assert.That(huds[0].CanPresentChoices, Is.True);
            Assert.That(Reference(runners[0], "storyDialogueHUD"), Is.SameAs(huds[0]));
            Assert.That(FindAll<WorldDialogueBubble>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<TimedChoicePanel>(), Has.Length.EqualTo(1));

            TimedChoicePanel choicePanel = FindAll<TimedChoicePanel>().Single();
            SerializedObject serializedChoices = new(choicePanel);
            Assert.That(serializedChoices.FindProperty("choiceButtons").arraySize, Is.GreaterThanOrEqualTo(2));
            Assert.That(serializedChoices.FindProperty("choiceTexts").arraySize, Is.GreaterThanOrEqualTo(2));

            InteractableObject[] productionNpcs = FindAll<InteractableObject>()
                .Where(item => item.gameObject.name == "ProductionNpcInteraction")
                .ToArray();
            Assert.That(productionNpcs, Has.Length.EqualTo(1));
            InteractableObject npc = productionNpcs[0];
            Assert.That(GetHierarchyPath(npc.transform), Is.EqualTo("Actors/NPCs/ProductionNpcInteraction"));
            Assert.That(npc.PromptText, Is.EqualTo("F: \uB300\uD654"));
            Assert.That(npc.UsePolicy, Is.EqualTo(InteractionUsePolicy.Repeatable));
            Assert.That(npc.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(npc.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(npc.Events, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(npc.Events[0]), Is.EqualTo(
                "Assets/GAME/Data/Interaction/ProductionNpcDialogue.asset"));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(npc.gameObject), Is.SameAs(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab")));

            Assert.That(FindAll<InteractionController>(), Has.Length.EqualTo(1));

            QuestRuntime[] runtimes = FindAll<QuestRuntime>();
            QuestObjectiveTracker[] objectiveTrackers = FindAll<QuestObjectiveTracker>();
            QuestCompletionFlow[] completionFlows = FindAll<QuestCompletionFlow>();
            CombatQuestObjectivePublisher[] combatQuestPublishers = FindAll<CombatQuestObjectivePublisher>();
            QuestTrackerUI[] questTrackers = FindAll<QuestTrackerUI>();
            RewardService[] rewardServices = FindAll<RewardService>();
            CurrencyWallet[] wallets = FindAll<CurrencyWallet>();
            Assert.That(runtimes, Has.Length.EqualTo(1));
            Assert.That(GetHierarchyPath(runtimes[0].transform), Is.EqualTo("Runtime/Quest"));
            Assert.That(objectiveTrackers, Has.Length.EqualTo(1));
            Assert.That(completionFlows, Has.Length.EqualTo(1));
            Assert.That(combatQuestPublishers, Has.Length.EqualTo(1));
            Assert.That(questTrackers, Has.Length.EqualTo(1));
            Assert.That(rewardServices, Has.Length.EqualTo(1));
            Assert.That(wallets, Has.Length.EqualTo(1));
            Assert.That(Reference(objectiveTrackers[0], "questRuntime"), Is.SameAs(runtimes[0]));
            Assert.That(Reference(completionFlows[0], "questRuntime"), Is.SameAs(runtimes[0]));
            Assert.That(Reference(completionFlows[0], "rewardService"), Is.SameAs(rewardServices[0]));
            Assert.That(Reference(rewardServices[0], "currencyWallet"), Is.SameAs(wallets[0]));
            Assert.That(new SerializedObject(completionFlows[0]).FindProperty("grantRewardOnCompletion").boolValue, Is.True);
            Assert.That(new SerializedObject(completionFlows[0]).FindProperty("enterRewardStateOnCompletion").boolValue, Is.False);

            SerializedObject serializedRuntime = new(runtimes[0]);
            SerializedProperty definitions = serializedRuntime.FindProperty("questDefinitions");
            Assert.That(definitions.arraySize, Is.EqualTo(1));
            QuestDefinitionSO validationQuest = definitions.GetArrayElementAtIndex(0).objectReferenceValue as QuestDefinitionSO;
            Assert.That(AssetDatabase.GetAssetPath(validationQuest), Is.EqualTo(
                "Assets/GAME/Data/Quest/VALIDATION_PRODUCTION_NPC_QUEST.asset"));
            Assert.That(validationQuest.QuestId, Is.EqualTo("validation.production.npc.quest"));
            Assert.That(validationQuest.Objectives, Has.Length.EqualTo(1));
            Assert.That(validationQuest.Objectives[0].EventType, Is.EqualTo(QuestEventType.Kill));
            Assert.That(validationQuest.Objectives[0].ObjectiveId, Is.EqualTo("defeat_validation_target"));
            Assert.That(validationQuest.RewardGold, Is.EqualTo(7));

            CombatEncounterGroup[] encounters = FindAll<CombatEncounterGroup>();
            Assert.That(encounters.Select(encounter => encounter.EncounterId), Has.None.Null.Or.Empty);
            Assert.That(encounters.Select(encounter => encounter.EncounterId).Distinct().Count(), Is.EqualTo(encounters.Length));
            CombatEncounterGroup validationEncounter = encounters.Single(encounter =>
                encounter.EncounterId == "validation.production.npc.quest.kill");
            Assert.That(Reference(combatQuestPublishers[0], "targetEncounter"), Is.SameAs(validationEncounter));
            Assert.That(Reference(combatQuestPublishers[0], "combatEntryPoint"), Is.SameAs(FindAll<CombatEntryPoint>().Single()));
            Assert.That(new SerializedObject(combatQuestPublishers[0]).FindProperty("questId").stringValue,
                Is.EqualTo(validationQuest.QuestId));
            Assert.That(new SerializedObject(combatQuestPublishers[0]).FindProperty("objectiveId").stringValue,
                Is.EqualTo(validationQuest.Objectives[0].ObjectiveId));

            GameUIRootController uiRoots = FindAll<GameUIRootController>().Single();
            GameObject fieldRoot = Reference(uiRoots, "fieldRoot") as GameObject;
            Assert.That(Reference(questTrackers[0], "questRuntime"), Is.SameAs(runtimes[0]));
            Assert.That(fieldRoot, Is.Not.Null);
            Assert.That(questTrackers[0].transform.IsChildOf(fieldRoot.transform), Is.True);

            StoryEventDefinitionSO validationStory = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(
                "Assets/GAME/Data/Interaction/ProductionNpcDialogueValidation.asset");
            SerializedProperty choices = new SerializedObject(validationStory)
                .FindProperty("nodes").GetArrayElementAtIndex(0).FindPropertyRelative("choices");
            Assert.That(choices.arraySize, Is.EqualTo(2));
            SerializedProperty acceptChoice = choices.GetArrayElementAtIndex(0);
            SerializedProperty rejectChoice = choices.GetArrayElementAtIndex(1);
            Assert.That(acceptChoice.FindPropertyRelative("effects").arraySize, Is.EqualTo(1));

            SerializedProperty acceptEffect = acceptChoice.FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Assert.That(acceptEffect.FindPropertyRelative("type").intValue, Is.EqualTo((int)StoryEffectType.StartQuest));
            Assert.That(acceptEffect.FindPropertyRelative("questDefinition").objectReferenceValue,
                Is.SameAs(validationQuest));
            Assert.That(rejectChoice.FindPropertyRelative("effects").arraySize, Is.Zero);

            Assert.That(FindAll<QuestManager>(), Is.Empty);
            Assert.That(FindAll<Game.Mission.MissionManager>(), Is.Empty);
            Assert.That(FindAll<DemoMissionRuntime>(), Is.Empty);
            Assert.That(FindAll<Game.Mission.UI.MissionHUD>(), Is.Empty);
        }

        [Test]
        public void ProductionDungeonUI_HasOneSafeFieldInteractionPromptAndKeepsRequiredUI()
        {
            const string path = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<CombatPlanningHUD>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<RewardUIPanel>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Has.Length.EqualTo(1));
            StoryDialogueHUD[] narrativeHuds = prefab.GetComponentsInChildren<StoryDialogueHUD>(true);
            Assert.That(narrativeHuds, Has.Length.EqualTo(1));
            Assert.That(narrativeHuds[0].IsPresentationReady, Is.True);
            Assert.That(narrativeHuds[0].CanPresentChoices, Is.True);
            Assert.That(prefab.GetComponentsInChildren<WorldDialogueBubble>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<TimedChoicePanel>(true), Has.Length.EqualTo(1));

            Canvas[] narrativeCanvases = prefab.GetComponentsInChildren<Canvas>(true)
                .Where(item => item.gameObject.name == "NarrativeCanvas")
                .ToArray();
            Assert.That(narrativeCanvases, Has.Length.EqualTo(1));
            Canvas narrativeCanvas = narrativeCanvases[0];
            Assert.That(narrativeCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(narrativeCanvas.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(narrativeCanvas.GetComponent<CanvasScaler>().uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ConstantPixelSize));
            Assert.That(narrativeCanvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<WorldDialogueBubble>(true).Single().transform.IsChildOf(narrativeCanvas.transform), Is.True);
            Assert.That(prefab.GetComponentsInChildren<TimedChoicePanel>(true).Single().transform.IsChildOf(narrativeCanvas.transform), Is.True);

            GameUIRootController roots = prefab.GetComponent<GameUIRootController>();
            InteractionPromptUI[] prompts = prefab.GetComponentsInChildren<InteractionPromptUI>(true);
            Assert.That(prompts, Has.Length.EqualTo(1));
            GameObject fieldRoot = Reference(roots, "fieldRoot") as GameObject;
            GameObject displayRoot = Reference(prompts[0], "root") as GameObject;
            Text messageText = Reference(prompts[0], "messageText") as Text;
            Assert.That(fieldRoot, Is.Not.Null);
            Assert.That(prompts[0].transform.IsChildOf(fieldRoot.transform), Is.True);
            Assert.That(displayRoot, Is.Not.Null.And.Not.SameAs(prompts[0].gameObject));
            Assert.That(displayRoot.transform.IsChildOf(prompts[0].transform), Is.True);
            Assert.That(messageText, Is.Not.Null);
            Assert.That(messageText.raycastTarget, Is.False);
            GameObject dialogueRoot = Reference(roots, "dialogueRoot") as GameObject;
            Assert.That(dialogueRoot, Is.Not.Null);
            Assert.That(dialogueRoot.transform.IsChildOf(narrativeCanvas.transform), Is.True);
            Assert.That(narrativeHuds[0].transform.IsChildOf(dialogueRoot.transform), Is.True);
        }

        [Test]
        public void ProductionInteractionPrompt_PublicShowAndHideToggleOnlyDisplayRoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                InteractionPromptUI prompt = instance.GetComponentInChildren<InteractionPromptUI>(true);
                GameObject displayRoot = Reference(prompt, "root") as GameObject;
                Text messageText = Reference(prompt, "messageText") as Text;

                prompt.Show("F: 조사");
                Assert.That(prompt.gameObject.activeSelf, Is.True);
                Assert.That(displayRoot.activeSelf, Is.True);
                Assert.That(messageText.text, Is.EqualTo("F: 조사"));

                prompt.Hide();
                Assert.That(prompt.gameObject.activeSelf, Is.True);
                Assert.That(displayRoot.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ProductionInteraction_DefaultPromptMatchesGameplayInteractKeyboardBinding()
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/GAME/Scripts/Input/inputactions.inputactions");
            InputAction interact = actions.FindAction("Gameplay/Interact", true);
            Assert.That(interact.bindings.Any(binding => binding.path == "<Keyboard>/f"), Is.True);

            GameObject owner = new("InteractablePromptDefault");
            try
            {
                owner.AddComponent<BoxCollider2D>();
                InteractableObject interactable = owner.AddComponent<InteractableObject>();
                string prompt = new SerializedObject(interactable).FindProperty("promptText").stringValue;
                Assert.That(prompt, Is.EqualTo("F: 조사"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProductionNpcInteractionPrefab_UsesCanonicalStoryRequestContract()
        {
            const string path = "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);

            InteractableObject interactable = prefab.GetComponent<InteractableObject>();
            Assert.That(interactable, Is.Not.Null);
            Assert.That(prefab.GetComponents<InteractableObject>(), Has.Length.EqualTo(1));
            Assert.That(interactable.PromptText, Is.EqualTo("F: \uB300\uD654"));
            Assert.That(interactable.UsePolicy, Is.EqualTo(InteractionUsePolicy.Repeatable));

            Collider2D trigger = prefab.GetComponent<Collider2D>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(interactable.Events, Has.Count.EqualTo(1));
            Assert.That(interactable.Events[0], Is.TypeOf<StoryInteractionEventSO>());

            StoryInteractionEventSO storyEvent = (StoryInteractionEventSO)interactable.Events[0];
            Assert.That(storyEvent.SupportsProductionExecution, Is.True);
            Assert.That(storyEvent.EventDefinition, Is.Not.Null);
            Assert.That(storyEvent.EventDefinition, Is.TypeOf<StoryEventDefinitionSO>());
            Assert.That(storyEvent.EventDefinition.EventId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ProductionNpcDetection_PlayerContactColliderCarriesPlayerTag()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GAME/Prefabs/Player.prefab");
            Assert.That(player, Is.Not.Null);
            Collider2D[] contactColliders = player.GetComponentsInChildren<Collider2D>(true);
            Assert.That(contactColliders, Is.Not.Empty);
            Assert.That(contactColliders.All(collider => collider.CompareTag("Player")), Is.True);
        }

        [Test]
        public void ProductionNpcRequest_StartsStoryExactlyOnceAndRestoresExploration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab");
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            GameObject core = new("ProductionNpcRequestTestCore");
            GameObject narrative = new("ProductionNpcRequestTestNarrative");
            try
            {
                GameStateMachine stateMachine = core.AddComponent<GameStateMachine>();
                GameFlowController gameFlow = core.AddComponent<GameFlowController>();
                InvokeLifecycle(stateMachine, "Awake");
                InvokeLifecycle(gameFlow, "Awake");
                StoryEventRunner storyRunner = narrative.AddComponent<StoryEventRunner>();
                int starts = 0;
                storyRunner.OnEventStarted += _ => starts++;

                InteractableObject interactable = instance.GetComponent<InteractableObject>();
                interactable.Interact(core);
                interactable.Interact(core);

                Assert.That(starts, Is.EqualTo(1));
                Assert.That(storyRunner.IsRunning, Is.True);
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Dialogue).Or.EqualTo(GameState.Choice));

                storyRunner.EndEvent();
                Assert.That(stateMachine.Current, Is.EqualTo(GameState.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(narrative);
                Object.DestroyImmediate(core);
                InteractionRunner runtimeRunner = Object.FindFirstObjectByType<InteractionRunner>();
                if (runtimeRunner != null)
                    Object.DestroyImmediate(runtimeRunner.gameObject);
            }
        }

        [Test]
        public void LegacyDialogueInteractionAssets_RemainLegacyOnly()
        {
            string[] paths =
            {
                "Assets/GAME/Data/Interaction/Dialogue_Investigate_Note.asset",
                "Assets/GAME/Data/Interaction/Dialogue_RescueN{pc.asset"
            };

            foreach (string path in paths)
            {
                DialogueInteractionEventSO legacy = AssetDatabase.LoadAssetAtPath<DialogueInteractionEventSO>(path);
                Assert.That(legacy, Is.Not.Null, path);
                Assert.That(legacy.SupportsProductionExecution, Is.False, path);
            }
        }

        [Test]
        public void ProductionBuildScenes_PassInteractionAuthoringValidation()
        {
            Assert.That(ProductionInteractionValidator.ValidateBuildScenes(), Is.Empty);
        }

        [Test]
        public void ProductionUIAssets_PassExplicitRoutingValidation()
        {
            Assert.That(ProductionUIRoutingValidator.ValidateProductionAssets(), Is.Empty);
        }

        [Test]
        public void BuildSettings_ExcludeTestDeletedAndRecoveryScenes()
        {
            Assert.That(System.IO.File.Exists(TestingDungeonTemplate), Is.True);
            string[] paths = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();
            Assert.That(paths, Does.Not.Contain(TestingDungeonTemplate));
            Assert.That(paths.Any(path => path.Contains("Assets/_Recovery/")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 2.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 3.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 5.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/TutorialScene.unity")), Is.False);
        }

        private static void Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        private static IEnumerable<GameObject> AllGameObjects() =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(item => item.gameObject);

        private static T[] FindAll<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void AssertOne<T>() where T : Object => Assert.That(FindAll<T>(), Has.Length.EqualTo(1), typeof(T).Name);

        private static SerializedObject Serialized<T>() where T : Object
        {
            T value = FindAll<T>().Single();
            return new SerializedObject(value);
        }

        private static Object Reference(Object owner, string property) => new SerializedObject(owner).FindProperty(property)?.objectReferenceValue;

        private static void InvokeLifecycle(Object owner, string method)
        {
            owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(owner, null);
        }

        private static void AssertReferences<T>(params string[] properties) where T : Object
        {
            T owner = FindAll<T>().Single();
            foreach (string property in properties)
                Assert.That(Reference(owner, property), Is.Not.Null, $"{typeof(T).Name}.{property}");
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
    }
}
