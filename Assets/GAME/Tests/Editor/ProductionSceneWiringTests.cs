using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.Input;
using Game.NonCombat.Inventory;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.Story.Interaction;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class ProductionSceneWiringTests
    {
        private const string Dungeon = "Assets/GAME/Scenes/Dungeon 1.unity";
        private const string Title = "Assets/GAME/Scenes/TitleScene.unity";

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (System.Type type in typeof(SaveLoadService).Assembly.GetTypes())
            {
                FieldInfo instance = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                if (instance != null && typeof(Object).IsAssignableFrom(instance.FieldType))
                    instance.SetValue(null, null);
            }
        }

        [TestCase(Dungeon)]
        [TestCase(Title)]
        public void ProductionScene_HasNoMissingScripts(string path)
        {
            Open(path);
            int missing = AllGameObjects().Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Assert.That(missing, Is.Zero, path);
        }

        [Test]
        public void Dungeon_HasExactlyOneCanonicalOwnerPerSystem()
        {
            Open(Dungeon);
            AssertOne<GameStateMachine>();
            AssertOne<GameFlowController>();
            AssertOne<GameInputInstaller>();
            AssertOne<UIScreenRouter>();
            AssertOne<GameUIRootController>();
            AssertOne<CombatEntryPoint>();
            AssertOne<CombatWorldLifecycleAdapter>();
            AssertOne<CombatRewardUIBinder>();
            AssertOne<StoryEventRunner>();
            AssertOne<StoryProgressManager>();
            AssertOne<QuestRuntime>();
            AssertOne<QuestObjectiveTracker>();
            AssertOne<QuestCompletionFlow>();
            AssertOne<SaveLoadService>();
            AssertOne<CurrencyWallet>();
            AssertOne<InventoryService>();
            AssertOne<RewardService>();
        }

        [Test]
        public void Dungeon_CombatReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<CombatWorldLifecycleAdapter>("entryPoint", "fieldLock", "cameraController", "formationManager");
            AssertReferences<CombatRewardUIBinder>("entryPoint", "rewardPanel", "rewardService");
            AssertReferences<CombatUIRootController>("entryPoint", "combatHudRoot", "planningPanel", "planningHUD", "rewardCanvas");
            SerializedObject fieldLock = Serialized<CombatFieldLock>();
            Assert.That(fieldLock.FindProperty("behavioursToDisable").arraySize, Is.GreaterThanOrEqualTo(3));
            Assert.That(fieldLock.FindProperty("freezeBodies2D").arraySize, Is.GreaterThanOrEqualTo(1));
            Assert.That(fieldLock.FindProperty("disableColliders2D").arraySize, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Dungeon_NarrativeAndQuestReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<StoryEventRunner>("dialoguePanel", "timedChoiceDialoguePanel");
            foreach (StoryInteractionController controller in FindAll<StoryInteractionController>())
                Assert.That(Reference(controller, "runner"), Is.Not.Null, controller.name);
            AssertReferences<QuestObjectiveTracker>("questRuntime");
            AssertReferences<QuestCompletionFlow>("questRuntime", "rewardService");
            AssertReferences<DemoMissionRuntime>("questRuntime", "currentMission");
            AssertReferences<RescueNpcActor>("missionRuntime", "npcDefinition", "interactPromptRoot");
        }

        [Test]
        public void Dungeon_EncounterAndSpawnIdsAreStableAndUnique()
        {
            Open(Dungeon);
            string[] ids = FindAll<CombatEncounterTrigger2D>()
                .Select(component => new SerializedObject(component).FindProperty("encounterId").stringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            Assert.That(ids, Has.Length.EqualTo(3));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Is.EquivalentTo(new[] { "dungeon1.ghost.01", "dungeon1.ghost.02", "dungeon1.ghost.03" }));
            string[] spawns = FindAll<SceneSpawnPoint>().Select(point => point.SpawnPointId).ToArray();
            Assert.That(spawns, Has.None.Null.Or.Empty);
            Assert.That(spawns.Distinct().Count(), Is.EqualTo(spawns.Length));
        }

        [Test]
        public void Dungeon_SaveAndPlayerReferencesResolve()
        {
            Open(Dungeon);
            AssertReferences<SaveLoadService>("player");
            Assert.That(GameObject.Find("Player_new"), Is.Not.Null);
        }

        [Test]
        public void BuildSettingsContainCanonicalSceneTargets()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(enabled, Does.Contain(Title));
            Assert.That(enabled, Does.Contain(Dungeon));
        }

        private static void Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        private static IEnumerable<GameObject> AllGameObjects() =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(item => item.gameObject);

        private static T[] FindAll<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void AssertOne<T>() where T : Object => Assert.That(FindAll<T>(), Has.Length.EqualTo(1), typeof(T).Name);

        private static SerializedObject Serialized<T>() where T : Object
        {
            T value = FindAll<T>().Single();
            return new SerializedObject(value);
        }

        private static Object Reference(Object owner, string property) => new SerializedObject(owner).FindProperty(property)?.objectReferenceValue;

        private static void AssertReferences<T>(params string[] properties) where T : Object
        {
            T owner = FindAll<T>().Single();
            foreach (string property in properties)
                Assert.That(Reference(owner, property), Is.Not.Null, $"{typeof(T).Name}.{property}");
        }
    }
}
