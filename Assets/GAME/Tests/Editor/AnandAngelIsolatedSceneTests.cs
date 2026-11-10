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

public sealed class AnandAngelIsolatedSceneTests
{
    private static int completed;
    private static CombatResult result;
    private static void OnCombatEnded(CombatResult value) { completed++; result = value; }
    private const string ScenePath = "Assets/GAME/Tests/PlayMode/AnandAngel_IsolatedPlaytest.unity";
    private const string PrefabPath = "Assets/GAME/Tests/PlayMode/Enemy_Angel_Playtest_Candidate.prefab";

    [Test]
    public void CandidateHasAuthoredSourceAndLoadoutWithoutScenePatrolReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<CombatHpComponent>().MaxHP, Is.EqualTo(14));
        Assert.That(prefab.GetComponent<CombatSkillLoadoutComponent>().SkillIds, Is.EqualTo(new[] { 10003 }));
        Assert.That(prefab.GetComponent<EnemySourceComponent>().Definition, Is.SameAs(AssetDatabase.LoadMainAssetAtPath("Assets/GAME/Data/Enemy/Enemy_Angel.asset")));
        Assert.That(prefab.GetComponent<FieldEnemyMotor2D>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<FieldEnemyPatrolAI2D>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Not.Null);
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), Is.Zero);
        var patrol = new SerializedObject(prefab.GetComponent<FieldEnemyPatrolAI2D>());
        Assert.That(patrol.FindProperty("leftPoint").objectReferenceValue, Is.Null);
        Assert.That(patrol.FindProperty("rightPoint").objectReferenceValue, Is.Null);
        Assert.That(prefab.transform.Find("PatrolLeft"), Is.Null);
        Assert.That(prefab.transform.Find("PatrolRight"), Is.Null);
    }

    [UnityTest]
    public IEnumerator ContactStartsCombatFromIsolatedScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
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
        var normal = hand.Hand.First(x => x.Kind == CombatCardKind.Normal);
        var flow = entry.FlowOrchestrator;
        Assert.That(flow.SubmitAttackDeclaration(new CombatAttackDeclaration(ally, enemy, normal.Skill, normal.RuntimeId), session.ExchangeState.Version), Is.True);
        Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2));
        int usedClashCards = 0;
        deadline = Time.realtimeSinceStartup + 15f;
        while (entry.ActiveSession != null && entry.ActiveStateMachine.Phase != Phase.ChainDecision && Time.realtimeSinceStartup < deadline)
        {
            var token = flow.PendingDecisionRequest;
            if (token?.Kind == CombatExchangeDecisionKind.ClashAttack)
            {
                var clash = hand.Hand.FirstOrDefault(x => x.Kind == CombatCardKind.Clash);
                if (clash != null) { Assert.That(flow.SubmitClashSeriesSkill(token, clash.Skill, clash.RuntimeId), Is.True); usedClashCards++; }
            }
            yield return null;
        }
        Assert.That(entry.ActiveStateMachine.Phase, Is.EqualTo(Phase.ChainDecision), "Director did not complete the exchange.");
        Assert.That(usedClashCards, Is.GreaterThan(0));
        Assert.That(session.GetCombatState(ally).CurrentMp, Is.EqualTo(2 - usedClashCards));
        Assert.That(enemy.HP, Is.LessThan(14));
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
        completed = 0; result = null;
        var entry = Object.FindFirstObjectByType<CombatEntryPoint>(); entry.OnCombatEnded += OnCombatEnded;
        var group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None).Single(x => x.name == "TestEncounter_01");
        var trigger = group.GetComponentInChildren<CombatEncounterTrigger2D>();
        var player = Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None).Single(x => x.CompareTag("Player"));
        var enemyField = group.GetActiveEnemies().Single();
        var fixedStep = Time.fixedDeltaTime;
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
