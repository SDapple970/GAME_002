using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Debugging;
using Game.Combat.Effects;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.Enemies;
using Game.Input;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    // Source scenes and their real owners are used unchanged during ordinary tests.
    public sealed class ProductionInitializationConnectionTests
    {
        internal const string StartPath = "Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset";
        internal const string TitlePath = "Assets/GAME/Scenes/TitleScene.unity";
        internal const string DungeonPath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string TemplatePath = "Assets/GAME/Scenes/Dungeon_Template.unity";
        private const string PrefabPath = "Assets/GAME/Prefabs/CombatRuntime.prefab";
        private const string PrefabGuid = "1bfc52cd235e809449d17d48571f5a38";
        private const long SourceBpFileId = 3049129679861795314;
        private const string Anand = "char.anand_de_morte";
        private const string Basic = "skill.playtest.anand.basic";
        private const string Zorn = "skill.playtest.anand.zornhau";
        private const string ColdPrefix = "Logs/C2AFinal-Cold-";
        private static readonly int[] RegistryIds = { 1, 2, 11, 12, 10001, 10002, 10003 };
        private string _directory;
        private Vector3 _safePosition;

        // Explicit authoring entry point. Never called by a test or by runtime bootstrap.
        public static void ApplyApprovedConnections()
        {
            string prefabHash = Hash(PrefabPath);
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
            Assert.That(start.TryValidate(out string error), Is.True, error);
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(DebugFreeProductionTestSupport.ConfigPath);
            Assert.That(config.HandCardRules.Select(rule => rule.Copies), Is.EqualTo(new[] { 2, 3, 4, 6 }));
            var profile = new SerializedObject(config);
            profile.FindProperty("enableProductionHandBp").boolValue = true;
            profile.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(config);
            foreach (string path in new[] { TitlePath, DungeonPath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
                config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(DebugFreeProductionTestSupport.ConfigPath);
                var bootstrap = InScene<RuntimeBootstrapper>(scene).Single();
                var data = new SerializedObject(bootstrap);
                data.FindProperty("characterStartDefinition").objectReferenceValue = start;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (path == DungeonPath)
                {
                    var entry = InScene<CombatEntryPoint>(scene).Single();
                    Assert.That(entry.RegisteredSkillDefinitions.Take(4).Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
                    Assert.That(entry.RegisteredSkillDefinitions.Count == 4 || entry.RegisteredSkillDefinitions.Count == 7, Is.True);
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
                    var entryData = new SerializedObject(entry);
                    var registry = entryData.FindProperty("skillDefinitions");
                    registry.arraySize = 7;
                    string[] additions = { "Skill_AnandBasic_PlaytestV1", "Skill_Zornhau_PlaytestV1", "Skill_AngelTheProphecy_PlaytestV1" };
                    for (int i = 0; i < additions.Length; i++)
                        registry.GetArrayElementAtIndex(i + 4).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/" + additions[i] + ".asset");
                    entryData.FindProperty("finalExchangeRuntimeConfig").objectReferenceValue = config;
                    entryData.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(entry);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True, path);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(Hash(PrefabPath), Is.EqualTo(prefabHash));
            AssertSavedConnections();
            Debug.Log("[C2A Final] Approved Start, Registry and Config saved/reloaded; original CombatRuntime prefab preserved.");
        }

        [Test]
        public void SavedConnections_KeepCanonicalOwnersLegacyLookupEnemiesAndPrefabInheritance() => AssertSavedConnections();

        // Explicit C2B authoring command. Ordinary tests never save protected source scenes.
        public static void RemoveApprovedProductionDebug()
        {
            string prefabHash = Hash(PrefabPath);
            string[] paths = { DungeonPath, TemplatePath };
            // Validate both targets before either scene is saved.
            foreach (string path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var entry = InScene<CombatEntryPoint>(scene).Single();
                Assert.That(GameObject.Find("Runtime/Combat/CombatRuntime"), Is.SameAs(entry.gameObject));
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
                Assert.That(PrefabUtility.GetRemovedComponents(entry.gameObject), Is.Empty);
                var bp = entry.GetComponents<CombatBpValidationOptIn>().Single();
                Assert.That(PrefabUtility.IsAddedComponentOverride(bp), Is.False);
                AssertSourceBp(PrefabUtility.GetCorrespondingObjectFromSource(bp));
                Assert.That(bp.enabled, Is.True);
                bool production = path == DungeonPath;
                Assert.That(entry.FinalExchangeRuntimeConfig.EnableProductionHandBp, Is.EqualTo(production));
                Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.skillId),
                    Is.EqualTo(production ? RegistryIds : new[] { 1, 2, 11, 12 }));
                Assert.That(InScene<CombatBpValidationOptIn>(scene), Has.Length.EqualTo(1));
                Assert.That(InScene<CombatHandValidationOptIn>(scene), Has.Length.EqualTo(production ? 1 : 0));
                Assert.That(InScene<CombatClashSeriesValidationOptIn>(scene), Has.Length.EqualTo(production ? 1 : 0));
                if (production)
                {
                    AssertAddedSceneComponent(entry.GetComponent<CombatHandValidationOptIn>(), path, 1665839071176935759);
                    AssertAddedSceneComponent(entry.GetComponent<CombatClashSeriesValidationOptIn>(), path, 1665839071176935761);
                }
                AssertNoMissingReferences(scene);
            }
            foreach (string path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var entry = InScene<CombatEntryPoint>(scene).Single();
                Component[] retained = entry.GetComponents<Component>().Where(component => !IsValidationDebug(component)).ToArray();
                string entryBefore = JsonUtility.ToJson(entry);
                var removed = entry.GetComponents<MonoBehaviour>().Where(IsValidationDebug).ToArray();
                foreach (var component in removed)
                {
                    Debug.Log($"[C2B-1] Removing {path}: {component.GetType().FullName}; {GlobalObjectId.GetGlobalObjectIdSlow(component)}");
                    Object.DestroyImmediate(component);
                }
                Assert.That(removed, Has.Length.EqualTo(path == DungeonPath ? 3 : 1));
                Assert.That(entry.GetComponents<Component>(), Is.EqualTo(retained));
                Assert.That(JsonUtility.ToJson(entry), Is.EqualTo(entryBefore), "Removing Debug must not reconfigure CombatEntryPoint.");
                AssertDebugRemovedRuntime(entry);
                AssertNoMissingReferences(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True, path);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(Hash(PrefabPath), Is.EqualTo(prefabHash), "Do not apply scene removals to the source prefab.");
            AssertSavedConnections();
            Debug.Log("[C2B-1] Four approved Debug components removed; both scenes saved/reloaded; source prefab preserved.");
        }

        internal static void AssertDebugRemovedRuntime(CombatEntryPoint entry)
        {
            Assert.That(entry.GetComponents<MonoBehaviour>().Any(IsValidationDebug), Is.False);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(entry);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(PrefabPath));
            var sourceBp = source.GetComponents<CombatBpValidationOptIn>().Single();
            AssertSourceBp(sourceBp);
            var removed = PrefabUtility.GetRemovedComponents(entry.gameObject);
            Assert.That(removed, Has.Count.EqualTo(1));
            Assert.That(removed.Single().assetComponent, Is.SameAs(sourceBp));
            Assert.That(entry.GetComponents<CombatEntryPoint>(), Has.Length.EqualTo(1));
            Assert.That(entry.GetComponents<CombatFlowOrchestrator>(), Has.Length.EqualTo(1));
            Assert.That(entry.GetComponents<CombatDirector>(), Has.Length.EqualTo(1));
            Assert.That(entry.GetComponents<CombatFieldLock>(), Has.Length.EqualTo(1));
            Assert.That(entry.GetComponents<CombatCameraController>(), Has.Length.EqualTo(1));
        }

        private static void AssertSourceBp(CombatBpValidationOptIn bp)
        {
            Assert.That(bp, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bp, out string guid, out long id), Is.True);
            Assert.That(guid, Is.EqualTo(PrefabGuid));
            Assert.That(id, Is.EqualTo(SourceBpFileId));
        }

        private static void AssertAddedSceneComponent(Component component, string path, ulong fileId)
        {
            Assert.That(component, Is.Not.Null);
            Assert.That(PrefabUtility.IsAddedComponentOverride(component), Is.True);
            var id = GlobalObjectId.GetGlobalObjectIdSlow(component);
            Assert.That(id.assetGUID.ToString(), Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(id.targetObjectId, Is.EqualTo(fileId));
        }

        private static bool IsValidationDebug(Component component) => component is CombatBpValidationOptIn ||
            component is CombatHandValidationOptIn || component is CombatClashSeriesValidationOptIn;

        internal static void AssertSavedConnections()
        {
            try
            {
                var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
                Assert.That(start.Definitions.Select(definition => definition.CharacterId), Is.EqualTo(new[] { Anand }));
                Assert.That(start.InitialMemberIds, Is.EqualTo(new[] { Anand }));
                Assert.That(start.TryValidate(out string error), Is.True, error);
                foreach (string path in new[] { TitlePath, DungeonPath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
                    var bootstrap = InScene<RuntimeBootstrapper>(scene).Single();
                    Assert.That(new SerializedObject(bootstrap).FindProperty("characterStartDefinition").objectReferenceValue, Is.SameAs(start));
                    AssertNoMissingReferences(scene);
                    if (path == TitlePath)
                    {
                        var progression = InScene<CharacterProgressionService>(scene).Single();
                        var definitions = new SerializedObject(progression).FindProperty("definitions");
                        Assert.That(Enumerable.Range(0, definitions.arraySize).Select(i => definitions.GetArrayElementAtIndex(i).objectReferenceValue),
                            Does.Contain(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset")));
                        continue;
                    }
                    Assert.That(new SerializedObject(bootstrap).FindProperty("legacyCharacterDefinition").objectReferenceValue,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset")));
                    var entry = InScene<CombatEntryPoint>(scene).Single();
                    Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(RegistryIds));
                    Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.PersistentKey).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(7));
                    Assert.That(entry.FinalExchangeRuntimeConfig, Is.SameAs(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(DebugFreeProductionTestSupport.ConfigPath)));
                    Assert.That(entry.FinalExchangeRuntimeConfig.EnableProductionHandBp, Is.True);
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(entry)), Is.EqualTo(PrefabPath));
                    AssertDebugRemovedRuntime(entry);
                    DebugFreeProductionTestSupport.AssertClean(scene);
                    foreach (var loadout in InScene<CombatSkillLoadoutComponent>(scene))
                        Assert.That(loadout.SkillIds.All(id => id == 11 || id == 12), Is.True, loadout.name);
                    // Existing Production enemies use the legacy source path; do not attach
                    // the isolated Angel's authored EnemySourceComponent during this connection.
                    Assert.That(InScene<EnemySourceComponent>(scene), Is.Empty);
                    foreach (var trigger in InScene<CombatEncounterTrigger2D>(scene))
                    {
                        var data = new SerializedObject(trigger);
                        Assert.That(data.FindProperty("startReason").intValue, Is.EqualTo((int)StartReason.PlayerGotHit));
                        Assert.That(data.FindProperty("initiativeSide").intValue, Is.EqualTo((int)Side.Enemies));
                    }
                }
                var template = EditorSceneManager.OpenScene(TemplatePath, OpenSceneMode.Single);
                var templateEntry = InScene<CombatEntryPoint>(template).Single();
                AssertDebugRemovedRuntime(templateEntry);
                DebugFreeProductionTestSupport.AssertClean(template);
                AssertNoMissingReferences(template);
                Assert.That(templateEntry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
                Assert.That(templateEntry.FinalExchangeRuntimeConfig,
                    Is.SameAs(PrefabUtility.GetCorrespondingObjectFromSource(templateEntry).FinalExchangeRuntimeConfig));
                Assert.That(templateEntry.FinalExchangeRuntimeConfig.EnableProductionHandBp, Is.False);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CombatEntryPoint>();
                Assert.That(prefab.RegisteredSkillDefinitions, Is.Empty);
                Assert.That(prefab.FinalExchangeRuntimeConfig.EnableProductionHandBp, Is.False);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [UnityTest]
        public IEnumerator TitleNewGame_OfficialGrants_ContactCombat_SaveAndContinue()
        {
            EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunTitle();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunTitle()
        {
            yield return null;
            PrepareStorage();
            Assert.That(PartyRuntime.Instance.StartDefinition, Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath)));
            Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills(Anand), Is.Empty, "Bootstrap configuration must not grant skills.");
            var title = Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            ButtonOf(title, "startButton").onClick.Invoke();
            yield return Wait(() => ButtonOf(title, "paperClickButton").interactable, "NPC paper");
            ButtonOf(title, "paperClickButton").onClick.Invoke();
            yield return Wait(() => ButtonOf(title, "paperClickButton").interactable, "monster paper");
            ButtonOf(title, "paperClickButton").onClick.Invoke();
            yield return Wait(() => SceneManager.GetActiveScene().name == "Dungeon_1_Production" && GameStateMachine.Instance.Current == GameState.Exploration, "official New Game");
            yield return null;
            AssertCharacter(Anand, new[] { Basic, Zorn }, 1, 0);
            _safePosition = Player().transform.position + new Vector3(30, 30, 0);
            FreezePlayer(); Teleport(_safePosition);
            Assert.That(SaveLoadService.Instance.TrySave(out string error), Is.True, error);
            string initial = File.ReadAllText(SaveLoadService.Instance.PrimarySavePath);
            yield return ContactAndFinish(Anand, 2, 3);
            CharacterSkillSaveParticipant.Instance.Runtime.TryUnequip(Anand, Zorn);
            CharacterProgressionService.Instance.ApplyExperience(Anand, 350);
            Assert.That(SaveLoadService.Instance.TrySave(out error), Is.True, error);
            string changed = File.ReadAllText(SaveLoadService.Instance.PrimarySavePath);
            if (ColdRole() == "write")
            {
                File.WriteAllText(ColdPrefix + "AnandInitial.json", initial);
                File.WriteAllText(ColdPrefix + "AnandChanged.json", changed);
                foreach (string variant in new[] { "known", "missing", "empty", "unknown", "mixed" })
                    File.WriteAllText(ColdPrefix + variant + ".json", RawSave(variant, _safePosition));
                File.WriteAllText(ColdPrefix + "WriterPid.txt", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            }
            SceneFlowController.Instance.LoadScene("TitleScene");
            yield return Wait(() => SceneManager.GetActiveScene().name == "TitleScene" && GameStateMachine.Instance.Current == GameState.Title, "return Title");
            Assert.That(SaveLoadService.Instance.TryResetForNewGame(out error), Is.True, error);
            AssertCharacter(Anand, new[] { Basic, Zorn }, 1, 0);
            title = Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Assert.That(ButtonOf(title, "continueButton").interactable, Is.True);
            ButtonOf(title, "continueButton").onClick.Invoke();
            yield return Wait(() => SceneManager.GetActiveScene().name == "Dungeon_1_Production" && GameStateMachine.Instance.Current == GameState.Exploration, "Continue");
            AssertCharacter(Anand, new[] { Basic }, 3, 50);
            Assert.That(File.ReadAllText(SaveLoadService.Instance.PrimarySavePath), Is.EqualTo(changed));
            FreezePlayer();
            yield return ContactAndFinish(Anand, 2, 0);
        }

        [UnityTest]
        public IEnumerator DirectDungeon_ActualConfig_LoadsSupportedSavesAndRejectsUnsafeEquipment()
        {
            EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunDirectDungeon();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunDirectDungeon()
        {
            yield return null;
            PrepareStorage(); FreezePlayer();
            _safePosition = Player().transform.position + new Vector3(30, 30, 0);
            Assert.That(CharacterProgressionService.Instance.TryGetDefinition("hero.test", out var legacy), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(legacy), Is.EqualTo("Assets/GAME/Data/Test/CP_TestHero.asset"));
            Assert.That(CharacterProgressionService.Instance.TryGetState("hero.test", out _, out _), Is.False);
            Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills(Anand), Is.Empty);
            bool cold = ColdRole() == "read";
            if (cold) Assert.That(File.ReadAllText(ColdPrefix + "WriterPid.txt"), Is.Not.EqualTo(System.Diagnostics.Process.GetCurrentProcess().Id.ToString()));
            foreach (string variant in new[] { "known", "missing", "empty", "unknown", "mixed", "AnandInitial", "AnandChanged" })
            {
                string raw = cold ? File.ReadAllText(ColdPrefix + variant + ".json") : RawSave(variant, _safePosition);
                var save = SaveLoadService.Instance;
                File.WriteAllText(save.PrimarySavePath, raw);
                int restored = 0; Action observe = () => restored++;
                CharacterSkillSaveParticipant.Instance.Runtime.Changed += observe;
                try { Assert.That(save.TryLoad(out string error), Is.True, error); }
                finally { CharacterSkillSaveParticipant.Instance.Runtime.Changed -= observe; }
                yield return Wait(() => GameStateMachine.Instance.Current == GameState.Exploration, "direct Dungeon restore");
                Assert.That(restored, Is.EqualTo(1));
                Assert.That(File.ReadAllText(save.PrimarySavePath), Is.EqualTo(raw));
                bool anand = variant.StartsWith("Anand", StringComparison.Ordinal);
                if (anand)
                {
                    bool initial = variant == "AnandInitial";
                    AssertCharacter(Anand, initial ? new[] { Basic, Zorn } : new[] { Basic }, initial ? 1 : 3, initial ? 0 : 50);
                    yield return ContactAndFinish(Anand, 2, initial ? 3 : 0);
                }
                else
                {
                    AssertCharacter("hero.test", LegacyKeys(variant), 2, 5);
                    Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetAcquiredSkills(Anand), Is.Empty);
                    if (variant == "known") yield return ContactAndFinish("hero.test", 4, 6);
                    else
                    {
                        var player = Player();
                        var request = new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null, CombatFlowMode.StandoffClashChain);
                        request.AllyFieldObjects.Add(player); request.EnemyFieldObjects.Add(Group().GetActiveEnemies().First());
                        Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, player, PartyRuntime.Instance, CharacterProgressionService.Instance, out string error), Is.True, error);
                        LogAssert.Expect(LogType.Warning, new Regex("Combat start rejected.*" + (variant == "unknown" || variant == "mixed" ? "unresolved or ambiguous" : "automatic fallback is disabled")));
                        var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
                        Assert.That(entry.StartCombat(request), Is.False); Assert.That(entry.ActiveSession, Is.Null);
                        Assert.That(new InputRouter().AllowsExplorationInput(), Is.True);
                        Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.False);
                    }
                }
                Assert.That(File.ReadAllText(save.PrimarySavePath), Is.EqualTo(raw));
            }
            if (cold) File.WriteAllText(ColdPrefix + "ReadEvidence.txt", "PASS: separate writer/reader Unity processes; actual Production Dungeon bootstrap/config/registry; Anand initial 2/3, changed 2/0, Legacy known 4/6 combat; missing/empty/unknown/mixed preserved and rejected; no initial grant replay.");
        }

        [UnityTest]
        public IEnumerator LegacyKnownSave_ProductionDirector_CompletesVictory()
        {
            EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunTerminal("known", CombatEndReason.Victory);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AnandSave_ProductionDirector_CompletesDefeat()
        {
            EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunTerminal("AnandInitial", CombatEndReason.Defeat);
            yield return new ExitPlayMode();
        }

        private IEnumerator RunTerminal(string variant, CombatEndReason reason)
        {
            yield return null;
            PrepareStorage(); FreezePlayer();
            _safePosition = Player().transform.position + new Vector3(30, 30, 0);
            File.WriteAllText(SaveLoadService.Instance.PrimarySavePath, RawSave(variant, _safePosition));
            Assert.That(SaveLoadService.Instance.TryLoad(out string error), Is.True, error);
            yield return ContactAndFinish(variant == "known" ? "hero.test" : Anand,
                variant == "known" ? 4 : 2, variant == "known" ? 6 : 3, reason);
        }

        private IEnumerator ContactAndFinish(string id, int normal, int clash, CombatEndReason endReason = CombatEndReason.Abort)
        {
            var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
            int completed = 0; CombatResult result = null;
            Action<CombatResult> observe = value => { completed++; result = value; };
            entry.OnCombatEnded += observe;
            try
            {
                var group = Group();
                foreach (var patrol in group.GetComponentsInChildren<Game.Enemies.FieldEnemyPatrolAI2D>()) patrol.enabled = false;
                Teleport(group.GetComponentInChildren<CombatEncounterTrigger2D>().GetComponent<Collider2D>().bounds.center);
                yield return Wait(() => entry.ActiveSession != null, "actual Contact entry");
                var session = entry.ActiveSession; var ally = session.Allies.Single();
                Assert.That(session.HasProductionHandBpInitialization, Is.True);
                Assert.That(session.StartReason, Is.EqualTo(StartReason.PlayerGotHit)); Assert.That(session.InitiativeSide, Is.EqualTo(Side.Enemies));
                Assert.That(session.TryGetAllyCharacterId(ally, out string characterId), Is.True); Assert.That(characterId, Is.EqualTo(id));
                Assert.That(session.TryGetHand(ally, out var hand), Is.True);
                Assert.That(hand.DeckSnapshot.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(normal));
                Assert.That(hand.DeckSnapshot.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(clash));
                Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(Math.Min(2, normal)));
                Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(Math.Min(3, clash)));
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
                Assert.That(session.BpRuntime.TryGetBalance(ally, out int bp), Is.True); Assert.That(bp, Is.Zero);
                Assert.That(session.Enemies.SelectMany(enemy => enemy.Skills).All(skill => skill.Id.Value == 11 || skill.Id.Value == 12), Is.True);
                Assert.That(new InputRouter().AllowsExplorationInput(), Is.False);
                Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.True);
                if (endReason == CombatEndReason.Abort)
                    Assert.That(entry.FlowOrchestrator.TerminateExplicit(endReason, session.ExchangeState.Version), Is.True);
                else
                {
                    // Arrange only the terminal HP boundary. Damage, result and presentation
                    // still complete through the existing resolver/orchestrator/Director.
                    if (endReason == CombatEndReason.Defeat) ally.ApplyDamage(ally.HP - 1);
                    else foreach (var enemy in session.Enemies) enemy.ApplyDamage(enemy.HP - 1);
                    float deadline = Time.realtimeSinceStartup + 30;
                    var flow = entry.FlowOrchestrator;
                    while (entry.ActiveSession != null && Time.realtimeSinceStartup < deadline)
                    {
                        var token = flow.PendingDecisionRequest;
                        if (token?.Kind == CombatExchangeDecisionKind.Response) flow.SubmitNoResponse(token);
                        else if (token?.Kind == CombatExchangeDecisionKind.ClashResponse) flow.SubmitClashSeriesNoResponse(token);
                        else if (entry.ActiveStateMachine.Phase == Phase.ChainDecision) flow.EndChain(session.ExchangeState.Version);
                        else if (entry.ActiveStateMachine.Phase == Phase.Standoff && session.ExchangeState.CurrentAttackSide == Side.Allies && session.TryGetHand(ally, out var attackHand))
                        {
                            var card = attackHand.Hand.FirstOrDefault(value => value.Kind == CombatCardKind.Normal);
                            if (card != null) flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, session.Enemies.First(enemy => enemy.HP > 0), card.Skill, card.RuntimeId), session.ExchangeState.Version);
                        }
                        yield return null;
                    }
                }
                yield return Wait(() => entry.ActiveSession == null && GameStateMachine.Instance.Current == GameState.Reward, "Director completion and Reward");
                Assert.That(completed, Is.EqualTo(1)); Assert.That(result.EndReason, Is.EqualTo(endReason));
                var panel = Object.FindFirstObjectByType<RewardUIPanel>(); Assert.That(panel.IsOpen, Is.True);
                ButtonOf(panel, "closeButton").onClick.Invoke(); Teleport(_safePosition);
                yield return Wait(() => GameStateMachine.Instance.Current == GameState.Exploration, "Reward close");
                yield return new WaitForFixedUpdate();
                Assert.That(entry.ActiveSession, Is.Null); Assert.That(completed, Is.EqualTo(1));
                Assert.That(new InputRouter().AllowsExplorationInput(), Is.True);
                Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.False);
                Assert.That(Object.FindFirstObjectByType<CombatCameraController>().IsInCombatMode, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            finally { entry.OnCombatEnded -= observe; }
        }

        private void PrepareStorage()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GAME002_C2AFinal_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            SaveLoadService.Instance.SetStoragePathForTests(Path.Combine(_directory, "save.json"));
        }

        private static string RawSave(string variant, Vector3 position)
        {
            bool anand = variant.StartsWith("Anand", StringComparison.Ordinal);
            string id = anand ? Anand : "hero.test";
            var data = new GameSaveData(); data.header.activeSceneId = "Dungeon_1_Production";
            data.location.positionX = position.x; data.location.positionY = position.y; data.location.positionZ = position.z;
            data.party.memberIds.Add(id); data.party.leaderCharacterId = id; data.party.selectedCombatMemberIds.Add(id);
            data.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = id, level = anand ? (variant == "AnandInitial" ? 1 : 3) : 2, experience = anand ? (variant == "AnandInitial" ? 0 : 50) : 5 });
            string[] keys = anand ? new[] { Basic, Zorn } : LegacyKeys(variant);
            data.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData { characterId = id, acquiredSkillKeys = keys.ToList(), equippedSkillKeys = (variant == "AnandChanged" ? new[] { Basic } : keys).ToList() });
            if (variant == "missing")
            {
                data.header.schemaVersion = 8;
                var root = Newtonsoft.Json.Linq.JObject.Parse(SaveSerializer.ToJson(data));
                ((Newtonsoft.Json.Linq.JObject)root["characterSkills"]["characters"][0]).Remove("equippedSkillKeys");
                return root.ToString();
            }
            return SaveSerializer.ToJson(data);
        }

        private static string[] LegacyKeys(string variant) => variant == "known" ? new[] { "skill.production.0001", "skill.production.0002" } : variant == "unknown" ? new[] { "legacy.skill" } : variant == "mixed" ? new[] { "skill.production.0001", "legacy.skill" } : Array.Empty<string>();
        private static string ColdRole() { var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-c2aColdRole"); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        private static void AssertCharacter(string id, string[] equipped, int level, int exp)
        {
            Assert.That(PartyRuntime.Instance.Members, Is.EqualTo(new[] { id })); Assert.That(PartyRuntime.Instance.CombatLineup, Is.EqualTo(new[] { id }));
            Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetEquippedSkills(id), Is.EqualTo(equipped));
            Assert.That(CharacterProgressionService.Instance.TryGetState(id, out int actualLevel, out int actualExp), Is.True);
            Assert.That(actualLevel, Is.EqualTo(level)); Assert.That(actualExp, Is.EqualTo(exp));
        }
        private static CombatEncounterGroup Group() => Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).First(group => group.EncounterId == "dungeon1.encounter.01");
        private static GameObject Player() => GameObject.FindGameObjectWithTag("Player");
        private static void FreezePlayer() { var body = Player().GetComponent<Rigidbody2D>(); body.gravityScale = 0; body.linearVelocity = Vector2.zero; }
        private static void Teleport(Vector3 position) { Player().transform.position = position; var body = Player().GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
        private static Button ButtonOf(Object owner, string field) => (Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static string Hash(string path) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static IEnumerator Wait(Func<bool> ready, string step) { float deadline = Time.realtimeSinceStartup + 30; while (!ready() && Time.realtimeSinceStartup < deadline) yield return null; Assert.That(ready(), Is.True, "Timed out: " + step); }
        private static void AssertNoMissingReferences(Scene scene)
        {
            foreach (var transform in InScene<Transform>(scene))
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (_directory != null && Directory.Exists(_directory))
            {
                string path = Path.GetFullPath(_directory);
                Assert.That(path.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_C2AFinal_"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(path, true);
            }
        }
    }
}
