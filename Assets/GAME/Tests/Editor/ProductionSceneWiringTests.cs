using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.DemoMission.Runtime;
using Game.Input;
using Game.Interaction;
using Game.Interaction.Editor;
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
        private const string DungeonTemplate = "Assets/GAME/Scenes/Dungeon_Template.unity";
        private const string TestingDungeonTemplate = "Assets/GAME/Scenes/Testing_Dungeon_Template.unity";
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

        [Test]
        public void DungeonTemplate_IsCanonicalSingleOwnerProductionSource()
        {
            Assert.That(System.IO.File.Exists(DungeonTemplate), Is.True);
            Open(DungeonTemplate);
            Assert.That(FindAll<CombatEntryPoint>(), Has.Length.EqualTo(1));
            MonoBehaviour[] components = FindAll<MonoBehaviour>();
            Assert.That(components.Any(component => component != null && component.gameObject.name == "CombatRuntime"), Is.True);
            Assert.That(components.Any(component =>
                component != null &&
                (component.GetType().Name == "CombatDemoFlowController" ||
                 component.GetType().Name == "SeamlessBattleManager" ||
                 component.GetType().Name == "BattleTrigger2D" ||
                 component.GetType().Namespace?.Contains("Debugging") == true ||
                 component.GetType().Namespace?.Contains("Legacy") == true)), Is.False);
        }

        [Test]
        public void DungeonTemplate_InteractionIdsAndRunnerOwnershipAreValid()
        {
            Open(DungeonTemplate);
            Assert.That(FindAll<InteractionRunner>(), Has.Length.LessThanOrEqualTo(1));

            InteractableObject[] persistent = FindAll<InteractableObject>()
                .Where(item => item.UsePolicy == InteractionUsePolicy.PersistentOnce)
                .ToArray();
            Assert.That(persistent.Select(item => item.InteractionId), Has.None.Null.Or.Empty);
            Assert.That(
                persistent.Select(item => item.InteractionId).Distinct(System.StringComparer.Ordinal).Count(),
                Is.EqualTo(persistent.Length));
        }

        [Test]
        public void ProductionBuildScenes_PassInteractionAuthoringValidation()
        {
            Assert.That(ProductionInteractionValidator.ValidateBuildScenes(), Is.Empty);
        }

        [Test]
        public void BuildSettings_ExcludeTestDeletedAndRecoveryScenes()
        {
            Assert.That(System.IO.File.Exists(TestingDungeonTemplate), Is.True);
            string[] paths = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();
            Assert.That(paths, Does.Not.Contain(TestingDungeonTemplate));
            Assert.That(paths.Any(path => path.Contains("Assets/_Recovery/")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 2.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 3.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/Dungeon 5.unity")), Is.False);
            Assert.That(paths.Any(path => path.EndsWith("/TutorialScene.unity")), Is.False);
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
