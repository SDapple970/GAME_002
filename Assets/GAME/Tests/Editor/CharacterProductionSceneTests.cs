using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Player;
using Game.Reward;
using Game.Story.Core;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    /// <summary>Actual Production owners/scenes with transient test configuration, never approved character content.</summary>
    public sealed class CharacterProductionSceneTests
    {
        private const string HeroA = "b02.scene.test.a";
        private const string HeroB = "b02.scene.test.b";
        private readonly List<UnityEngine.Object> _transient = new();
        private readonly List<string> _errors = new();
        private CharacterStartDefinitionSO _start;
        private string _directory;
        private bool _sceneReady;
        private bool _loadCompleted;
        private bool _loadSucceeded;

        [UnityTest]
        public IEnumerator TitleNewGame_ContactReward_ColdContinue_RestoresCharacterPartyAndSchemaEleven()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/TitleScene.unity");
            yield return new EnterPlayMode();
            yield return TitleScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator TitleScenario()
        {
            Application.logMessageReceived += RecordLog;
            yield return null;
            PrepareStart(); InstallStart();
            SceneManager.sceneLoaded += ConfigureLoadedScene;
            SaveLoadService service = SaveLoadService.Instance;
            _directory = Path.Combine(Path.GetTempPath(), "GAME002_B02_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory);
            service.SetStoragePathForTests(Path.Combine(_directory, "isolated.json"));
            Assert.That(service.TryResetForNewGame(out string resetMessage), Is.True, resetMessage);
            Assert.That(service.TryResetForNewGame(out resetMessage), Is.True, resetMessage);
            AssertInitialParty();
            yield return StartNewGame();
            yield return Wait(() => SceneManager.GetActiveScene().name == "Dungeon_1_Production" && GameStateMachine.Instance.Current == GameState.Exploration, "Title New Game");
            yield return null;
            AssertInitialParty();
            PartyRuntime originalParty = PartyRuntime.Instance; CharacterProgressionService originalProgression = CharacterProgressionService.Instance;
            yield return ContactAndReward(HeroA);
            State(HeroA, 5, 0); State(HeroB, 1, 0);
            PartyRuntime.Instance.SetLeader(HeroB); PartyRuntime.Instance.SelectForCombat(HeroA, false); PartyRuntime.Instance.SelectForCombat(HeroB, true);
            Assert.That(RewardService.Instance.GrantReward(new RewardGrantRequest(RewardSourceType.Story, "b02.exp", exp: 17, actionId: "grant", progressionTargetId: HeroB)).Exp, Is.EqualTo(17));
            State(HeroB, 2, 7);
            StoryFlagManager.Instance.SetBool("b02.b01.keep", false); StoryFlagManager.Instance.SetInt("b02.b01.count", 19);
            Teleport(Player().transform.position + new Vector3(30, 30, 0));
            Assert.That(service.TrySave(out string saveMessage), Is.True, saveMessage);
            string savedJson = File.ReadAllText(service.PrimarySavePath);
            Assert.That(JsonUtility.FromJson<GameSaveData>(savedJson).header.schemaVersion, Is.EqualTo(11));

            _sceneReady = false; SceneFlowController.Instance.LoadScene("TitleScene", HandleSceneReady);
            yield return Wait(() => _sceneReady && GameStateMachine.Instance.Current == GameState.Title, "return Title");
            Assert.That(PartyRuntime.Instance, Is.SameAs(originalParty)); State(HeroB, 2, 7);
            Assert.That(PartyRuntime.Instance.LeaderCharacterId, Is.EqualTo(HeroB));
            UnityEngine.Object.Destroy(originalParty.gameObject); UnityEngine.Object.Destroy(originalProgression.gameObject);
            yield return Wait(() => PartyRuntime.Instance == null && CharacterProgressionService.Instance == null, "remove persistent character owners");
            _loadCompleted = false; _loadSucceeded = false; service.OnLoadCompleted += HandleLoad;
            GAME.Title.TitleSceneController title = UnityEngine.Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Button continueButton = new SerializedObject(title).FindProperty("continueButton").objectReferenceValue as Button;
            Assert.That(continueButton, Is.Not.Null); Assert.That(continueButton.interactable, Is.True); continueButton.onClick.Invoke();
            yield return Wait(() => _loadCompleted, "Title Continue cold owners");
            Assert.That(_loadSucceeded, Is.True); Assert.That(PartyRuntime.Instance, Is.Not.SameAs(originalParty)); Assert.That(CharacterProgressionService.Instance, Is.Not.SameAs(originalProgression));
            Assert.That(PartyRuntime.Instance.Members, Is.EqualTo(new[] { HeroA, HeroB })); Assert.That(PartyRuntime.Instance.LeaderCharacterId, Is.EqualTo(HeroB));
            Assert.That(PartyRuntime.Instance.CombatLineup, Is.EqualTo(new[] { HeroB })); State(HeroA, 5, 0); State(HeroB, 2, 7);
            Assert.That(StoryFlagManager.Instance.HasBool("b02.b01.keep"), Is.True); Assert.That(StoryFlagManager.Instance.GetBool("b02.b01.keep"), Is.False);
            Assert.That(StoryFlagManager.Instance.GetInt("b02.b01.count"), Is.EqualTo(19));
            PartyRuntime.Instance.AddMember("stale"); CharacterProgressionService.Instance.ApplyExperience(HeroB, 5);
            _loadCompleted = false; Assert.That(service.TryLoad(out string loadMessage), Is.True, loadMessage);
            yield return Wait(() => _loadCompleted, "repeated Load"); State(HeroB, 2, 7); Assert.That(PartyRuntime.Instance.Contains("stale"), Is.False);
            Assert.That(File.ReadAllText(service.PrimarySavePath), Is.EqualTo(savedJson));

            _sceneReady = false; SceneFlowController.Instance.LoadScene("TitleScene", HandleSceneReady);
            yield return Wait(() => _sceneReady && GameStateMachine.Instance.Current == GameState.Title, "Title for another New Game");
            yield return StartNewGame();
            yield return Wait(() => SceneManager.GetActiveScene().name == "Dungeon_1_Production" && GameStateMachine.Instance.Current == GameState.Exploration, "second New Game");
            AssertInitialParty(); State(HeroA, 1, 0); State(HeroB, 1, 0);
            Assert.That(StoryFlagManager.Instance.HasBool("b02.b01.keep"), Is.False);
            Assert.That(_errors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DirectDungeon_ConfiguredStartup_FieldAttackUsesPartyIdentity()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/Dungeon_1_Production.unity");
            yield return new EnterPlayMode();
            yield return DirectDungeonScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator DirectDungeonScenario()
        {
            Application.logMessageReceived += RecordLog;
            yield return null;
            // Current scene has no approved startup asset. Compatibility remains explicit and unconfigured.
            Assert.That(PartyRuntime.Instance, Is.Not.Null); Assert.That(PartyRuntime.Instance.StartDefinition, Is.Null);
            Assert.That(CharacterProgressionService.Instance.TryGetDefinition(HeroA, out _), Is.False);
            PrepareStart(); InstallStart(); AssertInitialParty(); State(HeroA, 1, 0);
            Player().GetComponent<Rigidbody2D>().gravityScale = 0;
            CombatEncounterGroup group = UnityEngine.Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).First();
            foreach (Game.Enemies.FieldEnemyPatrolAI2D patrol in group.GetComponentsInChildren<Game.Enemies.FieldEnemyPatrolAI2D>()) patrol.enabled = false;
            Bounds bounds = ContactBounds(group); Collider2D playerCollider = Player().GetComponentInChildren<Collider2D>();
            Teleport(new Vector3(bounds.min.x - playerCollider.bounds.extents.x - .6f, bounds.center.y, 0));
            PlayerFieldAttackController fieldAttack = Player().GetComponent<PlayerFieldAttackController>();
            Set(fieldAttack, "hitBoxSize", new Vector2(4.8f, 3));
            // Use the Production command receiver; no new input ownership or debug runtime dependency.
            fieldAttack.RequestPrimaryAttack();
            CombatEntryPoint entry = UnityEngine.Object.FindFirstObjectByType<CombatEntryPoint>();
            yield return Wait(() => entry.ActiveSession != null, "field attack entry");
            Assert.That(entry.ActiveSession.StartReason, Is.EqualTo(StartReason.PlayerFirstHit));
            Assert.That(entry.ActiveSession.ProgressionTargetCharacterId, Is.EqualTo(HeroA));
            Assert.That(entry.ActiveSession.Allies, Has.Count.EqualTo(1)); Assert.That(_errors, Is.Empty);
        }

        private IEnumerator ContactAndReward(string id)
        {
            Player().GetComponent<Rigidbody2D>().gravityScale = 0;
            CombatEncounterGroup group = UnityEngine.Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).First();
            Teleport(ContactBounds(group).center);
            CombatEntryPoint entry = UnityEngine.Object.FindFirstObjectByType<CombatEntryPoint>();
            yield return Wait(() => entry.ActiveSession != null, "contact entry");
            Assert.That(entry.ActiveSession.StartReason, Is.EqualTo(StartReason.PlayerGotHit)); Assert.That(entry.ActiveSession.ProgressionTargetCharacterId, Is.EqualTo(id));
            Assert.That(entry.ActiveSession.FlowMode, Is.EqualTo(CombatFlowMode.StandoffClashChain)); Assert.That(entry.ActiveSession.Allies, Has.Count.EqualTo(1));
            // Existing Editor-only completion hook isolates B02 identity/reward integration from combat rules.
            foreach (ICombatant enemy in entry.ActiveSession.Enemies) enemy.ApplyDamage(enemy.HP);
            Invoke(entry, "ForceFinishCombat", CombatEndReason.Victory);
            yield return Wait(() => GameStateMachine.Instance.Current == GameState.Reward, "combat Reward");
            RewardUIPanel panel = UnityEngine.Object.FindFirstObjectByType<RewardUIPanel>();
            Button close = new SerializedObject(panel).FindProperty("closeButton").objectReferenceValue as Button; Assert.That(close, Is.Not.Null); close.onClick.Invoke();
            yield return Wait(() => GameStateMachine.Instance.Current == GameState.Exploration, "Reward close");
        }

        private void PrepareStart()
        {
            _start = Track(ScriptableObject.CreateInstance<CharacterStartDefinitionSO>());
            Set(_start, "definitions", new[] { Definition(HeroA), Definition(HeroB) }); Set(_start, "initialMemberIds", new[] { HeroA, HeroB });
            Set(_start, "initialLeaderId", HeroA); Set(_start, "initialCombatMemberIds", new[] { HeroA }); Set(_start, "defaultRewardTargetId", HeroB);
        }
        private CharacterProgressionDefinitionSO Definition(string id)
        {
            CharacterProgressionDefinitionSO definition = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            Set(definition, "characterId", id); Set(definition, "startingLevel", 1); Set(definition, "maximumLevel", 5); Set(definition, "experienceRequiredByLevel", new[] { 10, 20, 30, 40 }); return definition;
        }
        private void InstallStart()
        {
            RuntimeBootstrapper bootstrap = UnityEngine.Object.FindFirstObjectByType<RuntimeBootstrapper>(); Assert.That(bootstrap, Is.Not.Null);
            Set(bootstrap, "characterStartDefinition", _start); Invoke(bootstrap, "BootstrapCoreServices", true, false, false);
        }
        private void ConfigureLoadedScene(Scene scene, LoadSceneMode mode)
        { if (_start != null && (scene.name == "TitleScene" || scene.name == "Dungeon_1_Production")) InstallStart(); }
        private void HandleSceneReady(bool success) => _sceneReady = success;
        private void HandleLoad(bool success, string message) { _loadCompleted = true; _loadSucceeded = success; }
        private void RecordLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message); }
        private static void AssertInitialParty()
        { Assert.That(PartyRuntime.Instance.Members, Is.EqualTo(new[] { HeroA, HeroB })); Assert.That(PartyRuntime.Instance.LeaderCharacterId, Is.EqualTo(HeroA)); Assert.That(PartyRuntime.Instance.CombatLineup, Is.EqualTo(new[] { HeroA })); }
        private static void State(string id, int level, int exp)
        { Assert.That(CharacterProgressionService.Instance.TryGetState(id, out int actualLevel, out int actualExp), Is.True); Assert.That(actualLevel, Is.EqualTo(level)); Assert.That(actualExp, Is.EqualTo(exp)); }
        private static GameObject Player() => GameObject.FindGameObjectWithTag("Player");
        private static Bounds ContactBounds(CombatEncounterGroup group) => group.GetComponentsInChildren<CombatEncounterTrigger2D>().First().GetComponent<Collider2D>().bounds;
        private static void Teleport(Vector3 position)
        { Player().transform.position = position; Rigidbody2D body = Player().GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private T Track<T>(T value) where T : UnityEngine.Object { value.hideFlags = HideFlags.HideAndDontSave; _transient.Add(value); return value; }
        private static IEnumerator Wait(Func<bool> ready, string step)
        { float deadline = Time.realtimeSinceStartup + 30; while (!ready() && Time.realtimeSinceStartup < deadline) yield return null; Assert.That(ready(), Is.True, "Timed out: " + step); }
        private static IEnumerator StartNewGame()
        {
            GAME.Title.TitleSceneController title = UnityEngine.Object.FindFirstObjectByType<GAME.Title.TitleSceneController>(); SerializedObject data = new(title);
            Button start = data.FindProperty("startButton").objectReferenceValue as Button; Button paper = data.FindProperty("paperClickButton").objectReferenceValue as Button;
            start.onClick.Invoke(); yield return Wait(() => paper.interactable, "NPC paper"); paper.onClick.Invoke(); yield return Wait(() => paper.interactable, "monster paper"); paper.onClick.Invoke();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= ConfigureLoadedScene; Application.logMessageReceived -= RecordLog;
            if (SaveLoadService.Instance != null) SaveLoadService.Instance.OnLoadCompleted -= HandleLoad;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (UnityEngine.Object value in _transient) if (value != null) UnityEngine.Object.DestroyImmediate(value); _transient.Clear(); _errors.Clear();
            if (_directory != null && Directory.Exists(_directory))
            {
                string resolved = Path.GetFullPath(_directory); Assert.That(resolved.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_B02_"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
        }
    }
}
