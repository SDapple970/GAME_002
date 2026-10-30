using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Combat.Core;
using Game.Combat.Adapters;
using Game.Combat.Data;
using Game.Combat.Editor;
using Game.Enemies;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Read-only content audit. A clean structural check never approves authored balance.</summary>
    public static class CombatProductionDataAudit
    {
        public const string PlaytestCandidatePath = "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_ProductionPlaytestV1_Candidate.asset";

        [MenuItem("GAME/Verification/Create Disabled Production Playtest V1 Candidate")]
        public static void CreatePlaytestCandidateIfMissing()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Create candidate outside Play Mode.");
            if (AssetDatabase.LoadMainAssetAtPath(PlaytestCandidatePath) != null || File.Exists(PlaytestCandidatePath))
                return; // Never rewrite an existing authored or user-modified asset.
            var config = ScriptableObject.CreateInstance<FinalCombatRuntimeConfigSO>();
            var data = new SerializedObject(config);
            foreach (var pair in new Dictionary<string, int>
            {
                { "normalHandCapacity", 2 }, { "clashHandCapacity", 3 },
                { "bpMaximum", 3 }, { "bpRoundCyclesPerGain", 2 }, { "bpMaximumInvestment", 3 },
                { "clashAttackerDurability", 3 }, { "clashResponderDurability", 3 }, { "clashMaxComparisons", 3 }
            }) data.FindProperty(pair.Key).intValue = pair.Value;
            data.FindProperty("clashDecisionDurationSeconds").floatValue = 5f;
            // Existing supported policies, not a new combat rule or balance proposal.
            data.FindProperty("clashFinalSkillPolicy").intValue = (int)FinalCombatRuntimeConfigSO.ClashFinalSkillPolicy.LastCommittedWinningSkill;
            data.FindProperty("clashTimeoutAttackPolicy").intValue = (int)FinalCombatRuntimeConfigSO.ClashTimeoutAttackPolicy.FirstAvailableSkill;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, PlaytestCandidatePath);
            AssetDatabase.SetLabels(config, new[] { "ProductionPlaytestCandidate", "ApprovedPlaytestV1Numbers", "ContentPending", "Disabled" });
            AssetDatabase.SaveAssetIfDirty(config);
        }

        public static IReadOnlyList<string> CollectPlaytestNumberIssues(FinalCombatRuntimeConfigSO config)
        {
            List<string> issues = new();
            if (config == null) { issues.Add("Playtest Config missing."); return issues; }
            if (config.EnableProductionHandBp) issues.Add("Candidate must remain disabled.");
            if (config.NormalHandCapacity != 2 || config.ClashHandCapacity != 3) issues.Add("Hand requires Normal2/Clash3.");
            if (config.BpMaximum != 3 || config.BpRoundCyclesPerGain != 2 || config.BpMaximumInvestment != 3)
                issues.Add("BP requires maximum3/cycle2/investment3; initial0 and Ultimate1/2/3 use the existing runtime.");
            if (config.ClashAttackerDurability != 3 || config.ClashResponderDurability != 3 || config.ClashMaxComparisons != 3 || config.ClashDecisionDurationSeconds != 5f)
                issues.Add("Clash requires durability3/3, comparisons3 and timeout5s.");
            if (config.FinalSkillPolicy != FinalCombatRuntimeConfigSO.ClashFinalSkillPolicy.LastCommittedWinningSkill ||
                config.TimeoutAttackPolicy != FinalCombatRuntimeConfigSO.ClashTimeoutAttackPolicy.FirstAvailableSkill)
                issues.Add("Explicit supported Clash policies missing.");
            return issues;
        }

        public static IReadOnlyList<string> CollectPlaytestContentIssues(FinalCombatRuntimeConfigSO config)
        {
            List<string> issues = new();
            if (config == null) { issues.Add("Config content unavailable."); return issues; }
            string path = AssetDatabase.GetAssetPath(config);
            if (path.Length > 0)
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    if (IsNonProductionPath(dependency)) issues.Add("Non-production dependency: " + dependency);
            HashSet<string> keys = new(StringComparer.Ordinal);
            bool normal = false, clash = false;
            foreach (var rule in config.HandCardRules)
            {
                if (rule?.Skill == null || string.IsNullOrWhiteSpace(rule.Skill.PersistentKey) || rule.Copies <= 0 ||
                    !Enum.IsDefined(typeof(Game.Combat.Model.CombatCardKind), rule.Kind))
                { issues.Add("Invalid Hand rule: skill/key/copies/kind required."); continue; }
                if (!keys.Add(rule.Skill.PersistentKey)) issues.Add("Duplicate Hand skill placement: " + rule.Skill.PersistentKey);
                if (IsNonProductionPath(AssetDatabase.GetAssetPath(rule.Skill))) issues.Add("Test Hand skill: " + rule.Skill.PersistentKey);
                normal |= rule.Kind == Game.Combat.Model.CombatCardKind.Normal;
                clash |= rule.Kind == Game.Combat.Model.CombatCardKind.Clash;
            }
            if (!normal || !clash) issues.Add("Content Pending: authored Normal and Clash rules/copies and equipped snapshot required.");
            HashSet<string> sources = new(StringComparer.Ordinal);
            var enemyKeys = new HashSet<string>(Assets<EnemyDefinitionSO>().Where(value => !IsNonProductionPath(AssetDatabase.GetAssetPath(value)))
                .Select(value => value.PersistentKey), StringComparer.Ordinal);
            foreach (string source in config.EnemyHandSourceKeys)
                if (string.IsNullOrWhiteSpace(source) || !sources.Add(source.Trim()) || !enemyKeys.Contains(source.Trim()))
                    issues.Add("Enemy Hand source missing, duplicate or unresolved: " + source);
            return issues;
        }

        [MenuItem("GAME/Verification/Audit Production Playtest V1 Candidate")]
        public static void RunPlaytestCandidateAudit()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
            Directory.CreateDirectory("Logs");
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(PlaytestCandidatePath);
            var report = new StringBuilder("# Production Playtest V1 candidate readiness\n\nNumeric approval does not approve content or scene activation.\n\n");
            var numbers = CollectPlaytestNumberIssues(config);
            report.AppendLine("Approved numbers: " + (numbers.Count == 0 ? "MATCH / DISABLED" : "INVALID"));
            Issues(report, "Config", numbers);
            var content = new List<string>(CollectPlaytestContentIssues(config));
            var starts = Assets<CharacterStartDefinitionSO>().Where(value => !IsNonProductionPath(AssetDatabase.GetAssetPath(value))).ToArray();
            if (starts.Length == 0) content.Add("Content Pending: official CharacterStartDefinition, Character ID, leader/lineup and progression.");
            foreach (var start in starts) content.AddRange(CollectStartIssues(start));
            if (!Assets<EnemyDefinitionSO>().Any(value => !IsNonProductionPath(AssetDatabase.GetAssetPath(value))))
                content.Add("Content Pending: Enemy definition/source provenance; existing field loadout is compatibility data.");
            Issues(report, "Readiness", content);
            report.AppendLine("\nReadiness: " + (content.Count > 0 || numbers.Count > 0 ? "BLOCKED" : "VALIDATION PENDING"));
            report.AppendLine("Runtime snapshot, registry, actor stats, acquisition/equipment and Scene wiring require separate validation; asset location is not approval.");
            report.AppendLine("Enemy Hand: empty source list preserves ordinary authored-skill AI; non-empty keys require exact provenance matching.");
            report.AppendLine("MP/Posture/Mental/pressure values are existing SO class defaults, not approved protagonist stats or new balance decisions.");
            File.WriteAllText("Logs/CBT-R2-08-P2B3D3-Readiness.md", report.ToString());
        }

        private static readonly string[] ProductionPaths =
        {
            "Assets/GAME/Scenes/TitleScene.unity",
            "Assets/GAME/Scenes/Dungeon_1_Production.unity",
            "Assets/GAME/Prefabs/CombatRuntime.prefab",
            "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab"
        };

        [MenuItem("GAME/Verification/Audit Combat Production Data")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Run the content audit outside Play Mode.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CBT-R2-07-ContentAudit.md", BuildReport());
            Debug.Log("[CombatProductionDataAudit] Report: Logs/CBT-R2-07-ContentAudit.md. Findings require review; no assets saved.");
        }

        public static string BuildReport()
        {
            StringBuilder report = new("# CBT-R2-07 read-only content audit\n\nStructural evidence only; content approval and Production Play Mode remain separate.\n\n");
            SkillDefinitionSO[] skills = Assets<SkillDefinitionSO>();
            CharacterProgressionDefinitionSO[] characters = Assets<CharacterProgressionDefinitionSO>();
            EnemyDefinitionSO[] enemies = Assets<EnemyDefinitionSO>();
            Issues(report, "Skill identity", SkillDefinitionPersistentIdentityValidator.CollectPersistentKeyIssues(skills));
            Issues(report, "Character identity", CollectCharacterIssues(characters));
            Issues(report, "Unique mapping", CharacterUniqueSkillValidator.CollectIssues(characters, skills));
            Issues(report, "Enemy acquisition", EnemySkillAcquisitionValidator.CollectIssues(enemies, skills));
            foreach (CharacterStartDefinitionSO start in Assets<CharacterStartDefinitionSO>())
                Issues(report, AssetDatabase.GetAssetPath(start), CollectStartIssues(start));
            foreach (EnemyDefinitionSO enemy in enemies)
                if (enemy.FinalCombatBehaviorProfile == null)
                    report.AppendLine($"- MISSING AI profile: {AssetDatabase.GetAssetPath(enemy)}");
            foreach (ItemCatalogSO catalog in Assets<ItemCatalogSO>())
            {
                List<string> issues = new(); catalog.CollectValidationIssues(issues);
                Issues(report, AssetDatabase.GetAssetPath(catalog), issues);
            }
            ItemDefinitionSO[] items = Assets<ItemDefinitionSO>();
            foreach (CombatItemCatalogSO catalog in Assets<CombatItemCatalogSO>())
            {
                List<string> issues = new(); catalog.CollectValidationIssues(issues);
                foreach (CombatItemDefinitionSO item in catalog.Items)
                    if (item != null && !items.Any(value => value.ItemId == item.ItemId))
                        issues.Add($"Inventory definition missing for '{item.ItemId}'.");
                Issues(report, AssetDatabase.GetAssetPath(catalog), issues);
            }
            report.AppendLine("\n## Assets (location/classification is not approval)\n");
            Inventory<CharacterStartDefinitionSO>(report);
            Inventory<CharacterProgressionDefinitionSO>(report);
            Inventory<EnemyDefinitionSO>(report);
            Inventory<FinalCombatEnemyBehaviorProfileSO>(report);
            Inventory<ItemCatalogSO>(report);
            Inventory<CombatItemCatalogSO>(report);
            Inventory<CombatItemDefinitionSO>(report);
            Inventory<CombatStatusDefinitionSO>(report);
            report.AppendLine("\n| Skill asset | key | id | role | MP / Final MP | BP effect | tag / target |\n|---|---|---|---|---|---|---|");
            foreach (SkillDefinitionSO skill in skills)
                report.AppendLine($"| {AssetDatabase.GetAssetPath(skill)} | {skill.PersistentKey} | {skill.skillId} | {skill.OwnershipCategory} | {skill.MpCost} / {skill.FinalMpCost} | {skill.BpEnhancementKind}:{skill.BpEnhancementPerPoint} | {skill.tag} / {skill.targeting} |");

            foreach (string path in ProductionPaths)
            {
                report.AppendLine($"\n## {path}\n");
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    report.AppendLine("- MISSING asset"); continue;
                }
                foreach (string dependency in AssetDatabase.GetDependencies(path, true).OrderBy(value => value))
                    if (IsNonProductionPath(dependency))
                        report.AppendLine($"- REVIEW non-production dependency: {dependency}");
                if (path.EndsWith(".unity", StringComparison.Ordinal))
                {
                    // Preview scenes neither replace the user's setup nor run bootstrap Awake/OnEnable.
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try { InspectRoots(report, scene.GetRootGameObjects(), path); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                else InspectRoots(report, new[] { AssetDatabase.LoadAssetAtPath<GameObject>(path) }, path);
            }
            report.AppendLine("\nPending: official protagonist ID/growth; normal/tricky enemy definitions and Film mapping; item/status content; Ultimate/AllOut balance approval. Party owned-size six is not enforced by existing PartyRuntime. No save policy changed.");
            return report.ToString();
        }

        public static IReadOnlyList<string> CollectStartIssues(CharacterStartDefinitionSO start)
        {
            List<string> issues = new();
            if (start == null) { issues.Add("Start definition missing."); return issues; }
            if (!start.TryValidate(out string message)) issues.Add(message);
            if (start.InitialMemberIds.Count != 1 || start.InitialCombatMemberIds.Count != 1 ||
                start.InitialMemberIds.FirstOrDefault() != start.InitialLeaderId ||
                start.InitialCombatMemberIds.FirstOrDefault() != start.InitialLeaderId)
                issues.Add("CBT-R2-07 New Game requires one owned member, one combat member and the same leader.");
            return issues;
        }

        public static IReadOnlyList<string> CollectCharacterIssues(IEnumerable<CharacterProgressionDefinitionSO> definitions)
        {
            List<string> issues = new(); HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (CharacterProgressionDefinitionSO definition in definitions)
            {
                string id = definition == null ? null : definition.CharacterId?.Trim();
                if (string.IsNullOrEmpty(id)) issues.Add("Character ID missing.");
                else if (!ids.Add(id)) issues.Add($"Duplicate Character ID '{id}'.");
            }
            return issues;
        }

        public static bool IsNonProductionPath(string path)
        {
            string normalized = "/" + path.Replace('\\', '/').ToLowerInvariant();
            return new[] { "/tests/", "/test/", "/fixtures/", "/debug/", "/debugging/", "/demo/" }
                .Any(normalized.Contains);
        }

        private static void InspectRoots(StringBuilder report, GameObject[] roots, string context)
        {
            Component[] components = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            int entries = components.OfType<CombatEntryPoint>().Count();
            if (!context.Contains("TitleScene") && !context.Contains("ProductionDungeonUI") && entries != 1)
                report.AppendLine($"- OWNER ERROR CombatEntryPoint count: {entries}");
            foreach (CombatEntryPoint entry in components.OfType<CombatEntryPoint>())
            {
                Issues(report, "Production registry", SkillDefinitionPersistentIdentityValidator.CollectRegistrationIssues(entry.RegisteredSkillDefinitions, context));
                if (entry.RegisteredSkillDefinitions.Count == 0) report.AppendLine("- MISSING skill registry (scene overrides must be inspected separately)");
            }
            CombatEntryPoint registryOwner = components.OfType<CombatEntryPoint>().FirstOrDefault();
            if (registryOwner != null)
            {
                HashSet<int> ids = new(registryOwner.RegisteredSkillDefinitions.Where(value => value != null).Select(value => value.skillId));
                foreach (CombatSkillLoadoutComponent loadout in components.OfType<CombatSkillLoadoutComponent>())
                    foreach (int id in loadout.SkillIds ?? Array.Empty<int>())
                        if (!ids.Contains(id)) report.AppendLine($"- UNRESOLVED skill {id}: {Hierarchy(loadout.transform)} (local registry; prefab scene overrides may supply it)");
                foreach (EnemySourceComponent source in components.OfType<EnemySourceComponent>())
                    if (source.Definition == null) report.AppendLine($"- MISSING Enemy definition: {Hierarchy(source.transform)}");
                foreach (var group in components.OfType<Game.Combat.Integration.CombatEncounterGroup>())
                    foreach (GameObject enemy in group.GetActiveEnemies())
                        if (enemy.GetComponent<EnemySourceComponent>() == null)
                            report.AppendLine($"- MISSING EnemySourceComponent: {Hierarchy(enemy.transform)} (encounter {group.EncounterId})");
            }
            if (components.OfType<CombatEntryPoint>().Any() && !components.Any(value => value != null && value.GetType().Name == "CombatItemUseAdapter"))
                report.AppendLine("- MISSING CombatItemUseAdapter; combat items unavailable without authored catalog and Inventory reference.");
            foreach (Component component in components)
            {
                if (component == null) { report.AppendLine("- SERIALIZATION ERROR missing script"); continue; }
                Type type = component.GetType();
                if (type.Namespace != null && (type.Namespace.Contains("Debug") || type.Namespace.Contains("Legacy") || type.Namespace.Contains("Demo")))
                    report.AppendLine($"- REVIEW non-production component: {Hierarchy(component.transform)} :: {type.FullName}");
                if (!(component is MonoBehaviour)) continue;
                string[] watched = { "RuntimeBootstrapper", "CombatEntryPoint", "CombatItemUseAdapter", "InventoryService", "CharacterProgressionService", "EnemySourceComponent", "CombatSkillLoadoutComponent", "CharacterPartyBinding", "FinalCombatUIBinder", "UIScreenRouter", "GameUIRootController", "CombatBpValidationOptIn", "CombatHandValidationOptIn" };
                bool describe = watched.Contains(type.Name);
                if (describe) report.AppendLine($"\n### {Hierarchy(component.transform)} :: {type.Name}");
                SerializedObject serialized = new(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                    {
                        UnityEngine.Object reference = property.objectReferenceValue;
                        if (reference == null && property.objectReferenceInstanceIDValue != 0)
                            report.AppendLine($"- BROKEN {Hierarchy(component.transform)} :: {type.Name}.{property.propertyPath}");
                        if (describe)
                            report.AppendLine($"- `{property.propertyPath}` → {ReferenceLabel(reference)}");
                    }
                    else if (describe && property.propertyType == SerializedPropertyType.String)
                        report.AppendLine($"- `{property.propertyPath}` = {property.stringValue}");
                    else if (describe && property.propertyType == SerializedPropertyType.Integer && property.propertyPath.StartsWith("skillIds.Array.data", StringComparison.Ordinal))
                        report.AppendLine($"- `{property.propertyPath}` = {property.intValue}");
                }
            }
        }

        private static string ReferenceLabel(UnityEngine.Object reference)
        {
            if (reference == null) return "UNASSIGNED (required vs runtime/optional must be reviewed)";
            string asset = AssetDatabase.GetAssetPath(reference);
            string hierarchy = reference is Component component ? Hierarchy(component.transform) :
                reference is GameObject go ? Hierarchy(go.transform) : null;
            return asset.Length > 0 ? hierarchy == null ? asset : asset + " :: " + hierarchy : hierarchy ?? reference.name;
        }

        private static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
        private static T[] Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/GAME" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>).Where(value => value != null).ToArray();
        private static void Inventory<T>(StringBuilder report) where T : UnityEngine.Object
        {
            T[] assets = Assets<T>();
            report.AppendLine($"- {typeof(T).Name}: {assets.Length}");
            foreach (T asset in assets) report.AppendLine($"  - {AssetDatabase.GetAssetPath(asset)}");
        }
        private static void Issues(StringBuilder report, string context, IReadOnlyList<string> issues)
        {
            foreach (string issue in issues) report.AppendLine($"- INVALID {context}: {issue}");
        }
    }
}
