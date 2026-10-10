using System;
using System.IO;
using System.Linq;
using System.Text;
using Game.Combat.Core;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Editor;
using Game.Combat.Integration;
using Game.Enemies;
using Game.NonCombat.Progress;
using Game.UI;
using Game.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Read-only audit. Empty mappings are valid infrastructure, never approved Production content.</summary>
    public static class FilmUniqueProductionContentAudit
    {
        [MenuItem("GAME/Verification/Audit Film Unique Production Content")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Run the read-only audit outside Play Mode with a saved scene.");
            SkillDefinitionSO[] skills = Assets<SkillDefinitionSO>();
            EnemyDefinitionSO[] enemies = Assets<EnemyDefinitionSO>();
            CharacterProgressionDefinitionSO[] characters = Assets<CharacterProgressionDefinitionSO>();
            RequireEmpty(SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(skills));
            RequireEmpty(EnemySkillAcquisitionValidator.CollectIssues(enemies, skills));
            RequireEmpty(CharacterUniqueSkillValidator.CollectIssues(characters, skills));
            RequireEmpty(ProductionUIRoutingValidator.ValidateProductionAssets());
            Scene scene = EditorSceneManager.OpenScene(ProductionCharacterSkillUISetup.ScenePath);
            DungeonOneProductionMigrationUtility.ValidateProductionScene();
            ProductionCharacterSkillUISetup.ValidateScene(scene);
            GameObject[] roots = scene.GetRootGameObjects();
            CombatEntryPoint entry = roots.SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
            RequireEmpty(SkillDefinitionPersistentIdentityValidator.CollectRegistrationIssues(entry.RegisteredSkillDefinitions, "Dungeon_1_Production"));
            // Scene validators unload unreferenced assets; reacquire them before reporting asset paths.
            skills = Assets<SkillDefinitionSO>();
            characters = Assets<CharacterProgressionDefinitionSO>();
            StringBuilder report = new("# COMBAT-17C-6 Production Content Audit\n\n");
            report.AppendLine("All existing validators passed. This does not approve empty Film/Unique mappings. No asset or scene was saved.\n");
            report.AppendLine("| Asset | Persistent Key | Category | Owner | Production registry | Mapping evidence |\n| --- | --- | --- | --- | --- | --- |");
            foreach (SkillDefinitionSO skill in skills)
                report.AppendLine($"| {AssetDatabase.GetAssetPath(skill)} | {skill.PersistentKey} | {skill.OwnershipCategory} | {skill.UniqueOwnerCharacterId ?? "none"} | {entry.RegisteredSkillDefinitions.Contains(skill)} | Existing identity/loadout only; no approved Film/Unique assignment | ");
            foreach (CharacterProgressionDefinitionSO character in characters)
                report.AppendLine($"| {AssetDatabase.GetAssetPath(character)} | {character.CharacterId} | Character level {character.StartingLevel}–{character.MaximumLevel} | {character.CharacterId} | Test asset | Unique definitions: {character.UniqueSkillUnlocks.Count} | ");
            report.AppendLine($"\nEnemyDefinitionSO asset count: {enemies.Length}; Film assets: {skills.Count(skill => skill.OwnershipCategory == SkillOwnershipCategory.Film)}; Unique assets: {skills.Count(skill => skill.OwnershipCategory == SkillOwnershipCategory.Unique)}.\n");
            report.AppendLine("| Production encounter | Enemy object | Definition/source | Legacy skill IDs |\n| --- | --- | --- | --- |");
            foreach (CombatEncounterGroup group in roots.SelectMany(root => root.GetComponentsInChildren<CombatEncounterGroup>(true)).OrderBy(group => group.EncounterId))
                foreach (GameObject enemy in group.GetActiveEnemies())
                {
                    EnemySourceComponent source = enemy.GetComponent<EnemySourceComponent>();
                    CombatSkillLoadoutComponent loadout = enemy.GetComponent<CombatSkillLoadoutComponent>();
                    report.AppendLine($"| {group.EncounterId} | {enemy.name} | {(source != null ? "source component present" : "not authored")} | {string.Join(",", loadout != null ? loadout.SkillIds : Array.Empty<int>())} |");
                }
            report.AppendLine("\nProduction authoring: Pending. Existing enemy attacks are not evidence of acquisition mapping. Repository design search found no approved Enemy→Film / Character→Unique mapping. The user request establishes levels 5/10/20/40 and two Unconfigured entries only. Isolated in-memory verification is authorized; final Production assignment/balance is not.\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Combat17C6_ContentAudit.md", report.ToString());
            Debug.Log("[FilmUniqueContentAudit] PASS: identities, acquisition, Unique, Production scene and UI validators. Authoring remains Pending.");
        }

        private static T[] Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/GAME" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).ToArray();

        private static void RequireEmpty(System.Collections.Generic.IReadOnlyList<string> issues)
        {
            if (issues.Count != 0) throw new InvalidOperationException(string.Join("\n", issues));
        }
    }
}
