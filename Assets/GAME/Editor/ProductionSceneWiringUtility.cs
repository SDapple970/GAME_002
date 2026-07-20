using System;
using System.IO;
using System.Linq;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission.Data;
using Game.DemoMission.Runtime;
using Game.Dialogue;
using Game.NonCombat.Inventory;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Interaction;
using Game.Story.UI;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    internal static class ProductionSceneWiringUtility
    {
        private const string DungeonScenePath = "Assets/GAME/Scenes/Dungeon 1.unity";

        public static void WireDungeonOneFromCommandLine()
        {
            EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Single);
            WireDungeonOne();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductionSceneWiring] Dungeon 1 canonical wiring saved.");
        }

        public static void BuildDevelopmentPlayerFromCommandLine()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            string outputDirectory = Path.GetFullPath("Builds/ProductionWiringDevelopment");
            Directory.CreateDirectory(outputDirectory);
            BuildPlayerOptions options = new()
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDirectory, "GAME_002.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException($"Development build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
            Debug.Log($"[ProductionSceneWiring] Development build succeeded with {scenes.Length} enabled scenes.");
        }

        [MenuItem("GAME/Production Wiring/Wire Dungeon 1 Canonical Systems")]
        private static void WireDungeonOne()
        {
            GameObject systems = RequireObject("Systems");
            GameObject combatManager = RequireObject("CombatManager");
            GameObject storySystem = RequireObject("StorySystem");
            GameObject mission = RequireObject("Mission");
            GameObject combatHud = RequireObject("CombatHUD");
            GameObject player = RequireObject("Player_new");

            storySystem.SetActive(true);

            CurrencyWallet wallet = Ensure<CurrencyWallet>(systems);
            InventoryService inventory = Ensure<InventoryService>(systems);
            StoryProgressManager storyProgress = Ensure<StoryProgressManager>(storySystem);
            QuestRuntime questRuntime = Ensure<QuestRuntime>(mission);
            QuestObjectiveTracker questTracker = Ensure<QuestObjectiveTracker>(mission);
            QuestCompletionFlow questCompletion = Ensure<QuestCompletionFlow>(mission);

            CombatEntryPoint entry = FindOne<CombatEntryPoint>();
            CombatFieldLock fieldLock = Ensure<CombatFieldLock>(combatManager);
            CombatWorldLifecycleAdapter lifecycle = Ensure<CombatWorldLifecycleAdapter>(combatManager);
            CombatUIRootController combatUi = Ensure<CombatUIRootController>(combatHud);
            CombatPlanningHUD planningHud = FindOne<CombatPlanningHUD>();
            RewardService rewardService = FindOne<RewardService>();
            RewardUIPanel rewardPanel = FindOne<RewardUIPanel>();
            GameUIRootController globalRoots = FindOne<GameUIRootController>();

            Assign(lifecycle, "entryPoint", entry);
            Assign(lifecycle, "fieldLock", fieldLock);
            Assign(lifecycle, "cameraController", FindOne<CombatCameraController>());
            Assign(lifecycle, "formationManager", FindOne<CombatFormationManager>());

            Assign(combatUi, "entryPoint", entry);
            Assign(combatUi, "combatHudRoot", combatHud);
            Assign(combatUi, "planningPanel", FindObject("CombatPlanningPanel"));
            Assign(combatUi, "widgetContainer", FindObject("WidgetContainer"));
            Assign(combatUi, "planningHUD", planningHud);
            Assign(combatUi, "overworldCanvas", ReadObjectReference<GameObject>(globalRoots, "fieldRoot"));
            Assign(combatUi, "rewardCanvas", ReadObjectReference<GameObject>(globalRoots, "rewardRoot"));

            CombatRewardUIBinder rewardBinder = FindOne<CombatRewardUIBinder>();
            Assign(rewardBinder, "entryPoint", entry);
            Assign(rewardBinder, "rewardPanel", rewardPanel);
            Assign(rewardBinder, "rewardService", rewardService);

            Assign(questTracker, "questRuntime", questRuntime);
            Assign(questCompletion, "questRuntime", questRuntime);
            Assign(questCompletion, "rewardService", rewardService);
            SetBool(questCompletion, "enterRewardStateOnCompletion", false);

            QuestTrackerUI questHud = FindOne<QuestTrackerUI>();
            Assign(questHud, "questRuntime", questRuntime);

            DemoMissionRuntime demoMission = FindOne<DemoMissionRuntime>();
            Assign(demoMission, "questRuntime", questRuntime);
            DemoMissionDefinitionSO canonicalDemo = AssetDatabase.LoadAssetAtPath<DemoMissionDefinitionSO>("Assets/GAME/Data/Tutorial/DemoMission_Dungeon1.asset");
            Assign(demoMission, "currentMission", canonicalDemo);

            foreach (MissionObjectiveTracker tracker in FindAll<MissionObjectiveTracker>()) Assign(tracker, "questRuntime", questRuntime);
            foreach (MissionCompletionController controller in FindAll<MissionCompletionController>()) Assign(controller, "questRuntime", questRuntime);

            RescueNpcActor rescue = FindOne<RescueNpcActor>();
            Assign(rescue, "missionRuntime", demoMission);
            Assign(rescue, "npcDefinition", canonicalDemo != null ? canonicalDemo.rescueTarget : null);
            GameObject prompt = rescue.transform.Cast<Transform>().Select(item => item.gameObject)
                .FirstOrDefault(item => item.name.IndexOf("prompt", StringComparison.OrdinalIgnoreCase) >= 0);
            if (prompt != null) Assign(rescue, "interactPromptRoot", prompt);

            StoryEventRunner runner = FindOne<StoryEventRunner>();
            TimedChoiceDialoguePanel timed = FindOne<TimedChoiceDialoguePanel>();
            DialoguePanel dialogue = Ensure<DialoguePanel>(timed != null ? timed.gameObject : storySystem);
            if (timed != null)
            {
                Assign(dialogue, "root", ReadObjectReference<GameObject>(timed, "panelRoot"));
                Assign(dialogue, "speakerText", ReadObjectReference<UnityEngine.Object>(timed, "speakerText"));
                Assign(dialogue, "bodyText", ReadObjectReference<UnityEngine.Object>(timed, "bodyText"));
                Assign(dialogue, "nextButton", ReadObjectReference<Button>(timed, "optionAButton"));
                Button choicePrefab = ReadObjectReference<Button>(timed, "optionBButton");
                Assign(dialogue, "choiceButtonPrefab", choicePrefab);
                GameObject panelRoot = ReadObjectReference<GameObject>(timed, "panelRoot");
                if (panelRoot != null) Assign(dialogue, "choiceContainer", panelRoot.transform);
            }
            Assign(runner, "dialoguePanel", dialogue);
            Assign(runner, "timedChoiceDialoguePanel", timed);
            foreach (StoryInteractionController controller in FindAll<StoryInteractionController>()) Assign(controller, "runner", runner);

            SaveLoadService save = FindOne<SaveLoadService>();
            Assign(save, "player", player.transform);

            Behaviour[] playerLocks = player.GetComponents<Behaviour>()
                .Where(component => component != null &&
                                    (component.GetType().Name == "PlayerInputController" ||
                                     component.GetType().Name == "PlayerMotor2D_New" ||
                                     component.GetType().Name == "PlayerFieldAttackController"))
                .ToArray();
            SetObjectList(fieldLock, "behavioursToDisable", playerLocks);
            SetObjectList(fieldLock, "freezeBodies2D", player.GetComponents<Rigidbody2D>());
            SetObjectList(fieldLock, "disableColliders2D", player.GetComponents<Collider2D>());

            AssignEncounterId("Enemy_Ghost", "dungeon1.ghost.01");
            AssignEncounterId("Enemy_Ghost (1)", "dungeon1.ghost.02");
            AssignEncounterId("Enemy_Ghost (2)", "dungeon1.ghost.03");

            SceneSpawnPoint spawn = FindOne<SceneSpawnPoint>();
            if (spawn != null && string.IsNullOrWhiteSpace(spawn.SpawnPointId)) SetString(spawn, "spawnPointId", "Dungeon1_Start");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void AssignEncounterId(string objectName, string id)
        {
            GameObject owner = RequireObject(objectName);
            CombatEncounterTrigger2D trigger = owner.GetComponent<CombatEncounterTrigger2D>();
            if (trigger == null) throw new InvalidOperationException($"{objectName} has no CombatEncounterTrigger2D.");
            SetString(trigger, "encounterId", id);
        }

        private static T Ensure<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static T FindOne<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static GameObject RequireObject(string name)
        {
            return FindObject(name) ?? throw new InvalidOperationException($"Required Dungeon 1 object '{name}' was not found.");
        }

        private static GameObject FindObject(string name)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == name)?.gameObject;
        }

        private static void Assign(UnityEngine.Object owner, string propertyName, UnityEngine.Object value)
        {
            if (owner == null) return;
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{owner.GetType().Name}.{propertyName} was not found.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static T ReadObjectReference<T>(UnityEngine.Object owner, string propertyName) where T : UnityEngine.Object
        {
            if (owner == null) return null;
            return new SerializedObject(owner).FindProperty(propertyName)?.objectReferenceValue as T;
        }

        private static void SetString(UnityEngine.Object owner, string propertyName, string value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetBool(UnityEngine.Object owner, string propertyName, bool value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void SetObjectList(UnityEngine.Object owner, string propertyName, UnityEngine.Object[] values)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }
    }
}
