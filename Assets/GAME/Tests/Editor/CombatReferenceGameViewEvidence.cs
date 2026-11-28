#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.UI
{
    /// <summary>Optional interactive-editor evidence. Batch renders are deliberately named separately.</summary>
    internal static class CombatReferenceGameViewEvidence
    {
        internal static void Configure()
        {
            if (Application.isBatchMode) return;
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type modeType = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { Enum.Parse(modeType, "FixedResolution"), (object)1990, 1080, "Combat reference 1990x1080" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, count - 1);
            view.Show();
            view.Focus();
        }

        internal static IEnumerator Capture(string state)
        {
            if (Application.isBatchMode) yield break;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(Screen.width, Is.EqualTo(1990), "Evidence must use the source-video resolution.");
            Assert.That(Screen.height, Is.EqualTo(1080));
            Directory.CreateDirectory("Logs/CombatUI-2D-2/GameView");
            string path = $"Logs/CombatUI-2D-2/GameView/{state}_1990x1080.png";
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 15 && !File.Exists(path); frame++) yield return null;
            Assert.That(File.Exists(path), Is.True, "Game View screenshot was not written.");
            Debug.Log($"[CombatUI-2D] Actual Game View: {path}, {Screen.width}x{Screen.height}.");
        }
    }
}
#endif
