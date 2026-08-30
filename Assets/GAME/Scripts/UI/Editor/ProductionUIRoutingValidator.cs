using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Interaction;
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
                InteractionPromptUI[] prefabPrompts = prefab.GetComponentsInChildren<InteractionPromptUI>(true);
                if (prefabRoots.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one GameUIRootController, found {prefabRoots.Length}.");
                if (prefabRouters.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one UIScreenRouter, found {prefabRouters.Length}.");
                if (prefabEvents.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one EventSystem, found {prefabEvents.Length}.");
                if (prefabPrompts.Length != 1) issues.Add($"[Production UI] {prefabPath}: expected one InteractionPromptUI, found {prefabPrompts.Length}.");
                if (prefabRoots.Length == 1 && (!prefabRoots[0].HasAllRequiredRoots || !prefabRoots[0].ValidateRootGraph(false)))
                    issues.Add($"[Production UI] {prefabPath}: global routed roots are unresolved or unsafe.");
                if (prefabRouters.Length == 1 && new SerializedObject(prefabRouters[0]).FindProperty("uiRoot").objectReferenceValue == null)
                    issues.Add($"[Production UI] {prefabPath}: UIScreenRouter has no explicit root facade.");
                RewardUIPanel panel = prefab.GetComponentInChildren<RewardUIPanel>(true);
                if (panel == null || new SerializedObject(panel).FindProperty("fieldRewardToast").objectReferenceValue == null)
                    issues.Add($"[Production UI] {prefabPath}: full Reward panel and FieldRewardToast are not explicitly separated.");
                if (prefabPrompts.Length == 1)
                    ValidateInteractionPrompt(prefabRoots.SingleOrDefault(), prefabPrompts[0], prefabPath, issues);
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

                if (path == "Assets/GAME/Scenes/Dungeon_Template.unity")
                {
                    InteractionPromptUI[] prompts = FindAll<InteractionPromptUI>(scene);
                    InteractionController controller = FindAll<InteractionController>(scene).SingleOrDefault();
                    InteractionPromptUI prompt = prompts.Length == 1 ? prompts[0] : null;
                    if (prompts.Length != 1)
                        issues.Add($"[Production UI] {path}: expected exactly one InteractionPromptUI, found {prompts.Length}.");
                    if (controller == null || new SerializedObject(controller).FindProperty("promptUI").objectReferenceValue != prompt)
                        issues.Add($"[Production UI] {path}: InteractionController has no explicit canonical InteractionPromptUI reference.");
                }
            }
            return issues;
        }

        [MenuItem("Tools/GAME/Apply Production Interaction Prompt Wiring")]
        public static void ApplyProductionInteractionPromptWiring()
        {
            const string prefabPath = "Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab";
            const string templatePath = "Assets/GAME/Scenes/Dungeon_Template.unity";

            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                EnsureInteractionPrompt(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(templatePath, OpenSceneMode.Single);
            InteractionController[] controllers = FindAll<InteractionController>(scene);
            InteractionPromptUI[] prompts = FindAll<InteractionPromptUI>(scene);
            if (controllers.Length != 1 || prompts.Length != 1)
                throw new System.InvalidOperationException(
                    $"Production prompt wiring requires exactly one controller and prompt; found {controllers.Length} and {prompts.Length}.");

            SerializedObject serializedController = new(controllers[0]);
            serializedController.FindProperty("promptUI").objectReferenceValue = prompts[0];
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
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

        private static InteractionPromptUI EnsureInteractionPrompt(GameObject owner)
        {
            GameUIRootController roots = owner.GetComponent<GameUIRootController>();
            if (roots == null)
                throw new System.InvalidOperationException("ProductionDungeonUI is missing GameUIRootController.");

            GameObject fieldRoot = new SerializedObject(roots).FindProperty("fieldRoot").objectReferenceValue as GameObject;
            if (fieldRoot == null)
                throw new System.InvalidOperationException("ProductionDungeonUI is missing its canonical FieldRoot reference.");

            InteractionPromptUI[] existing = owner.GetComponentsInChildren<InteractionPromptUI>(true);
            if (existing.Length > 1)
                throw new System.InvalidOperationException($"ProductionDungeonUI contains {existing.Length} InteractionPromptUI components.");

            GameObject host = EnsureChild(fieldRoot.transform, "InteractionPromptHost");
            InteractionPromptUI prompt = existing.SingleOrDefault() ?? host.AddComponent<InteractionPromptUI>();
            if (prompt.gameObject != host)
                throw new System.InvalidOperationException("Canonical InteractionPromptUI must be owned by InteractionPromptHost.");

            Canvas canvas = host.GetComponent<Canvas>();
            if (canvas == null) canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject displayRoot = EnsureChild(host.transform, "InteractionPromptRoot");
            RectTransform displayRect = (RectTransform)displayRoot.transform;
            displayRect.anchorMin = new Vector2(0.5f, 0f);
            displayRect.anchorMax = new Vector2(0.5f, 0f);
            displayRect.pivot = new Vector2(0.5f, 0f);
            displayRect.anchoredPosition = new Vector2(0f, 96f);
            displayRect.sizeDelta = new Vector2(440f, 60f);

            Image background = displayRoot.GetComponent<Image>();
            if (background == null) background = displayRoot.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = false;

            GameObject textObject = EnsureChild(displayRoot.transform, "PromptText");
            Text text = textObject.GetComponent<Text>();
            if (text == null) text = textObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/GAME/Fonts/DungGeunMo.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "F: 조사";
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);

            SerializedObject serializedPrompt = new(prompt);
            serializedPrompt.FindProperty("root").objectReferenceValue = displayRoot;
            serializedPrompt.FindProperty("messageText").objectReferenceValue = text;
            serializedPrompt.ApplyModifiedPropertiesWithoutUndo();
            host.SetActive(true);
            displayRoot.SetActive(false);
            return prompt;
        }

        private static void ValidateInteractionPrompt(
            GameUIRootController roots,
            InteractionPromptUI prompt,
            string path,
            List<string> issues)
        {
            if (roots == null || prompt == null)
                return;

            GameObject fieldRoot = new SerializedObject(roots).FindProperty("fieldRoot").objectReferenceValue as GameObject;
            SerializedObject serializedPrompt = new(prompt);
            GameObject displayRoot = serializedPrompt.FindProperty("root").objectReferenceValue as GameObject;
            Text messageText = serializedPrompt.FindProperty("messageText").objectReferenceValue as Text;
            if (fieldRoot == null || !prompt.transform.IsChildOf(fieldRoot.transform))
                issues.Add($"[Production UI] {path}: InteractionPromptUI is not below canonical FieldRoot.");
            if (displayRoot == null || displayRoot == prompt.gameObject || !displayRoot.transform.IsChildOf(prompt.transform))
                issues.Add($"[Production UI] {path}: InteractionPromptUI owner and display root are not safely separated.");
            if (messageText == null || messageText.raycastTarget)
                issues.Add($"[Production UI] {path}: InteractionPromptUI has no non-raycast UI.Text message target.");
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
