using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Data;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Combat.UI;
using Game.Core;
using Game.EditorTools;
using Game.Enemies;
using Game.Input;
using Game.NonCombat.Inventory;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    /// <summary>Actual Production scene/owners; all unapproved content is transient and all disk saves are isolated.</summary>
    public sealed class FilmUniqueProductionPlayModeTests
    {
        private const string HeroA = "17c6.test.a";
        private const string HeroB = "17c6.test.b";
        private const string QuestId = "ch01.find-first-npc";
        private readonly List<UnityEngine.Object> _transient = new();
        private readonly List<string> _runtimeErrors = new();
        private SkillDefinitionSO[] _registry;
        private SkillDefinitionSO _filmA;
        private SkillDefinitionSO _filmB;
        private SkillDefinitionSO _unowned;
        private Keyboard _keyboard;
        private string _directory;
        private Vector3 _safePosition;
        private bool _loadCompleted;
        private bool _loadSucceeded;
        private int _attackCommands;
        private InputSettings _originalInputSettings;

        [UnityTest]
        [Category("COMBAT17C6")]
        public IEnumerator ProductionScene_ContactAcquire_UIEquip_FieldInputSkill_ColdSaveLoad()
        {
            EditorSceneManager.OpenScene(ProductionCharacterSkillUISetup.ScenePath);
            yield return new EnterPlayMode();
            // Instantiate captured locals only after the test runner's domain-reload boundary.
            yield return RunScenario();
            yield return new ExitPlayMode();
        }

        private IEnumerator RunScenario()
        {
            _runtimeErrors.Clear();
            Application.logMessageReceived += RecordRuntimeLog;
            yield return Wait(() => GameStateMachine.Instance != null && GameStateMachine.Instance.Current == GameState.Exploration, "initial Exploration");
            yield return null;
            // Keep the isolated save location outside live encounter detection/rearm volumes.
            _safePosition = Player().transform.position + new Vector3(30, 30, 0);
            PrepareTransientContent();
            _originalInputSettings = InputSystem.settings;
            InputSettings inputSettings = Track(UnityEngine.Object.Instantiate(_originalInputSettings));
            inputSettings.hideFlags = HideFlags.HideAndDontSave;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = inputSettings;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(_keyboard);
            GameInputInstaller.Instance.Service.Attack += HandleAttack;
            _directory = Path.Combine(Path.GetTempPath(), "GAME002_17C6_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            SaveLoadService service = SaveLoadService.Instance;
            service.SetStoragePathForTests(Path.Combine(_directory, "isolated.json"));
            SceneManager.sceneLoaded += ConfigureLoadedDungeon;
            ConfigureSceneInstances();
            CurrencyWallet.Instance.SetGold(7);
            InventoryService.Instance.AddItem("validation.17c6.token", 2);
            One<QuestRuntime>().StartQuest(AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>("Assets/GAME/Data/Quest/CH01_FindFirstNpc.asset"));

            // No films before an actual contact encounter. Party ownership and UI are real runtime services.
            yield return OpenSkills();
            CharacterSkillPanelView view = One<CharacterSkillPanelView>();
            Assert.That(view.LastModel.CharacterIds, Is.EqualTo(new[] { HeroA, HeroB }));
            Assert.That(view.LastModel.SharedFilms, Is.Empty);
            Assert.That(view.LastModel.UniqueSkills.First().Status, Is.EqualTo(UniqueSkillAvailabilityStatus.Locked));
            Click(view.CloseButton); yield return null;
            Debug.Log("[17C6] Closed initial UI; resolving combat owners.");
            CombatEntryPoint entry = One<CombatEntryPoint>();
            Assert.That(entry, Is.Not.Null);
            Debug.Log("[17C6] Entry resolved: " + entry.name);
            CombatEncounterGroup first = Encounter("dungeon1.encounter.01");
            Debug.Log("[17C6] Encounter resolved: " + first.name);
            Teleport(EnemyContactBounds(first).center);
            yield return new WaitForFixedUpdate();
            yield return Wait(() => entry.ActiveSession != null, "Physics Contact combat");
            Assert.That(entry.ActiveSession.StartReason, Is.EqualTo(StartReason.PlayerGotHit));
            Assert.That(entry.ActiveSession.SkillAcquisitionRecipientCharacterId, Is.EqualTo(HeroA));
            Assert.That(entry.ActiveSession.IsPartySkillAcquisitionEligible, Is.True);
            Assert.That(entry.ActiveSession.Allies.Single().Skills.Any(skill => skill.Id.Value >= 1010), Is.False);
            Assert.That(new FilmEquipApplication(CharacterSkillSaveParticipant.Instance.Runtime, PartyRuntime.Instance, _registry,
                GameStateMachine.Instance, entry).TryEquipFilm(HeroA, _filmA.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.EquipNotAllowed));

            // Arrange the first acquisition using the existing Editor-only completion helper.
            // External HP writes do not represent a resolved FinalExchange attack/terminal decision.
            foreach (ICombatant enemy in entry.ActiveSession.Enemies) enemy.ApplyDamage(enemy.HP);
            entry.GetType().GetMethod("ForceFinishCombat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(entry, new object[] { CombatEndReason.Victory });
            yield return Wait(() => entry.ActiveSession == null, "canonical victory completion");
            yield return Wait(() => GameStateMachine.Instance.Current == GameState.Reward, "Reward state");
            CharacterSkillRuntime runtime = CharacterSkillSaveParticipant.Instance.Runtime;
            Assert.That(runtime.GetSharedFilms(), Is.EquivalentTo(new[] { _filmA.PersistentKey, _filmB.PersistentKey }));
            Assert.That(runtime.GetEquippedSkills(HeroA), Is.Empty);
            Assert.That(CurrencyWallet.Instance.Gold, Is.EqualTo(57));
            Assert.That(CharacterProgressionService.Instance.TryGetState(HeroA, out int level, out _), Is.True);
            Assert.That(level, Is.EqualTo(5), "Production combat EXP should unlock the level-5 Unique.");
            Assert.That(One<QuestRuntime>().GetObjectiveProgress(QuestId, "clear_encounter_01"), Is.EqualTo(1));
            yield return CloseReward();

            yield return OpenSkills();
            view = One<CharacterSkillPanelView>();
            Assert.That(view.LastModel.UniqueSkills.First().IsUnlocked, Is.True);
            Assert.That(view.LastModel.UniqueSkills.Skip(4).All(skill => !skill.IsUnlocked), Is.True);
            ClickFilm(_filmB.PersistentKey, false); yield return null;
            ClickFilm(_filmA.PersistentKey, false); yield return null;
            Assert.That(runtime.GetEquippedSkills(HeroA), Is.EqualTo(new[] { _filmB.PersistentKey, _filmA.PersistentKey }));
            ClickFilm(_filmA.PersistentKey, true); yield return null;
            ClickFilm(_filmA.PersistentKey, false); yield return null;
            ClickCharacter(HeroB); yield return null;
            Assert.That(view.LastModel.UniqueSkills, Is.Empty);
            ClickFilm(_filmA.PersistentKey, false); yield return null;
            Assert.That(runtime.IsEquipped(HeroA, _filmA.PersistentKey) && runtime.IsEquipped(HeroB, _filmA.PersistentKey), Is.True);
            Assert.That(runtime.GetSharedFilms().Count, Is.EqualTo(2), "Films are shared and non-consumable.");
            FilmEquipApplication application = new(runtime, PartyRuntime.Instance, _registry, GameStateMachine.Instance, entry);
            Assert.That(application.TryEquipFilm(HeroB, _unowned.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.NotAcquired));
            Assert.That(application.TryEquipFilm(HeroB, _filmA.PersistentKey).Status, Is.EqualTo(CharacterSkillEquipStatus.AlreadyEquipped));
            Assert.That(application.TryEquipFilm(HeroB, _registry.First(skill => skill.OwnershipCategory == SkillOwnershipCategory.Unique).PersistentKey).Status,
                Is.EqualTo(CharacterSkillEquipStatus.NotFilm));
            ClickCharacter(HeroA); yield return null;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) })
                {
                    PlayModeWindow.SetCustomRenderingResolution((uint)size.x, (uint)size.y, "17C6 isolated content");
                    for (int frame = 0; frame < 4; frame++) yield return null;
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot($"Logs/Combat17C6_PopulatedUI_{size.x}x{size.y}.png");
                    for (int frame = 0; frame < 4; frame++) yield return null;
                }
            }
            Click(view.CloseButton); yield return null;
            Assert.That(new InputRouter().AllowsExplorationInput(), Is.True);

            // Input System keyboard event -> existing installer/field attack coroutine -> Physics overlap -> canonical entry.
            CombatEncounterGroup second = Encounter("dungeon1.encounter.02");
            // Keep fixture geometry deterministic across GPU/headless frame rates; production
            // patrol authoring is untouched and Contact behavior is tested separately above.
            foreach (Game.Enemies.FieldEnemyPatrolAI2D patrol in second.GetComponentsInChildren<Game.Enemies.FieldEnemyPatrolAI2D>())
                patrol.enabled = false;
            // The authored Contact volume extends beyond the stationary enemy hurtbox.
            // Broaden only this runtime fixture's overlap to exercise attack outside that volume.
            SerializedObject fieldAttack = new(Player().GetComponent<Game.Player.PlayerFieldAttackController>());
            fieldAttack.FindProperty("hitBoxSize").vector2Value = new Vector2(4.8f, 3);
            fieldAttack.ApplyModifiedPropertiesWithoutUndo();
            Bounds enemyBounds = EnemyContactBounds(second);
            Collider2D playerCollider = Player().GetComponentInChildren<Collider2D>();
            // Leave room for authored patrol motion during the field attack's hit-delay coroutine.
            Teleport(new Vector3(enemyBounds.min.x - playerCollider.bounds.extents.x - .6f, enemyBounds.center.y, 0));
            yield return new WaitForFixedUpdate();
            Assert.That(entry.ActiveSession, Is.Null, "Field attack must not silently fall back to Contact.");
            InputAction attackAction = GameInputInstaller.Instance.Actions.Gameplay.Attack;
            Assert.That(attackAction.enabled, Is.True, "Production Gameplay.Attack action is disabled.");
            Assert.That(attackAction.controls.Any(control => control.device == _keyboard && control.name == "j"), Is.True,
                "Virtual keyboard is not bound to the existing Attack action.");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.J)); PumpPlayerInput();
            Assert.That(_keyboard.jKey.isPressed, Is.True, "Queued player keyboard event was not processed.");
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            PumpPlayerInput();
            yield return Wait(() => _attackCommands > 0, "existing InputService attack command");
            yield return Wait(() => entry.ActiveSession != null, "Input/Physics Field Attack combat");
            CombatSession fieldSession = entry.ActiveSession;
            Assert.That(fieldSession.StartReason, Is.EqualTo(StartReason.PlayerFirstHit));
            Assert.That(fieldSession.SkillAcquisitionRecipientCharacterId, Is.EqualTo(HeroA));
            int[] snapshot = fieldSession.Allies.Single().Skills.Select(skill => skill.Id.Value).ToArray();
            Assert.That(snapshot, Is.EqualTo(new[] { 1011, 1010, 1020 }));
            Assert.That(snapshot, Has.No.Member(1012));
            Assert.That(snapshot, Has.No.Member(1021));
            GameSaveData beforeMutation = new(); CharacterSkillSaveParticipant.Instance.CaptureSaveData(beforeMutation);
            runtime.TryUnequip(HeroA, _filmA.PersistentKey);
            CharacterSkillSaveParticipant.Instance.RestoreSaveData(beforeMutation);
            Assert.That(fieldSession.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(snapshot));

            // Use the existing FinalCombat UI and player command controller, not a new skill runner.
            FinalCombatUIBinder combatView = One<FinalCombatUIBinder>();
            yield return Wait(() => combatView.ViewState.DecisionKind == CombatExchangeDecisionKind.Attack && combatView.ViewState.SelectableActors.Count > 0,
                "player attack decision UI");
            if (combatView.ViewState.SelectedActor == null) { Click(Options(combatView, "actorListRoot")[0]); yield return null; }
            int skillIndex = combatView.ViewState.SelectableSkills.ToList().FindIndex(skill => skill.Id.Value == 1010);
            Assert.That(skillIndex, Is.GreaterThanOrEqualTo(0));
            Click(Options(combatView, "skillListRoot")[skillIndex]); yield return null;
            Click(Options(combatView, "targetListRoot")[0]); yield return null;
            Assert.That(combatView.ViewState.CanConfirm, Is.True);
            Click((Button)Reference(combatView, "confirmButton"));
            yield return Wait(() => entry.ActiveSession == null, "Film skill damage and terminal victory");
            Assert.That(fieldSession.Enemies.Single().HP, Is.Zero, "Existing Film skill execution must actually defeat the enemy.");
            Assert.That(runtime.GetSharedFilms().Count, Is.EqualTo(2), "Repeated mapped Film grants must deduplicate.");
            Assert.That(One<QuestRuntime>().GetObjectiveProgress(QuestId, "clear_encounter_02"), Is.EqualTo(1));
            yield return CloseReward();

            // A contact after equip must see exactly the same identity/loadout as Field Attack.
            CombatEncounterGroup third = Encounter("dungeon1.encounter.03");
            Teleport(EnemyContactBounds(third).center);
            yield return new WaitForFixedUpdate();
            yield return Wait(() => entry.ActiveSession != null, "equipped Contact comparison");
            Assert.That(entry.ActiveSession.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(snapshot));
            Assert.That(entry.FlowOrchestrator.TerminateExplicit(CombatEndReason.Abort, entry.ActiveSession.ExchangeState.Version), Is.True);
            yield return Wait(() => entry.ActiveSession == null, "abort completion");
            Assert.That(runtime.HasSharedFilm(_unowned.PersistentKey), Is.False, "A living/unwon enemy must not grant Film.");
            yield return CloseReward();

            Teleport(_safePosition);
            Assert.That(service.TrySave(out string saveMessage), Is.True, saveMessage);
            Assert.That(File.Exists(service.PrimarySavePath), Is.True);
            GameSaveData saved = SaveSerializer.FromGameSaveJson(File.ReadAllText(service.PrimarySavePath));
            Assert.That(GameSaveDataMigrator.TryMigrate(File.ReadAllText(service.PrimarySavePath), out GameSaveData migrated, out _, out string migrationError), Is.True, migrationError);
            Assert.That(migrated.reward.ledger.Count, Is.EqualTo(saved.reward.ledger.Count), "Migration must preserve canonical reward records.");
            Debug.Log($"[17C6] Pre-load reward owner={RewardService.Instance.GetInstanceID()}, ledger={RewardService.Instance.GrantLedgerCount}");
            Assert.That(saved.header.schemaVersion, Is.EqualTo(GameSaveDataFormat.CurrentSchemaVersion));
            Assert.That(new Vector3(saved.location.positionX, saved.location.positionY, saved.location.positionZ), Is.EqualTo(_safePosition));
            Assert.That(saved.characterSkills.characters.Single(character => character.characterId == HeroA).equippedSkillKeys,
                Is.EqualTo(new[] { _filmB.PersistentKey, _filmA.PersistentKey }));
            SceneFlowController.Instance.LoadScene("TitleScene");
            yield return Wait(() => SceneManager.GetActiveScene().name == "TitleScene" && GameStateMachine.Instance.Current == GameState.Title, "Title re-entry");
            Assert.That(service.TryResetForNewGame(out string resetMessage), Is.True, resetMessage);
            Assert.That(runtime.GetSharedFilms(), Is.Empty);
            Assert.That(PartyRuntime.Instance.Members, Is.Empty);
            _loadCompleted = false; _loadSucceeded = false;
            service.OnLoadCompleted += HandleLoad;
            Assert.That(service.TryLoad(out string loadMessage), Is.True, loadMessage);
            yield return Wait(() => _loadCompleted && SceneManager.GetActiveScene().name == "Dungeon_1_Production", "cold scene load/restore");
            Assert.That(_loadSucceeded, Is.True);
            service.OnLoadCompleted -= HandleLoad;
            yield return null;
            GameSaveData restored = service.CaptureGameSaveDataSnapshot();
            Assert.That(JsonUtility.ToJson(restored.characterSkills), Is.EqualTo(JsonUtility.ToJson(saved.characterSkills)));
            Assert.That(JsonUtility.ToJson(restored.progression), Is.EqualTo(JsonUtility.ToJson(saved.progression)));
            Assert.That(JsonUtility.ToJson(restored.party), Is.EqualTo(JsonUtility.ToJson(saved.party)));
            Assert.That(JsonUtility.ToJson(restored.inventory), Is.EqualTo(JsonUtility.ToJson(saved.inventory)));
            Assert.That(JsonUtility.ToJson(restored.quest), Is.EqualTo(JsonUtility.ToJson(saved.quest)));
            Assert.That(JsonUtility.ToJson(restored.reward), Is.EqualTo(JsonUtility.ToJson(saved.reward)));
            Assert.That(restored.currency.gold, Is.EqualTo(saved.currency.gold));
            RewardLedgerSaveData firstGrant = saved.reward.ledger.First(record => record.gold > 0);
            RewardGrantResult duplicate = RewardService.Instance.GrantReward(RewardService.CreateCombatRewardRequest(
                new CombatResult { CompletionId = firstGrant.sourceId, EndReason = CombatEndReason.Victory, IsWin = true, TotalGold = 50, TotalExp = 150 }, null));
            Assert.That(duplicate.DuplicateBlocked, Is.True, "Loaded combat rewards must retain idempotency.");
            Assert.That(CurrencyWallet.Instance.Gold, Is.EqualTo(saved.currency.gold));
            yield return OpenSkills();
            view = One<CharacterSkillPanelView>();
            Assert.That(view.LastModel.EquippedFilmKeys, Is.EqualTo(new[] { _filmB.PersistentKey, _filmA.PersistentKey }));
            Assert.That(view.LastModel.UniqueSkills.First().IsUnlocked, Is.True);
            ClickCharacter(HeroB); yield return null;
            Assert.That(view.LastModel.EquippedFilmKeys, Is.EqualTo(new[] { _filmA.PersistentKey }));
            Click(view.CloseButton); yield return null;
            Assert.That(One<EventSystem>(), Is.Not.Null);
            entry = One<CombatEntryPoint>(); third = Encounter("dungeon1.encounter.03");
            Teleport(EnemyContactBounds(third).center);
            yield return new WaitForFixedUpdate();
            yield return Wait(() => entry.ActiveSession != null, "combat after cold Load");
            Assert.That(entry.ActiveSession.Allies.Single().Skills.Select(skill => skill.Id.Value), Is.EqualTo(snapshot));
            Assert.That(entry.FlowOrchestrator.TerminateExplicit(CombatEndReason.Abort, entry.ActiveSession.ExchangeState.Version), Is.True);
            yield return Wait(() => entry.ActiveSession == null, "final abort");
            yield return CloseReward();
            SceneManager.sceneLoaded -= ConfigureLoadedDungeon;
            Assert.That(_runtimeErrors, Is.Empty, string.Join("\n", _runtimeErrors));
            Application.logMessageReceived -= RecordRuntimeLog;
            File.WriteAllText("Logs/Combat17C6_PlayModeEvidence.txt", "PASS: actual Production scene; in-memory unapproved content only; Physics Contact acquisition; real Skill UI buttons/selection/equip/unequip; shared non-consuming independent ownership; combat EXP Unique unlock; Input System J -> field coroutine/Physics -> same snapshot; actual FinalCombat UI Film skill execution/damage/victory; reward/quest; Title/NewGame reset -> isolated disk Load -> UI and combat snapshot restored; loaded combat reward idempotency; schema " + GameSaveDataFormat.CurrentSchemaVersion + "; runtime Console Error/Exception/Assert count 0. Initial acquisition defeat HP and victory were arranged using the fixture and existing Editor-only completion helper; second victory used actual Film skill execution.\n");
        }

        private void PrepareTransientContent()
        {
            SkillDefinitionSO basic = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_BasicAttack.asset");
            _filmA = CloneSkill(basic, "film.a", 1010, SkillOwnershipCategory.Film, "Film A");
            _filmB = CloneSkill(basic, "film.b", 1011, SkillOwnershipCategory.Film, "Film B");
            _unowned = CloneSkill(basic, "film.unowned", 1012, SkillOwnershipCategory.Film, "Unowned Film");
            _filmA.baseDamage = 20;
            SerializedObject attack = new(_filmA); attack.FindProperty("clashPower").intValue = 100; attack.FindProperty("finalMpCost").intValue = 0; attack.ApplyModifiedPropertiesWithoutUndo();
            SkillDefinitionSO[] unique = Enumerable.Range(0, 6).Select(i => CloneSkill(basic, "unique." + i, 1020 + i, SkillOwnershipCategory.Unique, "Unique " + i)).ToArray();
            _registry = One<CombatEntryPoint>().RegisteredSkillDefinitions.Concat(new[] { _filmA, _filmB, _unowned }).Concat(unique).ToArray();
            CharacterProgressionDefinitionSO first = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            CharacterProgressionDefinitionSO second = Track(ScriptableObject.CreateInstance<CharacterProgressionDefinitionSO>());
            FilmUniqueProductionIntegrationTests.ConfigureCharacter(first, HeroA, 4, unique, 100);
            FilmUniqueProductionIntegrationTests.ConfigureCharacter(second, HeroB, 4, Array.Empty<SkillDefinitionSO>(), 100);
            CharacterProgressionService.Instance.ConfigureForTests(HeroA, first, second);
            PartyRuntime.Instance.AddMember(HeroA); PartyRuntime.Instance.AddMember(HeroB);
            PartyRuntime.Instance.SetLeader(HeroA); PartyRuntime.Instance.SelectForCombat(HeroA, true);
        }

        private SkillDefinitionSO CloneSkill(SkillDefinitionSO source, string key, int id, SkillOwnershipCategory category, string name)
        {
            SkillDefinitionSO clone = Track(UnityEngine.Object.Instantiate(source));
            clone.skillId = id; clone.displayName = name;
            SerializedObject data = new(clone);
            data.FindProperty("persistentKey").stringValue = "validation.17c6." + key;
            data.FindProperty("ownershipCategory").enumValueIndex = (int)category;
            data.FindProperty("uniqueOwnerCharacterId").stringValue = category == SkillOwnershipCategory.Unique ? HeroA : null;
            data.ApplyModifiedPropertiesWithoutUndo();
            return clone;
        }

        private void ConfigureLoadedDungeon(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Dungeon_1_Production") ConfigureSceneInstances();
        }

        private void ConfigureSceneInstances()
        {
            CombatEntryPoint entry = One<CombatEntryPoint>();
            SerializedObject data = new(entry); SerializedProperty definitions = data.FindProperty("skillDefinitions");
            definitions.arraySize = _registry.Length;
            for (int i = 0; i < _registry.Length; i++) definitions.GetArrayElementAtIndex(i).objectReferenceValue = _registry[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            entry.GetType().GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(entry, null);
            foreach (CombatEncounterGroup group in UnityEngine.Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (GameObject enemy in group.GetActiveEnemies())
                {
                    EnemyDefinitionSO definition = Track(ScriptableObject.CreateInstance<EnemyDefinitionSO>());
                    SerializedObject enemyData = new(definition);
                    enemyData.FindProperty("persistentKey").stringValue = "validation.17c6.enemy." + group.EncounterId;
                    SerializedProperty keys = enemyData.FindProperty("acquirableSkillPersistentKeys");
                    bool unowned = group.EncounterId.EndsWith("03", StringComparison.Ordinal);
                    keys.arraySize = unowned ? 1 : 2;
                    keys.GetArrayElementAtIndex(0).stringValue = unowned ? _unowned.PersistentKey : _filmA.PersistentKey;
                    if (!unowned) keys.GetArrayElementAtIndex(1).stringValue = _filmB.PersistentKey;
                    enemyData.ApplyModifiedPropertiesWithoutUndo();
                    EnemySourceComponent component = enemy.GetComponent<EnemySourceComponent>() ?? enemy.AddComponent<EnemySourceComponent>();
                    SerializedObject source = new(component); source.FindProperty("definition").objectReferenceValue = definition; source.ApplyModifiedPropertiesWithoutUndo();
                }
            Rigidbody2D body = Player().GetComponent<Rigidbody2D>();
            body.gravityScale = 0; body.linearVelocity = Vector2.zero;
        }

        private IEnumerator OpenSkills()
        {
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Click((Button)Reference(One<CharacterSkillUIHost>(), "openButton"));
            yield return null;
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.UIOnly));
            Assert.That(One<CharacterSkillUIHost>().HasPresenter, Is.True);
            Assert.That(new InputRouter().AllowsExplorationInput(), Is.False);
        }

        private IEnumerator CloseReward()
        {
            yield return Wait(() => GameStateMachine.Instance.Current == GameState.Reward, "reward presentation");
            Teleport(_safePosition);
            yield return new WaitForFixedUpdate();
            Click((Button)Reference(One<RewardUIPanel>(), "closeButton"));
            // Reward close synchronously restores the captured encounter position. Leave again
            // after that restore, before the next physics tick can start the aborted encounter.
            Teleport(_safePosition);
            yield return Wait(() => GameStateMachine.Instance.Current == GameState.Exploration, "Reward -> Exploration");
            yield return null;
        }

        private static void ClickCharacter(string id)
        {
            CharacterSkillPanelView view = One<CharacterSkillPanelView>();
            int index = view.LastModel.CharacterIds.ToList().IndexOf(id);
            Click(((RectTransform)Reference(view, "characterContent")).GetChild(index).GetComponent<Button>());
        }

        private static void ClickFilm(string key, bool unequip)
        {
            CharacterSkillPanelView view = One<CharacterSkillPanelView>();
            int index = unequip ? view.LastModel.EquippedFilmKeys.ToList().IndexOf(key) : view.LastModel.SharedFilms.ToList().FindIndex(item => item.PersistentSkillKey == key);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Click(((RectTransform)Reference(view, unequip ? "equippedContent" : "filmContent")).GetChild(index).GetComponentInChildren<Button>());
        }

        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True, "UI button must be visible and enabled.");
            Canvas.ForceUpdateCanvases();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { button.onClick.Invoke(); return; }
            RectTransform rect = (RectTransform)button.transform;
            PointerEventData pointer = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            List<RaycastResult> hits = new(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, Is.True, "Button is occluded: " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Button[] Options(Component owner, string field) => ((GameObject)Reference(owner, field)).GetComponentsInChildren<Button>().Where(button => button.isActiveAndEnabled).ToArray();
        private static UnityEngine.Object Reference(UnityEngine.Object owner, string field) => new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static T One<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static GameObject Player() => GameObject.FindGameObjectWithTag("Player");
        private static Bounds EnemyContactBounds(CombatEncounterGroup group)
        {
            Assert.That(group, Is.Not.Null);
            CombatEncounterTrigger2D trigger = group.GetComponentsInChildren<CombatEncounterTrigger2D>(true).Single();
            Collider2D collider = trigger.GetComponent<Collider2D>();
            Assert.That(collider, Is.Not.Null, "Production contact collider is missing on " + trigger.name);
            Debug.Log("[17C6] Physics contact bounds: " + collider.bounds);
            return collider.bounds;
        }
        private static CombatEncounterGroup Encounter(string id) => UnityEngine.Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(group => group.EncounterId == id);
        private T Track<T>(T value) where T : UnityEngine.Object { _transient.Add(value); return value; }
        private void HandleLoad(bool success, string message)
        {
            Debug.Log($"[17C6] Load completed: success={success}, state={GameStateMachine.Instance.Current}, position={Player()?.transform.position}, rewardOwner={RewardService.Instance?.GetInstanceID()}, ledger={RewardService.Instance?.GrantLedgerCount}, message={message}");
            _loadSucceeded = success; _loadCompleted = true;
        }

        private void RecordRuntimeLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _runtimeErrors.Add(message + "\n" + trace);
        }
        private void HandleAttack() => _attackCommands++;

        private static void PumpPlayerInput()
        {
            // Editor test enumerators can run inside an Editor input update; the pinned
            // package's public Update() then preserves that type. Pump the queued device
            // event as Dynamic so normal player InputActions/installer callbacks receive it.
            System.Reflection.MethodInfo update = typeof(InputSystem).GetMethod("Update",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null,
                new[] { typeof(InputUpdateType) }, null);
            Assert.That(update, Is.Not.Null, "Pinned Input System player-update test hook changed.");
            update.Invoke(null, new object[] { InputUpdateType.Dynamic });
        }

        private static void Teleport(Vector3 position)
        {
            GameObject player = Player(); Assert.That(player, Is.Not.Null);
            player.transform.position = position;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>(); Assert.That(body, Is.Not.Null);
            body.position = position; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private static IEnumerator Wait(Func<bool> ready, string step)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, "Timed out: " + step);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= ConfigureLoadedDungeon;
            Application.logMessageReceived -= RecordRuntimeLog;
            if (GameInputInstaller.Instance != null) GameInputInstaller.Instance.Service.Attack -= HandleAttack;
            if (SaveLoadService.Instance != null) SaveLoadService.Instance.OnLoadCompleted -= HandleLoad;
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_originalInputSettings != null) InputSystem.settings = _originalInputSettings;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (UnityEngine.Object value in _transient) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _transient.Clear();
            if (_directory != null && Directory.Exists(_directory))
            {
                string resolved = Path.GetFullPath(_directory);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "GAME002_17C6_"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
        }
    }
}
