using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor-only manual verification entry points for the canonical save owner.
    /// This is not a production save UI or input binding.
    /// </summary>
    internal static class SaveLoadPlayModeVerificationMenu
    {
        private const string SaveMenuPath = "GAME/Debug/Save Load/Save Current";
        private const string LoadMenuPath = "GAME/Debug/Save Load/Load Current";

        [MenuItem(SaveMenuPath)]
        private static void SaveCurrent()
        {
            SaveLoadService service = GetVerificationService("Save");
            service?.Save();
        }

        [MenuItem(LoadMenuPath)]
        private static void LoadCurrent()
        {
            SaveLoadService service = GetVerificationService("Load");
            service?.Load();
        }

        [MenuItem(SaveMenuPath, true)]
        private static bool ValidateSaveCurrent() => IsVerificationAvailable();

        [MenuItem(LoadMenuPath, true)]
        private static bool ValidateLoadCurrent() => IsVerificationAvailable();

        private static bool IsVerificationAvailable()
        {
            return Application.isPlaying && SaveLoadService.Instance != null;
        }

        private static SaveLoadService GetVerificationService(string operation)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[SaveLoadPlayModeVerificationMenu] {operation} is available only in Play Mode.");
                return null;
            }

            SaveLoadService service = SaveLoadService.Instance;
            if (service == null)
            {
                Debug.LogWarning(
                    $"[SaveLoadPlayModeVerificationMenu] {operation} requires the canonical SaveLoadService instance. " +
                    "Start the scene and allow RuntimeBootstrapper to initialize it first.");
            }

            return service;
        }
    }
}
