using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.NonCombat.Progress;
using Game.Player;
using Game.Quest;
using Game.Reward;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Integration
{
    public sealed class ProductionCombatQuestRewardIntegrationTests
    {
        private static int expApplied, completions, questCompleted;
        private static CombatResult completed;
        private static void OnExperience(ExperienceApplyResult result) => expApplied += result.AppliedExperience;
        private static void OnCombat(CombatResult result) { completed = result; completions++; }
        private static void OnQuest(string id) => questCompleted++;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ProductionEncounter_VictoryGrantsAndAdvancesOnce_ThenReturnsToExploration()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/TitleScene.unity", OpenSceneMode.Single);
            // Avoid even a save-existence check against the user's normal Player data.
            GAME.Title.TitleSceneController title = Object.FindFirstObjectByType<GAME.Title.TitleSceneController>();
            Assert.That(title, Is.Not.Null);
            title.enabled = false;
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(SaveLoadService.Instance.TryResetForNewGame(out string message), Is.True, message);
            SceneFlowController.Instance.LoadScene("Dungeon_1_Production");
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != "Dungeon_1_Production" || GameStateMachine.Instance.Current != GameState.Exploration ||
                   CharacterProgressionService.Instance == null || !SceneFlowController.Instance.CanRestartDungeon ||
                   Object.FindFirstObjectByType<QuestRuntime>() == null ||
                   !Object.FindFirstObjectByType<QuestRuntime>().IsQuestActive("ch01.find-first-npc")) && Time.realtimeSinceStartup < deadline)
                yield return null;
            QuestRuntime quest = Object.FindFirstObjectByType<QuestRuntime>();
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.IsQuestActive("ch01.find-first-npc"), Is.True);
            CombatEncounterGroup group = Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None)
                .Single(item => item != null && item.EncounterId == "dungeon1.encounter.01");
            CombatEncounterTrigger2D trigger = Object.FindObjectsByType<CombatEncounterTrigger2D>(FindObjectsSortMode.None)
                .Single(item => item != null && item.EncounterId == "dungeon1.encounter.01");
            Assert.That(new SerializedObject(trigger).FindProperty("encounterGroup").objectReferenceValue, Is.SameAs(group));
            CombatEntryPoint entry = Object.FindFirstObjectByType<CombatEntryPoint>();
            Assert.That(entry, Is.Not.Null);
            PlayerInputController player = Object.FindFirstObjectByType<PlayerInputController>();
            Assert.That(player, Is.Not.Null);
            Collider2D collider = player.GetComponentInChildren<Collider2D>();
            Assert.That(collider, Is.Not.Null);
            CurrencyWallet wallet = CurrencyWallet.Instance;
            Assert.That(wallet, Is.Not.Null);
            int goldBefore = wallet.Gold;
            expApplied = 0;
            Assert.That(CharacterProgressionService.Instance, Is.Not.Null);
            CharacterProgressionService.Instance.ProgressionChanged += OnExperience;
            completed = null;
            completions = 0;
            entry.OnCombatEnded += OnCombat;
            typeof(CombatEncounterTrigger2D).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(trigger, new object[] { collider });
            deadline = Time.realtimeSinceStartup + 5f;
            while (entry.ActiveSession == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(entry.ActiveSession, Is.Not.Null);
            CombatSession session = entry.ActiveSession;
            foreach (ICombatant enemy in session.Enemies) session.GetCombatState(enemy).ApplyDamage(enemy.HP);
            entry.ActiveStateMachine.ForceExit(CombatEndReason.Victory);
            yield return null;
            Assert.That(completed, Is.Not.Null);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(completed.EndReason, Is.EqualTo(CombatEndReason.Victory));
            Assert.That(wallet.Gold, Is.EqualTo(goldBefore + 50));
            Assert.That(expApplied, Is.EqualTo(150));
            Assert.That(quest.GetObjectiveProgress("ch01.find-first-npc", "clear_encounter_01"), Is.EqualTo(1));
            Assert.That(quest.GetObjectiveProgress("ch01.find-first-npc", "clear_encounter_02"), Is.Zero);
            Assert.That(quest.GetObjectiveProgress("ch01.find-first-npc", "clear_encounter_03"), Is.Zero);
            RewardUIPanel panel = Object.FindFirstObjectByType<RewardUIPanel>(FindObjectsInactive.Include);
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
            Assert.That(panel.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text), Does.Contain("Gold 50"));
            Assert.That(panel.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text), Does.Contain("EXP 150"));
            typeof(CombatEntryPoint).GetMethod("RaiseCombatEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(entry, new object[] { completed });
            Assert.That(wallet.Gold, Is.EqualTo(goldBefore + 50));
            Assert.That(expApplied, Is.EqualTo(150));
            Assert.That(quest.GetObjectiveProgress("ch01.find-first-npc", "clear_encounter_01"), Is.EqualTo(1));
            questCompleted = 0;
            quest.OnQuestCompleted += OnQuest;
            Assert.That(quest.ApplyEvent(new QuestEvent(QuestEventType.Talk, "ch01.find-first-npc", "talk_first_npc", 1, null, "test.talk", "dungeon1.npc.first-talk")), Is.True);
            Assert.That(quest.ApplyEvent(new QuestEvent(QuestEventType.ClearEncounter, "ch01.find-first-npc", "clear_encounter_02", 1, null, "test.02", "dungeon1.encounter.02")), Is.True);
            Assert.That(quest.ApplyEvent(new QuestEvent(QuestEventType.ClearEncounter, "ch01.find-first-npc", "clear_encounter_03", 1, null, "test.03", "dungeon1.encounter.03")), Is.True);
            Assert.That(quest.GetQuestStatus("ch01.find-first-npc"), Is.EqualTo(QuestStatus.Completed));
            Assert.That(questCompleted, Is.EqualTo(1));
            QuestCompletionFlow completionFlow = Object.FindFirstObjectByType<QuestCompletionFlow>();
            var pending = (System.Collections.Generic.Queue<string>)typeof(QuestCompletionFlow).GetField("_pendingQuestIds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(completionFlow);
            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(quest.ApplyEvent(new QuestEvent(QuestEventType.ClearEncounter, "ch01.find-first-npc", "clear_encounter_03", 1, null, "test.03", "dungeon1.encounter.03")), Is.False);
            Button close = (Button)new SerializedObject(panel).FindProperty("closeButton").objectReferenceValue;
            close.onClick.Invoke();
            close.onClick.Invoke();
            yield return null;
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Exploration));
            Assert.That(pending.Count, Is.Zero);
            Assert.That(wallet.Gold, Is.EqualTo(goldBefore + 50));
            Assert.That(questCompleted, Is.EqualTo(1));
            quest.OnQuestCompleted -= OnQuest;
            entry.OnCombatEnded -= OnCombat;
            CharacterProgressionService.Instance.ProgressionChanged -= OnExperience;
            yield return new ExitPlayMode();
        }
    }
}
