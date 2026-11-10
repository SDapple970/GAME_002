using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Combat.Adapters;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.Model;
using Game.Core;
using Game.NonCombat.Inventory;
using Game.Player;
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
    public sealed class DungeonRestartIntegrationTests
    {
        private string _savePath;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_savePath) && File.Exists(_savePath)) File.Delete(_savePath);
            if (!UnityEditor.EditorApplication.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator RestartTwice_RestoresStartState_AndDoesNotWriteSave()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/TitleScene.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return EnterDungeon();
            Vector3 start = UnityEngine.Object.FindFirstObjectByType<PlayerInputController>().transform.position;
            var hpStart = UnityEngine.Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None)
                .OrderBy(x => x.name).Select(x => (x.name, x.HP)).ToArray();
            int gold = CurrencyWallet.Instance.Gold;
            int items = InventoryService.Instance.GetCount("b2.test.item");
            File.WriteAllText(_savePath, "untouched sentinel");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                UnityEngine.Object.FindFirstObjectByType<PlayerInputController>().transform.position += Vector3.right * 3;
                foreach (CombatHpComponent hp in UnityEngine.Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None)) hp.HP = 1;
                CurrencyWallet.Instance.SetGold(gold + 41);
                InventoryService.Instance.AddItem("b2.test.item", 2);
                CombatEncounterGroup encounter = UnityEngine.Object.FindFirstObjectByType<CombatEncounterGroup>();
                Assert.That(encounter, Is.Not.Null);
                string encounterName = encounter.name;
                typeof(CombatEncounterGroup).GetMethod("RestoreFromSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(encounter, new object[] { true });
                CombatEntryPoint entry = UnityEngine.Object.FindFirstObjectByType<CombatEntryPoint>();
                typeof(CombatEntryPoint).GetMethod("RaiseCombatEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(entry, new object[] { new CombatResult { EndReason = CombatEndReason.Defeat } });
                Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
                RewardUIPanel panel = UnityEngine.Object.FindFirstObjectByType<RewardUIPanel>(FindObjectsInactive.Include);
                Button restart = new SerializedObject(panel).FindProperty("restartButton").objectReferenceValue as Button;
                Assert.That(restart, Is.Not.Null);
                Assert.That(restart.interactable, Is.True);
                restart.onClick.Invoke();
                restart.onClick.Invoke();
                Assert.That(GameFlowController.Instance.TryRestartDungeonAfterDefeat(), Is.False);
                Assert.That(GameInputInstaller.Instance.Router.AllowsExplorationInput(), Is.False);
                yield return WaitForExploration();
                Assert.That(GameInputInstaller.Instance.Router.AllowsExplorationInput(), Is.True);
                Assert.That(UnityEngine.Object.FindFirstObjectByType<PlayerInputController>().transform.position, Is.EqualTo(start));
                Assert.That(CurrencyWallet.Instance.Gold, Is.EqualTo(gold));
                Assert.That(InventoryService.Instance.GetCount("b2.test.item"), Is.EqualTo(items));
                CombatEncounterGroup restoredEncounter = UnityEngine.Object.FindObjectsByType<CombatEncounterGroup>(FindObjectsSortMode.None)
                    .Single(item => item.name == encounterName);
                object lifecycle = typeof(CombatEncounterGroup).GetProperty("Lifecycle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(restoredEncounter);
                Assert.That(lifecycle.ToString(), Is.EqualTo("Idle"));
                var actual = UnityEngine.Object.FindObjectsByType<CombatHpComponent>(FindObjectsSortMode.None)
                    .OrderBy(x => x.name).Select(x => (x.name, x.HP)).ToArray();
                Assert.That(actual, Is.EqualTo(hpStart));
                Assert.That(File.ReadAllText(_savePath), Is.EqualTo("untouched sentinel"));
                Assert.That(SceneFlowController.Instance.CanRestartDungeon, Is.True);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ContinueSameScene_InvalidatesStartSnapshot()
        {
            EditorSceneManager.OpenScene("Assets/GAME/Scenes/TitleScene.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return EnterDungeon();
            SaveLoadService save = SaveLoadService.Instance;
            Transform player = UnityEngine.Object.FindFirstObjectByType<PlayerInputController>().transform;
            player.position += Vector3.right * 2.5f;
            Vector3 savedPosition = player.position;
            Assert.That(save.TrySave(out string message), Is.True, message);
            Assert.That(save.TryLoad(out message), Is.True, message);
            Assert.That(SceneFlowController.Instance.CanRestartDungeon, Is.False);
            SceneFlowController.Instance.LoadScene("TitleScene");
            float titleDeadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != "TitleScene" || GameStateMachine.Instance.Current != GameState.Title) &&
                Time.realtimeSinceStartup < titleDeadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TitleScene"));
            Assert.That(save.TryLoad(out message), Is.True, message);
            yield return WaitForExploration();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<PlayerInputController>().transform.position, Is.EqualTo(savedPosition));
            Assert.That(SceneFlowController.Instance.CanRestartDungeon, Is.False);
            CombatEntryPoint entry = UnityEngine.Object.FindFirstObjectByType<CombatEntryPoint>();
            Assert.That(entry, Is.Not.Null);
            typeof(CombatEntryPoint).GetMethod("RaiseCombatEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(entry, new object[] { new CombatResult { EndReason = CombatEndReason.Defeat } });
            RewardUIPanel panel = UnityEngine.Object.FindFirstObjectByType<RewardUIPanel>(FindObjectsInactive.Include);
            Assert.That(panel, Is.Not.Null);
            SerializedObject ui = new(panel);
            Button restart = ui.FindProperty("restartButton").objectReferenceValue as Button;
            Button title = ui.FindProperty("titleButton").objectReferenceValue as Button;
            Assert.That(restart, Is.Not.Null);
            Assert.That(title, Is.Not.Null);
            Assert.That(restart.interactable, Is.False);
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Reward));
            title.onClick.Invoke();
            title.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != "TitleScene" || GameStateMachine.Instance.Current != GameState.Title) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TitleScene"));
            Assert.That(GameStateMachine.Instance.Current, Is.EqualTo(GameState.Title));
            yield return new ExitPlayMode();
        }

        private IEnumerator EnterDungeon()
        {
            yield return null;
            _savePath = Path.Combine(Application.dataPath, "b2-test-" + Guid.NewGuid().ToString("N") + ".json");
            typeof(SaveLoadService).GetMethod("SetStoragePathForTests", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(SaveLoadService.Instance, new object[] { _savePath });
            SceneFlowController.Instance.LoadScene("Dungeon_1_Production");
            yield return WaitForExploration();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!SceneFlowController.Instance.CanRestartDungeon && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneFlowController.Instance.CanRestartDungeon, Is.True);
        }

        private static IEnumerator WaitForExploration()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != "Dungeon_1_Production" || GameStateMachine.Instance == null ||
                GameStateMachine.Instance.Current != GameState.Exploration) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Dungeon_1_Production"));
            Assert.That(GameStateMachine.Instance?.Current, Is.EqualTo(GameState.Exploration));
        }
    }
}
