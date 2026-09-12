using System.Linq;
using System.Reflection;
using Game.Core;
using Game.EditorTools;
using NUnit.Framework;
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
    }
}
