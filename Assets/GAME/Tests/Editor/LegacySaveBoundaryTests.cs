using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static Game.Tests.Integration.DebugFreeProductionTestSupport;

namespace Game.Tests.Integration
{
    public sealed class LegacySaveBoundaryTests
    {
        private const string Hero = "hero.test";
        private const string ColdPrefix = "Logs/CBT-D4E1DC2AR4A-Cold-";
        private ProductionInitializationCompatibilityTests _fixture;

        [TestCase(8, "missing", "MissingInLegacySource")]
        [TestCase(8, "empty", "ExplicitEmpty")]
        [TestCase(8, "null", "ExplicitNull")]
        [TestCase(14, "missing", "Unknown")]
        [TestCase(14, "empty", "ExplicitEmpty")]
        [TestCase(14, "known", "ExplicitSkills")]
        [TestCase(14, "unknown", "ExplicitSkills")]
        public void OriginalEvidence_SurvivesMigration_AndIsNotSerialized(int version, string variant, string expected)
        {
            string raw = Raw(version, variant);
            Assert.That(GameSaveDataMigrator.TryMigrate(raw, out var data, out _, out string error), Is.True, error);
            Assert.That(data.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(data.ReadProvenance.SourceSchemaVersion, Is.EqualTo(version));
            Assert.That(data.ReadProvenance.ForCharacter(Hero).ToString(), Is.EqualTo(expected));
            Assert.That(data.ReadProvenance.Matches(data), Is.True);
            Assert.That(data.ReadProvenance.ForCharacter("not.in.source"), Is.EqualTo(EquipmentReadOrigin.Unknown));
            string canonical = SaveSerializer.ToJson(data);
            Assert.That(canonical, Does.Not.Contain("ReadProvenance").And.Not.Contain("SourceSchemaVersion"));
            Assert.That(SaveSerializer.FromGameSaveJson(canonical).ReadProvenance, Is.Null);
        }

        [Test]
        public void UnprovenVersionAndDuplicateProperties_DoNotProveHistoricalAbsence()
        {
            var root = JObject.Parse(Raw(8, "missing")); root.Remove("header");
            var info = SaveReadProvenance.FromOriginal(root.ToString(), new GameSaveData());
            Assert.That(info.ForCharacter(Hero), Is.EqualTo(EquipmentReadOrigin.Unknown));
            string duplicate = Raw(8, "empty").Replace("\"equippedSkillKeys\": []", "\"equippedSkillKeys\": [], \"equippedSkillKeys\": []");
            Assert.That(duplicate, Is.Not.EqualTo(Raw(8, "empty")));
            Assert.That(SaveReadProvenance.FromOriginal(duplicate, new GameSaveData()), Is.Null);
        }

        [Test]
        public void Provenance_IsInvalidatedByModelEditsEquipmentChangesSaveAndNewGame()
        {
            Prepare();
            string raw = Raw(8, "missing"); Load(raw);
            var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.MissingInLegacySource));
            owner.Runtime.TryAcquire(Hero, "skill.production.0001");
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.Unknown));
            Load(raw);
            Assert.That(Owner<SaveLoadService>("_save").TrySave(out string message), Is.True, message);
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.Unknown));
            Assert.That(Owner<SaveLoadService>("_save").TryLoad(out message), Is.True, message);
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.ExplicitEmpty));
            Assert.That(GameSaveDataMigrator.TryMigrate(raw, out var changed, out _, out message), Is.True, message);
            changed.characterSkills.characters.Single().acquiredSkillKeys.Add("key.changed");
            owner.RestoreSaveData(changed);
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.Unknown));
            Load(raw); Call(_fixture, "NewGame");
            Assert.That(owner.GetEquipmentReadOrigin(Hero), Is.EqualTo(EquipmentReadOrigin.Unknown));
            Assert.That(owner.Runtime.GetEquippedSkills("char.anand_de_morte"), Has.Count.EqualTo(2));
        }

        [TestCase(8, "missing", "MissingInLegacySource")]
        [TestCase(14, "missing", "Unknown")]
        [TestCase(14, "empty", "ExplicitEmpty")]
        [TestCase(14, "unknown", "ExplicitSkills")]
        [TestCase(14, "mixed", "ExplicitSkills")]
        public void LoadPreservesOriginal_ButNoFallbackOrPartialCombatIsCreated(int version, string variant, string expected)
        {
            Prepare(); string raw = Raw(version, variant); Load(raw);
            var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Assert.That(owner.GetEquipmentReadOrigin(Hero).ToString(), Is.EqualTo(expected));
            var request = (CombatStartRequest)Call(_fixture, "Request", Hero, new[] { 1, 2 }, 11);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, request.AllyFieldObjects[0], Owner<PartyRuntime>("_party"), Owner<CharacterProgressionService>("_progress"), out string message), Is.True, message);
            Call(_fixture, "CreateEntry", true);
            var entry = Owner<CombatEntryPoint>("_entry"); int started = 0; entry.OnCombatStarted += _ => started++;
            LogAssert.Expect(LogType.Warning, new Regex(@"Combat start rejected\..*" + (variant == "unknown" || variant == "mixed" ? "unresolved or ambiguous" : "automatic fallback is disabled")));
            Assert.That(entry.StartCombat(request), Is.False); Assert.That(entry.ActiveSession, Is.Null); Assert.That(started, Is.Zero);
            Assert.That(request.TryGetAllyLoadoutSnapshot(request.AllyFieldObjects[0], out _), Is.False);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(File.ReadAllText(Owner<SaveLoadService>("_save").PrimarySavePath), Is.EqualTo(raw));
            AssertSavedState(version, variant);
        }

        [Test]
        public void LegacyLookup_DoesNotSeedNewGamePartyProgressionOrSkills()
        {
            Prepare(); var progress = Owner<CharacterProgressionService>("_progress");
            Assert.That(progress.TryGetDefinition(Hero, out _), Is.True);
            Assert.That(progress.TryGetState(Hero, out _, out _), Is.False);
            Call(_fixture, "NewGame");
            Assert.That(Owner<PartyRuntime>("_party").Members, Is.EqualTo(new[] { "char.anand_de_morte" }));
            Assert.That(Owner<CharacterSkillSaveParticipant>("_skills").Runtime.GetAcquiredSkills(Hero), Is.Empty);
            var data = Owner<SaveLoadService>("_save").CaptureGameSaveDataSnapshot();
            Assert.That(data.progression.characters.Any(row => row.characterId == Hero), Is.False);
        }

        [TestCase("skill.production.0002")]
        [TestCase("legacy.skill")]
        public void RawUnownedEquipment_NormalizationCannotHideAnInvalidPartialLoadout(string unownedKey)
        {
            Prepare();
            var root = JObject.Parse(Raw(14, "known"));
            var row = (JObject)root["characterSkills"]["characters"][0];
            row["acquiredSkillKeys"] = new JArray("skill.production.0001");
            row["equippedSkillKeys"] = new JArray("skill.production.0001", unownedKey);
            string raw = root.ToString(); Load(raw);
            var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Assert.That(owner.HadDiscardedEquipment(Hero), Is.True);
            Assert.That(owner.Runtime.GetEquippedSkills(Hero), Is.EqualTo(new[] { "skill.production.0001" }));
            Call(_fixture, "CreateEntry", true);
            var request = (CombatStartRequest)Call(_fixture, "Request", Hero, new[] { 1, 2 }, 11);
            var entry = Owner<CombatEntryPoint>("_entry");
            LogAssert.Expect(LogType.Warning, new Regex("Combat start rejected.*discarded by save normalization.*partial combat snapshot is not allowed"));
            Assert.That(entry.StartCombat(request), Is.False);
            Assert.That(entry.ActiveSession, Is.Null);
            Assert.That(request.TryGetAllyLoadoutSnapshot(request.AllyFieldObjects[0], out _), Is.False);
            Assert.That(File.ReadAllText(Owner<SaveLoadService>("_save").PrimarySavePath), Is.EqualTo(raw));
        }

        [Test]
        public void ResolvedLegacySkills_CreateOriginalSnapshot_AndApprovedHandStartsCombat()
        {
            Prepare(); Load(Raw(14, "known")); Call(_fixture, "CreateEntry", true);
            var request = (CombatStartRequest)Call(_fixture, "Request", Hero, new[] { 1, 2 }, 12);
            var entry = Owner<CombatEntryPoint>("_entry");
            Assert.That(PersistentSkillCombatLoadoutBridge.TryApplyForProduction(request, entry.RegisteredSkillDefinitions, entry, out string message), Is.True, message);
            Assert.That(request.TryGetAllyLoadoutSnapshot(request.AllyFieldObjects[0], out var snapshot), Is.True);
            Assert.That(snapshot.Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(entry.StartCombat(request), Is.True);
            ProductionInitializationCompatibilityTests.AssertLegacySession(entry.ActiveSession);
            AssertSavedState(14, "known");
        }

        [Test]
        public void ExplicitLegacyReference_RegistersBeforeLoad_WithoutSeedingOrReplacingNewGameParty()
        {
            Prepare(); var bootstrap = Owner<RuntimeBootstrapper>("_bootstrap");
            var progress = Owner<CharacterProgressionService>("_progress");
            var definition = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Assert.That(new SerializedObject(bootstrap).FindProperty("legacyCharacterDefinition").objectReferenceValue, Is.SameAs(definition));
            Assert.That(progress.TryGetDefinition(Hero, out var registered), Is.True); Assert.That(registered, Is.SameAs(definition));
            Assert.That(progress.TryGetState(Hero, out _, out _), Is.False);
            Call(_fixture, "NewGame");
            var party = Owner<PartyRuntime>("_party");
            Assert.That(party.LeaderCharacterId, Is.EqualTo("char.anand_de_morte"));
            for (int i = 0; i < 2; i++) Call(bootstrap, "BootstrapCoreServices", false, false, false);
            Assert.That(party.Members, Is.EqualTo(new[] { "char.anand_de_morte" }));
            Assert.That(party.LeaderCharacterId, Is.EqualTo("char.anand_de_morte"));
            Assert.That(progress.TryGetState(Hero, out _, out _), Is.False);
            Assert.That(Owner<CharacterSkillSaveParticipant>("_skills").Runtime.GetAcquiredSkills(Hero), Is.Empty);
        }

        [Test]
        public void InvalidExplicitReference_IsRejectedWithoutEditorLookup_AndConflictsPreserveOwner()
        {
            _fixture = new ProductionInitializationCompatibilityTests(); Call(_fixture, "Prepare", false, true);
            var bootstrap = Owner<RuntimeBootstrapper>("_bootstrap"); var progress = Owner<CharacterProgressionService>("_progress");
            var invalid = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Character/CP_AnandDeMorte.asset");
            Set(bootstrap, "legacyCharacterDefinition", invalid);
            LogAssert.Expect(LogType.Warning, new Regex("Legacy definition registration rejected: The existing hero.test definition is required"));
            Call(bootstrap, "RegisterExistingLegacyDefinition", progress, true);
            Assert.That(progress.TryGetDefinition(Hero, out _), Is.False, "An explicit bad reference must not use Editor GUID lookup.");
            var original = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Set(bootstrap, "legacyCharacterDefinition", original); Call(bootstrap, "RegisterExistingLegacyDefinition", progress, false);
            var conflict = Object.Instantiate(original);
            try
            {
                Set(bootstrap, "legacyCharacterDefinition", conflict);
                LogAssert.Expect(LogType.Warning, new Regex("Legacy definition registration rejected: A different definition already owns hero.test"));
                Call(bootstrap, "RegisterExistingLegacyDefinition", progress, true);
                Assert.That(progress.TryGetDefinition(Hero, out var resolved), Is.True); Assert.That(resolved, Is.SameAs(original));
                Assert.That(progress.TryGetState(Hero, out _, out _), Is.False);
            }
            finally { Object.DestroyImmediate(conflict); }
        }

        // Explicitly invoked authoring command; ordinary tests never save protected source assets.
        public static void ApplyApprovedLegacyConnections()
        {
            const string scenePath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Assert.That(definition.CharacterId, Is.EqualTo(Hero));
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(config.EnableProductionHandBp, Is.False);
            Assert.That(config.HandCardRules.Count == 2 || config.HandCardRules.Count == 4, Is.True);
            Assert.That(config.HandCardRules.Take(2).Select(rule => rule.Skill.skillId), Is.EqualTo(new[] { 10001, 10002 }));
            Assert.That(config.HandCardRules.Take(2).Select(rule => rule.Copies), Is.EqualTo(new[] { 2, 3 }));
            var beforeConfig = JObject.Parse(JsonUtility.ToJson(config));
            var bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RuntimeBootstrapper>(true)).Single();
            Assert.That(bootstrap.gameObject.name, Is.EqualTo("Runtime"));
            var bootstrapData = new SerializedObject(bootstrap);
            Assert.That(bootstrapData.FindProperty("characterStartDefinition").objectReferenceValue, Is.Null);
            var entry = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
            string originalConfigPath = AssetDatabase.GetAssetPath(entry.FinalExchangeRuntimeConfig);
            Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
            AssertNoMissingReferences(scene);
            var data = new SerializedObject(config); var rules = data.FindProperty("handCardRules"); rules.arraySize = 4;
            for (int i = 0; i < 2; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i + 2);
                rule.FindPropertyRelative("skill").objectReferenceValue = entry.RegisteredSkillDefinitions[i];
                rule.FindPropertyRelative("kind").enumValueIndex = i;
                rule.FindPropertyRelative("copies").intValue = i == 0 ? 4 : 6;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var afterConfig = JObject.Parse(JsonUtility.ToJson(config));
            beforeConfig.Remove("handCardRules"); afterConfig.Remove("handCardRules");
            Assert.That(JToken.DeepEquals(beforeConfig, afterConfig), Is.True, "Only card rules may change in the Config.");
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            bootstrapData.FindProperty("legacyCharacterDefinition").objectReferenceValue = definition;
            bootstrapData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceUpdate);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            definition = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RuntimeBootstrapper>(true)).Single();
            Assert.That(new SerializedObject(bootstrap).FindProperty("legacyCharacterDefinition").objectReferenceValue, Is.SameAs(definition));
            Assert.That(new SerializedObject(bootstrap).FindProperty("characterStartDefinition").objectReferenceValue, Is.Null);
            entry = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
            Assert.That(AssetDatabase.GetAssetPath(entry.FinalExchangeRuntimeConfig), Is.EqualTo(originalConfigPath));
            Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
            AssertNoMissingReferences(scene);
            Debug.Log("[R4C] Saved and reloaded approved Legacy definition reference and disabled Config Hand rules only.");
        }

        private static void AssertNoMissingReferences(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        Assert.That(component, Is.Not.Null, transform.name);
                        var iterator = new SerializedObject(component).GetIterator();
                        while (iterator.Next(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null)
                                Assert.That(iterator.objectReferenceInstanceIDValue, Is.Zero, transform.name + "." + iterator.propertyPath);
                    }
                }
        }

        [TestCase(2, 5, true)]
        [TestCase(5, 0, true)]
        [TestCase(7, 31, false)]
        [TestCase(5, 12, false)]
        [TestCase(2, 25, false)]
        public void ExistingDefinition_RegistersWithoutPartyGrantOrProgressionLoss(int level, int exp, bool supported)
        {
            Prepare(); var progress = Owner<CharacterProgressionService>("_progress");
            Assert.That(progress.HasRegisteredLegacyDefinition, Is.True);
            Load(Raw(14, "known", level, exp));
            Assert.That(progress.TryGetState(Hero, out int restoredLevel, out int restoredExp), Is.True);
            Assert.That(restoredLevel, Is.EqualTo(level)); Assert.That(restoredExp, Is.EqualTo(exp));
            Assert.That(progress.TryGetDefinition(Hero, out _), Is.EqualTo(supported));
            var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Assert.That(owner.Runtime.GetAcquiredSkills("char.anand_de_morte"), Is.Empty);
            Assert.That(Owner<PartyRuntime>("_party").Members, Is.EqualTo(new[] { Hero }));
            var snapshot = Owner<SaveLoadService>("_save").CaptureGameSaveDataSnapshot();
            Assert.That(snapshot.progression.characters.Single(row => row.characterId == Hero).level, Is.EqualTo(level));
            Assert.That(snapshot.progression.characters.Single(row => row.characterId == Hero).experience, Is.EqualTo(exp));
            var definition = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset");
            Assert.That(progress.TryRegisterLegacyDefinition(definition, out _), Is.EqualTo(supported));
            Assert.That(progress.TryGetState(Hero, out restoredLevel, out restoredExp), Is.True);
            Assert.That(restoredLevel, Is.EqualTo(level)); Assert.That(restoredExp, Is.EqualTo(exp));
        }

        [UnityTest]
        public IEnumerator DirectProductionDungeon_EditorBootstrapAndRealLoadRejectUnsafeCombat()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/Dungeon_1_Production.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunDirectDungeon();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunDirectDungeon()
        {
            yield return null;
            var progress = CharacterProgressionService.Instance;
            var bootstrap = Object.FindFirstObjectByType<RuntimeBootstrapper>();
            Assert.That(new SerializedObject(bootstrap).FindProperty("legacyCharacterDefinition").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset")));
            Assert.That(progress.TryGetDefinition(Hero, out var definition), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(definition), Is.EqualTo("Assets/GAME/Data/Test/CP_TestHero.asset"));
            var save = SaveLoadService.Instance;
            string directory = Path.Combine(Path.GetTempPath(), "GAME002_R4A_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try
            {
                Call(save, "SetStoragePathForTests", Path.Combine(directory, "save.json"));
                foreach (var variant in new[] { "missing", "empty", "unknown", "mixed", "known" })
                {
                    string raw = Raw(variant == "missing" ? 8 : 14, variant); File.WriteAllText(save.PrimarySavePath, raw);
                    Assert.That(save.TryLoad(out string message), Is.True, message);
                    Assert.That(File.ReadAllText(save.PrimarySavePath), Is.EqualTo(raw));
                    Assert.That(PartyRuntime.Instance.Members, Is.EqualTo(new[] { Hero }));
                    Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills("char.anand_de_morte"), Is.Empty);
                    var player = Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None).Single(value => value.CompareTag("Player")).gameObject;
                    var group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).First();
                    var request = new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null, CombatFlowMode.StandoffClashChain);
                    request.AllyFieldObjects.Add(player); request.EnemyFieldObjects.Add(group.GetActiveEnemies().First());
                    Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, player, PartyRuntime.Instance, progress, out message), Is.True, message);
                    var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
                    if (variant == "known")
                    {
                        var originalConfig = entry.FinalExchangeRuntimeConfig;
                        var originalRegistry = entry.RegisteredSkillDefinitions.ToArray();
                        Assert.That(originalRegistry.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12, 10001, 10002, 10003 }));
                        Assert.That(originalConfig.EnableProductionHandBp, Is.True);
                        try
                        {
                            Assert.That(entry.StartCombat(request), Is.True);
                            ProductionInitializationCompatibilityTests.AssertLegacySession(entry.ActiveSession);
                            entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Call(entry, "Update");
                            Assert.That(entry.ActiveSession, Is.Null);
                            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
                            var rewardPanel = Object.FindFirstObjectByType<RewardUIPanel>();
                            Assert.That(rewardPanel.IsOpen, Is.True);
                            var close = (Button)new SerializedObject(rewardPanel).FindProperty("closeButton").objectReferenceValue;
                            Assert.That(close, Is.Not.Null); close.onClick.Invoke();
                            Assert.That(rewardPanel.IsOpen, Is.False);
                            Assert.That(File.ReadAllText(save.PrimarySavePath), Is.EqualTo(raw));
                        }
                        finally
                        {
                            Set(entry, "finalExchangeRuntimeConfig", originalConfig);
                            Set(entry, "skillDefinitions", originalRegistry); Call(entry, "Awake");
                        }
                    }
                    else
                    {
                        LogAssert.Expect(LogType.Warning, new Regex(@"Combat start rejected\..*" + (variant == "unknown" || variant == "mixed" ? "unresolved or ambiguous" : "automatic fallback is disabled")));
                        Assert.That(entry.StartCombat(request), Is.False); Assert.That(entry.ActiveSession, Is.Null);
                    }
                    Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
                    Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.False);
                }
            }
            finally { DeleteTestDirectory(directory); }
        }

        public static void WriteColdEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = "C2ACompatibilityRestart";
            foreach (string variant in new[] { "missing", "empty", "unproven", "unknown", "mixed", "known" })
                File.WriteAllText(ColdPrefix + variant + ".json", Raw(variant == "missing" ? 8 : 14, variant));
            File.WriteAllText(ColdPrefix + "WriterPid.txt", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
        }

        public static void ReadColdEvidence()
        {
            Assert.That(File.ReadAllText(ColdPrefix + "WriterPid.txt"), Is.Not.EqualTo(System.Diagnostics.Process.GetCurrentProcess().Id.ToString()));
            foreach (string variant in new[] { "missing", "empty", "unproven", "unknown", "mixed", "known" })
            {
                var fixture = new LegacySaveBoundaryTests();
                try
                {
                    fixture.Prepare(); fixture.Load(File.ReadAllText(ColdPrefix + variant + ".json"));
                    fixture.AssertSavedState(variant == "missing" ? 8 : 14, variant);
                    var owner = fixture.Owner<CharacterSkillSaveParticipant>("_skills");
                    string expected = variant == "missing" ? "MissingInLegacySource" : variant == "empty" ? "ExplicitEmpty" : variant == "unproven" ? "Unknown" : "ExplicitSkills";
                    Assert.That(owner.GetEquipmentReadOrigin(Hero).ToString(), Is.EqualTo(expected));
                    var request = (CombatStartRequest)Call(fixture._fixture, "Request", Hero, new[] { 1, 2 }, 11);
                    var registry = fixture.Owner<Game.Combat.Data.SkillDefinitionSO[]>("_registry");
                    Assert.That(PersistentSkillCombatLoadoutBridge.TryApplyForProduction(request, registry, null, out _), Is.EqualTo(variant == "known"));
                    if (variant == "known")
                    {
                        Call(fixture._fixture, "CreateEntry", true);
                        Assert.That(fixture.Owner<CombatEntryPoint>("_entry").StartCombat(request), Is.True);
                        ProductionInitializationCompatibilityTests.AssertLegacySession(fixture.Owner<CombatEntryPoint>("_entry").ActiveSession);
                        fixture.AssertSavedState(14, variant);
                    }
                }
                finally { fixture.Cleanup(); }
            }
            File.WriteAllText(ColdPrefix + "ReadEvidence.txt", "PASS: distinct Unity processes; raw legacy omission/explicit empty/current omission distinguished; six saves preserve hero.test state; unknown/mixed rejected; known Snapshot starts Legacy deck4/6 with visible Hand2/3; no automatic fallback.");
        }

        private void Prepare()
        {
            _fixture = new ProductionInitializationCompatibilityTests(); Call(_fixture, "Prepare", false, true);
            Call(Owner<RuntimeBootstrapper>("_bootstrap"), "BootstrapCoreServices", false, false, false);
            Assert.That(Owner<CharacterProgressionService>("_progress").HasRegisteredLegacyDefinition, Is.True);
        }
        private void Load(string raw)
        {
            var save = Owner<SaveLoadService>("_save"); File.WriteAllText(save.PrimarySavePath, raw);
            int notifications = 0; var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Action observe = () => notifications++; owner.Runtime.Changed += observe;
            try { Assert.That(save.TryLoad(out string message), Is.True, message); }
            finally { owner.Runtime.Changed -= observe; }
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(File.ReadAllText(save.PrimarySavePath), Is.EqualTo(raw));
        }
        private void AssertSavedState(int version, string variant)
        {
            var save = Owner<SaveLoadService>("_save"); var owner = Owner<CharacterSkillSaveParticipant>("_skills");
            Assert.That(Owner<PartyRuntime>("_party").Members, Is.EqualTo(new[] { Hero }));
            Assert.That(Owner<CharacterProgressionService>("_progress").TryGetState(Hero, out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(2)); Assert.That(exp, Is.EqualTo(5));
            Assert.That(owner.Runtime.GetAcquiredSkills(Hero), Is.EquivalentTo(Keys(variant)));
            Assert.That(owner.Runtime.GetEquippedSkills(Hero), Is.EqualTo(Keys(variant)));
            Assert.That(owner.Runtime.GetAcquiredSkills("char.anand_de_morte"), Is.Empty);
            Assert.That(SaveSerializer.FromGameSaveJson(File.ReadAllText(save.PrimarySavePath)).header.schemaVersion, Is.EqualTo(version));
        }
        private T Owner<T>(string field) => (T)_fixture.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_fixture);
        private static string[] Keys(string variant) => variant == "known" ? new[] { "skill.production.0001", "skill.production.0002" } : variant == "unknown" ? new[] { "legacy.skill" } : variant == "mixed" ? new[] { "skill.production.0001", "legacy.skill" } : Array.Empty<string>();
        private static string Raw(int version, string variant, int level = 2, int exp = 5)
        {
            var data = new GameSaveData(); data.header.schemaVersion = version; data.header.activeSceneId = SceneManager.GetActiveScene().name;
            data.party.memberIds.Add(Hero); data.party.leaderCharacterId = Hero; data.party.selectedCombatMemberIds.Add(Hero);
            data.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = Hero, level = level, experience = exp });
            data.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData { characterId = Hero, acquiredSkillKeys = Keys(variant).ToList(), equippedSkillKeys = Keys(variant).ToList() });
            var root = JObject.Parse(SaveSerializer.ToJson(data)); var row = (JObject)root["characterSkills"]["characters"][0];
            if (variant == "missing" || variant == "unproven") row.Remove("equippedSkillKeys");
            if (variant == "null") row["equippedSkillKeys"] = JValue.CreateNull();
            return root.ToString();
        }
        [TearDown]
        public void Cleanup() { _fixture?.Cleanup(); _fixture = null; }
        [UnityTearDown]
        public IEnumerator ExitIfNeeded() { if (Application.isPlaying) yield return new ExitPlayMode(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        private static void DeleteTestDirectory(string directory)
        {
            string absolute = Path.GetFullPath(directory);
            Assert.That(absolute.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_R4A_"), StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.Delete(absolute, true);
        }
    }
}
