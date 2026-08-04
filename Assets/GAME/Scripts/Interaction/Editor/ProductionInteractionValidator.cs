#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    if (runners.Length > 1)
                        issues.Add($"Scene '{paths[i]}' contains {runners.Length} InteractionRunner instances.");

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
            return issues;
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
