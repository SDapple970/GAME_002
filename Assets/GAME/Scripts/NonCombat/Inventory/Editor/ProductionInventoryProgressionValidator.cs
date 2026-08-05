using System.Collections.Generic;
using Game.NonCombat.Progress;
using UnityEditor;
using UnityEngine;

namespace Game.NonCombat.Inventory.Editor
{
    public static class ProductionInventoryProgressionValidator
    {
        [MenuItem("GAME/Validation/Inventory and Progression")]
        public static void Validate()
        {
            List<string> issues = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemCatalogSO"))
                AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guid))?.CollectValidationIssues(issues);

            HashSet<string> characterIds = new(System.StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterProgressionDefinitionSO"))
            {
                CharacterProgressionDefinitionSO definition = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                string id = CharacterProgressionService.NormalizeId(definition != null ? definition.CharacterId : null);
                if (id == null) issues.Add("Character progression definition has no stable ID.");
                else if (!characterIds.Add(id)) issues.Add($"Duplicate character progression ID '{id}'.");
                if (definition != null && definition.StartingLevel < definition.MaximumLevel && !definition.TryGetRequiredExperience(definition.StartingLevel, out _))
                    issues.Add($"Character '{id}' has an invalid XP curve.");
            }

            ValidateSingletonCount<InventoryService>(issues);
            ValidateSingletonCount<CurrencyWallet>(issues);
            ValidateSingletonCount<CharacterProgressionService>(issues);
            CharacterProgressionService[] progressionServices = Resources.FindObjectsOfTypeAll<CharacterProgressionService>();
            foreach (CharacterProgressionService service in progressionServices)
                if (service != null && string.IsNullOrWhiteSpace(service.DefaultRewardTargetId))
                    issues.Add($"CharacterProgressionService '{service.name}' has no default Reward EXP target; targetless EXP will remain pending.");
            if (issues.Count == 0) Debug.Log("[ProductionInventoryProgressionValidator] Validation passed.");
            else foreach (string issue in issues) Debug.LogWarning($"[ProductionInventoryProgressionValidator] {issue}");
        }

        private static void ValidateSingletonCount<T>(List<string> issues) where T : Object
        {
            int count = Resources.FindObjectsOfTypeAll<T>().Length;
            if (count > 1) issues.Add($"Multiple Production {typeof(T).Name} instances are loaded ({count}).");
        }
    }
}
