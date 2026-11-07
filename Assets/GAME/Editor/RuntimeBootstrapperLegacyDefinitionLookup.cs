using Game.Core;
using Game.NonCombat.Progress;
using UnityEditor;

namespace Game.EditorTools
{
    [InitializeOnLoad]
    internal static class RuntimeBootstrapperLegacyDefinitionLookup
    {
        private const string LegacyDefinitionGuid = "289a48d3275f99649a3f67dbc426837f";

        static RuntimeBootstrapperLegacyDefinitionLookup() => InstallResolver();

        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options) => InstallResolver();

        private static void InstallResolver()
        {
            // Install only the callback here: asset lookup waits until the existing bootstrap
            // owner needs it, after asset import and without creating or resetting services.
            RuntimeBootstrapper.EditorLegacyDefinitionResolver = ResolveDefinition;
        }

        private static CharacterProgressionDefinitionSO ResolveDefinition()
        {
            string path = AssetDatabase.GUIDToAssetPath(LegacyDefinitionGuid);
            return AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(path);
        }
    }
}
