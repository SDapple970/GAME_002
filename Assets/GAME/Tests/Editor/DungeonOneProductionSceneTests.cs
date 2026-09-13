using System.Linq;
using System.Reflection;
using Game.Core;
using Game.EditorTools;
using Game.Interaction;
using Game.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.Integration
{
    public sealed class DungeonOneProductionSceneTests
    {
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

        [Test]
        public void ProductionDungeonOne_IsCleanEnvironmentMigration()
        {
            Scene scene = EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.path, Is.EqualTo(DungeonOneProductionMigrationUtility.ProductionScenePath));
            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            string[] roots = scene.GetRootGameObjects().Select(item => item.name).ToArray();
            Assert.That(roots, Does.Contain("Runtime"));
            Assert.That(roots, Does.Contain("World"));
            Assert.That(roots, Does.Contain("Actors"));
            Assert.That(roots, Does.Contain("Main Camera"));
            Assert.That(roots, Does.Not.Contain("Systems"));
            Assert.That(roots, Does.Not.Contain("Mission"));
            Assert.That(roots, Does.Not.Contain("Enemies"));
            Assert.That(roots, Does.Not.Contain("UI"));
        }

        [Test]
        public void ProductionDungeonOne_HasOneCanonicalRepeatableNpcInteraction()
        {
            EditorSceneManager.OpenScene(
                DungeonOneProductionMigrationUtility.ProductionScenePath,
                OpenSceneMode.Single);

            Assert.DoesNotThrow(DungeonOneProductionMigrationUtility.ValidateProductionScene);

            InteractableObject[] interactables = Object.FindObjectsByType<InteractableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(interactables, Has.Length.EqualTo(1));

            InteractableObject npc = interactables[0];
            Assert.That(GetHierarchyPath(npc.transform), Is.EqualTo("Actors/NPCs/Dungeon1_FirstTalkNpc"));
            Assert.That(Vector3.Distance(npc.transform.position, new Vector3(187.105f, 7.698f, 0f)), Is.LessThan(0.001f));
            Assert.That(npc.PromptText, Is.EqualTo("F: 대화"));
            Assert.That(npc.InteractionId, Is.EqualTo("dungeon1.npc.first-talk"));
            Assert.That(npc.UsePolicy, Is.EqualTo(InteractionUsePolicy.Repeatable));
            Assert.That(npc.Events, Has.Count.EqualTo(1));
            Assert.That(npc.Events[0], Is.TypeOf<AcknowledgementInteractionEventSO>());
            Assert.That(npc.GetComponent<Collider2D>(), Is.Not.Null);
            Assert.That(npc.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(npc.GetComponent<InteractionController>(), Is.Null);
            Assert.That(npc.GetComponentInChildren<StoryInteractable2D>(true), Is.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npc.gameObject), Is.EqualTo(
                "Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab"));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
