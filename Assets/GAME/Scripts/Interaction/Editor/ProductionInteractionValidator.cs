#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Story;
using Game.Story.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Interaction.Editor
{
    public static class ProductionInteractionValidator
    {
        [MenuItem("GAME/Validation/Validate Production Interactions")]
        public static void ValidateMenu()
        {
            IReadOnlyList<string> issues = ValidateBuildScenes();
            if (issues.Count == 0)
                Debug.Log("[ProductionInteractionValidator] Production Interaction validation passed.");
            else
                Debug.LogError("[ProductionInteractionValidator] " + string.Join("\n", issues));
        }

        public static IReadOnlyList<string> ValidateBuildScenes()
        {
            List<string> issues = new();
            Dictionary<string, string> owners = new(StringComparer.Ordinal);
            string originalPath = SceneManager.GetActiveScene().path;
            string[] paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled &&
                                scene.path.StartsWith("Assets/GAME/Scenes/", StringComparison.Ordinal) &&
                                !scene.path.EndsWith("Testing_Dungeon_Template.unity", StringComparison.Ordinal))
                .Select(scene => scene.path)
                .ToArray();

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    Scene scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
                    InteractionRunner[] runners = UnityEngine.Object.FindObjectsByType<InteractionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    RuntimeBootstrapper[] bootstrappers = UnityEngine.Object.FindObjectsByType<RuntimeBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    InteractionRuntime[] runtimes = UnityEngine.Object.FindObjectsByType<InteractionRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (bootstrappers.Length != 1)
                        issues.Add($"Scene '{paths[i]}' has {bootstrappers.Length} authored RuntimeBootstrapper instances; Interaction would use compatibility bootstrap when zero.");
                    if (runners.Length > 1)
                        issues.Add($"Scene '{paths[i]}' contains {runners.Length} InteractionRunner instances.");
                    if (runners.Length == 1 && runtimes.Length != 1)
                        issues.Add($"Scene '{paths[i]}' has an authored InteractionRunner without exactly one InteractionRuntime.");

                    InteractableObject[] objects = UnityEngine.Object.FindObjectsByType<InteractableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int j = 0; j < objects.Length; j++)
                    {
                        InteractableObject item = objects[j];
                        if (item.UsePolicy != InteractionUsePolicy.PersistentOnce)
                            continue;

                        string id = item.InteractionId;
                        string location = $"{paths[i]}:{GetHierarchyPath(item.transform)}";
                        if (id == null)
                        {
                            issues.Add($"Persistent Interaction is missing interactionId at '{location}'.");
                            continue;
                        }

                        if (owners.TryGetValue(id, out string first))
                            issues.Add($"Duplicate Production interactionId '{id}' at '{first}' and '{location}'.");
                        else
                            owners.Add(id, location);

                        HashSet<string> actionIds = new(StringComparer.Ordinal);
                        IReadOnlyList<InteractionEventSO> events = item.Events;
                        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                        {
                            InteractionEventSO interactionEvent = events[eventIndex];
                            if (interactionEvent == null)
                                continue;
                            if (!interactionEvent.SupportsProductionExecution)
                                issues.Add($"Persistent Interaction uses Legacy-only event '{interactionEvent.name}' at '{location}'.");
                            if (interactionEvent.ActionId == null)
                                issues.Add($"Persistent Interaction uses index fallback for event {eventIndex} at '{location}'.");
                            else if (!actionIds.Add(interactionEvent.ActionId))
                                issues.Add($"Duplicate actionId '{interactionEvent.ActionId}' at '{location}'.");
                        }
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            ValidateProductionSources(issues);
            ValidateProductionNpcPrefabs(issues);
            ValidateDungeonTemplateNarrative(issues);
            return issues;
        }

        private static void ValidateDungeonTemplateNarrative(List<string> issues)
        {
            const string path = "Assets/GAME/Scenes/Dungeon_Template.unity";
            string originalPath = SceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                StoryEventRunner[] runners = UnityEngine.Object.FindObjectsByType<StoryEventRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                StoryDialogueHUD[] huds = UnityEngine.Object.FindObjectsByType<StoryDialogueHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                TimedChoicePanel[] choices = UnityEngine.Object.FindObjectsByType<TimedChoicePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (runners.Length != 1)
                    issues.Add($"Dungeon_Template requires exactly one StoryEventRunner; found {runners.Length}.");
                if (huds.Length != 1 || !huds[0].IsPresentationReady)
                    issues.Add("Dungeon_Template requires one ready StoryDialogueHUD.");
                if (choices.Length != 1 || huds.Length != 1 || !huds[0].CanPresentChoices)
                    issues.Add("Dungeon_Template requires one StoryDialogueHUD-connected TimedChoicePanel.");

                InteractableObject[] npcs = UnityEngine.Object.FindObjectsByType<InteractableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(item => item.gameObject.name == "ProductionNpcInteraction")
                    .ToArray();
                if (npcs.Length != 1)
                    issues.Add($"Dungeon_Template requires exactly one ProductionNpcInteraction instance; found {npcs.Length}.");
                else
                {
                    Collider2D trigger = npcs[0].GetComponent<Collider2D>();
                    if (trigger == null || !trigger.isTrigger)
                        issues.Add("Dungeon_Template ProductionNpcInteraction requires a trigger Collider2D.");
                    if (npcs[0].UsePolicy == InteractionUsePolicy.LegacyCompatibility)
                        issues.Add("Dungeon_Template ProductionNpcInteraction cannot use LegacyCompatibility.");
                    if (npcs[0].Events.Count != 1 || npcs[0].Events[0] is not StoryInteractionEventSO storyEvent ||
                        !storyEvent.SupportsProductionExecution || storyEvent.EventDefinition == null)
                    {
                        issues.Add("Dungeon_Template ProductionNpcInteraction requires one valid Production Story event.");
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void ValidateProductionNpcPrefabs(List<string> issues)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GAME/Prefabs/Interaction" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                InteractableObject[] interactables = prefab.GetComponentsInChildren<InteractableObject>(true);
                for (int itemIndex = 0; itemIndex < interactables.Length; itemIndex++)
                {
                    InteractableObject item = interactables[itemIndex];
                    string location = $"{path}:{GetHierarchyPath(item.transform)}";
                    Collider2D trigger = item.GetComponent<Collider2D>();
                    if (trigger == null || !trigger.isTrigger)
                        issues.Add($"Production NPC requires a trigger Collider2D at '{location}'.");
                    if (item.UsePolicy == InteractionUsePolicy.LegacyCompatibility)
                        issues.Add($"Production NPC uses LegacyCompatibility at '{location}'.");

                    IReadOnlyList<InteractionEventSO> events = item.Events;
                    if (events.Count == 0)
                        issues.Add($"Production NPC has no Interaction event at '{location}'.");
                    for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    {
                        InteractionEventSO interactionEvent = events[eventIndex];
                        if (interactionEvent == null || !interactionEvent.SupportsProductionExecution)
                            issues.Add($"Production NPC has a missing or Legacy-only event at '{location}'.");
                        if (interactionEvent is StoryInteractionEventSO storyEvent && storyEvent.EventDefinition == null)
                            issues.Add($"Production NPC Story event is missing its definition at '{location}'.");
                    }
                }
            }
        }

        private static void ValidateProductionSources(List<string> issues)
        {
            string root = Path.Combine(Application.dataPath, "GAME", "Scripts", "Interaction");
            string[] eventFiles = Directory.GetFiles(root, "*InteractionEventSO.cs", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < eventFiles.Length; i++)
            {
                string source = File.ReadAllText(eventFiles[i]);
                if (source.Contains("FindFirstObjectByType<RewardUIPanel>", StringComparison.Ordinal) ||
                    source.Contains("CurrencyWallet", StringComparison.Ordinal) ||
                    source.Contains("InventoryService", StringComparison.Ordinal) ||
                    source.Contains("TrySetState(", StringComparison.Ordinal))
                {
                    issues.Add($"Production Event SO has forbidden owner/UI discovery: '{eventFiles[i]}'.");
                }
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
    }
}
#endif
