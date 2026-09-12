using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Combat.Core;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.CameraSys;
using Game.Daily;
using Game.Demo;
using Game.Input;
using Game.Interaction;
using Game.NonCombat.Inventory;
using Game.Player;
using Game.Quest;
using Game.Reward;
using Game.Story;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Game.EditorTools
{
    public static class DungeonOneProductionMigrationUtility
    {
        public const string TemplateScenePath = "Assets/GAME/Scenes/Dungeon_Template.unity";
        public const string LegacyScenePath = "Assets/GAME/Scenes/Dungeon 1.unity";
        public const string ProductionScenePath = "Assets/GAME/Scenes/Dungeon_1_Production.unity";

        public const int ExpectedSpriteRendererCount = 172;
        public const int ExpectedPhysicalColliderCount = 53;
        public const int ExpectedRepairedLegacySpriteCount = 40;

        private static readonly Vector3 ProductionStartPosition = new(-39.88f, -6.33f, 0f);

        private static readonly EnvironmentRoot[] EnvironmentRoots =
        {
            new("Dungeon1_Background1-1 1_0", "Architecture"),
            new("Dungeon1_Background1-3", "Architecture"),
            new("Dungeon1_background", "Architecture"),
            new("apartment_side_house_0", "Architecture"),
            new("apartment_side_house_inside_1-4_0", "Architecture"),
            new("apartment_basefakdald_0", "Architecture"),
            new("apartment_basefakdald_0 (1)", "Architecture"),
            new("apartment_atlas_sprite 1_6", "Props"),
            new("apartment_atlas_sprite 1_7", "Props"),
            new("apartment_atlas_sprite 1_7 (1)", "Props"),
            new("apartment_atlas_sprite 1_9", "Props"),
            new("ladder_0", "Props"),
            new("ladder_0 (1)", "Props"),
            new("ladder_0 (2)", "Props")
        };

        private static readonly string[] ValidationWorldObjects =
        {
            "CalendarValidationInteractionTarget",
            "MultiObjectiveValidationInteractionTarget",
            "MultiObjectiveValidationStoryTarget",
            "StoryValidationInteractionTarget",
            "ValidationInteractionTarget"
        };

        private static readonly string[] LegacySpriteRepairAssetPaths =
        {
            "Assets/GAME/Image/TEST/share_interactive_1.png",
            "Assets/GAME/Image/background/Dungeon1_Background1-1 1.png",
            "Assets/GAME/Image/background/apartment_basefakdald.png"
        };

        [MenuItem("GAME/Production Migration/Create Dungeon 1 Production Scene")]
        public static void CreateProductionScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionScenePath) != null)
            {
                throw new InvalidOperationException(
                    $"Production scene already exists at '{ProductionScenePath}'. Refusing to overwrite it.");
            }

            if (!AssetDatabase.CopyAsset(TemplateScenePath, ProductionScenePath))
                throw new InvalidOperationException($"Could not copy '{TemplateScenePath}' to '{ProductionScenePath}'.");

            AssetDatabase.ImportAsset(ProductionScenePath, ImportAssetOptions.ForceSynchronousImport);

            Scene productionScene = EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            RemoveTemplateValidationContent();
            PrepareProductionRoots(out Transform environmentRoot, out Transform collisionRoot);
            PositionProductionPlayerSpawnAndCamera();

            Scene legacyScene = EditorSceneManager.OpenScene(LegacyScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(productionScene);

            Transform environmentArchitecture = CreateIdentityChild(environmentRoot, "Architecture");
            Transform environmentProps = CreateIdentityChild(environmentRoot, "Props");
            Transform collisionArchitecture = CreateIdentityChild(collisionRoot, "Architecture");
            Transform collisionProps = CreateIdentityChild(collisionRoot, "Props");

            foreach (EnvironmentRoot item in EnvironmentRoots)
            {
                GameObject source = FindRoot(legacyScene, item.Name);
                Transform visualParent = item.Category == "Architecture" ? environmentArchitecture : environmentProps;
                Transform physicalParent = item.Category == "Architecture" ? collisionArchitecture : collisionProps;

                if (CloneVisualBranch(source.transform, visualParent) == null)
                    throw new InvalidOperationException($"Environment root '{item.Name}' contained no SpriteRenderer data.");

                ClonePhysicalCollisionBranch(source.transform, physicalParent);
            }

            EditorSceneManager.CloseScene(legacyScene, true);
            SceneManager.SetActiveScene(productionScene);
            RepairUnresolvedDestinationSprites(environmentRoot);
            EditorSceneManager.MarkSceneDirty(productionScene);

            ValidateProductionScene();

            if (!EditorSceneManager.SaveScene(productionScene, ProductionScenePath))
                throw new InvalidOperationException($"Unity could not save '{ProductionScenePath}'.");

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[DungeonOneProductionMigration] Created '{ProductionScenePath}' from the Production template with " +
                $"{ExpectedSpriteRendererCount} sprite renderers and {ExpectedPhysicalColliderCount} physical colliders.");
        }

        public static void CreateProductionSceneFromCommandLine()
        {
            CreateProductionScene();
        }

        [MenuItem("GAME/Production Migration/Validate Dungeon 1 Production Scene")]
        public static void ValidateProductionSceneFromMenu()
        {
            EditorSceneManager.OpenScene(ProductionScenePath, OpenSceneMode.Single);
            ValidateProductionScene();
            Debug.Log($"[DungeonOneProductionMigration] Validation passed for '{ProductionScenePath}'.");
        }

        public static void ValidateProductionScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProductionScenePath)
                throw new InvalidOperationException($"Expected active scene '{ProductionScenePath}', found '{scene.path}'.");

            GameObject[] sceneObjects = GetSceneObjects(scene);
            int missingScripts = sceneObjects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Require(missingScripts == 0, $"Missing Script count was {missingScripts}.");

            RequireCount<QuestRuntime>(1);
            RequireCount<QuestObjectiveTracker>(1);
            RequireCount<QuestCompletionFlow>(1);
            RequireCount<QuestCalendarIntegration>(1);
            RequireCount<CalendarService>(1);
            RequireCount<CombatEntryPoint>(1);
            RequireCount<CombatWorldLifecycleAdapter>(1);
            RequireCount<CombatRewardUIBinder>(1);
            RequireCount<CombatPlanningHUD>(1);
            RequireCount<GameUIRootController>(1);
            RequireCount<UIScreenRouter>(1);
            RequireCount<RewardUIPanel>(1);
            RequireCount<StoryEventRunner>(1);
            RequireCount<StoryProgressManager>(1);
            RequireCount<RewardService>(1);
            RequireCount<CurrencyWallet>(1);
            RequireCount<PlayerInputController>(1);
            RequireCount<InteractionController>(1);
            RequireCount<CameraFollow2D>(1);

            RequireCount<QuestManager>(0);
            RequireCount<Game.Mission.MissionManager>(0);
            RequireCount<DungeonObjectiveManager>(0);
            RequireCount<Game.Tutorial.TutorialQuestCombatBridge>(0);
            RequireCount<CombatEncounterGroup>(0);
            RequireCount<CombatEncounterTrigger2D>(0);
            RequireCount<CombatQuestObjectivePublisher>(0);
            RequireCount<InteractionQuestObjectivePublisher>(0);
            RequireCount<InteractableObject>(0);

            MonoBehaviour[] behaviours = FindSceneComponents<MonoBehaviour>();
            MonoBehaviour forbidden = behaviours.FirstOrDefault(component =>
            {
                if (component == null)
                    return false;

                Type type = component.GetType();
                string typeNamespace = type.Namespace ?? string.Empty;
                return typeNamespace.Contains(".Debugging", StringComparison.Ordinal) ||
                       typeNamespace.Contains(".Legacy", StringComparison.Ordinal) ||
                       typeNamespace.Contains(".DemoMission", StringComparison.Ordinal) ||
                       type.Name.Contains("Demo", StringComparison.Ordinal) ||
                       type.Name.Contains("Test", StringComparison.Ordinal);
            });
            Require(forbidden == null,
                $"Forbidden Demo/Debug/Test/Legacy component remained: {forbidden?.GetType().FullName} on {forbidden?.name}.");

            SpriteRenderer[] renderers = FindRequired("World/Environment")
                .GetComponentsInChildren<SpriteRenderer>(true);
            Require(renderers.Length == ExpectedSpriteRendererCount,
                $"Expected {ExpectedSpriteRendererCount} SpriteRenderers, found {renderers.Length}.");
            Require(renderers.All(renderer => renderer.sprite != null), "One or more migrated SpriteRenderers have no Sprite.");

            Collider2D[] colliders = FindRequired("World/Collision")
                .GetComponentsInChildren<Collider2D>(true);
            Require(colliders.Length == ExpectedPhysicalColliderCount,
                $"Expected {ExpectedPhysicalColliderCount} physical Collider2D components, found {colliders.Length}.");
            Require(colliders.All(collider => !collider.isTrigger), "A trigger collider leaked into the environment collision tree.");
            Require(colliders.OfType<BoxCollider2D>().Count() == 43,
                "Expected 43 migrated BoxCollider2D components.");
            Require(colliders.OfType<PolygonCollider2D>().Count() == 10,
                "Expected 10 migrated PolygonCollider2D components.");
            Require(FindRequired("World/Environment").GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "A system or interaction MonoBehaviour leaked into the environment tree.");
            Require(FindRequired("World/Collision").GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "A system or interaction MonoBehaviour leaked into the collision tree.");

            RequireCount<Grid>(0);
            RequireCount<Tilemap>(0);
            RequireCount<TilemapRenderer>(0);
            RequireCount<TilemapCollider2D>(0);
            RequireCount<CompositeCollider2D>(0);

            Camera[] mainCameras = FindSceneComponents<Camera>().Where(camera => camera.CompareTag("MainCamera")).ToArray();
            Require(mainCameras.Length == 1, $"Expected one Main Camera, found {mainCameras.Length}.");
            Require(FindSceneComponents<PlayerInputController>().Select(item => item.gameObject).Distinct().Count() == 1,
                "Expected one Production Player root.");
            Require(Vector3.Distance(FindRequired("Actors/Player").position, ProductionStartPosition) < 0.001f,
                "Production Player wrapper does not match the migrated start position.");
            Require(Vector3.Distance(FindRequired("World/SpawnPoints/PlayerSpawn").position, ProductionStartPosition) < 0.001f,
                "PlayerSpawn does not match the migrated start position.");

            QuestRuntime questRuntime = FindSceneComponents<QuestRuntime>().Single();
            SerializedProperty questDefinitions = new SerializedObject(questRuntime).FindProperty("questDefinitions");
            Require(questDefinitions != null && questDefinitions.arraySize == 0,
                "Dungeon 1 Production must not retain template validation quest content.");

            Require(FindSceneComponents<global::GameInputInstaller>().Length == 0,
                "The template runtime-bootstrap input path must not be duplicated by a scene-local GameInputInstaller.");
            MethodInfo bootstrapMethod = typeof(Game.Core.RuntimeBootstrapper).GetMethod(
                "AutoBootstrapLoadedScene",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(bootstrapMethod != null,
                "RuntimeBootstrapper no longer exposes its Production runtime input/bootstrap entry path.");

            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/CombatRuntime.prefab", 1);
            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/Player.prefab", 1);
            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/UI/ProductionDungeonUI.prefab", 1);
            ValidatePrefabInstanceCount("Assets/GAME/Prefabs/Interaction/ProductionNpcInteraction.prefab", 0);

            foreach (Component component in sceneObjects.SelectMany(item => item.GetComponents<Component>()))
                ValidateMissingObjectReferences(component);
        }

        private static void RemoveTemplateValidationContent()
        {
            RemoveAllChildren(FindRequired("Actors/NPCs"));
            RemoveAllChildren(FindRequired("World/Encounters"));

            Transform world = FindRequired("World");
            foreach (string objectName in ValidationWorldObjects)
            {
                Transform target = world.Find(objectName);
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
            }

            foreach (CombatQuestObjectivePublisher publisher in FindSceneComponents<CombatQuestObjectivePublisher>())
                UnityEngine.Object.DestroyImmediate(publisher);
            foreach (InteractionQuestObjectivePublisher publisher in FindSceneComponents<InteractionQuestObjectivePublisher>())
                UnityEngine.Object.DestroyImmediate(publisher);

            QuestRuntime questRuntime = FindSceneComponents<QuestRuntime>().Single();
            SerializedObject serializedRuntime = new(questRuntime);
            SerializedProperty questDefinitions = serializedRuntime.FindProperty("questDefinitions");
            questDefinitions.arraySize = 0;
            serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PrepareProductionRoots(out Transform environmentRoot, out Transform collisionRoot)
        {
            environmentRoot = FindRequired("World/Environment");
            collisionRoot = FindRequired("World/Collision");
            RemoveAllChildren(environmentRoot);
            RemoveAllChildren(collisionRoot);
            CreateIdentityChild(environmentRoot, "Dungeon1");
            CreateIdentityChild(collisionRoot, "Dungeon1");
            environmentRoot = environmentRoot.Find("Dungeon1");
            collisionRoot = collisionRoot.Find("Dungeon1");
        }

        private static void PositionProductionPlayerSpawnAndCamera()
        {
            Transform playerWrapper = FindRequired("Actors/Player");
            Transform playerRoot = FindRequired("Actors/Player/PlayerRoot");
            Transform spawn = FindRequired("World/SpawnPoints/PlayerSpawn");
            Transform mainCamera = FindRequired("Main Camera");

            playerWrapper.position = ProductionStartPosition;
            playerRoot.localPosition = Vector3.zero;
            spawn.position = ProductionStartPosition;
            mainCamera.position = new Vector3(ProductionStartPosition.x, ProductionStartPosition.y, mainCamera.position.z);
        }

        private static GameObject CloneVisualBranch(Transform source, Transform destinationParent)
        {
            if (!HasVisualData(source))
                return null;

            GameObject destination = CreateCleanClone(source, destinationParent);
            foreach (SpriteRenderer renderer in source.GetComponents<SpriteRenderer>())
                CopyComponent(renderer, destination);
            foreach (Transform child in source)
                CloneVisualBranch(child, destination.transform);
            return destination;
        }

        private static GameObject ClonePhysicalCollisionBranch(Transform source, Transform destinationParent)
        {
            if (!HasPhysicalCollisionData(source))
                return null;

            GameObject destination = CreateCleanClone(source, destinationParent);
            foreach (Collider2D collider in source.GetComponents<Collider2D>().Where(item => !item.isTrigger))
                CopyComponent(collider, destination);
            foreach (Transform child in source)
                ClonePhysicalCollisionBranch(child, destination.transform);
            return destination;
        }

        private static bool HasVisualData(Transform source)
        {
            if (source.GetComponent<SpriteRenderer>() != null)
                return true;
            foreach (Transform child in source)
            {
                if (HasVisualData(child))
                    return true;
            }
            return false;
        }

        private static bool HasPhysicalCollisionData(Transform source)
        {
            if (source.GetComponents<Collider2D>().Any(item => !item.isTrigger))
                return true;
            foreach (Transform child in source)
            {
                if (HasPhysicalCollisionData(child))
                    return true;
            }
            return false;
        }

        private static void RepairUnresolvedDestinationSprites(Transform environmentRoot)
        {
            SpriteRenderer[] unresolved = environmentRoot
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.sprite == null)
                .ToArray();
            if (unresolved.Length != ExpectedRepairedLegacySpriteCount)
            {
                throw new InvalidOperationException(
                    $"Expected {ExpectedRepairedLegacySpriteCount} stale migrated SpriteRenderer references, " +
                    $"found {unresolved.Length}.");
            }

            Dictionary<string, Sprite> spritesByName = LegacySpriteRepairAssetPaths
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Sprite>()
                .GroupBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count() == 1
                        ? group.Single()
                        : throw new InvalidOperationException($"Sprite repair name '{group.Key}' is ambiguous."),
                    StringComparer.Ordinal);

            foreach (SpriteRenderer renderer in unresolved)
            {
                Sprite replacement = FindReplacementSprite(renderer.transform, environmentRoot, spritesByName);
                if (replacement == null)
                {
                    throw new InvalidOperationException(
                        $"Could not repair the stale Sprite reference at '{GetHierarchyPath(renderer.transform)}'.");
                }

                renderer.sprite = replacement;
                EditorUtility.SetDirty(renderer);
            }

            Debug.Log(
                $"[DungeonOneProductionMigration] Repaired {unresolved.Length} stale legacy sub-sprite references " +
                "from existing Sprite assets; no asset was duplicated.");
        }

        private static Sprite FindReplacementSprite(
            Transform transform,
            Transform environmentRoot,
            IReadOnlyDictionary<string, Sprite> spritesByName)
        {
            while (transform != null && transform != environmentRoot)
            {
                string normalizedName = Regex.Replace(transform.name, @" \(\d+\)$", string.Empty);
                if (spritesByName.TryGetValue(normalizedName, out Sprite sprite))
                    return sprite;
                transform = transform.parent;
            }
            return null;
        }

        private static GameObject CreateCleanClone(Transform source, Transform destinationParent)
        {
            GameObject destination = new(source.name);
            destination.transform.SetParent(destinationParent, false);
            destination.transform.localPosition = source.localPosition;
            destination.transform.localRotation = source.localRotation;
            destination.transform.localScale = source.localScale;
            destination.layer = source.gameObject.layer;
            destination.tag = source.gameObject.tag;
            GameObjectUtility.SetStaticEditorFlags(
                destination,
                GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            destination.SetActive(source.gameObject.activeSelf);
            return destination;
        }

        private static void CopyComponent(Component source, GameObject destination)
        {
            if (!ComponentUtility.CopyComponent(source) || !ComponentUtility.PasteComponentAsNew(destination))
                throw new InvalidOperationException($"Failed to copy {source.GetType().Name} from '{source.name}'.");
        }

        private static Transform CreateIdentityChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects().Where(item => item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected one legacy root named '{name}', found {matches.Length}.");
            return matches[0];
        }

        private static Transform FindRequired(string path)
        {
            string[] segments = path.Split('/');
            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects()
                .SingleOrDefault(item => item.name == segments[0]);
            if (root == null)
                throw new InvalidOperationException($"Required scene path '{path}' was not found.");

            Transform current = root.transform;
            for (int i = 1; i < segments.Length; i++)
            {
                current = current.Find(segments[i]);
                if (current == null)
                    throw new InvalidOperationException($"Required scene path '{path}' was not found.");
            }
            return current;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> segments = new();
            while (transform != null)
            {
                segments.Add(transform.name);
                transform = transform.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static void RemoveAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static T[] FindSceneComponents<T>() where T : Component
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject[] GetSceneObjects(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
        }

        private static void RequireCount<T>(int expected) where T : Component
        {
            int count = FindSceneComponents<T>().Length;
            Require(count == expected, $"Expected {expected} {typeof(T).Name} component(s), found {count}.");
        }

        private static void ValidatePrefabInstanceCount(string prefabPath, int expected)
        {
            int count = GetSceneObjects(SceneManager.GetActiveScene())
                .Count(item => PrefabUtility.IsAnyPrefabInstanceRoot(item) &&
                               PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item) == prefabPath);
            Require(count == expected, $"Expected {expected} instance(s) of '{prefabPath}', found {count}.");
        }

        private static void ValidateMissingObjectReferences(Component component)
        {
            if (component == null)
                return;

            SerializedObject serialized = new(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue == null &&
                    iterator.objectReferenceInstanceIDValue != 0)
                {
                    throw new InvalidOperationException(
                        $"Missing object reference at {component.GetType().Name}.{iterator.propertyPath} on '{component.name}'.");
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct EnvironmentRoot
        {
            public EnvironmentRoot(string name, string category)
            {
                Name = name;
                Category = category;
            }

            public string Name { get; }
            public string Category { get; }
        }
    }
}
