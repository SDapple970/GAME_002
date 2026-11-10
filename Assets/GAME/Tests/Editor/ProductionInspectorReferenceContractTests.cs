using System.Linq;
using GAME.Title;
using Game.Combat.Core;
using Game.Combat.Effects;
using Game.Combat.Integration;
using Game.Combat.UI;
using Game.Core;
using Game.Player;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.Tests.Integration
{
    public sealed class ProductionInspectorReferenceContractTests
    {
        private const string Title = "Assets/GAME/Scenes/TitleScene.unity";
        private const string Dungeon = "Assets/GAME/Scenes/Dungeon_1_Production.unity";

        [TearDown]
        public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TestCase(Title)]
        [TestCase(Dungeon)]
        public void InputAndRouter_HaveAuthoredOrBootstrapOwners(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            var installers = All<GameInputInstaller>(scene);
            Assert.That(installers.Length, Is.LessThanOrEqualTo(1), path + ": duplicate GameInputInstaller");
            if (installers.Length == 0)
            {
                var bootstrap = One<RuntimeBootstrapper>(scene);
                Assert.That(Property(bootstrap, "createMissingCoreServices").boolValue, Is.True, PathOf(bootstrap));
            }
            var router = One<UIScreenRouter>(scene);
            Candidate(router, "uiRoot", One<GameUIRootController>(scene));
            var states = All<GameStateMachine>(scene);
            Assert.That(states.Length, Is.LessThanOrEqualTo(1), path + ": duplicate GameStateMachine");
            if (states.Length == 1)
                Candidate(router, "stateMachine", states[0]);
            else
            {
                Assert.That(Property(router, "stateMachine").objectReferenceValue, Is.Null, PathOf(router));
                Assert.That(Property(One<RuntimeBootstrapper>(scene), "createMissingCoreServices").boolValue, Is.True, path + ": GameStateMachine bootstrap disabled");
                TestContext.WriteLine(PathOf(router) + ".stateMachine Bootstrap 생성 경로 (런타임 미검증)");
            }
            TestContext.WriteLine(path + ": " + PathOf(router) + " Input/Router 후보 확인");
        }

        [Test]
        public void InputAsset_ContainsInstallerGameplayActions()
        {
            const string path = "Assets/GAME/Scripts/Input/inputactions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            Assert.That(asset, Is.Not.Null, path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("5b2eeede448ec3f439fc8dfccc07d8c2"));
            var gameplay = asset.FindActionMap("Gameplay");
            Assert.That(gameplay, Is.Not.Null, path + ": Gameplay");
            foreach (string name in new[] { "Move", "Jump", "Attack", "Parry", "Interact", "Pause" })
                Assert.That(gameplay.FindAction(name), Is.Not.Null, path + ": Gameplay/" + name);
        }

        [Test]
        public void Title_RequiredTransitionReferencesAndDestination()
        {
            Scene scene = EditorSceneManager.OpenScene(Title);
            var title = One<TitleSceneController>(scene);
            foreach (string field in new[] { "startButton", "paperClickButton", "titleGroup", "requestPaperRoot", "requestPaperCanvasGroup", "npcPaperCanvasGroup", "monsterPaperCanvasGroup", "stampCanvasGroup", "fadeCanvasGroup" })
                Required(title, field);
            var animator = Property(title, "titleSceneAnimator").objectReferenceValue;
            Assert.That(animator != null || title.GetComponent<TitleSceneAnimator>() != null, Is.True, PathOf(title) + ".titleSceneAnimator");
            string destination = Property(title, "dungeonSceneName").stringValue;
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == destination), Is.True, PathOf(title) + ".dungeonSceneName");
            Required(One<GameUIRootController>(scene), "titleRoot");
        }

        [Test]
        public void Dungeon_PlayerAndCombatReferenceCandidates()
        {
            Scene scene = EditorSceneManager.OpenScene(Dungeon);
            var player = One<PlayerInputController>(scene);
            LocalCandidate<PlayerMotor2D_New>(player, "motor");
            LocalCandidate<PlayerFieldAttackController>(player, "fieldAttackController");
            var entry = One<CombatEntryPoint>(scene);
            var director = One<CombatDirector>(scene);
            var camera = One<CombatCameraController>(scene);
            Candidate(director, "entryPoint", entry);
            Candidate(director, "cameraController", camera);
            var binder = One<CombatRewardUIBinder>(scene);
            Candidate(binder, "entryPoint", entry);
            Candidate(binder, "rewardPanel", One<RewardUIPanel>(scene));
            Candidate(binder, "rewardService", One<Game.Reward.RewardService>(scene));

            var target = Property(camera, "targetCamera").objectReferenceValue as Camera;
            var legacy = Property(camera, "mainCamera").objectReferenceValue as Component;
            if (target == null && legacy != null) target = legacy.GetComponent<Camera>();
            if (target == null) target = camera.GetComponent<Camera>();
            if (target == null) target = All<Camera>(scene).SingleOrDefault(c => c.CompareTag("MainCamera"));
            Assert.That(target, Is.Not.Null, PathOf(camera) + ".targetCamera: 조회 후보 없음");
            var follow = Property(camera, "explorationCameraFollow").objectReferenceValue;
            TestContext.WriteLine(PathOf(camera) + ".explorationCameraFollow=" + (follow != null ? follow.name : legacy != null ? "mainCamera 조회" : "선택적 None"));

            var fieldLock = One<CombatFieldLock>(scene);
            foreach (string field in new[] { "behavioursToDisable", "freezeBodies2D", "disableColliders2D", "gameObjectsToDisable" })
            {
                var list = Property(fieldLock, field);
                for (int i = 0; i < list.arraySize; i++)
                    Assert.That(list.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, PathOf(fieldLock) + "." + field + "[" + i + "]");
                TestContext.WriteLine(PathOf(fieldLock) + "." + field + " count=" + list.arraySize);
            }
        }

        [TestCase(Title)]
        [TestCase(Dungeon)]
        public void UIInputModule_ResolvesConfiguredActions(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            var module = One<InputSystemUIInputModule>(scene);
            var asset = module.actionsAsset;
            Assert.That(asset, Is.Not.Null, PathOf(module) + ".actionsAsset");
            var fields = new[] { ("point", module.point, "Point", true), ("move", module.move, "Navigate", true),
                ("submit", module.submit, "Submit", true), ("cancel", module.cancel, "Cancel", true),
                ("leftClick", module.leftClick, "Click", true), ("middleClick", module.middleClick, "MiddleClick", false),
                ("rightClick", module.rightClick, "RightClick", false), ("scrollWheel", module.scrollWheel, "ScrollWheel", false),
                ("trackedDevicePosition", module.trackedDevicePosition, "TrackedDevicePosition", false),
                ("trackedDeviceOrientation", module.trackedDeviceOrientation, "TrackedDeviceOrientation", false) };
            foreach (var (field, reference, expected, required) in fields)
            {
                if (reference == null) { Assert.That(required, Is.False, PathOf(module) + "." + field); continue; }
                var action = reference.action;
                Assert.That(action, Is.Not.Null, PathOf(module) + "." + field);
                Assert.That(action.name, Is.EqualTo(expected), PathOf(module) + "." + field);
                Assert.That(action.actionMap.name, Is.EqualTo("UI"), PathOf(module) + "." + field);
                Assert.That(action.actionMap.asset, Is.SameAs(asset), PathOf(module) + "." + field);
                TestContext.WriteLine(path + " " + PathOf(module) + "." + field + " → " + AssetDatabase.GetAssetPath(asset) + "/UI/" + action.name);
            }
        }

        [TestCase(Title)]
        [TestCase(Dungeon)]
        public void InputOwners_ReportSceneComponentsAndReferences(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            foreach (var component in All<MonoBehaviour>(scene).Where(c => c != null))
            {
                bool owner = component is GameInputInstaller || component is PlayerInputController ||
                    component is OverworldInputAdapter || component is OverworldInputBridge || component is InputSystemUIInputModule;
                var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (!(value is InputActionReference) && !(value is InputActionAsset)) continue;
                    owner = true;
                    var reference = value as InputActionReference;
                    TestContext.WriteLine(path + " " + PathOf(component) + "." + property.propertyPath + " → " +
                        (reference != null && reference.action != null ? reference.action.actionMap.name + "/" + reference.action.name : AssetDatabase.GetAssetPath(value)));
                }
                if (owner) TestContext.WriteLine(path + " " + PathOf(component) + " enabled=" + component.enabled + " active=" + component.gameObject.activeInHierarchy + " [Production 씬; 동시 입력은 미검증]");
            }
        }

        [Test]
        public void Dungeon_FinalSceneUIRoots_AreConnected()
        {
            Scene scene = EditorSceneManager.OpenScene(Dungeon);
            var root = One<GameUIRootController>(scene);
            foreach (string field in new[] { "fieldRoot", "dialogueRoot", "choiceRoot", "combatRoot", "rewardRoot", "pauseRoot", "loadingRoot", "characterSkillRoot" })
            {
                var target = Property(root, field).objectReferenceValue as GameObject;
                Assert.That(target, Is.Not.Null, PathOf(root) + "." + field);
                Assert.That(target.scene, Is.EqualTo(scene), PathOf(root) + "." + field);
                TestContext.WriteLine(PathOf(root) + "." + field + " → " + PathOf(target.transform));
            }
            Assert.That(root.ValidateRootGraph(false), Is.True, PathOf(root));
        }

        [Test]
        public void Dungeon_CameraExplicitReferences_PointToMainCameraAndFollow()
        {
            Scene scene = EditorSceneManager.OpenScene(Dungeon);
            var controller = One<CombatCameraController>(scene);
            var target = Property(controller, "targetCamera").objectReferenceValue as Camera;
            var follow = Property(controller, "explorationCameraFollow").objectReferenceValue as MonoBehaviour;
            Assert.That(target, Is.Not.Null, PathOf(controller) + ".targetCamera");
            Assert.That(follow, Is.Not.Null, PathOf(controller) + ".explorationCameraFollow");
            Assert.That(follow, Is.TypeOf<Game.CameraSys.CameraFollow2D>(), PathOf(controller) + ".explorationCameraFollow");
            Assert.That(target.gameObject.scene, Is.EqualTo(scene));
            Assert.That(follow.gameObject, Is.SameAs(target.gameObject));
            Assert.That(target.CompareTag("MainCamera"), Is.True);
            var legacy = Property(controller, "mainCamera").objectReferenceValue as Component;
            if (legacy != null) Assert.That(legacy.gameObject, Is.SameAs(target.gameObject));
            TestContext.WriteLine(PathOf(controller) + ": target=" + PathOf(target) + ", follow=" + PathOf(follow) + ", mainCamera=" + (legacy == null ? "None (허용)" : PathOf(legacy)));
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static T One<T>(Scene scene) where T : Component { var items = All<T>(scene); Assert.That(items, Has.Length.EqualTo(1), scene.path + ": " + typeof(T).Name); return items[0]; }
        private static SerializedProperty Property(Component owner, string name) { var value = new SerializedObject(owner).FindProperty(name); Assert.That(value, Is.Not.Null, PathOf(owner) + "." + name); return value; }
        private static void Required(Component owner, string name) { Assert.That(Property(owner, name).objectReferenceValue, Is.Not.Null, PathOf(owner) + "." + name); TestContext.WriteLine(PathOf(owner) + "." + name + " 할당됨"); }
        private static void Candidate(Component owner, string name, Component expected) { var assigned = Property(owner, name).objectReferenceValue; if (assigned != null) Assert.That(assigned, Is.SameAs(expected), PathOf(owner) + "." + name); TestContext.WriteLine(PathOf(owner) + "." + name + (assigned == null ? " 유일 후보 " : " 할당 ") + PathOf(expected)); }
        private static void LocalCandidate<T>(Component owner, string name) where T : Component { var assigned = Property(owner, name).objectReferenceValue; if (assigned == null) assigned = owner.GetComponent<T>(); Assert.That(assigned, Is.Not.Null, PathOf(owner) + "." + name); TestContext.WriteLine(PathOf(owner) + "." + name + " 후보 확인"); }
        private static string PathOf(Component value) { string path = value.name; for (Transform t = value.transform.parent; t != null; t = t.parent) path = t.name + "/" + path; return path + " (" + value.GetType().Name + ")"; }
    }
}
