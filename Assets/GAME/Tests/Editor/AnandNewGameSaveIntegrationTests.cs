using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Actions;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Integration
{
    public sealed class AnandNewGameSaveIntegrationTests
    {
        private const string Id = "char.anand_de_morte";
        private const string Basic = "skill.playtest.anand.basic";
        private const string Zorn = "skill.playtest.anand.zornhau";
        private const string StartPath = "Assets/GAME/Data/Character/CharacterStart_AnandSkills_PlaytestV1_Candidate.asset";
        private const string ConfigPath = "Assets/GAME/Data/Combat/Runtime/FinalCombatRuntimeConfig_AnandStart_PlaytestV1_Candidate.asset";
        private readonly List<Object> _created = new();
        private SaveLoadService _save;
        private PartyRuntime _party;
        private CharacterProgressionService _progress;
        private CharacterSkillSaveParticipant _skills;
        private RuntimeBootstrapper _bootstrap;
        private GameFlowController _gameFlow;
        private CombatEntryPoint _entry;
        private string _directory;

        // Invoked explicitly once. Unity supplies the new asset GUID; no existing data is saved.
        public static void CreateStartCandidate()
        {
            if (File.Exists(StartPath) || AssetDatabase.LoadMainAssetAtPath(StartPath) != null)
                throw new InvalidOperationException("Existing candidate must not be overwritten.");
            var start = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(
                "Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset"));
            start.name = "CharacterStart_AnandSkills_PlaytestV1_Candidate";
            var data = new SerializedObject(start);
            var grants = data.FindProperty("initialSkillGrants"); grants.arraySize = 1;
            var grant = grants.GetArrayElementAtIndex(0);
            grant.FindPropertyRelative("characterId").stringValue = Id;
            var skills = grant.FindPropertyRelative("skills"); skills.arraySize = 2;
            skills.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset");
            skills.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!start.TryValidate(out string message)) throw new InvalidOperationException(message);
            AssetDatabase.CreateAsset(start, StartPath);
            AssetDatabase.SetLabels(start, new[] { "PlaytestV1", "Candidate", "ProductionWiringPending" });
            AssetDatabase.SaveAssetIfDirty(start);
        }

        // Run these commands in separate Unity processes; files stay under ignored Logs, never the user's save directory.
        public static void WriteProcessRestartEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AnandRestartVerification";
            var fixture = new AnandNewGameSaveIntegrationTests();
            try
            {
                fixture.Prepare(); fixture.Apply(fixture.Request()); fixture._gameFlow.EnterExploration();
                Assert.That(fixture._save.TrySave(out string message), Is.True, message);
                File.Copy(fixture._save.PrimarySavePath, "Logs/CBT-R2-08-P2B3D4E1CR1-RestartInitial.json", true);
                fixture._skills.Runtime.TryUnequip(Id, Zorn); fixture._progress.ApplyExperience(Id, 350);
                Assert.That(fixture._save.TrySave(out message), Is.True, message);
                File.Copy(fixture._save.PrimarySavePath, "Logs/CBT-R2-08-P2B3D4E1CR1-RestartChanged.json", true);
                File.WriteAllText("Logs/CBT-R2-08-P2B3D4E1CR1-RestartWriterPid.txt", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            }
            finally { fixture.TearDown(); }
        }

        public static void ReadProcessRestartEvidence()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AnandRestartVerification";
            Assert.That(File.ReadAllText("Logs/CBT-R2-08-P2B3D4E1CR1-RestartWriterPid.txt"), Is.Not.EqualTo(System.Diagnostics.Process.GetCurrentProcess().Id.ToString()));
            foreach (string variant in new[] { "Initial", "Changed" })
            {
                var fixture = new AnandNewGameSaveIntegrationTests();
                try
                {
                    fixture.Prepare();
                    string path = "Logs/CBT-R2-08-P2B3D4E1CR1-Restart" + variant + ".json";
                    File.Copy(path, fixture._save.PrimarySavePath, true);
                    int notifications = 0; fixture._skills.Runtime.Changed += () => notifications++;
                    Assert.That(fixture._save.TryLoad(out string message), Is.True, message);
                    Assert.That(notifications, Is.EqualTo(1));
                    if (variant == "Initial") fixture.AssertStart();
                    else
                    {
                        Assert.That(fixture._skills.Runtime.GetEquippedSkills(Id), Is.EqualTo(new[] { Basic }));
                        Assert.That(fixture._progress.TryGetState(Id, out int level, out int exp), Is.True);
                        Assert.That(level, Is.EqualTo(3)); Assert.That(exp, Is.EqualTo(50));
                    }
                    fixture.StartCombat(); AssertDeck(fixture._entry.ActiveSession, 2, variant == "Initial" ? 3 : 0);
                }
                finally { fixture.TearDown(); }
            }
            File.WriteAllText("Logs/CBT-R2-08-P2B3D4E1CR1-RestartReadEvidence.txt", "PASS: distinct Unity writer/reader processes; initial 2/3 deck and changed 2/0 deck; schema14; one restore notification; no initial grant replay.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_entry != null && _entry.ActiveStateMachine != null)
            { _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Call(_entry, "Update"); }
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            if (_directory != null && Directory.Exists(_directory))
            {
                string resolved = Path.GetFullPath(_directory);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_AnandStart_"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
            _directory = null;
        }

        [Test]
        public void FirstNewGame_GrantsOnlyAnandWithNoFilmOrDuplicateKeys()
        {
            Prepare();
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.Empty, "Configuration is not New Game.");
            Apply(Request());
            AssertStart();
            Assert.That(_skills.Runtime.GetSharedFilms(), Is.Empty);
            Assert.That(_skills.Runtime.GetAcquiredSkills("hero.test"), Is.Empty);
        }

        [Test]
        public void DuplicateRequest_PreservesInterveningProgressEquipmentAndNotifications()
        {
            Prepare(); object request = Request(); Apply(request);
            _progress.ApplyExperience(Id, 350); _skills.Runtime.TryUnequip(Id, Zorn);
            _party.AddMember("saved.other");
            int notifications = 0; _skills.Runtime.Changed += () => notifications++;
            Apply(request); _gameFlow.EnterExploration(); Apply(request);
            Assert.That(notifications, Is.Zero);
            Assert.That(_progress.TryGetState(Id, out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(3)); Assert.That(exp, Is.EqualTo(50));
            Assert.That(_skills.Runtime.GetEquippedSkills(Id), Is.EqualTo(new[] { Basic }));
            Assert.That(_party.Contains("saved.other"), Is.True);
            _gameFlow.RequestState(GameState.Title, "return"); Apply(request);
            Assert.That(_party.Contains("saved.other"), Is.True, "A Title revisit does not identify a new selection.");
        }

        [Test]
        public void DuplicateRequest_AfterProgress_BuildsExactlyTwoNormalAndThreeClash()
        {
            Prepare(); object request = Request(); Apply(request); _progress.ApplyExperience(Id, 5);
            Apply(request); Apply(request);
            Assert.That(_progress.TryGetState(Id, out _, out int exp), Is.True); Assert.That(exp, Is.EqualTo(5));
            StartCombat(); AssertDeck(_entry.ActiveSession, 2, 3);
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.EquivalentTo(new[] { Basic, Zorn }));
        }

        [Test]
        public void ExplicitNewSelection_ResetsOldProgressAndEquipment_RejectsUnappliedStaleRequest()
        {
            Prepare(); object stale = Request(); object first = Request();
            Assert.That(ApplyResult(stale), Is.False);
            Apply(first); _progress.ApplyExperience(Id, 350); _skills.Runtime.TryUnequip(Id, Zorn);
            _party.AddMember("old.session");
            _gameFlow.EnterExploration(); _gameFlow.RequestState(GameState.Title, "explicit restart");
            Apply(Request()); AssertStart();
            Apply(first); AssertStart();
            StartCombat(); AssertDeck(_entry.ActiveSession, 2, 3);
        }

        [Test]
        public void ReentrantDuplicate_DoesNotRunResetOwnersTwice()
        {
            Prepare(); object request = Request(); int resets = 0;
            _skills.Runtime.Changed += () => { resets++; Assert.That(ApplyResult(request), Is.False); };
            Apply(request);
            Assert.That(resets, Is.EqualTo(5), "One clear and one acquisition/equip notification per skill.");
            AssertStart();
        }

        [Test]
        public void InitialDiskSave_ColdOwnersContinue_RestoresWithoutInitialGrant()
        {
            Prepare(); object request = Request(); Apply(request);
            _gameFlow.EnterExploration(); Assert.That(_save.TrySave(out string message), Is.True, message);
            ColdSkillPartyAndProgressOwners();
            _gameFlow.RequestState(GameState.Title, "cold continue");
            int notifications = 0; _skills.Runtime.Changed += () => notifications++;
            Assert.That(_save.TryLoad(out message), Is.True, message);
            Assert.That(notifications, Is.EqualTo(1), "Restore only, no acquire/equip/reset notifications.");
            AssertStart(); Apply(request); AssertStart();
            StartCombat(); AssertDeck(_entry.ActiveSession, 2, 3);
        }

        [Test]
        public void ChangedEquipment_DiskLoadAndRepeatedBootstrap_PreserveUserLoadout()
        {
            Prepare(); Apply(Request()); _skills.Runtime.TryUnequip(Id, Zorn);
            _progress.ApplyExperience(Id, 350); _gameFlow.EnterExploration();
            Assert.That(_save.TrySave(out string message), Is.True, message);
            ColdSkillPartyAndProgressOwners(); Assert.That(_save.TryLoad(out message), Is.True, message);
            Configure(); Configure();
            Assert.That(_skills.Runtime.GetEquippedSkills(Id), Is.EqualTo(new[] { Basic }));
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.EquivalentTo(new[] { Basic, Zorn }));
            Assert.That(_progress.TryGetState(Id, out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(3)); Assert.That(exp, Is.EqualTo(50));
            StartCombat(); AssertDeck(_entry.ActiveSession, 2, 0);
        }

        [Test]
        public void Load_InvalidatesUnappliedTitleRequest_AndOldHeroSaveGetsNoAnandSkills()
        {
            Prepare(); object request = Request();
            var data = new GameSaveData();
            data.party.memberIds.Add("hero.test"); data.party.selectedCombatMemberIds.Add("hero.test"); data.party.leaderCharacterId = "hero.test";
            data.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "hero.test", level = 2, experience = 5 });
            data.characterSkills.characters.Add(new CharacterAcquiredSkillSaveData { characterId = "hero.test", acquiredSkillKeys = new() { "legacy.skill" }, equippedSkillKeys = new() { "legacy.skill" } });
            File.WriteAllText(_save.PrimarySavePath, SaveSerializer.ToJson(data));
            Assert.That(_save.TryLoad(out string message), Is.True, message);
            _gameFlow.RequestState(GameState.Title, "late callback");
            Assert.That(ApplyResult(request), Is.False); Configure();
            Assert.That(_party.Members, Is.EqualTo(new[] { "hero.test" }));
            Assert.That(_skills.Runtime.GetEquippedSkills("hero.test"), Is.EqualTo(new[] { "legacy.skill" }));
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.Empty);
            Assert.That(_progress.TryGetState("hero.test", out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(2)); Assert.That(exp, Is.EqualTo(5));
        }

        [Test]
        public void LegacyDiskSave_MigratesWithoutRetroactiveSkillGrantOrFileRewrite()
        {
            Prepare(); Apply(Request());
            string json = SaveSerializer.ToJson(new SaveData { gold = 17 });
            File.WriteAllText(_save.PrimarySavePath, json);
            Assert.That(_save.TryLoad(out string message), Is.True, message);
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.Empty);
            Assert.That(_skills.Runtime.GetEquippedSkills(Id), Is.Empty);
            Assert.That(File.ReadAllText(_save.PrimarySavePath), Is.EqualTo(json));
        }

        [Test]
        public void CandidateSnapshot_ConsumesIndependentCardsMpAndRecyclesWithoutChangingOwnership()
        {
            Prepare(); Apply(Request()); StartCombat();
            var session = _entry.ActiveSession; var ally = session.Allies[0]; var enemy = session.Enemies[0];
            AssertDeck(session, 2, 3); session.TryGetHand(ally, out var hand);
            long[] ids = hand.DeckSnapshot.Select(card => card.RuntimeId).ToArray();
            var normals = hand.Hand.Where(card => card.Kind == CombatCardKind.Normal).ToArray();
            foreach (var card in normals)
            {
                var attack = new CombatAttackDeclaration(ally, enemy, card.Skill, card.RuntimeId);
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.True);
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, attack, null, out _), Is.False);
            }
            Assert.That(hand.NormalDiscard.Count, Is.EqualTo(2));
            var clashes = hand.Hand.Where(card => card.Kind == CombatCardKind.Clash).ToArray();
            var enemyAttack = new CombatAttackDeclaration(enemy, ally, enemy.Skills[0]);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(CombatExchangeCommitPolicy.TryCommit(session, enemyAttack,
                    new CombatResponseDeclaration(ally, clashes[i].Skill, clashes[i].RuntimeId), out var result), Is.True);
                Assert.That(result.ResponseState, Is.EqualTo(CombatResponseState.CounterDeclared));
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(1 - i));
            }
            Assert.That(hand.ClashDiscard.Count, Is.EqualTo(2));
            Assert.That(hand.Hand.Single().RuntimeId, Is.EqualTo(clashes[2].RuntimeId));
            Assert.That(session.GetCombatState(ally).CanSpendMp(1), Is.False);
            Assert.That(CombatExchangeCommitPolicy.TryCommit(session, enemyAttack,
                new CombatResponseDeclaration(ally, clashes[2].Skill, clashes[2].RuntimeId), out var exhausted), Is.False);
            Assert.That(exhausted.Failure, Is.EqualTo(CombatExchangeCommitFailure.InsufficientAttackMp), "Both actors retain their natural initial MP; no fixture MP grant.");
            Assert.That(hand.Hand.Single().RuntimeId, Is.EqualTo(clashes[2].RuntimeId));
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Allies, 1, true), Is.True);
            Assert.That(session.TryRecordCompletedAttackCycle(Side.Enemies, 1, true), Is.True);
            Assert.That(session.TryBeginNextCardExchange(1), Is.True);
            AssertDeck(session, 2, 3);
            Assert.That(hand.Hand.Select(card => card.RuntimeId), Is.EquivalentTo(ids));
            Assert.That(hand.NormalDiscard, Is.Empty); Assert.That(hand.ClashDiscard, Is.Empty);
            Assert.That(hand.RefillCount, Is.Zero);
            AssertStart();
            _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); Call(_entry, "Update"); _gameFlow.EnterExploration();
            AssertStart(); StartCombat(); AssertDeck(_entry.ActiveSession, 2, 3);
            Assert.That(_entry.ActiveSession, Is.Not.SameAs(session));
            _entry.ActiveSession.TryGetHand(_entry.ActiveSession.Allies[0], out var fresh);
            Assert.That(fresh, Is.Not.SameAs(hand)); Assert.That(fresh.RefillCount, Is.Zero);
        }

        [Test]
        public void AnotherCharacterSnapshot_UsesItsOwnEquippedSkillsOnly()
        {
            Prepare(); Apply(Request());
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset");
            var other = Actor(new[] { basic.skillId });
            var request = new CombatStartRequest(StartReason.PlayerFirstHit, Side.Allies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.Add(other); request.BindAllyCharacter(other, "other.character");
            Game.Combat.Integration.PersistentSkillCombatLoadoutBridge.ApplyToRequest(request, new[] { basic }, null);
            Assert.That(request.TryGetAllyLoadoutSnapshot(other, out _), Is.False);
            Assert.That(_skills.Runtime.GetAcquiredSkills("other.character"), Is.Empty);
            Assert.That(_skills.Runtime.GetEquippedSkills("other.character"), Is.Empty);
        }

        [TestCase("characterId")]
        [TestCase("duplicate")]
        [TestCase("film")]
        public void InvalidInitialGrant_RejectsConfigurationWithoutResettingState(string invalid)
        {
            Prepare(); var start = Track(Object.Instantiate(LoadStart())); var data = new SerializedObject(start);
            var grant = data.FindProperty("initialSkillGrants").GetArrayElementAtIndex(0);
            if (invalid == "characterId") grant.FindPropertyRelative("characterId").stringValue = "other.character";
            else
            {
                var skills = grant.FindPropertyRelative("skills");
                if (invalid == "duplicate") skills.GetArrayElementAtIndex(1).objectReferenceValue = skills.GetArrayElementAtIndex(0).objectReferenceValue;
                else
                {
                    var film = Track(Object.Instantiate((SkillDefinitionSO)skills.GetArrayElementAtIndex(0).objectReferenceValue));
                    Set(film, "ownershipCategory", SkillOwnershipCategory.Film); skills.GetArrayElementAtIndex(0).objectReferenceValue = film;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo(); Assert.That(start.TryValidate(out _), Is.False);
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ActualPlayMode_NewGameSnapshotEnemyFirstResponseAndSecondCombat()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            try
            {
                Prepare(); object request = Request(); Apply(request); StartCombat();
                var session = _entry.ActiveSession; var ally = session.Allies[0];
                session.TryGetHand(ally, out var hand); AssertDeck(session, 2, 3);
                var flow = _entry.FlowOrchestrator;
                var card = hand.Hand.First(value => value.Kind == CombatCardKind.Clash);
                var pending = flow.PendingDecisionRequest;
                Assert.That(pending.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), pending), Is.True);
                Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(1));
                Assert.That(hand.ClashDiscard.Single().RuntimeId, Is.EqualTo(card.RuntimeId));
                Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(ally, card.Skill, card.RuntimeId), pending), Is.False);
                _entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
                Assert.That(_entry.ActiveSession, Is.Null); _gameFlow.EnterExploration();
                _progress.ApplyExperience(Id, 5); Apply(request);
                Assert.That(_progress.TryGetState(Id, out _, out int exp), Is.True); Assert.That(exp, Is.EqualTo(5));
                StartCombat(); AssertDeck(_entry.ActiveSession, 2, 3);
                Assert.That(_skills.Runtime.GetEquippedSkills(Id), Is.EquivalentTo(new[] { Basic, Zorn }));
            }
            finally { TearDown(); }
            yield return new ExitPlayMode();
        }

        private void Prepare()
        {
            foreach (var owner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (owner is SaveLoadService || owner is PartyRuntime || owner is CharacterProgressionService ||
                    owner is CharacterSkillSaveParticipant || owner is RuntimeBootstrapper || owner is GameFlowController || owner is GameStateMachine)
                    if (owner != null) Object.DestroyImmediate(owner.gameObject);
            var state = Component<GameStateMachine>(); Call(state, "Awake");
            _gameFlow = Component<GameFlowController>(); Call(_gameFlow, "Awake"); _gameFlow.RequestState(GameState.Title, "fixture");
            _save = Component<SaveLoadService>(); Call(_save, "Awake");
            _directory = Path.Combine(Path.GetTempPath(), "GAME002_AnandStart_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory);
            Call(_save, "SetStoragePathForTests", Path.Combine(_directory, "save.json"));
            CreateCharacterOwners();
            var host = Track(new GameObject("AnandStart.Bootstrap")); host.SetActive(false);
            _bootstrap = host.AddComponent<RuntimeBootstrapper>(); Set(_bootstrap, "createMissingCoreServices", false);
            Set(_bootstrap, "logWarnings", false); Set(_bootstrap, "applyInitialStateOnStart", false);
            Set(_bootstrap, "characterStartDefinition", LoadStart());
            Configure();
        }

        private void CreateCharacterOwners()
        {
            _party = Component<PartyRuntime>(); Call(_party, "Awake");
            _progress = Component<CharacterProgressionService>(); Call(_progress, "Awake");
            _skills = Component<CharacterSkillSaveParticipant>(); Call(_skills, "Awake");
        }

        private void ColdSkillPartyAndProgressOwners()
        {
            Object.DestroyImmediate(_skills.gameObject); Object.DestroyImmediate(_party.gameObject); Object.DestroyImmediate(_progress.gameObject);
            CreateCharacterOwners(); Configure();
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.Empty);
        }

        private void Configure() => Call(_bootstrap, "ConfigureCharacterServices", _party, _progress, false);
        private object Request()
        {
            object[] args = { null, null };
            Assert.That(Call(_save, "TryCreateNewGameRequest", args), Is.True, args[1] as string);
            return args[0];
        }
        private bool ApplyResult(object request) => (bool)Call(_save, "TryApplyNewGameRequest", request, null);
        private void Apply(object request)
        { object[] args = { request, null }; Assert.That(Call(_save, "TryApplyNewGameRequest", args), Is.True, args[1] as string); }
        private void AssertStart()
        {
            Assert.That(_party.Members, Is.EqualTo(new[] { Id })); Assert.That(_party.CombatLineup, Is.EqualTo(new[] { Id }));
            Assert.That(_progress.TryGetState(Id, out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(1)); Assert.That(exp, Is.Zero);
            Assert.That(_skills.Runtime.GetAcquiredSkills(Id), Is.EquivalentTo(new[] { Basic, Zorn }));
            Assert.That(_skills.Runtime.GetEquippedSkills(Id), Is.EquivalentTo(new[] { Basic, Zorn }));
        }

        private void StartCombat()
        {
            _gameFlow.EnterExploration();
            var source = AssetDatabase.LoadAssetAtPath<FinalCombatRuntimeConfigSO>(ConfigPath);
            Assert.That(AssetDatabase.AssetPathToGUID(ConfigPath), Is.EqualTo("44a8b6f4c6314396953f0792e126d13a"));
            Assert.That(source.EnableProductionHandBp, Is.True);
            if (_entry == null)
            {
                var config = Track(Object.Instantiate(source)); Set(config, "enableProductionHandBp", true);
                var host = Track(new GameObject("AnandStart.Combat")); var flow = host.AddComponent<CombatFlowOrchestrator>();
                var director = host.AddComponent<CombatDirector>(); _entry = host.AddComponent<CombatEntryPoint>();
                Set(_entry, "director", director); Set(_entry, "flowOrchestrator", flow); Set(flow, "entryPoint", _entry);
                Set(_entry, "finalExchangeRuntimeConfig", config);
                var definitions = source.HandCardRules.Select(rule => rule.Skill).ToList();
                definitions.Add(AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AngelTheProphecy_PlaytestV1.asset"));
                Set(_entry, "skillDefinitions", definitions.ToArray()); Call(_entry, "Awake");
            }
            var request = new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 10, 3, null, CombatFlowMode.StandoffClashChain);
            var ally = Actor(new[] { 10001 }); request.AllyFieldObjects.Add(ally); request.BindAllyCharacter(ally, Id);
            request.EnemyFieldObjects.Add(Actor(new[] { 10003 }));
            Assert.That(_entry.StartCombat(request), Is.True);
            Assert.That(_entry.ActiveSession.GetCombatState(_entry.ActiveSession.Allies[0]).CurrentMp, Is.EqualTo(2));
        }

        private GameObject Actor(int[] ids)
        {
            var actor = Track(new GameObject("AnandStart.Actor")); actor.AddComponent<CombatHpComponent>();
            Set(actor.AddComponent<CombatSkillLoadoutComponent>(), "skillIds", ids); return actor;
        }
        private static void AssertDeck(CombatSession session, int normal, int clash)
        {
            Assert.That(session.TryGetHand(session.Allies[0], out var hand), Is.True);
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(normal));
            Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(clash));
            Assert.That(hand.DeckSnapshot.Count, Is.EqualTo(normal + clash));
            Assert.That(hand.DeckSnapshot.Select(card => card.RuntimeId).Distinct().Count(), Is.EqualTo(normal + clash));
        }
        private static CharacterStartDefinitionSO LoadStart()
        { var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(StartPath); Assert.That(start, Is.Not.Null); return start; }
        private T Component<T>() where T : Component => Track(new GameObject("AnandStart." + typeof(T).Name)).AddComponent<T>();
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
        private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
