using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.CameraSys;
using Game.Core;
using Game.Daily;
using Game.Demo;
using Game.Enemies;
using Game.Input;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.Player;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Data;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Game.EditorTools
{
    public static class DungeonOneProductionMigrationUtility
    {
        public const string TemplateScenePath = "Assets/GAME/Scenes/Dungeon_Template.unity";
        public const string LegacyScenePath = "Assets/GAME/Scenes/Dungeon 1.unity";
        public const string ProductionScenePath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string ProductionNpcPrefabPath = "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab";
        private const string ProductionNpcEventPath = "Assets/GAME/Data/Interaction/DungeonOneFirstTalkStoryInteraction.asset";
        private const string ProductionNpcDialoguePath = "Assets/GAME/Data/Interaction/DungeonOneFirstTalkDialogue.asset";
        private const string ProductionIntroStoryPath = "Assets/GAME/Data/Story/CH01/DungeonOneIntroStory.asset";
        private const string ProductionQuestDefinitionPath = "Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset";
        private const string ProductionQuestId = "ch01.find-first-npc";
        private const string ProductionQuestObjectiveId = "talk_first_npc";
        private const string ProductionQuestObjectiveTargetId = "dungeon1.npc.first-talk";
        private const string ProductionIntroStoryEventId = "dungeon1.intro.story";
        private const string ProductionNpcName = "Dungeon1_FirstTalkNpc";
        private const string ProductionNpcInteractionId = "dungeon1.npc.first-talk";

        public const int ExpectedSpriteRendererCount = 172;
        public const int ExpectedPhysicalColliderCount = 53;
        public const int ExpectedRepairedLegacySpriteCount = 40;

        private static readonly ProductionEncounterSpec[] ProductionEncounters =
        {
            new("Encounter_01", "Enemy_01", "dungeon1.encounter.01", new Vector3(4.85948f, -5.41649f, 0f)),
            new("Encounter_02", "Enemy_02", "dungeon1.encounter.02", new Vector3(-2.26052f, 25.07351f, 0f)),
            new("Encounter_03", "Enemy_03", "dungeon1.encounter.03", new Vector3(144.28948f, -6.35649f, 0f))
        };

        private static readonly Vector3 ProductionStartPosition = new(-39.88f, -6.33f, 0f);
        private static readonly Vector3 ProductionNpcPosition = new(187.105f, 7.698f, 0f);

        private static readonly EnvironmentRoot[] EnvironmentRoots =
        {
            new("Dungeon1_Background1-1 1_0", "Architecture"),
            new("Dungeon1_Background1-3", "Architecture"),
            new("Dungeon1_background", "Architecture"),
            new("apartment_side_house_0", "Architecture"),
            new("apartment_side_house_inside_1-4_0", "Architecture"),
            new("apartment_basefakdald_0", "Architecture"),
            new("apartment_basefakdald_0 (1)", "Architecture"),
            new("apartment_atlas_sprite 1_6", "Props"),
            new("apartment_atlas_sprite 1_7", "Props"),
            new("apartment_atlas_sprite 1_7 (1)", "Props"),
            new("apartment_atlas_sprite 1_9", "Props"),
            new("ladder_0", "Props"),
            new("ladder_0 (1)", "Props"),
            new("ladder_0 (2)", "Props")
        };

        private static readonly string[] ValidationWorldObjects =
        {
            "CalendarValidationInteractionTarget",
            "MultiObjectiveValidationInteractionTarget",
            "MultiObjectiveValidationStoryTarget",
            "StoryValidationInteractionTarget",
            "ValidationInteractionTarget"
        };

        private static readonly string[] LegacySpriteRepairAssetPaths =
        {
            "Assets/GAME/Image/TEST/share_interactive_1.png",
            "Assets/GAME/Image/background/Dungeon1_Background1-1 1.png",
            "Assets/GAME/Image/background/apartment_basefakdald.png"
        };

        [MenuItem("GAME/Production Migration/Create Dungeon 1 Production Scene")]
        public static void CreateProductionScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionScenePath) != null)
            {
                throw new InvalidOperationException(
                    $"Production scene already exists at '{ProductionScenePath}'. Refusing to overwrite it.");
            }

            if (!AssetDatabase.CopyAsset(TemplateScenePath, ProductionScenePath))
                throw new InvalidOperationException($"Could not copy '{TemplateScenePath}' to '{ProductionScenePath}'.");

            AssetDatabase.ImportAsset(ProductionScenePath, ImportAssetOptions.ForceSynchronousImport);

            Scene productionScene = EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            RemoveTemplateValidationContent();
            PrepareProductionRoots(out Transform environmentRoot, out Transform collisionRoot);
            PositionProductionPlayerSpawnAndCamera();

            Scene legacyScene = EditorSceneManager.OpenScene(LegacyScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(productionScene);

            Transform environmentArchitecture = CreateIdentityChild(environmentRoot, "Architecture");
            Transform environmentProps = CreateIdentityChild(environmentRoot, "Props");
            Transform collisionArchitecture = CreateIdentityChild(collisionRoot, "Architecture");
            Transform collisionProps = CreateIdentityChild(collisionRoot, "Props");

            foreach (EnvironmentRoot item in EnvironmentRoots)
            {
                GameObject source = FindRoot(legacyScene, item.Name);
                Transform visualParent = item.Category == "Architecture" ? environmentArchitecture : environmentProps;
                Transform physicalParent = item.Category == "Architecture" ? collisionArchitecture : collisionProps;

                if (CloneVisualBranch(source.transform, visualParent) == null)
                    throw new InvalidOperationException($"Environment root '{item.Name}' contained no SpriteRenderer data.");

                ClonePhysicalCollisionBranch(source.transform, physicalParent);
            }

            EditorSceneManager.CloseScene(legacyScene, true);
            SceneManager.SetActiveScene(productionScene);
            RepairUnresolvedDestinationSprites(environmentRoot);
            PlaceProductionEncounters(productionScene);
            PlaceProductionNpc(productionScene);
            ConfigureProductionQuest(productionScene);
            ConfigureProductionSceneStartNarrative(productionScene);
            EditorSceneManager.MarkSceneDirty(productionScene);

            ValidateProductionScene();

            if (!EditorSceneManager.SaveScene(productionScene, ProductionScenePath))
                throw new InvalidOperationException($"Unity could not save '{ProductionScenePath}'.");

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[DungeonOneProductionMigration] Created '{ProductionScenePath}' from the Production template with " +
                $"{ExpectedSpriteRendererCount} sprite renderers and {ExpectedPhysicalColliderCount} physical colliders.");
        }

        public static void CreateProductionSceneFromCommandLine()
        {
            CreateProductionScene();
        }

        public static void PlaceProductionEncountersFromCommandLine()
        {
            EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            PlaceProductionEncounters(SceneManager.GetActiveScene());
            ValidateProductionScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DungeonOneProductionMigration] Placed {ProductionEncounters.Length} production encounters in '{ProductionScenePath}'.");
        }

        [MenuItem("GAME/Production Migration/Place Dungeon 1 Production Encounters")]
        public static void PlaceProductionEncountersFromMenu()
        {
            PlaceProductionEncountersFromCommandLine();
        }

        public static void PlaceProductionNpcFromCommandLine()
        {
            EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            PlaceProductionNpc(SceneManager.GetActiveScene());
            ConfigureProductionQuest(SceneManager.GetActiveScene());
            ConfigureProductionSceneStartNarrative(SceneManager.GetActiveScene());
            ValidateProductionScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DungeonOneProductionMigration] Placed Production NPC interaction in '{ProductionScenePath}'.");
        }

        [MenuItem("GAME/Production Migration/Place Dungeon 1 Production NPC Interaction")]
        public static void PlaceProductionNpcFromMenu()
        {
            PlaceProductionNpcFromCommandLine();
        }

        [MenuItem("GAME/Production Migration/Validate Dungeon 1 Production Scene")]
        public static void ValidateProductionSceneFromMenu()
        {
            EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            ValidateProductionScene();
            Debug.Log($"[DungeonOneProductionMigration] Validation passed for '{ProductionScenePath}'.");
        }

        public static void ValidateProductionScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected active scene '{ProductionScenePath}', found '{scene.path}'.");

            GameObject[] sceneObjects = GetSceneObjects(scene);
            int missingScripts = sceneObjects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Require(missingScripts == 0, $"Missing Script count was {missingScripts}.");

            RequireCount<QuestRuntime>(1);
            RequireCount<QuestObjectiveTracker>(1);
            RequireCount<QuestCompletionFlow>(1);
            RequireCount<QuestCalendarIntegration>(1);
            RequireCount<CalendarService>(1);
            RequireCount<CombatEntryPoint>(1);
            RequireCount<CombatWorldLifecycleAdapter>(1);
            RequireCount<CombatRewardUIBinder>(1);
            RequireCount<CombatPlanningHUD>(1);
            RequireCount<GameUIRootController>(1);
            RequireCount<UIScreenRouter>(1);
            RequireCount<RewardUIPanel>(1);
            RequireCount<StoryEventRunner>(1);
            RequireCount<SceneStartStoryEventAdapter>(1);
            RequireCount<StoryProgressManager>(1);
            RequireCount<RewardService>(1);
            RequireCount<CurrencyWallet>(1);
            RequireCount<PlayerInputController>(1);
            RequireCount<InteractionController>(1);
            RequireCount<CameraFollow2D>(1);

            RequireCount<QuestManager>(0);
            RequireCount<Game.Mission.MissionManager>(0);
            RequireCount<DungeonObjectiveManager>(0);
            RequireCount<Game.Tutorial.TutorialQuestCombatBridge>(0);
            CombatEncounterGroup[] authoredGroups = FindSceneComponents<CombatEncounterGroup>();
            CombatEncounterTrigger2D[] authoredTriggers = FindSceneComponents<CombatEncounterTrigger2D>();
            Require(authoredGroups.Length == 0 || authoredGroups.Length == ProductionEncounters.Length,
                $"Expected either no encounters before production placement or {ProductionEncounters.Length} CombatEncounterGroup components, found {authoredGroups.Length}.");
            Require(authoredTriggers.Length == 0 || authoredTriggers.Length == ProductionEncounters.Length,
                $"Expected either no encounters before production placement or {ProductionEncounters.Length} CombatEncounterTrigger2D components, found {authoredTriggers.Length}.");
            if (authoredGroups.Length != 0 || authoredTriggers.Length != 0)
                ValidateProductionEncounters();
            RequireCount<CombatQuestObjectivePublisher>(0);
            RequireCount<InteractionQuestObjectivePublisher>(0);
            ValidateProductionNpc();

            MonoBehaviour[] behaviours = FindSceneComponents<MonoBehaviour>();
            MonoBehaviour forbidden = behaviours.FirstOrDefault(component =>
            {
                if (component == null)
                    return false;

                Type type = component.GetType();
                string typeNamespace = type.Namespace ?? string.Empty;
                return typeNamespace.Contains(".Debugging", StringComparison.Ordinal) ||
                       typeNamespace.Contains(".Legacy", StringComparison.Ordinal) ||
                       typeNamespace.Contains(".DemoMission", StringComparison.Ordinal) ||
                       type.Name.Contains("Demo", StringComparison.Ordinal) ||
                       type.Name.Contains("Test", StringComparison.Ordinal);
            });
            Require(forbidden == null,
                $"Forbidden Demo/Debug/Test/Legacy component remained: {forbidden?.GetType().FullName} on {forbidden?.name}.");

            SpriteRenderer[] renderers = FindRequired("World/Environment")
                .GetComponentsInChildren<SpriteRenderer>(true);
            Require(renderers.Length == ExpectedSpriteRendererCount,
                $"Expected {ExpectedSpriteRendererCount} SpriteRenderers, found {renderers.Length}.");
            Require(renderers.All(renderer => renderer.sprite != null), "One or more migrated SpriteRenderers have no Sprite.");

            Collider2D[] colliders = FindRequired("World/Collision")
                .GetComponentsInChildren<Collider2D>(true);
            Require(colliders.Length == ExpectedPhysicalColliderCount,
                $"Expected {ExpectedPhysicalColliderCount} physical Collider2D components, found {colliders.Length}.");
            Require(colliders.All(collider => !collider.isTrigger), "A trigger collider leaked into the environment collision tree.");
            Require(colliders.OfType<BoxCollider2D>().Count() == 43,
                "Expected 43 migrated BoxCollider2D components.");
            Require(colliders.OfType<PolygonCollider2D>().Count() == 10,
                "Expected 10 migrated PolygonCollider2D components.");
            Require(FindRequired("World/Environment").GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "A system or interaction MonoBehaviour leaked into the environment tree.");
            Require(FindRequired("World/Collision").GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "A system or interaction MonoBehaviour leaked into the collision tree.");

            RequireCount<Grid>(0);
            RequireCount<Tilemap>(0);
            RequireCount<TilemapRenderer>(0);
            RequireCount<TilemapCollider2D>(0);
            RequireCount<CompositeCollider2D>(0);

            Camera[] mainCameras = FindSceneComponents<Camera>().Where(camera => camera.CompareTag("MainCamera")).ToArray();
            Require(mainCameras.Length == 1, $"Expected one Main Camera, found {mainCameras.Length}.");
            Require(FindSceneComponents<PlayerInputController>().Select(item => item.gameObject).Distinct().Count() == 1,
                "Expected one Production Player root.");
            Require(Vector3.Distance(FindRequired("Actors/Player").position, ProductionStartPosition) < 0.001f,
                "Production Player wrapper does not match the migrated start position.");
            Require(Vector3.Distance(FindRequired("World/SpawnPoints/PlayerSpawn").position, ProductionStartPosition) < 0.001f,
                "PlayerSpawn does not match the migrated start position.");

            QuestRuntime questRuntime = FindSceneComponents<QuestRuntime>().Single();
            SerializedProperty questDefinitions = new SerializedObject(questRuntime).FindProperty("questDefinitions");
            Require(questDefinitions != null && questDefinitions.arraySize == 1,
                "Production QuestRuntime must reference exactly one Dungeon 1 QuestDefinitionSO.");
            Require(questDefinitions.GetArrayElementAtIndex(0).objectReferenceValue == EnsureProductionQuestDefinition(),
                "Production QuestRuntime is not bound to the Dungeon 1 QuestDefinitionSO.");
            ValidateProductionQuest();
            ValidateProductionSceneStartNarrative();

            Require(FindSceneComponents<global::GameInputInstaller>().Length == 0,
                "The template runtime-bootstrap input path must not be duplicated by a scene-local GameInputInstaller.");
            MethodInfo bootstrapMethod = typeof(Game.Core.RuntimeBootstrapper).GetMethod(
                "AutoBootstrapLoadedScene",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(bootstrapMethod != null,
                "RuntimeBootstrapper no longer exposes its Production runtime input/bootstrap entry path.");

            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/CombatRuntime.prefab", 1);
            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/Player.prefab", 1);
            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab", 1);
            ValidatePrefabInstanceCount(ProductionNpcPrefabPath, 1);

            foreach (Component component in sceneObjects.SelectMany(item => item.GetComponents<Component>()))
                ValidateMissingObjectReferences(component);
        }

        private static void RemoveTemplateValidationContent()
        {
            RemoveAllChildren(FindRequired("Actors/NPCs"));
            RemoveAllChildren(FindRequired("World/Encounters"));

            Transform world = FindRequired("World");
            foreach (string objectName in ValidationWorldObjects)
            {
                Transform target = world.Find(objectName);
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
            }

            foreach (CombatQuestObjectivePublisher publisher in FindSceneComponents<CombatQuestObjectivePublisher>())
                UnityEngine.Object.DestroyImmediate(publisher);
            foreach (InteractionQuestObjectivePublisher publisher in FindSceneComponents<InteractionQuestObjectivePublisher>())
                UnityEngine.Object.DestroyImmediate(publisher);

            QuestRuntime questRuntime = FindSceneComponents<QuestRuntime>().Single();
            SerializedObject serializedRuntime = new(questRuntime);
            SerializedProperty questDefinitions = serializedRuntime.FindProperty("questDefinitions");
            questDefinitions.arraySize = 0;
            serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceProductionEncounters(Scene productionScene)
        {
            if (productionScene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected production scene '{ProductionScenePath}', found '{productionScene.path}'.");

            Transform destinationRoot = FindRequired("World/Encounters");
            CombatEntryPoint entryPoint = FindSceneComponents<CombatEntryPoint>().Single();
            Transform player = FindRequired("Actors/Player/PlayerRoot");

            RemoveAllChildren(destinationRoot);
            Scene templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            try
            {
                Transform templateRoot = FindRoot(templateScene, "World").transform.Find("Encounters");
                if (templateRoot == null)
                    throw new InvalidOperationException("Dungeon_Template is missing World/Encounters.");

                CombatEncounterGroup templateGroup = templateRoot
                    .GetComponentsInChildren<CombatEncounterGroup>(true)
                    .FirstOrDefault();
                if (templateGroup == null)
                    throw new InvalidOperationException("Dungeon_Template has no canonical CombatEncounterGroup fixture.");

                foreach (ProductionEncounterSpec spec in ProductionEncounters)
                    CloneProductionEncounter(templateGroup, destinationRoot, productionScene, entryPoint, player, spec);
            }
            finally
            {
                EditorSceneManager.CloseScene(templateScene, true);
                SceneManager.SetActiveScene(productionScene);
            }

            EditorSceneManager.MarkSceneDirty(productionScene);
        }

        private static void PlaceProductionNpc(Scene productionScene)
        {
            if (productionScene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected production scene '{ProductionScenePath}', found '{productionScene.path}'.");

            Transform destinationRoot = FindRequired("Actors/NPCs");
            Transform existing = destinationRoot.Find(ProductionNpcName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            if (destinationRoot.GetComponentsInChildren<InteractableObject>(true).Length != 0)
                throw new InvalidOperationException("Actors/NPCs already contains an unexpected interaction object.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionNpcPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Production NPC prefab '{ProductionNpcPrefabPath}' was not found.");

            StoryInteractionEventSO interactionEvent = EnsureProductionNpcInteractionEvent();
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, productionScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Could not instantiate Production NPC prefab '{ProductionNpcPrefabPath}'.");

            instance.name = ProductionNpcName;
            instance.transform.SetParent(destinationRoot, false);
            instance.transform.position = ProductionNpcPosition;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            InteractableObject interactable = instance.GetComponent<InteractableObject>();
            Collider2D trigger = instance.GetComponent<Collider2D>();
            if (interactable == null || trigger == null || !trigger.isTrigger)
                throw new InvalidOperationException("Production NPC prefab must provide an InteractableObject and trigger Collider2D.");

            SetString(interactable, "interactionId", ProductionNpcInteractionId);
            SetReferenceArrayElement(interactable, "events", 0, interactionEvent, 1);
            EditorSceneManager.MarkSceneDirty(productionScene);
        }

        private static StoryInteractionEventSO EnsureProductionNpcInteractionEvent()
        {
            StoryInteractionEventSO existing = AssetDatabase.LoadAssetAtPath<StoryInteractionEventSO>(ProductionNpcEventPath);
            StoryEventDefinitionSO dialogue = EnsureProductionNpcDialogue();
            StoryInteractionEventSO interactionEvent = existing;
            if (interactionEvent == null)
            {
                interactionEvent = ScriptableObject.CreateInstance<StoryInteractionEventSO>();
                interactionEvent.name = "DungeonOneFirstTalkStoryInteraction";
                AssetDatabase.CreateAsset(interactionEvent, ProductionNpcEventPath);
            }

            SerializedObject serialized = new(interactionEvent);
            SerializedProperty actionId = serialized.FindProperty("actionId");
            SerializedProperty eventDefinition = serialized.FindProperty("eventDefinition");
            if (actionId == null || eventDefinition == null)
                throw new InvalidOperationException("StoryInteractionEventSO Production fields were not found.");
            actionId.stringValue = "dungeon1.npc.first-talk.interact";
            eventDefinition.objectReferenceValue = dialogue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactionEvent);
            AssetDatabase.SaveAssets();
            return interactionEvent;
        }

        private static StoryEventDefinitionSO EnsureProductionNpcDialogue()
        {
            StoryEventDefinitionSO dialogue = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(ProductionNpcDialoguePath);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<StoryEventDefinitionSO>();
                dialogue.name = "DungeonOneFirstTalkDialogue";
                AssetDatabase.CreateAsset(dialogue, ProductionNpcDialoguePath);
            }

            SerializedObject serialized = new(dialogue);
            serialized.FindProperty("eventId").stringValue = "dungeon1.npc.first-talk.story";
            serialized.FindProperty("startNodeId").stringValue = "first-talk";

            SerializedProperty nodes = serialized.FindProperty("nodes");
            nodes.arraySize = 1;
            SerializedProperty firstTalk = nodes.GetArrayElementAtIndex(0);
            firstTalk.FindPropertyRelative("nodeId").stringValue = "first-talk";
            firstTalk.FindPropertyRelative("speakerName").stringValue = "NPC";
            firstTalk.FindPropertyRelative("body").stringValue = "여기 ㅈㄴ 위험한 곳임. 들어갈거임?";
            firstTalk.FindPropertyRelative("choices").arraySize = 0;
            firstTalk.FindPropertyRelative("useTimedChoices").boolValue = false;
            firstTalk.FindPropertyRelative("nextNodeId").stringValue = string.Empty;
            SerializedProperty effects = firstTalk.FindPropertyRelative("effects");
            effects.arraySize = 1;
            SerializedProperty objectiveEffect = effects.GetArrayElementAtIndex(0);
            objectiveEffect.FindPropertyRelative("type").intValue = (int)StoryEffectType.PublishQuestEvent;
            objectiveEffect.FindPropertyRelative("key").stringValue = string.Empty;
            objectiveEffect.FindPropertyRelative("boolValue").boolValue = false;
            objectiveEffect.FindPropertyRelative("intValue").intValue = 1;
            objectiveEffect.FindPropertyRelative("missionId").stringValue = ProductionQuestId;
            objectiveEffect.FindPropertyRelative("objectiveId").stringValue = ProductionQuestObjectiveId;
            objectiveEffect.FindPropertyRelative("questEventType").intValue = (int)QuestEventType.Talk;
            objectiveEffect.FindPropertyRelative("questTargetId").stringValue = ProductionQuestObjectiveTargetId;
            objectiveEffect.FindPropertyRelative("questDefinition").objectReferenceValue = null;
            objectiveEffect.FindPropertyRelative("rewardSourceId").stringValue = string.Empty;
            objectiveEffect.FindPropertyRelative("rewardGold").intValue = 0;
            objectiveEffect.FindPropertyRelative("rewardExp").intValue = 0;
            objectiveEffect.FindPropertyRelative("rewardItemId").stringValue = string.Empty;
            objectiveEffect.FindPropertyRelative("rewardItemCount").intValue = 0;
            firstTalk.FindPropertyRelative("endEvent").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dialogue);
            AssetDatabase.SaveAssets();
            return dialogue;
        }

        private static void ConfigureProductionQuest(Scene productionScene)
        {
            if (productionScene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected production scene '{ProductionScenePath}', found '{productionScene.path}'.");

            QuestRuntime questRuntime = FindSceneComponents<QuestRuntime>().Single();
            SetReferenceArrayElement(questRuntime, "questDefinitions", 0, EnsureProductionQuestDefinition(), 1);
            EnsureProductionNpcDialogue();
            EnsureProductionIntroStory();
            EditorSceneManager.MarkSceneDirty(productionScene);
        }

        public static void ConfigureProductionSceneStartNarrativeFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            ConfigureProductionSceneStartNarrative(scene);
            ValidateProductionScene();
            EditorSceneManager.SaveScene(scene, ProductionScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureProductionSceneStartNarrative(Scene productionScene)
        {
            if (productionScene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected production scene '{ProductionScenePath}', found '{productionScene.path}'.");

            Transform narrativeRoot = FindRequired("Runtime/Narrative");
            StoryEventRunner storyRunner = narrativeRoot.GetComponentInChildren<StoryEventRunner>(true);
            if (storyRunner == null)
                throw new InvalidOperationException("Production Narrative root is missing its StoryEventRunner.");

            SceneStartStoryEventAdapter[] adapters = narrativeRoot.GetComponents<SceneStartStoryEventAdapter>();
            if (adapters.Length > 1)
                throw new InvalidOperationException("Production Narrative root has duplicate SceneStartStoryEventAdapter components.");

            SceneStartStoryEventAdapter adapter = adapters.Length == 1
                ? adapters[0]
                : narrativeRoot.gameObject.AddComponent<SceneStartStoryEventAdapter>();
            SetReference(adapter, "runner", storyRunner);
            SetReference(adapter, "eventDefinition", EnsureProductionIntroStory());
            EditorSceneManager.MarkSceneDirty(productionScene);
        }

        private static StoryEventDefinitionSO EnsureProductionIntroStory()
        {
            const string folderPath = "Assets/GAME/Data/Story/CH01";
            if (!AssetDatabase.IsValidFolder("Assets/GAME/Data/Story"))
                AssetDatabase.CreateFolder("Assets/GAME/Data", "Story");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/GAME/Data/Story", "CH01");

            StoryEventDefinitionSO story = AssetDatabase.LoadAssetAtPath<StoryEventDefinitionSO>(ProductionIntroStoryPath);
            if (story == null)
            {
                story = ScriptableObject.CreateInstance<StoryEventDefinitionSO>();
                story.name = "DungeonOneIntroStory";
                AssetDatabase.CreateAsset(story, ProductionIntroStoryPath);
            }

            SerializedObject serialized = new(story);
            serialized.FindProperty("eventId").stringValue = ProductionIntroStoryEventId;
            serialized.FindProperty("startNodeId").stringValue = "start";
            SerializedProperty nodes = serialized.FindProperty("nodes");
            nodes.arraySize = 2;

            SerializedProperty start = nodes.GetArrayElementAtIndex(0);
            start.FindPropertyRelative("nodeId").stringValue = "start";
            start.FindPropertyRelative("speakerName").stringValue = "System";
            start.FindPropertyRelative("body").stringValue = "낯선 복도에 도착했다.";
            start.FindPropertyRelative("choices").arraySize = 0;
            start.FindPropertyRelative("useTimedChoices").boolValue = false;
            start.FindPropertyRelative("nextNodeId").stringValue = "look";
            start.FindPropertyRelative("effects").arraySize = 0;
            start.FindPropertyRelative("endEvent").boolValue = false;

            SerializedProperty look = nodes.GetArrayElementAtIndex(1);
            look.FindPropertyRelative("nodeId").stringValue = "look";
            look.FindPropertyRelative("speakerName").stringValue = "Player";
            look.FindPropertyRelative("body").stringValue = "여긴 어디지?";
            look.FindPropertyRelative("choices").arraySize = 0;
            look.FindPropertyRelative("useTimedChoices").boolValue = false;
            look.FindPropertyRelative("nextNodeId").stringValue = string.Empty;
            SerializedProperty effects = look.FindPropertyRelative("effects");
            effects.arraySize = 2;
            SerializedProperty startQuest = effects.GetArrayElementAtIndex(0);
            startQuest.FindPropertyRelative("type").intValue = (int)StoryEffectType.StartQuest;
            startQuest.FindPropertyRelative("key").stringValue = string.Empty;
            startQuest.FindPropertyRelative("boolValue").boolValue = false;
            startQuest.FindPropertyRelative("intValue").intValue = 0;
            startQuest.FindPropertyRelative("missionDefinition").objectReferenceValue = null;
            startQuest.FindPropertyRelative("missionId").stringValue = string.Empty;
            startQuest.FindPropertyRelative("objectiveId").stringValue = string.Empty;
            startQuest.FindPropertyRelative("questEventType").intValue = (int)QuestEventType.Unknown;
            startQuest.FindPropertyRelative("questTargetId").stringValue = string.Empty;
            startQuest.FindPropertyRelative("questDefinition").objectReferenceValue = EnsureProductionQuestDefinition();
            startQuest.FindPropertyRelative("rewardSourceId").stringValue = string.Empty;
            startQuest.FindPropertyRelative("rewardGold").intValue = 0;
            startQuest.FindPropertyRelative("rewardExp").intValue = 0;
            startQuest.FindPropertyRelative("rewardItemId").stringValue = string.Empty;
            startQuest.FindPropertyRelative("rewardItemCount").intValue = 0;
            SerializedProperty markIntroCompleted = effects.GetArrayElementAtIndex(1);
            markIntroCompleted.FindPropertyRelative("type").intValue = (int)StoryEffectType.MarkEventCompleted;
            markIntroCompleted.FindPropertyRelative("key").stringValue = ProductionIntroStoryEventId;
            markIntroCompleted.FindPropertyRelative("boolValue").boolValue = false;
            markIntroCompleted.FindPropertyRelative("intValue").intValue = 0;
            markIntroCompleted.FindPropertyRelative("missionDefinition").objectReferenceValue = null;
            markIntroCompleted.FindPropertyRelative("missionId").stringValue = string.Empty;
            markIntroCompleted.FindPropertyRelative("objectiveId").stringValue = string.Empty;
            markIntroCompleted.FindPropertyRelative("questEventType").intValue = (int)QuestEventType.Unknown;
            markIntroCompleted.FindPropertyRelative("questTargetId").stringValue = string.Empty;
            markIntroCompleted.FindPropertyRelative("questDefinition").objectReferenceValue = null;
            markIntroCompleted.FindPropertyRelative("rewardSourceId").stringValue = string.Empty;
            markIntroCompleted.FindPropertyRelative("rewardGold").intValue = 0;
            markIntroCompleted.FindPropertyRelative("rewardExp").intValue = 0;
            markIntroCompleted.FindPropertyRelative("rewardItemId").stringValue = string.Empty;
            markIntroCompleted.FindPropertyRelative("rewardItemCount").intValue = 0;
            look.FindPropertyRelative("endEvent").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(story);
            AssetDatabase.SaveAssets();
            return story;
        }

        private static QuestDefinitionSO EnsureProductionQuestDefinition()
        {
            QuestDefinitionSO definition = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(ProductionQuestDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
                definition.name = "CH01_FindFirstNpc";
                AssetDatabase.CreateAsset(definition, ProductionQuestDefinitionPath);
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("questId").stringValue = ProductionQuestId;
            serialized.FindProperty("questTitle").stringValue = "낯선 장소";
            serialized.FindProperty("description").stringValue = "첫 NPC와 대화한다.";
            serialized.FindProperty("category").intValue = (int)QuestCategory.Unspecified;
            serialized.FindProperty("missionDayCost").intValue = 0;
            SerializedProperty objectives = serialized.FindProperty("objectives");
            objectives.arraySize = 1;
            SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative("objectiveId").stringValue = ProductionQuestObjectiveId;
            objective.FindPropertyRelative("eventType").intValue = (int)QuestEventType.Talk;
            objective.FindPropertyRelative("targetId").stringValue = ProductionQuestObjectiveTargetId;
            objective.FindPropertyRelative("requiredCount").intValue = 1;
            objective.FindPropertyRelative("optional").boolValue = false;
            objective.FindPropertyRelative("groupIndex").intValue = 0;
            objective.FindPropertyRelative("visibility").intValue = (int)QuestObjectiveVisibility.Visible;
            objective.FindPropertyRelative("description").stringValue = "첫 NPC와 대화한다.";
            serialized.FindProperty("rewardGold").intValue = 0;
            serialized.FindProperty("rewardExp").intValue = 0;
            serialized.FindProperty("retryPolicy").intValue = (int)QuestRetryPolicy.NotRetryable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static void ValidateProductionQuest()
        {
            QuestDefinitionSO definition = EnsureProductionQuestDefinition();
            Require(definition.QuestId == ProductionQuestId, "Dungeon 1 questId does not match the approved Production ID.");
            Require(definition.QuestTitle == "낯선 장소", "Dungeon 1 quest title does not match the approved title.");
            Require(definition.MissionDayCost == 0, "Dungeon 1 quest must not consume calendar days.");
            Require(definition.RewardGold == 0 && definition.RewardExp == 0, "Dungeon 1 quest must not define rewards.");
            Require(definition.Objectives != null && definition.Objectives.Length == 1,
                "Dungeon 1 quest must define exactly one objective.");
            QuestObjectiveDefinition objective = definition.Objectives[0];
            Require(objective != null &&
                    objective.ObjectiveId == ProductionQuestObjectiveId &&
                    objective.EventType == QuestEventType.Talk &&
                    objective.TargetId == ProductionQuestObjectiveTargetId &&
                    objective.RequiredCount == 1,
                "Dungeon 1 quest objective does not match the approved Talk contract.");

            SerializedProperty effect = new SerializedObject(EnsureProductionNpcDialogue())
                .FindProperty("nodes").GetArrayElementAtIndex(0)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Require(effect.FindPropertyRelative("type").intValue == (int)StoryEffectType.PublishQuestEvent &&
                    effect.FindPropertyRelative("missionId").stringValue == ProductionQuestId &&
                    effect.FindPropertyRelative("objectiveId").stringValue == ProductionQuestObjectiveId &&
                    effect.FindPropertyRelative("questEventType").intValue == (int)QuestEventType.Talk &&
                    effect.FindPropertyRelative("questTargetId").stringValue == ProductionQuestObjectiveTargetId &&
                    effect.FindPropertyRelative("intValue").intValue == 1,
                "Dungeon 1 FirstTalk dialogue does not publish the approved canonical Talk QuestEvent.");
        }

        private static void ValidateProductionSceneStartNarrative()
        {
            SceneStartStoryEventAdapter adapter = FindSceneComponents<SceneStartStoryEventAdapter>().Single();
            StoryEventRunner runner = FindSceneComponents<StoryEventRunner>().Single();
            StoryEventDefinitionSO intro = EnsureProductionIntroStory();
            Require(ReadReference<StoryEventRunner>(adapter, "runner") == runner,
                "Scene-start narrative adapter is not bound to the canonical StoryEventRunner.");
            Require(ReadReference<StoryEventDefinitionSO>(adapter, "eventDefinition") == intro,
                "Scene-start narrative adapter is not bound to the Dungeon 1 intro Story.");
            Require(intro.EventId == ProductionIntroStoryEventId,
                "Dungeon 1 intro Story does not use its stable Production event ID.");
            Require(intro.Nodes.Count == 2 && intro.Nodes[1].Effects.Count == 2,
                "Dungeon 1 intro Story does not preserve the authored two-node legacy structure.");
            SerializedProperty effect = new SerializedObject(intro)
                .FindProperty("nodes").GetArrayElementAtIndex(1)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(0);
            Require(effect.FindPropertyRelative("type").intValue == (int)StoryEffectType.StartQuest &&
                    effect.FindPropertyRelative("questDefinition").objectReferenceValue == EnsureProductionQuestDefinition(),
                "Dungeon 1 intro Story must start the authored Production quest through StoryEffect.StartQuest.");
            SerializedProperty completionEffect = new SerializedObject(intro)
                .FindProperty("nodes").GetArrayElementAtIndex(1)
                .FindPropertyRelative("effects").GetArrayElementAtIndex(1);
            Require(completionEffect.FindPropertyRelative("type").intValue == (int)StoryEffectType.MarkEventCompleted &&
                    completionEffect.FindPropertyRelative("key").stringValue == ProductionIntroStoryEventId,
                "Dungeon 1 intro Story must persist its canonical completion marker after starting the quest.");
        }

        private static void CloneProductionEncounter(
            CombatEncounterGroup templateGroup,
            Transform destinationRoot,
            Scene productionScene,
            CombatEntryPoint entryPoint,
            Transform player,
            ProductionEncounterSpec spec)
        {
            GameObject clone = UnityEngine.Object.Instantiate(templateGroup.gameObject);
            SceneManager.MoveGameObjectToScene(clone, productionScene);
            clone.name = spec.GroupName;
            clone.transform.SetParent(destinationRoot, false);
            clone.transform.localPosition = spec.Position;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;

            CombatEncounterGroup group = clone.GetComponent<CombatEncounterGroup>();
            FieldEnemyMotor2D[] enemyMotors = clone.GetComponentsInChildren<FieldEnemyMotor2D>(true);
            CombatEncounterTrigger2D[] triggers = clone.GetComponentsInChildren<CombatEncounterTrigger2D>(true);
            if (group == null || enemyMotors.Length != 1 || triggers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Dungeon_Template encounter fixture must contain one group, one FieldEnemyMotor2D, and one trigger; " +
                    $"found group={(group != null ? 1 : 0)}, enemies={enemyMotors.Length}, triggers={triggers.Length}.");
            }

            GameObject enemy = enemyMotors[0].gameObject;
            CombatEncounterTrigger2D trigger = triggers[0];
            enemy.name = spec.EnemyName;
            trigger.gameObject.name = "ContactTrigger";

            SetString(group, "encounterId", spec.EncounterId);
            SetString(trigger, "encounterId", spec.EncounterId);
            SetReference(trigger, "entryPoint", entryPoint);
            SetReference(trigger, "enemyObject", enemy);
            SetReference(trigger, "encounterGroup", group);
            SetReference(trigger, "openingEffectOrNull", null);
            SetBool(trigger, "debugLog", false);

            foreach (FieldEnemyPatrolAI2D patrol in clone.GetComponentsInChildren<FieldEnemyPatrolAI2D>(true))
                SetReference(patrol, "player", player);
        }

        private static void ValidateProductionEncounters()
        {
            CombatEncounterGroup[] groups = FindSceneComponents<CombatEncounterGroup>();
            CombatEncounterTrigger2D[] triggers = FindSceneComponents<CombatEncounterTrigger2D>();
            Require(groups.Length == ProductionEncounters.Length,
                $"Expected {ProductionEncounters.Length} CombatEncounterGroup components, found {groups.Length}.");
            Require(triggers.Length == ProductionEncounters.Length,
                $"Expected {ProductionEncounters.Length} CombatEncounterTrigger2D components, found {triggers.Length}.");

            Transform encountersRoot = FindRequired("World/Encounters");
            CombatEntryPoint entryPoint = FindSceneComponents<CombatEntryPoint>().Single();
            Transform player = FindRequired("Actors/Player/PlayerRoot");
            foreach (ProductionEncounterSpec spec in ProductionEncounters)
            {
                CombatEncounterGroup group = groups.SingleOrDefault(item =>
                    ReadString(item, "encounterId") == spec.EncounterId);
                Require(group != null, $"Encounter group '{spec.EncounterId}' is missing.");
                Require(group.name == spec.GroupName, $"Encounter '{spec.EncounterId}' group name is '{group.name}'.");
                Require(group.transform.parent == encountersRoot, $"Encounter '{spec.EncounterId}' is outside World/Encounters.");
                Require(Vector3.Distance(group.transform.position, spec.Position) < 0.001f,
                    $"Encounter '{spec.EncounterId}' position is {group.transform.position}, expected {spec.Position}.");

                FieldEnemyMotor2D[] enemyMotors = group.GetComponentsInChildren<FieldEnemyMotor2D>(true);
                CombatEncounterTrigger2D[] memberTriggers = group.GetComponentsInChildren<CombatEncounterTrigger2D>(true);
                Require(enemyMotors.Length == 1, $"Encounter '{spec.EncounterId}' must have one canonical field enemy.");
                Require(memberTriggers.Length == 1, $"Encounter '{spec.EncounterId}' must have one contact trigger.");
                Require(enemyMotors[0].gameObject.name == spec.EnemyName,
                    $"Encounter '{spec.EncounterId}' enemy name is '{enemyMotors[0].name}'.");

                CombatEncounterTrigger2D trigger = memberTriggers[0];
                Require(ReadString(trigger, "encounterId") == spec.EncounterId,
                    $"Encounter trigger for '{spec.EncounterId}' has a mismatched stable ID.");
                Require(ReadReference<CombatEntryPoint>(trigger, "entryPoint") == entryPoint,
                    $"Encounter '{spec.EncounterId}' is not bound to the canonical CombatEntryPoint.");
                Require(ReadReference<GameObject>(trigger, "enemyObject") == enemyMotors[0].gameObject,
                    $"Encounter '{spec.EncounterId}' does not target its field enemy.");
                Require(ReadReference<CombatEncounterGroup>(trigger, "encounterGroup") == group,
                    $"Encounter '{spec.EncounterId}' trigger is not owned by its group.");
                Require(!ReadBool(trigger, "debugLog"),
                    $"Encounter '{spec.EncounterId}' must not enable debug trigger logging.");

                foreach (FieldEnemyPatrolAI2D patrol in group.GetComponentsInChildren<FieldEnemyPatrolAI2D>(true))
                    Require(ReadReference<Transform>(patrol, "player") == player,
                        $"Encounter '{spec.EncounterId}' patrol is not bound to the production player.");
            }
        }

        private static void ValidateProductionNpc()
        {
            InteractableObject[] interactables = FindSceneComponents<InteractableObject>();
            Require(interactables.Length == 1, $"Expected one Production NPC InteractableObject, found {interactables.Length}.");

            Transform npcRoot = FindRequired($"Actors/NPCs/{ProductionNpcName}");
            InteractableObject npc = interactables[0];
            Require(npc.transform == npcRoot, "Production NPC interaction is not on the expected NPC root.");
            Require(Vector3.Distance(npcRoot.position, ProductionNpcPosition) < 0.001f,
                $"Production NPC position is {npcRoot.position}, expected {ProductionNpcPosition}.");
            Require(npc.PromptText == "F: 대화", "Production NPC prompt does not use the canonical authored label.");
            Require(npc.UsePolicy == InteractionUsePolicy.Repeatable,
                "Production NPC must preserve the authored repeatable interaction contract.");
            Require(npc.InteractionId == ProductionNpcInteractionId,
                "Production NPC does not have the expected stable interaction ID.");
            Require(npc.Events.Count == 1 && npc.Events[0] is StoryInteractionEventSO,
                "Production NPC must use the authored Production Story interaction event.");
            Require(npc.Events[0] == EnsureProductionNpcInteractionEvent(),
                "Production NPC does not reference the Dungeon 1 authored interaction event.");
            StoryInteractionEventSO storyEvent = (StoryInteractionEventSO)npc.Events[0];
            Require(storyEvent.EventDefinition != null,
                "Production NPC Story interaction event is missing its dialogue definition.");
            Require(storyEvent.EventDefinition == EnsureProductionNpcDialogue(),
                "Production NPC Story interaction event does not reference the Dungeon 1 dialogue definition.");
            Require(npc.GetComponent<Collider2D>() is { isTrigger: true },
                "Production NPC must use a trigger Collider2D for interaction detection.");
            Require(npc.GetComponentsInChildren<Collider2D>(true).All(collider => collider.isTrigger),
                "Production NPC must not retain a legacy physical interaction collider.");
            Require(npc.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
                "Production NPC must contain one canonical visual, not a duplicate legacy visual.");
            Require(npc.GetComponentInChildren<StoryInteractable2D>(true) == null,
                "Production NPC must not retain StoryInteractable2D ownership.");
            Require(npc.GetComponent<InteractionController>() == null,
                "Production NPC must not own InteractionController or input routing.");
            Require(npc.GetComponent<GameStateMachine>() == null,
                "Production NPC must not own GameState writes.");
        }

        private static void PrepareProductionRoots(out Transform environmentRoot, out Transform collisionRoot)
        {
            environmentRoot = FindRequired("World/Environment");
            collisionRoot = FindRequired("World/Collision");
            RemoveAllChildren(environmentRoot);
            RemoveAllChildren(collisionRoot);
            CreateIdentityChild(environmentRoot, "Dungeon1");
            CreateIdentityChild(collisionRoot, "Dungeon1");
            environmentRoot = environmentRoot.Find("Dungeon1");
            collisionRoot = collisionRoot.Find("Dungeon1");
        }

        private static void PositionProductionPlayerSpawnAndCamera()
        {
            Transform playerWrapper = FindRequired("Actors/Player");
            Transform playerRoot = FindRequired("Actors/Player/PlayerRoot");
            Transform spawn = FindRequired("World/SpawnPoints/PlayerSpawn");
            Transform mainCamera = FindRequired("Main Camera");

            playerWrapper.position = ProductionStartPosition;
            playerRoot.localPosition = Vector3.zero;
            spawn.position = ProductionStartPosition;
            mainCamera.position = new Vector3(ProductionStartPosition.x, ProductionStartPosition.y, mainCamera.position.z);
        }

        private static GameObject CloneVisualBranch(Transform source, Transform destinationParent)
        {
            if (!HasVisualData(source))
                return null;

            GameObject destination = CreateCleanClone(source, destinationParent);
            foreach (SpriteRenderer renderer in source.GetComponents<SpriteRenderer>())
                CopyComponent(renderer, destination);
            foreach (Transform child in source)
                CloneVisualBranch(child, destination.transform);
            return destination;
        }

        private static GameObject ClonePhysicalCollisionBranch(Transform source, Transform destinationParent)
        {
            if (!HasPhysicalCollisionData(source))
                return null;

            GameObject destination = CreateCleanClone(source, destinationParent);
            foreach (Collider2D collider in source.GetComponents<Collider2D>().Where(item => !item.isTrigger))
                CopyComponent(collider, destination);
            foreach (Transform child in source)
                ClonePhysicalCollisionBranch(child, destination.transform);
            return destination;
        }

        private static bool HasVisualData(Transform source)
        {
            if (source.GetComponent<SpriteRenderer>() != null)
                return true;
            foreach (Transform child in source)
            {
                if (HasVisualData(child))
                    return true;
            }
            return false;
        }

        private static bool HasPhysicalCollisionData(Transform source)
        {
            if (source.GetComponents<Collider2D>().Any(item => !item.isTrigger))
                return true;
            foreach (Transform child in source)
            {
                if (HasPhysicalCollisionData(child))
                    return true;
            }
            return false;
        }

        private static void RepairUnresolvedDestinationSprites(Transform environmentRoot)
        {
            SpriteRenderer[] unresolved = environmentRoot
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.sprite == null)
                .ToArray();
            if (unresolved.Length != ExpectedRepairedLegacySpriteCount)
            {
                throw new InvalidOperationException(
                    $"Expected {ExpectedRepairedLegacySpriteCount} stale migrated SpriteRenderer references, " +
                    $"found {unresolved.Length}.");
            }

            Dictionary<string, Sprite> spritesByName = LegacySpriteRepairAssetPaths
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Sprite>()
                .GroupBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count() == 1
                        ? group.Single()
                        : throw new InvalidOperationException($"Sprite repair name '{group.Key}' is ambiguous."),
                    StringComparer.Ordinal);

            foreach (SpriteRenderer renderer in unresolved)
            {
                Sprite replacement = FindReplacementSprite(renderer.transform, environmentRoot, spritesByName);
                if (replacement == null)
                {
                    throw new InvalidOperationException(
                        $"Could not repair the stale Sprite reference at '{GetHierarchyPath(renderer.transform)}'.");
                }

                renderer.sprite = replacement;
                EditorUtility.SetDirty(renderer);
            }

            Debug.Log(
                $"[DungeonOneProductionMigration] Repaired {unresolved.Length} stale legacy sub-sprite references " +
                "from existing Sprite assets; no asset was duplicated.");
        }

        private static Sprite FindReplacementSprite(
            Transform transform,
            Transform environmentRoot,
            IReadOnlyDictionary<string, Sprite> spritesByName)
        {
            while (transform != null && transform != environmentRoot)
            {
                string normalizedName = Regex.Replace(transform.name, @" \(\d+\)$", string.Empty);
                if (spritesByName.TryGetValue(normalizedName, out Sprite sprite))
                    return sprite;
                transform = transform.parent;
            }
            return null;
        }

        private static GameObject CreateCleanClone(Transform source, Transform destinationParent)
        {
            GameObject destination = new(source.name);
            destination.transform.SetParent(destinationParent, false);
            destination.transform.localPosition = source.localPosition;
            destination.transform.localRotation = source.localRotation;
            destination.transform.localScale = source.localScale;
            destination.layer = source.gameObject.layer;
            destination.tag = source.gameObject.tag;
            GameObjectUtility.SetStaticEditorFlags(
                destination,
                GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            destination.SetActive(source.gameObject.activeSelf);
            return destination;
        }

        private static void CopyComponent(Component source, GameObject destination)
        {
            if (!ComponentUtility.CopyComponent(source) || !ComponentUtility.PasteComponentAsNew(destination))
                throw new InvalidOperationException($"Failed to copy {source.GetType().Name} from '{source.name}'.");
        }

        private static Transform CreateIdentityChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(item => item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected one legacy root named '{name}', found {matches.Length}.");
            return matches[0];
        }

        private static Transform FindRequired(string path)
        {
            string[] segments = path.Split('/');
            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects()
                .SingleOrDefault(item => item.name == segments[0]);
            if (root == null)
                throw new InvalidOperationException($"Required scene path '{path}' was not found.");

            Transform current = root.transform;
            for (int i = 1; i < segments.Length; i++)
            {
                current = current.Find(segments[i]);
                if (current == null)
                    throw new InvalidOperationException($"Required scene path '{path}' was not found.");
            }
            return current;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> segments = new();
            while (transform != null)
            {
                segments.Add(transform.name);
                transform = transform.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static void RemoveAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void SetString(UnityEngine.Object owner, string propertyName, string value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetReference(UnityEngine.Object owner, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetReferenceArrayElement(
            UnityEngine.Object owner,
            string propertyName,
            int index,
            UnityEngine.Object value,
            int expectedLength)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found as an array.");
            property.arraySize = expectedLength;
            property.GetArrayElementAtIndex(index).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetBool(UnityEngine.Object owner, string propertyName, bool value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static string ReadString(UnityEngine.Object owner, string propertyName)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            return property.stringValue;
        }

        private static bool ReadBool(UnityEngine.Object owner, string propertyName)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            return property.boolValue;
        }

        private static T ReadReference<T>(UnityEngine.Object owner, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            return property.objectReferenceValue as T;
        }

        private static T[] FindSceneComponents<T>() where T : Component
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject[] GetSceneObjects(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
        }

        private static void RequireCount<T>(int expected) where T : Component
        {
            int count = FindSceneComponents<T>().Length;
            Require(count == expected, $"Expected {expected} {typeof(T).Name} component(s), found {count}.");
        }

        private static void ValidatePrefabInstanceCount(string prefabPath, int expected)
        {
            int count = GetSceneObjects(SceneManager.GetActiveScene())
                .Count(item => PrefabUtility.IsAnyPrefabInstanceRoot(item) &&
                               PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item) == prefabPath);
            Require(count == expected, $"Expected {expected} instance(s) of '{prefabPath}', found {count}.");
        }

        private static void ValidateMissingObjectReferences(Component component)
        {
            if (component == null)
                return;

            SerializedObject serialized = new(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue == null &&
                    iterator.objectReferenceInstanceIDValue != 0)
                {
                    throw new InvalidOperationException(
                        $"Missing object reference at {component.GetType().Name}.{iterator.propertyPath} on '{component.name}'.");
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct EnvironmentRoot
        {
            public EnvironmentRoot(string name, string category)
            {
                Name = name;
                Category = category;
            }

            public string Name { get; }
            public string Category { get; }
        }

        private readonly struct ProductionEncounterSpec
        {
            public ProductionEncounterSpec(string groupName, string enemyName, string encounterId, Vector3 position)
            {
                GroupName = groupName;
                EnemyName = enemyName;
                EncounterId = encounterId;
                Position = position;
            }

            public string GroupName { get; }
            public string EnemyName { get; }
            public string EncounterId { get; }
            public Vector3 Position { get; }
        }
    }
}
