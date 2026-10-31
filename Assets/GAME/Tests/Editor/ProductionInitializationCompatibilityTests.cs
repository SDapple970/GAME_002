using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Debugging;
using Game.Combat.Effects;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Game.Tests.Integration.DebugFreeProductionTestSupport;

namespace Game.Tests.Integration
{
    // Conditional C2A gate: disk Load compatibility and candidate Combat compatibility are distinct.
    // Negative tests intentionally prove why the authored Scene/config activation must remain pending.
    public sealed class ProductionInitializationCompatibilityTests
    {
        private const string StartPath = "Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset";
        private const string DungeonPath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";
        private const string TitlePath = "Assets/GAME/Scenes/TitleScene.unity";
        private const string Anand = "char.anand_de_morte";
        private const string Hero = "hero.test";
        private const string Basic = "skill.playtest.anand.basic";
        private const string Zorn = "skill.playtest.anand.zornhau";
        private const string ColdPrefix = "Logs/CBT-D4E1DC2A-Cold-";
        private readonly List<Object> _created = new();
        private SaveLoadService _save;
        private PartyRuntime _party;
        private CharacterProgressionService _progress;
        private CharacterSkillSaveParticipant _skills;
        private GameFlowController _flow;
        private CombatEntryPoint _entry;
        private RuntimeBootstrapper _bootstrap;
        private FinalCombatRuntimeConfigSO _currentConfig;
        private SkillDefinitionSO[] _registry;
        private string _directory;

        // Explicit authoring command. Only the approved, still-unconnected Start asset is saved.
        public static void AuthorInitialGrantsOnly()
        {
            AssertProductionConnectionsRemainPending();
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
            var definitions = start.Definitions.ToArray();
            var members = start.InitialMemberIds.ToArray();
            string leader = start.InitialLeaderId, reward = start.DefaultRewardTargetId;
            WriteGrants(start);
            Assert.That(start.TryValidate(out string message), Is.True, message);
            Assert.That(start.Definitions, Is.EqualTo(definitions));
            Assert.That(start.InitialMemberIds, Is.EqualTo(members));
            Assert.That(start.InitialLeaderId, Is.EqualTo(leader));
            Assert.That(start.DefaultRewardTargetId, Is.EqualTo(reward));
            EditorUtility.SetDirty(start);
            AssetDatabase.SaveAssetIfDirty(start);
            Debug.Log("[C2A] Authored two initial grants only. Scenes/config activation remain pending.");
        }

        [TestCase("empty")]
        [TestCase("unknown")]
        [TestCase("known")]
        public void LegacyTitleLoad_PreservesSave_AndOnlyResolvedEquipmentStartsCandidate(string variant)
        {
            Prepare(true);
            LoadLegacy(variant);
            var request = Request(Hero, new[] { 1, 2 }, 11);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, request.AllyFieldObjects[0], _party, _progress, out string message), Is.True, message);
            CreateEntry(true);
            int started = 0; _entry.OnCombatStarted += _ => started++;
            if (variant == "known")
            {
                Assert.That(_entry.StartCombat(request), Is.True);
                Assert.That(started, Is.EqualTo(1));
                AssertLegacySession(_entry.ActiveSession);
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.CombatResolving));
            }
            else
            {
                string failure = variant == "empty" ? @"Combat start rejected\..*source ExplicitEmpty; automatic fallback is disabled"
                    : @"Combat start rejected\..*unresolved or ambiguous; stored equipment was preserved";
                LogAssert.Expect(LogType.Warning, new Regex(@"\[CombatEntryPoint\] " + failure));
                Assert.That(_entry.StartCombat(request), Is.False);
                Assert.That(_entry.ActiveSession, Is.Null); Assert.That(started, Is.Zero);
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            }
            AssertLegacy(variant);
        }

        [TestCase("empty")]
        [TestCase("unknown")]
        public void LegacyDirectDungeonLoad_PreservesState_ButLacksAuthoredHeroDefinition(string variant)
        {
            Prepare(false); LoadLegacy(variant);
            var request = Request(Hero, new[] { 1, 2 }, 11);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, request.AllyFieldObjects[0], _party, _progress, out string message), Is.False);
            Assert.That(message, Is.EqualTo("Character 'hero.test' has no authored definition or progression state."));
            AssertLegacy(variant);
        }

        [TestCase("empty")]
        [TestCase("unknown")]
        [TestCase("known")]
        public void LegacyCurrentConfig_LoadPreservesState_OnlyResolvedEquipmentStarts(string variant)
        {
            Prepare(true); LoadLegacy(variant);
            var request = Request(Hero, new[] { 1, 2 }, 11);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, request.AllyFieldObjects[0], _party, _progress, out string message), Is.True, message);
            CreateEntry(false);
            if (variant == "known")
            {
                Assert.That(_entry.StartCombat(request), Is.True);
                Assert.That(_entry.ActiveSession.Allies[0].Skills.Select(skill => skill.Id.Value), Is.EquivalentTo(new[] { 1, 2 }));
                Assert.That(_entry.ActiveSession.HasProductionHandBpInitialization, Is.False);
            }
            else
            {
                LogAssert.Expect(LogType.Warning, new Regex(@"Combat start rejected\..*" + (variant == "empty" ? "source ExplicitEmpty" : "unresolved or ambiguous")));
                Assert.That(_entry.StartCombat(request), Is.False);
                Assert.That(_entry.ActiveSession, Is.Null);
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            }
            AssertLegacy(variant);
        }

        [Test]
        public void AuthoredOfficialStart_NewGameDuplicateAndExplicitRestart_UseCanonicalOwners()
        {
            Prepare(true, true);
            object request = NewGame(); AssertAnand(true); StartAnandCombat(2, 3, 12);
            EndCombat();
            _skills.Runtime.TryUnequip(Anand, Zorn); _progress.ApplyExperience(Anand, 5);
            Apply(request);
            Assert.That(_skills.Runtime.GetEquippedSkills(Anand), Is.EqualTo(new[] { Basic }));
            Assert.That(_progress.TryGetState(Anand, out _, out int exp), Is.True); Assert.That(exp, Is.EqualTo(5));
            NewGame(); AssertAnand(true); StartAnandCombat(2, 3, 11);
        }

        [Test]
        public void AuthoredOfficialStart_ChangedSaveLoadDoesNotReplayInitialGrants()
        {
            Prepare(true, true); NewGame();
            _skills.Runtime.TryUnequip(Anand, Zorn); _progress.ApplyExperience(Anand, 350);
            _flow.EnterExploration(); Assert.That(_save.TrySave(out string message), Is.True, message);
            string text = File.ReadAllText(_save.PrimarySavePath);
            NewGame(); AssertAnand(true);
            Assert.That(_save.TryLoad(out message), Is.True, message);
            Assert.That(File.ReadAllText(_save.PrimarySavePath), Is.EqualTo(text));
            AssertAnand(false); StartAnandCombat(2, 0, 12);
        }

        [Test]
        public void ExistingFourAndProposedThreeRegistryEntries_AreUniqueAndRetainLegacyEnemies()
        {
            Prepare(true);
            Assert.That(_registry.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12, 10001, 10002, 10003 }));
            Assert.That(_registry.Select(skill => skill.skillId).Distinct().Count(), Is.EqualTo(7));
            Assert.That(_registry.Select(skill => skill.PersistentKey).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(7));
            var config = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(config.EnableProductionHandBp, Is.False);
            Assert.That(config.InitialMp, Is.EqualTo(2)); Assert.That(config.NormalHandCapacity, Is.EqualTo(2)); Assert.That(config.ClashHandCapacity, Is.EqualTo(3));
            Assert.That(config.HandCardRules.Select(rule => rule.Skill.skillId), Is.EqualTo(new[] { 10001, 10002, 1, 2 }));
            Assert.That(config.HandCardRules.Select(rule => rule.Kind), Is.EqualTo(new[] { CombatCardKind.Normal, CombatCardKind.Clash, CombatCardKind.Normal, CombatCardKind.Clash }));
            Assert.That(config.HandCardRules.Select(rule => rule.Copies), Is.EqualTo(new[] { 2, 3, 4, 6 }));
            Assert.That(config.BpMaximum, Is.EqualTo(3)); Assert.That(config.BpRoundCyclesPerGain, Is.EqualTo(2)); Assert.That(config.BpMaximumInvestment, Is.EqualTo(3));
            Assert.That(config.ClashAttackerDurability, Is.EqualTo(3)); Assert.That(config.ClashResponderDurability, Is.EqualTo(3));
            Assert.That(config.ClashMaxComparisons, Is.EqualTo(3)); Assert.That(config.ClashDecisionDurationSeconds, Is.EqualTo(5));
            Assert.That(config.UltimateSkills, Is.Empty); Assert.That(config.EnemyHandSourceKeys, Is.Empty);
        }

        [Test]
        public void ProtectedSceneConnectionsAndDebug_RemainAtPreActivationBoundary() => AssertProductionConnectionsRemainPending();

        [Test]
        public void LegacyRules_ApplyToAnotherEquippedCharacter_AndNewGameStillRestoresAnandTwoThree()
        {
            Prepare(true, true); NewGame(); AssertAnand(true);
            foreach (string key in Keys("known"))
            {
                Assert.That(_skills.Runtime.TryAcquire(Anand, key).Status, Is.EqualTo(CharacterSkillAcquireStatus.Acquired));
                Assert.That(_skills.Runtime.TryEquip(Anand, key).Status, Is.EqualTo(CharacterSkillEquipStatus.Equipped));
            }
            StartAnandCombat(2, 3, 11);
            var session = _entry.ActiveSession;
            Assert.That(session.TryGetHand(session.Allies[0], out var hand), Is.True);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(15));
            foreach (var expected in new[] { (id: 10001, count: 2), (id: 10002, count: 3), (id: 1, count: 4), (id: 2, count: 6) })
                Assert.That(hand.DeckSnapshot.Count(card => card.Skill.Id.Value == expected.id), Is.EqualTo(expected.count));
            EndCombat(); NewGame(); AssertAnand(true); StartAnandCombat(2, 3, 12);
            Assert.That(_entry.ActiveSession.TryGetHand(_entry.ActiveSession.Allies[0], out hand), Is.True);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(5));
            Assert.That(hand.DeckSnapshot.All(card => card.Skill.Id.Value == 10001 || card.Skill.Id.Value == 10002), Is.True);
        }

        internal static void AssertLegacySession(CombatSession session)
        {
            Assert.That(session, Is.Not.Null);
            Assert.That(session.HasProductionHandBpInitialization, Is.True);
            var ally = session.Allies.Single();
            Assert.That(session.TryGetAllyCharacterId(ally, out string id), Is.True); Assert.That(id, Is.EqualTo(Hero));
            Assert.That(ally.Skills.Select(skill => skill.Id.Value), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(session.TryGetHand(ally, out var hand), Is.True);
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(10));
            Assert.That(hand.DeckSnapshot.Count(card => card.Kind == CombatCardKind.Normal && card.Skill.Id.Value == 1), Is.EqualTo(4));
            Assert.That(hand.DeckSnapshot.Count(card => card.Kind == CombatCardKind.Clash && card.Skill.Id.Value == 2), Is.EqualTo(6));
            Assert.That(hand.DeckSnapshot.Select(card => card.RuntimeId).Distinct().Count(), Is.EqualTo(10));
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(2));
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(3));
            Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
            Assert.That(session.BpRuntime.TryGetBalance(ally, out int bp), Is.True); Assert.That(bp, Is.Zero);
        }

        public static void WriteColdEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = "C2ACompatibilityRestart";
            foreach (string variant in new[] { "empty", "unknown", "known", "AnandInitial", "AnandChanged" })
            {
                var fixture = new ProductionInitializationCompatibilityTests();
                try
                {
                    fixture.Prepare(true, true);
                    if (variant.StartsWith("Anand", StringComparison.Ordinal))
                    {
                        fixture.NewGame();
                        if (variant == "AnandChanged") { fixture._skills.Runtime.TryUnequip(Anand, Zorn); fixture._progress.ApplyExperience(Anand, 350); }
                    }
                    else fixture.LoadLegacy(variant);
                    fixture._flow.EnterExploration();
                    Assert.That(fixture._save.TrySave(out string message), Is.True, message);
                    File.Copy(fixture._save.PrimarySavePath, ColdPrefix + variant + ".json", true);
                }
                finally { fixture.Cleanup(); }
            }
            File.WriteAllText(ColdPrefix + "WriterPid.txt", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
        }

        public static void ReadColdEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = "C2ACompatibilityRestart";
            Assert.That(File.ReadAllText(ColdPrefix + "WriterPid.txt"), Is.Not.EqualTo(System.Diagnostics.Process.GetCurrentProcess().Id.ToString()));
            foreach (string variant in new[] { "empty", "unknown", "known", "AnandInitial", "AnandChanged" })
            {
                var fixture = new ProductionInitializationCompatibilityTests();
                try
                {
                    fixture.Prepare(true, true);
                    File.Copy(ColdPrefix + variant + ".json", fixture._save.PrimarySavePath, true);
                    string original = File.ReadAllText(fixture._save.PrimarySavePath);
                    int notifications = 0; fixture._skills.Runtime.Changed += () => notifications++;
                    Assert.That(fixture._save.TryLoad(out string message), Is.True, message);
                    Assert.That(notifications, Is.EqualTo(1));
                    Assert.That(File.ReadAllText(fixture._save.PrimarySavePath), Is.EqualTo(original));
                    if (variant.StartsWith("Anand", StringComparison.Ordinal))
                    {
                        bool initial = variant == "AnandInitial";
                        fixture.AssertAnand(initial); fixture.StartAnandCombat(2, initial ? 3 : 0, 11);
                    }
                    else
                    {
                        fixture.AssertLegacy(variant);
                        if (variant == "known")
                        {
                            fixture._flow.EnterExploration(); fixture.CreateEntry(true);
                            Assert.That(fixture._entry.StartCombat(fixture.Request(Hero, new[] { 1, 2 }, 11)), Is.True);
                            AssertLegacySession(fixture._entry.ActiveSession);
                            Assert.That(File.ReadAllText(fixture._save.PrimarySavePath), Is.EqualTo(original));
                        }
                    }
                }
                finally { fixture.Cleanup(); }
            }
            File.WriteAllText(ColdPrefix + "ReadEvidence.txt", "PASS: distinct Unity processes; hero.test empty/unknown/known disk Load preserves state without grant replay; known Legacy candidate starts with deck Normal4/Clash6 and visible Hand2/3; official Anand initial 2/3 and changed 2/0 candidate sessions. Authored Production Config remains disabled.");
        }

        private void Prepare(bool retainTitleLegacyDefinition, bool useAuthoredStart = false)
        {
            var title = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            var authoredProgress = title.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharacterProgressionService>(true)).Single();
            var definitions = new SerializedObject(authoredProgress).FindProperty("definitions");
            var legacy = Enumerable.Range(0, definitions.arraySize).Select(i => (CharacterProgressionDefinitionSO)definitions.GetArrayElementAtIndex(i).objectReferenceValue).Single(definition => definition.CharacterId == Hero);
            string legacyPath = AssetDatabase.GetAssetPath(legacy);
            var dungeon = EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
            var authoredEntry = dungeon.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
            Assert.That(authoredEntry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
            string[] registryPaths = authoredEntry.RegisteredSkillDefinitions.Select(AssetDatabase.GetAssetPath).Concat(new[] {
                "Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset", "Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset", "Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset" }).ToArray();
            string currentConfigPath = AssetDatabase.GetAssetPath(authoredEntry.FinalExchangeRuntimeConfig);
            var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); empty.name = "C2ACompatibilityRestart";
            // Close the source scenes before resolving test inputs: cold Editor runs unload assets
            // that are only referenced by managed fixture objects rather than a live Unity owner.
            legacy = AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>(legacyPath);
            _registry = registryPaths.Select(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>).ToArray();
            _currentConfig = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(currentConfigPath);
            Assert.That(legacy, Is.Not.Null);
            Assert.That(_registry.All(skill => skill != null), Is.True);
            var state = Component<GameStateMachine>(); Call(state, "Awake");
            _flow = Component<GameFlowController>(); Call(_flow, "Awake"); _flow.RequestState(GameState.Title, "C2A isolated gate");
            _party = Component<PartyRuntime>(); Call(_party, "Awake");
            _progress = Component<CharacterProgressionService>();
            if (retainTitleLegacyDefinition) Set(_progress, "definitions", new List<CharacterProgressionDefinitionSO> { legacy });
            Call(_progress, "Awake");
            _skills = Component<CharacterSkillSaveParticipant>(); Call(_skills, "Awake");
            _save = Component<SaveLoadService>(); Call(_save, "Awake");
            _directory = Path.Combine(Path.GetTempPath(), "GAME002_C2A_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory);
            Call(_save, "SetStoragePathForTests", Path.Combine(_directory, "save.json"));
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath);
            if (!useAuthoredStart) { start = Track(Object.Instantiate(start)); WriteGrants(start); }
            var host = Track(new GameObject("TestOnly.C2A.Bootstrap")); host.SetActive(false);
            _bootstrap = host.AddComponent<RuntimeBootstrapper>(); Set(_bootstrap, "characterStartDefinition", start);
            Set(_bootstrap, "legacyCharacterDefinition", legacy);
            Call(_bootstrap, "ConfigureCharacterServices", _party, _progress, false);
            Assert.That(_party.StartDefinition, Is.SameAs(start));
            Assert.That(_progress.TryGetDefinition(Hero, out _), Is.EqualTo(retainTitleLegacyDefinition));
        }

        private void LoadLegacy(string variant)
        {
            var data = new GameSaveData(); data.header.activeSceneId = SceneManager.GetActiveScene().name;
            data.party.memberIds.Add(Hero); data.party.leaderCharacterId = Hero; data.party.selectedCombatMemberIds.Add(Hero);
            data.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = Hero, level = 2, experience = 5 });
            string[] keys = Keys(variant);
            data.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData { characterId = Hero, acquiredSkillKeys = keys.ToList(), equippedSkillKeys = keys.ToList() });
            string text = SaveSerializer.ToJson(data); File.WriteAllText(_save.PrimarySavePath, text);
            int notifications = 0; _skills.Runtime.Changed += () => notifications++;
            Assert.That(_save.TryLoad(out string message), Is.True, message);
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(File.ReadAllText(_save.PrimarySavePath), Is.EqualTo(text));
            AssertLegacy(variant);
        }

        private void AssertLegacy(string variant)
        {
            Assert.That(_party.Members, Is.EqualTo(new[] { Hero })); Assert.That(_party.CombatLineup, Is.EqualTo(new[] { Hero }));
            Assert.That(_party.LeaderCharacterId, Is.EqualTo(Hero));
            Assert.That(_progress.TryGetState(Hero, out int level, out int exp), Is.True); Assert.That(level, Is.EqualTo(2)); Assert.That(exp, Is.EqualTo(5));
            Assert.That(_skills.Runtime.GetAcquiredSkills(Hero), Is.EquivalentTo(Keys(variant)));
            Assert.That(_skills.Runtime.GetEquippedSkills(Hero), Is.EqualTo(Keys(variant)));
            Assert.That(_skills.Runtime.GetAcquiredSkills(Anand), Is.Empty, "Load must not replay Anand's initial grants.");
            Assert.That(SaveSerializer.FromGameSaveJson(File.ReadAllText(_save.PrimarySavePath)).header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
        }

        private void AssertAnand(bool initial)
        {
            Assert.That(_party.Members, Is.EqualTo(new[] { Anand })); Assert.That(_party.CombatLineup, Is.EqualTo(new[] { Anand }));
            Assert.That(_progress.TryGetState(Anand, out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(initial ? 1 : 3)); Assert.That(exp, Is.EqualTo(initial ? 0 : 50));
            Assert.That(_skills.Runtime.GetAcquiredSkills(Anand), Is.EquivalentTo(new[] { Basic, Zorn }));
            Assert.That(_skills.Runtime.GetEquippedSkills(Anand), Is.EqualTo(initial ? new[] { Basic, Zorn } : new[] { Basic }));
        }

        private object NewGame()
        {
            _flow.RequestState(GameState.Title, "C2A explicit New Game"); object[] args = { null, null };
            Assert.That(Call(_save, "TryCreateNewGameRequest", args), Is.True, args[1] as string); Apply(args[0]); return args[0];
        }
        private void Apply(object request) { object[] args = { request, null }; Assert.That(Call(_save, "TryApplyNewGameRequest", args), Is.True, args[1] as string); }

        private void CreateEntry(bool candidate)
        {
            if (_entry != null) return;
            Assert.That(_registry.All(skill => skill != null), Is.True, "Scene closure must not discard a fixture's registry inputs.");
            Assert.That(_registry.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12, 10001, 10002, 10003 }));
            var host = Track(new GameObject("TestOnly.C2A.Combat")); var orchestrator = host.AddComponent<CombatFlowOrchestrator>(); var director = host.AddComponent<CombatDirector>(); _entry = host.AddComponent<CombatEntryPoint>();
            Set(_entry, "director", director); Set(_entry, "flowOrchestrator", orchestrator); Set(orchestrator, "entryPoint", _entry);
            Set(_entry, "skillDefinitions", _registry); Set(_entry, "finalExchangeRuntimeConfig", _currentConfig);
            if (candidate) Track(EnableCandidate(_entry));
            Call(_entry, "Awake");
        }

        private void StartAnandCombat(int normal, int clash, int enemyId)
        {
            _flow.EnterExploration(); CreateEntry(true);
            var request = Request(Anand, new[] { 10001 }, enemyId);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, request.AllyFieldObjects[0], _party, _progress, out string message), Is.True, message);
            Assert.That(_entry.StartCombat(request), Is.True);
            var session = _entry.ActiveSession; var ally = session.Allies[0];
            Assert.That(session.HasProductionHandBpInitialization, Is.True); Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
            Assert.That(session.TryGetHand(ally, out var hand), Is.True);
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(normal)); Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(clash));
            Assert.That(session.BpRuntime.TryGetBalance(ally, out int bp), Is.True); Assert.That(bp, Is.Zero);
            Assert.That(session.Enemies.Single().Skills.Single().Id.Value, Is.EqualTo(enemyId));
            Assert.That(session.StartReason, Is.EqualTo(StartReason.PlayerGotHit)); Assert.That(session.InitiativeSide, Is.EqualTo(Side.Enemies));
        }

        private CombatStartRequest Request(string id, int[] playerSkills, int enemyId)
        {
            var request = new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            var player = Actor(playerSkills); request.AllyFieldObjects.Add(player); request.BindAllyCharacter(player, id); request.EnemyFieldObjects.Add(Actor(new[] { enemyId })); return request;
        }
        private GameObject Actor(int[] ids) { var actor = Track(new GameObject("TestOnly.C2A.Actor")); actor.AddComponent<CombatHpComponent>(); Set(actor.AddComponent<CombatSkillLoadoutComponent>(), "skillIds", ids); return actor; }
        private void EndCombat() { if (_entry?.ActiveStateMachine != null) { _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Call(_entry, "Update"); _flow.EnterExploration(); } }

        [TearDown]
        public void Cleanup()
        {
            EndCombat();
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]); _created.Clear();
            if (_directory != null && Directory.Exists(_directory))
            {
                string absolute = Path.GetFullPath(_directory);
                Assert.That(absolute.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_C2A_"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(absolute, true);
            }
        }
        private T Component<T>() where T : Component => Track(new GameObject("TestOnly.C2A." + typeof(T).Name)).AddComponent<T>();
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
        private static T Load<T>(string relative) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/GAME/Data/" + relative);
        private static string[] Keys(string variant) => variant == "empty" ? Array.Empty<string>() : variant == "unknown" ? new[] { "legacy.skill" } : new[] { "skill.production.0001", "skill.production.0002" };
        private static void WriteGrants(CharacterStartDefinitionSO start)
        {
            var data = new SerializedObject(start); var grants = data.FindProperty("initialSkillGrants"); grants.arraySize = 1;
            var grant = grants.GetArrayElementAtIndex(0); grant.FindPropertyRelative("characterId").stringValue = Anand;
            var skills = grant.FindPropertyRelative("skills"); skills.arraySize = 2;
            skills.GetArrayElementAtIndex(0).objectReferenceValue = Load<SkillDefinitionSO>("Skill/Skill_AnandBasic_PlaytestV1.asset"); skills.GetArrayElementAtIndex(1).objectReferenceValue = Load<SkillDefinitionSO>("Skill/Skill_Zornhau_PlaytestV1.asset"); data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertProductionConnectionsRemainPending()
        {
            try
            {
                var title = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
                var bootstrap = title.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RuntimeBootstrapper>(true)).Single();
                Assert.That(new SerializedObject(bootstrap).FindProperty("characterStartDefinition").objectReferenceValue, Is.Null);
                var dungeon = EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
                bootstrap = dungeon.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RuntimeBootstrapper>(true)).Single();
                Assert.That(new SerializedObject(bootstrap).FindProperty("characterStartDefinition").objectReferenceValue, Is.Null);
                Assert.That(new SerializedObject(bootstrap).FindProperty("legacyCharacterDefinition").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterProgressionDefinitionSO>("Assets/GAME/Data/Test/CP_TestHero.asset")));
                var entry = dungeon.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
                Assert.That(entry.RegisteredSkillDefinitions.Select(skill => skill.skillId), Is.EqualTo(new[] { 1, 2, 11, 12 }));
                Assert.That(entry.FinalExchangeRuntimeConfig, Is.Not.SameAs(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath)));
                Assert.That(AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath).EnableProductionHandBp, Is.False);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
                Assert.That(entry.GetComponents<CombatBpValidationOptIn>(), Has.Length.EqualTo(1)); Assert.That(entry.GetComponents<CombatHandValidationOptIn>(), Has.Length.EqualTo(1)); Assert.That(entry.GetComponents<CombatClashSeriesValidationOptIn>(), Has.Length.EqualTo(1));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
    }
}
