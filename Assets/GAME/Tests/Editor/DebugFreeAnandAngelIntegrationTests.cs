using System;
using System.Collections.Generic;
using System.IO;
using Game.Tests.Integration;
using Game.Combat.Actions;
using Game.Combat.Data;
using Game.Combat.Effects;
using Game.Combat.Debugging;
using Game.Combat.UI;
using Game.NonCombat.Party;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using Game.Quest;
using Game.Reward;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using System.Collections;
using System.Linq;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.UI;
using Game.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Game.Player;

public sealed class DebugFreeAnandAngelIntegrationTests
{
    private static int completed;
    private static CombatResult result;
    private static void OnCombatEnded(CombatResult value) { completed++; result = value; }
    private const string ScenePath = "Assets/GAME/Tests/PlayMode/AnandAngel_DebugFree_Integration.unity";
    private static readonly List<Object> transient = new();
    private const string Anand = "char.anand_de_morte";

    // Explicit one-time test asset authoring. Save only the new copy; never apply prefab overrides.
    public static void CreateDebugFreeScene()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Existing test scene must not be overwritten.");
        var source = EditorSceneManager.OpenScene("Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity", OpenSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(source, ScenePath, true), Is.True);
        var copy = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Assert.That(DebugFreeProductionTestSupport.RemoveDebug(copy), Is.EqualTo(1));
        var setup = copy.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AnandAngelPlaytestSetup>(true)).Single();
        Object.DestroyImmediate(setup);
        DebugFreeProductionTestSupport.Set(Object.FindFirstObjectByType<RuntimeBootstrapper>(), "characterStartDefinition", null);
        Assert.That(EditorSceneManager.SaveScene(copy), Is.True);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DebugFreeProductionTestSupport.AssertClean(SceneManager.GetActiveScene());
        Debug.Log("[D4E1DC1] New debug-free scene saved/reloaded; source prefab untouched.");
    }

    public static void PrepareFixtureStartup()
    {
        // Only the C1-created test scene: its first Character configuration is supplied by each test.
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DebugFreeProductionTestSupport.AssertClean(scene);
        var bootstrap = Object.FindFirstObjectByType<RuntimeBootstrapper>();
        var data = new SerializedObject(bootstrap);
        data.FindProperty("characterStartDefinition").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        Assert.That(EditorSceneManager.SaveScene(scene, ScenePath), Is.True);
    }

    [TestCase("Assets/GAME/Scenes/Dungeon_1_Production.unity", 0)]
    [TestCase("Assets/GAME/Scenes/Dungeon_Template.unity", 0)]
    public void OriginalScene_InMemoryRemoval_PreservesPrefabAndAllReferences(string source, int expected)
    {
        string copyPath = AssetDatabase.GenerateUniqueAssetPath("Assets/GAME/Tests/PlayMode/C1_TemporaryRemovalProbe.unity");
        var original = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
        var originalEntry = original.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
        ProductionInitializationConnectionTests.AssertDebugRemovedRuntime(originalEntry);
        DebugFreeProductionTestSupport.AssertClean(original);
        Assert.That(EditorSceneManager.SaveScene(original, copyPath, true), Is.True);
        var scene = EditorSceneManager.OpenScene(copyPath, OpenSceneMode.Single);
        try
        {
            var entry = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatEntryPoint>(true)).Single();
            ProductionInitializationConnectionTests.AssertDebugRemovedRuntime(entry);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
            var prefabEntry = PrefabUtility.GetCorrespondingObjectFromSource(entry);
            Assert.That(prefabEntry.GetComponent<CombatBpValidationOptIn>(), Is.Not.Null);
            Assert.That(DebugFreeProductionTestSupport.RemoveDebug(scene), Is.EqualTo(expected));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(entry), Is.SameAs(prefabEntry));
            Assert.That(prefabEntry.GetComponent<CombatBpValidationOptIn>(), Is.Not.Null);
            Assert.That(PrefabUtility.GetRemovedComponents(entry.gameObject).Any(item => item.assetComponent is CombatBpValidationOptIn), Is.True);
            Assert.That(entry.GetComponent<CombatDirector>(), Is.Not.Null);
            Assert.That(entry.FlowOrchestrator, Is.Not.Null);
            Assert.That(entry.GetComponent<CombatCameraController>(), Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(AssetDatabase.DeleteAsset(copyPath), Is.True);
        }
    }

    [Test]
    public void SavedTestCopy_HasNoDebugOrGrantSetup_AndRetainsSourceInheritance()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        try
        {
            DebugFreeProductionTestSupport.AssertClean(scene);
            Assert.That(Object.FindObjectsByType<AnandAngelPlaytestSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(entry), Is.True);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(entry).GetComponent<CombatBpValidationOptIn>(), Is.Not.Null);
            Assert.That(PrefabUtility.GetRemovedComponents(entry.gameObject).Any(item => item.assetComponent is CombatBpValidationOptIn), Is.True);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    private static void PrepareCanonicalNewGame()
    {
        DebugFreeProductionTestSupport.AssertClean(SceneManager.GetActiveScene());
        Assert.That(Object.FindObjectsByType<CombatEntryPoint>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<GameFlowController>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<UIScreenRouter>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<global::GameInputInstaller>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        var start = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>(
            "Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset"));
        transient.Add(start);
        var data = new SerializedObject(start);
        var grants = data.FindProperty("initialSkillGrants"); grants.arraySize = 1;
        var grant = grants.GetArrayElementAtIndex(0);
        grant.FindPropertyRelative("characterId").stringValue = Anand;
        var skills = grant.FindPropertyRelative("skills"); skills.arraySize = 2;
        skills.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_AnandBasic_PlaytestV1.asset");
        skills.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>("Assets/GAME/Data/Skill/Skill_Zornhau_PlaytestV1.asset");
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(start.TryValidate(out string error), Is.True, error);
        var bootstrap = Object.FindFirstObjectByType<RuntimeBootstrapper>();
        DebugFreeProductionTestSupport.Set(bootstrap, "characterStartDefinition", start);
        DebugFreeProductionTestSupport.Call(bootstrap, "ConfigureCharacterServices", PartyRuntime.Instance, CharacterProgressionService.Instance, false);
        GameFlowController.Instance.RequestState(GameState.Title, "TestOnly canonical New Game");
        object[] request = { null, null };
        Assert.That(DebugFreeProductionTestSupport.Call(SaveLoadService.Instance, "TryCreateNewGameRequest", request), Is.True, request[1] as string);
        object[] apply = { request[0], null };
        Assert.That(DebugFreeProductionTestSupport.Call(SaveLoadService.Instance, "TryApplyNewGameRequest", apply), Is.True, apply[1] as string);
        Assert.That(PartyRuntime.Instance.Members, Is.EqualTo(new[] { Anand }));
        Assert.That(CharacterProgressionService.Instance.TryGetState(Anand, out int level, out int exp), Is.True);
        Assert.That(level, Is.EqualTo(1)); Assert.That(exp, Is.Zero);
        Assert.That(CharacterSkillSaveParticipant.Instance.Runtime.GetEquippedSkills(Anand), Is.EquivalentTo(new[] { "skill.playtest.anand.basic", "skill.playtest.anand.zornhau" }));
        transient.Add(DebugFreeProductionTestSupport.EnableCandidate(Object.FindFirstObjectByType<CombatEntryPoint>()));
        GameFlowController.Instance.EnterExploration();
    }

    private static void AssertSession(CombatSession session)
    {
        var actor = session.Allies.Single();
        Assert.That(actor.Skills.Select(skill => skill.Id.Value), Is.EquivalentTo(new[] { 10001, 10002 }));
        Assert.That(session.Enemies.Single().Skills.Single().Id.Value, Is.EqualTo(10003));
        Assert.That(((SoSkill)actor.Skills.Single(skill => skill.Id.Value == 10001)).ClashPower, Is.EqualTo(2));
        Assert.That(((SoSkill)actor.Skills.Single(skill => skill.Id.Value == 10002)).ClashPower, Is.EqualTo(3));
        Assert.That(session.TryGetHand(actor, out var hand), Is.True);
        Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Normal), Is.EqualTo(2));
        Assert.That(hand.Hand.Count(card => card.Kind == CombatCardKind.Clash), Is.EqualTo(3));
        Assert.That(session.BpRuntime, Is.Not.Null);
        Assert.That(session.BpRuntime.TryGetBalance(actor, out int balance), Is.True); Assert.That(balance, Is.Zero);
        var ui = Object.FindFirstObjectByType<FinalCombatUIBinder>();
        Assert.That(ui.IsBound, Is.True); Assert.That(ui.gameObject.activeInHierarchy, Is.True);
        var phase = Object.FindFirstObjectByType<CombatEntryPoint>().ActiveStateMachine.Phase;
        Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(phase == Phase.Planning || phase == Phase.Standoff
            ? GameState.CombatPlanning : GameState.CombatResolving), "Router state must follow the actual combat phase.");
        Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.True);
        Assert.That(Object.FindFirstObjectByType<CombatCameraController>().IsInCombatMode, Is.True);
    }

    private static void VerifyBpCalculationContract(CombatSession session)
    {
        // Deterministic calculation probe, separate from the Director-driven attack-cycle path.
        // Authored Anand skills have no enhancement/Ultimate: never invent investment content.
        foreach (var skill in session.Allies[0].Skills)
            Assert.That(session.BpRuntime.TryGetEnhancement(skill, out _, out _), Is.False);
        int before = session.BpRuntime.RoundCycleCount;
        for (int i = 0; i < 2; i++)
        {
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Allies, 10000 + i, true), Is.True);
            Assert.That(session.TryRecordCompletedBpAttackCycle(Side.Enemies, 10000 + i, true), Is.True);
        }
        Assert.That(session.BpRuntime.RoundCycleCount, Is.EqualTo(before + 2));
        Assert.That(session.BpRuntime.TryGetBalance(session.Allies[0], out int bp), Is.True);
        Assert.That(bp, Is.GreaterThanOrEqualTo(1));
    }

    private static void InstallQuest(CombatEntryPoint entry, CombatEncounterGroup encounter)
    {
        var definition = ScriptableObject.CreateInstance<QuestDefinitionSO>(); transient.Add(definition);
        var data = new SerializedObject(definition);
        data.FindProperty("questId").stringValue = "testonly.debugfree";
        var objectives = data.FindProperty("objectives"); objectives.arraySize = 1;
        var objective = objectives.GetArrayElementAtIndex(0);
        objective.FindPropertyRelative("objectiveId").stringValue = "angel";
        objective.FindPropertyRelative("eventType").intValue = (int)QuestEventType.Kill;
        objective.FindPropertyRelative("requiredCount").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        var runtime = Object.FindFirstObjectByType<QuestRuntime>(); runtime.StartQuest(definition);
        var publisher = encounter.gameObject.AddComponent<CombatQuestObjectivePublisher>();
        DebugFreeProductionTestSupport.Set(publisher, "combatEntryPoint", entry);
        DebugFreeProductionTestSupport.Set(publisher, "targetEncounter", encounter);
        DebugFreeProductionTestSupport.Set(publisher, "questRuntime", runtime);
        DebugFreeProductionTestSupport.Set(publisher, "questId", "testonly.debugfree");
        DebugFreeProductionTestSupport.Set(publisher, "objectiveId", "angel");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        foreach (var item in transient) if (item != null) Object.DestroyImmediate(item);
        transient.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void LegacyHeroRestore_PreservesIdentity_ButAnandOnlyStartBlocksCombatWithoutMapping()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var host = new GameObject("TestOnly.LegacyRestore");
        try
        {
            var party = host.AddComponent<PartyRuntime>();
            var progression = host.AddComponent<CharacterProgressionService>();
            var start = AssetDatabase.LoadAssetAtPath<CharacterStartDefinitionSO>("Assets/GAME/Data/Character/CharacterStart_AnandDeMorte.asset");
            Assert.That(party.TryConfigureStartDefinition(start, out string message), Is.True, message);
            Assert.That(progression.TryConfigureStartDefinition(start, out message), Is.True, message);
            var save = new GameSaveData();
            save.party.memberIds.Add("hero.test"); save.party.leaderCharacterId = "hero.test";
            save.party.selectedCombatMemberIds.Add("hero.test");
            save.progression.characters.Add(new CharacterProgressionStateSaveData { characterId = "hero.test", level = 2, experience = 5 });
            party.RestoreSaveData(save); progression.RestoreSaveData(save);
            Assert.That(party.Members, Is.EqualTo(new[] { "hero.test" }));
            Assert.That(progression.TryGetState("hero.test", out int level, out int exp), Is.True);
            Assert.That(level, Is.EqualTo(2)); Assert.That(exp, Is.EqualTo(5));
            var player = new GameObject("TestOnly.LegacyPlayer"); player.transform.SetParent(host.transform);
            player.AddComponent<CombatHpComponent>();
            var request = new CombatStartRequest(StartReason.PlayerGotHit, Side.Enemies, 3, 2, null, CombatFlowMode.StandoffClashChain);
            request.AllyFieldObjects.Add(player);
            Assert.That(new CharacterPartyCombatAdapter().TryBindSinglePlayerRequest(request, player, party, progression, out message), Is.False);
            Assert.That(message, Is.EqualTo("Character 'hero.test' has no authored definition or progression state."));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [UnityTest]
    public IEnumerator ContactStartsCombatFromIsolatedScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        PrepareCanonicalNewGame();
        var initialFixedStep = Time.fixedDeltaTime;
        var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
        completed = 0; result = null;
        entry.OnCombatEnded += OnCombatEnded;
        var group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).Single(x => x.name == "TestEncounter_01");
        var trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>();
        Assert.That(new SerializedObject(trigger).FindProperty("encounterGroup").objectReferenceValue, Is.SameAs(group));
        var player = Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None).Single(x => x.CompareTag("Player"));
        Assert.That(player.HP, Is.EqualTo(320));
        Assert.That(group.GetActiveEnemies().Single().GetComponent<CombatHpComponent>().HP, Is.EqualTo(14));
        player.transform.position = trigger.transform.position;
        Physics2D.SyncTransforms();
        var deadline = Time.realtimeSinceStartup + 5f;
        while (entry.ActiveSession == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(entry.ActiveSession, Is.Not.Null, "Contact did not create a combat session.");
        var session = entry.ActiveSession;
        var ally = session.Allies.Single(); var enemy = session.Enemies.Single();
        Assert.That(enemy.HP, Is.EqualTo(14));
        Assert.That(enemy.Skills.Single().Id.Value, Is.EqualTo(10003));
        Assert.That(session.TryGetEnemySource(enemy, out var source), Is.True);
        Assert.That(source.SourceKey, Is.EqualTo("enemy.chapter1.angel"));
        Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
        Assert.That(session.TryGetHand(ally, out var hand), Is.True);
        AssertSession(session);
        var initialCards = hand.Hand.Select(card => (card.RuntimeId, card.Kind)).ToArray();
        int initialExchange = session.CardExchangeIndex;
        var normal = hand.Hand.First(x => x.Kind == CombatCardKind.Normal);
        var flow = entry.FlowOrchestrator;
        Assert.That(flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, enemy, normal.Skill, normal.RuntimeId), session.ExchangeState.Version), Is.True);
        Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
        Assert.That(hand.NormalDiscard.Single().RuntimeId, Is.EqualTo(normal.RuntimeId));
        int usedClashCards = 0;
        deadline = Time.realtimeSinceStartup + 15f;
        while (entry.ActiveSession != null && entry.ActiveStateMachine.Phase != Phase.ChainDecision && Time.realtimeSinceStartup < deadline)
        {
            var token = flow.PendingDecisionRequest;
            if (token?.Kind == CombatExchangeDecisionKind.ClashAttack)
            {
                var clash = hand.Hand.FirstOrDefault(x => x.Kind == CombatCardKind.Clash);
                if (clash != null) { Assert.That(flow.SubmitClashSeriesSkill(token, clash.Skill, clash.RuntimeId), Is.True); usedClashCards++; Assert.That(hand.ClashDiscard.Any(card => card.RuntimeId == clash.RuntimeId), Is.True); Assert.That(session.CardExchangeIndex, Is.EqualTo(initialExchange)); }
            }
            yield return null;
        }
        Assert.That(entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision), "Director did not complete the exchange.");
        Assert.That(usedClashCards, Is.GreaterThan(0));
        Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2 - usedClashCards));
        Assert.That(enemy.HP, Is.LessThan(14));
        Assert.That(hand.ClashDiscard, Is.Empty);
        Assert.That(hand.NormalDiscard, Is.Empty);
        Assert.That(session.CardExchangeIndex, Is.EqualTo(initialExchange + 1));
        Assert.That(hand.Hand.Select(card => (card.RuntimeId, card.Kind)), Is.EquivalentTo(initialCards));
        VerifyBpCalculationContract(session);
        entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort);
        yield return null;
        Assert.That(entry.ActiveSession, Is.Null);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(result.EndReason, Is.EqualTo(CombatEndReason.Abort));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(initialFixedStep));
        Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
        var panel = Object.FindFirstObjectByType<RewardUIPanel>();
        Assert.That(panel, Is.Not.Null);
        var close = (Button)new SerializedObject(panel).FindProperty("closeButton").objectReferenceValue;
        Assert.That(close, Is.Not.Null);
        close.onClick.Invoke();
        var returnDeadline = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < returnDeadline) yield return null;
        Assert.That(trigger.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(entry.ActiveSession, Is.Null, "Unrequested contact restart after reward close.");
        Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.False);
        Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
        Assert.That(GameStateMachine.Instance.AllowsExplorationInput(), Is.True);
        Assert.That(Object.FindFirstObjectByType<CombatCameraController>().IsInCombatMode, Is.False);
        Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.RearmPending));
        player.transform.position += Vector3.left * 20f;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);
        Assert.That(group.Lifecycle, Is.EqualTo(EncounterRuntimeLifecycle.Idle));
        player.transform.position = trigger.transform.position;
        Physics2D.SyncTransforms();
        var reentryDeadline = Time.realtimeSinceStartup + 3f;
        while (entry.ActiveSession == null && Time.realtimeSinceStartup < reentryDeadline) yield return null;
        Assert.That(entry.ActiveSession, Is.Not.Null, "Fresh contact must remain allowed after Abort.");
        entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
        ((Button)new SerializedObject(Object.FindFirstObjectByType<RewardUIPanel>()).FindProperty("closeButton").objectReferenceValue).onClick.Invoke();
        entry.OnCombatEnded -= OnCombatEnded;
        yield return new ExitPlayMode();
    }


    [UnityTest]
    public IEnumerator EnemyFirstContact_ProphecyVsZornhau_ActualDirectorAndUi()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        // Create captured event state after Unity's domain-reload boundary.
        yield return ExerciseEnemyFirstResponse();
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseEnemyFirstResponse()
    {
        yield return null;
        PrepareCanonicalNewGame();
        var entry = Object.FindFirstObjectByType<CombatEntryPoint>();
        var group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).Single(x => x.name == "TestEncounter_01");
        var trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>();
        DebugFreeProductionTestSupport.Set(trigger, "startReason", StartReason.PlayerGotHit);
        DebugFreeProductionTestSupport.Set(trigger, "initiativeSide", Side.Enemies);
        var player = Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None).Single(x => x.CompareTag("Player"));
        player.transform.position = trigger.transform.position; Physics2D.SyncTransforms();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (entry.ActiveSession == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(entry.ActiveSession, Is.Not.Null);
        var session = entry.ActiveSession; AssertSession(session);
        Assert.That(session.StartReason, Is.EqualTo(StartReason.PlayerGotHit));
        Assert.That(session.InitiativeSide, Is.EqualTo(Side.Enemies));
        var flow = entry.FlowOrchestrator;
        session.TryGetHand(session.Allies[0], out var hand);
        var card = hand.Hand.First(x => x.Kind == CombatCardKind.Clash);
        deadline = Time.realtimeSinceStartup + 10f;
        while (flow.PendingDecisionRequest?.Kind != CombatExchangeDecisionKind.Response && Time.realtimeSinceStartup < deadline) yield return null;
        var token = flow.PendingDecisionRequest;
        Assert.That(token, Is.Not.Null);
        Assert.That(token.Kind, Is.EqualTo(CombatExchangeDecisionKind.Response));
        Assert.That(token.DeclaredAttack, Is.Not.Null, "Response token must retain its declared attack.");
        Assert.That(token.DeclaredAttack.Skill, Is.Not.Null);
        Assert.That(token.DeclaredAttack.Skill.Id.Value, Is.EqualTo(10003), "The actual enemy declaration must be The Prophecy.");
        bool reachedChainDecision = false;
        // Enemy AI may leave ChainDecision in the same Update; observe the transition itself.
        var stateMachine = entry.ActiveStateMachine;
        Action<Phase, Phase> observe = (_, next) => reachedChainDecision |= next == Phase.ChainDecision;
        stateMachine.OnPhaseChanged += observe;
        Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(session.Allies[0], card.Skill, card.RuntimeId), token), Is.True);
        Assert.That(session.GetCombatState(session.Allies[0]).CurrentMp, Is.EqualTo(1));
        Assert.That(hand.ClashDiscard.Single().RuntimeId, Is.EqualTo(card.RuntimeId));
        Assert.That(flow.SubmitResponse(new CombatResponseDeclaration(session.Allies[0], card.Skill, card.RuntimeId), token), Is.False);
        deadline = Time.realtimeSinceStartup + 20f;
        while (entry.ActiveSession != null && !reachedChainDecision && Time.realtimeSinceStartup < deadline)
        {
            token = flow.PendingDecisionRequest;
            if (token?.Kind == CombatExchangeDecisionKind.ClashResponse) flow.SubmitClashSeriesNoResponse(token);
            else if (token?.Kind == CombatExchangeDecisionKind.ClashAttack)
            {
                var next = hand.Hand.FirstOrDefault(x => x.Kind == CombatCardKind.Clash);
                if (next != null && session.GetCombatState(session.Allies[0]).CurrentMp > 0) flow.SubmitClashSeriesSkill(token, next.Skill, next.RuntimeId);
            }
            yield return null;
        }
        stateMachine.OnPhaseChanged -= observe;
        Assert.That(reachedChainDecision, Is.True, "Actual Director must finish the exchange and reach the chain decision.");
        Assert.That(Object.FindFirstObjectByType<FinalCombatUIBinder>().IsBound, Is.True);
        entry.ActiveStateMachine.ForceExit(CombatEndReason.Abort); yield return null;
        ((Button)new SerializedObject(Object.FindFirstObjectByType<RewardUIPanel>()).FindProperty("closeButton").objectReferenceValue).onClick.Invoke();
    }

    [UnityTest]
    public IEnumerator FieldAttackVictory_ActualSkillsAndDirector()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseTerminal(true);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EnemyContactDefeat_LowHpSessionFixture()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseTerminal(false);
        yield return new ExitPlayMode();
    }

    private static IEnumerator ExerciseTerminal(bool victory)
    {
        yield return null;
        PrepareCanonicalNewGame();
        completed = 0; result = null;
        var entry = Object.FindFirstObjectByType<CombatEntryPoint>(); entry.OnCombatEnded += OnCombatEnded;
        var group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).Single(x => x.name == "TestEncounter_01");
        var trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>();
        var player = Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None).Single(x => x.CompareTag("Player"));
        var enemyField = group.GetActiveEnemies().Single();
        var fixedStep = Time.fixedDeltaTime;
        InstallQuest(entry, group);
        int ledgerBefore = RewardService.Instance.GrantLedgerCount;
        if (victory)
        {
            var attack = player.GetComponent<PlayerFieldAttackController>();
            var origin = (Transform)new SerializedObject(attack).FindProperty("attackOrigin").objectReferenceValue;
            Assert.That(origin, Is.Not.Null);
            player.transform.position += enemyField.transform.position - origin.position;
            Physics2D.SyncTransforms();
            attack.RequestPrimaryAttack();
        }
        else
        {
            var data = new SerializedObject(trigger); data.FindProperty("startReason").intValue = (int)StartReason.PlayerGotHit; data.FindProperty("initiativeSide").intValue = (int)Side.Enemies; data.ApplyModifiedPropertiesWithoutUndo();
            player.transform.position = trigger.transform.position; Physics2D.SyncTransforms();
        }
        var deadline = Time.realtimeSinceStartup + 5f;
        while (entry.ActiveSession == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(entry.ActiveSession, Is.Not.Null);
        var session = entry.ActiveSession;
        Assert.That(session.StartReason, Is.EqualTo(victory ? StartReason.PlayerFirstHit : StartReason.PlayerGotHit));
        Assert.That(session.InitiativeSide, Is.EqualTo(victory ? Side.Allies : Side.Enemies));
        Assert.That(session.Allies[0].HP, Is.EqualTo(320)); Assert.That(session.Enemies[0].HP, Is.EqualTo(14));
        AssertSession(session);
        if (!victory) session.Allies[0].ApplyDamage(319); // Endpoint fixture; approved Character SO stays unchanged.
        var flow = entry.FlowOrchestrator;
        deadline = Time.realtimeSinceStartup + 60f;
        while (entry.ActiveSession != null && Time.realtimeSinceStartup < deadline)
        {
            var token = flow.PendingDecisionRequest;
            if (token?.Kind == CombatExchangeDecisionKind.Response) flow.SubmitNoResponse(token);
            else if (token?.Kind == CombatExchangeDecisionKind.ClashResponse) flow.SubmitClashSeriesNoResponse(token);
            else if (entry.ActiveStateMachine.Phase == Phase.ChainDecision) flow.EndChain(session.ExchangeState.Version);
            else if (entry.ActiveStateMachine.Phase == Phase.Standoff && session.ExchangeState.CurrentAttackSide == Side.Allies && session.TryGetHand(session.Allies[0], out var hand))
            {
                var card = hand.Hand.FirstOrDefault(x => x.Kind == CombatCardKind.Normal);
                if (card != null) flow.SubmitAttackDeclaration(new CombatAttackDeclaration(session.Allies[0], session.Enemies[0], card.Skill, card.RuntimeId), session.ExchangeState.Version);
            }
            else if (token?.Kind == CombatExchangeDecisionKind.ClashAttack && session.TryGetHand(session.Allies[0], out var attackHand))
            {
                var card = attackHand.Hand.FirstOrDefault(x => x.Kind == CombatCardKind.Clash);
                if (card != null && session.GetCombatState(session.Allies[0]).CurrentMp > 0) flow.SubmitClashSeriesSkill(token, card.Skill, card.RuntimeId);
            }
            yield return null;
        }
        Assert.That(entry.ActiveSession, Is.Null, "Terminal did not complete through actual Director.");
        Assert.That(completed, Is.EqualTo(1)); Assert.That(result.EndReason, Is.EqualTo(victory ? CombatEndReason.Victory : CombatEndReason.Defeat));
        Assert.That(Object.FindFirstObjectByType<QuestRuntime>().GetObjectiveProgress("testonly.debugfree", "angel"), Is.EqualTo(victory ? 1 : 0));
        if (victory)
            Assert.That(RewardService.Instance.GrantLedgerCount, Is.GreaterThan(ledgerBefore));
        else
            Assert.That(RewardService.Instance.GrantLedgerCount, Is.EqualTo(ledgerBefore));
        var panel = Object.FindFirstObjectByType<RewardUIPanel>(); Assert.That(panel.IsOpen, Is.True);
        ((Button)new SerializedObject(panel).FindProperty("closeButton").objectReferenceValue).onClick.Invoke();
        deadline = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(entry.ActiveSession, Is.Null); Assert.That(completed, Is.EqualTo(1));
        if (!victory)
        {
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(GameStateMachine.Instance.AllowsExplorationInput(), Is.False);
            entry.OnCombatEnded -= OnCombatEnded;
            yield break;
        }
        Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
        Assert.That(GameStateMachine.Instance.AllowsExplorationInput(), Is.True);
        Assert.That(Object.FindFirstObjectByType<CombatFieldLock>().IsLocked, Is.False);
        Assert.That(Object.FindFirstObjectByType<CombatCameraController>().IsInCombatMode, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
        Assert.That(group.Lifecycle, Is.EqualTo(victory ? EncounterRuntimeLifecycle.Cleared : EncounterRuntimeLifecycle.RearmPending));
        entry.OnCombatEnded -= OnCombatEnded;
    }
}
