#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class ProductionCombatAssetRestoration
    {
        private const string RowPath = "Assets/GAME/Image/ui/ui/CombatItemRowBackground.png";

        [MenuItem("GAME/Production UI/Install Combat Asset Restoration (2-D-2)")]
        public static void Install()
        {
            AssetDatabase.ImportAsset(RowPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(RowPath);
            if (importer == null) throw new InvalidOperationException("Separated item row is missing.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
            Sprite row = AssetDatabase.LoadAssetAtPath<Sprite>(RowPath);
            if (row == null) throw new InvalidOperationException("Item row sprite did not import.");

            GameObject root = PrefabUtility.LoadPrefabContents(ProductionCombatUILayoutSetup.PrefabPath);
            try
            {
                FinalCombatUIBinder binder = root.GetComponentInChildren<FinalCombatUIBinder>(true);
                if (binder == null) throw new InvalidOperationException("Canonical binder is missing.");
                SerializedObject fields = new(binder);
                Dictionary<string, UnityEngine.Object> before = new();
                SerializedProperty field = fields.GetIterator();
                while (field.NextVisible(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference)
                        before[field.propertyPath] = field.objectReferenceValue;
                Dictionary<Button, string> events = root.GetComponentsInChildren<Button>(true)
                    .ToDictionary(button => button, button => JsonUtility.ToJson(button.onClick));
                Transform overlay = binder.transform.Find("ItemSelectionOverlay");
                if (overlay == null) throw new InvalidOperationException("Item overlay is missing.");
                Transform existing = overlay.Find("ItemRowSource");
                Image image;
                if (existing == null)
                {
                    GameObject child = new("ItemRowSource", typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(overlay, false);
                    child.SetActive(false);
                    image = child.GetComponent<Image>();
                }
                else image = existing.GetComponent<Image>();
                image.sprite = row;
                image.raycastTarget = false;
                fields.Update();
                foreach (var pair in before)
                    if (fields.FindProperty(pair.Key).objectReferenceValue != pair.Value)
                        throw new InvalidOperationException("Existing binder reference changed: " + pair.Key);
                foreach (var pair in events)
                    if (JsonUtility.ToJson(pair.Key.onClick) != pair.Value)
                        throw new InvalidOperationException("UnityEvent changed: " + pair.Key.name);
                ProductionCombatUILayoutSetup.Validate(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, ProductionCombatUILayoutSetup.PrefabPath) == null)
                    throw new InvalidOperationException("Could not save restored prefab.");
                Debug.Log("[CombatUI-2D-2] Installed separated authored item row; existing bindings and events retained.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
