using System.IO;
using System.Linq;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.Tests.UI
{
    public sealed class CharacterSkillProductionWiringTests
    {
        [TearDown]
        public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void SavedProductionScene_HasSingleExistingOwnersAndConnectedNestedPrefab()
        {
            Scene scene = EditorSceneManager.OpenScene(ProductionCharacterSkillUISetup.ScenePath);
            Assert.DoesNotThrow(() => ProductionCharacterSkillUISetup.ValidateScene(scene));
            GameObject[] roots = scene.GetRootGameObjects();
            Assert.That(roots.SelectMany(go => go.GetComponentsInChildren<UIScreenRouter>(true)).Count(), Is.EqualTo(1));
            Assert.That(roots.SelectMany(go => go.GetComponentsInChildren<GameUIRootController>(true)).Count(), Is.EqualTo(1));
            Assert.That(roots.SelectMany(go => go.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
            CharacterSkillPanelView panel = roots.SelectMany(go => go.GetComponentsInChildren<CharacterSkillPanelView>(true)).Single();
            Assert.That(UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel), Is.EqualTo(ProductionCharacterSkillUISetup.PanelPath));
        }

        [Test]
        public void RepeatInstallation_DoesNotResaveSceneOrSharedUI()
        {
            string sharedPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
            string sceneBefore = File.ReadAllText(ProductionCharacterSkillUISetup.ScenePath);
            string sharedBefore = File.ReadAllText(sharedPath);
            EditorSceneManager.OpenScene(ProductionCharacterSkillUISetup.ScenePath);
            ProductionCharacterSkillUISetup.BuildAndConnect();
            Assert.That(File.ReadAllText(ProductionCharacterSkillUISetup.ScenePath), Is.EqualTo(sceneBefore));
            Assert.That(File.ReadAllText(sharedPath), Is.EqualTo(sharedBefore));
        }
    }
}
