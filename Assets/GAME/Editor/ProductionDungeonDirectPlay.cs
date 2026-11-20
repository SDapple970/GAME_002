using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    [InitializeOnLoad]
    internal static class ProductionDungeonDirectPlay
    {
        private const string DungeonPath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string PendingKey = "GAME.ProductionDungeonDirectPlay.Pending";

        static ProductionDungeonDirectPlay()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                string startPath = EditorSceneManager.playModeStartScene != null
                    ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
                    : SceneManager.GetActiveScene().path;
                SessionState.SetBool(PendingKey, startPath == DungeonPath);
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode) return;
            bool pending = SessionState.GetBool(PendingKey, false);
            SessionState.EraseBool(PendingKey);
            if (!pending || SceneManager.GetActiveScene().path != DungeonPath) return;

            var party = PartyRuntime.Instance;
            var progression = CharacterProgressionService.Instance;
            var skills = CharacterSkillSaveParticipant.Instance;
            var save = SaveLoadService.Instance;
            if (party == null || party.StartDefinition == null || progression == null ||
                progression.StartDefinition != party.StartDefinition || skills == null || save == null ||
                save.CurrentOperationState != SaveLoadService.OperationState.Idle ||
                SceneFlowController.Instance != null && SceneFlowController.Instance.IsSceneLoadInProgress)
            {
                Debug.LogWarning("[ProductionDungeonDirectPlay] Direct Play skill initialization skipped: startup configuration is not ready.");
                return;
            }

            // Reuse the authored New Game grant. This initializes memory only and never loads or writes a save.
            skills.ResetForNewGame();
        }
    }
}
