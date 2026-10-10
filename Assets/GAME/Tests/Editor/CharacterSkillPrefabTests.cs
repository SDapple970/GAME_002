using System.IO;
using System.Linq;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.UI
{
    public sealed class CharacterSkillPrefabTests
    {
        [Test]
        public void SavedPanel_HasCompleteSerializedReferencesAndReusableRows()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCharacterSkillUISetup.PanelPath);
            Assert.DoesNotThrow(() => ProductionCharacterSkillUISetup.ValidatePanel(prefab));
            Assert.That(prefab.activeSelf, Is.False);
            Assert.That(prefab.GetComponentsInChildren<ScrollRect>(true).Length, Is.EqualTo(4));
            Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).All(text => !text.raycastTarget), Is.True);
            Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).All(text => text.font != null), Is.True);
            CharacterSkillPanelView view = prefab.GetComponent<CharacterSkillPanelView>();
            Assert.That(view.CloseButton.onClick.GetPersistentTarget(0), Is.EqualTo(view));
            Assert.That(view.CloseButton.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(CharacterSkillPanelView.RequestClose)));
            foreach (string path in new[] { ProductionCharacterSkillUISetup.PanelPath, ProductionCharacterSkillUISetup.RowPath,
                ProductionCharacterSkillUISetup.CharacterButtonPath, ProductionCharacterSkillUISetup.EntryButtonPath })
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, path);
        }

        [Test]
        public void RepeatedPrefabSetup_DoesNotOverwriteExistingCustomization()
        {
            string[] paths = { ProductionCharacterSkillUISetup.PanelPath, ProductionCharacterSkillUISetup.RowPath,
                ProductionCharacterSkillUISetup.CharacterButtonPath, ProductionCharacterSkillUISetup.EntryButtonPath };
            string[] before = paths.Select(File.ReadAllText).ToArray();
            ProductionCharacterSkillUISetup.EnsurePrefabs();
            Assert.That(paths.Select(File.ReadAllText), Is.EqualTo(before));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        public void ReferenceCanvasAndAnchors_KeepPanelWithinSupportedAspectRatio(int width, int height)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCharacterSkillUISetup.PanelPath);
            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            RectTransform panel = (RectTransform)prefab.transform.Find("Panel");
            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(panel.anchorMax, Is.EqualTo(Vector2.one));
            float scale = width / 1920f;
            Assert.That(panel.offsetMin.x * scale, Is.GreaterThan(0).And.LessThan(width / 2));
            Assert.That(-panel.offsetMax.y * scale, Is.GreaterThan(0).And.LessThan(height / 2));
        }
    }
}
