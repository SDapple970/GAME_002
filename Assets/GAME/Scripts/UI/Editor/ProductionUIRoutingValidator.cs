using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Editor
{
    public static class ProductionUIRoutingValidator
    {
        private static readonly string[] ProductionScenes =
        {
            "Assets/GAME/Scenes/Dungeon 1.unity",
            "Assets/GAME/Scenes/Dungeon_Template.unity"
        };

        [MenuItem("Tools/GAME/Validate Production UI Routing")]
        public static void ValidateMenu()
        {
            IReadOnlyList<string> issues = ValidateProductionAssets();
            if (issues.Count == 0) Debug.Log("[ProductionUIRoutingValidator] Production UI routing is valid.");
            else foreach (string issue in issues) Debug.LogError(issue);
        }

        public static IReadOnlyList<string> ValidateProductionAssets()
        {
            List<string> issues = new();
            const string prefabPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) issues.Add($"[Production UI] Missing prefab: {prefabPath}");
            else
            {
                GameUIRootController[] prefabRoots = prefab.GetComponentsInChildren<GameUIRootController>(true);
                UIScreenRouter[] prefabRouters = prefab.GetComponentsInChildren<UIScreenRouter>(true);
                EventSystem[] prefabEvents = prefab.GetComponentsInChildren<EventSystem>(true);
                if (prefabRoots.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one GameUIRootController, found {prefabRoots.Length}.");
                if (prefabRouters.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one UIScreenRouter, found {prefabRouters.Length}.");
                if (prefabEvents.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one EventSystem, found {prefabEvents.Length}.");
                if (prefabRoots.Length == 1 && (!prefabRoots[0].HasAllRequiredRoots || !prefabRoots[0].ValidateRootGraph(false)))
                    issues.Add($"[Production UI] {prefabPath}: global routed roots are unresolved or unsafe.");
                if (prefabRouters.Length == 1 && new SerializedObject(prefabRouters[0]).FindProperty("uiRoot").objectReferenceValue == null)
                    issues.Add($"[Production UI] {prefabPath}: UIScreenRouter has no explicit root facade.");
                RewardUIPanel panel = prefab.GetComponentInChildren<RewardUIPanel>(true);
                if (panel == null || new SerializedObject(panel).FindProperty("fieldRewardToast").objectReferenceValue == null)
                    issues.Add($"[Production UI] {prefabPath}: full Reward panel and FieldRewardToast are not explicitly separated.");
            }
            foreach (string path in ProductionScenes)
            {
                if (!System.IO.File.Exists(path)) { issues.Add($"[Production UI] Missing scene: {path}"); continue; }
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ValidateCount<UIScreenRouter>(scene, path, issues);
                ValidateCount<GameUIRootController>(scene, path, issues);
                ValidateCount<EventSystem>(scene, path, issues);
                GameUIRootController roots = FindAll<GameUIRootController>(scene).SingleOrDefault();
                if (roots != null)
                {
                    if (!roots.HasAllRequiredRoots) issues.Add($"[Production UI] {path}: '{GetPath(roots.transform)}' has unresolved global roots.");
                    if (!roots.ValidateRootGraph(false)) issues.Add($"[Production UI] {path}: '{GetPath(roots.transform)}' has an unsafe or duplicate routed-root graph.");
                }
                UIScreenRouter router = FindAll<UIScreenRouter>(scene).SingleOrDefault();
                if (router != null && new SerializedObject(router).FindProperty("uiRoot").objectReferenceValue == null)
                    issues.Add($"[Production UI] {path}: '{GetPath(router.transform)}' has no explicit GameUIRootController reference.");
            }
            return issues;
        }

        // Explicit authoring command; never runs during player or scene load.
        [MenuItem("Tools/GAME/Apply Batch 7 Production UI Wiring")]
        public static void ApplyProductionWiring()
        {
            const string prefabPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Wire(prefab, true);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            foreach (string path in ProductionScenes)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GameUIRootController[] existing = FindAll<GameUIRootController>(scene);
                if (existing.Length == 1) Wire(existing[0].gameObject, false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Wire(GameObject owner, bool prefabAsset)
        {
            GameUIRootController roots = owner.GetComponent<GameUIRootController>();
            if (roots == null) roots = owner.AddComponent<GameUIRootController>();
            UIScreenRouter router = owner.GetComponent<UIScreenRouter>();
            if (router == null) router = owner.AddComponent<UIScreenRouter>();
            GameObject title = EnsureChild(owner.transform, "TitleRoot");
            GameObject field = EnsureChild(owner.transform, "FieldRoot");
            GameObject dialogue = FindNamed(owner, "DialogueRoot") ?? EnsureChild(owner.transform, "DialogueRoot");
            GameObject choice = FindNamed(owner, "ChoiceRoot") ?? EnsureChild(owner.transform, "ChoiceRoot");
            GameObject combat = FindNamed(owner, "CombatCanvas") ?? EnsureChild(owner.transform, "CombatRoot");
            GameObject reward = FindNamed(owner, "RewardCanvas") ?? FindNamed(owner, "RewardRoot") ?? EnsureChild(owner.transform, "RewardRoot");
            GameObject pause = EnsureChild(owner.transform, "PauseRoot");
            GameObject loading = EnsureChild(owner.transform, "LoadingRoot");

            if (!prefabAsset)
            {
                field = FindSceneComponentRoot<OverworldHUDRoot>(owner.scene) ?? field;
                dialogue = FindSceneNamed(owner.scene, "DemodialoguePanel") ?? FindSceneComponentRoot<Game.Story.UI.DialoguePanel>(owner.scene) ?? dialogue;
                choice = FindSceneNamed(owner.scene, "DemoChoicePanel") ?? FindSceneComponentRoot<Game.Dialogue.TimedChoiceDialoguePanel>(owner.scene) ?? choice;
                combat = FindSceneNamed(owner.scene, "CombatHUD") ?? FindSceneComponentRoot<Game.Combat.UI.CombatUIRootController>(owner.scene) ?? combat;
                reward = FindSceneNamed(owner.scene, "RewardUI") ?? FindSceneComponentRoot<RewardUIPanel>(owner.scene) ?? reward;
            }

            SerializedObject serializedRoots = new(roots);
            Assign(serializedRoots, "titleRoot", title); Assign(serializedRoots, "fieldRoot", field);
            Assign(serializedRoots, "dialogueRoot", dialogue); Assign(serializedRoots, "choiceRoot", choice);
            Assign(serializedRoots, "combatRoot", combat); Assign(serializedRoots, "rewardRoot", reward);
            Assign(serializedRoots, "pauseRoot", pause); Assign(serializedRoots, "loadingRoot", loading);
            serializedRoots.FindProperty("allowCompatibilityAutoBinding").boolValue = false;
            serializedRoots.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedRouter = new(router);
            serializedRouter.FindProperty("uiRoot").objectReferenceValue = roots;
            serializedRouter.ApplyModifiedPropertiesWithoutUndo();

            RewardUIPanel panel = owner.GetComponentInChildren<RewardUIPanel>(true);
            if (panel == null && !prefabAsset)
            {
                RewardUIPanel[] panels = FindAll<RewardUIPanel>(owner.scene);
                if (panels.Length == 1) panel = panels[0];
            }
            if (panel != null)
            {
                FieldRewardToast toast = EnsureToast(field.transform);
                SerializedObject serializedPanel = new(panel);
                serializedPanel.FindProperty("fieldRewardToast").objectReferenceValue = toast;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!prefabAsset)
                RemoveUnusedGeneratedRoots(owner.transform, new[] { title, field, dialogue, choice, combat, reward, pause, loading });

            title.SetActive(false); field.SetActive(true); dialogue.SetActive(false); choice.SetActive(false);
            combat.SetActive(false); reward.SetActive(false); pause.SetActive(false); loading.SetActive(false);
        }

        private static FieldRewardToast EnsureToast(Transform field)
        {
            GameObject toastRoot = EnsureChild(field, "FieldRewardToast");
            FieldRewardToast toast = toastRoot.GetComponent<FieldRewardToast>();
            if (toast == null) toast = toastRoot.AddComponent<FieldRewardToast>();
            Canvas canvas = toastRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = toastRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (toastRoot.GetComponent<CanvasScaler>() == null) toastRoot.AddComponent<CanvasScaler>();
            if (toastRoot.GetComponent<GraphicRaycaster>() == null) toastRoot.AddComponent<GraphicRaycaster>();
            GameObject labelObject = EnsureChild(toastRoot.transform, "Message");
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
            RectTransform rect = label.rectTransform; rect.anchorMin = new Vector2(0.25f, 0.8f); rect.anchorMax = new Vector2(0.75f, 0.95f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            SerializedObject serialized = new(toast);
            serialized.FindProperty("root").objectReferenceValue = toastRoot;
            serialized.FindProperty("messageText").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            toastRoot.SetActive(false);
            return toast;
        }

        private static void Assign(SerializedObject target, string property, GameObject value) => target.FindProperty(property).objectReferenceValue = value;
        private static GameObject FindNamed(GameObject owner, string name) => owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name)?.gameObject;
        private static GameObject EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject FindSceneNamed(UnityEngine.SceneManagement.Scene scene, string name)
        {
            Transform[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(item => item.name == name).ToArray();
            return matches.Length == 1 ? matches[0].gameObject : null;
        }

        private static GameObject FindSceneComponentRoot<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            T[] matches = FindAll<T>(scene);
            return matches.Length == 1 ? matches[0].gameObject : null;
        }

        private static void RemoveUnusedGeneratedRoots(Transform owner, GameObject[] used)
        {
            HashSet<GameObject> retained = new(used);
            string[] generated = { "FieldRoot", "DialogueRoot", "ChoiceRoot", "CombatRoot", "RewardRoot" };
            foreach (string name in generated)
            {
                Transform candidate = owner.Find(name);
                if (candidate != null && !retained.Contains(candidate.gameObject)) Object.DestroyImmediate(candidate.gameObject);
            }
        }

        private static void ValidateCount<T>(UnityEngine.SceneManagement.Scene scene, string path, List<string> issues) where T : Component
        {
            int count = FindAll<T>(scene).Length;
            if (count != 1) issues.Add($"[Production UI] {path}: expected exactly one {typeof(T).Name}, found {count}.");
        }

        private static T[] FindAll<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static string GetPath(Transform value)
        {
            string path = value.name;
            while (value.parent != null) { value = value.parent; path = value.name + "/" + path; }
            return path;
        }
    }
}
