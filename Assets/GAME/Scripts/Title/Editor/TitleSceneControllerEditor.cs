#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GAME.Title;

namespace GAME.Title.Editor
{
    [CustomEditor(typeof(TitleSceneController))]
    public sealed class TitleSceneControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty dungeonSceneName;

        private void OnEnable()
        {
            dungeonSceneName = serializedObject.FindProperty("dungeonSceneName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Keep the existing serialized field, but replace its free-form text field
            // with a Build Settings-backed selector.
            DrawPropertiesExcluding(serializedObject, "dungeonSceneName");
            DrawDungeonSceneSelector();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDungeonSceneSelector()
        {
            if (dungeonSceneName == null)
            {
                return;
            }

            BuildSceneOption[] enabledScenes = GetEnabledBuildScenes();
            string currentValue = dungeonSceneName.stringValue ?? string.Empty;
            var labels = new List<string>(enabledScenes.Length + 1);

            foreach (BuildSceneOption scene in enabledScenes)
            {
                labels.Add(scene.Name);
            }

            int currentIndex = Array.FindIndex(enabledScenes, scene =>
                string.Equals(scene.Name, currentValue, StringComparison.Ordinal));
            int enabledSceneOffset = 0;

            if (currentIndex < 0)
            {
                if (string.IsNullOrWhiteSpace(currentValue))
                {
                    labels.Insert(0, "<None selected>");
                    currentIndex = 0;
                    enabledSceneOffset = 1;
                }
                else
                {
                    // Keep an invalid saved value visible and selectable without
                    // silently replacing it with another scene.
                    labels.Add($"{currentValue} (not enabled)");
                    currentIndex = labels.Count - 1;
                }
            }

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Dungeon Scene", currentIndex, labels.ToArray());
            if (EditorGUI.EndChangeCheck() && selectedIndex >= enabledSceneOffset)
            {
                int enabledIndex = selectedIndex - enabledSceneOffset;
                if (enabledIndex >= 0 && enabledIndex < enabledScenes.Length)
                {
                    dungeonSceneName.stringValue = enabledScenes[enabledIndex].Name;
                }
            }

            if (string.IsNullOrWhiteSpace(currentValue))
            {
                EditorGUILayout.HelpBox("Dungeon scene name is empty.", MessageType.Warning);
            }
            else if (Array.FindIndex(enabledScenes, scene =>
                         string.Equals(scene.Name, currentValue, StringComparison.Ordinal)) < 0)
            {
                EditorGUILayout.HelpBox(
                    $"Scene '{currentValue}' is not enabled in Build Settings.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"✓ {currentValue} is enabled in Build Settings.",
                    MessageType.Info);
            }
        }

        private static BuildSceneOption[] GetEnabledBuildScenes()
        {
            var result = new List<BuildSceneOption>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || string.IsNullOrWhiteSpace(scene.path))
                {
                    continue;
                }

                result.Add(new BuildSceneOption(Path.GetFileNameWithoutExtension(scene.path)));
            }

            return result.ToArray();
        }

        private readonly struct BuildSceneOption
        {
            public BuildSceneOption(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}
#endif
